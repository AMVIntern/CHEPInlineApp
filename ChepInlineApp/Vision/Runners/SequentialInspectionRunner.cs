using ChepInlineApp.Helpers;
using ChepInlineApp.Vision.Handlers.Core;
using ChepInlineApp.Vision.Handlers.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Vision.Runners
{
    public class SequentialInspectionRunner : IInspectionRunner
    {
        private readonly List<IInspectionStep> _steps;

        public SequentialInspectionRunner(IEnumerable<IInspectionStep> steps)
        {
            _steps = new List<IInspectionStep>(steps);
        }

        public async Task<InspectionContext> RunAsync(InspectionContext context)
        {
            foreach (var step in _steps)
            {
                var sw = Stopwatch.StartNew();
                await step.RunAsync(context);
                sw.Stop();
                AppLogger.Info($"Step '{step.Name}' completed in {sw.ElapsedMilliseconds} ms.");
            }
            return context;
        }
    }

}
