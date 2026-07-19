using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class WarrantyCoverageViewModel : ObservableObject, IRefreshable
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly WarrantyClaimService _warrantyService;
        private readonly int _actorId;
        private CancellationTokenSource? _filterDebounceCts;
        private CancellationTokenSource? _loadCts;
        private int _loadGeneration;

        [ObservableProperty] private ObservableCollection<WarrantyCoverage> _coverages = new();
        [ObservableProperty] private WarrantyCoverage? _selectedCoverage;
        [ObservableProperty] private string _searchSerial = string.Empty;
        [ObservableProperty] private string _searchCustomer = string.Empty;
        [ObservableProperty] private string _searchProduct = string.Empty;
        [ObservableProperty] private string _selectedStatusFilter = "Tất cả";
        [ObservableProperty] private string? _loadErrorMessage;

        public ObservableCollection<string> StatusFilterOptions { get; } = new() { "Tất cả", "Active", "Expired", "Voided" };

        // Form properties for New/Edit
        [ObservableProperty] private string _serialNumber = string.Empty;
        [ObservableProperty] private DateTime _startDate = DateTime.Now;
        [ObservableProperty] private DateTime _endDate = DateTime.Now.AddYears(1);
        [ObservableProperty] private string _status = "Active";

        // Footer statistics
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _activeCount;
        [ObservableProperty] private int _expiredCount;
        [ObservableProperty] private int _voidedCount;

        // Drawer control
        [ObservableProperty] private bool _isDetailPanelOpen;

        public WarrantyCoverageViewModel(AppUser currentUser, Func<AppDbContext> contextFactory)
        {
            _actorId = currentUser.Id;
            _contextFactory = contextFactory;
            _warrantyService = new WarrantyClaimService(contextFactory);
            _ = LoadData();
        }

        internal WarrantyCoverageViewModel(Func<AppDbContext> contextFactory)
            : this(new AppUser { Id = 1 }, contextFactory)
        {
        }

        partial void OnSearchSerialChanged(string value) => ScheduleFilterReload();
        partial void OnSearchCustomerChanged(string value) => ScheduleFilterReload();
        partial void OnSearchProductChanged(string value) => ScheduleFilterReload();
        partial void OnSelectedStatusFilterChanged(string value) => ScheduleFilterReload();

        [RelayCommand]
        public void ResetFilters()
        {
            SearchSerial = string.Empty;
            SearchCustomer = string.Empty;
            SearchProduct = string.Empty;
            SelectedStatusFilter = "Tất cả";
            ScheduleFilterReload();
        }

        // debounce tìm kiếm; token mới hủy lần chờ cũ
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
        // chụp filter rồi tải snapshot coverage; trạng thái Expired được suy ra theo ngày hiện tại
        public async Task LoadData()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            var cancellationToken = _loadCts.Token;
            var generation = Interlocked.Increment(ref _loadGeneration);

            try
            {
                using var db = _contextFactory();

                var allCoverages = await db.WarrantyCoverages
                    .AsNoTracking()
                    .Include(coverage => coverage.ProductSerial!)
                    .ThenInclude(serial => serial.Product!)
                    .Include(coverage => coverage.Customer!)
                    .ToListAsync(cancellationToken);

                var today = DateTime.Today;
                foreach (var coverage in allCoverages)
                {
                    coverage.EffectiveCoverageStatus = WarrantyClaimService.GetEffectiveCoverageStatus(
                        coverage.CoverageStatus,
                        coverage.WarrantyEndDate,
                        today);
                }

                var filtered = allCoverages.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(SearchSerial))
                {
                    var term = SearchSerial.Trim();
                    filtered = filtered.Where(coverage => coverage.ProductSerial != null
                        && coverage.ProductSerial.SerialNumber.Contains(term));
                }

                if (!string.IsNullOrWhiteSpace(SearchCustomer))
                {
                    var term = SearchCustomer.Trim();
                    filtered = filtered.Where(coverage => coverage.Customer != null
                        && coverage.Customer.DisplayName.Contains(term));
                }

                if (!string.IsNullOrWhiteSpace(SearchProduct))
                {
                    var term = SearchProduct.Trim();
                    filtered = filtered.Where(coverage => coverage.ProductSerial != null
                        && coverage.ProductSerial.Product != null
                        && coverage.ProductSerial.Product.DisplayName.Contains(term));
                }

                if (!string.IsNullOrWhiteSpace(SelectedStatusFilter)
                    && SelectedStatusFilter != "Tất cả")
                {
                    filtered = filtered.Where(coverage =>
                        coverage.EffectiveCoverageStatus == SelectedStatusFilter);
                }

                var coverages = filtered
                    .OrderBy(coverage => coverage.ProductSerial.SerialNumber)
                    .ToList();

                cancellationToken.ThrowIfCancellationRequested();
                if (generation != Volatile.Read(ref _loadGeneration))
                    throw new OperationCanceledException(cancellationToken);
                TotalCount = allCoverages.Count;
                ActiveCount = allCoverages.Count(coverage => coverage.EffectiveCoverageStatus == "Active");
                ExpiredCount = allCoverages.Count(coverage => coverage.EffectiveCoverageStatus == "Expired");
                VoidedCount = allCoverages.Count(coverage => coverage.EffectiveCoverageStatus == "Voided");
                Coverages = new ObservableCollection<WarrantyCoverage>(coverages);
                LoadErrorMessage = null;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested && generation == Volatile.Read(ref _loadGeneration))
                    LoadErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        // copy ngày/status vào form chi tiết, không sửa entity trong danh sách trước khi Save
        private void EditCoverage(WarrantyCoverage coverage)
        {
            SelectedCoverage = coverage;
            SerialNumber = coverage.ProductSerial?.SerialNumber ?? string.Empty;
            StartDate = coverage.WarrantyStartDate;
            EndDate = coverage.WarrantyEndDate;
            Status = coverage.CoverageStatus;
            IsDetailPanelOpen = true;
        }

        [RelayCommand]
        private void CloseDetail()
        {
            IsDetailPanelOpen = false;
        }

        [RelayCommand]
        private async Task SaveCoverageAsync()
        {
            var selected = SelectedCoverage;
            if (selected == null) return;

            try
            {
                WarrantyClaimService.EnsureValidCoverageDates(StartDate, EndDate);
                var operationId = Guid.NewGuid();
                await _warrantyService.UpdateCoverageAsync(
                    selected.Id,
                    StartDate,
                    EndDate,
                    Status,
                    selected.RowVersion,
                    _actorId,
                    operationId);
                MessageBox.Show("Cập nhật thông tin bảo hành thành công!", "Thông báo");
                IsDetailPanelOpen = false;
                await LoadData();
            }
            catch (DatabaseWriteConflictException)
            {
                MessageBox.Show("Thông tin bảo hành đã được thay đổi ở máy khác. Dữ liệu mới nhất sẽ được tải lại.", "Dữ liệu đã thay đổi");
                IsDetailPanelOpen = false;
                await LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
            }
        }

        [RelayCommand]
        private async Task DeleteCoverageAsync(WarrantyCoverage coverage)
        {
            if (coverage == null) return;
            if (MessageBox.Show("Xóa thông tin bảo hành này?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                var operationId = Guid.NewGuid();
                await _warrantyService.DeleteCoverageAsync(
                    coverage.Id,
                    coverage.RowVersion,
                    _actorId,
                    operationId);
                IsDetailPanelOpen = false;
                await LoadData();
            }
            catch (DatabaseWriteConflictException)
            {
                MessageBox.Show("Thông tin bảo hành đã được thay đổi ở máy khác. Dữ liệu mới nhất sẽ được tải lại.", "Dữ liệu đã thay đổi");
                IsDetailPanelOpen = false;
                await LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
            }
        }

        [RelayCommand]
        private Task DeleteSelectedCoverageAsync() =>
            SelectedCoverage == null
                ? Task.CompletedTask
                : DeleteCoverageAsync(SelectedCoverage);

        public void RefreshData() => _ = LoadData();

        partial void OnSelectedCoverageChanged(WarrantyCoverage? value)
        {
            if (value != null)
            {
                SerialNumber = value.ProductSerial?.SerialNumber ?? string.Empty;
                StartDate = value.WarrantyStartDate;
                EndDate = value.WarrantyEndDate;
                Status = value.CoverageStatus;
            }
        }
    }
}
