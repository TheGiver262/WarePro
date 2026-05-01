using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;

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
        private readonly AppUser _currentUser;

        [ObservableProperty] private string _claimCode = string.Empty;
        [ObservableProperty] private string _serialNumber = string.Empty;
        [ObservableProperty] private string _problemDescription = string.Empty;
        [ObservableProperty] private string _claimIdText = string.Empty;
        [ObservableProperty] private string _technicalConclusion = string.Empty;
        [ObservableProperty] private string _manufacturerNote = string.Empty;
        [ObservableProperty] private string _rejectionReason = string.Empty;
        [ObservableProperty] private string _replacementSerialNumber = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;
        
        [ObservableProperty] private ObservableCollection<WarrantyClaim> _warranties = new();
        [ObservableProperty] private WarrantyClaim? _selectedWarranty;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private List<string> _statusList = new() { "Open", "Ready", "ManufacturerWait", "Closed", "Rejected" };

        private readonly AppDbContext _dbContext;

        public WarrantyViewModel(AppUser currentUser, AppDbContext dbContext)
            : this(
                currentUser,
                new WarrantyClaimService(() => dbContext).CreateClaim,
                new WarrantyClaimService(() => dbContext).CompleteRepair,
                new WarrantyClaimService(() => dbContext).SendToManufacturer,
                new WarrantyClaimService(() => dbContext).RejectClaim,
                new WarrantyClaimService(() => dbContext).ReplaceSerial,
                (message, title) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information))
        {
            _dbContext = dbContext;
            LoadData();
        }

        public WarrantyViewModel(
            AppUser currentUser,
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
            _dbContext = new AppDbContext();
            LoadData();
        }

        public WarrantyViewModel(
            AppUser currentUser,
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
            _dbContext = new AppDbContext();
            ClaimCode = CreateDefaultClaimCode();
        }

        [RelayCommand]
        public void LoadData()
        {
            var query = _dbContext.WarrantyClaims
                .Include("ProductSerial")
                .Include("ProductSerial.Product")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(c => 
                    (c.ClaimCode != null && c.ClaimCode.Contains(SearchText)) ||
                    (c.ProductSerial != null && c.ProductSerial.SerialNumber != null && c.ProductSerial.SerialNumber.Contains(SearchText))
                );
            }

            Warranties = new ObservableCollection<WarrantyClaim>(query.ToList());
        }

        [RelayCommand]
        private void SaveWarranty()
        {
            if (SelectedWarranty == null) return;
            try
            {
                _dbContext.WarrantyClaims.Update(SelectedWarranty);
                _dbContext.SaveChanges();
                _showMessage("Cập nhật phiếu bảo hành thành công!", "Thông báo");
                LoadData();
            }
            catch (Exception ex)
            {
                _showMessage(ex.Message, "Lỗi");
            }
        }

        [RelayCommand]
        private void DeleteWarranty()
        {
            if (SelectedWarranty == null) return;
            if (MessageBox.Show("Bạn có chắc chắn muốn xóa phiếu bảo hành này?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    _dbContext.WarrantyClaims.Remove(SelectedWarranty);
                    _dbContext.SaveChanges();
                    _showMessage("Đã xóa phiếu bảo hành.", "Thông báo");
                    LoadData();
                }
                catch (Exception ex)
                {
                    _showMessage(ex.Message, "Lỗi");
                }
            }
        }

        [RelayCommand]
        private void CreateWarranty() => ResetForm();

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

                StatusMessage = $"Đã tạo phiếu bảo hành #{claimId}.";
                _showMessage(StatusMessage, "Thông báo");
                ResetForm();
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                _showMessage(ex.Message, "Lỗi bảo hành");
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
                "Đã gửi claim sang hãng.");
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
                "Đã hoàn tất sửa bảo hành.");
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
                "Đã từ chối và trả máy cho khách.");
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
                "Đã đổi serial bảo hành.");
        }

        private bool Validate()
        {
            if (string.IsNullOrWhiteSpace(ClaimCode))
            {
                StatusMessage = "Vui lòng nhập mã phiếu bảo hành.";
                _showMessage(StatusMessage, "Cảnh báo");
                return false;
            }

            if (string.IsNullOrWhiteSpace(SerialNumber))
            {
                StatusMessage = "Vui lòng nhập serial.";
                _showMessage(StatusMessage, "Cảnh báo");
                return false;
            }

            if (string.IsNullOrWhiteSpace(ProblemDescription))
            {
                StatusMessage = "Vui lòng nhập mô tả lỗi.";
                _showMessage(StatusMessage, "Cảnh báo");
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
                StatusMessage = "ClaimId không hợp lệ.";
                _showMessage(StatusMessage, "Cảnh báo");
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
                _showMessage(StatusMessage, "Thông báo");
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                _showMessage(ex.Message, "Lỗi bảo hành");
            }
        }

        private static string CreateDefaultClaimCode()
        {
            return $"WC-{DateTime.Now:yyyyMMddHHmmss}";
        }
    }
}
