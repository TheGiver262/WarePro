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
using Microsoft.EntityFrameworkCore;

namespace QuanLyHangHoa.ViewModels
{
    public partial class UnitViewModel : ObservableObject
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly UnitService _service;
        private readonly AppUser _currentUser;


        [ObservableProperty] private bool _canManage;
        [ObservableProperty] private ObservableCollection<Unit> _units = new();
        [ObservableProperty] private Unit? _selectedUnit;

        // Search Filters
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private string? _searchStatus = "Tất cả";
        public ObservableCollection<string> StatusOptions { get; } = ["Tất cả", "Hoạt động", "Dừng"];

        public UnitViewModel(Func<AppDbContext> contextFactory, AppUser currentUser)
        {
            _contextFactory = contextFactory;
            _service = new UnitService(_contextFactory);
            _currentUser = currentUser;
            CanManage = AuthorizationService.CanPerform(_currentUser, PermissionAction.ManageMasterData);
            LoadData();
        }

        [RelayCommand]
        public void LoadData()
        {
            using var db = _contextFactory();
            var query = db.Units.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchCode))
                query = query.Where(u => u.UnitCode.Contains(SearchCode));

            if (!string.IsNullOrWhiteSpace(SearchName))
                query = query.Where(u => u.DisplayName.Contains(SearchName));

            if (SearchStatus == "Hoạt động")
                query = query.Where(u => u.IsActive);
            else if (SearchStatus == "Dừng")
                query = query.Where(u => !u.IsActive);

            var list = query.OrderBy(u => u.UnitCode).ToList();
            Units = new ObservableCollection<Unit>(list);
        }

        [RelayCommand]
        private void Refresh()
        {
            SearchCode = string.Empty;
            SearchName = string.Empty;
            SearchStatus = "Tất cả";
            LoadData();
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
                var newUnit = new Unit();
                vm.ApplyTo(newUnit);
                _service.Add(newUnit, _currentUser.Id);
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
                _service.Update(unit, beforeJson, _currentUser.Id);
                LoadData();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void DeleteUnit(Unit unit)
        {
            using var db = _contextFactory();
            // 1. Kiểm tra phát sinh dữ liệu
            bool isUsed = db.Products.Any(p => p.DefaultUnitId == unit.Id) ||
                         db.ProductUnits.Any(pu => pu.UnitId == unit.Id) ||
                         db.SalesInvoiceLines.Any(sil => sil.UnitId == unit.Id) ||
                         db.PurchaseInvoiceLines.Any(pil => pil.UnitId == unit.Id) ||
                         db.StockInLines.Any(sil => sil.UnitId == unit.Id) ||
                         db.StockOutLines.Any(sol => sol.UnitId == unit.Id);

            if (isUsed)
            {
                MessageBox.Show($"Không thể xoá đơn vị tính '{unit.DisplayName}' vì đang có dữ liệu liên quan (Sản phẩm, Hóa đơn hoặc Phiếu kho).\n\nVui lòng chuyển trạng thái đơn vị sang 'Dừng' nếu không còn sử dụng.", 
                    "Không thể xoá", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            // 2. Xác nhận xoá (nếu không có ràng buộc)
            var result = MessageBox.Show($"Đơn vị tính '{unit.DisplayName}' chưa có dữ liệu liên quan. Bạn có chắc chắn muốn xoá vĩnh viễn đơn vị tính này?", 
                "Xác nhận xoá", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try 
                {
                    _service.Delete(unit.Id, _currentUser.Id);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xoá đơn vị tính: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            worksheet.Cell(i + 2, 3).Value = Units[i].IsActive ? "Hoạt động" : "Dừng";
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
        private string Serialize(Unit u)
        {
            return System.Text.Json.JsonSerializer.Serialize(new { u.Id, u.UnitCode, u.DisplayName, u.IsActive });
        }
    }
}
