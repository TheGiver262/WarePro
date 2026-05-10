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
        [ObservableProperty] private decimal _taxRate = 0.08m; // Default 8%

        public decimal TotalPrice => Quantity * UnitPrice * (1 + TaxRate);

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                UnitPrice = value.DefaultPrice;
            }
        }

        partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(TotalPrice));
        partial void OnUnitPriceChanged(decimal value) => OnPropertyChanged(nameof(TotalPrice));
        partial void OnTaxRateChanged(decimal value) => OnPropertyChanged(nameof(TotalPrice));
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
        [ObservableProperty] private DateTime? _dueDate;
        [ObservableProperty] private decimal _paidAmount;
        [ObservableProperty] private string _notes = string.Empty;
        
        [ObservableProperty] private decimal _totalPurchaseAmount;
        [ObservableProperty] private int _totalPurchaseCount;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private int _selectedTabIndex = 0; // 0: List, 1: Create
        [ObservableProperty] private DateTime? _filterStartDate;
        [ObservableProperty] private DateTime? _filterEndDate;
        [ObservableProperty] private string? _selectedFilterPaymentStatus;
        [ObservableProperty] private string? _filterLinkDocCode;
        [ObservableProperty] private ObservableCollection<string> _availablePaymentStatuses = new() { "Tất cả", "Chưa thanh toán", "Thanh toán một phần", "Đã thanh toán", "Quá hạn" };

        [ObservableProperty] [NotifyPropertyChangedFor(nameof(FormRemainingAmount))] private decimal _formTotalAmount;
        public decimal FormRemainingAmount => FormTotalAmount - PaidAmount;

        private readonly MainViewModel? _mainViewModel;

        public PurchaseInvoiceViewModel() : this(null) { }

        public PurchaseInvoiceViewModel(MainViewModel? mainViewModel)
        {
            _mainViewModel = mainViewModel;
            var factory = _mainViewModel?.ContextFactory ?? (() => new QuanLyHangHoa.Data.AppDbContext());
            _invoiceService = new InvoiceService(factory);
            _productService = new ProductService(factory);
            _refDataService = new ReferenceDataService(factory);

            Lines.CollectionChanged += (s, e) => 
            {
                if (e.NewItems != null)
                {
                    foreach (PurchaseInvoiceLineEditor item in e.NewItems)
                        item.PropertyChanged += OnLineItemPropertyChanged;
                }
                if (e.OldItems != null)
                {
                    foreach (PurchaseInvoiceLineEditor item in e.OldItems)
                        item.PropertyChanged -= OnLineItemPropertyChanged;
                }
                RecalculateTotal();
            };
            
            LoadData();
            ResetForm();
            SelectedFilterPaymentStatus = "Tất cả";
        }

        private void OnLineItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PurchaseInvoiceLineEditor.TotalPrice))
            {
                RecalculateTotal();
            }
        }

        private void RecalculateTotal()
        {
            FormTotalAmount = Lines.Sum(l => l.TotalPrice);
        }

        partial void OnPaidAmountChanged(decimal value) => OnPropertyChanged(nameof(FormRemainingAmount));

        [RelayCommand]
        public void LoadData()
        {
            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            AvailableSuppliers = new ObservableCollection<Supplier>(_refDataService.GetAllSuppliers());
            
            var allInvoices = _invoiceService.GetAllPurchaseInvoices();

            // Apply Filters
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                allInvoices = allInvoices.Where(i => 
                    (i.InvoiceCode != null && i.InvoiceCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (i.Supplier != null && i.Supplier.DisplayName != null && i.Supplier.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            if (FilterStartDate.HasValue)
            {
                allInvoices = allInvoices.Where(i => i.InvoiceDate.Date >= FilterStartDate.Value.Date).ToList();
            }

            if (FilterEndDate.HasValue)
            {
                allInvoices = allInvoices.Where(i => i.InvoiceDate.Date <= FilterEndDate.Value.Date).ToList();
            }

            if (SelectedFilterPaymentStatus != "Tất cả" && !string.IsNullOrEmpty(SelectedFilterPaymentStatus))
            {
                allInvoices = allInvoices.Where(i => i.PaymentStatus == SelectedFilterPaymentStatus).ToList();
            }

            if (!string.IsNullOrWhiteSpace(FilterLinkDocCode))
            {
                // Placeholder for linking documents if needed
            }

            Invoices = new ObservableCollection<PurchaseInvoice>(allInvoices.OrderByDescending(i => i.InvoiceDate));
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
            var newLine = new PurchaseInvoiceLineEditor();
            newLine.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(PurchaseInvoiceLineEditor.TotalPrice))
                    OnPropertyChanged(nameof(FormTotalAmount));
            };
            Lines.Add(newLine);
        }

        [RelayCommand]
        private void RemoveLine(PurchaseInvoiceLineEditor line)
        {
            if (line != null)
            {
                Lines.Remove(line);
                OnPropertyChanged(nameof(FormTotalAmount));
            }
        }

        [RelayCommand]
        private void SaveInvoice()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show("Vui lòng chọn nhà cung cấp!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Lines.Any(l => l.SelectedProduct != null))
            {
                MessageBox.Show("Vui lòng thêm ít nhất một mặt hàng hợp lệ!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var invoice = new PurchaseInvoice
                {
                    InvoiceCode = InvoiceCode,
                    SupplierId = SelectedSupplier.Id,
                    InvoiceDate = InvoiceDate,
                    DueDate = DueDate,
                    PaidAmount = PaidAmount,
                    Notes = Notes,
                    CreatedAt = DateTime.Now,
                    CreatedBy = _mainViewModel?.CurrentUser?.Id ?? 1,
                    Lines = Lines.Where(l => l.SelectedProduct != null).Select(l => new PurchaseInvoiceLine
                    {
                        ProductId = l.SelectedProduct!.Id,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        TaxRate = l.TaxRate
                    }).ToList()
                };

                _invoiceService.SavePurchaseInvoice(invoice);
                MessageBox.Show("Lưu hóa đơn mua hàng thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ResetForm();
                SelectedTabIndex = 0; // Back to list
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
            DueDate = DateTime.Now.AddDays(30);
            PaidAmount = 0;
            Notes = string.Empty;
            Lines.Clear();
            AddLine();
            OnPropertyChanged(nameof(FormTotalAmount));
        }
    }
}
