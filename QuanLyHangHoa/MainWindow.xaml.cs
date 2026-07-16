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
            DataContext = new MainViewModel(user, contextFactory);
            ContentRendered += OnContentRendered;
        }

        private async void OnContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= OnContentRendered;

            try
            {
                if (DataContext is MainViewModel viewModel)
                {
                    await viewModel.LoadInitialViewAsync();
                    _ = viewModel.CheckForUpdatesAutomaticallyAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dashboard load failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}