using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Stores
{
    public class TriggerStore
    {
        private readonly SynchronizationContext _uiContext = SynchronizationContext.Current!;

        // Fire with ID
        public event Action<ushort>? TriggerReceived;

        public void FireTrigger(ushort inspectionId)
        {
            _uiContext.Post(_ => TriggerReceived?.Invoke(inspectionId), null);
        }
    }
}
