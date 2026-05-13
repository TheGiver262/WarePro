using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;

namespace QuanLyHangHoa.Scratch
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    Console.WriteLine("Checking StockOut columns...");
                    var stockOutColumns = context.Database.SqlQueryRaw<string>(
                        "SELECT name FROM sys.columns WHERE object_id = OBJECT_ID('StockOut')").ToList();
                    
                    Console.WriteLine("Columns in StockOut:");
                    foreach (var col in stockOutColumns)
                    {
                        Console.WriteLine($"- {col}");
                    }

                    Console.WriteLine("\nChecking StockIn columns...");
                    var stockInColumns = context.Database.SqlQueryRaw<string>(
                        "SELECT name FROM sys.columns WHERE object_id = OBJECT_ID('StockIn')").ToList();
                    
                    Console.WriteLine("Columns in StockIn:");
                    foreach (var col in stockInColumns)
                    {
                        Console.WriteLine($"- {col}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
                }
            }
        }
    }
}
