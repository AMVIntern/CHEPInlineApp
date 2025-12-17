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
        public async Task LogAsync(HImage image, long timestamp, string cameraName, string result = "Good", double confidence = 0.0, string format = "tiff")
        {
            try
            {
                var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToLocalTime();
                string year = date.Year.ToString("0000");
                string month = date.Month.ToString("00");
                string day = date.Day.ToString("00");

                string basePath = Path.Combine(_baseDirectory, year, month, day, cameraName);

                Directory.CreateDirectory(basePath);

                string fileName = $"{timestamp}_{cameraName}_{result}_{confidence:F2}.{GetExtension(format)}";
                string fullPath = Path.Combine(basePath, fileName);

                await Task.Run(() =>
                {
                    image.WriteImage(format, 0, fullPath);
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to log image for {cameraName}", ex);
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
