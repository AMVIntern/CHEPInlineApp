using ChepInlineApp.Helpers;
using ChepInlineApp.Models;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ChepInlineApp.ViewModels
{
    public class ImageAcquisitionViewModel
    {
        private readonly ImageAcquisitionModel? _imageAcquisitionModel;
        private string HalconProcedurePath => PathConfig.HalconFolder;

        public ImageAcquisitionViewModel(ImageAcquisitionModel imageAcquisitionModel)
        {
            _imageAcquisitionModel = imageAcquisitionModel;
            _imageAcquisitionModel.Engine.SetProcedurePath(Path.GetFullPath(HalconProcedurePath));
        }
        public HImage GrabLocalImage(int imageIndex, string[] imagePath)
        {
            HImage Image;
            Image = new HImage(imagePath[imageIndex]);
            return Image;
        }
    }
}
