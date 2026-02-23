using ChepInlineApp.Vision.Handlers.Core;
using ChepInlineApp.Vision.Handlers.Interfaces;
using ChepInlineApp.Vision.Results;

namespace ChepInlineApp.Vision.Handlers.Steps
{
    public sealed class CombineInspectionResultsStep : IInspectionStep
    {
        public string Name { get; } = "Overall";

        private readonly string _classifierKey;
        private readonly string _patchCoreKey;

        public CombineInspectionResultsStep(string classifierKey, string patchCoreKey)
        {
            _classifierKey = classifierKey;
            _patchCoreKey = patchCoreKey;
        }

        public Task RunAsync(InspectionContext context)
        {
            return Task.Run(() =>
            {
                // Pull both results (must exist if steps ran)
                context.InspectionResults.TryGetValue(_classifierKey, out var cObj);
                context.InspectionResults.TryGetValue(_patchCoreKey, out var pObj);

                var c = cObj as InspectionResult;
                var p = pObj as InspectionResult;

                bool overallPass =
                    (c?.Passed ?? false) &&
                    (p?.Passed ?? false);

                // Pick a conservative confidence
                double conf = 0.0;
                if (c != null && p != null) conf = Math.Min(c.Confidence, p.Confidence);
                else if (c != null) conf = c.Confidence;
                else if (p != null) conf = p.Confidence;
                context.InspectionResults["Overall"] = new InspectionResult
                {
                    InspectionName = "Overall",
                    Passed = overallPass,
                    Confidence = conf,
                    InspectionComplete = false
                };

                // Also store a bool (some of your code reads these)
                context.InspectionResults["OverallPass"] = overallPass;
            });
        }
    }
}