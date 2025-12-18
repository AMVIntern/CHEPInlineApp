using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChepInlineApp.Resources
{
    public class BooleanToBrushConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = Brushes.Green;
        public Brush FalseBrush { get; set; } = Brushes.Transparent;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueBrush : FalseBrush;
            }
            if (value == null)
            {
                return FalseBrush;
            }
            return FalseBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}



