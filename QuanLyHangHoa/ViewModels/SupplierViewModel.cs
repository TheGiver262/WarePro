using System.Collections.ObjectModel;
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

namespace QuanLyHangHoa.ViewModels
{
    public partial class SupplierViewModel : ObservableObject
    {
        private readonly AppDbContext _db;
        private readonly AppUser _currentUser;
        private readonly AuthorizationService _authService = new();

        [ObservableProperty] private bool _canManage;
        [ObservableProperty] private ObservableCollection<Supplier> _suppliers = new();
        [ObservableProperty] private Supplier? _selectedSupplier;

        // Search Filters
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private string _searchEmail = string.Empty;
        [ObservableProperty] private string _searchPhone = string.Empty;
        [ObservableProperty] private string? _searchStatus = "Tất cả";
        public ObservableCollection<string> StatusOptions { get; } = ["Tất cả", "Hoạt động", "Ngưng"];

        public SupplierViewModel(AppDbContext db, AppUser currentUser)
        {
            _db = db;
            _currentUser = currentUser;
            CanManage = _authService.CanPerform(_currentUser, PermissionAction.ManageMasterData);
            LoadData();
        }

        [RelayCommand]
        public void LoadData()
        {
            var query = _db.Suppliers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchCode))
                query = query.Where(s => s.SupplierCode.Contains(SearchCode));

            if (!string.IsNullOrWhiteSpace(SearchName))
                query = query.Where(s => s.DisplayName.Contains(SearchName));

            if (!string.IsNullOrWhiteSpace(SearchEmail))
                query = query.Where(s => s.Email != null && s.Email.Contains(SearchEmail));

            if (!string.IsNullOrWhiteSpace(SearchPhone))
                query = query.Where(s => s.Phone != null && s.Phone.Contains(SearchPhone));

            if (SearchStatus == "Hoạt động")
                query = query.Where(s => s.IsActive);
            else if (SearchStatus == "Ngưng")
                query = query.Where(s => !s.IsActive);

            var list = query.OrderBy(s => s.SupplierCode).ToList();
            Suppliers = new ObservableCollection<Supplier>(list);
        }

        partial void OnSearchCodeChanged(string value) => LoadData();
        partial void OnSearchNameChanged(string value) => LoadData();
        partial void OnSearchEmailChanged(string value) => LoadData();
        partial void OnSearchPhoneChanged(string value) => LoadData();
        partial void OnSearchStatusChanged(string? value) => LoadData();

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void OpenAddSupplierDialog()
        {
            var vm = new SupplierEditViewModel();
            var window = new SupplierEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                if (_db.Suppliers.Any(s => s.SupplierCode == vm.SupplierCode))
                {
                    MessageBox.Show($"Mã nhà cung cấp '{vm.SupplierCode}' đã tồn tại.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newSup = new Supplier();
                vm.ApplyTo(newSup);
                _db.Suppliers.Add(newSup);
                _db.SaveChanges();

                LogAction("CREATE", newSup.Id, null, Serialize(newSup));
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
                _db.SaveChanges();

                LogAction("UPDATE", supplier.Id, beforeJson, Serialize(supplier));
                LoadData();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void DeleteSupplier(Supplier supplier)
        {
            var result = MessageBox.Show($"Bạn có chắc chắn muốn xoá nhà cung cấp '{supplier.DisplayName}'?", "Xác nhận xoá", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try 
                {
                    // Check for dependencies
                    bool isUsed = _db.PurchaseInvoices.Any(pi => pi.SupplierId == supplier.Id) ||
                                 _db.StockIns.Any(si => si.SupplierId == supplier.Id);

                    if (isUsed)
                    {
                        MessageBox.Show("Không thể xoá nhà cung cấp này vì đang có dữ liệu liên quan (Hóa đơn mua hoặc Phiếu nhập kho).", 
                            "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var beforeJson = Serialize(supplier);
                    int entityId = supplier.Id;

                    _db.Suppliers.Remove(supplier);
                    _db.SaveChanges();

                    LogAction("DELETE", entityId, beforeJson, null);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xoá nhà cung cấp: {ex.Message}", 
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
                            worksheet.Cell(i + 2, 6).Value = Suppliers[i].IsActive ? "Hoạt động" : "Ngưng";
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
            return JsonSerializer.Serialize(new { s.Id, s.SupplierCode, s.DisplayName, s.Phone, s.Email, s.Address, s.IsActive });
        }

        private void LogAction(string action, int entityId, string? before = null, string? after = null)
        {
            try
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    EntityName = "Supplier",
                    EntityId = entityId,
                    ActionCode = action,
                    BeforeJson = before,
                    AfterJson = after,
                    PerformedBy = _currentUser.Id,
                    PerformedAt = DateTime.Now
                });
                _db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write audit log: {ex.Message}");
            }
        }
    }
}
