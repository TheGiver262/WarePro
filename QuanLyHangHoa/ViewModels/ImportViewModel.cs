using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    // Cầu nối giao diện chức năng Phiếu Nhập Kho -> Service Nhập Kho
    public partial class ImportViewModel : ObservableObject
    {
        private readonly ImportService _importService;
        private readonly ProductService _productService;

        // Lưu giỏ hàng nhập kho (Các sản phẩm bên ncc giao tới)
        [ObservableProperty]
        private ObservableCollection<ImportReceiptDetail> _importItems;

        [ObservableProperty]
        private ObservableCollection<Product> _availableProducts;

        [ObservableProperty]
        private Product? _selectedProduct;

        // Tính tổng tiền phải trả cho nhà cung cấp
        public decimal TotalImportAmount => ImportItems.Sum(x => x.ImportPrice * x.Quantity);

        private Employee _currentEmployee;

        public ImportViewModel(Employee employee)
        {
            _importService = new ImportService();
            _productService = new ProductService();
            _currentEmployee = employee;
            ImportItems = new ObservableCollection<ImportReceiptDetail>();
            
            LoadProductsList();
        }

        private void LoadProductsList()
        {
            // Trong nhập kho, load ra tất cả các loại Hàng để thủ kho tick vào
            var list = _productService.GetAllProducts().ToList();
            AvailableProducts = new ObservableCollection<Product>(list);
        }

        [RelayCommand]
        private void AddToImportList()
        {
            if (SelectedProduct == null) return;

            var existingItem = ImportItems.FirstOrDefault(c => c.ProductId == SelectedProduct.Id);
            if (existingItem != null)
            {
                existingItem.Quantity++;
                ImportItems.Remove(existingItem);
                ImportItems.Add(existingItem);
            }
            else
            {
                ImportItems.Add(new ImportReceiptDetail 
                { 
                    ProductId = SelectedProduct.Id,
                    Product = SelectedProduct,
                    Quantity = 1,
                    // Lúc nhập mặc định lấy theo UnitPrice (có thể user tự sửa tay sau đó)
                    ImportPrice = SelectedProduct.UnitPrice * 0.8m, // Giả định giá nhập rẻ hơn 20%
                    Status = "Mới"
                });
            }
            OnPropertyChanged(nameof(TotalImportAmount));
        }

        [RelayCommand]
        private void CompleteImport()
        {
            if (!ImportItems.Any()) return;

            var newReceipt = new ImportReceipt()
            {
                TotalAmount = this.TotalImportAmount,
                EmployeeId = _currentEmployee.Id
            };

            // Gọi Transaction DB tự động nhập và cộng số lượng
            bool isSuccess = _importService.CreateImportReceipt(newReceipt, ImportItems.ToList());

            if (isSuccess)
            {
                ImportItems.Clear();
                LoadProductsList();
                OnPropertyChanged(nameof(TotalImportAmount));
                System.Windows.MessageBox.Show("Nhập kho thành công! Đã tự động CỘNG số lượng hàng trong kho.", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("Có lỗi xảy ra, không thể nạp phiếu nhập!", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
