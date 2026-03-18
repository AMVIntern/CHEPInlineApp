using OpenCvSharp;

namespace ChepInlineApp.YOLOX.Models;

public class PredictionObject
{
	public Rect2f Rect { get; set; }
	public int Label { get; set; }
	public float Probability { get; set; }
}
