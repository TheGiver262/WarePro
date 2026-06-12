using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
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
        private int _skip = 0;
        private const int PageSize = 100;
        private bool _isLoading = false;
        private bool _isInitialized = false;

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

        [RelayCommand]
        private void Refresh()
        {
            SearchInvoiceCode = string.Empty;
            SearchSupplierName = string.Empty;
            FilterStartDate = null;
            FilterEndDate = null;
            SelectedFilterPaymentStatus = "Tất cả";
            FilterLinkDocCode = string.Empty;
            FilterMinTotal = null;
            FilterMaxTotal = null;

            LoadData();
        }

        partial void OnSearchInvoiceCodeChanged(string value) { if (_isInitialized) LoadData(); }
        partial void OnSearchSupplierNameChanged(string value) { if (_isInitialized) LoadData(); }
        partial void OnFilterStartDateChanged(DateTime? value) { if (_isInitialized) LoadData(); }
        partial void OnFilterEndDateChanged(DateTime? value) { if (_isInitialized) LoadData(); }
        partial void OnSelectedFilterPaymentStatusChanged(string? value) { if (_isInitialized) LoadData(); }
        partial void OnFilterLinkDocCodeChanged(string? value) { if (_isInitialized) LoadData(); }
        partial void OnFilterMinTotalChanged(decimal? value) { if (_isInitialized) LoadData(); }
        partial void OnFilterMaxTotalChanged(decimal? value) { if (_isInitialized) LoadData(); }

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
            _isInitialized = true;
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
            _ = LoadDataAsync(true);
        }

        private async Task LoadDataAsync(bool reset)
        {
            if (_isLoading) return;
            _isLoading = true;
            try
            {
                if (reset)
                {
                    _skip = 0;
                    Invoices.Clear();
                }

                if (reset)
                {
                    var products = await Task.Run(() => _productService.GetAllProducts());
                    var suppliers = await Task.Run(() => _refDataService.GetAllSuppliers());
                    List<StockIn> stockIns;
                    using (var context = _mainViewModel?.ContextFactory?.Invoke() ?? new AppDbContext())
                    {
                        var tempStockIns = await Task.Run(() => context.StockIns
                            .AsNoTracking()
                            .Select(s => new { s.Id, s.DocumentCode })
                            .ToList());
                        
                        stockIns = tempStockIns.Select(t => new StockIn 
                        { 
                            Id = t.Id, 
                            DocumentCode = t.DocumentCode 
                        }).ToList();
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AvailableProducts = new ObservableCollection<Product>(products);
                        AvailableSuppliers = new ObservableCollection<Supplier>(suppliers);
                        AvailableStockIns = new ObservableCollection<StockIn>(stockIns);
                    });
                }

                var paymentStatus = SelectedFilterPaymentStatus != "Tất cả" && !string.IsNullOrEmpty(SelectedFilterPaymentStatus)
                    ? StatusToEnglish(SelectedFilterPaymentStatus)
                    : null;

                var data = await Task.Run(() => _invoiceService.GetPurchaseInvoicesPaged(
                    SearchInvoiceCode, SearchSupplierName, FilterStartDate, FilterEndDate, paymentStatus, FilterMinTotal, FilterMaxTotal, _skip, PageSize));

                foreach (var inv in data)
                {
                    Invoices.Add(inv);
                }
                _skip += data.Count;

                // Thống kê đếm bất đồng bộ từ database (gộp thành 1 truy vấn duy nhất)
                await Task.Run(() =>
                {
                    using var db = _mainViewModel?.ContextFactory?.Invoke() ?? new AppDbContext();
                    var query = db.PurchaseInvoices.AsNoTracking().AsQueryable();
                    query = ApplyPurchaseInvoiceFiltersStatic(query, SearchInvoiceCode, SearchSupplierName, FilterStartDate, FilterEndDate, paymentStatus, FilterMinTotal, FilterMaxTotal);
                    
                    var stats = query.GroupBy(i => 1)
                        .Select(g => new
                        {
                            TotalCount = g.Count(),
                            TotalAmount = g.Sum(i => i.GrandTotal),
                            Paid = g.Count(i => i.PaymentStatus == "Paid"),
                            Partial = g.Count(i => i.PaymentStatus == "Partial"),
                            Unpaid = g.Count(i => i.PaymentStatus == "Unpaid"),
                            Overdue = g.Count(i => i.PaymentStatus == "Overdue")
                        })
                        .FirstOrDefault() ?? new { TotalCount = 0, TotalAmount = 0m, Paid = 0, Partial = 0, Unpaid = 0, Overdue = 0 };

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TotalPurchaseCount = stats.TotalCount;
                        TotalPurchaseAmount = stats.TotalAmount;
                        PaidCount = stats.Paid;
                        PartialCount = stats.Partial;
                        UnpaidCount = stats.Unpaid;
                        OverdueCount = stats.Overdue;
                    });
                });
            }
            catch (Exception)
            {
            }
            finally
            {
                _isLoading = false;
            }
        }

        [RelayCommand]
        private async Task LoadMore()
        {
            await LoadDataAsync(false);
        }

        private static IQueryable<PurchaseInvoice> ApplyPurchaseInvoiceFiltersStatic(
            IQueryable<PurchaseInvoice> query,
            string code,
            string supplierName,
            DateTime? startDate,
            DateTime? endDate,
            string? paymentStatus,
            decimal? minTotal,
            decimal? maxTotal)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                var term = code.Trim().ToLower();
                query = query.Where(i => i.InvoiceCode != null && i.InvoiceCode.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(supplierName))
            {
                var term = supplierName.Trim().ToLower();
                query = query.Where(i => i.Supplier != null && i.Supplier.DisplayName != null && i.Supplier.DisplayName.ToLower().Contains(term));
            }

            if (startDate.HasValue)
            {
                query = query.Where(i => i.InvoiceDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(i => i.InvoiceDate <= endOfDay);
            }

            if (!string.IsNullOrEmpty(paymentStatus) && paymentStatus != "Tất cả" && paymentStatus != "All")
            {
                query = query.Where(i => i.PaymentStatus == paymentStatus);
            }

            if (minTotal.HasValue)
            {
                query = query.Where(i => i.GrandTotal >= minTotal.Value);
            }

            if (maxTotal.HasValue)
            {
                query = query.Where(i => i.GrandTotal <= maxTotal.Value);
            }

            return query;
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
            SelectedPaymentStatus = StatusToVietnamese(invoice.PaymentStatus ?? "Unpaid");
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

        private string StatusToVietnamese(string englishStatus)
        {
            return englishStatus switch
            {
                "Unpaid" => "Chưa TT",
                "Partial" => "TT 1 phần",
                "Paid" => "Đã TT",
                "Overdue" => "Quá hạn",
                _ => "Chưa TT"
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
