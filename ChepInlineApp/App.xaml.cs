using ChepInlineApp.AppCycleManager;
using ChepInlineApp.Comms;
using ChepInlineApp.DataServices;
using ChepInlineApp.Helpers;
using ChepInlineApp.ImageSources;
using ChepInlineApp.Models;
using ChepInlineApp.Navigation.Stores;
using ChepInlineApp.Stores;
using ChepInlineApp.ViewModels;
using ChepInlineApp.Views;
using ChepInlineApp.Vision.Handlers.Core;
using HalconDotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Configuration;
using System.Data;
using System.Windows;

namespace ChepInlineApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IHost _host;
        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton<MainWindow>(provider => new MainWindow
                    {
                        DataContext = provider.GetRequiredService<MainWindowViewModel>()
                    })
                    .AddSingleton<MainWindowViewModel>()
                    .AddSingleton<NavigationStore>()
                    .AddSingleton<HomeViewModel>(provider =>
                    {
                        var navigationStore = provider.GetRequiredService<NavigationStore>();
                        var imageStore = provider.GetRequiredService<MultiCameraImageStore>();
                        var imageLogger = provider.GetRequiredService<ImageLogger>();
                        var imageAcquisitionModel = provider.GetRequiredService<ImageAcquisitionModel>();
                        var cameraFrameGrabber = provider.GetRequiredService<CameraFrameGrabber>();
                        var triggerSessionManager = provider.GetRequiredService<TriggerSessionManager>();
                        var modalStore = provider.GetRequiredService<ModalStore>();
                        var plcEventStore = provider.GetRequiredService<PlcEventStore>();   
                        return new HomeViewModel(
                            navigationStore,
                            imageStore,
                            imageLogger,
                            imageAcquisitionModel,
                            cameraFrameGrabber,
                            triggerSessionManager,
                            () => provider.GetRequiredService<SettingsViewModel>(),
                            modalStore,
                            plcEventStore);
                    })
                    .AddSingleton<MultiCameraImageStore>()
                    .AddSingleton<AppConfigModel>()
                    .AddSingleton<ImageLogger>()
                    .AddSingleton<ImageLoggingService>()
                    .AddSingleton<ChepInlineApp.MetadataExporter.Services.ImageCaptureCsvWriter>()
                    .AddSingleton<TriggerSessionManager>()
                    .AddSingleton<ImageAcquisitionModel>()
                    .AddSingleton<CameraFrameGrabber>()
                    .AddSingleton<ImageLoggingService>()
                    .AddSingleton<ImageLoggingService>()
                    .AddSingleton<SettingsViewModel>()
                    .AddSingleton<JSONDataService>()
                    .AddSingleton<ModalStore>()
                    .AddSingleton<InspectionContext>()
                    .AddSingleton<ImageAcquisitionModel>()
                    .AddSingleton<ImageAcquisitionViewModel>()
                    .AddSingleton<TriggerStore>()
                    .AddSingleton<PlcEventStore>()
                    .AddSingleton<PlcCommsManager>()
                    .AddSingleton<NavigationBarViewModel>(provider =>
                    {
                        var navigationStore = provider.GetRequiredService<NavigationStore>();
                        var modalStore = provider.GetRequiredService<ModalStore>();
                        return new NavigationBarViewModel(
                            navigationStore,
                            () => provider.GetRequiredService<HomeViewModel>(),
                            () => provider.GetRequiredService<SettingsViewModel>(),
                            modalStore);
                    });
                }).Build();
        }
        protected override async void OnStartup(StartupEventArgs e)
        {
            _ = _host.Services.GetRequiredService<ImageAcquisitionViewModel>();

            var settingsViewModel = _host.Services.GetRequiredService<SettingsViewModel>();
            await settingsViewModel.InitializeAsync();

            var navigationStore = _host.Services.GetRequiredService<NavigationStore>();
            var homeViewModel = _host.Services.GetRequiredService<HomeViewModel>();
            navigationStore.CurrentViewModel = homeViewModel;

            var mainWindow =  _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
            var plcCommsManager = _host.Services.GetRequiredService<PlcCommsManager>();
            await plcCommsManager.InitializeAsync();

            base.OnStartup(e);
        }
    }
}
