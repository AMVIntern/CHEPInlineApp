using ChepInlineApp.AppCycleManager;
using ChepInlineApp.Base;
using ChepInlineApp.DataServices;
using ChepInlineApp.Enums;
using ChepInlineApp.Helpers;
using ChepInlineApp.ImageSources;
using ChepInlineApp.Interfaces;
using ChepInlineApp.Models;
using ChepInlineApp.Navigation.Stores;
using ChepInlineApp.Stores;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Windows;

namespace ChepInlineApp.ViewModels
{
    public partial class HomeViewModel : ViewModelBase, IDisposable
    {
        private readonly List<IImageSource> _imageSources = new();
        private readonly HashSet<string> _activeCameraIds = new();
        private readonly MultiCameraImageStore _imageStore;
        private readonly NavigationStore _navigationStore;
        private readonly ImageLogger _imageLogger;
        private readonly Func<SettingsViewModel> _getSettingsViewModel;
        private readonly ModalStore _modalStore;
        public CameraViewModel InfeedCam { get; }
        public CameraViewModel Station1_Cam2 { get; }
        public CameraViewModel Station1_Cam3 { get; }
        public CameraViewModel Station1_Cam4 { get; }

        private readonly ImageAcquisitionModel _imageAcquisitionModel;
        private readonly CameraFrameGrabber _cameraFrameGrabber;
        private readonly TriggerSessionManager _triggerSessionManager;
        private readonly CancellationTokenSource _cts = new();
        private readonly PlcEventStore _plcEventStore;
        private readonly ChepInlineApp.Comms.PlcCommsManager? _plcCommsManager;
        [ObservableProperty]
        private string[]? barcodeResults;

        [ObservableProperty]
        private string barcodeDisplayText = "No barcode detected";
        private readonly System.Windows.Threading.DispatcherTimer _syncTimer;
        private const int frameIntervalSeconds = 2;
        private int _cycleFrameIndex;
        private readonly List<Task> _cameraTasks = new();
        public HomeViewModel(NavigationStore navigationStore, MultiCameraImageStore imageStore, ImageLogger imageLogger,
            ImageAcquisitionModel imageAcquisitionModel, CameraFrameGrabber cameraFrameGrabber, TriggerSessionManager triggerSessionManager,
            Func<SettingsViewModel> getSettingsViewModel, ModalStore modalStore, PlcEventStore plcEventStore,
            ChepInlineApp.Comms.PlcCommsManager? plcCommsManager = null)
        {
            _navigationStore = navigationStore;
            _imageStore = imageStore;
            _imageLogger = imageLogger;
            _imageAcquisitionModel = imageAcquisitionModel;
            _cameraFrameGrabber = cameraFrameGrabber;
            _triggerSessionManager = triggerSessionManager;
            _getSettingsViewModel = getSettingsViewModel;
            _modalStore = modalStore;
            _plcEventStore = plcEventStore;
            _plcCommsManager = plcCommsManager;

            imageStore.RegisterCamera("InfeedCam", "Infeed Camera");

            InfeedCam = new CameraViewModel("InfeedCam", _imageStore, _navigationStore, this, _triggerSessionManager);
            Station1_Cam2 = new CameraViewModel("InfeedCam", _imageStore, _navigationStore, this, _triggerSessionManager); // Keep for compatibility
            Station1_Cam3 = new CameraViewModel("InfeedCam", _imageStore, _navigationStore, this, _triggerSessionManager); // Keep for compatibility
            Station1_Cam4 = new CameraViewModel("InfeedCam", _imageStore, _navigationStore, this, _triggerSessionManager); // Keep for compatibility

            if (AppEnvironment.IsOfflineMode)
            {
                _imageSources.Add(new FolderImageLoader(Path.Combine(PathConfig.LocalImagePath, "InfeedCam"), _imageStore, "InfeedCam", _imageLogger));

                _syncTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(frameIntervalSeconds)
                };

                _syncTimer.Tick += async (s, e) =>
                {
                    foreach (var source in _imageSources)
                    {
                        await source.GrabNextFrameAsync();
                    }
                    _cycleFrameIndex = (_cycleFrameIndex + 1) % 3;
                };

                _syncTimer.Start();
            }
            else
            {
                TryRegisterCamera("InfeedCam", 0.0, InfeedCam);
            }

