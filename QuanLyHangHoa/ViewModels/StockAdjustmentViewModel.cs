using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class StockAdjustmentLineEditor : ObservableObject
    {
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private decimal _quantityDelta;

        public string Direction => QuantityDelta >= 0
            ? StockLedgerDirection.In.ToString()
            : StockLedgerDirection.Out.ToString();

        partial void OnQuantityDeltaChanged(decimal value)
        {
            OnPropertyChanged(nameof(Direction));
        }

        public StockAdjustmentLine ToAdjustmentLine()
        {
            if (SelectedProduct == null)
            {
                throw new InvalidOperationException("Product is required.");
            }

            return new StockAdjustmentLine
            {
                ProductId = SelectedProduct.Id,
                QuantityDelta = QuantityDelta,
                BaseQuantityDelta = QuantityDelta,
                Direction = Direction
            };
        }
    }

    public partial class StockAdjustmentViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly StockAdjustmentService _stockAdjustmentService;
        private readonly Employee _currentUser;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts;
        [ObservableProperty] private ObservableCollection<StockAdjustmentLineEditor> _lines;
        [ObservableProperty] private string _documentCode = string.Empty;
        [ObservableProperty] private string _referenceDocumentCode = string.Empty;
        [ObservableProperty] private string _reasonCode = "ManualAdjustment";
        [ObservableProperty] private int _warehouseId = 1;

        public StockAdjustmentViewModel(Employee currentUser)
        {
            _currentUser = currentUser;
            _productService = new ProductService();
            _stockAdjustmentService = new StockAdjustmentService();
            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            Lines = new ObservableCollection<StockAdjustmentLineEditor>();
            DocumentCode = CreateDefaultDocumentCode();
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
            if (!Validate())
            {
                return;
            }

            try
            {
                var adjustment = new StockAdjustment
                {
                    DocumentCode = DocumentCode.Trim(),
                    WarehouseId = WarehouseId,
                    AdjustmentType = "Manual",
                    Status = StockDocumentStatus.Approved.ToString(),
                    ReferenceDocumentCode = ReferenceDocumentCode.Trim(),
                    ReasonCode = ReasonCode.Trim(),
                    CreatedBy = _currentUser.Id,
                    PostedBy = _currentUser.Id
                };

                foreach (var line in Lines)
                {
                    adjustment.Lines.Add(line.ToAdjustmentLine());
                }

                _stockAdjustmentService.Post(adjustment);
                MessageBox.Show("Dieu chinh ton kho thanh cong!", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Information);
                ResetForm();
            }
            catch (InventoryDomainException ex)
            {
                MessageBox.Show(ex.Message, "Loi ton kho", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Loi du lieu", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool Validate()
        {
            if (string.IsNullOrWhiteSpace(DocumentCode))
            {
                MessageBox.Show("Vui long nhap ma chung tu.", "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (WarehouseId <= 0)
            {
                MessageBox.Show("Kho khong hop le.", "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!Lines.Any() || Lines.Any(line => line.SelectedProduct == null))
            {
                MessageBox.Show("Vui long chon san pham cho tat ca cac dong.", "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (Lines.Any(line => line.QuantityDelta == 0))
            {
                MessageBox.Show("So luong dieu chinh phai khac 0.", "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void ResetForm()
        {
            Lines.Clear();
            DocumentCode = CreateDefaultDocumentCode();
            ReferenceDocumentCode = string.Empty;
            ReasonCode = "ManualAdjustment";
            WarehouseId = 1;
        }

        private static string CreateDefaultDocumentCode()
        {
            return $"ADJ-MAN-{DateTime.Now:yyyyMMddHHmmss}";
        }
    }
}
