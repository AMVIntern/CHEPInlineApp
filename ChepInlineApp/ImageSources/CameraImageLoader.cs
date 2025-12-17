using ChepInlineApp.DataServices;
using ChepInlineApp.Enums;
using ChepInlineApp.Helpers;
using ChepInlineApp.Interfaces;
using ChepInlineApp.Models;
using ChepInlineApp.Stores;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChepInlineApp.ImageSources
{
    public class CameraImageLoader : IImageSource
    {
        private readonly CameraFrameGrabber _cameraFrameGrabber;
        private readonly ImageAcquisitionModel _imageAcquisitionModel;
        private readonly MultiCameraImageStore _imageStore;
        private readonly string _cameraId;
        private readonly ImageLogger _imageLogger;
        private readonly Action<CameraStatus>? _setStatus;
        private CameraStatus? _lastStatus = null;
        private CancellationTokenSource? _continuousGrabbingCts;
        private const int GrabDelayMs = 20; // ~20 FPS delay after grabbing
        public string CameraId => _cameraId;

        public CameraImageLoader(
            CameraFrameGrabber cameraFrameGrabber,
            ImageAcquisitionModel imageAcquisitionModel,
            MultiCameraImageStore imageStore,
            string cameraId,
            ImageLogger imageLogger,
            Action<CameraStatus>? setStatus = null)
        {
            _cameraFrameGrabber = cameraFrameGrabber;
            _imageAcquisitionModel = imageAcquisitionModel;
            _imageStore = imageStore;
            _cameraId = cameraId;
            _imageLogger = imageLogger;
            _setStatus = setStatus;
        }

        public Task GrabNextFrameAsync()
        {
            try
            {
                var image = GrabImage(_imageAcquisitionModel.AcqHandles[_cameraId]);
                _imageStore.UpdateImage(_cameraId, image);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Live image acquisition failed for {_cameraId}", ex);
            }

            return Task.CompletedTask;
        }

        private HImage GrabImage(HTuple acqHandle)
        {
            bool timeout = true;
            HImage hImage = new HImage();

            while (timeout)
            {
                try
                {
                    HOperatorSet.GrabImage(out HObject img, acqHandle);
                    hImage = new HImage(img);
                    timeout = false;
                    AppLogger.Info($"[CAPTURE:OK] cam={_cameraId} via GrabImage");
                    Debug.WriteLine($"Image {_cameraId} Captured!");
                }
                catch (Exception ex)
                {
                    if (ex is HDevEngineException hdevEx)
                    {
                        int errorCode = hdevEx.HalconError;

                        if (errorCode == 5322)
                        {
                            AppLogger.Info($"[CAPTURE:TIMEOUT] cam={_cameraId} waiting for trigger");
                            Debug.WriteLine($"[HALCON] Timeout waiting for trigger ({_cameraId})");
                            timeout = true;
                        }
                        else
                        {
                            AppLogger.Error($"[CAPTURE:ERR] cam={_cameraId} halconError={errorCode} closing and restarting frame grabber");
                            Debug.WriteLine($"[HALCON] Error {errorCode} on {_cameraId}: closing and restarting frame grabber");
                            UpdateStatus(CameraStatus.Disconnected);
                            _cameraFrameGrabber.CloseFrameGrabber(acqHandle);
                            try
                            {
                                AppLogger.Info($"[CAPTURE:RESTARTED] cam={_cameraId}");
                                _imageAcquisitionModel.AcqHandles[_cameraId] = StartLiveCamera(_cameraId);
                                UpdateStatus(CameraStatus.Connected);
                            }
                            catch (Exception innerEx)
                            {
                                AppLogger.Error($"Failed to restart frame grabber for {_cameraId}", innerEx);
                            }
                            throw; // or swallow depending on your policy
                        }
                    }
                    else
                    {
                        AppLogger.Error($"[CAPTURE:EXC] cam={_cameraId} ex={ex.GetType().Name}");
                        Debug.WriteLine($"[HALCON] Error {ex} on {_cameraId}: closing and restarting frame grabber");
                        UpdateStatus(CameraStatus.Disconnected);
                        _cameraFrameGrabber.CloseFrameGrabber(acqHandle);
                        try
                        {
                            _imageAcquisitionModel.AcqHandles[_cameraId] = StartLiveCamera(_cameraId);
                            UpdateStatus(CameraStatus.Connected);
                            AppLogger.Info($"[CAPTURE:RESTARTED] cam={_cameraId}");
                        }
                        catch (Exception innerEx)
                        {
                            AppLogger.Error($"Failed to restart frame grabber for {_cameraId}", innerEx);
                        }
                        throw; // or swallow depending on your policy
                    }
                }
                finally
                {
                    //call.Dispose();
                    //proc.Dispose();
                }
            }

            return hImage;
        }

        private void UpdateStatus(CameraStatus newStatus)
        {
            if (_lastStatus != newStatus)
            {
                _setStatus?.Invoke(newStatus);
                _lastStatus = newStatus;
            }
        }

        public void StartContinuousGrabbing()
        {
            StopContinuousGrabbing(); // Stop any existing grabbing

            var acqHandle = _imageAcquisitionModel.AcqHandles[_cameraId];
            if (acqHandle == null || acqHandle.Length == 0)
            {
                AppLogger.Error($"Cannot start continuous grabbing for {_cameraId}: AcqHandle is invalid");
                return;
            }

            try
            {
                // Start the continuous grabbing loop
                _continuousGrabbingCts = new CancellationTokenSource();
                Task.Run(async () =>
                {
                    AppLogger.Info($"[ContinuousGrab] Starting continuous grabbing for {_cameraId}");
                    while (!_continuousGrabbingCts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var image = GrabImageContinuous(acqHandle);
                            if (image != null && image.IsInitialized())
                            {
                                _imageStore.UpdateImage(_cameraId, image);
                            }
                            
                            // Delay after grabbing
                            await Task.Delay(GrabDelayMs, _continuousGrabbingCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // Expected when stopping
                            break;
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Error($"[ContinuousGrab] Error grabbing frame for {_cameraId}: {ex.Message}", ex);
                            // Continue trying even if there's an error
                            await Task.Delay(GrabDelayMs, _continuousGrabbingCts.Token);
                        }
                    }
                    AppLogger.Info($"[ContinuousGrab] Stopped continuous grabbing for {_cameraId}");
                }, _continuousGrabbingCts.Token);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to start continuous grabbing for {_cameraId}", ex);
            }
        }

        public void StopContinuousGrabbing()
        {
            if (_continuousGrabbingCts != null)
            {
                _continuousGrabbingCts.Cancel();
                _continuousGrabbingCts.Dispose();
                _continuousGrabbingCts = null;
                AppLogger.Info($"[ContinuousGrab] Stopping continuous grabbing for {_cameraId}");
            }
        }

        private HImage GrabImageContinuous(HTuple acqHandle)
        {
            try
            {
                // After grab_image_start, use grab_image to get images continuously
                HOperatorSet.GrabImage(out HObject img, acqHandle);
                HImage hImage = new HImage(img);
                hImage.GetImageSize(out int width, out int height);
                Debug.WriteLine($"[{_cameraId}] Width={width}, Height={height}");
                _imageStore.SetCameraImageWidth(_cameraId, width);
                _imageStore.SetCameraImageHeight(_cameraId, height);
                
                // Calculate center points
                double centerX = width / 2.0;
                double centerY = height / 2.0;
                _imageStore.SetCameraCenter(_cameraId, centerX, centerY);
                Debug.WriteLine($"[{_cameraId}] CenterX={centerX}, CenterY={centerY}");
                
                // Draw dotted line through center
                hImage = DrawDottedCenterLine(hImage, width, height, centerX, centerY);
                
                AppLogger.Info($"[CAPTURE:OK] cam={_cameraId} via GrabImage (continuous)");
                return hImage;
            }
            catch (Exception ex)
            {
                if (ex is HDevEngineException hdevEx)
                {
                    int errorCode = hdevEx.HalconError;
                    AppLogger.Error($"[CAPTURE:ERR] cam={_cameraId} halconError={errorCode}");
                }
                else
                {
                    AppLogger.Error($"[CAPTURE:EXC] cam={_cameraId} ex={ex.GetType().Name}");
                }
                throw;
            }
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
            StopContinuousGrabbing();
        }

        public HTuple StartLiveCamera(string cameraId)
        {
            var call = new HDevProcedureCall(new HDevProcedure("StartCameraFrameGrabber"));
            call.SetInputCtrlParamTuple("CameraName", cameraId);
            call.Execute();
            return call.GetOutputCtrlParamTuple("AcqHandle");
        }
    }
}