            WirePlcHandlers();
        }
        private readonly Channel<(ushort trigger, DateTime ts)> _triggerQueue =
    Channel.CreateUnbounded<(ushort, DateTime)>(new UnboundedChannelOptions
    {
        SingleWriter = false,
        SingleReader = true
    });

        // Call this once during startup (after you created PlcEventStore and subscribed before)
        private void WirePlcHandlers()
        {
            // Unify: push only the trigger id; time is for diagnostics
            _plcEventStore.TriggerDetected1 += id => _triggerQueue.Writer.TryWrite((id, DateTime.Now));
            _plcEventStore.TriggerDetected2 += id => _triggerQueue.Writer.TryWrite((id, DateTime.Now));
            _plcEventStore.TriggerDetected3 += id => _triggerQueue.Writer.TryWrite((id, DateTime.Now));
            _plcEventStore.TriggerDetected4 += id => _triggerQueue.Writer.TryWrite((id, DateTime.Now));
            _plcEventStore.TriggerDetected5 += id => _triggerQueue.Writer.TryWrite((id, DateTime.Now));

            // Start the single reader loop
            //_ = Task.Run(ProcessTriggersAsync);
        }
        private void TryRegisterCamera(string cameraId, double rotation, CameraViewModel viewModel)
        {
            _imageAcquisitionModel.RotateAngles[cameraId] = rotation;
            viewModel.Status = CameraStatus.Disconnected;

            var connected = TryRegisterCameraOnce(cameraId, viewModel);

            if (!connected)
                StartCameraReconnectLoop(cameraId, viewModel);
        }
        private bool TryRegisterCameraOnce(string cameraId, CameraViewModel viewModel)
        {
            try
            {
                if (_activeCameraIds.Contains(cameraId))
                    return false;

                var handle = _cameraFrameGrabber.StartFrameGrabber(cameraId, "StartCameraFrameGrabber");

                if (handle != null && handle.Length > 0)
                {
                    _imageAcquisitionModel.AcqHandles[cameraId] = handle;
                    viewModel.Status = CameraStatus.Connected;

                    var loader = new CameraImageLoader(
                        _cameraFrameGrabber,
                        _imageAcquisitionModel,
                        _imageStore,
                        cameraId,
                        _imageLogger,
                        status => viewModel.Status = status,
                        _plcCommsManager
                    );
                    _imageSources.Add(loader);
                    _activeCameraIds.Add(cameraId);

                    StartGrabbingForLoader(loader);

                    return true;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Initial camera connect failed for {cameraId}: {ex.Message}");
            }

            return false;
        }
        private void StartCameraReconnectLoop(string cameraId, CameraViewModel viewModel)
        {
            Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    AppLogger.Info($"Retrying camera {cameraId}...");

                    var success = TryRegisterCameraOnce(cameraId, viewModel);
                    if (success) break;

                    await Task.Delay(2000);
                }
            });
        }
        private void StartGrabbingForLoader(IImageSource loader)
        {
            var task = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        await loader.GrabNextFrameAsync();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error("GrabNextFrameAsync failed", ex);
                    }
                    await Task.Delay(1000);
                }
            }, _cts.Token);

            _cameraTasks.Add(task);
        }
        public CameraViewModel? GetCameraViewModel(string cameraId) => cameraId switch
        {
            "InfeedCam" => InfeedCam,
            "Station1_Cam2" => InfeedCam, // Map to single camera
            "Station1_Cam3" => InfeedCam, // Map to single camera
            "Station1_Cam4" => InfeedCam, // Map to single camera
            _ => null
        };

        [RelayCommand]
        public void HomeButton()
        {
            Debug.WriteLine("Home Button Clicked!");
            _navigationStore.CurrentViewModel = this;
        }

        [RelayCommand]
        public void RecipeManagerButton()
        {
            Debug.WriteLine("Recipe Manager Button Clicked!");
            _navigationStore.CurrentViewModel = _getSettingsViewModel();
        }

        [RelayCommand]
        public async Task ExitApplicationButton()
        {
            bool confirm = await _modalStore.ShowConfirmationAsync("Exit Application!", "Are you sure you want to exit?");
            if (confirm == true)
            {
                Application.Current.Shutdown();
            }
        }

        public void Dispose()
        {
            // Cancel all operations
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
