using HalconDotNet;
using OpenCvSharp;

namespace ChepInlineApp.Vision.Results
{
    public class InspectionResult : IDisposable
    {
        public string InspectionName { get; init; } = string.Empty;
        public bool Passed { get; init; }
        public bool InspectionComplete { get; set; } = false;
        public double Confidence { get; init; }
        public List<HRegion> FailRegions { get; } = new();
        public List<HRegion> AllowedRegions { get; } = new();
        public List<Rect> RectRegions { get; init; } = new();
        public List<(string Label, Rect Rect)> LabelRects { get; init; } = new();
        public List<Mat> CropMatImages { get; init; } = new();
        public HImage ProcessedImage { get; init; } = new();
        public void Dispose()
        {
        }
    }
}
