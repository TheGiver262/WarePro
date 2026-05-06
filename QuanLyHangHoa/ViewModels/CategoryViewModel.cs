using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Views;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using ClosedXML.Excel;
using System.Text.Json;
using QuanLyHangHoa.Services;
using System.Text.Json.Serialization;

namespace QuanLyHangHoa.ViewModels
{
    public partial class CategoryViewModel : ObservableObject
    {
        private readonly AppDbContext _db;
        private readonly AppUser _currentUser;
        private readonly AuthorizationService _authService = new();

        [ObservableProperty] private bool _canManage;
        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private Category? _selectedCategory;

        // Search Filters
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private string? _searchStatus = "Tất cả";
        public ObservableCollection<string> StatusOptions { get; } = ["Tất cả", "Hoạt động", "Ngưng"];

        public CategoryViewModel(AppDbContext db, AppUser currentUser)
        {
            _db = db;
            _currentUser = currentUser;
            CanManage = _authService.CanPerform(_currentUser, PermissionAction.ManageMasterData);
            LoadCategories();
        }

        [RelayCommand]
        public void LoadCategories()
        {
            var query = _db.Categories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchCode))
                query = query.Where(c => c.CategoryCode.Contains(SearchCode));

            if (!string.IsNullOrWhiteSpace(SearchName))
                query = query.Where(c => c.DisplayName.Contains(SearchName));

            if (SearchStatus == "Hoạt động")
                query = query.Where(c => c.IsActive);
            else if (SearchStatus == "Ngưng")
                query = query.Where(c => !c.IsActive);

            var list = query.OrderBy(c => c.CategoryCode).ToList();
            Categories = new ObservableCollection<Category>(list);
        }

        partial void OnSearchCodeChanged(string value) => LoadCategories();
        partial void OnSearchNameChanged(string value) => LoadCategories();
        partial void OnSearchStatusChanged(string? value) => LoadCategories();

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void OpenAddCategoryDialog()
        {
            var vm = new CategoryEditViewModel();
            var window = new CategoryEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                if (_db.Categories.Any(c => c.CategoryCode == vm.CategoryCode))
                {
                    MessageBox.Show($"Mã danh mục '{vm.CategoryCode}' đã tồn tại.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newCat = new Category();
                vm.ApplyTo(newCat);
                _db.Categories.Add(newCat);
                _db.SaveChanges();
                
                // Audit Log
                LogAction("CREATE", newCat.Id, null, Serialize(newCat));

                LoadCategories();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void EditCategory(Category category)
        {
            var beforeJson = Serialize(category);
            var vm = new CategoryEditViewModel(category);
            var window = new CategoryEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                vm.ApplyTo(category);
                _db.SaveChanges();

                // Audit Log
                LogAction("UPDATE", category.Id, beforeJson, Serialize(category));

                LoadCategories();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void DeleteCategory(Category category)
        {
            var result = MessageBox.Show($"Bạn có chắc chắn muốn xoá danh mục '{category.DisplayName}'?", "Xác nhận xoá", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try 
                {
                    // Check for dependencies
                    bool isUsed = _db.Products.Any(p => p.CategoryId == category.Id);

                    if (isUsed)
                    {
                        MessageBox.Show("Không thể xoá danh mục này vì đang có sản phẩm thuộc danh mục này.", 
                            "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var beforeJson = Serialize(category);
                    int entityId = category.Id;

                    _db.Categories.Remove(category);
                    _db.SaveChanges();

                    // Audit Log
                    LogAction("DELETE", entityId, beforeJson, null);

                    LoadCategories();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xoá danh mục: {ex.Message}", 
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
                    FileName = $"DanhSachDanhMuc_{DateTime.Now:yyyyMMdd_HHmm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Categories");

                        // Headers
                        worksheet.Cell(1, 1).Value = "Mã Danh Mục";
                        worksheet.Cell(1, 2).Value = "Tên Danh Mục";
                        worksheet.Cell(1, 3).Value = "Trạng Thái";

                        var headerRange = worksheet.Range(1, 1, 1, 3);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                        // Data
                        for (int i = 0; i < Categories.Count; i++)
                        {
                            worksheet.Cell(i + 2, 1).Value = Categories[i].CategoryCode;
                            worksheet.Cell(i + 2, 2).Value = Categories[i].DisplayName;
                            worksheet.Cell(i + 2, 3).Value = Categories[i].IsActive ? "Hoạt động" : "Ngưng";
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
        private string Serialize(Category category)
        {
            return JsonSerializer.Serialize(new
            {
                category.Id,
                category.CategoryCode,
                category.DisplayName,
                category.IsActive
            });
        }

        private void LogAction(string action, int entityId, string? before = null, string? after = null)
        {
            try
            {
                var log = new AuditLog
                {
                    EntityName = "Category",
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
                // We don't want to crash the app if logging fails, but maybe log it to console/debug
                System.Diagnostics.Debug.WriteLine($"Failed to write audit log: {ex.Message}");
            }
        }
    }
}
