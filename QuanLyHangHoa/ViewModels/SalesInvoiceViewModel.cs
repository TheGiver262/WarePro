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
        [ObservableProperty] private decimal _taxRate;

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                UnitPrice = value.DefaultPrice;
            }
        }
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
        [ObservableProperty] private DateTime _invoiceDate = DateTime.Now;
        [ObservableProperty] private decimal _paidAmount;
        [ObservableProperty] private string _notes = string.Empty;

        [ObservableProperty] private decimal _totalSalesAmount;
        [ObservableProperty] private decimal _totalPaidAmount;
        [ObservableProperty] private decimal _totalDebtAmount;
        [ObservableProperty] private int _totalSalesCount;

        private readonly MainViewModel? _mainViewModel;

        public SalesInvoiceViewModel() : this(null) { }

        public SalesInvoiceViewModel(MainViewModel? mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _invoiceService = new InvoiceService();
            _productService = new ProductService();
            _refDataService = new ReferenceDataService();

            LoadInitialData();
            ResetForm();
        }

        private void LoadInitialData()
        {
            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            AvailableCustomers = new ObservableCollection<Customer>(_refDataService.GetAllCustomers());
            var allInvoices = _invoiceService.GetAllSalesInvoices();
            Invoices = new ObservableCollection<SalesInvoice>(allInvoices);

            UpdateSummaries(allInvoices);
        }

        private void UpdateSummaries(System.Collections.Generic.IEnumerable<SalesInvoice> allInvoices)
        {
            TotalSalesCount = allInvoices.Count();
            TotalSalesAmount = allInvoices.Sum(i => i.GrandTotal);
            TotalPaidAmount = allInvoices.Sum(i => i.PaidAmount);
            TotalDebtAmount = TotalSalesAmount - TotalPaidAmount;
        }

        [RelayCommand]
        private void OpenPayment()
        {
            if (SelectedInvoice == null) return;
            
            if (_mainViewModel != null)
            {
                _mainViewModel.OpenInvoicePaymentView(SelectedInvoice.Id, true);
            }
        }

        [RelayCommand]
        private void AddLine()
        {
            Lines.Add(new SalesInvoiceLineEditor());
        }

        [RelayCommand]
        private void RemoveLine(SalesInvoiceLineEditor line)
        {
            if (line != null) Lines.Remove(line);
        }

        [RelayCommand]
        private void SaveInvoice()
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("Vui lòng chọn khách hàng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Lines.Any())
            {
                MessageBox.Show("Vui lòng thêm ít nhất một mặt hàng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var invoice = new SalesInvoice
                {
                    InvoiceCode = InvoiceCode,
                    CustomerId = SelectedCustomer.Id,
                    InvoiceDate = InvoiceDate,
                    PaidAmount = PaidAmount,
                    Notes = Notes,
                    CreatedAt = DateTime.Now,
                    Lines = Lines.Select(l => new SalesInvoiceLine
                    {
                        ProductId = l.SelectedProduct?.Id ?? 0,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        TaxRate = l.TaxRate
                    }).ToList()
                };

                _invoiceService.SaveSalesInvoice(invoice);
                MessageBox.Show("Lưu hóa đơn bán hàng thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadInitialData(); // Refresh list and summaries
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
            InvoiceCode = $"SINV-{DateTime.Now:yyyyMMddHHmmss}";
            SelectedCustomer = null;
            InvoiceDate = DateTime.Now;
            PaidAmount = 0;
            Notes = string.Empty;
            Lines.Clear();
            Lines.Add(new SalesInvoiceLineEditor());
        }
    }
}
