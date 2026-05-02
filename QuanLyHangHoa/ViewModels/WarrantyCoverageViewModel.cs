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
        private readonly AppDbContext _dbContext;

        [ObservableProperty] private ObservableCollection<WarrantyCoverage> _coverages = new();
        [ObservableProperty] private WarrantyCoverage? _selectedCoverage;
        [ObservableProperty] private string _searchText = string.Empty;

        // Form properties for New/Edit
        [ObservableProperty] private string _serialNumber = string.Empty;
        [ObservableProperty] private DateTime _startDate = DateTime.Now;
        [ObservableProperty] private DateTime _endDate = DateTime.Now.AddYears(1);
        [ObservableProperty] private string _status = "Active";

        public WarrantyCoverageViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            LoadData();
        }

        [RelayCommand]
        public void LoadData()
        {
            var query = _dbContext.WarrantyCoverages
                .Include(c => c.ProductSerial!)
                .ThenInclude(p => p.Product!)
                .Include(c => c.Customer!)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(c => 
                    (c.ProductSerial != null && c.ProductSerial.SerialNumber.Contains(SearchText)) ||
                    (c.Customer != null && c.Customer.DisplayName.Contains(SearchText)) ||
                    (c.ProductSerial != null && c.ProductSerial.Product != null && c.ProductSerial.Product.DisplayName.Contains(SearchText))
                );
            }

            Coverages = new ObservableCollection<WarrantyCoverage>(query.ToList());
        }

        [RelayCommand]
        private void SaveCoverage()
        {
            if (SelectedCoverage == null)
            {
                // Create new logic could go here if needed, 
                // but usually coverage is created automatically during sales.
                // For now, let's just support editing existing coverage.
                return;
            }

            try
            {
                _dbContext.WarrantyCoverages.Update(SelectedCoverage);
                _dbContext.SaveChanges();
                MessageBox.Show("Cập nhật thông tin bảo hành thành công!", "Thông báo");
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
            }
        }

        [RelayCommand]
        private void DeleteCoverage()
        {
            if (SelectedCoverage == null) return;
            if (MessageBox.Show("Xóa thông tin bảo hành này?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    _dbContext.WarrantyCoverages.Remove(SelectedCoverage);
                    _dbContext.SaveChanges();
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi");
                }
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
