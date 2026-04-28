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
        private readonly Action<string, string> _showMessage;
        private readonly Employee _currentUser;

        [ObservableProperty] private string _claimCode = string.Empty;
        [ObservableProperty] private string _serialNumber = string.Empty;
        [ObservableProperty] private string _problemDescription = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public WarrantyViewModel(Employee currentUser)
            : this(
                currentUser,
                new WarrantyClaimService().CreateClaim,
                (message, title) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information))
        {
        }

        public WarrantyViewModel(
            Employee currentUser,
            Func<string, string, string, int, int> createClaim,
            Action<string, string> showMessage)
        {
            _currentUser = currentUser;
            _createClaim = createClaim;
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
                MessageBox.Show(ex.Message, "Loi bao hanh", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool Validate()
        {
            if (string.IsNullOrWhiteSpace(ClaimCode))
            {
                StatusMessage = "Vui long nhap ma phieu bao hanh.";
                MessageBox.Show(StatusMessage, "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(SerialNumber))
            {
                StatusMessage = "Vui long nhap serial.";
                MessageBox.Show(StatusMessage, "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(ProblemDescription))
            {
                StatusMessage = "Vui long nhap mo ta loi.";
                MessageBox.Show(StatusMessage, "Canh bao", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private static string CreateDefaultClaimCode()
        {
            return $"WC-{DateTime.Now:yyyyMMddHHmmss}";
        }
    }
}
