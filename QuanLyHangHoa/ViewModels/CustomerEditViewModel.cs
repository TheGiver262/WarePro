using CommunityToolkit.Mvvm.ComponentModel;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.ViewModels
{
    public partial class CustomerEditViewModel : ObservableObject
    {
        [ObservableProperty] private string _windowTitle = string.Empty;
        [ObservableProperty] private string _customerCode = string.Empty;
        [ObservableProperty] private string _displayName = string.Empty;
        [ObservableProperty] private string? _phone;
        [ObservableProperty] private string? _email;
        [ObservableProperty] private string? _address;
        [ObservableProperty] private bool _isActive = true;
        [ObservableProperty] private bool _isEditMode;

        public CustomerEditViewModel(Customer? customer = null)
        {
            if (customer != null)
            {
                WindowTitle = "CHỈNH SỬA KHÁCH HÀNG";
                CustomerCode = customer.CustomerCode;
                DisplayName = customer.DisplayName;
                Phone = customer.Phone;
                Email = customer.Email;
                Address = customer.Address;
                IsActive = customer.IsActive;
                IsEditMode = true;
            }
            else
            {
                WindowTitle = "THÊM KHÁCH HÀNG MỚI";
                IsEditMode = false;
            }
        }

        // ApplyTo tách state dialog khỏi entity danh sách và chỉ chạy khi validation của View thành công
        public void ApplyTo(Customer customer)
        {
            customer.CustomerCode = CustomerCode;
            customer.DisplayName = DisplayName;
            customer.Phone = Phone;
            customer.Email = Email;
            customer.Address = Address;
            customer.IsActive = IsActive;
        }
    }
}
