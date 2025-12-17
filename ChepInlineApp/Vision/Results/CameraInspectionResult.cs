using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Vision.Results
{
    public class CameraInspectionResult : IDisposable
    {
        public string CameraId { get; init; } = string.Empty;
        public List<InspectionResult> Results { get; init; } = new();
        public bool OverallPass => Results.All(r => r.Passed);

        public void Dispose()
        {
            foreach (var result in Results)
                result?.Dispose();
            Results.Clear();
        }
    }
}
