using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Helpers
{
    public static class AppBrand
    {
        public static string AppName => "ChepInlineApp";
        public static string AppFolderName => "ChepInlineApp";
        public static string Publisher => "AMV";
        public const string Version = "0.0";

        public static string InstallPath => $@"C:\{Publisher}\{AppFolderName}";
        public static string ProgramDataPath => $@"C:\ProgramData\{Publisher}\{AppFolderName}";
    }
}
