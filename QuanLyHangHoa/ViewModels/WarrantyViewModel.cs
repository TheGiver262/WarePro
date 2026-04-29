using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class WarrantyViewModel : ObservableObject
    {
        private readonly Func<string, string, string, int, int> _createClaim;
        private readonly Action<int, string, int> _completeRepair;
        private readonly Action<int, string, int> _sendToManufacturer;
        private readonly Action<int, string, int> _rejectClaim;
        private readonly Action<int, string, string, int> _replaceSerial;
        private readonly Action<string, string> _showMessage;
        private readonly Employee _currentUser;

        [ObservableProperty] private string _claimCode = string.Empty;
        [ObservableProperty] private string _serialNumber = string.Empty;
        [ObservableProperty] private string _problemDescription = string.Empty;
        [ObservableProperty] private string _claimIdText = string.Empty;
        [ObservableProperty] private string _technicalConclusion = string.Empty;
        [ObservableProperty] private string _manufacturerNote = string.Empty;
        [ObservableProperty] private string _rejectionReason = string.Empty;
        [ObservableProperty] private string _replacementSerialNumber = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public WarrantyViewModel(Employee currentUser)
            : this(
                currentUser,
                new WarrantyClaimService().CreateClaim,
                new WarrantyClaimService().CompleteRepair,
                new WarrantyClaimService().SendToManufacturer,
                new WarrantyClaimService().RejectClaim,
                new WarrantyClaimService().ReplaceSerial,
                (message, title) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information))
        {
        }

        public WarrantyViewModel(
            Employee currentUser,
            Func<string, string, string, int, int> createClaim,
            Action<string, string> showMessage)
            : this(
                currentUser,
                createClaim,
                (_, _, _) => { },
                (_, _, _) => { },
                (_, _, _) => { },
                (_, _, _, _) => { },
                showMessage)
        {
        }

        public WarrantyViewModel(
            Employee currentUser,
            Func<string, string, string, int, int> createClaim,
            Action<int, string, int> completeRepair,
            Action<int, string, int> sendToManufacturer,
            Action<int, string, int> rejectClaim,
            Action<int, string, string, int> replaceSerial,
            Action<string, string> showMessage)
        {
            _currentUser = currentUser;
            _createClaim = createClaim;
            _completeRepair = completeRepair;
            _sendToManufacturer = sendToManufacturer;
            _rejectClaim = rejectClaim;
            _replaceSerial = replaceSerial;
            _showMessage = showMessage;
            ClaimCode = CreateDefaultClaimCode();
        }

        [RelayCommand]
        private void CreateWarrantyClaim()
        {
            if (!Validate())
            {
                return;
            }

            try
            {
                var claimId = _createClaim(
                    ClaimCode.Trim(),
                    SerialNumber.Trim(),
                    ProblemDescription.Trim(),
                    _currentUser.Id);

                StatusMessage = $"Da tao phieu bao hanh #{claimId}.";
                _showMessage(StatusMessage, "Thong bao");
                ResetForm();
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                _showMessage(ex.Message, "Loi bao hanh");
            }
        }

        [RelayCommand]
        private void SendManufacturer()
        {
            if (!TryGetClaimId(out var claimId))
            {
                return;
            }

            RunWarrantyAction(
                () => _sendToManufacturer(claimId, ManufacturerNote.Trim(), _currentUser.Id),
                "Da gui claim sang hang.");
        }

        [RelayCommand]
        private void CompleteRepair()
        {
            if (!TryGetClaimId(out var claimId))
            {
                return;
            }

            RunWarrantyAction(
                () => _completeRepair(claimId, TechnicalConclusion.Trim(), _currentUser.Id),
                "Da hoan tat sua bao hanh.");
        }

        [RelayCommand]
        private void RejectWarranty()
        {
            if (!TryGetClaimId(out var claimId))
            {
                return;
            }

            RunWarrantyAction(
                () => _rejectClaim(claimId, RejectionReason.Trim(), _currentUser.Id),
                "Da tu choi va tra may cho khach.");
        }

        [RelayCommand]
        private void ReplaceWarrantySerial()
        {
            if (!TryGetClaimId(out var claimId))
            {
                return;
            }

            RunWarrantyAction(
                () => _replaceSerial(
                    claimId,
                    ReplacementSerialNumber.Trim(),
                    TechnicalConclusion.Trim(),
                    _currentUser.Id),
                "Da doi serial bao hanh.");
        }

        private bool Validate()
        {
            if (string.IsNullOrWhiteSpace(ClaimCode))
            {
                StatusMessage = "Vui long nhap ma phieu bao hanh.";
                _showMessage(StatusMessage, "Canh bao");
                return false;
            }

            if (string.IsNullOrWhiteSpace(SerialNumber))
            {
                StatusMessage = "Vui long nhap serial.";
                _showMessage(StatusMessage, "Canh bao");
                return false;
            }

            if (string.IsNullOrWhiteSpace(ProblemDescription))
            {
                StatusMessage = "Vui long nhap mo ta loi.";
                _showMessage(StatusMessage, "Canh bao");
                return false;
            }

            return true;
        }

        private void ResetForm()
        {
            ClaimCode = CreateDefaultClaimCode();
            SerialNumber = string.Empty;
            ProblemDescription = string.Empty;
        }

        private bool TryGetClaimId(out int claimId)
        {
            if (!int.TryParse(ClaimIdText, out claimId) || claimId <= 0)
            {
                StatusMessage = "ClaimId khong hop le.";
                _showMessage(StatusMessage, "Canh bao");
                return false;
            }

            return true;
        }

        private void RunWarrantyAction(Action action, string successMessage)
        {
            try
            {
                action();
                StatusMessage = successMessage;
                _showMessage(StatusMessage, "Thong bao");
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                _showMessage(ex.Message, "Loi bao hanh");
            }
        }

        private static string CreateDefaultClaimCode()
        {
            return $"WC-{DateTime.Now:yyyyMMddHHmmss}";
        }
    }
}
