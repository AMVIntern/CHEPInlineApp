using ChepInlineApp.AppCycleManager;
using ChepInlineApp.Base;
using ChepInlineApp.DataServices;
using ChepInlineApp.Models;
using ChepInlineApp.Navigation.Stores;
using ChepInlineApp.Stores;
using ChepInlineApp.Vision.Coordinator;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly NavigationStore? _navigationStore;
        public ViewModelBase? CurrentViewModel => _navigationStore?.CurrentViewModel;
        private readonly HomeViewModel _homeViewModel;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly ModalStore _modalStore;
        public ViewModelBase? ModalViewModel => _modalStore.ModalViewModel;
        public bool IsModalOpen => _modalStore.IsModalOpen;
        private readonly MultiCameraImageStore _imageStore;
        private readonly ImageLogger _imageLogger;
        private readonly TriggerSessionManager _triggerSessionManager;

        public MainWindowViewModel(NavigationStore navigationStore, HomeViewModel homeViewModel,
            SettingsViewModel settingsViewModel,ModalStore modalStore, MultiCameraImageStore imageStore,ImageLogger imageLogger, TriggerSessionManager triggerSessionManager) 
        { 
            _navigationStore = navigationStore;
            _homeViewModel = homeViewModel;
            _modalStore = modalStore;
            _imageLogger = imageLogger;
            _triggerSessionManager = triggerSessionManager;
            _imageStore = imageStore;
            _navigationStore.CurrentViewModelChanged += NavigationStore_CurrentViewModelChanged;
            _modalStore.PropertyChanged += ModalStore_PropertyChanged;
            _settingsViewModel = settingsViewModel;
            _navigationStore.RetainViewModel(_settingsViewModel);
            _navigationStore.RetainViewModel(_homeViewModel);
            var cameraViewModels = new Dictionary<string, CameraViewModel>
        {
            { "Station1_Cam1", homeViewModel.Station1_Cam1 },
            { "Station1_Cam2", homeViewModel.Station1_Cam2 },
            { "Station1_Cam3", homeViewModel.Station1_Cam3 },
            { "Station1_Cam4", homeViewModel.Station1_Cam4 },

        };
          //  var bootstrapper = new InspectionBoostrapper(imageStore, cameraViewModels, imageLogger, triggerSessionManager, settingsViewModel, homeViewModel);
        }
        private void NavigationStore_CurrentViewModelChanged()
        {
            OnPropertyChanged(nameof(CurrentViewModel));
        }
        private void ModalStore_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModalStore.ModalViewModel) || e.PropertyName == nameof(ModalStore.IsModalOpen))
            {
                OnPropertyChanged(nameof(ModalViewModel));
                OnPropertyChanged(nameof(IsModalOpen));
            }
        }
        public void Dispose()
        {
            _modalStore.PropertyChanged -= ModalStore_PropertyChanged;
        }
    }
}
