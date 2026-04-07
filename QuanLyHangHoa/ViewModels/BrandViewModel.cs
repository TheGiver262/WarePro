using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class BrandViewModel : ObservableObject
    {
        private readonly ReferenceDataService _svc = new();
        [ObservableProperty] private ObservableCollection<Brand> _brands = new();
        [ObservableProperty] private Brand? _selectedBrand;
        [ObservableProperty] private string _editName = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public BrandViewModel() => LoadData();
        private void LoadData() => Brands = new ObservableCollection<Brand>(_svc.GetAllBrands());

        [RelayCommand]
        private void Add()
        {
            if (string.IsNullOrWhiteSpace(EditName)) { StatusMessage = "Tên không được trống!"; return; }
            _svc.AddBrand(new Brand { Name = EditName.Trim() });
            EditName = string.Empty; LoadData(); StatusMessage = "Thêm thành công.";
        }
        [RelayCommand]
        private void SaveEdit()
        {
            if (SelectedBrand == null) { StatusMessage = "Chưa chọn mục!"; return; }
            SelectedBrand.Name = EditName.Trim(); _svc.UpdateBrand(SelectedBrand); LoadData(); StatusMessage = "Cập nhật thành công.";
        }
        [RelayCommand]
        private void Delete()
        {
            if (SelectedBrand == null) { StatusMessage = "Chưa chọn mục!"; return; }
            _svc.DeleteBrand(SelectedBrand.Id); LoadData(); StatusMessage = "Đã xoá.";
        }
        partial void OnSelectedBrandChanged(Brand? value) => EditName = value?.Name ?? string.Empty;
    }
}
