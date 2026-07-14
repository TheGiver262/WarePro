using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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

    public partial class SalesInvoiceViewModel : ObservableObject, IRefreshable
    {
        private readonly InvoiceService _invoiceService;
        private readonly ProductService _productService;
        private readonly ReferenceDataService _refDataService;
        private readonly Func<AppDbContext> _contextFactory;
        private int _skip = 0;
        private const int PageSize = 100;
        private bool _isLoading = false;
        private bool _isInitialized = false;
        private bool _referenceDataLoaded;
        private bool _reloadRequested;
        private readonly DebouncedAction _filterReload = new();

        [ObservableProperty] private ObservableCollection<SalesInvoice> _invoices = new();
        [ObservableProperty] private SalesInvoice? _selectedInvoice;
        [ObservableProperty] private string? _loadErrorMessage;

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

            RefreshData();
        }

        partial void OnSearchInvoiceCodeChanged(string value) => ScheduleFilterReload();
        partial void OnSearchCustomerNameChanged(string value) => ScheduleFilterReload();
        partial void OnFilterStartDateChanged(DateTime? value) => ScheduleFilterReload();
        partial void OnFilterEndDateChanged(DateTime? value) => ScheduleFilterReload();
        partial void OnSelectedFilterPaymentStatusChanged(string? value) => ScheduleFilterReload();
        partial void OnFilterLinkDocCodeChanged(string? value) => ScheduleFilterReload();
        partial void OnFilterMinTotalChanged(decimal? value) => ScheduleFilterReload();
        partial void OnFilterMaxTotalChanged(decimal? value) => ScheduleFilterReload();

        [ObservableProperty] [NotifyPropertyChangedFor(nameof(FormRemainingAmount))] private decimal _formTotalAmount;
        [ObservableProperty] private decimal _formSubTotal;
        [ObservableProperty] private decimal _formTaxAmount;
        public decimal FormRemainingAmount => FormTotalAmount - PaidAmount;

        private readonly AppUser _currentUser;

        public SalesInvoiceViewModel(AppUser currentUser, Func<AppDbContext> contextFactory)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(contextFactory);
            _currentUser = currentUser;
            _contextFactory = contextFactory;
            _invoiceService = new InvoiceService(_contextFactory);
            _productService = new ProductService(_contextFactory);
            _refDataService = new ReferenceDataService(_contextFactory);

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
            if (_isLoading)
            {
                _reloadRequested = true;
                return;
            }

            _ = LoadDataAsync(true);
        }

        public void RefreshData()
        {
            _referenceDataLoaded = false;
            LoadData();
        }

        private void ScheduleFilterReload()
        {
            if (_isInitialized)
            {
                _filterReload.Schedule(LoadData);
            }
        }

        private async Task LoadDataAsync(bool reset)
        {
            if (_isLoading) return;
            _isLoading = true;
            try
            {
                var querySkip = reset ? 0 : _skip;
                if (reset && !_referenceDataLoaded)
                {
                    var productsTask = Task.Run(() => _productService.GetAllProducts());
                    var customersTask = Task.Run(() => _refDataService.GetAllCustomers());
                    var stockOutsTask = Task.Run(() =>
                    {
                        using var context = _contextFactory();
                        return context.StockOuts
                            .AsNoTracking()
                            .Select(stockOut => new StockOut
                            {
                                Id = stockOut.Id,
                                DocumentCode = stockOut.DocumentCode
                            })
                            .ToList();
                    });

                    await Task.WhenAll(productsTask, customersTask, stockOutsTask);
                    AvailableProducts = new ObservableCollection<Product>(await productsTask);
                    AvailableCustomers = new ObservableCollection<Customer>(await customersTask);
                    AvailableStockOuts = new ObservableCollection<StockOut>(await stockOutsTask);
                    _referenceDataLoaded = true;
                }

                var paymentStatus = SelectedFilterPaymentStatus != "Tất cả"
                    && !string.IsNullOrEmpty(SelectedFilterPaymentStatus)
                    ? StatusToEnglish(SelectedFilterPaymentStatus)
                    : null;
                var data = await Task.Run(() => _invoiceService.GetSalesInvoicesPaged(
                    SearchInvoiceCode,
                    SearchCustomerName,
                    FilterStartDate,
                    FilterEndDate,
                    paymentStatus ?? string.Empty,
                    FilterMinTotal,
                    FilterMaxTotal,
                    querySkip,
                    PageSize));
                var stats = await Task.Run(() =>
                {
                    using var db = _contextFactory();
                    var query = ApplySalesInvoiceFiltersStatic(
                        db.SalesInvoices.AsNoTracking().AsQueryable(),
                        SearchInvoiceCode,
                        SearchCustomerName,
                        FilterStartDate,
                        FilterEndDate,
                        paymentStatus ?? string.Empty,
                        FilterMinTotal,
                        FilterMaxTotal);
                    var today = DateTime.Today;
                    return query.GroupBy(invoice => 1)
                        .Select(group => new
                        {
                            TotalCount = group.Count(),
                            TotalAmount = group.Sum(invoice => invoice.GrandTotal),
                            Paid = group.Count(invoice => invoice.PaymentStatus == PaymentStatus.Paid),
                            Partial = group.Count(invoice =>
                                invoice.PaymentStatus == PaymentStatus.PartiallyPaid
                                && (!invoice.DueDate.HasValue || invoice.DueDate.Value >= today)),
                            Unpaid = group.Count(invoice =>
                                invoice.PaymentStatus == PaymentStatus.Unpaid
                                && (!invoice.DueDate.HasValue || invoice.DueDate.Value >= today)),
                            Overdue = group.Count(invoice =>
                                invoice.PaymentStatus != PaymentStatus.Paid
                                && invoice.DueDate.HasValue
                                && invoice.DueDate.Value < today)
                        })
                        .FirstOrDefault();
                });

                if (reset)
                    Invoices = new ObservableCollection<SalesInvoice>(data);
                else
                    foreach (var invoice in data)
                        Invoices.Add(invoice);

                _skip = querySkip + data.Count;
                TotalSalesCount = stats?.TotalCount ?? 0;
                TotalSalesAmount = stats?.TotalAmount ?? 0m;
                PaidCount = stats?.Paid ?? 0;
                PartialCount = stats?.Partial ?? 0;
                UnpaidCount = stats?.Unpaid ?? 0;
                OverdueCount = stats?.Overdue ?? 0;
                LoadErrorMessage = null;
            }
            catch (Exception ex)
            {
                LoadErrorMessage = ex.Message;
            }
            finally
            {
                _isLoading = false;
                if (_reloadRequested)
                {
                    _reloadRequested = false;
                    _referenceDataLoaded = false;
                    LoadData();
                }
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
                query = InvoicePaymentStatusFilter.Apply(query, paymentStatus);
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
            SelectedPaymentStatus = StatusToVietnamese(invoice.PaymentStatus ?? PaymentStatus.Unpaid);
            Notes = invoice.Notes ?? string.Empty;

            Lines.Clear();
            if (invoice.Lines != null)
            {
                foreach (var line in invoice.Lines)
                {
                    Lines.Add(new SalesInvoiceLineEditor
                    {
                        SelectedProduct = AvailableProducts.FirstOrDefault(p => p.Id == line.ProductId),
                        SourceLineId = line.StockOutLineId,
                        SourceUnitId = line.UnitId,
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
                    invoice.CreatedBy = _currentUser.Id;
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
                    UnitId = l.SourceUnitId ?? l.SelectedProduct!.DefaultUnitId,
                    StockOutLineId = l.SourceLineId,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    TaxRate = l.TaxRate,
                    SalesInvoiceId = invoice.Id
                }).ToList();

                _invoiceService.SaveSalesInvoice(invoice, _currentUser.Id);
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
                "Chưa TT" => PaymentStatus.Unpaid,
                "TT 1 phần" => PaymentStatus.PartiallyPaid,
                "Đã TT" => PaymentStatus.Paid,
                "Quá hạn" => PaymentStatus.Overdue,
                _ => PaymentStatus.Unpaid
            };
        }

        private string StatusToVietnamese(string englishStatus)
        {
            return englishStatus switch
            {
                PaymentStatus.Unpaid => "Chưa TT",
                PaymentStatus.PartiallyPaid => "TT 1 phần",
                PaymentStatus.Paid => "Đã TT",
                PaymentStatus.Overdue => "Quá hạn",
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
        private void ExportToExcel()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = "HoaDonBan_" + DateTime.Now.ToString("yyyyMMdd_HHmm")
                };
                if (dialog.ShowDialog() != true) return;
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("HoaDonBan");
                var headers = new[] { "Số hóa đơn", "Ngày", "Khách hàng", "Trước thuế", "Thuế", "Tổng tiền", "Đã trả", "Trạng thái" };
                for (var column = 0; column < headers.Length; column++)
                {
                    var cell = worksheet.Cell(1, column + 1);
                    cell.Value = headers[column];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
                }
                for (var index = 0; index < Invoices.Count; index++)
                {
                    var invoice = Invoices[index];
                    var row = index + 2;
                    worksheet.Cell(row, 1).Value = invoice.InvoiceCode;
                    worksheet.Cell(row, 2).Value = invoice.InvoiceDate;
                    worksheet.Cell(row, 2).Style.DateFormat.Format = "dd/MM/yyyy";
                    worksheet.Cell(row, 3).Value = invoice.Customer?.DisplayName ?? string.Empty;
                    worksheet.Cell(row, 4).Value = invoice.SubTotal;
                    worksheet.Cell(row, 5).Value = invoice.TaxAmount;
                    worksheet.Cell(row, 6).Value = invoice.GrandTotal;
                    worksheet.Cell(row, 7).Value = invoice.PaidAmount;
                    worksheet.Cell(row, 8).Value = invoice.PaymentStatus;
                }
                worksheet.Range(2, 4, Math.Max(2, Invoices.Count + 1), 7).Style.NumberFormat.Format = "#,##0";
                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(dialog.FileName);
                MessageBox.Show("Xuất file Excel thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi xuất Excel: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void ResetForm()
        {
            InitializeForm();
            SelectedTabIndex = 1; // Explicitly switch to form tab when user creates new
        }

        [RelayCommand]
        private void CreateFromStockOut()
        {
            InitializeForm();
            SelectedTabIndex = 1;
        }

        [RelayCommand]
        private void PrintInvoice(SalesInvoice? invoice)
        {
            if (invoice == null) return;
            try
            {
                var model = new DocumentPrintService(_contextFactory).LoadSalesInvoice(invoice.Id);
                new Views.DocumentPrintWindow(model).ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở bản in hóa đơn: {ex.Message}", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
