using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace QuanLyHangHoa.Converters
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class SafePackIconKindConverter : IValueConverter
    {
        // tên icon không hợp lệ trả Help để binding không ném lỗi khi dữ liệu cũ có giá trị lạ
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
