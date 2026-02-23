using System;
using System.Diagnostics;
using System.IO;
using Serilog;
using Serilog.Events;

namespace ChepInlineApp.Helpers
{
    public static class AppLogger
    {
        private static bool _initialized;
        private static readonly object _lock = new object();

        /// <summary>
        /// Call once at app startup (App.xaml.cs OnStartup).
        /// Safe: will not throw.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    Directory.CreateDirectory(PathConfig.LogsFolder);

                    string logPath = Path.Combine(PathConfig.LogsFolder, "ChepInlineApp-.log");

                    Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        .Enrich.FromLogContext()
                        .WriteTo.File(
                            path: logPath,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 14,
                            shared: true,
                            flushToDiskInterval: TimeSpan.FromSeconds(1),
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
                        )
                        .CreateLogger();

                    _initialized = true;

                    // Optional: helpful for local debugging
                    Debug.WriteLine($"[AppLogger] Initialized. Log folder: {PathConfig.LogsFolder}");
                    Info("[APP] Logger initialized.");
                }
                catch (Exception ex)
                {
                    // Do NOT throw. App must continue running.
                    _initialized = false;
                    Debug.WriteLine($"[AppLogger] Failed to initialize Serilog: {ex}");
                }
            }
        }

        /// <summary>
        /// Call on app exit (App.xaml.cs OnExit). Safe: will not throw.
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                Log.CloseAndFlush();
            }
            catch
            {
                // ignore
            }
        }

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
            Log.ForContext("SourceContext", "INFO").Information(message);
        }

        // INFO (template + args)
        public static void Info(string template, params object?[] args)
        {
            Log.ForContext("SourceContext", "INFO").Information(template, args);
        }

        // INFO (exception + template + args)
        public static void Info(Exception exception, string template, params object?[] args)
        {
            Log.ForContext("SourceContext", "INFO").Information(exception, template, args);
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
