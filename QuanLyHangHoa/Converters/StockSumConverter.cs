using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Converters
{
    public class StockSumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<StockBalance> balances)
            {
                return balances.Sum(b => b.OnHandQuantity).ToString("N0");
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
