using ChepInlineApp.Helpers;
using ChepInlineApp.Models;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.ImageSources
{
    public class CameraFrameGrabber
    {
        private readonly ImageAcquisitionModel _imageAcquisitionModel;

        public CameraFrameGrabber(ImageAcquisitionModel imageAcquisitionModel)
        {
            _imageAcquisitionModel = imageAcquisitionModel;
            // You can optionally start a frame grabber here
            // _imageAcquisitionModel.AcqHandleCam1 = StartFrameGrabber("StartFrameGrabberCam1");
        }

        // Start frame grabber using HDevelop procedure
        public HTuple StartFrameGrabber(string cameraName, string frameGrabberProcName = "StartFolderFrameGrabber")
        {
            HDevProcedure procedure = new HDevProcedure(frameGrabberProcName);
            HDevProcedureCall call = new HDevProcedureCall(procedure);
            try
            {
                call.SetInputCtrlParamTuple("CameraName", cameraName);
                call.Execute();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to start frame grabber '{frameGrabberProcName}'", ex);
            }
            return call.GetOutputCtrlParamTuple("AcqHandle");
        }

        // Close frame grabber using HDevelop procedure
        public void CloseFrameGrabber(HTuple acqHandle)
        {
            HDevProcedure procedure = new HDevProcedure("CloseFrameGrabber");
            HDevProcedureCall call = new HDevProcedureCall(procedure);

            call.SetInputCtrlParamTuple("AcqHandle", acqHandle);

            try
            {
                call.Execute();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to close frame grabber", ex);
            }
        }
    }
}
