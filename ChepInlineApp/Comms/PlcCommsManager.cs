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
        private const int ReadDelayMs = 100;    // target poll period
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

            _cts = new CancellationTokenSource();

            var t = Task.Factory.StartNew(
                () => MonitorPalletIdLoop(_cts.Token),
                _cts.Token,
                TaskCreationOptions.LongRunning,   // dedicated thread
                TaskScheduler.Default);

            _tasks.Add(t);
            t.ContinueWith(tt =>
            {
                if (tt.IsFaulted)
                {
                    // Flatten to the root cause; Serilog will capture the full stack
                    var ex = tt.Exception?.Flatten().InnerException ?? tt.Exception!;
                    AppLogger.Info("PLC monitor ended with FAULT. Status={Status}", ex, tt.Status);
                }
                else if (tt.IsCanceled)
                {
                    AppLogger.Info("PLC monitor was CANCELED. Status={Status}", tt.Status);
                }
                else
                {
                    AppLogger.Info("PLC monitor ended OK. Status={Status}", tt.Status);
                }
            }, TaskContinuationOptions.ExecuteSynchronously);

        }

        private async Task MonitorPalletIdLoop(CancellationToken token)
        {
            // Raise this thread's priority a notch so polling is less jittery under load.
            try { Thread.CurrentThread.Priority = ThreadPriority.AboveNormal; } catch { /* best effort */ }

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

            try
            {
                _palletIdTag.Initialize();
                _palletIdTag.Read();
                if (_palletIdTag.GetStatus() == 0)
                {
                    int initialPalletId = _palletIdTag.GetInt32(0);
                    _triggerStore.SetCurrentPalletId(initialPalletId);
                    System.Diagnostics.Debug.WriteLine($"[PLC] Pallet ID tag initialized: {initialPalletId}");
                    AppLogger.Info("Pallet ID tag initialized: {PalletId}", initialPalletId);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[PLC] Init error on Pallet ID tag: status {_palletIdTag.GetStatus()}");
                    AppLogger.Info("[Error:] Init error on Pallet ID tag: status {Status}", null, _palletIdTag.GetStatus());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PLC] Failed to initialize Pallet ID tag: {ex.Message}");
                AppLogger.Info("[Error:] Failed to initialize Pallet ID tag: {Error}", ex, ex.Message);
            }

            var sw = Stopwatch.StartNew();

            try
            {
                var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(ReadDelayMs));
                var lastTick = sw.ElapsedMilliseconds;

                while (!token.IsCancellationRequested)
                {
                    // Wait for the next "ideal" tick boundary; logs if the wakeup is late.
                    if (!await timer.WaitForNextTickAsync(token)) break;

                    var nowMs = sw.ElapsedMilliseconds;
                    var loopLag = nowMs - lastTick - ReadDelayMs;
                    if (loopLag > ReadDelayMs * 3)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PLC] Poll overrun +{loopLag}ms (target={ReadDelayMs}ms). System under load?");
                    }
                    lastTick = nowMs;

                    // Read Pallet ID tag
                    ReadPalletId();
                }
            }
            finally
            {
                try { _palletIdTag?.Dispose(); } catch { /* ignore */ }
                System.Diagnostics.Debug.WriteLine("[PLC] Monitoring stopped.");
                AppLogger.Info("Monitoring stopped.");
            }
        }

        private void ReadPalletId()
        {
            if (_palletIdTag == null) return;

            try
            {
                _palletIdTag.Read();
                var status = _palletIdTag.GetStatus();
                if (status == 0)
                {
                    int palletId = _palletIdTag.GetInt32(0);
                    _triggerStore.SetCurrentPalletId(palletId);
                    System.Diagnostics.Debug.WriteLine($"[PLC] Pallet ID read: {palletId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[PLC] Failed to read Pallet ID: status {status}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PLC] Exception reading Pallet ID: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads the current Pallet ID from PLC on demand and updates the store.
        /// Returns the pallet ID, or -1 if read failed.
        /// </summary>
        public int ReadPalletIdOnDemand()
        {
            if (_palletIdTag == null)
            {
                System.Diagnostics.Debug.WriteLine("[PLC] Pallet ID tag not initialized");
                return -1;
            }

            try
            {
                _palletIdTag.Read();
                var status = _palletIdTag.GetStatus();
                if (status == 0)
                {
                    int palletId = _palletIdTag.GetInt32(0);
                    _triggerStore.SetCurrentPalletId(palletId);
                    System.Diagnostics.Debug.WriteLine($"[PLC] Pallet ID read on demand: {palletId}");
                    AppLogger.Info("Pallet ID read on demand: {PalletId}", palletId);
                    return palletId;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[PLC] Failed to read Pallet ID on demand: status {status}");
                    AppLogger.Info("[Error:] Failed to read Pallet ID on demand: status {Status}", null, status);
                    return -1;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PLC] Exception reading Pallet ID on demand: {ex.Message}");
                AppLogger.Info("[Error:] Exception reading Pallet ID on demand: {Error}", ex, ex.Message);
                return -1;
            }
        }

        public void Stop()
        {
            if (_cts == null) return;
            _cts.Cancel();
            try { Task.WaitAll(_tasks.ToArray(), TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
            _tasks.Clear();
            _cts.Dispose();
            _cts = null;
        }

        public void Dispose() => Stop();

        internal Task InitializeAsync() => Start();
    }
}
