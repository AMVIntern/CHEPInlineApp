using HalconDotNet;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Classifier.Utils
{
    public static class ImageUtils
    {
        public static Mat static_resize(Mat img, int inputWidth, int inputHeight)
        {
            // calculate resize scale
            float r = Math.Min(inputWidth / (float)img.Width, inputHeight / (float)img.Height);

            // calculate unpadded with and height
            int unpadWidth = (int)(r * img.Width);
            int unpadHeight = (int)(r * img.Height);

            // Create a scaled image with unpadded dimensions
            Mat scaledImg = new Mat(new Size(unpadWidth, unpadHeight), MatType.CV_8UC3);
            Cv2.Resize(img, scaledImg, new Size(unpadWidth, unpadHeight));

            // Create an empty resized Mat with the target dimensions, filled with (114, 114, 114)
            Mat resizedMat = new Mat(new Size(inputWidth, inputHeight), MatType.CV_8UC3, new Scalar(114, 114, 114));

            // Copy scaled image onto the top-left corner of the resized Mat
            Rect roi = new Rect(0, 0, scaledImg.Width, scaledImg.Height);
            scaledImg.CopyTo(resizedMat[roi]);

            return resizedMat;
        }

        /// <summary>
        /// Crops multiple rectangular regions from a source image.
        /// </summary>
        /// <param name="image">Original image (Mat)</param>
        /// <param name="rois">List of bounding boxes (Rects) to crop</param>
        /// <returns>List of cropped Mat regions</returns>
        public static List<Mat> CropRegions(Mat image, List<Rect> rois)
        {
            var croppedRegions = new List<Mat>();

            foreach (var roi in rois)
            {
                // Ensure the ROI is within image bounds
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

        /// <summary>
        /// Converts a Halcon HImage to an OpenCV Mat (BGR).
        /// Caller must dispose the returned Mat.
        /// </summary>
        public static Mat HImageToMatBGR(HImage hImage)
        {
            // Extract pointers and image properties from HImage
            hImage.GetImagePointer3(
                out IntPtr redPtr,
                out IntPtr greenPtr,
                out IntPtr bluePtr,
                out string type,
                out int width,
                out int height
            );

            // Ensure the type is compatible with OpenCV (e.g., "byte")
            if (type != "byte")
            {
                throw new NotSupportedException($"Unsupported image type: {type}");
            }

            // Calculate the total number of pixels
            int pixelCount = width * height;

            // Allocate managed byte arrays for each channel
            byte[] redChannel = new byte[pixelCount];
            byte[] greenChannel = new byte[pixelCount];
            byte[] blueChannel = new byte[pixelCount];

            // Copy data from Halcon pointers to managed arrays
            Marshal.Copy(redPtr, redChannel, 0, pixelCount);
            Marshal.Copy(greenPtr, greenChannel, 0, pixelCount);
            Marshal.Copy(bluePtr, blueChannel, 0, pixelCount);

            // Create single-channel Mat objects using FromPixelData
            Mat redMat = Mat.FromPixelData(height, width, MatType.CV_8UC1, redChannel);
            Mat greenMat = Mat.FromPixelData(height, width, MatType.CV_8UC1, greenChannel);
            Mat blueMat = Mat.FromPixelData(height, width, MatType.CV_8UC1, blueChannel);

            // Merge the individual channels into a 3-channel BGR image
            Mat bgrMat = new Mat();
            Cv2.Merge(new[] { blueMat, greenMat, redMat }, bgrMat);

            // Release the single channel Mats (optional but Pass practice)
            redMat.Dispose();
            greenMat.Dispose();
            blueMat.Dispose();

            return bgrMat;
        }

        public static Mat ResizeWarp(Mat img, int inputWidth, int inputHeight)
        {
            var target = new Size(inputWidth, inputHeight);

            // Pick interpolation: Area for downscale, Linear for upscale
            bool shrinking = (inputWidth < img.Width) || (inputHeight < img.Height);
            var interp = shrinking ? InterpolationFlags.Area : InterpolationFlags.Linear;

            Mat resized = new Mat();
            Cv2.Resize(img, resized, target, 0, 0, interp);
            return resized;
        }


        //public static float[] NormalizeToTensor(Mat image, int width, int height)
        //{
        //	// Resize to match model input size
        //	using var resized = image.Resize(new Size(width, height));

        //	// Create blob (applies: scale 1/255, mean subtraction, RGB conversion)
        //	using var blob = CvDnn.BlobFromImage(
        //		resized,
        //		scaleFactor: 1.0 / 255.0,
        //		size: new Size(), // already resized
        //		mean: new Scalar(0.485, 0.456, 0.406), // ImageNet mean
        //		swapRB: true,
        //		crop: false
        //	);

        //	using var blobClone = blob.Clone(); // Prevent memory release
        //	float[] inputTensor = new float[blobClone.Total()];
        //	Marshal.Copy(blobClone.Data, inputTensor, 0, inputTensor.Length);

        //	// Manually divide by std (ImageNet std) — required to match PyTorch
        //	for (int i = 0; i < inputTensor.Length; i += 3)
        //	{
        //		inputTensor[i] /= 0.229f; // R
        //		inputTensor[i + 1] /= 0.224f; // G
        //		inputTensor[i + 2] /= 0.225f; // B
        //	}

        //	return inputTensor;
        //}

        public static float[] NormalizeToTensorPostResized(Mat imageAlreadyResizedRGBorBGR, int width, int height)
        {
            // Only scale and swapRB; DO NOT pass mean here.
            using var blob = CvDnn.BlobFromImage(
                imageAlreadyResizedRGBorBGR,
                scaleFactor: 1.0f / 255.0f,
                size: new Size(),   // already resized by caller
                mean: new Scalar(), // no mean here
                swapRB: true,       // BGR->RGB
                crop: false
            );

            using var blobClone = blob.Clone();
            float[] data = new float[blobClone.Total()];
            Marshal.Copy(blobClone.Data, data, 0, data.Length);

            int h = height, w = width, cStride = h * w; // N=1, C=3
            float[] mean = { 0.485f, 0.456f, 0.406f };
            float[] std = { 0.229f, 0.224f, 0.225f };

            // NCHW layout: [R block][G block][B block]
            for (int ch = 0; ch < 3; ch++)
            {
                int off = ch * cStride;
                float m = mean[ch], s = std[ch];
                for (int i = 0; i < cStride; i++)
                    data[off + i] = (data[off + i] - m) / s;
            }
            return data;
        }


    }

}
