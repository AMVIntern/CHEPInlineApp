using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace ChepInlineApp.Helpers
{
    public static class AppLogger
    {
        private static readonly ILogger InfoLog = Log.ForContext("SourceContext", "INFO");
        public static void Halcon(string message)
        {
            Log.ForContext("SourceContext", "HALCON").Debug(message);
        }

        public static void Comms(string message)
        {
            Log.ForContext("SourceContext", "COMMS").Debug(message);
        }

        // INFO (plain string)
        public static void Info(string message)
        {
            InfoLog.Information(message);
        }

        // INFO (template + args)
        public static void Info(string template, params object?[] args)
        {
            InfoLog.Information(template, args);
        }

        // INFO (exception + template + args)
        public static void Info(Exception exception, string template, params object?[] args)
        {
            InfoLog.Information(exception, template, args);
        }
        public static void Error(string message, Exception? ex = null)
        {
            var logger = Log.ForContext("SourceContext", "ERROR");
            if (ex != null)
                logger.Error(ex, message);
            else
                logger.Error(message);
        }
    }
}
