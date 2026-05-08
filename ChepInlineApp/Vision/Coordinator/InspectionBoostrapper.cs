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

        public InspectionBoostrapper(MultiCameraImageStore imageStore, Dictionary<string, CameraViewModel> cameraViewModels, ImageLogger imageLogger, ImageCaptureCsvWriter csvWriter, TriggerSessionManager triggerSessionManager, PlcEventStore plcEventStore, PlcCommsManager plcCommsManager, ResultPlcWriter resultPlcWriter, SettingsViewModel settingsViewModel, HomeViewModel? homeViewModel = null)
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

                        new PalletCropYoloXStep("InfeedCam YOLOX Step", resources.PalletModelPath, resources.YoloxModelPath),

                        new CombineInspectionResultsStep("InfeedCam Inspection Step", null, "InfeedCam YOLOX Step"),
                    })
                },
            };

            Coordinator = new InspectionCoordinator(runners, imageStore, cameraViewModels, imageLogger, csvWriter, _triggerSessionManager, _plcEventStore, plcCommsManager, resultPlcWriter,homeViewModel);
        }
        private InspectionResources InitializeInspectionResources()
        {
            var classifierModelPath = Path.Combine(PathConfig.ModelsFolder, "best_efficientnet_b0_2Classes_Brigthness_19JAN.onnx");
            if (!File.Exists(classifierModelPath))
                AppLogger.Error($"Model file not found at: {classifierModelPath}");

            var patchCoreModelPath = Path.Combine(PathConfig.ModelsFolder, "model_layer2_res.onnx");
            if (!File.Exists(patchCoreModelPath))
                AppLogger.Error($"Model file not found at: {patchCoreModelPath}");

            var yoloxModelPath = Path.Combine(PathConfig.ModelsFolder, "yolox_m_infeed08MAY26.onnx");
            if (!File.Exists(yoloxModelPath))
                AppLogger.Error($"Model file not found at: {yoloxModelPath}");

            var palletModelPath = Path.Combine(PathConfig.ModelsFolder, "yolox_m_pallet07MAY26.onnx");
            if (!File.Exists(palletModelPath))
                AppLogger.Error($"Model file not found at: {palletModelPath}");

            return new InspectionResources
            {
                ClassifierModelPath = classifierModelPath,
                PatchCoreModelPath = patchCoreModelPath,
                YoloxModelPath = yoloxModelPath,
                PalletModelPath = palletModelPath
            };
        }
    }
}
public class InspectionResources
{
    public string ClassifierModelPath { get; init; } = string.Empty;
    public string PatchCoreModelPath { get; init; } = string.Empty;
    public string YoloxModelPath { get; init; } = string.Empty;
    public string PalletModelPath { get; init; } = string.Empty;
}