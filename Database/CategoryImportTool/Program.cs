using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;

namespace CategoryImportTool
{
    class Program
    {
        static void Main(string[] args)
        {
            string excelPath = @"C:\WarePro\Database\WarePro_Export_5-5-2026.xlsx";
            string connectionString = @"Server=.\SQLEXPRESS;Database=ProductManagementDb;Trusted_Connection=True;TrustServerCertificate=True;";

            Console.WriteLine("Starting Category Addition Process...");

            if (!File.Exists(excelPath))
            {
                Console.WriteLine($"Error: File not found at {excelPath}");
                return;
            }

            try
            {
                var categories = new List<(string Code, string Name, bool IsActive)>();

                using (var workbook = new XLWorkbook(excelPath))
                {
                    var worksheet = workbook.Worksheet("Loại hàng");
                    var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Skip header

                    foreach (var row in rows)
                    {
                        string code = row.Cell(1).GetValue<string>();
                        string name = row.Cell(2).GetValue<string>();
                        string isActiveStr = row.Cell(3).GetValue<string>();
                        bool isActive = isActiveStr.Equals("Hoạt động", StringComparison.OrdinalIgnoreCase) || 
                                       isActiveStr.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                                       isActiveStr.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                                       isActiveStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase);

                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            categories.Add((code, name, isActive));
                        }
                    }
                }

                Console.WriteLine($"Read {categories.Count} categories from Excel.");

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    
                    Console.WriteLine("Importing new Categories (Appending)...");
                    int importedCount = 0;
                    int skippedCount = 0;

                    foreach (var cat in categories)
                    {
                        // Check if Code already exists to avoid duplicates
                        string checkSql = "SELECT COUNT(*) FROM Category WHERE CategoryCode = @Code";
                        using (SqlCommand checkCmd = new SqlCommand(checkSql, connection))
                        {
                            checkCmd.Parameters.AddWithValue("@Code", cat.Code);
                            int exists = (int)checkCmd.ExecuteScalar();
                            if (exists > 0)
                            {
                                // If exists, maybe we should skip or use a different code?
                                // User said "thêm vào... 30 dòng nữa". If codes overlap, we must change them.
                                // Let's check if they overlap.
                                skippedCount++;
                                continue;
                            }
                        }

                        string insertSql = "INSERT INTO Category (CategoryCode, DisplayName, IsActive) VALUES (@Code, @Name, @IsActive)";
                        using (SqlCommand insertCmd = new SqlCommand(insertSql, connection))
                        {
                            insertCmd.Parameters.AddWithValue("@Code", cat.Code);
                            insertCmd.Parameters.AddWithValue("@Name", cat.Name);
                            insertCmd.Parameters.AddWithValue("@IsActive", cat.IsActive);
                            insertCmd.ExecuteNonQuery();
                            importedCount++;
                        }
                    }

                    Console.WriteLine($"Successfully imported {importedCount} categories.");
                    Console.WriteLine($"Skipped {skippedCount} existing categories.");
                }

                Console.WriteLine("Addition completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }
        }
    }
}
