using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Services.DataImport;

namespace SeedRunner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Starting Database Seeder Runner ===");
            
            string excelPath = @"C:\WarePro\Database\WarePro_Export_5-5-2026.xlsx";
            
            try
            {
                using (var db = new AppDbContext())
                {
                    Console.WriteLine($"Checking database connection...");
                    
                    var seeder = new DatabaseSeeder(db, excelPath);
                    Console.WriteLine("Running SeedAsync...");
                    
                    var result = await seeder.SeedAsync();
                    
                    Console.WriteLine("--- Seeder Result ---");
                    Console.WriteLine(result);
                    Console.WriteLine("---------------------");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL ERROR: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"INNER ERROR: {ex.InnerException.Message}");
                }
            }
            
            Console.WriteLine("=== Seeder Runner Finished ===");
        }
    }
}
