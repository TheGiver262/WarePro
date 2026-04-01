using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    // Cầu nối giao diện BẢO HÀNH -> Service
    public partial class WarrantyViewModel : ObservableObject
    {
        private readonly WarrantyService _warrantyService;
        private readonly InvoiceService _invoiceService;

        [ObservableProperty]
        private string _customerName = string.Empty;

        [ObservableProperty]
        private string _condition = string.Empty;

        // Cho phép nhân viên copy đường dẫn ảnh Lỗi vào đây
        [ObservableProperty]
        private string _imagePath = string.Empty;

        // Tìm hoá đơn bán hàng cũ bằng ID
        [ObservableProperty]
        private string _searchInvoiceId = string.Empty;

        // Hoá đơn hiện tại tìm thấy
        [ObservableProperty]
        private Invoice? _foundInvoice;

        // Những món nằm trong hoá đơn cũ mà bị lỗi muốn bảo hành
        [ObservableProperty]
        private ObservableCollection<WarrantyTicketDetail> _brokenItems;

        [ObservableProperty]
        private InvoiceDetail? _selectedInvoiceDetail;

        public WarrantyViewModel()
        {
            _warrantyService = new WarrantyService();
            _invoiceService = new InvoiceService();
            BrokenItems = new ObservableCollection<WarrantyTicketDetail>();
        }

        [RelayCommand]
        private void SearchInvoice()
        {
            if (int.TryParse(SearchInvoiceId, out int id))
            {
                var invoices = _invoiceService.GetAllInvoices();
                FoundInvoice = invoices.FirstOrDefault(i => i.Id == id);

                if (FoundInvoice != null)
                {
                    CustomerName = FoundInvoice.CustomerName; // Auto điền tên khách
                    BrokenItems.Clear();
                }
                else
                {
                    System.Windows.MessageBox.Show("Không tìm thấy hoá đơn này!", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void AddToBrokenList()
        {
            if (SelectedInvoiceDetail == null) return;

            // Kiểm tra xem đã thêm chưa
            if (!BrokenItems.Any(b => b.ProductId == SelectedInvoiceDetail.ProductId))
            {
                BrokenItems.Add(new WarrantyTicketDetail
                {
                    ProductId = SelectedInvoiceDetail.ProductId,
                    Product = SelectedInvoiceDetail.Product,
                    Resolution = "N/A"
                });
            }
        }

        [RelayCommand]
        private void CreateWarranty()
        {
            if (FoundInvoice == null || !BrokenItems.Any())
            {
                System.Windows.MessageBox.Show("Thêm sản phẩm cần bảo hành trước!", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Tiến hành chạy logic phân mảnh phiếu bảo hành theo hạn dựa vào Grouping (đã viết ở Service)
            bool result = _warrantyService.CreateWarrantyTickets(FoundInvoice.Id, CustomerName, Condition, ImagePath, BrokenItems.ToList());

            if (result)
            {
                System.Windows.MessageBox.Show("Đã lập thành công các phiếu Bảo Hành! Ảnh đã được lưu vào hệ thống an toàn.", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                BrokenItems.Clear();
                Condition = "";
                ImagePath = "";
            }
            else
            {
                System.Windows.MessageBox.Show("Có lỗi xảy ra khi tạo mã bảo hành!", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
