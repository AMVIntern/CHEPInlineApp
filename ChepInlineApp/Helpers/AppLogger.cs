using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Helpers
{
    public static class AppLogger
    {
        public static void Halcon(string message)
        {
            Serilog.Log.ForContext("SourceContext", "HALCON").Debug(message);
        }

        public static void Comms(string message)
        {
            Serilog.Log.ForContext("SourceContext", "COMMS").Debug(message);
        }

        public static void Info(string message)
        {
            Serilog.Log.ForContext("SourceContext", "INFO").Information(message);
        }

        public static void Error(string message, Exception? ex = null)
        {
            var logger = Serilog.Log.ForContext("SourceContext", "ERROR");
            if (ex != null)
                logger.Error(ex, message);
            else
                logger.Error(message);
        }
    }
}
