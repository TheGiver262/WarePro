using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class UnitViewModel : ObservableObject
    {
        private readonly ReferenceDataService _svc = new();

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

        partial void OnSelectedUnitChanged(Unit? value)
        {
            EditName = value?.Name ?? string.Empty;
        }
    }
}
