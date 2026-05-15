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
                "đã tt" or "paid" or "posted" => "SuccessTextBrush",
                "tt 1 phần" or "partial" => "WarningTextBrush",
                "chưa tt" or "unpaid" => "DangerTextBrush",
                "quá hạn" or "overdue" => "DangerTextBrush",
                "nháp" or "draft" => "NeutralTextBrush",
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
                "đã tt" or "paid" or "posted" => "SuccessBgBrush",
                "tt 1 phần" or "partial" => "WarningBgBrush",
                "chưa tt" or "unpaid" => "DangerBgBrush",
                "quá hạn" or "overdue" => "DangerBgBrush",
                "nháp" or "draft" => "NeutralBgBrush",
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
                "posted" => "Đã ghi sổ",
                "draft" => "Phiếu nháp",
                _ => value?.ToString() ?? ""
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class StatusToEditVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString()?.ToLower() ?? "";
            return (status == "nháp" || status == "draft") ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter?.ToString() == "PrimaryIfTrue" && value is bool boolValue)
            {
                return boolValue
                    ? Application.Current.TryFindResource("AppTertiaryBrush") as Brush ?? Brushes.Indigo
                    : Application.Current.TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
            }

            string status = value?.ToString()?.ToLower() ?? "";
            string brushKey = status switch
            {
                "đã tt" or "paid" or "posted" => "SuccessTextBrush",
                "tt 1 phần" or "partial" => "WarningTextBrush",
                "chưa tt" or "unpaid" or "overdue" => "DangerTextBrush",
                "nháp" or "draft" => "NeutralTextBrush",
                _ => "TextSecondaryBrush"
            };

            return Application.Current.TryFindResource(brushKey) as Brush ?? Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
