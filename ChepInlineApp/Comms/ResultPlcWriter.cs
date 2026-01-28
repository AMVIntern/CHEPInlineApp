using ChepInlineApp.Helpers;
using libplctag;
using System.Threading.Channels;

namespace ChepInlineApp.Comms
{
    public class ResultPlcWriter : IDisposable
    {
        private const string PlcIp = "192.168.1.2";
        private const string CipPath = "1,0";

        private const string ResultPalletIdTagName = "L1_TA_BC2_VisionResultPalletID"; // DINT
        private const string ResultValueTagName = "L1_TA_BC2_VisionResultValue";       // DINT

        private Tag? _resultPalletIdTag;
        private Tag? _resultValueTag;

        private bool _initialized;
        private readonly object _initLock = new object();

        private readonly Channel<ResultMessage> _channel;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task? _worker;

        // Optional: prevent duplicate writes for same pallet
        private int _lastWrittenPalletId = -1;

        public ResultPlcWriter()
        {
            _channel = Channel.CreateUnbounded<ResultMessage>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }

        public void Start()
        {
            if (_worker != null) return;
            _worker = Task.Run(WorkerLoopAsync);
        }

        /// <summary>
        /// Enqueue a result to write to PLC.
        /// resultValue: 1 = good (no inspection required), 2 = bad (inspection required)
        /// </summary>
        public ValueTask PublishAsync(int palletId, int resultValue)
        {
            return _channel.Writer.WriteAsync(new ResultMessage
            {
                PalletId = palletId,
                ResultValue = resultValue
            });
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;

            lock (_initLock)
            {
                if (_initialized) return;

                _resultPalletIdTag = new Tag
                {
                    Gateway = PlcIp,
                    Path = CipPath,
                    PlcType = PlcType.ControlLogix,
                    Protocol = Protocol.ab_eip,
                    Name = ResultPalletIdTagName,
                    ElementSize = 4,
                    ElementCount = 1,
                };

                _resultValueTag = new Tag
                {
                    Gateway = PlcIp,
                    Path = CipPath,
                    PlcType = PlcType.ControlLogix,
                    Protocol = Protocol.ab_eip,
                    Name = ResultValueTagName,
                    ElementSize = 4,
                    ElementCount = 1,
                };

                _resultPalletIdTag.Initialize();
                _resultValueTag.Initialize();

                _initialized = true;
                AppLogger.Info("[PLC] ResultPlcWriter initialized tags.");
            }
        }

        private async Task WorkerLoopAsync()
        {
            try
            {
                while (await _channel.Reader.WaitToReadAsync(_cts.Token))
                {
                    while (_channel.Reader.TryRead(out var msg))
                    {
                        try
                        {
                            EnsureInitialized();

                            if (msg.PalletId <= 0)
                            {
                                AppLogger.Info("[PLC] Skipping result write because PalletId is {PalletId}", msg.PalletId);
                                continue;
                            }

                            // Optional: only one write per pallet ID
                            if (msg.PalletId == _lastWrittenPalletId)
                            {
                                AppLogger.Info("[PLC] Skipping duplicate write for PalletId={PalletId}", msg.PalletId);
                                continue;
                            }


                            // 1) Write value first
                            _resultValueTag!.SetInt32(0, msg.ResultValue);
                            _resultValueTag.Write();

                            // 2) Write palletId last (PLC detects change here)
                            _resultPalletIdTag!.SetInt32(0, msg.PalletId);
                            _resultPalletIdTag.Write();

                            _lastWrittenPalletId = msg.PalletId;

                            AppLogger.Info("[PLC] Wrote VisionResult: PalletId={PalletId}, Value={Value}",
                                msg.PalletId, msg.ResultValue);
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Info("[Error:] PLC result write failed: {Error}", ex, ex.Message);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }

        public void Dispose()
        {
            _cts.Cancel();

            try { _resultPalletIdTag?.Dispose(); } catch { }
            try { _resultValueTag?.Dispose(); } catch { }

            _resultPalletIdTag = null;
            _resultValueTag = null;
        }

        private class ResultMessage
        {
            public int PalletId { get; set; }
            public int ResultValue { get; set; }
        }
    }
}
