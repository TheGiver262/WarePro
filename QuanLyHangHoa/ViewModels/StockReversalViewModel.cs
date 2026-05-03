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
        private readonly AppUser _currentUser;
        private readonly Func<string, int, int, int> _reverseDocument;
        private readonly Action<string, string> _showMessage;

        [ObservableProperty] private string _documentType = "StockIn";
        [ObservableProperty] private string _documentIdText = string.Empty;
        [ObservableProperty] private string _reason = "WrongPosting";
        [ObservableProperty] private string _statusMessage = string.Empty;

        public StockReversalViewModel(AppUser currentUser)
            : this(
                currentUser,
                new StockReversalService().ReversePostedLedgerDocument,
                (message, title) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information))
        {
        }

        public StockReversalViewModel(
            AppUser currentUser,
            Func<string, int, int, int> reverseDocument,
            Action<string, string> showMessage)
        {
            _currentUser = currentUser;
            _reverseDocument = reverseDocument;
            _showMessage = showMessage;
        }

        [RelayCommand]
        private void ReverseDocument()
        {
            if (!int.TryParse(DocumentIdText, out var documentId))
            {
                StatusMessage = "DocumentId không hợp lệ.";
                _showMessage(StatusMessage, "Cảnh báo");
                return;
            }

            if (string.IsNullOrWhiteSpace(Reason))
            {
                StatusMessage = "Vui lòng nhập lý do đảo chứng từ.";
                _showMessage(StatusMessage, "Cảnh báo");
                return;
            }

            try
            {
                var adjustmentId = _reverseDocument(DocumentType, documentId, _currentUser.Id);
                StatusMessage = $"Đã đảo chứng từ kho, adjustment #{adjustmentId}.";
                _showMessage(StatusMessage, "Thông báo");
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                _showMessage(ex.Message, "Lỗi đảo chứng từ");
            }
        }
    }
}
