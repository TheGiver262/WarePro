using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class InvoicePaymentViewModel : ObservableObject
    {
        private readonly Employee _currentUser;
        private readonly Action<int, decimal, string, string, int> _recordSalesPayment;
        private readonly Action<int, decimal, string, string, int> _recordPurchasePayment;
        private readonly Action<string, string> _showMessage;

        [ObservableProperty] private bool _isSalesMode = true;
        [ObservableProperty] private string _invoiceIdText = string.Empty;
        [ObservableProperty] private decimal _amount;
        [ObservableProperty] private string _paymentMethod = "Cash";
        [ObservableProperty] private string _note = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public InvoicePaymentViewModel(Employee currentUser)
            : this(
                currentUser,
                new InvoicePaymentService().RecordSalesPayment,
                new InvoicePaymentService().RecordPurchasePayment,
                (message, title) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information))
        {
        }

        public InvoicePaymentViewModel(
            Employee currentUser,
            Action<int, decimal, string, string, int> recordSalesPayment,
            Action<int, decimal, string, string, int> recordPurchasePayment,
            Action<string, string> showMessage)
        {
            _currentUser = currentUser;
            _recordSalesPayment = recordSalesPayment;
            _recordPurchasePayment = recordPurchasePayment;
            _showMessage = showMessage;
        }

        public string ModeTitle => IsSalesMode ? "Thu tien hoa don ban" : "Tra tien hoa don mua";

        partial void OnIsSalesModeChanged(bool value)
        {
            OnPropertyChanged(nameof(ModeTitle));
        }

        [RelayCommand]
        private void SwitchToSales() => IsSalesMode = true;

        [RelayCommand]
        private void SwitchToPurchase() => IsSalesMode = false;

        [RelayCommand]
        private void SavePayment()
        {
            if (!int.TryParse(InvoiceIdText, out var invoiceId) || invoiceId <= 0)
            {
                StatusMessage = "InvoiceId khong hop le.";
                _showMessage(StatusMessage, "Canh bao");
                return;
            }

            if (Amount <= 0)
            {
                StatusMessage = "So tien thanh toan phai lon hon 0.";
                _showMessage(StatusMessage, "Canh bao");
                return;
            }

            try
            {
                if (IsSalesMode)
                {
                    _recordSalesPayment(invoiceId, Amount, PaymentMethod.Trim(), Note.Trim(), _currentUser.Id);
                }
                else
                {
                    _recordPurchasePayment(invoiceId, Amount, PaymentMethod.Trim(), Note.Trim(), _currentUser.Id);
                }

                StatusMessage = "Da ghi nhan thanh toan.";
                _showMessage(StatusMessage, "Thong bao");
                Amount = 0m;
                Note = string.Empty;
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                _showMessage(ex.Message, "Loi thanh toan");
            }
        }
    }
}
