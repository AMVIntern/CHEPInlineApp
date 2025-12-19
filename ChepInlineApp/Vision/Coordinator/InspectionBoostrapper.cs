using ChepInlineApp.AppCycleManager;
using ChepInlineApp.Comms;
using ChepInlineApp.DataServices;
using ChepInlineApp.MetadataExporter.Services;
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

        private readonly PlcEventStore _plcEventStore;

        public InspectionBoostrapper(MultiCameraImageStore imageStore, Dictionary<string, CameraViewModel> cameraViewModels, ImageLogger imageLogger, ImageCaptureCsvWriter csvWriter, TriggerSessionManager triggerSessionManager, PlcEventStore plcEventStore, PlcCommsManager plcCommsManager, SettingsViewModel settingsViewModel, HomeViewModel? homeViewModel = null)
        {
            _triggerSessionManager = triggerSessionManager;
            _settingsViewModel = settingsViewModel;
            _plcEventStore = plcEventStore;

            var runners = new Dictionary<string, IInspectionRunner>()
            {
            };

            Coordinator = new InspectionCoordinator(runners, imageStore, cameraViewModels, imageLogger, csvWriter, _triggerSessionManager, _plcEventStore, plcCommsManager, homeViewModel);
        }
    }
}
