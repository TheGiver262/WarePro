using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Services.DataImport;
using QuanLyHangHoa.Views;

namespace QuanLyHangHoa.ViewModels
{
    public partial class UnitViewModel : ObservableObject
    {
        private readonly ReferenceDataService _svc = new();
        private readonly DataImportManager _importManager = new();

        [ObservableProperty] private ObservableCollection<Unit> _units = new();
        [ObservableProperty] private Unit? _selectedUnit;
        [ObservableProperty] private string _editName = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public UnitViewModel() => LoadData();

        private void LoadData()
        {
            Units = new ObservableCollection<Unit>(_svc.GetAllUnits());
        }

        [RelayCommand]
        private void Add()
        {
            if (string.IsNullOrWhiteSpace(EditName)) { StatusMessage = "Tên không được trống!"; return; }
            _svc.AddUnit(new Unit { Name = EditName.Trim() });
            EditName = string.Empty;
            LoadData();
            StatusMessage = "Thêm thành công.";
        }

        [RelayCommand]
        private void SaveEdit()
        {
            if (SelectedUnit == null) { StatusMessage = "Chưa chọn mục!"; return; }
            if (string.IsNullOrWhiteSpace(EditName)) { StatusMessage = "Tên không được trống!"; return; }
            SelectedUnit.Name = EditName.Trim();
            _svc.UpdateUnit(SelectedUnit);
            LoadData();
            StatusMessage = "Cập nhật thành công.";
        }

        [RelayCommand]
        private void Delete()
        {
            if (SelectedUnit == null) { StatusMessage = "Chưa chọn mục!"; return; }
            _svc.DeleteUnit(SelectedUnit.Id);
            LoadData();
            StatusMessage = "Đã xoá.";
        }

        [RelayCommand]
        private void ImportData()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls|CSV Files|*.csv|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var result = _importManager.ProcessFile<Unit>(dialog.FileName);
                    LoadData();
                    var reportWin = new ImportResultWindow(result.SuccessCount, result.Errors);
                    reportWin.ShowDialog();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.Message, "Lỗi Import", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        partial void OnSelectedUnitChanged(Unit? value)
        {
            EditName = value?.Name ?? string.Empty;
        }
    }
}
