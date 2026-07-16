using CommunityToolkit.Mvvm.ComponentModel;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.ViewModels
{
    public partial class SupplierEditViewModel : ObservableObject
    {
        [ObservableProperty] private string _windowTitle = string.Empty;
        [ObservableProperty] private string _supplierCode = string.Empty;
        [ObservableProperty] private string _displayName = string.Empty;
        [ObservableProperty] private string? _phone;
        [ObservableProperty] private string? _email;
        [ObservableProperty] private string? _address;
        [ObservableProperty] private bool _isActive = true;
        [ObservableProperty] private bool _isEditMode;

        public SupplierEditViewModel(Supplier? supplier = null)
        {
            if (supplier != null)
            {
                WindowTitle = "CHỈNH SỬA NHÀ CUNG CẤP";
                SupplierCode = supplier.SupplierCode;
                DisplayName = supplier.DisplayName;
                Phone = supplier.Phone;
                Email = supplier.Email;
                Address = supplier.Address;
                IsActive = supplier.IsActive;
                IsEditMode = true;
            }
            else
            {
                WindowTitle = "THÊM NHÀ CUNG CẤP MỚI";
                IsEditMode = false;
            }
        }

        // chép snapshot form vào target; service phía danh sách chịu audit và dependency
        public void ApplyTo(Supplier supplier)
        {
            supplier.SupplierCode = SupplierCode;
            supplier.DisplayName = DisplayName;
            supplier.Phone = Phone;
            supplier.Email = Email;
            supplier.Address = Address;
            supplier.IsActive = IsActive;
        }
    }
}
