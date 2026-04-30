using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class CustomerViewModel : ObservableObject
    {
        private readonly ReferenceDataService _service;

        [ObservableProperty]
        private ObservableCollection<Customer> _customers = new();

        [ObservableProperty]
        private Customer? _selectedCustomer;

        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private string _customerCode = string.Empty;

        [ObservableProperty]
        private string _address = string.Empty;

        [ObservableProperty]
        private string _phone = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        public CustomerViewModel()
        {
            _service = new ReferenceDataService();
            LoadData();
        }

        private void LoadData()
        {
            var list = _service.GetAllCustomers();
            Customers = new ObservableCollection<Customer>(list);
        }

        [RelayCommand]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(CustomerCode)) return;

            if (SelectedCustomer == null)
            {
                _service.AddCustomer(new Customer 
                { 
                    DisplayName = DisplayName, 
                    CustomerCode = CustomerCode,
                    Address = Address,
                    Phone = Phone,
                    Email = Email
                });
            }
            else
            {
                SelectedCustomer.DisplayName = DisplayName;
                SelectedCustomer.CustomerCode = CustomerCode;
                SelectedCustomer.Address = Address;
                SelectedCustomer.Phone = Phone;
                SelectedCustomer.Email = Email;
                _service.UpdateCustomer(SelectedCustomer);
            }
            LoadData();
            Clear();
        }

        [RelayCommand]
        private void Delete()
        {
            if (SelectedCustomer != null)
            {
                _service.DeactivateCustomer(SelectedCustomer.Id);
                LoadData();
                Clear();
            }
        }

        private void Clear()
        {
            SelectedCustomer = null;
            DisplayName = string.Empty;
            CustomerCode = string.Empty;
            Address = string.Empty;
            Phone = string.Empty;
            Email = string.Empty;
        }

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            if (value != null)
            {
                DisplayName = value.DisplayName;
                CustomerCode = value.CustomerCode;
                Address = value.Address ?? string.Empty;
                Phone = value.Phone ?? string.Empty;
                Email = value.Email ?? string.Empty;
            }
        }
    }
}
