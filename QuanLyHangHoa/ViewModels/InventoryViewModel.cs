using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class InventoryViewModel : ObservableObject
    {
        private readonly ProductService _productService;

        [ObservableProperty] private ObservableCollection<Product> _inventoryItems = new();
        [ObservableProperty] private string _searchText = string.Empty;

        private readonly Func<Data.AppDbContext> _contextFactory;

        public InventoryViewModel(Func<Data.AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _productService = new ProductService(contextFactory);
            LoadData();
        }

        [RelayCommand]
        private void LoadData()
        {
            var results = _productService.GetAllProducts();
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.ToLower();
                results = results.Where(p => 
                    p.DisplayName.ToLower().Contains(term) || 
                    p.ProductCode.ToLower().Contains(term)).ToList();
            }
            InventoryItems = new ObservableCollection<Product>(results);
        }

        [RelayCommand]
        private void Search()
        {
            LoadData();
        }

        [RelayCommand]
        private void Refresh()
        {
            SearchText = string.Empty;
            LoadData();
        }

        [RelayCommand]
        private void Export()
        {
            // Placeholder for export logic
            System.Windows.MessageBox.Show("Chức năng xuất báo cáo đang được phát triển.", "Thông báo");
        }
    }
}
