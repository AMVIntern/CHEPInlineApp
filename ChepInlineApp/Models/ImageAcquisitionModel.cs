using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Models
{
    public class ImageAcquisitionModel
    {
        public HDevEngine Engine => new HDevEngine();
        public HImage Image { get; set; }
        public Dictionary<string, HTuple> AcqHandles { get; set; } = new();
        public Dictionary<string, double> RotateAngles { get; set; } = new();
        public int ImageID { get; set; } = 0;
        public bool InspectStart { get; set; } = true;

    }
}
