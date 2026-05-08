using ChepInlineApp.AppCycleManager;
using ChepInlineApp.Comms;
using ChepInlineApp.DataServices;
using ChepInlineApp.Helpers;
using ChepInlineApp.MetadataExporter.Services;
using ChepInlineApp.Stores;
using ChepInlineApp.ViewModels;
using ChepInlineApp.Vision.Handlers.Core;
using ChepInlineApp.Vision.Handlers.Interfaces;
using ChepInlineApp.Vision.Results;
using HalconDotNet;
using System.Diagnostics;
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
        private readonly ResultPlcWriter _resultPlcWriter;

        public InspectionCoordinator(
            Dictionary<string, IInspectionRunner> runners,
            MultiCameraImageStore imageStore,
            Dictionary<string, CameraViewModel> cameraViewModels,
            ImageLogger imageLogger,
            ImageCaptureCsvWriter csvWriter,
            TriggerSessionManager triggerSessionManager,
            PlcEventStore plcEventStore,
            PlcCommsManager plcCommsManager,
            ResultPlcWriter resultPlcWriter,
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
            _resultPlcWriter = resultPlcWriter;
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
                // For now, this is a placeholder that randomly determines Pass/Fail
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
                // For demonstration, randomly return Pass/Fail
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
            double confidence = 0.0;
            bool hasInspectionResult = false;

            // ✅ Ordered logging: Classifier -> PatchCore -> Overall
            void LogInspection(string key)
            {
                if (context.InspectionResults.TryGetValue(key, out var obj) && obj is InspectionResult r)
                {
                    AppLogger.Info($"[{cameraId}] {key}: {(r.Passed ? "Pass" : "Fail")}  Conf={r.Confidence:F4}");
                }
                else
                {
                    AppLogger.Info($"[{cameraId}] {key}: (no result)");
                }
            }

            string clsKey = $"{cameraId} Inspection Step";
            string pcKey = $"{cameraId} PatchCore Step";

            LogInspection(clsKey);
            LogInspection(pcKey);
            LogInspection("Overall");

            // ✅ Prefer deterministic Overall result if present (new Combine step will set this)
            InspectionResult? inspectionResult = null;

            if (context.InspectionResults.TryGetValue("Overall", out var overallObj) &&
                overallObj is InspectionResult overallRes)
            {
                inspectionResult = overallRes;
                hasInspectionResult = true;
            }
            else
            {
                // Fallback to existing behaviour (kept for safety)
                foreach (var kvp in context.InspectionResults)
                {
                    if (kvp.Value is InspectionResult result)
                    {
                        inspectionResult = result;
                        hasInspectionResult = true;
                        break; // Use the first InspectionResult found
                    }
                }
            }

            if (inspectionResult != null)
            {
                passed = inspectionResult.Passed;
                confidence = inspectionResult.Confidence;
                message = inspectionResult.Passed ? "Inspection Passed" : "Inspection Failed";
                AppLogger.Info($"[{cameraId}] USED result: {(inspectionResult.Passed ? "Pass" : "Fail")}, Conf={confidence:F4} (Name={inspectionResult.InspectionName})");
            }
            else if (context.InspectionResults.TryGetValue("Passed", out var passedObj) && passedObj is bool passedValue)
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

            // Send result to PLC (1 = good, 2 = bad) using pallet id from store
            if (passed.HasValue)
            {
                int palletId = _imageStore.GetPalletId(cameraId);

                int resultValue = passed.Value ? 1 : 2;

                await _resultPlcWriter.PublishAsync(palletId, resultValue);

                Debug.WriteLine($"[Result to PLC] Camera={cameraId}, PalletId={palletId}, Passed={passed.Value}, Value={resultValue}");

            }

            // Update UI on the dispatcher thread
            var dispatcher = Application.Current?.Dispatcher;

            // Show annotated image from YOLOX if available
            string yoloxStepKey = $"{cameraId} YOLOX Step";
            HalconDotNet.HImage? annotatedImage = null;
            if (context.InspectionResults.TryGetValue(yoloxStepKey, out var yoloxObj) &&
                yoloxObj is ChepInlineApp.Vision.Results.InspectionResult yoloxResult &&
                yoloxResult.ProcessedImage != null &&
                yoloxResult.ProcessedImage.IsInitialized())
            {
                annotatedImage = yoloxResult.ProcessedImage;
            }

            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() =>
                {
                    if (annotatedImage != null)
                        cameraViewModel.Image = annotatedImage;
                    cameraViewModel.InspectionPassed = passed;
                    cameraViewModel.IsInspecting = false;
                    cameraViewModel.InspectionMessage = message;
                });
            }
            else
            {
                if (annotatedImage != null)
                    cameraViewModel.Image = annotatedImage;
                cameraViewModel.InspectionPassed = passed;
                cameraViewModel.IsInspecting = false;
                cameraViewModel.InspectionMessage = message;
            }

            // Log the image with inspection result
            if (image != null && image.IsInitialized() && passed.HasValue)
            {
                try
                {
                    long timestamp = _imageStore.GetTimestamp(cameraId);
                    string result = passed.Value ? "Pass" : "Fail";
                    // Use confidence from InspectionResult if available, otherwise default based on passed status
                    if (!hasInspectionResult && passed.HasValue)
                    {
                        confidence = passed.Value ? 1.0 : 0.0;
                    }

                    // Clone the image for logging to avoid disposal issues
                    HImage imageToLog = image.Clone();
                    string? imagePath = await _imageLogger.LogIfEnabledAsync(imageToLog, timestamp, cameraId, result, confidence, "jpeg");

                    // Write to CSV if image was logged
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        // Get Pallet ID that was stored with the image at capture time
                        int palletId = _imageStore.GetPalletId(cameraId);
                        string tagId = palletId.ToString(); // Use pallet ID stored with image
                        await _csvWriter.WriteImageCaptureAsync(imagePath, timestamp, tagId, result, confidence);
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
