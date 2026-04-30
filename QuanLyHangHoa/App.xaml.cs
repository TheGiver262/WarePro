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
                
                // Manual Migration for new columns
                var connection = db.Database.GetDbConnection();
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    string[] tables = { "SalesInvoices", "PurchaseInvoices" };
                    string[] columns = { "CreatedAt", "Notes" };

                    foreach (var table in tables)
                    {
                        foreach (var column in columns)
                        {
                            try
                            {
                                command.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} TEXT";
                                command.ExecuteNonQuery();
                            }
                            catch { /* Column already exists or table doesn't exist yet */ }
                        }
                    }
                }
            }
        }
    }
}
