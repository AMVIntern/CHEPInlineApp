using ChepInlineApp.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using libplctag;
using ChepInlineApp.Stores;

namespace ChepInlineApp.Comms
{
    public class PlcCommsManager : IDisposable
    {
        private readonly PlcEventStore _triggerStore;

        private CancellationTokenSource? _cts;
        private readonly List<Task> _tasks = new();

        // ---- PLC config ----
        private const string PlcIp = "192.168.1.2";
        private const string CipPath = "1,0"; // backplane 1, slot 0
        private const string PalletIdTag = "L1_TA_BC2_VisionTriggerPalletID"; // DINT tag for pallet ID

        // Pallet ID tag
        private Tag? _palletIdTag;


        public PlcCommsManager(PlcEventStore triggerStore) => _triggerStore = triggerStore;

        public async Task Start()
        {
            // Give PLC time to settle after app boot
            await Task.Delay(3000);

            // Initialize Pallet ID tag for on-demand reading
            InitializePalletIdTag();

            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Initializes the Pallet ID tag for on-demand reading
        /// </summary>
        private void InitializePalletIdTag()
        {
            if (_palletIdTag != null) return; // Already initialized

            try
            {
                // Initialize Pallet ID tag (DINT = 32-bit integer)
                _palletIdTag = new Tag
                {
                    Gateway = PlcIp,
                    Path = CipPath,
                    PlcType = PlcType.ControlLogix,
                    Protocol = Protocol.ab_eip,
                    Name = PalletIdTag,
                    ElementSize = 4, // DINT is 4 bytes
                    ElementCount = 1,
                };

                _palletIdTag.Initialize();
                System.Diagnostics.Debug.WriteLine("[PLC] Pallet ID tag initialized for on-demand reading");
                AppLogger.Info("Pallet ID tag initialized for on-demand reading");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PLC] Failed to initialize Pallet ID tag: {ex.Message}");
                AppLogger.Info("[Error:] Failed to initialize Pallet ID tag: {Error}", ex, ex.Message);
            }
        }

        public int ReadPalletIdOnDemand()
        {
            // Initialize tag if not already done (lazy initialization)
            if (_palletIdTag == null)
            {
                InitializePalletIdTag();
                if (_palletIdTag == null)
                {
                    System.Diagnostics.Debug.WriteLine("[PLC] Pallet ID tag initialization failed");
                    return 0;
                }
            }

            try
            {
                _palletIdTag.Read();
                var status = _palletIdTag.GetStatus();
                if (status == 0)
                {
                    int palletId = _palletIdTag.GetInt32(0);
                    System.Diagnostics.Debug.WriteLine($"[PLC] Pallet ID read on demand: {palletId}");
                    AppLogger.Info("Pallet ID read on demand: {PalletId}", palletId);
                    return palletId;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[PLC] Failed to read Pallet ID on demand: status {status}");
                    AppLogger.Info("[Error:] Failed to read Pallet ID on demand: status {Status}", null, status);
                    return 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PLC] Exception reading Pallet ID on demand: {ex.Message}");
                AppLogger.Info("[Error:] Exception reading Pallet ID on demand: {Error}", ex, ex.Message);
                return 0;
            }
        }

        public void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            try { _palletIdTag?.Dispose(); } catch { /* ignore */ }
            _palletIdTag = null;
        }

        public void Dispose() => Stop();

        internal Task InitializeAsync() => Start();
    }
}
