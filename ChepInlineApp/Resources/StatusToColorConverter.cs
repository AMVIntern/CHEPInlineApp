using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using ChepInlineApp.Enums;

namespace ChepInlineApp.Resources
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CameraStatus status)
            {
                return status switch
                {
                    CameraStatus.Connected => new SolidColorBrush(Colors.Green),
                    CameraStatus.Disconnected => new SolidColorBrush(Colors.Red),
                    CameraStatus.Emulated => new SolidColorBrush(Colors.Gray),
                    _ => new SolidColorBrush(Colors.Transparent)
                };
            }

            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
