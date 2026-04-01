using System.Windows;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa
{
    public partial class MainWindow : Window
    {
        // Nhận Employee từ màn hình Login truyền sang MainWindow
        public MainWindow(Employee user)
        {
            InitializeComponent();
            
            // Gắn view model và cắm DataContext
            // Quản lý sẽ điều khiển view và binding dữ liệu
            this.DataContext = new MainViewModel(user);
        }
    }
}
