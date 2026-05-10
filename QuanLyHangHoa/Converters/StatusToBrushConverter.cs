using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace QuanLyHangHoa.Converters
{
    public class StatusToFgBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString()?.ToLower() ?? "";
            
            string brushKey = status switch
            {
                "đã tt" or "paid" => "SuccessTextBrush",
                "tt 1 phần" or "partial" => "WarningTextBrush",
                "chưa tt" or "unpaid" => "DangerTextBrush",
                "quá hạn" or "overdue" => "DangerTextBrush",
                _ => "NeutralTextBrush"
            };

            return Application.Current.TryFindResource(brushKey) as Brush ?? Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToBgBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString()?.ToLower() ?? "";
            
            string brushKey = status switch
            {
                "đã tt" or "paid" => "SuccessBgBrush",
                "tt 1 phần" or "partial" => "WarningBgBrush",
                "chưa tt" or "unpaid" => "DangerBgBrush",
                "quá hạn" or "overdue" => "DangerBgBrush",
                _ => "NeutralBgBrush"
            };

            return Application.Current.TryFindResource(brushKey) as Brush ?? Brushes.LightGray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString()?.ToLower() ?? "";

            return status switch
            {
                "đã tt" or "paid" => "Đã TT",
                "tt 1 phần" or "partial" => "TT 1 phần",
                "chưa tt" or "unpaid" => "Chưa TT",
                "quá hạn" or "overdue" => "Quá hạn",
                _ => value?.ToString() ?? ""
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
