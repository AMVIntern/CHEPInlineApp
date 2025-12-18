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
        private const int ReadDelayMs = 5;    // target poll period
        private const int MinLowMs = 15;    // must see LOW for this long to re-arm
        private const string PlcIp = "192.168.1.2";
        private const string CipPath = "1,0"; // backplane 1, slot 0
        private const string BaseTag = "CameraStation1_Control.AppTriggerIndex"; // .1 .. .5
        private const string PalletIdTag = "L1_TA_BC2_VisionTriggerPalletID"; // DINT tag for pallet ID

        // per-bit edge memory (index 0..4 => triggers 1..5)
        private readonly int[] _prev = new int[5];
        private readonly bool[] _armed = new bool[5];
        private readonly long[] _lastLowTicks = new long[5];  // Stopwatch ticks when we last saw a low
        private readonly DateTime[] _lastEdgeUtc = new DateTime[5];

        private volatile bool _synced = false; // becomes true only after a valid rising edge on trigger 1
        
        // Pallet ID tag
        private Tag? _palletIdTag;


        public PlcCommsManager(PlcEventStore triggerStore) => _triggerStore = triggerStore;

        public async Task Start()
        {
            // Give PLC time to settle after app boot
            await Task.Delay(3000);

            _cts = new CancellationTokenSource();

            var t = Task.Factory.StartNew(
                () => MonitorAllBitsLoop(_cts.Token),
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

        private async Task MonitorAllBitsLoop(CancellationToken token)
        {
            // Raise this thread’s priority a notch so polling is less jittery under load.
            try { Thread.CurrentThread.Priority = ThreadPriority.AboveNormal; } catch { /* best effort */ }

            // Build five BOOL tags: BaseTag.1 .. BaseTag.5
            var tags = new Tag[5];
            for (int i = 0; i < 5; i++)
            {
                tags[i] = new Tag
                {
                    Gateway = PlcIp,
                    Path = CipPath,
                    PlcType = PlcType.ControlLogix,
                    Protocol = Protocol.ab_eip,
                    Name = $"{BaseTag}.{i + 1}",
                    ElementSize = 1,
                    ElementCount = 1,
                };
            }

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
            long ToMs(long ticks) => (long)(ticks * 1000.0 / Stopwatch.Frequency);

            try
            {
                // Initialize tags and seed state
                for (int i = 0; i < 5; i++)
                {
                    var tag = tags[i];
                    tag.Initialize();
                    tag.Read();
                    if (tag.GetStatus() != 0)
                    {
                        _prev[i] = 0;
                        _armed[i] = false;  // will re-arm after sustained low
                        _lastLowTicks[i] = sw.ElapsedTicks;
                        System.Diagnostics.Debug.WriteLine($"[PLC] Init error on {tag.Name}: status {tag.GetStatus()}");
                        AppLogger.Info("[Error:] Init error on {Tag}: status {Status}", null, tag.Name, tag.GetStatus());
                    }
                    else
                    {
                        int initial = tag.GetBit(0) ? 1 : 0;
                        _prev[i] = initial;
                        _armed[i] = (initial == 0);
                        _lastLowTicks[i] = initial == 0 ? sw.ElapsedTicks : 0;
                        System.Diagnostics.Debug.WriteLine($"[PLC] Connected: {tag.Name} (initial={initial})");
                        AppLogger.Info("Connected: {Tag} (initial={Initial})", tag.Name, initial);
                    }
                }

                var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(ReadDelayMs));
                var lastTick = sw.ElapsedMilliseconds;

                while (!token.IsCancellationRequested)
                {
                    // Wait for the next “ideal” tick boundary; logs if the wakeup is late.
                    if (!await timer.WaitForNextTickAsync(token)) break;

                    var nowMs = sw.ElapsedMilliseconds;
                    var loopLag = nowMs - lastTick - ReadDelayMs;
                    if (loopLag > ReadDelayMs * 3)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PLC] Poll overrun +{loopLag}ms (target={ReadDelayMs}ms). System under load?");
                    }
                    lastTick = nowMs;

                    for (int i = 0; i < 5; i++)
                    {
                        var tag = tags[i];

                        try
                        {
                            tag.Read();
                            var st = tag.GetStatus();
                            if (st != 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"[PLC] Read error {tag.Name}: status {st} @ {DateTime.Now:HH:mm:ss.fff}");
                                //AppLogger.Info("Poll overrun +{LoopLag}ms (target={TargetMs}ms). System under load?", loopLag, ReadDelayMs);
                                continue;
                            }

                            int value = tag.GetBit(0) ? 1 : 0;

                            // Track sustained LOW to re-arm (anti-chatter)
                            if (value == 0)
                            {
                                if (_lastLowTicks[i] == 0)
                                    _lastLowTicks[i] = sw.ElapsedTicks;

                                var lowDur = ToMs(sw.ElapsedTicks - _lastLowTicks[i]);
                                if (lowDur >= MinLowMs)
                                    _armed[i] = true;
                            }
                            else
                            {
                                // went high - reset low window
                                _lastLowTicks[i] = 0;
                            }

                            // Rising edge?
                            //if (_prev[i] == 0 && value == 1 && _armed[i])
                            //{
                            //    _armed[i] = false; // require LOW again
                            //    _lastEdgeUtc[i] = DateTime.UtcNow;
                            //    var ts = DateTime.Now;
                            //    System.Diagnostics.Debug.WriteLine($"[PLC] Rising edge {tag.Name} (trigger {i + 1}) at {ts:HH:mm:ss.fff}");
                            //    RaiseEdge(i + 1);
                            //}

                            // inside the polling loop, where you detect rising edges:
                            if (_prev[i] == 0 && value == 1 && _armed[i])
                            {
                                _armed[i] = false; // require LOW again
                                _lastEdgeUtc[i] = DateTime.UtcNow;

                                // Read Pallet ID when trigger is detected
                                ReadPalletId();

                                // i == 0 -> trigger 1
                                if (i == 0)
                                {
                                    _synced = true;                // now we trust subsequent 2..5 edges
                                    var ts = DateTime.Now;
                                    Debug.WriteLine($"[PLC] Rising edge {tag.Name} (trigger 1) at {ts:HH:mm:ss.fff}");
                                    AppLogger.Info("Rising edge {Tag} (trigger 1) at {Ts:HH:mm:ss.fff}", tag.Name, ts);
                                    RaiseEdge(1);
                                }
                                else
                                {
                                    if (_synced)
                                    {
                                        var ts = DateTime.Now;
                                        Debug.WriteLine($"[PLC] Rising edge {tag.Name} (trigger {i + 1}) at {ts:HH:mm:ss.fff}");
                                        AppLogger.Info("Rising edge {Tag} (trigger {Trigger}) at {Ts:HH:mm:ss.fff}", tag.Name, i + 1, ts);
                                        RaiseEdge(i + 1);
                                    }
                                    else
                                    {
                                        // Drop 2..5 until we've seen a valid 1 after a sustained LOW
                                        // (prevents partial previous cycle from leaking into our first new cycle)
                                    }
                                }
                            }

                            _prev[i] = value;
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[PLC] Tag[{i}] exception: {ex.Message}");
                            //AppLogger.Info("[Error:] Tag[{Index}] exception: {Error}", ex, i, ex.Message);
                        }
                    }
                }
            }
            finally
            {
                foreach (var t in tags)
                {
                    try { t?.Dispose(); } catch { /* ignore */ }
                }
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

        private void RaiseEdge(int trig /*1..5*/)
        {
            switch (trig)
            {
                case 1: _triggerStore.OnTriggerDetected1(1); break;
                case 2: _triggerStore.OnTriggerDetected2(2); break;
                case 3: _triggerStore.OnTriggerDetected3(3); break;
                case 4: _triggerStore.OnTriggerDetected4(4); break;
                case 5: _triggerStore.OnTriggerDetected5(5); break;
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
