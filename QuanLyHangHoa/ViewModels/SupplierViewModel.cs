using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Views;
using QuanLyHangHoa.Data;
using System;
using System.Windows;
using ClosedXML.Excel;
using System.Text.Json;
using QuanLyHangHoa.Services;
using Microsoft.EntityFrameworkCore;

namespace QuanLyHangHoa.ViewModels
{
    public partial class SupplierViewModel : ObservableObject
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly SupplierService _service;
        private readonly AppUser _currentUser;


        [ObservableProperty] private bool _canManage;
        [ObservableProperty] private ObservableCollection<Supplier> _suppliers = new();
        [ObservableProperty] private Supplier? _selectedSupplier;

        // Footer counts
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _activeCount;
        [ObservableProperty] private int _inactiveCount;

        // Search Filters
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private string _searchEmail = string.Empty;
        [ObservableProperty] private string _searchPhone = string.Empty;
        [ObservableProperty] private string? _searchStatus = "Tất cả";
        public ObservableCollection<string> StatusOptions { get; } = ["Tất cả", "Hoạt động", "Dừng"];

        public SupplierViewModel(Func<AppDbContext> contextFactory, AppUser currentUser)
        {
            _contextFactory = contextFactory;
            _service = new SupplierService(_contextFactory);
            _currentUser = currentUser;
            CanManage = AuthorizationService.CanPerform(_currentUser, PermissionAction.ManageMasterData);
            LoadData();
        }

        private List<Supplier> _allSuppliers = new();

        [RelayCommand]
        public void LoadData()
        {
            using var db = _contextFactory();
            _allSuppliers = db.Suppliers.AsNoTracking().ToList();

            // Calculate counts in memory (instant)
            TotalCount = _allSuppliers.Count;
            ActiveCount = _allSuppliers.Count(s => s.IsActive);
            InactiveCount = _allSuppliers.Count(s => !s.IsActive);

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var query = _allSuppliers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchCode))
                query = query.Where(s => s.SupplierCode.Contains(SearchCode, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchName))
                query = query.Where(s => s.DisplayName.Contains(SearchName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchEmail))
                query = query.Where(s => s.Email != null && s.Email.Contains(SearchEmail, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchPhone))
                query = query.Where(s => s.Phone != null && s.Phone.Contains(SearchPhone, StringComparison.OrdinalIgnoreCase));

            if (SearchStatus == "Hoạt động")
                query = query.Where(s => s.IsActive);
            else if (SearchStatus == "Dừng")
                query = query.Where(s => !s.IsActive);

            var list = query.OrderBy(s => s.SupplierCode).ToList();
            Suppliers = new ObservableCollection<Supplier>(list);
        }

        [RelayCommand]
        private void Refresh()
        {
            SearchCode = string.Empty;
            SearchName = string.Empty;
            SearchEmail = string.Empty;
            SearchPhone = string.Empty;
            SearchStatus = "Tất cả";
            LoadData();
        }

        partial void OnSearchCodeChanged(string value) => ApplyFilters();
        partial void OnSearchNameChanged(string value) => ApplyFilters();
        partial void OnSearchEmailChanged(string value) => ApplyFilters();
        partial void OnSearchPhoneChanged(string value) => ApplyFilters();
        partial void OnSearchStatusChanged(string? value) => ApplyFilters();

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void OpenAddSupplierDialog()
        {
            var vm = new SupplierEditViewModel();
            var window = new SupplierEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                var newSup = new Supplier();
                vm.ApplyTo(newSup);
                _service.Add(newSup, _currentUser.Id);
                LoadData();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void EditSupplier(Supplier supplier)
        {
            var beforeJson = Serialize(supplier);
            var vm = new SupplierEditViewModel(supplier);
            var window = new SupplierEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                vm.ApplyTo(supplier);
                _service.Update(supplier, beforeJson, _currentUser.Id);
                LoadData();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void DeleteSupplier(Supplier supplier)
        {
            using var db = _contextFactory();
            // 1. Kiểm tra phát sinh dữ liệu
            bool isUsed = db.PurchaseInvoices.Any(pi => pi.SupplierId == supplier.Id) ||
                         db.StockIns.Any(si => si.SupplierId == supplier.Id);

            if (isUsed)
            {
                MessageBox.Show($"Không thể xoá nhà cung cấp '{supplier.DisplayName}' vì đang có dữ liệu liên quan (Hóa đơn mua hoặc Phiếu nhập kho).\n\nVui lòng chuyển trạng thái nhà cung cấp sang 'Dừng' nếu không còn sử dụng.", 
                    "Không thể xoá", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            // 2. Xác nhận xoá (nếu không có ràng buộc)
            var result = MessageBox.Show($"Nhà cung cấp '{supplier.DisplayName}' chưa có dữ liệu liên quan. Bạn có chắc chắn muốn xoá vĩnh viễn nhà cung cấp này?", 
                "Xác nhận xoá", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try 
                {
                    _service.Delete(supplier.Id, _currentUser.Id);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xoá nhà cung cấp: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    FileName = $"DanhSachNhaCungCap_{DateTime.Now:yyyyMMdd_HHmm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Suppliers");
                        worksheet.Cell(1, 1).Value = "Mã Nhà Cung Cấp";
                        worksheet.Cell(1, 2).Value = "Tên Nhà Cung Cấp";
                        worksheet.Cell(1, 3).Value = "Số Điện Thoại";
                        worksheet.Cell(1, 4).Value = "Email";
                        worksheet.Cell(1, 5).Value = "Địa Chỉ";
                        worksheet.Cell(1, 6).Value = "Trạng Thái";

                        var headerRange = worksheet.Range(1, 1, 1, 6);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                        for (int i = 0; i < Suppliers.Count; i++)
                        {
                            worksheet.Cell(i + 2, 1).Value = Suppliers[i].SupplierCode;
                            worksheet.Cell(i + 2, 2).Value = Suppliers[i].DisplayName;
                            worksheet.Cell(i + 2, 3).Value = Suppliers[i].Phone;
                            worksheet.Cell(i + 2, 4).Value = Suppliers[i].Email;
                            worksheet.Cell(i + 2, 5).Value = Suppliers[i].Address;
                            worksheet.Cell(i + 2, 6).Value = Suppliers[i].IsActive ? "Hoạt động" : "Dừng";
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


        private string Serialize(Supplier s)
        {
            return System.Text.Json.JsonSerializer.Serialize(new { s.Id, s.SupplierCode, s.DisplayName, s.Phone, s.Email, s.Address, s.IsActive });
        }
    }
}
