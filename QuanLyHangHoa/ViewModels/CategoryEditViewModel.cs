using CommunityToolkit.Mvvm.ComponentModel;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.ViewModels
{
    public partial class CategoryEditViewModel : ObservableObject
    {
        [ObservableProperty] private string _windowTitle;
        [ObservableProperty] private string _categoryCode = string.Empty;
        [ObservableProperty] private string _displayName = string.Empty;
        [ObservableProperty] private bool _isActive = true;
        [ObservableProperty] private bool _isEditMode;

        public CategoryEditViewModel(Category? category = null)
        {
            if (category != null)
            {
                WindowTitle = "CHỈNH SỬA DANH MỤC";
                CategoryCode = category.CategoryCode;
                DisplayName = category.DisplayName;
                IsActive = category.IsActive;
                IsEditMode = true;
            }
            else
            {
                WindowTitle = "THÊM DANH MỤC MỚI";
                IsEditMode = false;
            }
        }

        // chỉ chép field form vào entity sau khi cửa sổ xác nhận; list row gốc không đổi khi Cancel
        public void ApplyTo(Category category)
        {
            category.CategoryCode = CategoryCode;
            category.DisplayName = DisplayName;
            category.IsActive = IsActive;
        }
    }
}
