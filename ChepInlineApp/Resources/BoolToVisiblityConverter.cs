using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace ChepInlineApp.Resources
{
    public class BoolToVisiblityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Handle null (covers null bool? values)
            if (value == null)
            {
                return Visibility.Collapsed;
            }

            // Handle bool (works for both bool and non-null bool? values)
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }

            return DependencyProperty.UnsetValue;
        }
    }

}
