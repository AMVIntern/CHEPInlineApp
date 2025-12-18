using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.PLC.Interfaces
{
    public interface IPlcCommsService
    {
        void Start();
        void Stop();
        void SendHeartbeat();
        void SendInspectionId(ushort id);
        void SendResult(bool passed);
        ushort ReadPlcAlive();
        ushort ReadFailBarcode();
        bool ReadTriggerPulse();
        ushort ReadInspectionId();
    }
}
