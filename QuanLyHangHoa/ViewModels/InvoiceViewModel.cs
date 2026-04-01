using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    // ViewModel kết nối Giao diện Hoá đơn với hệ thống Service
    public partial class InvoiceViewModel : ObservableObject
    {
        private readonly InvoiceService _invoiceService;
        private readonly ProductService _productService;

        // Lưu thông tin khách hàng nhập từ UI
        [ObservableProperty]
        private string _customerName = string.Empty;

        // Lưu giỏ hàng (Danh sách món đồ khách đang chọn để mua)
        [ObservableProperty]
        private ObservableCollection<InvoiceDetail> _cartItems;

        // Sản phẩm kho đang được chọn ở Bảng bên tay trái
        [ObservableProperty]
        private Product? _selectedProduct;

        // List chứa toàn bộ thực đơn Hàng hoá bên tay trái cho thu ngân chọn
        [ObservableProperty]
        private ObservableCollection<Product> _availableProducts;

        // Tính tự động tổng tiền của giỏ hàng
        public decimal TotalCartAmount => CartItems.Sum(x => x.UnitPrice * x.Quantity);

        // Lưu thông tin Nhân viên đang đăng nhập để gắn vào Hoá đơn
        private Employee _currentEmployee;

        public InvoiceViewModel(Employee employee)
        {
            _invoiceService = new InvoiceService();
            _productService = new ProductService();
            _currentEmployee = employee;
            
            CartItems = new ObservableCollection<InvoiceDetail>();
            // Khi CartItems thay đổi items bên trong, ta nên cập nhật TotalCartAmount (Bỏ qua cấu hình nâng cao trong MVP này)

            LoadProductsList();
        }

        private void LoadProductsList()
        {
            // Lấy danh sách hàng có số lượng > 0 mang lên quầy
            var list = _productService.GetAllProducts().Where(p => p.Quantity > 0).ToList();
            AvailableProducts = new ObservableCollection<Product>(list);
        }

        // Bấm nút thêm sản phẩm đang chọn vào Giỏ Hàng
        [RelayCommand]
        private void AddToCart()
        {
            if (SelectedProduct == null) return;

            // Kiểm tra món hàng đã có trong giỏ chưa
            var existingItem = CartItems.FirstOrDefault(c => c.ProductId == SelectedProduct.Id);
            if (existingItem != null)
            {
                existingItem.Quantity++;
                // Để giao diện WPF tự tính lại tổng, ta làm mẹo thay thế hoặc update Property (cần thêm framework xíu)
                // MVP: Xóa và Add lại để lừa UI update DataGrid
                CartItems.Remove(existingItem);
                CartItems.Add(existingItem);
            }
            else
            {
                // Thêm món mới vào Cart
                CartItems.Add(new InvoiceDetail 
                { 
                    ProductId = SelectedProduct.Id,
                    Product = SelectedProduct, // Lưu tạm để View có thể đọc Name lên lưới
                    Quantity = 1,
                    UnitPrice = SelectedProduct.UnitPrice
                });
            }

            // Gửi tín hiệu báo TotalCartAmount đã thay đổi để UI tự render lại Text Tổng tiền
            OnPropertyChanged(nameof(TotalCartAmount));
        }

        // Thanh toán - Đẩy bill vào Database
        [RelayCommand]
        private void Checkout()
        {
            if (!CartItems.Any()) return;

            var newInvoice = new Invoice()
            {
                CustomerName = this.CustomerName,
                TotalAmount = this.TotalCartAmount,
                EmployeeId = _currentEmployee.Id // ID thu ngân
            };

            // Tiến hành lưu thông qua Transaction (Đã viết kỹ bên Service)
            bool isSuccess = _invoiceService.CreateInvoice(newInvoice, CartItems.ToList());

            if (isSuccess)
            {
                // Thành công: Dọn dẹp giỏ hàng, cập nhật lại kho hàng hiển thị
                CartItems.Clear();
                CustomerName = "";
                LoadProductsList();
                OnPropertyChanged(nameof(TotalCartAmount));
                System.Windows.MessageBox.Show("Thanh toán thành công! Đã tự động trừ số lượng hàng trong kho.", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("Có lỗi xảy ra, kiểm tra lại lượng hàng trong kho!", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
