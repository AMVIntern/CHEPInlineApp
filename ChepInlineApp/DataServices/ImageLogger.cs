using HalconDotNet;
using ChepInlineApp.Models;

namespace ChepInlineApp.DataServices
{
    public class ImageLogger
    {
        private readonly AppConfigModel _appConfigModel;
        private readonly ImageLoggingService _imageLoggingService;

        public ImageLogger(AppConfigModel appConfigModel, ImageLoggingService imageLoggingService)
        {
            _appConfigModel = appConfigModel;
            _imageLoggingService = imageLoggingService;
        }

        public async Task<string?> LogIfEnabledAsync(HImage image, long timestamp, string cameraName, string result = "Pass", double confidence = 0.0, string format = "tiff")
        {
            if (!_appConfigModel.ShouldLog(cameraName, result))
                return null;

            return await _imageLoggingService.LogAsync(image, timestamp, cameraName, result, confidence, format);
        }
    }
}
