using ChepInlineApp.Vision.Handlers.Core;
using ChepInlineApp.Vision.Handlers.Interfaces;
using ChepInlineApp.Vision.Results;

namespace ChepInlineApp.Vision.Handlers.Steps
{
    public sealed class CombineInspectionResultsStep : IInspectionStep
    {
        public string Name { get; } = "Overall";

        private readonly string _classifierKey;
        private readonly string? _patchCoreKey;
        private readonly string? _yoloxKey;

        public CombineInspectionResultsStep(string classifierKey, string? patchCoreKey = null, string? yoloxKey = null)
        {
            _classifierKey = classifierKey;
            _patchCoreKey = patchCoreKey;
            _yoloxKey = yoloxKey;
        }

        public Task RunAsync(InspectionContext context)
        {
            return Task.Run(() =>
            {
                context.InspectionResults.TryGetValue(_classifierKey, out var cObj);
                object? pObj = null;
                if (_patchCoreKey != null)
                    context.InspectionResults.TryGetValue(_patchCoreKey, out pObj);
                object? yObj = null;
                if (_yoloxKey != null)
                    context.InspectionResults.TryGetValue(_yoloxKey, out yObj);

                var c = cObj as InspectionResult;
                var p = pObj as InspectionResult;
                var y = yObj as InspectionResult;

                bool overallPass =
                    (c?.Passed ?? false) &&
                    (_patchCoreKey == null || (p?.Passed ?? false)) &&
                    (_yoloxKey == null || (y?.Passed ?? false));

                // Conservative confidence: minimum across all present results
                var presentConfs = new List<double>();
                if (c != null) presentConfs.Add(c.Confidence);
                if (p != null) presentConfs.Add(p.Confidence);
                if (y != null) presentConfs.Add(y.Confidence);
                double conf = presentConfs.Count > 0 ? presentConfs.Min() : 0.0;

                context.InspectionResults["Overall"] = new InspectionResult
                {
                    InspectionName = "Overall",
                    Passed = overallPass,
                    Confidence = conf,
                    InspectionComplete = false
                };

                context.InspectionResults["OverallPass"] = overallPass;
            });
        }
    }
}
