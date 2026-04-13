using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Services.DataImport;
using QuanLyHangHoa.Views;

namespace QuanLyHangHoa.ViewModels
{
    public partial class CategoryViewModel : ObservableObject
    {
        private readonly ReferenceDataService _svc = new();
        private readonly DataImportManager _importManager = new();

        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private Category? _selectedCategory;
        [ObservableProperty] private string _editName = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public CategoryViewModel() => LoadData();
        private void LoadData() => Categories = new ObservableCollection<Category>(_svc.GetAllCategories());

        [RelayCommand]
        private void Add()
        {
            if (string.IsNullOrWhiteSpace(EditName)) { StatusMessage = "Tên không được trống!"; return; }
            _svc.AddCategory(new Category { Name = EditName.Trim() });
            EditName = string.Empty; LoadData(); StatusMessage = "Thêm thành công.";
        }
        [RelayCommand]
        private void SaveEdit()
        {
            if (SelectedCategory == null) { StatusMessage = "Chưa chọn mục!"; return; }
            SelectedCategory.Name = EditName.Trim(); _svc.UpdateCategory(SelectedCategory); LoadData(); StatusMessage = "Cập nhật thành công.";
        }
        [RelayCommand]
        private void Delete()
        {
            if (SelectedCategory == null) { StatusMessage = "Chưa chọn mục!"; return; }
            _svc.DeleteCategory(SelectedCategory.Id); LoadData(); StatusMessage = "Đã xoá.";
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
                    var result = _importManager.ProcessFile<Category>(dialog.FileName);
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

        partial void OnSelectedCategoryChanged(Category? value) => EditName = value?.Name ?? string.Empty;
    }
}
