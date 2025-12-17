using ChepInlineApp.Base;
using ChepInlineApp.DataServices;
using ChepInlineApp.Helpers;
using ChepInlineApp.Helpers;
using ChepInlineApp.Models;
using ChepInlineApp.Navigation.Stores;
using ChepInlineApp.Stores;
using ChepInlineApp.Stores;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ChepInlineApp.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        private readonly HomeViewModel _homeViewModel;
        private readonly NavigationStore _navigationStore;
        [ObservableProperty]
        public bool isDefaultRecipeLoaded;
        public SettingsViewModel(HomeViewModel homeViewModel, NavigationStore navigationStore)
        {
            _homeViewModel = homeViewModel;
            _navigationStore = navigationStore;
        }
        public async Task InitializeAsync()
        {
        }
        [RelayCommand]
        public void HomeButton()
        {
            Debug.WriteLine("Home Button Clicked!");
            _navigationStore.CurrentViewModel = _homeViewModel;
        }
        [RelayCommand]
        public async Task SaveRecipe()
        {
        }
        public void Dispose()
        {
            
        }
    }
}
