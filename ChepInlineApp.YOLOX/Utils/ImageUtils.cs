using HalconDotNet;
using OpenCvSharp;
using System.Runtime.InteropServices;

namespace ChepInlineApp.YOLOX.Utils;

public static class ImageUtils
{
	public static Mat static_resize(Mat img, int inputWidth, int inputHeight)
	{
		float r = Math.Min(inputWidth / (float)img.Width, inputHeight / (float)img.Height);

		int unpadWidth = (int)(r * img.Width);
		int unpadHeight = (int)(r * img.Height);

		Mat scaledImg = new Mat(new Size(unpadWidth, unpadHeight), MatType.CV_8UC3);
		Cv2.Resize(img, scaledImg, new Size(unpadWidth, unpadHeight));

		Mat resizedMat = new Mat(new Size(inputWidth, inputHeight), MatType.CV_8UC3, new Scalar(114, 114, 114));

		Rect roi = new Rect(0, 0, scaledImg.Width, scaledImg.Height);
		scaledImg.CopyTo(resizedMat[roi]);

		return resizedMat;
	}

	public static List<Mat> CropRegions(Mat image, List<Rect> rois)
	{
		var croppedRegions = new List<Mat>();

		foreach (var roi in rois)
		{
			Rect validRoi = new Rect
			{
				X = Math.Max(0, roi.X),
				Y = Math.Max(0, roi.Y),
				Width = Math.Min(roi.Width, image.Width - roi.X),
				Height = Math.Min(roi.Height, image.Height - roi.Y)
			};

			if (validRoi.Width > 0 && validRoi.Height > 0)
			{
				croppedRegions.Add(new Mat(image, validRoi).Clone());
			}
		}

		return croppedRegions;
	}

	public static Mat HImageToMatBGR(HImage hImage)
	{
		hImage.GetImagePointer3(
			out IntPtr redPtr,
			out IntPtr greenPtr,
			out IntPtr bluePtr,
			out string type,
			out int width,
			out int height
		);

		if (type != "byte")
			throw new NotSupportedException($"Unsupported image type: {type}");

		int pixelCount = width * height;

		byte[] redChannel = new byte[pixelCount];
		byte[] greenChannel = new byte[pixelCount];
		byte[] blueChannel = new byte[pixelCount];

		Marshal.Copy(redPtr, redChannel, 0, pixelCount);
		Marshal.Copy(greenPtr, greenChannel, 0, pixelCount);
		Marshal.Copy(bluePtr, blueChannel, 0, pixelCount);

		Mat redMat = Mat.FromPixelData(height, width, MatType.CV_8UC1, redChannel);
		Mat greenMat = Mat.FromPixelData(height, width, MatType.CV_8UC1, greenChannel);
		Mat blueMat = Mat.FromPixelData(height, width, MatType.CV_8UC1, blueChannel);

		Mat bgrMat = new Mat();
		Cv2.Merge(new[] { blueMat, greenMat, redMat }, bgrMat);

		redMat.Dispose();
		greenMat.Dispose();
		blueMat.Dispose();

		return bgrMat;
	}

	public static HImage MatBGRToHImage(Mat bgrMat)
	{
		// Split BGR channels (OpenCV order: 0=B, 1=G, 2=R)
		Cv2.Split(bgrMat, out Mat[] channels);
		Mat blueMat = channels[0];
		Mat greenMat = channels[1];
		Mat redMat = channels[2];

		int width = bgrMat.Width;
		int height = bgrMat.Height;
		int pixelCount = width * height;

		byte[] redChannel = new byte[pixelCount];
		byte[] greenChannel = new byte[pixelCount];
		byte[] blueChannel = new byte[pixelCount];

		Marshal.Copy(redMat.Data, redChannel, 0, pixelCount);
		Marshal.Copy(greenMat.Data, greenChannel, 0, pixelCount);
		Marshal.Copy(blueMat.Data, blueChannel, 0, pixelCount);

		blueMat.Dispose();
		greenMat.Dispose();
		redMat.Dispose();

		// Pin arrays so Halcon can read them during GenImage3
		var gcRed = GCHandle.Alloc(redChannel, GCHandleType.Pinned);
		var gcGreen = GCHandle.Alloc(greenChannel, GCHandleType.Pinned);
		var gcBlue = GCHandle.Alloc(blueChannel, GCHandleType.Pinned);

		try
		{
			HImage hImage = new HImage();
			hImage.GenImage3(
				"byte",
				width, height,
				gcRed.AddrOfPinnedObject(),
				gcGreen.AddrOfPinnedObject(),
				gcBlue.AddrOfPinnedObject());
			return hImage;
		}
		finally
		{
			gcRed.Free();
			gcGreen.Free();
			gcBlue.Free();
		}
	}
}
