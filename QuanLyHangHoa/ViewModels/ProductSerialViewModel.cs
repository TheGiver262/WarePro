using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ProductSerialViewModel : ObservableObject
    {
        private readonly Func<string, string, List<ProductSerial>> _serialLoader;

        [ObservableProperty] private ObservableCollection<ProductSerial> _serials = new();
        [ObservableProperty] private ObservableCollection<string> _statuses = new();
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedStatus = "All";
        [ObservableProperty] private ProductSerial? _selectedSerial;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public ProductSerialViewModel()
            : this(new ProductSerialService().SearchSerials)
        {
        }

        public ProductSerialViewModel(Func<string, string, List<ProductSerial>> serialLoader)
        {
            _serialLoader = serialLoader;
            Statuses = new ObservableCollection<string> { "All" };
            foreach (var status in Enum.GetNames<SerialStatus>())
            {
                Statuses.Add(status);
            }

            LoadSerials();
        }

        [RelayCommand]
        private void SearchSerials()
        {
            LoadSerials();
        }

        [RelayCommand]
        private void ClearSearch()
        {
            SearchText = string.Empty;
            SelectedStatus = "All";
            LoadSerials();
        }

        private void LoadSerials()
        {
            Serials = new ObservableCollection<ProductSerial>(_serialLoader(SearchText, SelectedStatus));
            StatusMessage = $"Tim thay {Serials.Count} serial.";
        }
    }
}
