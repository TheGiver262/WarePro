using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class BrandViewModel : ObservableObject
    {
        private readonly ReferenceDataService _service;

        [ObservableProperty] private ObservableCollection<Brand> _brands = new();
        [ObservableProperty] private Brand? _selectedBrand;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _brandCode = string.Empty;
        [ObservableProperty] private string _displayName = string.Empty;

        public BrandViewModel()
        {
            _service = new ReferenceDataService();
            LoadData();
        }

        [RelayCommand]
        private void LoadData()
        {
            var data = _service.GetAllBrands();
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lowerSearch = SearchText.ToLower().Trim();
                data = data.Where(x => 
                    (x.DisplayName?.ToLower().Contains(lowerSearch) ?? false) || 
                    (x.BrandCode?.ToLower().Contains(lowerSearch) ?? false)).ToList();
            }
            Brands = new ObservableCollection<Brand>(data);
        }

        [RelayCommand]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(BrandCode)) return;

            if (SelectedBrand == null)
            {
                var b = new Brand { BrandCode = BrandCode, DisplayName = DisplayName, IsActive = true };
                _service.AddBrand(b);
            }
            else
            {
                SelectedBrand.BrandCode = BrandCode;
                SelectedBrand.DisplayName = DisplayName;
                _service.UpdateBrand(SelectedBrand);
            }
            LoadData();
            Clear();
        }

        [RelayCommand]
        private void Delete()
        {
            if (SelectedBrand != null)
            {
                _service.DeactivateBrand(SelectedBrand.Id);
                LoadData();
                Clear();
            }
        }

        [RelayCommand]
        private void Clear()
        {
            SelectedBrand = null;
            BrandCode = string.Empty;
            DisplayName = string.Empty;
        }

        partial void OnSelectedBrandChanged(Brand? value)
        {
            if (value != null)
            {
                BrandCode = value.BrandCode;
                DisplayName = value.DisplayName;
            }
        }
    }
}
