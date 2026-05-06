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
    public partial class UnitViewModel : ObservableObject
    {
        private readonly AppDbContext _db;
        private readonly AppUser _currentUser;


        [ObservableProperty] private bool _canManage;
        [ObservableProperty] private ObservableCollection<Unit> _units = new();
        [ObservableProperty] private Unit? _selectedUnit;

        // Search Filters
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private string? _searchStatus = "Tất cả";
        public ObservableCollection<string> StatusOptions { get; } = ["Tất cả", "Hoạt động", "Ngưng"];

        public UnitViewModel(AppDbContext db, AppUser currentUser)
        {
            _db = db;
            _currentUser = currentUser;
            CanManage = AuthorizationService.CanPerform(_currentUser, PermissionAction.ManageMasterData);
            LoadData();
        }

        [RelayCommand]
        public void LoadData()
        {
            var query = _db.Units.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchCode))
                query = query.Where(u => u.UnitCode.Contains(SearchCode));

            if (!string.IsNullOrWhiteSpace(SearchName))
                query = query.Where(u => u.DisplayName.Contains(SearchName));

            if (SearchStatus == "Hoạt động")
                query = query.Where(u => u.IsActive);
            else if (SearchStatus == "Ngưng")
                query = query.Where(u => !u.IsActive);

            var list = query.OrderBy(u => u.UnitCode).ToList();
            Units = new ObservableCollection<Unit>(list);
        }

        partial void OnSearchCodeChanged(string value) => LoadData();
        partial void OnSearchNameChanged(string value) => LoadData();
        partial void OnSearchStatusChanged(string? value) => LoadData();

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void OpenAddUnitDialog()
        {
            var vm = new UnitEditViewModel();
            var window = new UnitEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                if (_db.Units.Any(u => u.UnitCode == vm.UnitCode))
                {
                    MessageBox.Show($"Mã đơn vị '{vm.UnitCode}' đã tồn tại.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newUnit = new Unit();
                vm.ApplyTo(newUnit);
                _db.Units.Add(newUnit);
                _db.SaveChanges();

                LogAction("CREATE", newUnit.Id, null, Serialize(newUnit));
                LoadData();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void EditUnit(Unit unit)
        {
            var beforeJson = Serialize(unit);
            var vm = new UnitEditViewModel(unit);
            var window = new UnitEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                vm.ApplyTo(unit);
                _db.SaveChanges();

                LogAction("UPDATE", unit.Id, beforeJson, Serialize(unit));
                LoadData();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void DeleteUnit(Unit unit)
        {
            var result = MessageBox.Show($"Bạn có chắc chắn muốn xoá đơn vị tính '{unit.DisplayName}'?", "Xác nhận xoá", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try 
                {
                    // Check for dependencies before deleting to prevent SQL Exception
                    bool isUsed = _db.Products.Any(p => p.DefaultUnitId == unit.Id) ||
                                 _db.ProductUnits.Any(pu => pu.UnitId == unit.Id) ||
                                 _db.SalesInvoiceLines.Any(sil => sil.UnitId == unit.Id) ||
                                 _db.PurchaseInvoiceLines.Any(pil => pil.UnitId == unit.Id) ||
                                 _db.StockInLines.Any(sil => sil.UnitId == unit.Id) ||
                                 _db.StockOutLines.Any(sol => sol.UnitId == unit.Id);

                    if (isUsed)
                    {
                        MessageBox.Show("Không thể xoá đơn vị tính này vì đang có dữ liệu liên quan (Sản phẩm, Hóa đơn hoặc Phiếu kho).", 
                            "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var beforeJson = Serialize(unit);
                    int entityId = unit.Id;

                    _db.Units.Remove(unit);
                    _db.SaveChanges();

                    // Audit Log - Only log if save was successful
                    LogAction("DELETE", entityId, beforeJson, null);
                    
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xoá đơn vị tính: {ex.Message}", 
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
                    FileName = $"DanhSachDonVi_{DateTime.Now:yyyyMMdd_HHmm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Units");
                        worksheet.Cell(1, 1).Value = "Mã Đơn Vị";
                        worksheet.Cell(1, 2).Value = "Tên Đơn Vị";
                        worksheet.Cell(1, 3).Value = "Trạng Thái";

                        var headerRange = worksheet.Range(1, 1, 1, 3);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                        for (int i = 0; i < Units.Count; i++)
                        {
                            worksheet.Cell(i + 2, 1).Value = Units[i].UnitCode;
                            worksheet.Cell(i + 2, 2).Value = Units[i].DisplayName;
                            worksheet.Cell(i + 2, 3).Value = Units[i].IsActive ? "Hoạt động" : "Ngưng";
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

        private string Serialize(Unit unit)
        {
            return JsonSerializer.Serialize(new { unit.Id, unit.UnitCode, unit.DisplayName, unit.IsActive });
        }

        private void LogAction(string action, int entityId, string? before = null, string? after = null)
        {
            try
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    EntityName = "Unit",
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
