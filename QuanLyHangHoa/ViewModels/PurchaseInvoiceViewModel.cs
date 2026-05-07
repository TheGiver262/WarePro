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
    public partial class PurchaseInvoiceLineEditor : ObservableObject
    {
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private decimal _quantity = 1;
        [ObservableProperty] private decimal _unitPrice;
        [ObservableProperty] private decimal _taxRate;

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                UnitPrice = value.DefaultPrice; // For purchase, this might be cost, but we use DefaultPrice as fallback
            }
        }
    }

    public partial class PurchaseInvoiceViewModel : ObservableObject
    {
        private readonly InvoiceService _invoiceService;
        private readonly ProductService _productService;
        private readonly ReferenceDataService _refDataService;

        [ObservableProperty] private ObservableCollection<PurchaseInvoice> _invoices = new();
        [ObservableProperty] private PurchaseInvoice? _selectedInvoice;
        
        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private ObservableCollection<Supplier> _availableSuppliers = new();
        [ObservableProperty] private ObservableCollection<PurchaseInvoiceLineEditor> _lines = new();

        [ObservableProperty] private string _invoiceCode = string.Empty;
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private DateTime _invoiceDate = DateTime.Now;
        [ObservableProperty] private string _notes = string.Empty;
        [ObservableProperty] private decimal _totalPurchaseAmount;
        [ObservableProperty] private int _totalPurchaseCount;
        [ObservableProperty] private string _searchText = string.Empty;

        private readonly MainViewModel? _mainViewModel;

        public PurchaseInvoiceViewModel() : this(null) { }

        public PurchaseInvoiceViewModel(MainViewModel? mainViewModel)
        {
            _mainViewModel = mainViewModel;
            var factory = _mainViewModel?.ContextFactory ?? (() => new QuanLyHangHoa.Data.AppDbContext());
            _invoiceService = new InvoiceService(factory);
            _productService = new ProductService(factory);
            _refDataService = new ReferenceDataService(factory);

            LoadData();
            ResetForm();
        }

        [RelayCommand]
        public void LoadData()
        {
            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            AvailableSuppliers = new ObservableCollection<Supplier>(_refDataService.GetAllSuppliers());
            
            var allInvoices = _invoiceService.GetAllPurchaseInvoices();
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                allInvoices = allInvoices.Where(i => 
                    (i.InvoiceCode != null && i.InvoiceCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (i.Supplier != null && i.Supplier.DisplayName != null && i.Supplier.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (i.Notes != null && i.Notes.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            Invoices = new ObservableCollection<PurchaseInvoice>(allInvoices);
            UpdateSummaries(allInvoices);
        }

        private void UpdateSummaries(System.Collections.Generic.IEnumerable<PurchaseInvoice> allInvoices)
        {
            TotalPurchaseCount = allInvoices.Count();
            TotalPurchaseAmount = allInvoices.Sum(i => i.GrandTotal);
        }



        [RelayCommand]
        private void AddLine()
        {
            Lines.Add(new PurchaseInvoiceLineEditor());
        }

        [RelayCommand]
        private void RemoveLine(PurchaseInvoiceLineEditor line)
        {
            if (line != null) Lines.Remove(line);
        }

        [RelayCommand]
        private void SaveInvoice()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show("Vui lòng chọn nhà cung cấp!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Lines.Any())
            {
                MessageBox.Show("Vui lòng thêm ít nhất một mặt hàng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var invoice = new PurchaseInvoice
                {
                    InvoiceCode = InvoiceCode,
                    SupplierId = SelectedSupplier.Id,
                    InvoiceDate = InvoiceDate,
                    Notes = Notes,
                    CreatedAt = DateTime.Now,
                    Lines = Lines.Select(l => new PurchaseInvoiceLine
                    {
                        ProductId = l.SelectedProduct?.Id ?? 0,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        TaxRate = l.TaxRate
                    }).ToList()
                };

                _invoiceService.SavePurchaseInvoice(invoice);
                MessageBox.Show("Lưu hóa đơn mua hàng thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData(); // Refresh list and summaries
                ResetForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu hóa đơn: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ResetForm()
        {
            InvoiceCode = $"PINV-{DateTime.Now:yyyyMMddHHmmss}";
            SelectedSupplier = null;
            InvoiceDate = DateTime.Now;
            Notes = string.Empty;
            Lines.Clear();
            Lines.Add(new PurchaseInvoiceLineEditor());
        }
    }
}
