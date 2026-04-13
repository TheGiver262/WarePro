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
    // Cầu nối màn hình Quản trị Tài Khoản - Chức năng đặc quyền phân vùng Admin
    public partial class EmployeeViewModel : ObservableObject
    {
        private readonly EmployeeService _employeeService;
        private readonly DataImportManager _importManager = new();

        // Bảng dữ liệu người lao động hiển thị trực tiếp lên DataGrid WPF
        [ObservableProperty]
        private ObservableCollection<Employee> _employees = new();

        // Nhân viên đang được click chuột vào
        [ObservableProperty]
        private Employee? _selectedEmployee;

        // Khung thông tin Form nhập liệu bên trên (Binding 2 Cập nhật)
        [ObservableProperty]
        private Employee _currentInputEmployee;

        public EmployeeViewModel()
        {
            _employeeService = new EmployeeService();
            CurrentInputEmployee = new Employee();
            LoadData();
        }

        private void LoadData()
        {
            var list = _employeeService.GetAllEmployees();
            Employees = new ObservableCollection<Employee>(list);
        }

        // Resest Form rỗng để chuẩn bị Add mới (Tránh ghi đè lên tài khoản cũ)
        [RelayCommand]
        private void ClearInput()
        {
            CurrentInputEmployee = new Employee() { DateOfBirth = System.DateTime.Now.AddYears(-20) };
            SelectedEmployee = null;
        }

        // Nạp và Lưu Nhân Viên
        [RelayCommand]
        private void SaveEmployee()
        {
            if (string.IsNullOrWhiteSpace(CurrentInputEmployee.FullName) || string.IsNullOrWhiteSpace(CurrentInputEmployee.Username))
            {
                System.Windows.MessageBox.Show("Vui lòng điền đủ Tên Đầy đủ và Tên tài khoản (để đăng nhập)!", "Cảnh báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Dấu hiệu ID=0 nghĩa là tài khoản MỚI. Khác 0 nghĩa là đang CẬP NHẬT 
            if (CurrentInputEmployee.Id == 0)
            {
                _employeeService.AddEmployee(CurrentInputEmployee);
            }
            else
            {
                _employeeService.UpdateEmployee(CurrentInputEmployee);
            }

            LoadData(); // Re render mượt mà
            ClearInput();
        }

        // Lệnh xoá - Gọi khi bấm nút màu đỏ
        [RelayCommand]
        private void DeleteEmployee()
        {
            if (SelectedEmployee != null && SelectedEmployee.Id > 0)
            {
                // Cấm xoá trùm cuối admin
                if (SelectedEmployee.Id == 1)
                {
                    System.Windows.MessageBox.Show("Thao tác Vô Hiệu Lệnh: Không được tự xoá tài khoản Admin Chủ Tịch tối cao!", "Thành Trì Cuối Cùng", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Hand);
                    return;
                }

                _employeeService.DeleteEmployee(SelectedEmployee.Id);
                LoadData();
                ClearInput();
            }
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
                    var result = _importManager.ProcessFile<Employee>(dialog.FileName);
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

        // Khi lưới bên UI chọn vào dòng nào, ta bốc data quăng lên form trên
        partial void OnSelectedEmployeeChanged(Employee? value)
        {
            if (value != null)
            {
                CurrentInputEmployee = new Employee 
                {
                    Id = value.Id,
                    FullName = value.FullName,
                    DateOfBirth = value.DateOfBirth,
                    Position = value.Position,
                    Username = value.Username,
                    PasswordHash = value.PasswordHash, // Có thể ẩn đi nếu cần bảo mật
                    Role = value.Role
                };
            }
        }
    }
}
