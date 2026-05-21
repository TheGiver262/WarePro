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
    public partial class WarrantyViewModel : ObservableObject, IRefreshable
    {
        private readonly WarrantyClaimService _warrantyService;
        private readonly Action<string, string> _showMessage;
        private readonly AppUser _currentUser;
        private readonly Func<AppDbContext> _contextFactory;

        // Create Claim fields
        [ObservableProperty] private string _claimCode = string.Empty;
        [ObservableProperty] private string _serialNumber = string.Empty;
        [ObservableProperty] private string _problemDescription = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        // List & Filter fields
        [ObservableProperty] private ObservableCollection<WarrantyClaim> _warranties = new();
        [ObservableProperty] private WarrantyClaim? _selectedWarranty;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedStatusFilter = "Tất cả";
        [ObservableProperty] private DateTime? _searchFromDate;
        [ObservableProperty] private DateTime? _searchToDate;
        [ObservableProperty] private bool _isAdvancedFilterOpen;
        [ObservableProperty] private List<string> _statusList = new() { "Tất cả", "Open", "Ready", "ManufacturerWait", "Closed", "Rejected" };

        // Resolution fields
        [ObservableProperty] private string _technicalConclusion = string.Empty;
        [ObservableProperty] private string _manufacturerNote = string.Empty;
        [ObservableProperty] private string _rejectionReason = string.Empty;
        [ObservableProperty] private string _replacementSerialNumber = string.Empty;

        // Manufacturer tracking fields
        [ObservableProperty] private string _manufacturerName = string.Empty;
        [ObservableProperty] private string _manufacturerTrackingCode = string.Empty;
        [ObservableProperty] private DateTime? _manufacturerExpectedReturnDate;
        [ObservableProperty] private string _newManufacturerSerial = string.Empty;

        // Summary stats
        [ObservableProperty] private int _totalWarrantyCount;
        [ObservableProperty] private int _repairingCount;
        [ObservableProperty] private int _completedCount;
        [ObservableProperty] private int _overdueCount;

        // Detail panel visibility
        [ObservableProperty] private bool _isDetailPanelOpen;

        public WarrantyViewModel(AppUser currentUser, Func<AppDbContext> contextFactory)
            : this(
                currentUser,
                contextFactory,
                (message, title) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information))
        {
        }

        public WarrantyViewModel(
            AppUser currentUser,
            Func<AppDbContext> contextFactory,
            Action<string, string> showMessage)
        {
            _currentUser = currentUser;
            _contextFactory = contextFactory;
            _warrantyService = new WarrantyClaimService(contextFactory);
            _showMessage = showMessage;
            ClaimCode = CreateDefaultClaimCode();
        }

        // Keep backward-compatible constructor for tests
        public WarrantyViewModel(
            AppUser currentUser,
            Func<AppDbContext> contextFactory,
            Func<string, string, string, int, int> createClaim,
            Action<string, string> showMessage)
        {
            _currentUser = currentUser;
            _contextFactory = contextFactory;
            _warrantyService = new WarrantyClaimService(contextFactory);
            _showMessage = showMessage;
            ClaimCode = CreateDefaultClaimCode();
        }

        [RelayCommand]
        private void ToggleAdvancedFilter() => IsAdvancedFilterOpen = !IsAdvancedFilterOpen;

        [RelayCommand]
        private void ResetFilter()
        {
            SearchText = string.Empty;
            SelectedStatusFilter = "Tất cả";
            SearchFromDate = null;
            SearchToDate = null;
            LoadData();
        }

        [RelayCommand]
        public void LoadData()
        {
            using var db = _contextFactory();
            var query = db.WarrantyClaims
                .Include(c => c.ProductSerial)
                .ThenInclude(s => s.Product)
                .Include(c => c.WarrantyCoverage)
                .ThenInclude(wc => wc.Customer)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.ToLower();
                query = query.Where(c =>
                    (c.ClaimCode != null && c.ClaimCode.ToLower().Contains(term)) ||
                    (c.ProductSerial != null && c.ProductSerial.SerialNumber != null && c.ProductSerial.SerialNumber.ToLower().Contains(term)) ||
                    (c.ProductSerial != null && c.ProductSerial.Product != null && c.ProductSerial.Product.DisplayName != null && c.ProductSerial.Product.DisplayName.ToLower().Contains(term))
                );
            }

            if (SelectedStatusFilter != "Tất cả")
            {
                query = query.Where(c => c.Status == SelectedStatusFilter);
            }

            if (SearchFromDate.HasValue)
            {
                query = query.Where(c => c.ReceivedDate >= SearchFromDate.Value);
            }

            if (SearchToDate.HasValue)
            {
                query = query.Where(c => c.ReceivedDate <= SearchToDate.Value);
            }

            var allClaims = query.OrderByDescending(c => c.ReceivedDate).ToList();
            Warranties = new ObservableCollection<WarrantyClaim>(allClaims);

            // Update summary stats
            TotalWarrantyCount = allClaims.Count;
            RepairingCount = allClaims.Count(c => c.Status == "Open" || c.Status == "ManufacturerWait");
            CompletedCount = allClaims.Count(c => c.Status == "Ready");
            OverdueCount = allClaims.Count(c => c.ExpectedReturnDate.HasValue && c.ExpectedReturnDate.Value.Date < DateTime.Today && c.Status != "Closed" && c.Status != "Rejected");
        }

        [RelayCommand]
        private void CreateWarranty()
        {
            SelectedWarranty = null;
            ResetForm();
            IsDetailPanelOpen = true;
        }

        [RelayCommand]
        private void CreateWarrantyClaim()
        {
            if (!Validate()) return;

            try
            {
                var claimId = _warrantyService.CreateClaim(
                    ClaimCode.Trim(),
                    SerialNumber.Trim(),
                    ProblemDescription.Trim(),
                    _currentUser.Id);

                StatusMessage = $"Đã tạo phiếu bảo hành #{claimId}.";
                _showMessage(StatusMessage, "Thông báo");
                ResetForm();
                LoadData();
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                _showMessage(ex.Message, "Lỗi bảo hành");
            }
        }

        [RelayCommand]
        private void SaveWarranty()
        {
            if (SelectedWarranty == null) return;
            try
            {
                _warrantyService.UpdateClaim(SelectedWarranty);
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
                    _warrantyService.DeleteClaim(SelectedWarranty.Id);
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
        private void CompleteRepair()
        {
            if (SelectedWarranty == null) return;
            RunWarrantyAction(
                () => _warrantyService.CompleteRepair(SelectedWarranty.Id, TechnicalConclusion.Trim(), _currentUser.Id),
                "Đã hoàn tất sửa bảo hành.");
        }

        [RelayCommand]
        private void SendManufacturer()
        {
            if (SelectedWarranty == null) return;
            RunWarrantyAction(
                () => _warrantyService.SendToManufacturer(
                    SelectedWarranty.Id,
                    ManufacturerName.Trim(),
                    ManufacturerTrackingCode.Trim(),
                    ManufacturerExpectedReturnDate,
                    ManufacturerNote.Trim(),
                    _currentUser.Id),
                "Đã gửi hãng bảo hành.");
        }

        [RelayCommand]
        private void ReceiveManufacturerRepaired()
        {
            if (SelectedWarranty == null) return;
            RunWarrantyAction(
                () => _warrantyService.ReceiveFromManufacturerRepaired(
                    SelectedWarranty.Id, TechnicalConclusion.Trim(), _currentUser.Id),
                "Hãng đã sửa xong, serial cũ trả lại khách.");
        }

        [RelayCommand]
        private void ReceiveManufacturerReplaced()
        {
            if (SelectedWarranty == null) return;
            if (string.IsNullOrWhiteSpace(NewManufacturerSerial))
            {
                _showMessage("Vui lòng nhập Serial mới từ hãng.", "Cảnh báo");
                return;
            }
            RunWarrantyAction(
                () => _warrantyService.ReceiveFromManufacturerReplaced(
                    SelectedWarranty.Id,
                    NewManufacturerSerial.Trim(),
                    TechnicalConclusion.Trim(),
                    _currentUser.Id),
                "Hãng đã đổi mới, đã tạo phiếu nhập/xuất kho tự động.");
        }

        [RelayCommand]
        private void RejectWarranty()
        {
            if (SelectedWarranty == null) return;
            RunWarrantyAction(
                () => _warrantyService.RejectClaim(SelectedWarranty.Id, RejectionReason.Trim(), _currentUser.Id),
                "Đã từ chối và trả máy cho khách.");
        }

        [RelayCommand]
        private void ReplaceWarrantySerial()
        {
            if (SelectedWarranty == null) return;
            if (string.IsNullOrWhiteSpace(ReplacementSerialNumber))
            {
                _showMessage("Vui lòng nhập Serial thay thế.", "Cảnh báo");
                return;
            }
            RunWarrantyAction(
                () => _warrantyService.ReplaceSerial(
                    SelectedWarranty.Id,
                    ReplacementSerialNumber.Trim(),
                    TechnicalConclusion.Trim(),
                    _currentUser.Id),
                "Đã đổi serial bảo hành từ kho.");
        }

        [RelayCommand]
        private void PrintWarranty()
        {
            if (SelectedWarranty == null)
            {
                _showMessage("Vui lòng chọn phiếu bảo hành để in.", "Cảnh báo");
                return;
            }

            try
            {
                using var db = _contextFactory();
                var claim = db.WarrantyClaims
                    .Include(c => c.ProductSerial)
                        .ThenInclude(s => s.Product)
                    .Include(c => c.WarrantyCoverage)
                        .ThenInclude(wc => wc.Customer)
                    .Include(c => c.Processor)
                    .FirstOrDefault(c => c.Id == SelectedWarranty.Id);

                if (claim == null)
                {
                    _showMessage("Không tìm thấy phiếu bảo hành.", "Lỗi");
                    return;
                }

                var printWindow = new Views.WarrantyPrintWindow(claim);
                printWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _showMessage(ex.Message, "Lỗi");
            }
        }

        [RelayCommand]
        private void ViewDetail()
        {
            if (SelectedWarranty == null) return;

            // Populate fields from selected warranty for editing
            TechnicalConclusion = SelectedWarranty.TechnicalConclusion ?? string.Empty;
            ManufacturerNote = SelectedWarranty.ManufacturerResult ?? string.Empty;
            ManufacturerName = SelectedWarranty.ManufacturerName ?? string.Empty;
            ManufacturerTrackingCode = SelectedWarranty.ManufacturerTrackingCode ?? string.Empty;
            ManufacturerExpectedReturnDate = SelectedWarranty.ManufacturerExpectedReturnDate;
            RejectionReason = SelectedWarranty.RejectionReason ?? string.Empty;
            NewManufacturerSerial = string.Empty;
            ReplacementSerialNumber = string.Empty;
            IsDetailPanelOpen = true;
        }

        [RelayCommand]
        private void CloseDetail()
        {
            IsDetailPanelOpen = false;
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

        private void RunWarrantyAction(Action action, string successMessage)
        {
            try
            {
                action();
                StatusMessage = successMessage;
                _showMessage(StatusMessage, "Thông báo");
                LoadData();
                IsDetailPanelOpen = false;
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                _showMessage(ex.Message, "Lỗi bảo hành");
            }
        }

        private static string CreateDefaultClaimCode()
        {
            return $"WC-{DateTime.Now:yyyyMMddHHmmss}";
        }

        public void RefreshData()
        {
            LoadData();
        }
    }
}
