using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockOutService
    {
        public List<StockOut> GetAll()
        {
            using var db = new AppDbContext();
            return db.StockOuts
                .Where(s => !s.IsDeleted)
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.StockOutDetails)
                    .ThenInclude(d => d.Product)
                .Include(s => s.StockOutDetails)
                    .ThenInclude(d => d.ProductSerials)
                .OrderByDescending(s => s.ExportDate)
                .ToList();
        }

        public void Create(StockOut stockOut)
        {
            using var db = new AppDbContext();
            stockOut.TotalAmount = stockOut.StockOutDetails.Sum(d => d.Quantity * d.ExportPrice);

            foreach (var detail in stockOut.StockOutDetails)
            {
                var product = db.Products.Find(detail.ProductId);
                if (product != null)
                    product.Quantity -= detail.Quantity;

                // Mark each serial as Sold
                foreach (var serial in detail.ProductSerials)
                {
                    var ps = db.ProductSerials.FirstOrDefault(x => x.SerialNumber == serial.SerialNumber);
                    if (ps != null) ps.Status = "Sold";
                }
            }

            db.StockOuts.Add(stockOut);
            db.SaveChanges();
        }

        public void SoftDelete(int id)
        {
            using var db = new AppDbContext();
            var s = db.StockOuts.Find(id);
            if (s == null) return;
            s.IsDeleted = true;
            db.SaveChanges();
        }

        /// <summary>Get InStock serials for a given productId (for selection when creating StockOut)</summary>
        public List<ProductSerial> GetInStockSerials(int productId)
        {
            using var db = new AppDbContext();
            return db.ProductSerials
                .Where(ps => ps.ProductId == productId && ps.Status == "InStock" && !ps.IsDeleted)
                .ToList();
        }
    }
}
