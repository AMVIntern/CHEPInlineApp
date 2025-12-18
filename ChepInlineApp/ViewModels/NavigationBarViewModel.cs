using ChepInlineApp.Base;
using ChepInlineApp.Helpers;
using ChepInlineApp.Navigation.Stores;
using ChepInlineApp.Stores;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ChepInlineApp.ViewModels
{
    public partial class NavigationBarViewModel : ViewModelBase, IDisposable
    {
        private readonly NavigationStore _navigationStore;
        private readonly Func<HomeViewModel> _getHomeViewModel;
        private readonly Func<SettingsViewModel> _getSettingsViewModel;
        private readonly ModalStore _modalStore;
      //  private readonly UserStore _userStore;
      //  private readonly UserManagerViewModel _userManagerViewModel;
      //  public string LoginButtonLabel => _userStore.IsLoggedIn ? "Logout" : "Login";
        [ObservableProperty]
        private bool _isCollapsed = true;

        public Visibility ExpandImageVisbility => IsCollapsed ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CollapseImageVisibility => IsCollapsed ? Visibility.Collapsed : Visibility.Visible;

        [RelayCommand]
        public void ToggleNavigationBar()
        {
            IsCollapsed = !IsCollapsed;
        }
        partial void OnIsCollapsedChanged(bool value)
        {
            OnPropertyChanged(nameof(ExpandImageVisbility));
            OnPropertyChanged(nameof(CollapseImageVisibility));
        }

        [RelayCommand]
        public void HomeButton()
        {
            Debug.WriteLine("Home Button Clicked!");
            _navigationStore.CurrentViewModel = _getHomeViewModel();
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
            else
            {
                return;
            }
        }

        public NavigationBarViewModel(NavigationStore navigationStore, Func<HomeViewModel> getHomeViewModel,
                                    Func<SettingsViewModel> getSettingsViewModel, ModalStore modalStore)
        {
            AppLogger.Info("Navigation Bar Initializing...");
            _navigationStore = navigationStore;
            _getHomeViewModel = getHomeViewModel;
            _getSettingsViewModel = getSettingsViewModel;
            _modalStore = modalStore;
            _navigationStore.CurrentViewModelChanged += NavigationStore_CurrentViewModelChanged;
            AppLogger.Info("Navigation Bar Initialized!");
        }
        private void NavigationStore_CurrentViewModelChanged()
        {
            IsCollapsed = true;
        }

        public void Dispose()
        {
            _navigationStore.CurrentViewModelChanged -= NavigationStore_CurrentViewModelChanged;
        }
    }
}
