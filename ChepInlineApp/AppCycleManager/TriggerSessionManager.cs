using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.AppCycleManager
{
    public class TriggerSessionManager
    {
        public string? _currentTriggerId;
        private long _lastTriggerTime = 0;
        private readonly object _lock = new();


        public string GetOrCreateTriggerId(long timestamp)
        {
            lock (_lock)
            {
                long rounded = 0;

                //rounded = timestamp - (timestamp % 500000); // 4-second window
                rounded = timestamp; // 4-second window

                string triggerId = $"cycle_{rounded}";

                if (_currentTriggerId != triggerId && AssignedTriggerBool == false)
                    _currentTriggerId = triggerId;

                return _currentTriggerId;
            }
        }

        public string CurrentTriggerId => _currentTriggerId!;
        public string AssignedTriggerId { get; set; }
        public bool AssignedTriggerBool { get; set; }
    }
}
