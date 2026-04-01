using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    // ViewModel liên kết giao diện Danh sách Hàng Hoá với Source Code
    public partial class ProductViewModel : ObservableObject
    {
        private readonly ProductService _productService;

        // Collection quan sát được giúp UI tự update khi Thêm/Sửa/Xoá List
        [ObservableProperty]
        private ObservableCollection<Product> _products;

        // Hàng hoá đang được chọn trên Bảng (DataGrid) => 
        // Biến này được bind thẳng vô SelectedItem của DataGrid ngoài UI
        [ObservableProperty]
        private Product? _selectedProduct;

        // Chứa thông tin Input Thêm Mới hoặc Sửa
        [ObservableProperty]
        private Product _currentInputProduct;

        public ProductViewModel()
        {
            _productService = new ProductService();
            _currentInputProduct = new Product();
            LoadData(); // Gọi hàm lấy data lúc đầu khởi tạo
        }

        private void LoadData()
        {
            // Lấy từ DB rồi nhét vào ObservableCollection để bind lên giao diện WPF
            var list = _productService.GetAllProducts();
            Products = new ObservableCollection<Product>(list);
        }

        // Hàm Reset Trắng các ô Nhập liệu
        [RelayCommand]
        private void ClearInput()
        {
            CurrentInputProduct = new Product();
            SelectedProduct = null;
        }

        // Hàm gọi Database lưu hàng hoá
        [RelayCommand]
        private void SaveProduct()
        {
            if (string.IsNullOrWhiteSpace(CurrentInputProduct.Name)) return;

            // Nếu ID == 0 thì là tạo mới. Còn > 0 là sửa món hàng đã chọn
            if (CurrentInputProduct.Id == 0)
            {
                _productService.AddProduct(CurrentInputProduct);
            }
            else
            {
                _productService.UpdateProduct(CurrentInputProduct);
            }
            // Load lại Data Grid báo thành công
            LoadData();
            ClearInput();
        }

        [RelayCommand]
        private void DeleteProduct()
        {
            if (SelectedProduct != null && SelectedProduct.Id > 0)
            {
                _productService.DeleteProduct(SelectedProduct.Id);
                LoadData();
                ClearInput();
            }
        }

        // Khi click vào 1 dòng trong DataGrid, load data lên CurrentInput
        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                // Copy giá trị sang Input (không bind thẳng tránh làm hỏng list gốc khi chưa Save)
                CurrentInputProduct = new Product 
                {
                    Id = value.Id,
                    Name = value.Name,
                    Category = value.Category,
                    Quantity = value.Quantity,
                    UnitPrice = value.UnitPrice,
                    Origin = value.Origin,
                    WarrantyMonths = value.WarrantyMonths,
                    Notes = value.Notes
                };
            }
        }
    }
}
