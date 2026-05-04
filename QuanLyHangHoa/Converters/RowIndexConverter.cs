using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace QuanLyHangHoa.Converters
{
    public class RowIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return index + 1;
            }
            if (value is DataGridRow row)
            {
                return row.GetIndex() + 1;
            }
            return 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
