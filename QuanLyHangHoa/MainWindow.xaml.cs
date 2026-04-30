using System.Windows;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa
{
    public partial class MainWindow : Window
    {
        public MainWindow(AppUser user, Data.AppDbContext dbContext)
        {
            InitializeComponent();
            this.DataContext = new MainViewModel(user, dbContext);
        }
    }
}
