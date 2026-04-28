using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class StockReversalViewModel : ObservableObject
    {
        private readonly Employee _currentUser;
        private readonly Func<Guid, string, int, int> _reverseDocument;
        private readonly Action<string, string> _showMessage;

        [ObservableProperty] private string _documentIdText = string.Empty;
        [ObservableProperty] private string _reason = "WrongPosting";
        [ObservableProperty] private string _statusMessage = string.Empty;

        public StockReversalViewModel(Employee currentUser)
            : this(
                currentUser,
                new StockReversalService().ReversePostedLedgerDocument,
                (message, title) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information))
        {
        }

        public StockReversalViewModel(
            Employee currentUser,
            Func<Guid, string, int, int> reverseDocument,
            Action<string, string> showMessage)
        {
            _currentUser = currentUser;
            _reverseDocument = reverseDocument;
            _showMessage = showMessage;
        }

        [RelayCommand]
        private void ReverseDocument()
        {
            if (!Guid.TryParse(DocumentIdText, out var documentId))
            {
                StatusMessage = "DocumentId khong hop le.";
                _showMessage(StatusMessage, "Canh bao");
                return;
            }

            if (string.IsNullOrWhiteSpace(Reason))
            {
                StatusMessage = "Vui long nhap ly do dao chung tu.";
                _showMessage(StatusMessage, "Canh bao");
                return;
            }

            try
            {
                var adjustmentId = _reverseDocument(documentId, Reason.Trim(), _currentUser.Id);
                StatusMessage = $"Da dao chung tu kho, adjustment #{adjustmentId}.";
                _showMessage(StatusMessage, "Thong bao");
            }
            catch (InventoryDomainException ex)
            {
                StatusMessage = ex.Message;
                _showMessage(ex.Message, "Loi dao chung tu");
            }
        }
    }
}
