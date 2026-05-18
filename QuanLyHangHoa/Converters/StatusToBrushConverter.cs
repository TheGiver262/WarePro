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
                "đã tt" or "paid" or "posted" or "hoàn thành" or "đã trả" or "đã nhập" or "đã xuất" or "đã chuyển" or "instock" or "adjusted" or "đã kiểm kê" or "đã điều chỉnh" or "đã ghi sổ" => "SuccessTextBrush",
                "tt 1 phần" or "partial" or "đang sửa" or "counted" or "pending" or "đang kiểm kê" => "WarningTextBrush",
                "chưa tt" or "unpaid" or "quá hạn" or "overdue" or "lowstock" or "cancelled" or "deleted" or "scrapped" or "hủy" => "DangerTextBrush",
                "nháp" or "draft" or "tiếp nhận" => "NeutralTextBrush",
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
                "đã tt" or "paid" or "posted" or "hoàn thành" or "đã trả" or "đã nhập" or "đã xuất" or "đã chuyển" or "instock" or "adjusted" or "đã kiểm kê" or "đã điều chỉnh" or "đã ghi sổ" => "SuccessBgBrush",
                "tt 1 phần" or "partial" or "đang sửa" or "counted" or "pending" or "đang kiểm kê" => "WarningBgBrush",
                "chưa tt" or "unpaid" or "quá hạn" or "overdue" or "lowstock" or "cancelled" or "deleted" or "scrapped" or "hủy" => "DangerBgBrush",
                "nháp" or "draft" or "tiếp nhận" => "NeutralBgBrush",
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
                "posted" or "đã ghi sổ" => "Đã ghi sổ",
                "draft" or "nháp" => "Dự thảo",
                "counted" or "đã kiểm kê" => "Đã kiểm kê",
                "đang kiểm kê" => "Đang kiểm kê",
                "adjusted" or "đã cân bằng" or "đã điều chỉnh" => "Đã điều chỉnh",
                "pending" or "chờ duyệt" => "Chờ duyệt",
                "cancelled" or "deleted" or "hủy" => "Đã hủy",
                "hoàn thành" => "Hoàn thành",
                "đã trả" => "Đã trả khách",
                "đang sửa" => "Đang sửa",
                "tiếp nhận" => "Tiếp nhận",
                "đã nhập" => "Đã nhập kho",
                "đã xuất" => "Đã xuất kho",
                "đã điều chuyển" or "đã chuyển" => "Đã chuyển",
                "instock" => "Còn hàng",
                "lowstock" => "Sắp hết",
                "scrapped" => "Đã thanh lý",
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
                "đã tt" or "paid" or "posted" or "hoàn thành" or "đã trả" or "đã nhập" or "đã xuất" or "đã điều chuyển" or "đã chuyển" or "adjusted" or "đã kiểm kê" or "đã điều chỉnh" or "đã ghi sổ" => "SuccessTextBrush",
                "tt 1 phần" or "partial" or "đang sửa" or "counted" or "pending" or "đang kiểm kê" => "WarningTextBrush",
                "chưa tt" or "unpaid" or "quá hạn" or "overdue" or "cancelled" or "deleted" or "scrapped" or "hủy" => "DangerTextBrush",
                "nháp" or "draft" or "tiếp nhận" => "NeutralTextBrush",
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

