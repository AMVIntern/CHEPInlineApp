using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChepInlineApp.Resources
{
    public class InspectionStatusGlowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return Color.FromRgb(158, 158, 158); // Gray
            }

            if (value is bool status)
            {
                return status
                    ? Color.FromRgb(76, 175, 80)   // Green
                    : Color.FromRgb(244, 67, 54);  // Red
            }

            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}


