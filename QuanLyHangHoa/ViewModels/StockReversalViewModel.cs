using System;
using System.Threading;
using System.Threading.Tasks;
using QuanLyHangHoa.Data;
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
        private readonly Func<string, int, int, Guid, CancellationToken, Task<int>> _reverseDocument;
        private readonly Action<string, string> _showMessage;

        [ObservableProperty] private string _documentType = "StockIn";
        [ObservableProperty] private string _documentIdText = string.Empty;
        [ObservableProperty] private string _reason = "WrongPosting";
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isWriting;
        [ObservableProperty] private string _writeStatus = string.Empty;

        private void ResetAfterWriteFailure()
        {
            DocumentType = "StockIn";
            DocumentIdText = string.Empty;
            Reason = "WrongPosting";
            StatusMessage = string.Empty;
        }
        private Task<bool> ExecuteWriteAsync(
            Func<CancellationToken, Task> write,
            CancellationToken cancellationToken) =>
            DatabaseWriteUi.ExecuteAsync(
                write,
                () => IsWriting,
                value => IsWriting = value,
                value => WriteStatus = value,
                ResetAfterWriteFailure,
                message => _showMessage(message, "Lỗi"),
                cancellationToken);
        public StockReversalViewModel(AppUser currentUser, Func<AppDbContext>? contextFactory = null)
            : this(
                currentUser,
                new StockReversalService(contextFactory ?? (() => new QuanLyHangHoa.Data.AppDbContext())).ReverseDocumentAsync,
                (message, title) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information))
        {
        }

        public StockReversalViewModel(
            AppUser currentUser,
            Func<string, int, int, Guid, CancellationToken, Task<int>> reverseDocument,
            Action<string, string> showMessage)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            _currentUser = currentUser;
            _reverseDocument = reverseDocument;
            _showMessage = showMessage;
        }

        [RelayCommand]
        // ViewModel chỉ kiểm tra input và hiển thị kết quả; transaction đảo kho nằm hoàn toàn trong StockReversalService
        private async Task ReverseDocument(CancellationToken cancellationToken)
        {
            var operationId = Guid.NewGuid();
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
                var adjustmentId = 0;
                if (!await ExecuteWriteAsync(
                    async _ =>
                    {
                        adjustmentId = await _reverseDocument(DocumentType, documentId, _currentUser.Id, operationId, cancellationToken);
                    },
                    cancellationToken)) return;
                if (adjustmentId <= 0)
                {
                    StatusMessage = "Không tìm thấy chứng từ kho đã ghi sổ.";
                    _showMessage(StatusMessage, "Lỗi đảo chứng từ");
                    return;
                }

                StatusMessage = $"Đã đảo chứng từ kho, adjustment #{adjustmentId}.";
                _showMessage(StatusMessage, "Thông báo");
            }
            catch (Exception)
            {
                StatusMessage = DatabaseWriteUi.TechnicalErrorMessage;
                _showMessage(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi đảo chứng từ");
            }
        }
    }
}
