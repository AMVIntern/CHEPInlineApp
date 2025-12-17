using ChepInlineApp.DataServices;
using ChepInlineApp.Helpers;
using ChepInlineApp.Interfaces;
using ChepInlineApp.Stores;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace ChepInlineApp.ImageSources
{
    public class FolderImageLoader : IImageSource
    {
        private readonly string _folderPath;
        private readonly MultiCameraImageStore _imageStore;
        private readonly string _cameraId;
        private readonly ImageLogger _imageLogger;

        private string[] _imageFiles = Array.Empty<string>();
        private int _currentIndex = 0;
        public string CameraId => _cameraId;

        public FolderImageLoader(string folderPath, MultiCameraImageStore imageStore, string cameraId, ImageLogger imageLogger)
        {
            _folderPath = folderPath;
            _imageStore = imageStore;
            _cameraId = cameraId;
            _imageLogger = imageLogger;

            if (Directory.Exists(_folderPath))
            {
                _imageFiles = Directory.GetFiles(_folderPath, "*.*")
                    .Where(f => f.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png") || f.EndsWith(".jpg"))
                    .OrderBy(f => f)
                    .ToArray();
            }
        }
        public Task GrabNextFrameAsync()
        {
            if (_imageFiles.Length == 0)
                return Task.CompletedTask;

            string imagePath = _imageFiles[_currentIndex];

            if (File.Exists(imagePath))
            {
                try
                {
                    HImage image = new HImage(imagePath);
                    image.GetImageSize(out int width, out int height);
                    _imageStore.SetCameraImageWidth(_cameraId, width);
                    _imageStore.SetCameraImageHeight(_cameraId, height);

                    // Calculate center points
                    double centerX = width / 2.0;
                    double centerY = height / 2.0;
                    _imageStore.SetCameraCenter(_cameraId, centerX, centerY);
                    Debug.WriteLine($"[{_cameraId}] CenterX={centerX}, CenterY={centerY}");

                    // Draw dotted line through center
                    image = DrawDottedCenterLine(image, width, height, centerX, centerY);
                    _imageStore.UpdateImage(_cameraId, image);
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Failed to load image from {_folderPath}", ex);
                }
            }

            _currentIndex = (_currentIndex + 1) % _imageFiles.Length;
            return Task.CompletedTask;
        }
        private HImage DrawDottedCenterLine(HImage image, int width, int height, double centerX, double centerY)
        {
            try
            {
                // Create horizontal dotted line through center Y with thicker line
                int dotLength = 15;
                int gapLength = 5;
                int lineThickness = 3; // Make line 3 pixels thick for better visibility
                
                int centerYInt = (int)Math.Round(centerY);
                Debug.WriteLine($"[{_cameraId}] Drawing dotted line at Y={centerYInt}, width={width}");
                AppLogger.Info($"[DrawLine] Drawing line for {_cameraId} at Y={centerYInt}, width={width}");
                
                HImage resultImage = image.Clone();

                Debug.WriteLine($"[{_cameraId}] Processing color image - painting on RGB channels");
                HImage redChannel = image.Decompose3(out HImage greenChannel, out HImage blueChannel);
                    
                // Draw horizontal dotted line through center Y - paint each segment directly
                int currentX = 0;
                while (currentX < width)
                {
                    int endX = Math.Min(currentX + dotLength, width);
                    // Create rectangle region for each dot segment (thicker than line)
                    int topY = Math.Max(0, centerYInt - lineThickness / 2);
                    int bottomY = Math.Min(height - 1, centerYInt + lineThickness / 2);
                    HOperatorSet.GenRectangle1(out HObject rect, topY, currentX, bottomY, endX);
                    HRegion segment = new HRegion(rect);
                        
                    // Paint white (255) on all channels for this segment
                    redChannel = redChannel.PaintRegion(segment, new HTuple(255), "fill");
                    greenChannel = greenChannel.PaintRegion(segment, new HTuple(255), "fill");
                    blueChannel = blueChannel.PaintRegion(segment, new HTuple(255), "fill");
                        
                    segment.Dispose();
                    currentX = endX + gapLength;
                }
                    
                // Compose back to color image once after all segments are painted
                resultImage = redChannel.Compose3(greenChannel, blueChannel);
                    
                // Dispose intermediate images
                redChannel.Dispose();
                greenChannel.Dispose();
                blueChannel.Dispose();
                image.Dispose();
                
                Debug.WriteLine($"[{_cameraId}] Line painted successfully");
                return resultImage;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to draw dotted line for {_cameraId}: {ex.Message}", ex);
                Debug.WriteLine($"Exception in DrawDottedCenterLine: {ex}");
                return image;
            }
        }
        public void Dispose()
        {
        }
    }
}
