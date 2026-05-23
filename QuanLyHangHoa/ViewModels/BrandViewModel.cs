using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using System.Text.Json;
using QuanLyHangHoa.Services;
using ClosedXML.Excel;

namespace QuanLyHangHoa.ViewModels
{
    public partial class BrandViewModel : ObservableObject
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly BrandService _service;
        private readonly AppUser _currentUser;
        private List<Brand> _allBrands = new();


        [ObservableProperty] private bool _canManage;
        [ObservableProperty] private ObservableCollection<Brand> _brands = new();
        [ObservableProperty] private Brand? _selectedBrand;

        // Footer counts
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _activeCount;
        [ObservableProperty] private int _inactiveCount;
        
        // Search Filters
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private string? _searchOrigin = "Tất cả";
        [ObservableProperty] private string? _searchStatus = "Tất cả";
        public ObservableCollection<string> StatusOptions { get; } = ["Tất cả", "Hoạt động", "Dừng"];
        public ObservableCollection<string> OriginOptions { get; } = new();

        // Edit Properties
        [ObservableProperty] private bool _isEditing;
        [ObservableProperty] private string _editBrandCode = string.Empty;
        [ObservableProperty] private string _editDisplayName = string.Empty;
        [ObservableProperty] private string _editOriginCountry = string.Empty;
        [ObservableProperty] private bool _editIsActive = true;

        public BrandViewModel(Func<AppDbContext> contextFactory, AppUser currentUser)
        {
            _contextFactory = contextFactory;
            _service = new BrandService(_contextFactory);
            _currentUser = currentUser;
            CanManage = AuthorizationService.CanPerform(_currentUser, PermissionAction.ManageMasterData);
            LoadOrigins();
            LoadBrands();
        }

