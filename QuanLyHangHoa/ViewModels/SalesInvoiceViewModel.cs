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
        [ObservableProperty] private string _searchInvoiceCode = string.Empty;
        [ObservableProperty] private string _searchCustomerName = string.Empty;
        [ObservableProperty] private int _selectedTabIndex = 0; // 0: List, 1: Create
        [ObservableProperty] private DateTime? _filterStartDate;
        [ObservableProperty] private DateTime? _filterEndDate;
        [ObservableProperty] private string? _selectedFilterPaymentStatus;
        [ObservableProperty] private string? _filterLinkDocCode;
        [ObservableProperty] private ObservableCollection<string> _availablePaymentStatuses = new() { "Tất cả", "Chưa TT", "TT 1 phần", "Đã TT", "Quá hạn" };

        partial void OnSearchInvoiceCodeChanged(string value) => LoadData();
        partial void OnSearchCustomerNameChanged(string value) => LoadData();
        partial void OnFilterStartDateChanged(DateTime? value) => LoadData();
        partial void OnFilterEndDateChanged(DateTime? value) => LoadData();
        partial void OnSelectedFilterPaymentStatusChanged(string? value) => LoadData();
        partial void OnFilterLinkDocCodeChanged(string? value) => LoadData();

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
            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            AvailableCustomers = new ObservableCollection<Customer>(_refDataService.GetAllCustomers());
            
            using (var context = _mainViewModel?.ContextFactory?.Invoke() ?? new QuanLyHangHoa.Data.AppDbContext())
            {
                AvailableStockOuts = new ObservableCollection<StockOut>(context.StockOuts.ToList());
            }
            var allInvoices = _invoiceService.GetAllSalesInvoices();

            // Apply Filters
            if (!string.IsNullOrWhiteSpace(SearchInvoiceCode))
            {
                allInvoices = allInvoices.Where(i => i.InvoiceCode != null && i.InvoiceCode.Contains(SearchInvoiceCode, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(SearchCustomerName))
            {
                allInvoices = allInvoices.Where(i => i.Customer != null && i.Customer.DisplayName != null && i.Customer.DisplayName.Contains(SearchCustomerName, StringComparison.OrdinalIgnoreCase)).ToList();
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
                // Note: LinkDocCode logic can be extended if there's a field for it
                // For now, we search in Notes as a placeholder if needed, or skip if not in model
            }

            Invoices = new ObservableCollection<SalesInvoice>(allInvoices.OrderByDescending(i => i.InvoiceDate));
            UpdateSummaries(allInvoices);
        }

        private void UpdateSummaries(System.Collections.Generic.IEnumerable<SalesInvoice> allInvoices)
        {
            TotalSalesCount = allInvoices.Count();
            TotalSalesAmount = allInvoices.Sum(i => i.GrandTotal);
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
            SelectedPaymentStatus = invoice.PaymentStatus ?? "Chưa TT";
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
