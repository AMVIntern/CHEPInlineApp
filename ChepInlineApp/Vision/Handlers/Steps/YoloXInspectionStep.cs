using ChepInlineApp.Helpers;
using ChepInlineApp.Vision.Handlers.Core;
using ChepInlineApp.Vision.Handlers.Interfaces;
using ChepInlineApp.Vision.Results;
using ChepInlineApp.YOLOX.Core;
using ChepInlineApp.YOLOX.Utils;
using HalconDotNet;
using OpenCvSharp;

namespace ChepInlineApp.Vision.Handlers.Steps
{
    public class YoloXInspectionStep : IInspectionStep
    {
        public string Name { get; }

        private static readonly string[] ClassLabels = { "Cracks", "Staples", "Contamination" };

        // BGR colours per class
        private static readonly Scalar[] ClassColours =
        {
            new Scalar(0, 255, 255),  // Cracks        → yellow
            new Scalar(0, 165, 255),  // Staples        → orange
            new Scalar(0, 0, 255),    // Contamination  → red
        };

        private const int StaplesMaxAllowed = 7;
        private readonly string _modelPath;

        public YoloXInspectionStep(string name, string modelPath)
        {
            Name = name;
            _modelPath = modelPath;
        }

        public Task RunAsync(InspectionContext context)
        {
            return Task.Run(() =>
            {
                AppLogger.Info($"[{Name}] YoloXInspectionStep.RunAsync START - Camera: {context.CameraId}");

                HImage hImage = context.Image ?? throw new ArgumentNullException(nameof(context.Image));

                using var model = new YoloXModel(_modelPath, confThreshold: 0.3f);
                using var mat = ImageUtils.HImageToMatBGR(hImage);

                AppLogger.Info($"[{Name}] Running YOLOX inference");

                var predictions = model.Infer(mat);

                // Pass/fail rules:
                //  Class 0 (Cracks)         → always ignored
                //  Class 1 (Staples)         → >7 detections = Fail
                //  Class 2 (Contamination)   → any detection = Fail
                int contaminationCount = predictions.Count(p => p.Label == 2);
                int staplesCount = predictions.Count(p => p.Label == 1);

                bool passed = contaminationCount == 0 && staplesCount <= StaplesMaxAllowed;

                double confidence = predictions.Count > 0
                    ? predictions.Max(p => p.Probability)
                    : 1.0;

                AppLogger.Info($"[{Name}] YOLOX result: {(passed ? "Pass" : "Fail")} | " +
                               $"Contamination={contaminationCount} Staples={staplesCount} Cracks={predictions.Count(p => p.Label == 0)} | " +
                               $"Conf={confidence:F4}");

                // Draw bounding boxes on a copy of the image
                using var annotated = mat.Clone();
                var labelRects = new List<(string Label, Rect Rect)>();

                foreach (var pred in predictions)
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
    }
}
