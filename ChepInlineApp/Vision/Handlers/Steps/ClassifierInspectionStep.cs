using ChepInlineApp.Helpers;
using ChepInlineApp.Vision.Handlers.Core;
using ChepInlineApp.Vision.Handlers.Interfaces;
using ChepInlineApp.Vision.Results;
using HalconDotNet;
using ChepInlineApp.Classifier;
using ChepInlineApp.Classifier.Utils;
using ChepInlineApp.Classifier.Core;

namespace ChepInlineApp.Vision.Handlers.Steps
{
    public class ClassifierInspectionStep : IInspectionStep
    {
        public string Name { get; }
        private readonly string ClassifierModelPath;
        public ClassifierInspectionStep(string name, string classifierModelPath)
        {
            Name = name;
            ClassifierModelPath = classifierModelPath;
        }
        public Task RunAsync(InspectionContext context)
        {
            return Task.Run(() =>
            {
                AppLogger.Info($"[{Name}] ClassifierInspectionStep.RunAsync START - Camera: {context.CameraId}");
                HImage hImage = context.Image ?? throw new ArgumentNullException(nameof(context.Image));

                AppLogger.Info($"[{Name}] Converting HImage to Mat");

                var sessionOptions = ModelUtils.GetDefaultSessionOptions();
                var model = new ClassifierModel(ClassifierModelPath, sessionOptions);


                // Convert Halcon HImage to OpenCvSharp Mat
                using var mat = ImageUtils.HImageToMatBGR(hImage);

                AppLogger.Info($"[{Name}] Running DenseT classification inference ");

                // Run classification on the full image
                var prediction = model.Infer(mat);

                // ClassID 0 = Pass (Pass), ClassID 1, 2, 3 = Fail (Fail)
                var passed = prediction.ClassID == 0;
                var confidence = prediction.Probability;
                var resultLabel = passed ? "Pass" : "Fail";

                AppLogger.Info($"[{Name}] DenseT classification result: {resultLabel}, Confidence: {confidence:F4}, ClassID: {prediction.ClassID}");

                // Store classification result using step name (for compatibility with SendResultsStep)
                var result = new InspectionResult
                {
                    InspectionName = Name,
                    Passed = passed,
                    Confidence = confidence,
                    InspectionComplete = false
                };

                context.InspectionResults[Name] = result;
            });
        }
    }
}
