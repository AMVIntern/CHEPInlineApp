using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Stores
{
    public class PlcEventStore
    {
        private int _currentPalletId = 0;
        private readonly object _palletIdLock = new object();

        public event Action<ushort>? TriggerDetected1;
        public void OnTriggerDetected1(ushort inspectionId)
        {
            TriggerDetected1?.Invoke(inspectionId);
        }

        public event Action<ushort>? TriggerDetected2;
        public void OnTriggerDetected2(ushort inspectionId)
        {
            TriggerDetected2?.Invoke(inspectionId);
        }

        public event Action<ushort>? TriggerDetected3;
        public void OnTriggerDetected3(ushort inspectionId)
        {
            TriggerDetected3?.Invoke(inspectionId);
        }

        public event Action<ushort>? TriggerDetected4;
        public void OnTriggerDetected4(ushort inspectionId)
        {
            TriggerDetected4?.Invoke(inspectionId);
        }

        public event Action<ushort>? TriggerDetected5;
        public void OnTriggerDetected5(ushort inspectionId)
        {
            TriggerDetected5?.Invoke(inspectionId);
        }

        public void SetCurrentPalletId(int palletId)
        {
            lock (_palletIdLock)
            {
                _currentPalletId = palletId;
            }
        }

        public int GetCurrentPalletId()
        {
            lock (_palletIdLock)
            {
                return _currentPalletId;
            }
        }
    }
}
