using ChepInlineApp.Helpers;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.DataServices
{
    public class ImageLoggingService
    {
        private readonly string _baseDirectory = @"C:\AMV\ImageLogs";
        public async Task<string?> LogAsync(HImage image, long timestamp, string cameraName, string result = "Pass", double confidence = 0.0, string format = "tiff")
        {
            try
            {
                var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToLocalTime();
                string year = date.Year.ToString("0000");
                string month = date.Month.ToString("00");
                string day = date.Day.ToString("00");

                string basePath = Path.Combine(_baseDirectory, year, month, day, cameraName);

                Directory.CreateDirectory(basePath);

                string confidenceStr = (confidence * 100).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                string fileName = $"{timestamp}_{cameraName}_{result}_{confidenceStr}.{GetExtension(format)}";
                string fullPath = Path.Combine(basePath, fileName);

                // PNG subfolder
                string pngFolder = Path.Combine(basePath, "png");
                Directory.CreateDirectory(pngFolder);
                string pngFileName = $"{timestamp}_{cameraName}_{result}_{confidenceStr}.png";
                string pngFullPath = Path.Combine(pngFolder, pngFileName);

                await Task.Run(() =>
                {
                    image.WriteImage(format, 0, fullPath);
                    image.WriteImage("png", 0, pngFullPath);
                });

                return fullPath;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to log image for {cameraName}", ex);
                return null;
            }
        }
        private string GetExtension(string format)
        {
            return format.ToLower() switch
            {
                "jpeg" or "jpg" => "jpg",
                "png" => "png",
                "bmp" => "bmp",
                _ => "tif",
            };
        }
    }
}
