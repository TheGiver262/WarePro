using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using ClosedXML.Excel;
using System;
using System.Windows;

namespace QuanLyHangHoa.ViewModels
{
    public partial class UnitViewModel : ObservableObject
    {
        private readonly ReferenceDataService _service;

        [ObservableProperty]
        private ObservableCollection<Unit> _units = new();

        [ObservableProperty]
        private Unit? _selectedUnit;

        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private string _displayName = string.Empty;
        [ObservableProperty] private string _unitCode = string.Empty;

        public UnitViewModel()
        {
            _service = new ReferenceDataService();
            LoadData();
        }

        [RelayCommand]
        private void LoadData()
        {
            var data = _service.GetAllUnits();
            
            if (!string.IsNullOrWhiteSpace(SearchCode))
            {
                var lower = SearchCode.ToLower().Trim();
                data = data.Where(x => x.UnitCode?.ToLower().Contains(lower) ?? false).ToList();
            }

            if (!string.IsNullOrWhiteSpace(SearchName))
            {
                var lower = SearchName.ToLower().Trim();
                data = data.Where(x => x.DisplayName?.ToLower().Contains(lower) ?? false).ToList();
            }

            Units = new ObservableCollection<Unit>(data);
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

                        // Headers
                        worksheet.Cell(1, 1).Value = "Mã Đơn Vị";
                        worksheet.Cell(1, 2).Value = "Tên Đơn Vị";

                        var headerRange = worksheet.Range(1, 1, 1, 2);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                        // Data
                        for (int i = 0; i < Units.Count; i++)
                        {
                            worksheet.Cell(i + 2, 1).Value = Units[i].UnitCode;
                            worksheet.Cell(i + 2, 2).Value = Units[i].DisplayName;
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

        partial void OnSearchCodeChanged(string value) => LoadData();
        partial void OnSearchNameChanged(string value) => LoadData();

        [RelayCommand]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(UnitCode)) return;

            if (SelectedUnit == null)
            {
                _service.AddUnit(new Unit { DisplayName = DisplayName, UnitCode = UnitCode });
            }
            else
            {
                SelectedUnit.DisplayName = DisplayName;
                SelectedUnit.UnitCode = UnitCode;
                _service.UpdateUnit(SelectedUnit);
            }
            LoadData();
            Clear();
        }

        [RelayCommand]
        private void Delete()
        {
            if (SelectedUnit != null)
            {
                _service.DeactivateUnit(SelectedUnit.Id);
                LoadData();
                Clear();
            }
        }

        [RelayCommand]
        private void Clear()
        {
            SelectedUnit = null;
            DisplayName = string.Empty;
            UnitCode = string.Empty;
        }

        partial void OnSelectedUnitChanged(Unit? value)
        {
            if (value != null)
            {
                DisplayName = value.DisplayName;
                UnitCode = value.UnitCode;
            }
        }
    }
}
