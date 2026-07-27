using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace ExcelAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            string excelPath = args.Length > 0
                ? args[0]
                : Path.Combine(AppContext.BaseDirectory, "warepro_database_seed.xlsx");
            
            if (!File.Exists(excelPath))
            {
                Console.WriteLine("File not found!");
                return;
            }

            using (var workbook = new XLWorkbook(excelPath))
            {
                Console.WriteLine($"Workbook: {Path.GetFileName(excelPath)}");
                foreach (var sheet in workbook.Worksheets)
                {
                    Console.WriteLine($"--- Sheet: {sheet.Name} ---");
                    var firstRow = sheet.Row(1);
                    var lastCell = sheet.LastCellUsed();
                    if (lastCell != null)
                    {
                        for (int i = 1; i <= lastCell.Address.ColumnNumber; i++)
                        {
                            Console.Write($"[{sheet.Cell(1, i).GetValue<string>()}] ");
                        }
                        Console.WriteLine();
                        Console.WriteLine($"Rows: {sheet.LastRowUsed().RowNumber()}");
                    }
                    Console.WriteLine();
                }
            }
        }
    }
}
