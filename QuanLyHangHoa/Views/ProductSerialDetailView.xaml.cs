using System;
using System.Windows;
using System.Windows.Media;
using System.Linq;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Data;

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

            string invoiceCode = "---";
            if (serial.LastStockOutLine?.StockOut != null)
            {
                try
                {
                    using (var db = new AppDbContext())
                    {
                        var invoice = db.SalesInvoices.FirstOrDefault(si => si.StockOutId == serial.LastStockOutLine.StockOutId);
                        if (invoice != null)
                        {
                            invoiceCode = invoice.InvoiceCode;
                        }
                    }
                }
                catch
                {
                    // Fallback
                }
            }
            TxtInvoiceCode.Text = invoiceCode;
        }

        private string GetStatusDisplay(string status)
        {
            if (string.Equals(status, "ReturnedToManufacturer", StringComparison.OrdinalIgnoreCase))
                return "Trả lại NCC";

            return status switch
            {
                "InStock" => "Trong kho",
                "Sold" => "Đã bán",
                "Reserved" => "Đã đặt",
                "InWarrantyProcess" => "Đang bảo hành",
                "WarrantyDefective" => "Lỗi bảo hành",
                "Returned" => "Đã trả hàng",
                "ReturnedToManufacturer" => "Trả lại NCC",
                "Scrapped" => "Đã thanh lý",
                "Replaced" => "Đã đổi mới",
                "Inactive" => "Dừng",
                _ => status
            };
        }

        private Brush GetStatusBrush(string status)
        {
            if (string.Equals(status, "ReturnedToManufacturer", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")); // Amber 100

            return status switch
            {
                "InStock" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1FAE5")), // Green 100
                "Sold" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE")),    // Blue 100
                "Reserved" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")), // Amber 100
                "Returned" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")), // Slate 100
                "ReturnedToManufacturer" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")), // Amber 100
                "Inactive" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")), // Gray 100
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"))         // Red 100
            };
        }

        private Brush GetStatusTextBrush(string status)
        {
            if (string.Equals(status, "ReturnedToManufacturer", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706")); // WarningTextBrush

            return status switch
            {
                "InStock" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#065F46")),
                "Sold" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E40AF")),
                "Reserved" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400E")),
                "Returned" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")),
                "ReturnedToManufacturer" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706")), // WarningTextBrush
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
