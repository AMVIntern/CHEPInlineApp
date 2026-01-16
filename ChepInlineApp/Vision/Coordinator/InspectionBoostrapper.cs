using ChepInlineApp.AppCycleManager;
using ChepInlineApp.Comms;
using ChepInlineApp.DataServices;
using ChepInlineApp.Helpers;
using ChepInlineApp.MetadataExporter.Services;
using ChepInlineApp.PLC.Enums;
using ChepInlineApp.PLC.Interfaces;
using ChepInlineApp.Stores;
using ChepInlineApp.ViewModels;
using ChepInlineApp.Vision.HalconProcedures;
using ChepInlineApp.Vision.Handlers.Interfaces;
using ChepInlineApp.Vision.Runners;
using System.IO;
using ChepInlineApp.Vision.Handlers.Steps;

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
            var resources = InitializeInspectionResources();

            var runners = new Dictionary<string, IInspectionRunner>()
            {
                {
                    "InfeedCam", new SequentialInspectionRunner(new IInspectionStep[]
                    {
                        new ClassifierInspectionStep("InfeedCam Inspection Step", resources.ClassifierModelPath),
                    })
                },
            };

            Coordinator = new InspectionCoordinator(runners, imageStore, cameraViewModels, imageLogger, csvWriter, _triggerSessionManager, _plcEventStore, plcCommsManager, homeViewModel);
        }
        private InspectionResources InitializeInspectionResources()
        {
            var classifierModelPath = Path.Combine(PathConfig.ModelsFolder, "best_efficientnet_b0_2Classes_Brigthness.onnx");
            if (!File.Exists(classifierModelPath))
                AppLogger.Error($"Model file not found at: {classifierModelPath}");

            return new InspectionResources
            {
                ClassifierModelPath = classifierModelPath,
            };
        }
    }
}
public class InspectionResources
{
    public string ClassifierModelPath { get; init; } = string.Empty;
}