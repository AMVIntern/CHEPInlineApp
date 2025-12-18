using ChepInlineApp.AppCycleManager;
using ChepInlineApp.Base;
using ChepInlineApp.Enums;
using ChepInlineApp.Helpers;
using ChepInlineApp.Navigation.Stores;
using ChepInlineApp.Stores;
using CommunityToolkit.Mvvm.ComponentModel;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace ChepInlineApp.ViewModels
{
    public partial class CameraViewModel: ViewModelBase,IDisposable
    {
        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private HImage? image;
        [ObservableProperty]
        private Brush borderBrush = Brushes.Gray;

        [ObservableProperty]
        private CameraStatus status = CameraStatus.Disconnected;
        public event Action? StatusChanged;
        private readonly string _cameraId;
        public string CameraId => _cameraId;
        
        [ObservableProperty]
        private bool showStatusIndicator = true;

        [ObservableProperty]
        private bool? inspectionPassed; // null = no result yet, true = good, false = bad

        [ObservableProperty]
        private bool isInspecting = false;

        [ObservableProperty]
        private string inspectionMessage = "Waiting for inspection...";

        private readonly HomeViewModel _homeViewModel;
        private readonly MultiCameraImageStore _imageStore;
        private readonly NavigationStore _navigationStore;
        public bool IsDisconnected => Status == CameraStatus.Disconnected;
        private readonly TriggerSessionManager _triggerSessionManager;
        public static readonly object TriggerRegistrationLock = new();
        public event Action? InspectionVisualsUpdated;

        public CameraViewModel(string cameraId,MultiCameraImageStore imageStore,NavigationStore navigationStore, HomeViewModel homeViewModel,
            TriggerSessionManager triggerSessionManager)
        {
            _cameraId = cameraId;
            _imageStore = imageStore;
            _navigationStore = navigationStore;
            _homeViewModel = homeViewModel;
            _triggerSessionManager = triggerSessionManager;
            Title = _imageStore.GetTitle(_cameraId);
            Image = _imageStore.GetImage(_cameraId);

            _imageStore.Subscribe(_cameraId, OnImageCaptured);
        }
        private void OnImageCaptured()
        {
            AppLogger.Info($"[{_cameraId}] inside on Image Captured");

            var newImage = _imageStore.GetImage(_cameraId);
            AppLogger.Info($"[{_cameraId}] Retrieved image from store. IsNull: {newImage == null}, IsInitialized: {newImage?.IsInitialized() ?? false}");

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() =>
                {
                    Image = newImage;
                    AppLogger.Info($"[{_cameraId}] Image property updated on UI thread. Image is now: {(Image?.IsInitialized() ?? false ? "Initialized" : "Null/Not Initialized")}");
                });
            }
            else
            {
                Image = newImage;
                AppLogger.Info($"[{_cameraId}] Image property updated (already on UI thread). Image is now: {(Image?.IsInitialized() ?? false ? "Initialized" : "Null/Not Initialized")}");
            }

        }
        partial void OnStatusChanged(CameraStatus oldValue, CameraStatus newValue)
        {
            OnPropertyChanged(nameof(IsDisconnected));
            StatusChanged?.Invoke();
        }
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
