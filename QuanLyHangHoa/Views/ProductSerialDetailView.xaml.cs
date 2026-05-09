using System;
using System.Windows;
using System.Windows.Media;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Views
{
    public partial class ProductSerialDetailView : Window
    {
        public ProductSerialDetailView(ProductSerial serial)
        {
            InitializeComponent();
            PopulateData(serial);
        }

        private void PopulateData(ProductSerial serial)
        {
            if (serial == null) return;

            TxtSerial.Text = serial.SerialNumber;
            TxtProduct.Text = serial.Product?.DisplayName ?? "N/A";
            TxtBrand.Text = serial.Product?.Brand?.DisplayName ?? "N/A";
            TxtWarehouse.Text = serial.CurrentWarehouse?.DisplayName ?? "N/A";
            TxtNote.Text = string.IsNullOrWhiteSpace(serial.Note) ? "(\u004B\u0068\u00F4\u006E\u0067\u0020\u0063\u00F3\u0020\u0067\u0068\u0069\u0020\u0063\u0068\u00FA)" : serial.Note;
            
            // Status with color
            TxtStatus.Text = GetStatusDisplay(serial.CurrentStatus);
            BadgeStatus.Background = GetStatusBrush(serial.CurrentStatus);
            TxtStatus.Foreground = GetStatusTextBrush(serial.CurrentStatus);

            // Dates and Codes
            if (serial.LastStockInLine?.StockIn != null)
            {
                TxtDate.Text = serial.LastStockInLine.StockIn.CreatedAt.ToString("dd/MM/yyyy HH:mm");
                TxtImportCode.Text = serial.LastStockInLine.StockIn.DocumentCode;
            }
            else
            {
                TxtDate.Text = "N/A";
                TxtImportCode.Text = "N/A";
            }

            TxtExportCode.Text = serial.LastStockOutLine?.StockOut?.DocumentCode ?? "---";
        }

        private string GetStatusDisplay(string status)
        {
            return status switch
            {
                "InStock" => "Trong kho",
                "Sold" => "\u0110\u00E3 b\u00E1n",
                "Reserved" => "\u0110\u00E3 \u0111\u1EB7t",
                "InWarrantyProcess" => "\u0110ang b\u1EA3o h\u00E0nh",
                "WarrantyDefective" => "L\u1ED7i b\u1EA3o h\u00E0nh",
                "Returned" => "\u0110\u00E3 tr\u1EA3 h\u00E3ng",
                "ReturnedToManufacturer" => "\u0110\u00E3 tr\u1EA3 h\u00E3ng",
                "Scrapped" => "\u0110\u00E3 thanh l\u00FD",
                "Replaced" => "\u0110\u00E3 \u0111\u1ED5i m\u1EDBi",
                "Inactive" => "Ng\u1EEBng ho\u1EA1t \u0111\u1ED9ng",
                _ => status
            };
        }

        private Brush GetStatusBrush(string status)
        {
            return status switch
            {
                "InStock" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1FAE5")), // Green 100
                "Sold" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE")),    // Blue 100
                "Reserved" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")), // Amber 100
                "Returned" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")), // Slate 100
                "ReturnedToManufacturer" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                "Inactive" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")), // Gray 100
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"))         // Red 100
            };
        }

        private Brush GetStatusTextBrush(string status)
        {
            return status switch
            {
                "InStock" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#065F46")),
                "Sold" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E40AF")),
                "Reserved" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400E")),
                "Returned" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")),
                "ReturnedToManufacturer" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")),
                "Inactive" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"))
            };
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
