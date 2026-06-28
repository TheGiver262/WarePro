using System;
using System.Globalization;
using System.Windows.Data;

namespace QuanLyHangHoa.Converters
{
    public class CountedQuantityConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            if (value is decimal decValue)
            {
                if (decValue < 0) return string.Empty;
                return decValue.ToString("G29", culture);
            }

            return value.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var strValue = value as string;
            if (string.IsNullOrWhiteSpace(strValue))
            {
                if (targetType == typeof(decimal?))
                {
                    return (decimal?)null;
                }
                return -1m;
            }

            if (decimal.TryParse(strValue, NumberStyles.Any, culture, out decimal result))
            {
                if (targetType == typeof(decimal?))
                {
                    return (decimal?)result;
                }
                return result;
            }

            if (targetType == typeof(decimal?))
            {
                return (decimal?)null;
            }
            return -1m;
        }
    }
}
