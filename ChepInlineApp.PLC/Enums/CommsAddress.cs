using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.PLC.Enums
{
    public enum CommsAddress
    {
        // Write Data
        WriteVisionHeartbeat = 0,        // 40001
        WriteInspectionId = 1,           // 40002
        WriteInspectionResult = 2,       // 40003

        // Read Data
        ReadPlcHeartbeat = 9,            // 40010
        ReadFailBarcode = 10,           // 40011

        ReadTriggerPulse = 29
    }

}
