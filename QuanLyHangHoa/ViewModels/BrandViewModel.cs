using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using System.Text.Json;
using ClosedXML.Excel;

namespace QuanLyHangHoa.ViewModels
{
    public partial class BrandViewModel : ObservableObject
    {
        private readonly AppDbContext _db;
        private readonly AppUser _currentUser;
        private readonly AuthorizationService _authService = new();

        [ObservableProperty] private bool _canManage;
        [ObservableProperty] private ObservableCollection<Brand> _brands = new();
        [ObservableProperty] private Brand? _selectedBrand;
        
        // Search Filters
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private string _searchOrigin = string.Empty;
        [ObservableProperty] private string? _searchStatus = "Tất cả";
        public ObservableCollection<string> StatusOptions { get; } = ["Tất cả", "Hoạt động", "Ngưng"];

        // Edit Properties
        [ObservableProperty] private bool _isEditing;
        [ObservableProperty] private string _editBrandCode = string.Empty;
        [ObservableProperty] private string _editDisplayName = string.Empty;
        [ObservableProperty] private string _editOriginCountry = string.Empty;
        [ObservableProperty] private bool _editIsActive = true;

        public BrandViewModel(AppDbContext db, AppUser currentUser)
        {
            _db = db;
            _currentUser = currentUser;
            CanManage = _authService.CanPerform(_currentUser, PermissionAction.ManageMasterData);
            LoadBrands();
        }

        [RelayCommand]
        public void LoadBrands()
        {
            var query = _db.Brands.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchCode))
                query = query.Where(b => b.BrandCode.Contains(SearchCode));

            if (!string.IsNullOrWhiteSpace(SearchName))
                query = query.Where(b => b.DisplayName.Contains(SearchName));

            if (!string.IsNullOrWhiteSpace(SearchOrigin))
                query = query.Where(b => b.OriginCountry != null && b.OriginCountry.Contains(SearchOrigin));

            if (SearchStatus == "Hoạt động")
                query = query.Where(b => b.IsActive);
            else if (SearchStatus == "Ngưng")
                query = query.Where(b => !b.IsActive);

            var list = query.OrderBy(b => b.BrandCode).ToList();
            Brands = new ObservableCollection<Brand>(list);
        }

        partial void OnSearchCodeChanged(string value) => LoadBrands();
        partial void OnSearchNameChanged(string value) => LoadBrands();
        partial void OnSearchOriginChanged(string value) => LoadBrands();
        partial void OnSearchStatusChanged(string? value) => LoadBrands();

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
                    if (_db.Brands.Any(b => b.BrandCode == EditBrandCode))
                    {
                        MessageBox.Show($"Mã thương hiệu '{EditBrandCode}' đã tồn tại.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var newBrand = new Brand
                    {
                        BrandCode = EditBrandCode,
                        DisplayName = EditDisplayName,
                        OriginCountry = EditOriginCountry,
                        IsActive = EditIsActive
                    };

                    _db.Brands.Add(newBrand);
                    _db.SaveChanges();
                    LogAction("CREATE", newBrand.Id, null, Serialize(newBrand));
                }
                else // Update
                {
                    var beforeJson = Serialize(SelectedBrand);
                    SelectedBrand.BrandCode = EditBrandCode;
                    SelectedBrand.DisplayName = EditDisplayName;
                    SelectedBrand.OriginCountry = EditOriginCountry;
                    SelectedBrand.IsActive = EditIsActive;

                    _db.SaveChanges();
                    LogAction("UPDATE", SelectedBrand.Id, beforeJson, Serialize(SelectedBrand));
                }

                IsEditing = false;
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
            var result = MessageBox.Show($"Bạn có chắc chắn muốn xoá thương hiệu '{brand.DisplayName}'?", "Xác nhận xoá", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try 
                {
                    // Check for dependencies
                    bool isUsed = _db.Products.Any(p => p.BrandId == brand.Id);

                    if (isUsed)
                    {
                        MessageBox.Show("Không thể xoá thương hiệu này vì đang có sản phẩm thuộc thương hiệu này.", 
                            "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var beforeJson = Serialize(brand);
                    int entityId = brand.Id;

                    _db.Brands.Remove(brand);
                    _db.SaveChanges();

                    LogAction("DELETE", entityId, beforeJson, null);
                    LoadBrands();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xoá thương hiệu: {ex.Message}", 
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            worksheet.Cell(i + 2, 4).Value = Brands[i].IsActive ? "Hoạt động" : "Ngưng";
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

        private string Serialize(Brand brand)
        {
            return JsonSerializer.Serialize(new
            {
                brand.Id,
                brand.BrandCode,
                brand.DisplayName,
                brand.OriginCountry,
                brand.IsActive
            });
        }

        private void LogAction(string action, int entityId, string? before = null, string? after = null)
        {
            try
            {
                var log = new AuditLog
                {
                    EntityName = "Brand",
                    EntityId = entityId,
                    ActionCode = action,
                    BeforeJson = before,
                    AfterJson = after,
                    PerformedBy = _currentUser.Id,
                    PerformedAt = DateTime.Now
                };
                _db.AuditLogs.Add(log);
                _db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write audit log: {ex.Message}");
            }
        }
    }
}