        [RelayCommand]
        public void LoadBrands()
        {
            using var db = _contextFactory();
            _allBrands = db.Brands.AsNoTracking().ToList();

            // Calculate counts in memory (instant)
            TotalCount = _allBrands.Count;
            ActiveCount = _allBrands.Count(b => b.IsActive);
            InactiveCount = _allBrands.Count(b => !b.IsActive);

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var query = _allBrands.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchCode))
                query = query.Where(b => b.BrandCode.Contains(SearchCode, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchName))
                query = query.Where(b => b.DisplayName.Contains(SearchName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchOrigin) && SearchOrigin != "Tất cả")
                query = query.Where(b => b.OriginCountry == SearchOrigin);

            if (SearchStatus == "Hoạt động")
                query = query.Where(b => b.IsActive);
            else if (SearchStatus == "Dừng")
                query = query.Where(b => !b.IsActive);

            var list = query.OrderBy(b => b.BrandCode).ToList();
            Brands = new ObservableCollection<Brand>(list);
        }

        private void LoadOrigins()
        {
            using var db = _contextFactory();
            var origins = db.Brands
                .AsNoTracking()
                .Select(b => b.OriginCountry)
                .Where(o => !string.IsNullOrEmpty(o))
                .Distinct()
                .OrderBy(o => o)
                .ToList();

            OriginOptions.Clear();
            OriginOptions.Add("Tất cả");
            foreach (var origin in origins)
            {
                OriginOptions.Add(origin!);
            }
        }

        [RelayCommand]
        private void Refresh()
        {
            SearchCode = string.Empty;
            SearchName = string.Empty;
            SearchOrigin = "Tất cả";
            SearchStatus = "Tất cả";
            LoadBrands();
        }

        partial void OnSearchCodeChanged(string value) => ApplyFilters();
        partial void OnSearchNameChanged(string value) => ApplyFilters();
        partial void OnSearchOriginChanged(string? value) => ApplyFilters();
        partial void OnSearchStatusChanged(string? value) => ApplyFilters();

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void AddNew()
        {
            SelectedBrand = null;
            EditBrandCode = string.Empty;
            EditDisplayName = string.Empty;
            EditOriginCountry = string.Empty;
            EditIsActive = true;
            IsEditing = true;
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void EditBrand(Brand brand)
        {
            SelectedBrand = brand;
            EditBrandCode = brand.BrandCode;
            EditDisplayName = brand.DisplayName;
            EditOriginCountry = brand.OriginCountry ?? string.Empty;
            EditIsActive = brand.IsActive;
            IsEditing = true;
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(EditBrandCode) || string.IsNullOrWhiteSpace(EditDisplayName))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ Mã và Tên thương hiệu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (SelectedBrand == null) // New
                {
                    var newBrand = new Brand
                    {
                        BrandCode = EditBrandCode,
                        DisplayName = EditDisplayName,
                        OriginCountry = EditOriginCountry,
                        IsActive = EditIsActive
                    };

                    _service.Add(newBrand, _currentUser.Id);
                }
                else // Update
                {
                    var beforeJson = Serialize(SelectedBrand);
                    SelectedBrand.BrandCode = EditBrandCode;
                    SelectedBrand.DisplayName = EditDisplayName;
                    SelectedBrand.OriginCountry = EditOriginCountry;
                    SelectedBrand.IsActive = EditIsActive;

                    _service.Update(SelectedBrand, beforeJson, _currentUser.Id);
                }

                IsEditing = false;
                LoadOrigins();
                LoadBrands();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            IsEditing = false;
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void DeleteBrand(Brand brand)
        {
            using var db = _contextFactory();
            // 1. Kiểm tra phát sinh dữ liệu
            bool isUsed = db.Products.Any(p => p.BrandId == brand.Id);

            if (isUsed)
            {
                MessageBox.Show($"Không thể xoá thương hiệu '{brand.DisplayName}' vì đang có sản phẩm thuộc thương hiệu này.\n\nVui lòng chuyển trạng thái thương hiệu sang 'Dừng' nếu không còn sử dụng.", 
                    "Không thể xoá", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            // 2. Xác nhận xoá (nếu không có ràng buộc)
            var result = MessageBox.Show($"Thương hiệu '{brand.DisplayName}' chưa có dữ liệu liên quan. Bạn có chắc chắn muốn xoá vĩnh viễn thương hiệu này?", 
                "Xác nhận xoá", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try 
                {
                    _service.Delete(brand.Id, _currentUser.Id);
                    LoadOrigins();
                    LoadBrands();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xoá thương hiệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = $"DanhSachThuongHieu_{DateTime.Now:yyyyMMdd_HHmm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Brands");

                        // Headers
                        worksheet.Cell(1, 1).Value = "Mã Thương Hiệu";
                        worksheet.Cell(1, 2).Value = "Tên Thương Hiệu";
                        worksheet.Cell(1, 3).Value = "Xuất Xứ";
                        worksheet.Cell(1, 4).Value = "Trạng Thái";

                        var headerRange = worksheet.Range(1, 1, 1, 4);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                        // Data
                        for (int i = 0; i < Brands.Count; i++)
                        {
                            worksheet.Cell(i + 2, 1).Value = Brands[i].BrandCode;
                            worksheet.Cell(i + 2, 2).Value = Brands[i].DisplayName;
                            worksheet.Cell(i + 2, 3).Value = Brands[i].OriginCountry;
                            worksheet.Cell(i + 2, 4).Value = Brands[i].IsActive ? "Ho\u1EA1t \u0111\u1ED9ng" : "D\u1EEBng";
                        }

                        worksheet.Columns().AdjustToContents();
                        workbook.SaveAs(saveFileDialog.FileName);
                    }
                    MessageBox.Show("Xuất file Excel thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất Excel: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private string Serialize(Brand b)
        {
            return System.Text.Json.JsonSerializer.Serialize(new { b.Id, b.BrandCode, b.DisplayName, b.OriginCountry, b.IsActive });
        }
    }
}
