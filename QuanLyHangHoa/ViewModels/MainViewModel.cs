using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Views;
using System.Windows.Controls;

namespace QuanLyHangHoa.ViewModels
{
    // Kế thừa ObservableObject để quản lý trạng thái, dữ liệu UI
    public partial class MainViewModel : ObservableObject
    {
        // Thuộc tính lưu trữ thông tin User đang đăng nhập 
        [ObservableProperty]
        private Employee _currentUser;

        // View hiện tại hiển thị ở khu vực giữa (ContentControl)
        // Sẽ được bind với binding trên MainWindow UI
        [ObservableProperty]
        private UserControl? _currentView;

        public MainViewModel(Employee user)
        {
            // Thiết lập user đăng nhập
            _currentUser = user;
            
            // Trang chủ lúc đầu chuyển thẳng sang Quản lý hàng hoá
            CurrentView = new ProductView();
        }

        [RelayCommand]
        private void OpenProductView()
        {
            // Set frame chính sang trang Quản lý Hàng Hoá
            CurrentView = new ProductView();
        }

        [RelayCommand]
        private void OpenInvoiceView()
        {
            // Set frame chính sang trang Hoá đơn
            var invoiceView = new InvoiceView();
            // Gắn ViewModel & truyền nhân viên hiện tại để ghi log thu ngân
            invoiceView.DataContext = new InvoiceViewModel(_currentUser);
            CurrentView = invoiceView;
        }

        [RelayCommand]
        private void OpenImportView()
        {
            var importView = new ImportView();
            importView.DataContext = new ImportViewModel(_currentUser);
            CurrentView = importView;
        }

        [RelayCommand]
        private void OpenWarrantyView()
        {
            CurrentView = new WarrantyView();
        }
        
        [RelayCommand]
        private void OpenEmployeeView()
        {
            // Gọi màn hình Quản lý nhân sự
            if (_currentUser.Role == "Admin")
            {
                CurrentView = new EmployeeView();
            }
            else
            {
                System.Windows.MessageBox.Show("Bạn không phải Admin! Vui lòng quay lại.", "Cảnh Báo Xâm Nhập", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Logout()
        {
            // Thoát hệ thống
            Views.LoginView login = new Views.LoginView();
            login.Show();

            foreach (System.Windows.Window window in System.Windows.Application.Current.Windows)
            {
                if (window is QuanLyHangHoa.MainWindow)
                {
                    window.Close();
                    break;
                }
            }
        }
    }
}
