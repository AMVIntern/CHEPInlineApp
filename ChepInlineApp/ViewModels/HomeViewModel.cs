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
using System.Diagnostics;
using System.Windows;
using OpenCvSharp;
using System.IO;
using System.Linq;

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
        public CameraViewModel Station1_Cam1 { get; }
        public CameraViewModel Station1_Cam2 { get; }
        public CameraViewModel Station1_Cam3 { get; }
        public CameraViewModel Station1_Cam4 { get; }

        private readonly ImageAcquisitionModel _imageAcquisitionModel;
        private readonly CameraFrameGrabber _cameraFrameGrabber;
        private readonly TriggerSessionManager _triggerSessionManager;
        private readonly CancellationTokenSource _cts = new();

        [ObservableProperty]
        private string[]? barcodeResults;

        [ObservableProperty]
        private string barcodeDisplayText = "No barcode detected";
        private readonly System.Windows.Threading.DispatcherTimer _syncTimer;
        private const int frameIntervalSeconds = 2;
        private int _cycleFrameIndex;

        public HomeViewModel(NavigationStore navigationStore, MultiCameraImageStore imageStore, ImageLogger imageLogger, 
            ImageAcquisitionModel imageAcquisitionModel, CameraFrameGrabber cameraFrameGrabber, TriggerSessionManager triggerSessionManager,
            Func<SettingsViewModel> getSettingsViewModel, ModalStore modalStore)
        {
            _navigationStore = navigationStore;
            _imageStore = imageStore;
            _imageLogger = imageLogger;
            _imageAcquisitionModel = imageAcquisitionModel;
            _cameraFrameGrabber = cameraFrameGrabber;
            _triggerSessionManager = triggerSessionManager;
            _getSettingsViewModel = getSettingsViewModel;
            _modalStore = modalStore;

            imageStore.RegisterCamera("Station1_Cam1", "Camera 1");
            imageStore.RegisterCamera("Station1_Cam2", "Camera 2");
            imageStore.RegisterCamera("Station1_Cam3", "Camera 3");
            imageStore.RegisterCamera("Station1_Cam4", "Camera 4");

            Station1_Cam1 = new CameraViewModel("Station1_Cam1", _imageStore, _navigationStore, this, _triggerSessionManager);
            Station1_Cam2 = new CameraViewModel("Station1_Cam2", _imageStore, _navigationStore, this, _triggerSessionManager);
            Station1_Cam3 = new CameraViewModel("Station1_Cam3", _imageStore, _navigationStore, this, _triggerSessionManager);
            Station1_Cam4 = new CameraViewModel("Station1_Cam4", _imageStore, _navigationStore, this, _triggerSessionManager);

            if (AppEnvironment.IsOfflineMode)
            {
                _imageSources.Add(new FolderImageLoader(Path.Combine(PathConfig.LocalImagePath, "Station1_Cam1"), _imageStore, "Station1_Cam1", _imageLogger));
                _imageSources.Add(new FolderImageLoader(Path.Combine(PathConfig.LocalImagePath, "Station1_Cam2"), _imageStore, "Station1_Cam2", _imageLogger));
                _imageSources.Add(new FolderImageLoader(Path.Combine(PathConfig.LocalImagePath, "Station1_Cam3"), _imageStore, "Station1_Cam3", _imageLogger));
                _imageSources.Add(new FolderImageLoader(Path.Combine(PathConfig.LocalImagePath, "Station1_Cam4"), _imageStore, "Station1_Cam4", _imageLogger));

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
                TryRegisterCamera("Station1_Cam1", 0.0, Station1_Cam1);
                TryRegisterCamera("Station1_Cam2", 0.0, Station1_Cam2);
                TryRegisterCamera("Station1_Cam3", 0.0, Station1_Cam3);
                TryRegisterCamera("Station1_Cam4", 0.0, Station1_Cam4);
            }
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

                var handle = _cameraFrameGrabber.StartFrameGrabber(cameraId, "StartCameraFrameGrabberContinuous");

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
                        status => viewModel.Status = status
                    );
                    _imageSources.Add(loader);
                    _activeCameraIds.Add(cameraId);
                    
                    // Start continuous grabbing with grab_image_start
                    loader.StartContinuousGrabbing();
                    
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
        public CameraViewModel? GetCameraViewModel(string cameraId) => cameraId switch
        {
            "Station1_Cam1" => Station1_Cam1,
            "Station1_Cam2" => Station1_Cam2,
            "Station1_Cam3" => Station1_Cam3,
            "Station1_Cam4" => Station1_Cam4,
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
            // Stop all continuous grabbing
            foreach (var source in _imageSources)
            {
                if (source is CameraImageLoader loader)
                {
                    loader.StopContinuousGrabbing();
                }
            }

            // Cancel all operations
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
