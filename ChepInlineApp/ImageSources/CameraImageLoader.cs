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
            bool timeout = true;
            HImage hImage = new HImage();
            int retry = 1;
            while (timeout || retry==3)
            {
                try
                {
                    // After grab_image_start, use grab_image to get images continuously
                    HOperatorSet.GrabImage(out HObject img, acqHandle);
                    hImage = new HImage(img);
                    timeout = false;
                    return hImage;
                }
                catch (HalconException hex)
                {
                    //if (ex is HDevEngineException hdevEx)
                    //{
                    int errorCode = hex.GetErrorCode();

                    retry++;
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
                    //}
                    
                }
            }

                AppLogger.Error($"[CAPTURE:EXC] cam={_cameraId} ");
                Debug.WriteLine($"[HALCON] Error  on {_cameraId}: closing and restarting frame grabber");
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
            
            return hImage;

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
