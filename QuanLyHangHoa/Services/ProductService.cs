using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class ProductService
    {
        public List<Product> GetAllProducts()
        {
            using var db = new AppDbContext();
            return db.Products
                .Where(p => !p.IsDeleted)
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Include(p => p.Unit)
                .ToList();
        }

        public void AddProduct(Product p)
        {
            using var db = new AppDbContext();
            db.Products.Add(p);
            db.SaveChanges();
        }

        public void UpdateProduct(Product updated)
        {
            using var db = new AppDbContext();
            var p = db.Products.Find(updated.Id);
            if (p == null) return;
            p.Name          = updated.Name;
            p.CategoryId    = updated.CategoryId;
            p.BrandId       = updated.BrandId;
            p.UnitId        = updated.UnitId;
            p.Quantity      = updated.Quantity;
            p.UnitPrice     = updated.UnitPrice;
            p.Origin        = updated.Origin;
            p.WarrantyMonths = updated.WarrantyMonths;
            p.Notes         = updated.Notes;
            db.SaveChanges();
        }

        public void DeleteProduct(int id)
        {
            using var db = new AppDbContext();
            var p = db.Products.Find(id);
            if (p == null) return;
            p.IsDeleted = true; // Soft delete
            db.SaveChanges();
        }

        public void AddInitialStock(int productId, List<string> serialNumbers)
        {
            using var db = new AppDbContext();
            var product = db.Products.Find(productId);
            if (product == null) return;

            foreach (var sn in serialNumbers)
            {
                // Check if serial already exists
                if (db.ProductSerials.Any(ps => ps.SerialNumber == sn)) continue;

                db.ProductSerials.Add(new ProductSerial
                {
                    ProductId = productId,
                    SerialNumber = sn,
                    Status = "InStock"
                });
            }

            product.Quantity += serialNumbers.Count;
            db.SaveChanges();
        }
    }
}
