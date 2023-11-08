using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace IgusSwarovskiHmi.Converters
{
    internal class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(parameter != null && parameter.ToString() == "Invert")
            {
                return (value is bool && (bool)value) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            }
            else
            {
                return (value is bool && (bool)value) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
