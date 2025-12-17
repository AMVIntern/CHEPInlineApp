using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Models
{
    public class AppConfigModel
    {
        public string CurrentRecipe { get; set; } = "Default";
        public bool LogAllImages { get; set; } = true;
        public bool ShouldLog(string cameraName, string result)
        {
            if (LogAllImages)
                return true;
            return false;
        }
    }
}
