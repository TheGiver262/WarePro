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
    public partial class SalesInvoiceLineEditor : ObservableObject
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

    public partial class SalesInvoiceViewModel : ObservableObject
    {
        private readonly InvoiceService _invoiceService;
        private readonly ProductService _productService;
        private readonly ReferenceDataService _refDataService;
        private int _skip = 0;
        private const int PageSize = 100;
        private bool _isLoading = false;
        private bool _isInitialized = false;

        [ObservableProperty] private ObservableCollection<SalesInvoice> _invoices = new();
        [ObservableProperty] private SalesInvoice? _selectedInvoice;
        
        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private ObservableCollection<Customer> _availableCustomers = new();
        [ObservableProperty] private ObservableCollection<SalesInvoiceLineEditor> _lines = new();

        [ObservableProperty] private string _invoiceCode = string.Empty;
        [ObservableProperty] private Customer? _selectedCustomer;
        [ObservableProperty] private StockOut? _selectedStockOut;
        [ObservableProperty] private DateTime _invoiceDate = DateTime.Now;
        [ObservableProperty] private DateTime? _dueDate;
        [ObservableProperty] private decimal _paidAmount;
        [ObservableProperty] private string _selectedPaymentStatus = "Chưa TT";
        [ObservableProperty] private string _notes = string.Empty;
        
        [ObservableProperty] private ObservableCollection<StockOut> _availableStockOuts = new();
        
        [ObservableProperty] private decimal _totalSalesAmount;
        [ObservableProperty] private int _totalSalesCount;
        [ObservableProperty] private int _paidCount;
        [ObservableProperty] private int _partialCount;
        [ObservableProperty] private int _unpaidCount;
        [ObservableProperty] private int _overdueCount;
        [ObservableProperty] private string _searchInvoiceCode = string.Empty;
        [ObservableProperty] private string _searchCustomerName = string.Empty;
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
            _isInitialized = false;
            SearchInvoiceCode = string.Empty;
            SearchCustomerName = string.Empty;
            FilterStartDate = null;
            FilterEndDate = null;
            SelectedFilterPaymentStatus = "Tất cả";
            FilterLinkDocCode = string.Empty;
            FilterMinTotal = null;
            FilterMaxTotal = null;
            _isInitialized = true;

            LoadData();
        }

        partial void OnSearchInvoiceCodeChanged(string value) { if (_isInitialized) LoadData(); }
        partial void OnSearchCustomerNameChanged(string value) { if (_isInitialized) LoadData(); }
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

        public SalesInvoiceViewModel() : this(null) { }

        public SalesInvoiceViewModel(MainViewModel? mainViewModel)
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
                    foreach (SalesInvoiceLineEditor item in e.NewItems)
                        item.PropertyChanged += OnLineItemPropertyChanged;
                }
                if (e.OldItems != null)
                {
                    foreach (SalesInvoiceLineEditor item in e.OldItems)
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
            if (e.PropertyName == nameof(SalesInvoiceLineEditor.TotalPrice))
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
                    var customers = await Task.Run(() => _refDataService.GetAllCustomers());
                    List<StockOut> stockOuts;
                    using (var context = _mainViewModel?.ContextFactory?.Invoke() ?? new AppDbContext())
                    {
                        var tempStockOuts = await Task.Run(() => context.StockOuts
                            .AsNoTracking()
                            .Select(s => new { s.Id, s.DocumentCode })
                            .ToList());
                        
                        stockOuts = tempStockOuts.Select(t => new StockOut 
                        { 
                            Id = t.Id, 
                            DocumentCode = t.DocumentCode 
                        }).ToList();
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AvailableProducts = new ObservableCollection<Product>(products);
                        AvailableCustomers = new ObservableCollection<Customer>(customers);
                        AvailableStockOuts = new ObservableCollection<StockOut>(stockOuts);
                    });
                }

                var paymentStatus = SelectedFilterPaymentStatus != "Tất cả" && !string.IsNullOrEmpty(SelectedFilterPaymentStatus)
                    ? StatusToEnglish(SelectedFilterPaymentStatus)
                    : null;

                var data = await Task.Run(() => _invoiceService.GetSalesInvoicesPaged(
                    SearchInvoiceCode, SearchCustomerName, FilterStartDate, FilterEndDate, paymentStatus ?? string.Empty, FilterMinTotal, FilterMaxTotal, _skip, PageSize));

                foreach (var inv in data)
                {
                    Invoices.Add(inv);
                }
                _skip += data.Count;

                // Thống kê đếm bất đồng bộ từ database (gộp thành 1 truy vấn duy nhất)
                await Task.Run(() =>
                {
                    using var db = _mainViewModel?.ContextFactory?.Invoke() ?? new AppDbContext();
                    var query = db.SalesInvoices.AsNoTracking().AsQueryable();
                    query = ApplySalesInvoiceFiltersStatic(query, SearchInvoiceCode, SearchCustomerName, FilterStartDate, FilterEndDate, paymentStatus ?? string.Empty, FilterMinTotal, FilterMaxTotal);
                    
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
                        TotalSalesCount = stats.TotalCount;
                        TotalSalesAmount = stats.TotalAmount;
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

        private static IQueryable<SalesInvoice> ApplySalesInvoiceFiltersStatic(
            IQueryable<SalesInvoice> query,
            string code,
            string customerName,
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

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                var term = customerName.Trim().ToLower();
                query = query.Where(i => i.Customer != null && i.Customer.DisplayName != null && i.Customer.DisplayName.ToLower().Contains(term));
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
            var newLine = new SalesInvoiceLineEditor();
            newLine.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(SalesInvoiceLineEditor.TotalPrice))
                    OnPropertyChanged(nameof(FormTotalAmount));
            };
            Lines.Add(newLine);
        }

        [RelayCommand]
        private void RemoveLine(SalesInvoiceLineEditor line)
        {
            if (line != null)
            {
                Lines.Remove(line);
                OnPropertyChanged(nameof(FormTotalAmount));
            }
        }

        [ObservableProperty] private bool _isViewMode;
        [ObservableProperty] private bool _isEditMode;
        private SalesInvoice? _editingInvoice;

        [RelayCommand]
        private void ViewInvoice(SalesInvoice? invoice)
        {
            if (invoice == null) return;
            _editingInvoice = invoice;
            PopulateForm(invoice);
            IsViewMode = true;
            IsEditMode = false;
            SelectedTabIndex = 1; // Switch to form tab
        }

        [RelayCommand]
        private void EditInvoice(SalesInvoice? invoice)
        {
            if (invoice == null) return;
            _editingInvoice = invoice;
            PopulateForm(invoice);
            IsViewMode = false;
            IsEditMode = true;
            SelectedTabIndex = 1; // Switch to form tab
        }

        private void PopulateForm(SalesInvoice invoice)
        {
            InvoiceCode = invoice.InvoiceCode;
            SelectedCustomer = AvailableCustomers.FirstOrDefault(c => c.Id == invoice.CustomerId);
            InvoiceDate = invoice.InvoiceDate;
            DueDate = invoice.DueDate ?? DateTime.Now;
            PaidAmount = invoice.PaidAmount;
            SelectedStockOut = AvailableStockOuts.FirstOrDefault(s => s.Id == invoice.StockOutId);
            SelectedPaymentStatus = StatusToVietnamese(invoice.PaymentStatus ?? "Unpaid");
            Notes = invoice.Notes ?? string.Empty;
            
            Lines.Clear();
            if (invoice.Lines != null)
            {
                foreach (var line in invoice.Lines)
                {
                    Lines.Add(new SalesInvoiceLineEditor
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

            if (SelectedCustomer == null)
            {
                MessageBox.Show("Vui lòng chọn khách hàng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Lines.Any(l => l.SelectedProduct != null))
            {
                MessageBox.Show("Vui lòng thêm ít nhất một mặt hàng hợp lệ!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var invoice = _editingInvoice ?? new SalesInvoice();
                
                invoice.InvoiceCode = InvoiceCode;
                invoice.CustomerId = SelectedCustomer.Id;
                invoice.InvoiceDate = InvoiceDate;
                invoice.DueDate = DueDate;
                invoice.PaidAmount = PaidAmount;
                invoice.Notes = Notes;
                invoice.StockOutId = SelectedStockOut?.Id;
                invoice.PaymentStatus = StatusToEnglish(SelectedPaymentStatus);
                
                if (invoice.Id == 0)
                {
                    invoice.CreatedAt = DateTime.Now;
                    invoice.CreatedBy = _mainViewModel?.CurrentUser?.Id ?? 1;
                }

                // IMPORTANT: Clear navigation properties to avoid EF tracking issues
                invoice.Customer = null!;
                invoice.StockOut = null;
                invoice.Creator = null!;

                // Map lines
                invoice.Lines = Lines.Where(l => l.SelectedProduct != null).Select(l => new SalesInvoiceLine
                {
                    Id = 0,
                    ProductId = l.SelectedProduct!.Id,
                    UnitId = l.SelectedProduct!.DefaultUnitId,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    TaxRate = l.TaxRate,
                    SalesInvoiceId = invoice.Id
                }).ToList();

                _invoiceService.SaveSalesInvoice(invoice);
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

        /// <summary>Initializes form fields without switching the active tab. Used on ViewModel init.</summary>
        private void InitializeForm()
        {
            _editingInvoice = null;
            IsViewMode = false;
            IsEditMode = false;
            InvoiceCode = $"SINV-{DateTime.Now:yyyyMMddHHmmss}";
            SelectedCustomer = null;
            InvoiceDate = DateTime.Now;
            DueDate = DateTime.Now.AddDays(7);
            PaidAmount = 0;
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

        [RelayCommand]
        private void PrintInvoice(SalesInvoice? invoice)
        {
            if (invoice == null) return;
            MessageBox.Show($"In hoá đơn {invoice.InvoiceCode} (Chức năng đang phát triển)", "Thông báo");
        }
    }
}
