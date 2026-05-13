using System.Windows;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
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

                // Manual Migration for new columns - MUST RUN BEFORE SEEDING
                var connection = db.Database.GetDbConnection();
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    var migrations = new[] 
                    {
                        "ALTER TABLE Product ADD Description NVARCHAR(MAX)",
                        "ALTER TABLE Product ADD CostPrice DECIMAL(18,2)",
                        "ALTER TABLE ProductSerial ADD Note NVARCHAR(MAX)",
                        "ALTER TABLE SalesInvoice ADD CreatedAt DATETIME",
                        "ALTER TABLE SalesInvoice ADD Notes NVARCHAR(MAX)",
                        "ALTER TABLE PurchaseInvoice ADD CreatedAt DATETIME",
                        "ALTER TABLE PurchaseInvoice ADD Notes NVARCHAR(MAX)",
                        "ALTER TABLE StockIn ADD ImportDate DATETIME",
                        "ALTER TABLE StockIn ADD Notes NVARCHAR(MAX)",
                        "ALTER TABLE StockOut ADD ExportDate DATETIME",
                        "ALTER TABLE StockOut ADD Notes NVARCHAR(MAX)",
                        "ALTER TABLE StockAdjustment ADD Notes NVARCHAR(MAX)",
                        "ALTER TABLE StockCount ADD Notes NVARCHAR(MAX)"
                    };

                    foreach (var sql in migrations)
                    {
                        try
                        {
                            command.CommandText = sql;
                            command.ExecuteNonQuery();
                        }
                        catch { /* Column likely already exists */ }
                    }
                }

                // Seed database if empty
                string excelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "warepro_database_seed.xlsx");
                
                // Fallback for development (absolute path if needed)
                if (!File.Exists(excelPath))
                {
                    excelPath = @"C:\WarePro\Database\warepro_database_seed.xlsx";
                }
                
                if (File.Exists(excelPath))
                {
                    try
                    {
                        // The existing seeder already handles ProductSerials
                        Console.WriteLine("[APP] Initializing DatabaseSeeder...");
                        var seeder = new Services.DataImport.DatabaseSeeder(db, excelPath);
                        Console.WriteLine("[APP] Starting SeedAsync (Task.Run)...");
                        var log = Task.Run(async () => await seeder.SeedAsync()).GetAwaiter().GetResult();
                        Console.WriteLine($"[APP] SeedAsync Result: {log}");
                        Console.WriteLine($"[SEED] Result: {log}");
                    }
                    catch (Exception ex)
                    {
                        // Log error to console for the agent to see
                        Console.WriteLine($"Seed Error: {ex.Message}");
                    }
                }
            }
        }
    }
}
