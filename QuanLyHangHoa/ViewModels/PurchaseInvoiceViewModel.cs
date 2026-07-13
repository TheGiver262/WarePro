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
        private readonly Func<AppDbContext> _contextFactory;
        private int _skip = 0;
        private const int PageSize = 100;
        private bool _isLoading = false;
        private bool _isInitialized = false;
        private bool _referenceDataLoaded;
        private bool _reloadRequested;
        private readonly DebouncedAction _filterReload = new();

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
            _isInitialized = false;
            SearchInvoiceCode = string.Empty;
            SearchSupplierName = string.Empty;
            FilterStartDate = null;
            FilterEndDate = null;
            SelectedFilterPaymentStatus = "Tất cả";
            FilterLinkDocCode = string.Empty;
            FilterMinTotal = null;
            FilterMaxTotal = null;
            _isInitialized = true;

            LoadData();
        }

        partial void OnSearchInvoiceCodeChanged(string value) => ScheduleFilterReload();
        partial void OnSearchSupplierNameChanged(string value) => ScheduleFilterReload();
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

        public PurchaseInvoiceViewModel(AppUser currentUser, Func<AppDbContext> contextFactory)
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
            if (_isLoading)
            {
                _reloadRequested = true;
                return;
            }

            _ = LoadDataAsync(true);
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
                if (reset)
                {
                    _skip = 0;
                    Invoices.Clear();
                }

                if (reset && !_referenceDataLoaded)
                {
                    var productsTask = Task.Run(() => _productService.GetAllProducts());
                    var suppliersTask = Task.Run(() => _refDataService.GetAllSuppliers());
                    var stockInsTask = Task.Run(() =>
                    {
                        using var context = _contextFactory();
                        return context.StockIns
                            .AsNoTracking()
                            .Select(stockIn => new StockIn
                            {
                                Id = stockIn.Id,
                                DocumentCode = stockIn.DocumentCode
                            })
                            .ToList();
                    });

                    await Task.WhenAll(productsTask, suppliersTask, stockInsTask);

                    AvailableProducts = new ObservableCollection<Product>(await productsTask);
                    AvailableSuppliers = new ObservableCollection<Supplier>(await suppliersTask);
                    AvailableStockIns = new ObservableCollection<StockIn>(await stockInsTask);
                    _referenceDataLoaded = true;
                }

                var paymentStatus = SelectedFilterPaymentStatus != "Tất cả" && !string.IsNullOrEmpty(SelectedFilterPaymentStatus)
                    ? StatusToEnglish(SelectedFilterPaymentStatus)
                    : null;

                var data = await Task.Run(() => _invoiceService.GetPurchaseInvoicesPaged(
                    SearchInvoiceCode, SearchSupplierName, FilterStartDate, FilterEndDate, paymentStatus ?? string.Empty, FilterMinTotal, FilterMaxTotal, _skip, PageSize));

                foreach (var inv in data)
                {
                    Invoices.Add(inv);
                }
                _skip += data.Count;

                // Thống kê đếm bất đồng bộ từ database (gộp thành 1 truy vấn duy nhất)
                await Task.Run(() =>
                {
                    using var db = _contextFactory();
                    var query = db.PurchaseInvoices.AsNoTracking().AsQueryable();
                    query = ApplyPurchaseInvoiceFiltersStatic(query, SearchInvoiceCode, SearchSupplierName, FilterStartDate, FilterEndDate, paymentStatus ?? string.Empty, FilterMinTotal, FilterMaxTotal);
                    
                    var today = DateTime.Today;
                    var stats = query.GroupBy(i => 1)
                        .Select(g => new
                        {
                            TotalCount = g.Count(),
                            TotalAmount = g.Sum(i => i.GrandTotal),
                            Paid = g.Count(i => i.PaymentStatus == PaymentStatus.Paid),
                            Partial = g.Count(i => i.PaymentStatus == PaymentStatus.PartiallyPaid
                                && (!i.DueDate.HasValue || i.DueDate.Value >= today)),
                            Unpaid = g.Count(i => i.PaymentStatus == PaymentStatus.Unpaid
                                && (!i.DueDate.HasValue || i.DueDate.Value >= today)),
                            Overdue = g.Count(i => i.PaymentStatus != PaymentStatus.Paid
                                && i.DueDate.HasValue
                                && i.DueDate.Value < today)
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
                if (_reloadRequested)
                {
                    _reloadRequested = false;
                    LoadData();
                }
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
            SelectedPaymentStatus = StatusToVietnamese(invoice.PaymentStatus ?? PaymentStatus.Unpaid);
            Notes = invoice.Notes ?? string.Empty;
            
            Lines.Clear();
            if (invoice.Lines != null)
            {
                foreach (var line in invoice.Lines)
                {
                    Lines.Add(new PurchaseInvoiceLineEditor
                    {
                        SelectedProduct = AvailableProducts.FirstOrDefault(p => p.Id == line.ProductId),
                        SourceLineId = line.StockInLineId,
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
                    invoice.CreatedBy = _currentUser.Id;
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
                    UnitId = l.SourceUnitId ?? l.SelectedProduct!.DefaultUnitId,
                    StockInLineId = l.SourceLineId,
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
        private void ExportToExcel()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = "HoaDonMua_" + DateTime.Now.ToString("yyyyMMdd_HHmm")
                };
                if (dialog.ShowDialog() != true) return;
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("HoaDonMua");
                var headers = new[] { "Số hóa đơn", "Ngày", "Nhà cung cấp", "Trước thuế", "Thuế", "Tổng tiền", "Đã trả", "Trạng thái" };
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
                    worksheet.Cell(row, 3).Value = invoice.Supplier?.DisplayName ?? string.Empty;
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
        private void CreateFromStockIn()
        {
            InitializeForm();
            SelectedTabIndex = 1;
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

        [RelayCommand]
        private void PrintInvoice(PurchaseInvoice? invoice)
        {
            if (invoice == null) return;
            try
            {
                var model = new DocumentPrintService(_contextFactory).LoadPurchaseInvoice(invoice.Id);
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
