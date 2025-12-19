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
        private readonly ChepInlineApp.Comms.PlcCommsManager? _plcCommsManager;
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
            Action<CameraStatus>? setStatus = null,
            ChepInlineApp.Comms.PlcCommsManager? plcCommsManager = null)
        {
            _cameraFrameGrabber = cameraFrameGrabber;
            _imageAcquisitionModel = imageAcquisitionModel;
            _imageStore = imageStore;
            _cameraId = cameraId;
            _imageLogger = imageLogger;
            _setStatus = setStatus;
            _plcCommsManager = plcCommsManager;
        }

        public Task GrabNextFrameAsync()
        {
            try
            {
                var image = GrabImage(_imageAcquisitionModel.AcqHandles[_cameraId]);

                // Read Pallet ID from PLC on demand at the moment of image capture
                int palletId = _plcCommsManager?.ReadPalletIdOnDemand() ?? 0;

                _imageStore.UpdateImage(_cameraId, image, palletId);
                Debug.WriteLine($"[CAPTURE] Image captured for {_cameraId} with Pallet ID: {palletId}");
                AppLogger.Info($"[CAPTURE] Image captured for {_cameraId} with Pallet ID: {palletId}");
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
                    if (ex is HalconException hdevEx)
                    {
                        int errorCode = hdevEx.GetErrorCode();

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
        public HTuple StartLiveCamera(string cameraId)
        {
            var call = new HDevProcedureCall(new HDevProcedure("StartCameraFrameGrabber"));
            call.SetInputCtrlParamTuple("CameraName", cameraId);
            call.Execute();
            return call.GetOutputCtrlParamTuple("AcqHandle");
        }



        public void Dispose()
        {
        }


    }
}
