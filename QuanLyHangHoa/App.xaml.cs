using System.Windows;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;

namespace QuanLyHangHoa
{
    public partial class App : Application
    {
        // Khởi tạo Database nếu chưa có khi App khởi động
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Chạy Database EnsureCreated để hệ thống tự tạo DB
            using (var db = new AppDbContext())
            {
                db.Database.EnsureCreated();
            }
        }
    }
}
