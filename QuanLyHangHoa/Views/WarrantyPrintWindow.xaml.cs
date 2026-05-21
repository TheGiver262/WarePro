using System;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Views
{
    public partial class WarrantyPrintWindow : Window
    {
        public WarrantyPrintWindow(WarrantyClaim claim)
        {
            InitializeComponent();
            PopulateData(claim);
        }

        private void PopulateData(WarrantyClaim claim)
        {
            // Claim Code
            ClaimCodeText.Text = claim.ClaimCode;

            // Customer Info
            var customer = claim.WarrantyCoverage?.Customer;
            CustomerNameRun.Text = customer?.DisplayName ?? "—";
            CustomerPhoneRun.Text = customer?.Phone ?? "—";
            ReceivedDateRun.Text = claim.ReceivedDate.ToString("dd/MM/yyyy");
            ExpectedReturnDateRun.Text = claim.ExpectedReturnDate?.ToString("dd/MM/yyyy") ?? "—";

            // Product Info
            var serial = claim.ProductSerial;
            ProductNameRun.Text = serial?.Product?.DisplayName ?? "—";
            SerialNumberRun.Text = serial?.SerialNumber ?? "—";
            WarrantyEndDateRun.Text = claim.WarrantyCoverage?.WarrantyEndDate.ToString("dd/MM/yyyy") ?? "—";

            // Status mapping
            StatusRun.Text = claim.Status switch
            {
                "Open" => "Tiếp nhận",
                "Ready" => "Chờ trả khách",
                "ManufacturerWait" => "Chờ hãng",
                "Closed" => "Đã đóng",
                "Rejected" => "Từ chối",
                _ => claim.Status
            };

            // Problem & Conclusion
            ProblemText.Text = claim.ProblemDescription ?? "—";
            ConclusionText.Text = claim.TechnicalConclusion ?? "(Chưa có kết luận)";

            // Resolution section visibility
            if (string.IsNullOrWhiteSpace(claim.TechnicalConclusion))
            {
                ResolutionSection.Visibility = Visibility.Collapsed;
            }

            // Signatures
            TechnicianNameText.Text = claim.Processor?.FullName ?? "—";
            CustomerSignNameText.Text = customer?.DisplayName ?? "—";
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Hide print buttons during printing
                ButtonPanel.Visibility = Visibility.Collapsed;

                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    printDialog.PrintVisual(PrintArea, "Phiếu Bảo Hành");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi in: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Restore print buttons
                ButtonPanel.Visibility = Visibility.Visible;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
