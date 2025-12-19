using ChepInlineApp.AppCycleManager;
using ChepInlineApp.Comms;
using ChepInlineApp.DataServices;
using ChepInlineApp.Helpers;
using ChepInlineApp.MetadataExporter.Services;
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
        private readonly ImageCaptureCsvWriter _csvWriter;
        private readonly TriggerSessionManager _triggerSessionManager;
        private readonly PlcEventStore _plcEventStore;
        private readonly PlcCommsManager _plcCommsManager;
        private readonly HomeViewModel? _homeViewModel;

        public InspectionCoordinator(
            Dictionary<string, IInspectionRunner> runners,
            MultiCameraImageStore imageStore,
            Dictionary<string, CameraViewModel> cameraViewModels,
            ImageLogger imageLogger,
            ImageCaptureCsvWriter csvWriter,
            TriggerSessionManager triggerSessionManager,
            PlcEventStore plcEventStore,
            PlcCommsManager plcCommsManager,
            HomeViewModel? homeViewModel = null)
        {
            _runners = runners;
            _imageStore = imageStore;
            _cameraViewModels = cameraViewModels;
            _imageLogger = imageLogger;
            _csvWriter = csvWriter;
            _triggerSessionManager = triggerSessionManager;
            _plcEventStore = plcEventStore;
            _plcCommsManager = plcCommsManager;
            _homeViewModel = homeViewModel;

            // Subscribe to all cameras for inspection, even if no runner is registered
            foreach (var cameraId in cameraViewModels.Keys)
            {
                if (_runners.TryGetValue(cameraId, out var runner))
                {
                    _imageStore.Subscribe(cameraId, async () => await HandleNewImage(cameraId, runner));
                }
                else
                {
                    // Subscribe for automatic inspection even without a runner
                    _imageStore.Subscribe(cameraId, async () => await HandleNewImageWithoutRunner(cameraId));
                }
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

            // Mark as inspecting
            if (_cameraViewModels.TryGetValue(cameraId, out var cameraViewModel))
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(() =>
                    {
                        cameraViewModel.IsInspecting = true;
                        cameraViewModel.InspectionMessage = "Processing...";
                    });
                }
                else
                {
                    cameraViewModel.IsInspecting = true;
                    cameraViewModel.InspectionMessage = "Processing...";
                }
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
                await HandleResult(cameraId, context, image);
            }
            finally
            {
                context?.Dispose();
            }
        }

        private async Task HandleNewImageWithoutRunner(string cameraId)
        {
            var image = _imageStore.GetImage(cameraId);
            if (image == null || !image.IsInitialized())
            {
                return;
            }

            // Mark as inspecting
            if (_cameraViewModels.TryGetValue(cameraId, out var cameraViewModel))
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(() =>
                    {
                        cameraViewModel.IsInspecting = true;
                        cameraViewModel.InspectionMessage = "Processing...";
                    });
                }
                else
                {
                    cameraViewModel.IsInspecting = true;
                    cameraViewModel.InspectionMessage = "Processing...";
                }
            }

            // Simulate inspection processing time
            await Task.Delay(100);

            // Perform simple inspection (placeholder - replace with your actual inspection logic)
            var context = new InspectionContext
            {
                Image = image.Clone(),
                CameraId = cameraId,
            };

            try
            {
                // TODO: Add your actual inspection logic here
                // For now, this is a placeholder that randomly determines good/bad
                // Replace this with your actual inspection algorithm
                bool passed = PerformInspection(image);
                context.InspectionResults["Passed"] = passed;
                context.InspectionResults["OverallPass"] = passed;

                await HandleResult(cameraId, context, image);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Inspection failed for camera '{cameraId}': {ex.Message}", ex);
                await HandleResult(cameraId, context, image);
            }
            finally
            {
                context?.Dispose();
            }
        }

        private bool PerformInspection(HImage image)
        {
            // Placeholder inspection logic
            // TODO: Replace with your actual inspection algorithm
            // This could check image quality, detect defects, analyze features, etc.

            try
            {
                if (image == null || !image.IsInitialized())
                    return false;

                // Example: Simple check - you can add more sophisticated logic
                // For demonstration, randomly return good/bad
                // In production, implement your actual inspection criteria
                var random = new Random();
                return random.Next(0, 100) > 30; // 70% pass rate for demo
            }
            catch
            {
                return false;
            }
        }

        private async Task HandleResult(string cameraId, InspectionContext context, HImage image)
        {
            if (!_cameraViewModels.TryGetValue(cameraId, out var cameraViewModel))
                return;

            // Extract inspection result from context
            bool? passed = null;
            string message = "Processing...";

            if (context.InspectionResults.TryGetValue("Passed", out var passedObj) && passedObj is bool passedValue)
            {
                passed = passedValue;
                message = passedValue ? "Inspection Passed" : "Inspection Failed";
            }
            else if (context.InspectionResults.TryGetValue("OverallPass", out var overallPassObj) && overallPassObj is bool overallPassValue)
            {
                passed = overallPassValue;
                message = overallPassValue ? "Inspection Passed" : "Inspection Failed";
            }
            else
            {
                // Default: simulate inspection result for demonstration
                // In real implementation, this would come from your inspection logic
                passed = true; // Placeholder - replace with actual inspection logic
                message = passed.Value ? "Inspection Passed" : "Inspection Failed";
            }

            // Update UI on the dispatcher thread
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() =>
                {
                    cameraViewModel.IsInspecting = false;
                    cameraViewModel.InspectionPassed = passed;
                    cameraViewModel.InspectionMessage = message;
                });
            }
            else
            {
                cameraViewModel.IsInspecting = false;
                cameraViewModel.InspectionPassed = passed;
                cameraViewModel.InspectionMessage = message;
            }

            // Log the image with inspection result
            if (image != null && image.IsInitialized() && passed.HasValue)
            {
                try
                {
                    long timestamp = _imageStore.GetTimestamp(cameraId);
                    string result = passed.Value ? "Good" : "Bad";
                    double confidence = passed.Value ? 1.0 : 0.0; // You can extract actual confidence from context if available

                    // Clone the image for logging to avoid disposal issues
                    HImage imageToLog = image.Clone();
                    string? imagePath = await _imageLogger.LogIfEnabledAsync(imageToLog, timestamp, cameraId, result, confidence, "tiff");

                    // Write to CSV if image was logged
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        // Get Pallet ID that was stored with the image at capture time
                        int palletId = _imageStore.GetPalletId(cameraId);
                        string tagId = palletId.ToString(); // Use pallet ID stored with image
                        await _csvWriter.WriteImageCaptureAsync(imagePath, timestamp, tagId);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Failed to log image for {cameraId}: {ex.Message}", ex);
                }
            }
        }

    }
}
