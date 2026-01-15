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
        private const int frameIntervalSeconds = 5;
        private int _cycleFrameIndex;
        private readonly List<Task> _cameraTasks = new();

        [ObservableProperty]
        private int passCount = 0;

        [ObservableProperty]
        private int failCount = 0;

        [ObservableProperty]
        private int totalPalletsInspected = 0;

        private bool _wasInspecting = false;
        private bool? _lastCountedResult = null;
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
            
            // Initialize tracking state
            _wasInspecting = InfeedCam.IsInspecting;
            
            InfeedCam.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(InfeedCam.IsInspecting))
                {
                    // When a new inspection starts (was not inspecting, now inspecting)
                    // Reset the last counted result so we can count this new inspection
                    if (!_wasInspecting && InfeedCam.IsInspecting)
                    {
                        _lastCountedResult = null;
                    }
                    // When inspection completes (was inspecting, now not inspecting)
                    else if (_wasInspecting && !InfeedCam.IsInspecting && InfeedCam.InspectionPassed.HasValue)
                    {
                        // Count this inspection if we haven't counted it yet
                        // We check if the result is different OR if we haven't counted any result yet
                        if (_lastCountedResult == null || _lastCountedResult != InfeedCam.InspectionPassed.Value)
                        {
                            UpdateInspectionCounters();
                            _lastCountedResult = InfeedCam.InspectionPassed.Value;
                        }
                    }
                    _wasInspecting = InfeedCam.IsInspecting;
                }
                else if (e.PropertyName == nameof(InfeedCam.InspectionPassed))
                {
                    // When InspectionPassed is set and inspection is not in progress
                    // This handles cases where InspectionPassed is set after IsInspecting becomes false
                    if (!InfeedCam.IsInspecting && InfeedCam.InspectionPassed.HasValue)
                    {
                        // Count this inspection if we haven't counted it yet
                        if (_lastCountedResult == null || _lastCountedResult != InfeedCam.InspectionPassed.Value)
                        {
                            UpdateInspectionCounters();
                            _lastCountedResult = InfeedCam.InspectionPassed.Value;
                        }
                    }
                }
            };

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

        private void UpdateInspectionCounters()
        {
            // Count every time an inspection completes (when IsInspecting goes from true to false)
            // This ensures we count consecutive passes or fails, not just when the result changes
            if (InfeedCam.InspectionPassed.HasValue)
            {
                TotalPalletsInspected++;
                if (InfeedCam.InspectionPassed.Value)
                {
                    PassCount++;
                }
                else
                {
                    FailCount++;
                }
            }
        }

        [RelayCommand]
        public async Task ResetCounters()
        {
            bool confirm = await _modalStore.ShowConfirmationAsync("Reset Counters", "Are you sure you want to reset all inspection counters?");
            if (confirm == true)
            {
                PassCount = 0;
                FailCount = 0;
                TotalPalletsInspected = 0;
                _wasInspecting = InfeedCam.IsInspecting;
                _lastCountedResult = null;
                InfeedCam.InspectionPassed = null;
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
