using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class UnitViewModel : ObservableObject
    {
        private readonly ReferenceDataService _service;

        [ObservableProperty]
        private ObservableCollection<Unit> _units = new();

        [ObservableProperty]
        private Unit? _selectedUnit;

        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private string _unitCode = string.Empty;

        public UnitViewModel()
        {
            _service = new ReferenceDataService();
            LoadData();
        }

        private void LoadData()
        {
            var list = _service.GetAllUnits();
            Units = new ObservableCollection<Unit>(list);
        }

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
