using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class CategoryViewModel : ObservableObject
    {
        private readonly ReferenceDataService _service;

        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private Category? _selectedCategory;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _categoryCode = string.Empty;
        [ObservableProperty] private string _displayName = string.Empty;

        public CategoryViewModel()
        {
            _service = new ReferenceDataService();
            LoadData();
        }

        [RelayCommand]
        private void LoadData()
        {
            var data = _service.GetAllCategories();
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lowerSearch = SearchText.ToLower().Trim();
                data = data.Where(x => 
                    (x.DisplayName?.ToLower().Contains(lowerSearch) ?? false) || 
                    (x.CategoryCode?.ToLower().Contains(lowerSearch) ?? false)).ToList();
            }
            Categories = new ObservableCollection<Category>(data);
        }

        [RelayCommand]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(CategoryCode)) return;

            if (SelectedCategory == null)
            {
                var c = new Category { CategoryCode = CategoryCode, DisplayName = DisplayName, IsActive = true };
                _service.AddCategory(c);
            }
            else
            {
                SelectedCategory.CategoryCode = CategoryCode;
                SelectedCategory.DisplayName = DisplayName;
                _service.UpdateCategory(SelectedCategory);
            }
            LoadData();
            Clear();
        }

        [RelayCommand]
        private void Delete()
        {
            if (SelectedCategory != null)
            {
                _service.DeactivateCategory(SelectedCategory.Id);
                LoadData();
                Clear();
            }
        }

        [RelayCommand]
        private void Clear()
        {
            SelectedCategory = null;
            CategoryCode = string.Empty;
            DisplayName = string.Empty;
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            if (value != null)
            {
                CategoryCode = value.CategoryCode;
                DisplayName = value.DisplayName;
            }
        }
    }
}
