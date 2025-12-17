using ChepInlineApp.AppCycleManager;
using ChepInlineApp.DataServices;
using ChepInlineApp.Helpers;
using ChepInlineApp.Stores;
using ChepInlineApp.ViewModels;
using HalconDotNet;
using ChepInlineApp.Vision.Handlers.Core;
using ChepInlineApp.Vision.Results;
using ChepInlineApp.Vision.Handlers.Interfaces;
using System.Windows;

namespace ChepInlineApp.Vision.Coordinator
{
    public class InspectionCoordinator
    {
        private readonly Dictionary<string, IInspectionRunner> _runners;
        private readonly MultiCameraImageStore _imageStore;
        private readonly Dictionary<string, CameraViewModel> _cameraViewModels;
        private readonly ImageLogger _imageLogger;
        private readonly TriggerSessionManager _triggerSessionManager;
        private readonly HomeViewModel? _homeViewModel;

        public InspectionCoordinator(
            Dictionary<string, IInspectionRunner> runners,
            MultiCameraImageStore imageStore,
            Dictionary<string, CameraViewModel> cameraViewModels,
            ImageLogger imageLogger,
            TriggerSessionManager triggerSessionManager,
            HomeViewModel? homeViewModel = null)
        {
            _runners = runners;
            _imageStore = imageStore;
            _cameraViewModels = cameraViewModels;
            _imageLogger = imageLogger;
            _triggerSessionManager = triggerSessionManager;
            _homeViewModel = homeViewModel;
            foreach (var (cameraId, runner) in _runners)
            {
                _imageStore.Subscribe(cameraId, async () => await HandleNewImage(cameraId, runner));
            }
        }

        private async Task HandleNewImage(string cameraId, IInspectionRunner runner)
        {
            var image = _imageStore.GetImage(cameraId);
            if (image == null || !image.IsInitialized())
            {
                AppLogger.Error($"[Inspection] Skipping frame for '{cameraId}' — image is null or uninitialized.");
                return;
            }

            var context = new InspectionContext
            {
                Image = image.Clone(),
                CameraId = cameraId,
            };

            try
            {
                var updatedContext = await runner.RunAsync(context);

                await HandleResult(cameraId, updatedContext, image);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Inspection failed for camera '{cameraId}': {ex.Message}", ex);
            }
            finally
            {
                context?.Dispose();
            }
        }

        private async Task HandleResult(string cameraId, InspectionContext context, HImage image)
        {
            
        }

    }
}
