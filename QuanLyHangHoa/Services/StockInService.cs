using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockInService
    {
        public List<StockIn> GetAll()
        {
            using var db = new AppDbContext();
            return db.StockIns
                .Where(s => !s.IsDeleted)
                .Include(s => s.Supplier)
                .Include(s => s.Employee)
                .Include(s => s.StockInDetails)
                    .ThenInclude(d => d.Product)
                .Include(s => s.StockInDetails)
                    .ThenInclude(d => d.ProductSerials)
                .OrderByDescending(s => s.ImportDate)
                .ToList();
        }

        /// <summary>
        /// Save a new StockIn with its details and generated serial numbers.
        /// </summary>
        public void Create(StockIn stockIn)
        {
            using var db = new AppDbContext();
            // Recalculate total
            stockIn.TotalAmount = stockIn.StockInDetails.Sum(d => d.Quantity * d.ImportPrice);

            // Update product quantity for each line
            foreach (var detail in stockIn.StockInDetails)
            {
                var product = db.Products.Find(detail.ProductId);
                if (product != null)
                    product.Quantity += detail.Quantity;
            }

            db.StockIns.Add(stockIn);
            db.SaveChanges();
        }

        public void SoftDelete(int id)
        {
            using var db = new AppDbContext();
            var s = db.StockIns.Find(id);
            if (s == null) return;
            s.IsDeleted = true;
            db.SaveChanges();
        }

        /// <summary>
        /// Parse a serial range string like "EM90126000-EM90126030" into individual serial numbers.
        /// Also supports comma-separated and newline-separated individual serials.
        /// </summary>
        public static List<string> ParseSerialRange(string input)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(input)) return result;

            // Split on commas or newlines first
            var parts = input.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                // Check for dash range: PREFIX+DIGITS - PREFIX+DIGITS  or DIGITS-DIGITS
                var rangeMatch = Regex.Match(trimmed, @"^([A-Za-z]*)(\d+)-[A-Za-z]*(\d+)$");
                if (rangeMatch.Success)
                {
                    string prefix = rangeMatch.Groups[1].Value;
                    string startStr = rangeMatch.Groups[2].Value;
                    string endStr   = rangeMatch.Groups[3].Value;
                    if (long.TryParse(startStr, out long start) && long.TryParse(endStr, out long end) && end >= start)
                    {
                        int padLen = startStr.Length; // preserve leading zeros
                        for (long i = start; i <= end; i++)
                            result.Add(prefix + i.ToString().PadLeft(padLen, '0'));
                    }
                }
                else
                {
                    result.Add(trimmed);
                }
            }
            return result;
        }
    }
}
