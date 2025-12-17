using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Helpers
{
    public static class PathConfig
    {
        public static string BasePath => AppBrand.ProgramDataPath;
        public static string LogsFolder => Path.Combine(BasePath, "Logs");
        public static string HalconFolder => Path.Combine(BasePath, "Halcon");
        public static string ModelsFolder => Path.Combine(BasePath, "Models");
        public static string LocalImagePath => Path.Combine(BasePath, "Images");
        public static string RecipesFolder => Path.Combine(BasePath, "Recipes");
        public static string AppConfigFile => Path.Combine(BasePath, "Configurations", "AppConfig.json");

        static PathConfig()
        {
            Directory.CreateDirectory(LogsFolder);
            Directory.CreateDirectory(HalconFolder);
            Directory.CreateDirectory(ModelsFolder);
            Directory.CreateDirectory(LocalImagePath);
        }
    }
}
