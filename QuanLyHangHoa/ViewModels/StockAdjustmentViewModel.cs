using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class StockAdjustmentLineEditor : ObservableObject
    {
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private string _direction = "In";
        [ObservableProperty] private decimal _quantity;
    }

    public partial class StockAdjustmentViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly StockAdjustmentService _adjustmentService;
        private readonly AppUser _currentUser;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts;
        [ObservableProperty] private ObservableCollection<StockAdjustmentLineEditor> _lines;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _documentCode = string.Empty;
        [ObservableProperty] private int _warehouseId = 1;
        [ObservableProperty] private string _reasonCode = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        [RelayCommand]
        private void LoadData()
        {
            // Placeholder for search functionality
        }

        public StockAdjustmentViewModel() : this(new AppUser { Id = 1 }) { }

        public StockAdjustmentViewModel(AppUser currentUser)
        {
            _currentUser = currentUser;
            _productService = new ProductService();
            _adjustmentService = new StockAdjustmentService();
            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            Lines = new ObservableCollection<StockAdjustmentLineEditor>();
            DocumentCode = $"ADJ-{DateTime.Now:yyyyMMddHHmmss}";
        }

        [RelayCommand]
        private void AddLine()
        {
            Lines.Add(new StockAdjustmentLineEditor());
        }

        [RelayCommand]
        private void RemoveLine(StockAdjustmentLineEditor line)
        {
            if (line != null)
            {
                Lines.Remove(line);
            }
        }

        [RelayCommand]
        private void SaveAdjustment()
        {
            if (string.IsNullOrWhiteSpace(DocumentCode) || !Lines.Any())
            {
                MessageBox.Show("Vui lòng nhập đủ thông tin.", "Cảnh báo");
                return;
            }

            try
            {
                var adj = new StockAdjustment
                {
                    DocumentCode = DocumentCode,
                    WarehouseId = WarehouseId,
                    AdjustmentType = "Manual",
                    ReasonCode = ReasonCode,
                    Status = "Posted",
                    CreatedBy = _currentUser.Id,
                    PostedBy = _currentUser.Id,
                    PostedAt = DateTime.Now,
                    ReferenceDocumentCode = "MANUAL"
                };

                adj.Lines = Lines.Select(l => new StockAdjustmentLine
                {
                    ProductId = l.SelectedProduct?.Id ?? 0,
                    QuantityDelta = l.Quantity,
                    BaseQuantityDelta = l.Quantity,
                    Direction = l.Direction
                }).ToList();

                _adjustmentService.Post(adj);
                MessageBox.Show("Đã lưu phiếu điều chỉnh kho.", "Thông báo");
                ResetForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
            }
        }

        private void ResetForm()
        {
            Lines.Clear();
            DocumentCode = $"ADJ-{DateTime.Now:yyyyMMddHHmmss}";
            ReasonCode = string.Empty;
        }
    }
}
