using System;
using System.Globalization;
using System.Windows.Data;

namespace ChepInlineApp.Resources
{
    public class InspectionStatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return "PROCESSING...";
            }

            if (value is bool status)
            {
                return status ? "GOOD" : "BAD";
            }

            return "WAITING...";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}


