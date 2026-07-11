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

namespace QuanLyHangHoa.ViewModels
{
    public partial class WarrantyCoverageViewModel : ObservableObject
    {
        private readonly Func<AppDbContext> _contextFactory;
        private CancellationTokenSource? _filterDebounceCts;
        private CancellationTokenSource? _loadCts;

        [ObservableProperty] private ObservableCollection<WarrantyCoverage> _coverages = new();
        [ObservableProperty] private WarrantyCoverage? _selectedCoverage;
        [ObservableProperty] private string _searchSerial = string.Empty;
        [ObservableProperty] private string _searchCustomer = string.Empty;
        [ObservableProperty] private string _searchProduct = string.Empty;
        [ObservableProperty] private string _selectedStatusFilter = "Tất cả";

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

        public WarrantyCoverageViewModel(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _ = LoadData();
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
        public async Task LoadData()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            var cancellationToken = _loadCts.Token;

            try
            {
                using var db = _contextFactory();

                var stats = await db.WarrantyCoverages
                    .AsNoTracking()
                    .GroupBy(_ => 1)
                    .Select(group => new
                    {
                        Total = group.Count(),
                        Active = group.Count(coverage => coverage.CoverageStatus == "Active"),
                        Expired = group.Count(coverage => coverage.CoverageStatus == "Expired"),
                        Voided = group.Count(coverage => coverage.CoverageStatus == "Voided")
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                var query = db.WarrantyCoverages
                    .AsNoTracking()
                    .Include(coverage => coverage.ProductSerial!)
                    .ThenInclude(serial => serial.Product!)
                    .Include(coverage => coverage.Customer!)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(SearchSerial))
                {
                    var term = SearchSerial.Trim();
                    query = query.Where(coverage => coverage.ProductSerial != null
                        && coverage.ProductSerial.SerialNumber.Contains(term));
                }

                if (!string.IsNullOrWhiteSpace(SearchCustomer))
                {
                    var term = SearchCustomer.Trim();
                    query = query.Where(coverage => coverage.Customer != null
                        && coverage.Customer.DisplayName.Contains(term));
                }

                if (!string.IsNullOrWhiteSpace(SearchProduct))
                {
                    var term = SearchProduct.Trim();
                    query = query.Where(coverage => coverage.ProductSerial != null
                        && coverage.ProductSerial.Product != null
                        && coverage.ProductSerial.Product.DisplayName.Contains(term));
                }

                if (!string.IsNullOrWhiteSpace(SelectedStatusFilter) && SelectedStatusFilter != "Tất cả")
                {
                    query = query.Where(coverage => coverage.CoverageStatus == SelectedStatusFilter);
                }

                var coverages = await query
                    .OrderBy(coverage => coverage.ProductSerial.SerialNumber)
                    .ToListAsync(cancellationToken);

                TotalCount = stats?.Total ?? 0;
                ActiveCount = stats?.Active ?? 0;
                ExpiredCount = stats?.Expired ?? 0;
                VoidedCount = stats?.Voided ?? 0;
                Coverages = new ObservableCollection<WarrantyCoverage>(coverages);
            }
            catch (OperationCanceledException)
            {
            }
        }

        [RelayCommand]
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
        private void SaveCoverage()
        {
            if (SelectedCoverage == null) return;

            try
            {
                using var db = _contextFactory();
                // Cập nhật giá trị thay đổi từ form vào SelectedCoverage
                SelectedCoverage.WarrantyStartDate = StartDate;
                SelectedCoverage.WarrantyEndDate = EndDate;
                SelectedCoverage.CoverageStatus = Status;

                db.WarrantyCoverages.Update(SelectedCoverage);
                db.SaveChanges();
                MessageBox.Show("Cập nhật thông tin bảo hành thành công!", "Thông báo");
                IsDetailPanelOpen = false;
                _ = LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
            }
        }

        [RelayCommand]
        private void DeleteCoverage(WarrantyCoverage coverage)
        {
            if (coverage == null) return;
            if (MessageBox.Show("Xóa thông tin bảo hành này?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using var db = _contextFactory();
                    db.WarrantyCoverages.Remove(coverage);
                    db.SaveChanges();
                    IsDetailPanelOpen = false;
                    _ = LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi");
                }
            }
        }

        // Hỗ trợ xóa SelectedCoverage khi đang ở trong Drawer
        [RelayCommand]
        private void DeleteSelectedCoverage()
        {
            if (SelectedCoverage != null)
            {
                DeleteCoverage(SelectedCoverage);
            }
        }

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
