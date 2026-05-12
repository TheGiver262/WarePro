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
        [ObservableProperty] private StockIn? _selectedStockIn;
        [ObservableProperty] private DateTime _invoiceDate = DateTime.Now;
        [ObservableProperty] private DateTime? _dueDate;
        [ObservableProperty] private decimal _paidAmount;
        [ObservableProperty] private string _selectedPaymentStatus = "Chưa TT";
        [ObservableProperty] private string _notes = string.Empty;
        
        [ObservableProperty] private ObservableCollection<StockIn> _availableStockIns = new();
        
        [ObservableProperty] private decimal _totalPurchaseAmount;
        [ObservableProperty] private int _totalPurchaseCount;
        [ObservableProperty] private int _paidCount;
        [ObservableProperty] private int _partialCount;
        [ObservableProperty] private int _unpaidCount;
        [ObservableProperty] private int _overdueCount;
        [ObservableProperty] private string _searchInvoiceCode = string.Empty;
        [ObservableProperty] private string _searchSupplierName = string.Empty;
        [ObservableProperty] private int _selectedTabIndex = 0; // 0: List, 1: Create
        [ObservableProperty] private DateTime? _filterStartDate;
        [ObservableProperty] private DateTime? _filterEndDate;
        [ObservableProperty] private string? _selectedFilterPaymentStatus;
        [ObservableProperty] private string? _filterLinkDocCode;
        [ObservableProperty] private decimal? _filterMinTotal;
        [ObservableProperty] private decimal? _filterMaxTotal;
        [ObservableProperty] private bool _isAdvancedFilterOpen;
        [ObservableProperty] private ObservableCollection<string> _availablePaymentStatuses = new() { "Tất cả", "Chưa TT", "TT 1 phần", "Đã TT", "Quá hạn" };

        [RelayCommand]
        private void ToggleAdvancedFilter() => IsAdvancedFilterOpen = !IsAdvancedFilterOpen;

        partial void OnSearchInvoiceCodeChanged(string value) => LoadData();
        partial void OnSearchSupplierNameChanged(string value) => LoadData();
        partial void OnFilterStartDateChanged(DateTime? value) => LoadData();
        partial void OnFilterEndDateChanged(DateTime? value) => LoadData();
        partial void OnSelectedFilterPaymentStatusChanged(string? value) => LoadData();
        partial void OnFilterLinkDocCodeChanged(string? value) => LoadData();
        partial void OnFilterMinTotalChanged(decimal? value) => LoadData();
        partial void OnFilterMaxTotalChanged(decimal? value) => LoadData();

        [ObservableProperty] [NotifyPropertyChangedFor(nameof(FormRemainingAmount))] private decimal _formTotalAmount;
        [ObservableProperty] private decimal _formSubTotal;
        [ObservableProperty] private decimal _formTaxAmount;
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
            InitializeForm(); // Init form fields without switching tab
            SelectedFilterPaymentStatus = "Tất cả";
            SelectedTabIndex = 0; // Always start on list tab
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
            FormSubTotal = Lines.Sum(l => l.Quantity * l.UnitPrice);
            FormTaxAmount = Lines.Sum(l => l.Quantity * l.UnitPrice * l.TaxRate);
            FormTotalAmount = Lines.Sum(l => l.TotalPrice);
        }

        partial void OnPaidAmountChanged(decimal value) => OnPropertyChanged(nameof(FormRemainingAmount));

        [RelayCommand]
        public void LoadData()
        {
            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            AvailableSuppliers = new ObservableCollection<Supplier>(_refDataService.GetAllSuppliers());
            
            using (var context = _mainViewModel?.ContextFactory?.Invoke() ?? new QuanLyHangHoa.Data.AppDbContext())
            {
                AvailableStockIns = new ObservableCollection<StockIn>(context.StockIns.ToList());
            }
            var allInvoices = _invoiceService.GetAllPurchaseInvoices();

            // Apply Filters
            if (!string.IsNullOrWhiteSpace(SearchInvoiceCode))
            {
                allInvoices = allInvoices.Where(i => i.InvoiceCode != null && i.InvoiceCode.Contains(SearchInvoiceCode, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(SearchSupplierName))
            {
                allInvoices = allInvoices.Where(i => i.Supplier != null && i.Supplier.DisplayName != null && i.Supplier.DisplayName.Contains(SearchSupplierName, StringComparison.OrdinalIgnoreCase)).ToList();
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
                var englishStatus = StatusToEnglish(SelectedFilterPaymentStatus);
                allInvoices = allInvoices.Where(i => i.PaymentStatus == englishStatus).ToList();
            }

            if (!string.IsNullOrWhiteSpace(FilterLinkDocCode))
            {
                // Placeholder for linking documents if needed
            }

            if (FilterMinTotal.HasValue)
            {
                allInvoices = allInvoices.Where(i => i.GrandTotal >= FilterMinTotal.Value).ToList();
            }

            if (FilterMaxTotal.HasValue)
            {
                allInvoices = allInvoices.Where(i => i.GrandTotal <= FilterMaxTotal.Value).ToList();
            }

            Invoices = new ObservableCollection<PurchaseInvoice>(allInvoices.OrderByDescending(i => i.InvoiceDate));
            UpdateSummaries(allInvoices);
        }

        private void UpdateSummaries(System.Collections.Generic.IEnumerable<PurchaseInvoice> allInvoices)
        {
            TotalPurchaseCount = allInvoices.Count();
            TotalPurchaseAmount = allInvoices.Sum(i => i.GrandTotal);

            PaidCount = allInvoices.Count(i => i.PaymentStatus == "Paid");
            PartialCount = allInvoices.Count(i => i.PaymentStatus == "Partial");
            UnpaidCount = allInvoices.Count(i => i.PaymentStatus == "Unpaid");
            OverdueCount = allInvoices.Count(i => i.PaymentStatus == "Overdue");
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

        [ObservableProperty] private bool _isViewMode;
        [ObservableProperty] private bool _isEditMode;
        private PurchaseInvoice? _editingInvoice;

        [RelayCommand]
        private void ViewInvoice(PurchaseInvoice? invoice)
        {
            if (invoice == null) return;
            _editingInvoice = invoice;
            PopulateForm(invoice);
            IsViewMode = true;
            IsEditMode = false;
            SelectedTabIndex = 1; // Switch to form tab
        }

        [RelayCommand]
        private void EditInvoice(PurchaseInvoice? invoice)
        {
            if (invoice == null) return;
            _editingInvoice = invoice;
            PopulateForm(invoice);
            IsViewMode = false;
            IsEditMode = true;
            SelectedTabIndex = 1; // Switch to form tab
        }

        private void PopulateForm(PurchaseInvoice invoice)
        {
            InvoiceCode = invoice.InvoiceCode;
            SelectedSupplier = AvailableSuppliers.FirstOrDefault(s => s.Id == invoice.SupplierId);
            InvoiceDate = invoice.InvoiceDate;
            DueDate = invoice.DueDate ?? DateTime.Now;
            PaidAmount = invoice.PaidAmount;
            SelectedStockIn = AvailableStockIns.FirstOrDefault(s => s.Id == invoice.StockInId);
            SelectedPaymentStatus = invoice.PaymentStatus ?? "Chưa TT";
            Notes = invoice.Notes ?? string.Empty;
            
            Lines.Clear();
            if (invoice.Lines != null)
            {
                foreach (var line in invoice.Lines)
                {
                    Lines.Add(new PurchaseInvoiceLineEditor
                    {
                        SelectedProduct = AvailableProducts.FirstOrDefault(p => p.Id == line.ProductId),
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        TaxRate = line.TaxRate
                    });
                }
            }
            RecalculateTotal();
        }

        [RelayCommand]
        private void SaveInvoice()
        {
            if (IsViewMode) return;
            try
            {
                if (SelectedSupplier == null)
                {
                    MessageBox.Show("Vui lòng chọn nhà cung cấp!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!Lines.Any(l => l.SelectedProduct != null))
                {
                    MessageBox.Show("Vui lòng thêm ít nhất một sản phẩm!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var invoice = _editingInvoice ?? new PurchaseInvoice();
                invoice.InvoiceCode = InvoiceCode;
                invoice.SupplierId = SelectedSupplier.Id;
                invoice.StockInId = SelectedStockIn?.Id;
                invoice.InvoiceDate = InvoiceDate;
                invoice.DueDate = DueDate;
                invoice.PaidAmount = PaidAmount;
                invoice.PaymentStatus = StatusToEnglish(SelectedPaymentStatus);
                invoice.Notes = Notes;
                
                // Set audit fields
                if (invoice.Id == 0)
                {
                    invoice.CreatedAt = DateTime.Now;
                    invoice.CreatedBy = _mainViewModel?.CurrentUser?.Id ?? 1; // Default to admin if user not found
                }

                // IMPORTANT: Clear navigation properties to avoid EF tracking issues with detached entities
                invoice.Supplier = null!;
                invoice.StockIn = null;
                invoice.Creator = null!;

                // Map lines
                invoice.Lines = Lines.Where(l => l.SelectedProduct != null).Select(l => new PurchaseInvoiceLine
                {
                    Id = 0, // Always 0 for simplicity if we replace the collection, 
                            // though better handling would be needed for true updates
                    ProductId = l.SelectedProduct!.Id,
                    UnitId = l.SelectedProduct!.DefaultUnitId,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    TaxRate = l.TaxRate,
                    PurchaseInvoiceId = invoice.Id
                }).ToList();

                _invoiceService.SavePurchaseInvoice(invoice);

                MessageBox.Show("Lưu hoá đơn thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                ResetForm();
                LoadData();
                SelectedTabIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu hoá đơn: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Initializes form fields without switching the active tab. Used on ViewModel init.</summary>
        private void InitializeForm()
        {
            _editingInvoice = null;
            IsViewMode = false;
            IsEditMode = false;
            InvoiceCode = $"PINV-{DateTime.Now:yyyyMMddHHmmss}";
            SelectedSupplier = null;
            InvoiceDate = DateTime.Now;
            DueDate = DateTime.Now.AddDays(7);
            PaidAmount = 0;
            SelectedStockIn = null;
            SelectedPaymentStatus = "Chưa TT";
            Notes = string.Empty;
            Lines.Clear();
            AddLine();
            OnPropertyChanged(nameof(FormTotalAmount));
        }

        [RelayCommand]
        public void ResetForm()
        {
            InitializeForm();
            SelectedTabIndex = 1; // Explicitly switch to form tab when user creates new
        }

        private string StatusToEnglish(string vietnameseStatus)
        {
            return vietnameseStatus switch
            {
                "Chưa TT" => "Unpaid",
                "TT 1 phần" => "Partial",
                "Đã TT" => "Paid",
                "Quá hạn" => "Overdue",
                _ => "Unpaid"
            };
        }

        [RelayCommand]
        private void PrintInvoice(PurchaseInvoice? invoice)
        {
            if (invoice == null) return;
            MessageBox.Show($"In hoá đơn {invoice.InvoiceCode} (Chức năng đang phát triển)", "Thông báo");
        }
    }
}
