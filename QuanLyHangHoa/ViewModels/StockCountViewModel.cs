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
    public partial class StockCountLineEditor : ObservableObject
    {
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private decimal _countedQuantity;

        public StockCountInput ToInput()
        {
            if (SelectedProduct == null)
            {
                throw new InvalidOperationException("Product is required.");
            }

            return new StockCountInput(SelectedProduct.Id, CountedQuantity);
        }
    }

    public partial class StockCountViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly StockCountService _stockCountService;
        private readonly Employee _currentUser;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts;
        [ObservableProperty] private ObservableCollection<StockCountLineEditor> _lines;
        [ObservableProperty] private string _sessionCode = string.Empty;
        [ObservableProperty] private int _warehouseId = 1;
        [ObservableProperty] private DateTime _countDate = DateTime.Now;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public StockCountViewModel(Employee currentUser)
        {
            _currentUser = currentUser;
            _productService = new ProductService();
            _stockCountService = new StockCountService();
            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            Lines = new ObservableCollection<StockCountLineEditor>();
            SessionCode = CreateDefaultSessionCode();
        }

        [RelayCommand]
        private void AddLine()
        {
            Lines.Add(new StockCountLineEditor());
        }

        [RelayCommand]
        private void RemoveLine(StockCountLineEditor line)
        {
            if (line != null)
            {
                Lines.Remove(line);
            }
        }

        [RelayCommand]
        private void SaveStockCount()
        {
            if (!Validate())
            {
                return;
            }

            try
            {
                var sessionId = _stockCountService.CreateApprovedSession(
                    SessionCode.Trim(),
                    WarehouseId,
                    CountDate,
                    _currentUser.Id,
                    Lines.Select(line => line.ToInput()));

                var adjustmentId = _stockCountService.CreateAdjustmentForDifferences(sessionId, _currentUser.Id);
                StatusMessage = $"Da tao phien kiem ke #{sessionId} va adjustment #{adjustmentId}.";
                MessageBox.Show(StatusMessage, "Thong bao", MessageBoxButton.OK, MessageBoxImage.Information);
                ResetForm();
            }
            catch (InventoryDomainException ex)
            {
                StatusMessage = ex.Message;
                MessageBox.Show(ex.Message, "Loi kiem ke", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                MessageBox.Show(ex.Message, "Loi du lieu", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool Validate()
        {
            if (string.IsNullOrWhiteSpace(SessionCode))
            {
                StatusMessage = "Vui long nhap ma phien kiem ke.";
                MessageBox.Show(StatusMessage, "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (WarehouseId <= 0)
            {
                StatusMessage = "Kho khong hop le.";
                MessageBox.Show(StatusMessage, "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!Lines.Any() || Lines.Any(line => line.SelectedProduct == null))
            {
                StatusMessage = "Vui long chon san pham cho tat ca cac dong.";
                MessageBox.Show(StatusMessage, "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (Lines.Any(line => line.CountedQuantity < 0))
            {
                StatusMessage = "So dem thuc te khong duoc am.";
                MessageBox.Show(StatusMessage, "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void ResetForm()
        {
            Lines.Clear();
            SessionCode = CreateDefaultSessionCode();
            CountDate = DateTime.Now;
            WarehouseId = 1;
        }

        private static string CreateDefaultSessionCode()
        {
            return $"COUNT-{DateTime.Now:yyyyMMddHHmmss}";
        }
    }
}
