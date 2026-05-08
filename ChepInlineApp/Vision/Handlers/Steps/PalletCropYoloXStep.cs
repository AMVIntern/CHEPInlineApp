using ChepInlineApp.Helpers;
using ChepInlineApp.Vision.Handlers.Core;
using ChepInlineApp.Vision.Handlers.Interfaces;
using ChepInlineApp.Vision.Results;
using ChepInlineApp.YOLOX.Core;
using ChepInlineApp.YOLOX.Models;
using ChepInlineApp.YOLOX.Utils;
using HalconDotNet;
using OpenCvSharp;

namespace ChepInlineApp.Vision.Handlers.Steps
{
    /// <summary>
    /// Two-stage YOLOX inspection step:
    ///   S1 – Pallet detection (crop)
    ///   S2 – Defect detection on cropped region
    /// Annotations are drawn on the full image with coordinates remapped from crop space.
    /// </summary>
    public class PalletCropYoloXStep : IInspectionStep, IDisposable
    {
        public string Name { get; }

        private static readonly string[] ClassLabels = { "Cracks", "Staples", "Contamination" };

        private static readonly Scalar[] ClassColours =
        {
            new Scalar(0, 255, 255),  // Cracks        → yellow
            new Scalar(0, 165, 255),  // Staples        → orange
            new Scalar(0, 0, 255),    // Contamination  → red
        };

        private const int StaplesMaxAllowed = 7;

        private readonly YoloXModel _palletModel;  // S1
        private readonly YoloXModel _defectModel;  // S2

        public PalletCropYoloXStep(string name, string palletModelPath, string defectModelPath)
        {
            Name = name;
            _palletModel = new YoloXModel(palletModelPath, confThreshold: 0.5f);
            _defectModel = new YoloXModel(defectModelPath, confThreshold: 0.4f);
        }

        public Task RunAsync(InspectionContext context)
        {
            return Task.Run(() =>
            {
                AppLogger.Info($"[{Name}] PalletCropYoloXStep.RunAsync START - Camera: {context.CameraId}");

                HImage hImage = context.Image ?? throw new ArgumentNullException(nameof(context.Image));
                using var mat = ImageUtils.HImageToMatBGR(hImage);

                // ── S1: Pallet detection ────────────────────────────────────────
                AppLogger.Info($"[{Name}] Running S1 pallet detection");
                var palletPreds = _palletModel.Infer(mat);

                // Pick highest-confidence class-0 (pallet) detection
                var bestPallet = palletPreds
                    .Where(p => p.Label == 0)
                    .OrderByDescending(p => p.Probability)
                    .FirstOrDefault();

                int cropX, cropY, cropW, cropH;
                Mat croppedMat;
                bool palletFound;

                if (bestPallet != null)
                {
                    // Clamp crop region to image bounds
                    cropX = Math.Max(0, (int)bestPallet.Rect.X);
                    cropY = Math.Max(0, (int)bestPallet.Rect.Y);
                    cropW = Math.Min((int)bestPallet.Rect.Width, mat.Width - cropX);
                    cropH = Math.Min((int)bestPallet.Rect.Height, mat.Height - cropY);

                    AppLogger.Info($"[{Name}] S1 pallet found: x={cropX} y={cropY} w={cropW} h={cropH} conf={bestPallet.Probability:F4}");

                    croppedMat = new Mat(mat, new Rect(cropX, cropY, cropW, cropH));
                    palletFound = true;
                }
                else
                {
                    // Fallback: no pallet detected → run S2 on full image
                    AppLogger.Info($"[{Name}] S1 no pallet detected — falling back to full image");
                    cropX = 0;
                    cropY = 0;
                    cropW = mat.Width;
                    cropH = mat.Height;
                    croppedMat = mat;
                    palletFound = false;
                }

                // ── S2: Defect detection on cropped region ──────────────────────
                AppLogger.Info($"[{Name}] Running S2 defect detection on {(palletFound ? "cropped" : "full")} image ({croppedMat.Width}x{croppedMat.Height})");
                var defectPreds = _defectModel.Infer(croppedMat);

                // Dispose the cropped mat only if we created a new one
                if (palletFound)
                    croppedMat.Dispose();

                // ── Remap S2 bounding boxes to full-image coordinates ───────────
                var remappedPreds = new List<PredictionObject>();
                foreach (var pred in defectPreds)
                {
                    remappedPreds.Add(new PredictionObject
                    {
                        Rect = new Rect2f(
                            pred.Rect.X + cropX,
                            pred.Rect.Y + cropY,
                            pred.Rect.Width,
                            pred.Rect.Height),
                        Label = pred.Label,
                        Probability = pred.Probability
                    });
                }

                // ── Pass/fail logic ─────────────────────────────────────────────
                int contaminationCount = remappedPreds.Count(p => p.Label == 2);
                int staplesCount = remappedPreds.Count(p => p.Label == 1);
                int cracksCount = remappedPreds.Count(p => p.Label == 0);

                bool passed = contaminationCount == 0 && staplesCount <= StaplesMaxAllowed;

                double confidence = remappedPreds.Count > 0
                    ? remappedPreds.Max(p => p.Probability)
                    : 1.0;

                AppLogger.Info($"[{Name}] S2 result: {(passed ? "Pass" : "Fail")} | " +
                               $"Contamination={contaminationCount} Staples={staplesCount} Cracks={cracksCount} | " +
                               $"Conf={confidence:F4}");

                // ── Draw annotations on full image ──────────────────────────────
                using var annotated = mat.Clone();
                var labelRects = new List<(string Label, Rect Rect)>();

                foreach (var pred in remappedPreds)
                {
                    var rect = new Rect(
                        (int)pred.Rect.X,
                        (int)pred.Rect.Y,
                        (int)pred.Rect.Width,
                        (int)pred.Rect.Height);

                    string label = ClassLabels[pred.Label];
                    Scalar colour = ClassColours[pred.Label];

                    Cv2.Rectangle(annotated, rect, colour, 2);
                    Cv2.PutText(
                        annotated,
                        $"{label} {pred.Probability:P0}",
                        new Point(rect.X, Math.Max(rect.Y - 5, 10)),
                        HersheyFonts.HersheySimplex,
                        0.6,
                        colour,
                        2);

                    labelRects.Add((label, rect));
                }

                HImage processedImage = ImageUtils.MatBGRToHImage(annotated);

                context.InspectionResults[Name] = new InspectionResult
                {
                    InspectionName = Name,
                    Passed = passed,
                    Confidence = confidence,
                    LabelRects = labelRects,
                    ProcessedImage = processedImage,
                    InspectionComplete = false
                };
            });
        }

        public void Dispose()
        {
            _palletModel?.Dispose();
            _defectModel?.Dispose();
        }
    }
}
