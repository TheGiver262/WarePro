using System.Windows;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;
using System;
using QuanLyHangHoa.Data;

namespace QuanLyHangHoa.Views
{
    public partial class ProductSerialEditView : Window
    {
        public ProductSerialEditView(Func<AppDbContext> contextFactory, ProductSerial serial, int userId)
        {
            InitializeComponent();
            DataContext = new ProductSerialEditViewModel(contextFactory, serial, userId);
        }
    }
}
