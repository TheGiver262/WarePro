using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Views;
using QuanLyHangHoa.Data;
using System;
using System.Threading.Tasks;
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

        // Footer counts
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _activeCount;
        [ObservableProperty] private int _inactiveCount;

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

        private List<Unit> _allUnits = new();

        [RelayCommand]
        public void LoadData()
        {
            using var db = _contextFactory();
            _allUnits = db.Units.AsNoTracking().ToList();

            // Calculate counts in memory (instant)
            TotalCount = _allUnits.Count;
            ActiveCount = _allUnits.Count(u => u.IsActive);
            InactiveCount = _allUnits.Count(u => !u.IsActive);

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var query = _allUnits.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchCode))
                query = query.Where(u => u.UnitCode.Contains(SearchCode, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchName))
                query = query.Where(u => u.DisplayName.Contains(SearchName, StringComparison.OrdinalIgnoreCase));

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

        partial void OnSearchCodeChanged(string value) => ApplyFilters();
        partial void OnSearchNameChanged(string value) => ApplyFilters();
        partial void OnSearchStatusChanged(string? value) => ApplyFilters();

        [RelayCommand(CanExecute = nameof(CanManage))]
        private async Task OpenAddUnitDialog()
        {
            var vm = new UnitEditViewModel();
            var window = new UnitEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                var newUnit = new Unit();
                vm.ApplyTo(newUnit);
                await _service.AddAsync(newUnit, _currentUser.Id, Guid.NewGuid());
                LoadData();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        // form edit là snapshot; service nhận before JSON để audit thay đổi
        private async Task EditUnit(Unit unit)
        {
            var expectedRowVersion = unit.RowVersion.ToArray();
            var vm = new UnitEditViewModel(unit);
            var window = new UnitEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                var updated = new Unit { Id = unit.Id };
                vm.ApplyTo(updated);
                try
                {
                    await _service.UpdateAsync(unit.Id, updated, expectedRowVersion, _currentUser.Id, Guid.NewGuid());
                }
                catch (DatabaseWriteConflictException)
                {
                    LoadData();
                    return;
                }
                catch (StaleEntityException)
                {
                    LoadData();
                    return;
                }
                LoadData();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        // service quyết định inactive hay xóa theo quan hệ product-unit/chứng từ, rồi ViewModel reload
        private async Task DeleteUnit(Unit unit)
        {
            var dependencies = _service.GetDependencies(unit.Id)
                .Where(dependency => dependency.Count > 0)
                .ToList();
            var dependencySummary = string.Join(
                Environment.NewLine,
                dependencies.Select(dependency => $"• {dependency.Name}: {dependency.Count}"));
            var message = dependencies.Count > 0
                ? $"Đơn vị tính '{unit.DisplayName}' đang được tham chiếu:\n{dependencySummary}\n\nThao tác này sẽ chuyển trạng thái đơn vị sang 'Dừng'. Bạn có muốn tiếp tục?"
                : $"Đơn vị tính '{unit.DisplayName}' chưa có dữ liệu liên quan. Bạn có chắc chắn muốn xoá vĩnh viễn đơn vị tính này?";

            var result = MessageBox.Show(
                message,
                dependencies.Count > 0 ? "Xác nhận dừng" : "Xác nhận xoá",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _service.DeleteAsync(unit.Id, unit.RowVersion, _currentUser.Id, Guid.NewGuid());
                    LoadData();
                }
                catch (DatabaseWriteConflictException)
                {
                    LoadData();
                }
                catch (InventoryDomainException ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                    MessageBox.Show(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private string Serialize(Unit u)
        {
            return System.Text.Json.JsonSerializer.Serialize(new { u.Id, u.UnitCode, u.DisplayName, u.IsActive });
        }
    }
}
