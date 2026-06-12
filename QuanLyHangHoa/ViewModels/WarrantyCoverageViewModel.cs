using System;
using System.Collections.ObjectModel;
using System.Linq;
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
            LoadData();
        }

        partial void OnSearchSerialChanged(string value) => LoadData();
        partial void OnSearchCustomerChanged(string value) => LoadData();
        partial void OnSearchProductChanged(string value) => LoadData();
        partial void OnSelectedStatusFilterChanged(string value) => LoadData();

        [RelayCommand]
        public void ResetFilters()
        {
            SearchSerial = string.Empty;
            SearchCustomer = string.Empty;
            SearchProduct = string.Empty;
            SelectedStatusFilter = "Tất cả";
            LoadData();
        }

        [RelayCommand]
        public void LoadData()
        {
            using var db = _contextFactory();
            
            // Tính toán stats cho footer từ dữ liệu gốc không lọc
            var baseQuery = db.WarrantyCoverages.AsQueryable();
            var allCoveragesForStats = baseQuery.ToList();

            TotalCount = allCoveragesForStats.Count;
            ActiveCount = allCoveragesForStats.Count(c => c.CoverageStatus == "Active");
            ExpiredCount = allCoveragesForStats.Count(c => c.CoverageStatus == "Expired");
            VoidedCount = allCoveragesForStats.Count(c => c.CoverageStatus == "Voided");

            // Áp dụng bộ lọc
            var query = db.WarrantyCoverages
                .Include(c => c.ProductSerial!)
                .ThenInclude(p => p.Product!)
                .Include(c => c.Customer!)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchSerial))
            {
                var term = SearchSerial.Trim();
                query = query.Where(c => c.ProductSerial != null && c.ProductSerial.SerialNumber.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(SearchCustomer))
            {
                var term = SearchCustomer.Trim();
                query = query.Where(c => c.Customer != null && c.Customer.DisplayName.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(SearchProduct))
            {
                var term = SearchProduct.Trim();
                query = query.Where(c => c.ProductSerial != null && c.ProductSerial.Product != null && c.ProductSerial.Product.DisplayName.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(SelectedStatusFilter) && SelectedStatusFilter != "Tất cả")
            {
                query = query.Where(c => c.CoverageStatus == SelectedStatusFilter);
            }

            Coverages = new ObservableCollection<WarrantyCoverage>(query.OrderBy(c => c.ProductSerial.SerialNumber).ToList());
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
                LoadData();
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
                    LoadData();
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
