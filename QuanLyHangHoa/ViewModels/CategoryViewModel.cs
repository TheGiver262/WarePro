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
        private readonly Func<AppDbContext> _contextFactory;
        private readonly CategoryService _service;
        private readonly AppUser _currentUser;


        [ObservableProperty] private bool _canManage;
        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private Category? _selectedCategory;

        // Search Filters
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private string? _searchStatus = "Tất cả";
        public ObservableCollection<string> StatusOptions { get; } = ["Tất cả", "Ho\u00E1t \u0111\u1ED9ng", "D\u1EEBng"];

        public CategoryViewModel(Func<AppDbContext> contextFactory, AppUser currentUser)
        {
            _contextFactory = contextFactory;
            _service = new CategoryService(_contextFactory);
            _currentUser = currentUser;
            CanManage = AuthorizationService.CanPerform(_currentUser, PermissionAction.ManageMasterData);
            LoadCategories();
        }

        [RelayCommand]
        public void LoadCategories()
        {
            using var db = _contextFactory();
            var query = db.Categories.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchCode))
                query = query.Where(c => c.CategoryCode.Contains(SearchCode));

            if (!string.IsNullOrWhiteSpace(SearchName))
                query = query.Where(c => c.DisplayName.Contains(SearchName));

            if (SearchStatus == "Ho\u00E1t \u0111\u1ED9ng")
                query = query.Where(c => c.IsActive);
            else if (SearchStatus == "D\u1EEBng")
                query = query.Where(c => !c.IsActive);

            var list = query.OrderBy(c => c.CategoryCode).ToList();
            Categories = new ObservableCollection<Category>(list);
        }

        [RelayCommand]
        private void Refresh()
        {
            SearchCode = string.Empty;
            SearchName = string.Empty;
            SearchStatus = "Tất cả";
            LoadCategories();
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
                var newCat = new Category();
                vm.ApplyTo(newCat);
                _service.Add(newCat, _currentUser.Id);

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
                _service.Update(category, beforeJson, _currentUser.Id);

                LoadCategories();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void DeleteCategory(Category category)
        {
            using var db = _contextFactory();
            // 1. Kiểm tra phát sinh dữ liệu
            bool isUsed = db.Products.Any(p => p.CategoryId == category.Id);

            if (isUsed)
            {
                MessageBox.Show($"Không thể xoá danh mục '{category.DisplayName}' vì đang có sản phẩm thuộc danh mục này.\n\nVui lòng chuyển trạng thái danh mục sang 'Dừng' nếu không còn sử dụng.", 
                    "Không thể xoá", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            // 2. Xác nhận xoá (nếu không có ràng buộc)
            var result = MessageBox.Show($"Danh mục '{category.DisplayName}' chưa có dữ liệu liên quan. Bạn có chắc chắn muốn xoá vĩnh viễn danh mục này?", 
                "Xác nhận xoá", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try 
                {
                    _service.Delete(category.Id, _currentUser.Id);
                    LoadCategories();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xoá danh mục: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            worksheet.Cell(i + 2, 3).Value = Categories[i].IsActive ? "Ho\u00E1t \u0111\u1ED9ng" : "D\u1EEBng";
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

        private string Serialize(Category c)
        {
            return System.Text.Json.JsonSerializer.Serialize(new { c.Id, c.CategoryCode, c.DisplayName, c.IsActive });
        }
    }
}
