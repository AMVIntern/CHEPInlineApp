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
        public void Dispose()
        {
        }
    }
}
