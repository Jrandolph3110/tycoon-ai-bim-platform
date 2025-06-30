using System;
using System.Globalization;
using System.Windows.Data;

namespace TycoonRevitAddin.UI
{
    /// <summary>
    /// Converts boolean to expander icon
    /// </summary>
    public class BoolToExpanderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? "▼" : "▶";
            }
            return "▶";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
