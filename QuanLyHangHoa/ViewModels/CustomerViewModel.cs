using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class CustomerViewModel : ObservableObject
    {
        private readonly ReferenceDataService _svc = new();
        [ObservableProperty] private ObservableCollection<Customer> _customers = new();
        [ObservableProperty] private Customer? _selectedCustomer;
        [ObservableProperty] private string _editName    = string.Empty;
        [ObservableProperty] private string _editAddress = string.Empty;
        [ObservableProperty] private string _editPhone   = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public CustomerViewModel() => LoadData();
        private void LoadData() => Customers = new ObservableCollection<Customer>(_svc.GetAllCustomers());

        [RelayCommand]
        private void Add()
        {
            if (string.IsNullOrWhiteSpace(EditName)) { StatusMessage = "Tên không được trống!"; return; }
            _svc.AddCustomer(new Customer { Name = EditName.Trim(), Address = EditAddress, Phone = EditPhone });
            ClearInputs(); LoadData(); StatusMessage = "Thêm thành công.";
        }
        [RelayCommand]
        private void SaveEdit()
        {
            if (SelectedCustomer == null) { StatusMessage = "Chưa chọn mục!"; return; }
            SelectedCustomer.Name = EditName.Trim(); SelectedCustomer.Address = EditAddress; SelectedCustomer.Phone = EditPhone;
            _svc.UpdateCustomer(SelectedCustomer); LoadData(); StatusMessage = "Cập nhật thành công.";
        }
        [RelayCommand]
        private void Delete()
        {
            if (SelectedCustomer == null) { StatusMessage = "Chưa chọn mục!"; return; }
            _svc.DeleteCustomer(SelectedCustomer.Id); LoadData(); StatusMessage = "Đã xoá.";
        }
        private void ClearInputs() { EditName = string.Empty; EditAddress = string.Empty; EditPhone = string.Empty; }
        partial void OnSelectedCustomerChanged(Customer? value)
        {
            EditName    = value?.Name    ?? string.Empty;
            EditAddress = value?.Address ?? string.Empty;
            EditPhone   = value?.Phone   ?? string.Empty;
        }
    }
}
