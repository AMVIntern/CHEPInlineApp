using OpenCvSharp;

namespace ChepInlineApp.PatchCore.Utils
{
    public static class ImageUtils
    {
        public static Mat ResizeWarp(Mat srcBgr, int w, int h)
        {
            var dst = new Mat();
            Cv2.Resize(srcBgr, dst, new Size(w, h), 0, 0, InterpolationFlags.Linear);
            return dst;
        }

        // float[] NCHW, RGB, normalized 0..1
        public static float[] ToRgbNchwFloat01(Mat resizedBgr, int w, int h)
        {
            using var rgb = new Mat();
            Cv2.CvtColor(resizedBgr, rgb, ColorConversionCodes.BGR2RGB);

            var data = new float[1 * 3 * h * w];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var px = rgb.At<Vec3b>(y, x); // RGB
                    float r = px.Item0 / 255f;
                    float g = px.Item1 / 255f;
                    float b = px.Item2 / 255f;

                    int baseIdx = y * w + x;
                    data[0 * h * w + baseIdx] = r;
                    data[1 * h * w + baseIdx] = g;
                    data[2 * h * w + baseIdx] = b;
                }
            }

            return data;
        }
    }
}