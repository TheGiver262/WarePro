using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace QuanLyHangHoa.Converters
{
    public class BooleanToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool val)
            {
                // If parameter is "Color", return the brush/color
                if (parameter?.ToString() == "Color")
                {
                    return val ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
                }
                
                // Default return the icon kind
                return val ? PackIconKind.Check : PackIconKind.Close;
            }
            return PackIconKind.Help;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
