using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace QuanLyHangHoa.Converters
{
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString() ?? "";
            
            return status switch
            {
                "Đã thanh toán" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")), // Emerald 500
                "Thanh toán một phần" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")), // Amber 500
                "Chưa thanh toán" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")), // Red 500
                "Quá hạn" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED")), // Violet 600
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")) // Gray 500
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
