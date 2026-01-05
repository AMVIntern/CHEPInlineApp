using HalconDotNet;
using System;
using System.Globalization;
using System.Windows.Data;

namespace ChepInlineApp.Resources
{
    /// <summary>
    /// Converter that ensures only initialized HALCON images are passed to the binding
    /// </summary>
    public class HalconImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HImage image && image != null && image.IsInitialized())
            {
                return image;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HImage image && image != null && image.IsInitialized())
            {
                return image;
            }
            return null;
        }
    }
}
