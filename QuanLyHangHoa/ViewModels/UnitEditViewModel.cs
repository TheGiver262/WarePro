using CommunityToolkit.Mvvm.ComponentModel;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.ViewModels
{
    public partial class UnitEditViewModel : ObservableObject
    {
        [ObservableProperty] private string _windowTitle = string.Empty;
        [ObservableProperty] private string _unitCode = string.Empty;
        [ObservableProperty] private string _displayName = string.Empty;
        [ObservableProperty] private bool _isActive = true;
        [ObservableProperty] private bool _isEditMode;

        public UnitEditViewModel(Unit? unit = null)
        {
            if (unit != null)
            {
                WindowTitle = "CHỈNH SỬA ĐƠN VỊ TÍNH";
                UnitCode = unit.UnitCode;
                DisplayName = unit.DisplayName;
                IsActive = unit.IsActive;
                IsEditMode = true;
            }
            else
            {
                WindowTitle = "THÊM ĐƠN VỊ TÍNH MỚI";
                IsEditMode = false;
            }
        }

        public void ApplyTo(Unit unit)
        {
            unit.UnitCode = UnitCode;
            unit.DisplayName = DisplayName;
            unit.IsActive = IsActive;
        }
    }
}
