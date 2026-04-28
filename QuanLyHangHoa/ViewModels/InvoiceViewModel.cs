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
    public partial class InvoiceLineEditor : ObservableObject
    {
        public event EventHandler? TotalsChanged;

        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private decimal _quantity = 1m;
        [ObservableProperty] private decimal _unitPrice;
        [ObservableProperty] private decimal _taxRate;

        public int UnitId => SelectedProduct?.UnitId ?? 0;
        public string UnitName => SelectedProduct?.Unit?.Name ?? string.Empty;
        public decimal SubTotal => Quantity * UnitPrice;
        public decimal TaxAmount => SubTotal * TaxRate;
        public decimal GrandTotal => SubTotal + TaxAmount;

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                UnitPrice = value.UnitPrice;
            }

            OnPropertyChanged(nameof(UnitId));
            OnPropertyChanged(nameof(UnitName));
            NotifyTotalsChanged();
        }

        partial void OnQuantityChanged(decimal value) => NotifyTotalsChanged();
        partial void OnUnitPriceChanged(decimal value) => NotifyTotalsChanged();
        partial void OnTaxRateChanged(decimal value) => NotifyTotalsChanged();

        private void NotifyTotalsChanged()
        {
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(TaxAmount));
            OnPropertyChanged(nameof(GrandTotal));
            TotalsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public partial class InvoiceViewModel : ObservableObject
    {
        private readonly InvoiceService _invoiceService;
        private readonly ProductService _productService;
        private readonly ReferenceDataService _refDataService;

        [ObservableProperty] private bool _isSalesMode = true;
        [ObservableProperty] private ObservableCollection<Product> _availableProducts;
        [ObservableProperty] private ObservableCollection<Customer> _availableCustomers;
        [ObservableProperty] private ObservableCollection<Supplier> _availableSuppliers;
        [ObservableProperty] private Customer? _selectedCustomer;
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private string _invoiceCode = string.Empty;
        [ObservableProperty] private DateTime _invoiceDate = DateTime.Now;
        [ObservableProperty] private DateTime _dueDate = DateTime.Now.AddDays(7);
        [ObservableProperty] private decimal _paidAmount;
        [ObservableProperty] private ObservableCollection<InvoiceLineEditor> _lines;
        [ObservableProperty] private decimal _subTotal;
        [ObservableProperty] private decimal _taxAmount;
        [ObservableProperty] private decimal _grandTotal;
        [ObservableProperty] private string _paymentStatus = "Unpaid";

        public InvoiceViewModel()
        {
            _invoiceService = new InvoiceService();
            _productService = new ProductService();
            _refDataService = new ReferenceDataService();

            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            AvailableCustomers = new ObservableCollection<Customer>(_refDataService.GetAllCustomers());
            AvailableSuppliers = new ObservableCollection<Supplier>(_refDataService.GetAllSuppliers());
            Lines = new ObservableCollection<InvoiceLineEditor>();
            InvoiceCode = CreateDefaultInvoiceCode();
        }

        public string ModeTitle => IsSalesMode ? "Hoa don ban hang" : "Hoa don mua hang";
        public string PartyLabel => IsSalesMode ? "Khach hang" : "Nha cung cap";
        public string SaveButtonText => IsSalesMode ? "Luu hoa don ban" : "Luu hoa don mua";

        partial void OnIsSalesModeChanged(bool value)
        {
            SelectedCustomer = null;
            SelectedSupplier = null;
            InvoiceCode = CreateDefaultInvoiceCode();
            OnPropertyChanged(nameof(ModeTitle));
            OnPropertyChanged(nameof(PartyLabel));
            OnPropertyChanged(nameof(SaveButtonText));
        }

        partial void OnPaidAmountChanged(decimal value) => RecalculateTotals();

        [RelayCommand]
        private void SwitchToSales() => IsSalesMode = true;

        [RelayCommand]
        private void SwitchToPurchase() => IsSalesMode = false;

        [RelayCommand]
        private void AddLine()
        {
            var line = new InvoiceLineEditor();
            line.TotalsChanged += OnLineTotalsChanged;
            Lines.Add(line);
            RecalculateTotals();
        }

        [RelayCommand]
        private void RemoveLine(InvoiceLineEditor line)
        {
            if (line == null)
            {
                return;
            }

            line.TotalsChanged -= OnLineTotalsChanged;
            Lines.Remove(line);
            RecalculateTotals();
        }

        [RelayCommand]
        private void SaveInvoice()
        {
            if (!ValidateInvoice())
            {
                return;
            }

            try
            {
                if (IsSalesMode)
                {
                    _invoiceService.SaveSalesInvoice(CreateSalesInvoice());
                }
                else
                {
                    _invoiceService.SavePurchaseInvoice(CreatePurchaseInvoice());
                }

                MessageBox.Show("Luu hoa don thanh cong!", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Information);
                ResetForm();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Loi du lieu", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private SalesInvoice CreateSalesInvoice()
        {
            var invoice = new SalesInvoice
            {
                InvoiceCode = InvoiceCode.Trim(),
                InvoiceDate = InvoiceDate,
                DueDate = DueDate,
                CustomerId = SelectedCustomer!.Id,
                PaidAmount = PaidAmount
            };

            foreach (var line in Lines)
            {
                invoice.Lines.Add(new SalesInvoiceLine
                {
                    ProductId = line.SelectedProduct!.Id,
                    UnitId = line.UnitId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    TaxRate = line.TaxRate
                });
            }

            return invoice;
        }

        private PurchaseInvoice CreatePurchaseInvoice()
        {
            var invoice = new PurchaseInvoice
            {
                InvoiceCode = InvoiceCode.Trim(),
                InvoiceDate = InvoiceDate,
                DueDate = DueDate,
                SupplierId = SelectedSupplier!.Id,
                PaidAmount = PaidAmount
            };

            foreach (var line in Lines)
            {
                invoice.Lines.Add(new PurchaseInvoiceLine
                {
                    ProductId = line.SelectedProduct!.Id,
                    UnitId = line.UnitId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    TaxRate = line.TaxRate
                });
            }

            return invoice;
        }

        private bool ValidateInvoice()
        {
            if (string.IsNullOrWhiteSpace(InvoiceCode))
            {
                MessageBox.Show("Vui long nhap ma hoa don.", "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (IsSalesMode && SelectedCustomer == null)
            {
                MessageBox.Show("Vui long chon khach hang.", "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!IsSalesMode && SelectedSupplier == null)
            {
                MessageBox.Show("Vui long chon nha cung cap.", "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!Lines.Any() || Lines.Any(line => line.SelectedProduct == null))
            {
                MessageBox.Show("Vui long chon san pham cho tat ca cac dong.", "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (Lines.Any(line => line.Quantity <= 0 || line.UnitPrice < 0 || line.TaxRate < 0))
            {
                MessageBox.Show("So luong, don gia va thue suat khong hop le.", "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (PaidAmount < 0 || PaidAmount > GrandTotal)
            {
                MessageBox.Show("So tien da thanh toan phai nam trong khoang 0 den tong tien.", "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void OnLineTotalsChanged(object? sender, EventArgs e) => RecalculateTotals();

        private void RecalculateTotals()
        {
            SubTotal = Lines.Sum(line => line.SubTotal);
            TaxAmount = Lines.Sum(line => line.TaxAmount);
            GrandTotal = Lines.Sum(line => line.GrandTotal);
            PaymentStatus = GetPaymentStatus();
        }

        private string GetPaymentStatus()
        {
            if (GrandTotal <= 0 || PaidAmount <= 0)
            {
                return "Unpaid";
            }

            return PaidAmount >= GrandTotal ? "Paid" : "Partial";
        }

        private void ResetForm()
        {
            foreach (var line in Lines)
            {
                line.TotalsChanged -= OnLineTotalsChanged;
            }

            Lines.Clear();
            SelectedCustomer = null;
            SelectedSupplier = null;
            PaidAmount = 0m;
            InvoiceDate = DateTime.Now;
            DueDate = DateTime.Now.AddDays(7);
            InvoiceCode = CreateDefaultInvoiceCode();
            RecalculateTotals();
        }

        private string CreateDefaultInvoiceCode()
        {
            var prefix = IsSalesMode ? "HD-BAN" : "HD-MUA";
            return $"{prefix}-{DateTime.Now:yyyyMMddHHmmss}";
        }
    }
}
