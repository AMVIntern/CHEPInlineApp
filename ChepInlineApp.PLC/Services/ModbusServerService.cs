using ChepInlineApp.PLC.Enums;
using ChepInlineApp.PLC.Interfaces;
using NModbus;
using NModbus.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.PLC.Services
{
    public class ModbusServerService : IPlcCommsService
    {
        private TcpListener _listener;
        private IModbusSlaveNetwork _network;
        private bool _isRunning = false;
        private CancellationTokenSource _cts = new();

        private DefaultSlaveDataStore _dataStore;
        private ModbusFactory _factory;

        private ushort _heartbeatValue = 1;

        public ModbusServerService()
        {
            _factory = new ModbusFactory();
            _dataStore = new DefaultSlaveDataStore();
        }
        public ushort ReadFailBarcode()
        {
            return _dataStore.HoldingRegisters.ReadPoints(
                (ushort)CommsAddress.ReadFailBarcode, 1)[0];
        }

        public ushort ReadPlcAlive()
        {
            return _dataStore.HoldingRegisters.ReadPoints(
                (ushort)CommsAddress.ReadPlcHeartbeat, 1)[0];
        }

        public void SendHeartbeat()
        {
            _dataStore.HoldingRegisters.WritePoints(
                (ushort)CommsAddress.WriteVisionHeartbeat,
                new ushort[] { _heartbeatValue });

            _heartbeatValue = (ushort)(_heartbeatValue % 255 + 1);
            Debug.WriteLine($"Vision PC Heartbeat: {_heartbeatValue}");
        }

        public void SendInspectionId(ushort id)
        {
            _dataStore.HoldingRegisters.WritePoints(
            (ushort)CommsAddress.WriteInspectionId,
            new ushort[] { id });
        }

        public void SendResult(bool passed)
        {
            _dataStore.HoldingRegisters.WritePoints(
            (ushort)CommsAddress.WriteInspectionResult,
            new ushort[] { passed ? (ushort)1 : (ushort)0 });
        }

        public bool ReadTriggerPulse()
        {
            ushort value = _dataStore.HoldingRegisters.ReadPoints(29, 1)[0]; // <- try 29
            Debug.WriteLine($"Trigger register raw value: {value}");
            return value > 0;
        }

        public ushort ReadInspectionId()
        {
            return _dataStore.HoldingRegisters.ReadPoints(
                (ushort)CommsAddress.WriteInspectionId, 1)[0];
            // WriteInspectionId = 1 (40002)
        }

        public void Start()
        {
            if (_isRunning) return;

            _listener = new TcpListener(IPAddress.Any, 5020);
            _listener.Start();

            var slave = _factory.CreateSlave(1, _dataStore);
            _network = _factory.CreateSlaveNetwork(_listener);
            _network.AddSlave(slave);

            Task.Run(async () =>
            {
                try
                {
                    await _network.ListenAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Server shutdown requested.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Server error:" + ex.Message);
                }
            });
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _cts.Cancel();
            _listener.Stop();
            _isRunning = false;
        }
    }
}
