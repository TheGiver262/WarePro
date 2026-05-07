using System;
using System.Windows;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa
{
    public partial class MainWindow : Window
    {
        public MainWindow(AppUser user, Func<Data.AppDbContext> contextFactory)
        {
            InitializeComponent();
            this.DataContext = new MainViewModel(user, contextFactory);
        }
    }
}
