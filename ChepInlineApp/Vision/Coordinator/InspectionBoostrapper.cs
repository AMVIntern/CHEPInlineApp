using ChepInlineApp.AppCycleManager;
using ChepInlineApp.DataServices;
using ChepInlineApp.Stores;
using ChepInlineApp.ViewModels;
using ChepInlineApp.Vision.HalconProcedures;
using ChepInlineApp.Vision.Handlers.Interfaces;
using ChepInlineApp.Vision.Runners;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Vision.Coordinator
{
    public class InspectionBoostrapper
    {
        private readonly TriggerSessionManager _triggerSessionManager;
        private readonly SettingsViewModel _settingsViewModel;

        public InspectionCoordinator Coordinator { get; }

        public InspectionBoostrapper(MultiCameraImageStore imageStore, Dictionary<string, CameraViewModel> cameraViewModels, ImageLogger imageLogger, TriggerSessionManager triggerSessionManager, SettingsViewModel settingsViewModel, HomeViewModel? homeViewModel = null)
        {
            _triggerSessionManager = triggerSessionManager;
            _settingsViewModel = settingsViewModel;

            var runners = new Dictionary<string, IInspectionRunner>()
            {
            };

            Coordinator = new InspectionCoordinator(runners, imageStore, cameraViewModels, imageLogger, _triggerSessionManager, homeViewModel);
        }
    }
}
