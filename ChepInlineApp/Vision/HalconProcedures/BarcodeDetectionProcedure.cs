using ChepInlineApp.Helpers;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Vision.HalconProcedures
{
    public class BarcodeDetectionProcedure
    {
        public static string[] GetBarcode(HImage image)
        {
            if (image == null || !image.IsInitialized())
            {
                AppLogger.Error("BarcodeDetectionProcedure: Image is null or not initialized");
                throw new ArgumentException("Cam1 image must be initialized", nameof(image));
            }
            try
            {
                HDevProcedure procedure = new HDevProcedure("ReadandOutputOCRDotPrint");
                HDevProcedureCall call = new HDevProcedureCall(procedure);

                call.SetInputIconicParamObject("Image", image);

                call.Execute();

                // Get output parameter
                HTuple barcodeChars = call.GetOutputCtrlParamTuple("Class");

                string[] result = barcodeChars.SArr;
                AppLogger.Info($"BarcodeDetectionProcedure:Bar code characters are = {result}%");

                return result;
            }
            catch (Exception ex)
            {
                AppLogger.Error("BarcodeDetectionProcedure: Failed to execute ReadandOutputOCRDotPrint procedure", ex);
                throw;
            }
        }
    }
}
