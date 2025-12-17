using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Helpers
{
    public class HalconDisposableHelper
    {
        public static void DisposeAndAssign<T>(ref T? oldValue, T? newValue) where T : class, IDisposable
        {
            if (!ReferenceEquals(oldValue, newValue))
            {
                oldValue?.Dispose();
                oldValue = newValue;
            }
        }
    }
}
