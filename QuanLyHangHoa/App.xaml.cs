using System.Windows;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System;
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

                // Seed database if empty
                string excelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "WarePro_Export_5-5-2026.xlsx");
                // If not in bin folder, check parent folder (development)
                if (!File.Exists(excelPath))
                    excelPath = Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.FullName ?? "", "database", "WarePro_Export_5-5-2026.xlsx");
                
                if (File.Exists(excelPath))
                {
                    try
                    {
                        var seeder = new Services.DataImport.DatabaseSeeder(db, excelPath);
                        var log = seeder.SeedAsync().GetAwaiter().GetResult();
                        if (!string.IsNullOrEmpty(log) && (log.Contains("Lỗi") || log.Contains("Đã nạp")))
                        {
                            // Optional: MessageBox.Show(log, "Kết quả nạp dữ liệu");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi khởi tạo dữ liệu: {ex.Message}", "Lỗi hệ thống");
                    }
                }
                
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
