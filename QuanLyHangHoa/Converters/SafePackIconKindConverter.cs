using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace QuanLyHangHoa.Converters
{
    public class SafePackIconKindConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return PackIconKind.Help;
            
            string iconName = value.ToString() ?? string.Empty;
            if (Enum.TryParse(typeof(PackIconKind), iconName, out var kind))
            {
                return kind;
            }
            
            return PackIconKind.Help; // Default fallback icon
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
