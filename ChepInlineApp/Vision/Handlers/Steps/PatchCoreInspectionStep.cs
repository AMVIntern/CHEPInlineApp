using ChepInlineApp.Helpers;
using System;
using System.IO;
using ChepInlineApp.PatchCore.Core;
using ChepInlineApp.PatchCore.Utils;
using ChepInlineApp.Vision.Handlers.Core;
using ChepInlineApp.Vision.Handlers.Interfaces;
using ChepInlineApp.Vision.Results;
using HalconDotNet;
using OpenCvSharp;
using PatchCoreModelUtils = ChepInlineApp.PatchCore.Utils.ModelUtils;

namespace ChepInlineApp.Vision.Handlers.Steps
{
    public sealed class PatchCoreInspectionStep : IInspectionStep
    {
        public string Name { get; }
        private readonly string _modelPath;
        private readonly float _threshold;
        private readonly string _mapScoreMode;

        public PatchCoreInspectionStep(string name, string modelPath, float threshold, string mapScoreMode = "max")
        {
            Name = name;
            _modelPath = modelPath;
            _threshold = threshold;
            _mapScoreMode = mapScoreMode;
        }

        public Task RunAsync(InspectionContext context)
        {
            return Task.Run(() =>
            {
                AppLogger.Info($"[{Name}] PatchCoreInspectionStep START - Camera: {context.CameraId}");

                HImage hImage = context.Image ?? throw new ArgumentNullException(nameof(context.Image));

                var sessionOptions = PatchCoreModelUtils.GetDefaultSessionOptions();
                using var model = new PatchCoreModel(_modelPath, sessionOptions);

                using var mat = ChepInlineApp.Classifier.Utils.ImageUtils.HImageToMatBGR(hImage);

                // ✅ APPLY CROP BEFORE PATCHCORE
                int cropX = 376;
                int cropY = 0;
                int cropW = 2216;
                int cropH = 2400;

                // Clamp to bounds (safe for production)
                if (cropX + cropW > mat.Width)
                    cropW = mat.Width - cropX;

                if (cropY + cropH > mat.Height)
                    cropH = mat.Height - cropY;

                var roi = new OpenCvSharp.Rect(cropX, cropY, cropW, cropH);
                using var croppedMat = new Mat(mat, roi).Clone();
               
                // 🔵 Now send cropped image to PatchCore
                var (score, mn, mx, mean, src) = model.Infer(croppedMat, _mapScoreMode);

                bool passed = score <= _threshold;

                // Confidence convention in your UI/logs is “higher better”.
                // For PatchCore: higher score = more anomalous, so invert.
                double confidence = 1.0 - score;


                // Save extra debug fields (optional but useful)
                context.InspectionResults[$"{Name}.Score"] = score;
                context.InspectionResults[$"{Name}.MapMin"] = mn;
                context.InspectionResults[$"{Name}.MapMax"] = mx;
                context.InspectionResults[$"{Name}.MapMean"] = mean;
                context.InspectionResults[$"{Name}.Source"] = src;

                // Store as InspectionResult too
                context.InspectionResults[Name] = new InspectionResult
                {
                    InspectionName = Name,
                    Passed = passed,
                    Confidence = score,
                    InspectionComplete = false
                };
            });
        }
    }
}