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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace QuanLyHangHoa.ViewModels
{
    public partial class WarrantyViewModel : ObservableObject, IRefreshable
    {
        private readonly WarrantyClaimService _warrantyService;
        private readonly Action<string, string> _showMessage;
        private readonly AppUser _currentUser;
        private readonly Func<AppDbContext> _contextFactory;
        private CancellationTokenSource? _filterDebounceCts;
        private CancellationTokenSource? _loadCts;

        // Create Claim fields
        [ObservableProperty] private string _claimCode = string.Empty;
        [ObservableProperty] private string _serialNumber = string.Empty;
        [ObservableProperty] private string _problemDescription = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private ObservableCollection<string> _availableSerials = new();

        // List & Filter fields
        [ObservableProperty] private ObservableCollection<WarrantyClaim> _warranties = new();
        [ObservableProperty] private WarrantyClaim? _selectedWarranty;
        [ObservableProperty] private string _searchSerial = string.Empty;
        [ObservableProperty] private string _searchCustomer = string.Empty;
        [ObservableProperty] private string _searchClaimCode = string.Empty;
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

        // Detailed stats for footer
        [ObservableProperty] private int _openCount;
        [ObservableProperty] private int _manufacturerWaitCount;
        [ObservableProperty] private int _readyCount;
        [ObservableProperty] private int _closedCount;
        [ObservableProperty] private int _rejectedCount;

        // Detail panel visibility
        [ObservableProperty] private bool _isDetailPanelOpen;

        public bool CanCompleteRepair => IsActionAllowed(WarrantyClaimAction.CompleteShopRepair);

        public bool CanSendManufacturer => IsActionAllowed(WarrantyClaimAction.Send);

        public bool CanReceiveManufacturerRepaired =>
            IsActionAllowed(WarrantyClaimAction.ReceiveManufacturerRepair);

        public bool CanReceiveManufacturerReplaced =>
            IsActionAllowed(WarrantyClaimAction.ReceiveManufacturerReplacement);

        public bool CanReceiveManufacturerActions =>
            CanReceiveManufacturerRepaired || CanReceiveManufacturerReplaced;

        public bool CanRejectWarranty => IsActionAllowed(WarrantyClaimAction.Reject);

        public bool CanReplaceWarrantySerial => IsActionAllowed(WarrantyClaimAction.ReplaceFromStock);

        public bool IsSelectedWarrantyMutable => SelectedWarranty != null
            && !WarrantyClaimTransitions.IsTerminal(SelectedWarranty.Status);

        // trạng thái nút dùng cùng WarrantyClaimTransitions với service để UI và backend không lệch luật
        private bool IsActionAllowed(WarrantyClaimAction action) =>
            SelectedWarranty != null
            && WarrantyClaimTransitions.IsAllowed(SelectedWarranty, action);

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
            SearchSerial = string.Empty;
            SearchCustomer = string.Empty;
            SearchClaimCode = string.Empty;
            SelectedStatusFilter = "Tất cả";
            SearchFromDate = null;
            SearchToDate = null;
        }

        partial void OnSearchSerialChanged(string value) => ScheduleFilterReload();
        partial void OnSearchCustomerChanged(string value) => ScheduleFilterReload();
        partial void OnSearchClaimCodeChanged(string value) => ScheduleFilterReload();
        partial void OnSelectedStatusFilterChanged(string value) => ScheduleFilterReload();
        partial void OnSearchFromDateChanged(DateTime? value) => ScheduleFilterReload();
        partial void OnSearchToDateChanged(DateTime? value) => ScheduleFilterReload();

        // debounce filter danh sách claim, hủy lượt chờ cũ khi người dùng tiếp tục nhập
        private void ScheduleFilterReload()
        {
            _filterDebounceCts?.Cancel();
            _filterDebounceCts?.Dispose();
            _filterDebounceCts = new CancellationTokenSource();
            _ = ReloadAfterDelayAsync(_filterDebounceCts.Token);
        }

        private async Task ReloadAfterDelayAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(300, cancellationToken);
                await LoadData();
            }
            catch (OperationCanceledException)
            {
            }
        }

        [RelayCommand]
        // tải claim và thống kê ở worker, sau đó thay collection một lần để giảm nhấp nháy UI
        public async Task LoadData()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            var cancellationToken = _loadCts.Token;

            try
            {
                using var db = _contextFactory();
                var today = DateTime.Today;

                var stats = await db.WarrantyClaims
                    .AsNoTracking()
                    .GroupBy(_ => 1)
                    .Select(group => new
                    {
                        Total = group.Count(),
                        Repairing = group.Count(claim => claim.Status == "Open" || claim.Status == "ManufacturerWait"),
                        Completed = group.Count(claim => claim.Status == "Ready"),
                        Overdue = group.Count(claim => claim.ExpectedReturnDate.HasValue
                            && claim.ExpectedReturnDate.Value < today
                            && claim.Status != "Closed"
                            && claim.Status != "Rejected"),
                        Open = group.Count(claim => claim.Status == "Open"),
                        ManufacturerWait = group.Count(claim => claim.Status == "ManufacturerWait"),
                        Ready = group.Count(claim => claim.Status == "Ready"),
                        Closed = group.Count(claim => claim.Status == "Closed"),
                        Rejected = group.Count(claim => claim.Status == "Rejected")
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                var query = db.WarrantyClaims
                    .AsNoTracking()
                    .Include(claim => claim.ProductSerial)
                    .ThenInclude(serial => serial.Product)
                    .Include(claim => claim.WarrantyCoverage)
                    .ThenInclude(coverage => coverage.Customer)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(SearchClaimCode))
                {
                    var term = SearchClaimCode.ToLower();
                    query = query.Where(claim => claim.ClaimCode != null && claim.ClaimCode.ToLower().Contains(term));
                }

                if (!string.IsNullOrWhiteSpace(SearchSerial))
                {
                    var term = SearchSerial.ToLower();
                    query = query.Where(claim => claim.ProductSerial != null
                        && claim.ProductSerial.SerialNumber.ToLower().Contains(term));
                }

                if (!string.IsNullOrWhiteSpace(SearchCustomer))
                {
                    var term = SearchCustomer.ToLower();
                    query = query.Where(claim => claim.WarrantyCoverage != null
                        && claim.WarrantyCoverage.Customer != null
                        && claim.WarrantyCoverage.Customer.DisplayName.ToLower().Contains(term));
                }

                if (SelectedStatusFilter != "Tất cả")
                {
                    query = query.Where(claim => claim.Status == SelectedStatusFilter);
                }

                if (SearchFromDate.HasValue)
                {
                    query = query.Where(claim => claim.ReceivedDate >= SearchFromDate.Value);
                }

                if (SearchToDate.HasValue)
                {
                    query = query.Where(claim => claim.ReceivedDate <= SearchToDate.Value);
                }

                var claims = await query
                    .OrderByDescending(claim => claim.ReceivedDate)
                    .ToListAsync(cancellationToken);

                TotalWarrantyCount = stats?.Total ?? 0;
                RepairingCount = stats?.Repairing ?? 0;
                CompletedCount = stats?.Completed ?? 0;
                OverdueCount = stats?.Overdue ?? 0;
                OpenCount = stats?.Open ?? 0;
                ManufacturerWaitCount = stats?.ManufacturerWait ?? 0;
                ReadyCount = stats?.Ready ?? 0;
                ClosedCount = stats?.Closed ?? 0;
                RejectedCount = stats?.Rejected ?? 0;
                Warranties = new ObservableCollection<WarrantyClaim>(claims);
            }
            catch (OperationCanceledException)
            {
            }
        }

        [RelayCommand]
        private void CreateWarranty()
        {
            SelectedWarranty = null;
            ResetForm();

            LoadAvailableSerials();

            var createClaimWindow = new Views.CreateWarrantyWindow(this);
            createClaimWindow.ShowDialog();
        }

        // chỉ lấy serial có coverage còn hiệu lực làm lựa chọn tạo claim
        public void LoadAvailableSerials()
        {
            try
            {
                using var db = _contextFactory();
                var serials = db.ProductSerials
                    .Where(s => s.WarrantyCoverage != null
                        && s.WarrantyCoverage.CoverageStatus == "Active"
                        && s.WarrantyCoverage.WarrantyStartDate.Date <= DateTime.Today
                        && s.WarrantyCoverage.WarrantyEndDate.Date >= DateTime.Today)
                    .Select(s => s.SerialNumber)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();
                AvailableSerials = new ObservableCollection<string>(serials);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi tải danh sách Serial: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CreateWarrantyClaimAsync(object? parameter)
        {
            if (!Validate()) return;
            var operationId = Guid.NewGuid();

            try
            {
                var claimId = await _warrantyService.CreateClaimAsync(
                    ClaimCode.Trim(),
                    SerialNumber.Trim(),
                    ProblemDescription.Trim(),
                    _currentUser.Id,
                    operationId);

                StatusMessage = $"Đã tạo phiếu bảo hành #{claimId}.";
                _showMessage(StatusMessage, "Thông báo");
                ResetForm();
                await LoadData();

                if (parameter is Window window)
                {
                    window.Close();
                }
            }
            catch (Exception)
            {
                StatusMessage = DatabaseWriteUi.TechnicalErrorMessage;
                _showMessage(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi bảo hành");
            }
        }

        [RelayCommand(CanExecute = nameof(IsSelectedWarrantyMutable))]
        private async Task SaveWarrantyAsync()
        {
            var selected = SelectedWarranty;
            if (selected == null) return;
            await RunWarrantyActionAsync(
                operationId => _warrantyService.UpdateClaimAsync(
                    selected.Id,
                    selected.ProblemDescription,
                    selected.ExpectedReturnDate,
                    selected.RowVersion,
                    _currentUser.Id,
                    operationId),
                "Cập nhật phiếu bảo hành thành công!");
        }

        [RelayCommand(CanExecute = nameof(IsSelectedWarrantyMutable))]
        private async Task DeleteWarrantyAsync()
        {
            var selected = SelectedWarranty;
            if (selected == null) return;
            if (MessageBox.Show("Bạn có chắc chắn muốn xóa phiếu bảo hành này?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            await RunWarrantyActionAsync(
                operationId => _warrantyService.DeleteClaimAsync(
                    selected.Id,
                    selected.RowVersion,
                    _currentUser.Id,
                    operationId),
                "Đã xóa phiếu bảo hành.");
        }

        [RelayCommand(CanExecute = nameof(CanCompleteRepair))]
        private async Task CompleteRepairAsync()
        {
            var selected = SelectedWarranty;
            if (selected == null) return;
            await RunWarrantyActionAsync(
                operationId => _warrantyService.CompleteRepairAsync(
                    selected.Id,
                    TechnicalConclusion.Trim(),
                    _currentUser.Id,
                    selected.RowVersion,
                    operationId),
                "Đã hoàn tất sửa bảo hành.");
        }

        [RelayCommand(CanExecute = nameof(CanSendManufacturer))]
        private async Task SendManufacturerAsync()
        {
            var selected = SelectedWarranty;
            if (selected == null) return;
            await RunWarrantyActionAsync(
                operationId => _warrantyService.SendToManufacturerAsync(
                    selected.Id,
                    ManufacturerName.Trim(),
                    ManufacturerTrackingCode.Trim(),
                    ManufacturerExpectedReturnDate,
                    ManufacturerNote.Trim(),
                    _currentUser.Id,
                    selected.RowVersion,
                    operationId),
                "Đã gửi hãng bảo hành.");
        }

        [RelayCommand(CanExecute = nameof(CanReceiveManufacturerRepaired))]
        private async Task ReceiveManufacturerRepairedAsync()
        {
            var selected = SelectedWarranty;
            if (selected == null) return;
            await RunWarrantyActionAsync(
                operationId => _warrantyService.ReceiveFromManufacturerRepairedAsync(
                    selected.Id,
                    TechnicalConclusion.Trim(),
                    _currentUser.Id,
                    selected.RowVersion,
                    operationId),
                "Hãng đã sửa xong, serial cũ trả lại khách.");
        }

        [RelayCommand(CanExecute = nameof(CanReceiveManufacturerReplaced))]
        private async Task ReceiveManufacturerReplacedAsync()
        {
            var selected = SelectedWarranty;
            if (selected == null) return;
            if (string.IsNullOrWhiteSpace(NewManufacturerSerial))
            {
                _showMessage("Vui lòng nhập Serial mới từ hãng.", "Cảnh báo");
                return;
            }

            await RunWarrantyActionAsync(
                operationId => _warrantyService.ReceiveFromManufacturerReplacedAsync(
                    selected.Id,
                    NewManufacturerSerial.Trim(),
                    TechnicalConclusion.Trim(),
                    _currentUser.Id,
                    selected.RowVersion,
                    operationId),
                "Hãng đã đổi mới, đã tạo phiếu nhập/xuất kho tự động.");
        }

        [RelayCommand(CanExecute = nameof(CanRejectWarranty))]
        private async Task RejectWarrantyAsync()
        {
            var selected = SelectedWarranty;
            if (selected == null) return;
            await RunWarrantyActionAsync(
                operationId => _warrantyService.RejectClaimAsync(
                    selected.Id,
                    RejectionReason.Trim(),
                    _currentUser.Id,
                    selected.RowVersion,
                    operationId),
                "Đã từ chối và trả máy cho khách.");
        }

        [RelayCommand(CanExecute = nameof(CanReplaceWarrantySerial))]
        private async Task ReplaceWarrantySerialAsync()
        {
            var selected = SelectedWarranty;
            if (selected == null) return;
            if (string.IsNullOrWhiteSpace(ReplacementSerialNumber))
            {
                _showMessage("Vui lòng nhập Serial thay thế.", "Cảnh báo");
                return;
            }

            await RunWarrantyActionAsync(
                operationId => _warrantyService.ReplaceSerialAsync(
                    selected.Id,
                    ReplacementSerialNumber.Trim(),
                    TechnicalConclusion.Trim(),
                    selected.RowVersion,
                    _currentUser.Id,
                    operationId),
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
        // copy claim đang chọn sang field chi tiết và cập nhật CanExecute của toàn bộ action
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

        partial void OnSelectedWarrantyChanged(WarrantyClaim? value)
        {
            OnPropertyChanged(nameof(CanCompleteRepair));
            OnPropertyChanged(nameof(CanSendManufacturer));
            OnPropertyChanged(nameof(CanReceiveManufacturerRepaired));
            OnPropertyChanged(nameof(CanReceiveManufacturerReplaced));
            OnPropertyChanged(nameof(CanReceiveManufacturerActions));
            OnPropertyChanged(nameof(CanRejectWarranty));
            OnPropertyChanged(nameof(CanReplaceWarrantySerial));
            OnPropertyChanged(nameof(IsSelectedWarrantyMutable));

            CompleteRepairCommand.NotifyCanExecuteChanged();
            SendManufacturerCommand.NotifyCanExecuteChanged();
            ReceiveManufacturerRepairedCommand.NotifyCanExecuteChanged();
            ReceiveManufacturerReplacedCommand.NotifyCanExecuteChanged();
            RejectWarrantyCommand.NotifyCanExecuteChanged();
            ReplaceWarrantySerialCommand.NotifyCanExecuteChanged();
            SaveWarrantyCommand.NotifyCanExecuteChanged();
            DeleteWarrantyCommand.NotifyCanExecuteChanged();
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

        private async Task RunWarrantyActionAsync(
            Func<Guid, Task> action,
            string successMessage)
        {
            var operationId = Guid.NewGuid();
            try
            {
                await action(operationId);
                StatusMessage = successMessage;
                _showMessage(StatusMessage, "Thông báo");
                await LoadData();
                IsDetailPanelOpen = false;
            }
            catch (DatabaseWriteConflictException)
            {
                StatusMessage = "Phiếu bảo hành đã được thay đổi ở máy khác.";
                _showMessage($"{StatusMessage} Dữ liệu mới nhất sẽ được tải lại.", "Dữ liệu đã thay đổi");
                await LoadData();
                IsDetailPanelOpen = false;
            }
            catch (Exception)
            {
                StatusMessage = DatabaseWriteUi.TechnicalErrorMessage;
                _showMessage(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi bảo hành");
            }
        }

        private static string CreateDefaultClaimCode()
        {
            return $"WC-{DateTime.Now:yyyyMMddHHmmss}";
        }

        public void RefreshData()
        {
            _ = LoadData();
        }
    }
}
