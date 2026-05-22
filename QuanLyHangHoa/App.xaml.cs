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
                        // 1. Ensure new tables exist (EnsureCreated doesn't add tables to existing DB)
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransfer') CREATE TABLE StockTransfer (Id INT IDENTITY(1,1) PRIMARY KEY, DocumentCode NVARCHAR(50) NOT NULL, FromWarehouseId INT NOT NULL, ToWarehouseId INT NOT NULL, Status NVARCHAR(50) NOT NULL, TransferDate DATETIME NOT NULL, Notes NVARCHAR(500), CreatedBy INT NOT NULL, ApprovedBy INT, PostedBy INT, CreatedAt DATETIME DEFAULT GETUTCDATE(), UpdatedAt DATETIME, UpdatedBy INT)",
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransferLine') CREATE TABLE StockTransferLine (Id INT IDENTITY(1,1) PRIMARY KEY, StockTransferId INT NOT NULL, ProductId INT NOT NULL, UnitId INT NOT NULL, Quantity DECIMAL(18,2) NOT NULL, BaseQuantity DECIMAL(18,2) NOT NULL)",
                        
                        // 2. Ensure new columns exist
                        "IF COL_LENGTH('Product', 'Description') IS NULL ALTER TABLE Product ADD Description NVARCHAR(MAX)",
                        "IF COL_LENGTH('Product', 'CostPrice') IS NULL ALTER TABLE Product ADD CostPrice DECIMAL(18,2)",
                        "IF COL_LENGTH('ProductSerial', 'Note') IS NULL ALTER TABLE ProductSerial ADD Note NVARCHAR(MAX)",
                        "IF COL_LENGTH('ProductSerial', 'StockTransferLineId') IS NULL ALTER TABLE ProductSerial ADD StockTransferLineId INT",
                        
                        // Invoices
                        "IF COL_LENGTH('SalesInvoice', 'CreatedAt') IS NULL ALTER TABLE SalesInvoice ADD CreatedAt DATETIME",
                        "IF COL_LENGTH('SalesInvoice', 'Notes') IS NULL ALTER TABLE SalesInvoice ADD Notes NVARCHAR(MAX)",
                        "IF COL_LENGTH('SalesInvoice', 'PaidAmount') IS NULL ALTER TABLE SalesInvoice ADD PaidAmount DECIMAL(18,2)",
                        "IF COL_LENGTH('SalesInvoice', 'PaymentStatus') IS NULL ALTER TABLE SalesInvoice ADD PaymentStatus NVARCHAR(50)",
                        "IF COL_LENGTH('SalesInvoice', 'DueDate') IS NULL ALTER TABLE SalesInvoice ADD DueDate DATETIME",
                        
                        "IF COL_LENGTH('PurchaseInvoice', 'CreatedAt') IS NULL ALTER TABLE PurchaseInvoice ADD CreatedAt DATETIME",
                        "IF COL_LENGTH('PurchaseInvoice', 'Notes') IS NULL ALTER TABLE PurchaseInvoice ADD Notes NVARCHAR(MAX)",
                        "IF COL_LENGTH('PurchaseInvoice', 'PaidAmount') IS NULL ALTER TABLE PurchaseInvoice ADD PaidAmount DECIMAL(18,2)",
                        "IF COL_LENGTH('PurchaseInvoice', 'PaymentStatus') IS NULL ALTER TABLE PurchaseInvoice ADD PaymentStatus NVARCHAR(50)",
                        "IF COL_LENGTH('PurchaseInvoice', 'DueDate') IS NULL ALTER TABLE PurchaseInvoice ADD DueDate DATETIME",
                        
                        // Stock Operations
                        "IF COL_LENGTH('StockIn', 'ImportDate') IS NULL ALTER TABLE StockIn ADD ImportDate DATETIME",
                        "IF COL_LENGTH('StockIn', 'Notes') IS NULL ALTER TABLE StockIn ADD Notes NVARCHAR(MAX)",
                        "IF COL_LENGTH('StockIn', 'UpdatedAt') IS NULL ALTER TABLE StockIn ADD UpdatedAt DATETIME",
                        "IF COL_LENGTH('StockIn', 'UpdatedBy') IS NULL ALTER TABLE StockIn ADD UpdatedBy INT",
                        
                        "IF COL_LENGTH('StockOut', 'ExportDate') IS NULL ALTER TABLE StockOut ADD ExportDate DATETIME",
                        "IF COL_LENGTH('StockOut', 'Notes') IS NULL ALTER TABLE StockOut ADD Notes NVARCHAR(MAX)",
                        "IF COL_LENGTH('StockOut', 'UpdatedAt') IS NULL ALTER TABLE StockOut ADD UpdatedAt DATETIME",
                        "IF COL_LENGTH('StockOut', 'UpdatedBy') IS NULL ALTER TABLE StockOut ADD UpdatedBy INT",
                        "IF COL_LENGTH('StockOutLine', 'DraftSerials') IS NULL ALTER TABLE StockOutLine ADD DraftSerials NVARCHAR(MAX)",
                        "IF COL_LENGTH('StockInLine', 'DraftSerials') IS NULL ALTER TABLE StockInLine ADD DraftSerials NVARCHAR(MAX)",
                        
                        "IF COL_LENGTH('StockAdjustment', 'Notes') IS NULL ALTER TABLE StockAdjustment ADD Notes NVARCHAR(MAX)",
                        "IF COL_LENGTH('StockCountSession', 'Notes') IS NULL ALTER TABLE StockCountSession ADD Notes NVARCHAR(MAX)",
                        
                        "IF COL_LENGTH('StockTransfer', 'Notes') IS NULL ALTER TABLE StockTransfer ADD Notes NVARCHAR(MAX)",
                        "IF COL_LENGTH('StockTransfer', 'UpdatedAt') IS NULL ALTER TABLE StockTransfer ADD UpdatedAt DATETIME",
                        "IF COL_LENGTH('StockTransfer', 'UpdatedBy') IS NULL ALTER TABLE StockTransfer ADD UpdatedBy INT"
                    };

                    foreach (var sql in migrations)
                    {
                        try
                        {
                            command.CommandText = sql;
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Migration Error: {sql} -> {ex.Message}");
                        }
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
