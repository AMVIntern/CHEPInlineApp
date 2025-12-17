using ChepInlineApp.Base;
using ChepInlineApp.Enums;
using ChepInlineApp.Helpers;
using ChepInlineApp.Stores;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HalconDotNet;
using HalconDotNet;
using System.ComponentModel;
using System.Windows.Media;

namespace ChepInlineApp.ViewModels
{
    public partial class ImageViewModel : ViewModelBase, IDisposable
    {
        [ObservableProperty]
        private HImage? image;
        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private Brush borderBrush = Brushes.Gray;

        [ObservableProperty]
        private CameraStatus status;
        private readonly MultiCameraImageStore _imageStore;
        private readonly string _cameraId;
        public string CameraId => _cameraId;
        private readonly Action _navigateHomeAction;
        private CameraViewModel? _cameraVM;
        private HomeViewModel _homeViewModel;

        public ImageViewModel(MultiCameraImageStore imageStore, string title, string cameraId, Action navigateHomeAction,HomeViewModel homeViewModel)
        {
            _imageStore = imageStore;
            _cameraId = cameraId;
            Title = title;
            _navigateHomeAction = navigateHomeAction;
            _homeViewModel = homeViewModel;

            _imageStore.Subscribe(_cameraId, OnCamImageCaptured);
            Image = _imageStore.GetImage(_cameraId);

            _cameraVM = homeViewModel.GetCameraViewModel(_cameraId);
            if (_cameraVM != null)
            {
                BorderBrush = _cameraVM.BorderBrush;
                Status = _cameraVM.Status;
                _cameraVM.StatusChanged += OnStatusChanged;
            }
        }        
        private void OnStatusChanged()
        {
            if (_cameraVM != null)
                Status = _cameraVM.Status;
        }

        public void Dispose()
        {
            _imageStore.Unsubscribe(_cameraId, OnCamImageCaptured);
            if (_cameraVM != null)
            {
                _cameraVM.InspectionVisualsUpdated -= OnInspectionVisualsUpdated;
                _cameraVM.StatusChanged -= OnStatusChanged;
            }
        }

        private void OnInspectionVisualsUpdated()
        {
            BorderBrush = _cameraVM?.BorderBrush ?? Brushes.Gray;
        }

        private void OnCamImageCaptured()
        {
            var img = _imageStore.GetImage(_cameraId);

            if (img != null && img.IsInitialized())
            {
                Image = img;
            }
        }

        [RelayCommand]
        public void NavigateHome()
        {
            _navigateHomeAction.Invoke();
        }

        partial void OnImageChanging(HImage? value)
        {
            HalconDisposableHelper.DisposeAndAssign(ref image, value);
        }
    }
}
