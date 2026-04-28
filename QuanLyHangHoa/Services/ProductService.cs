using System.Collections.Generic;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class ProductService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public ProductService()
            : this(() => new AppDbContext())
        {
        }

        public ProductService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public List<Product> GetAllProducts()
        {
            using var db = _contextFactory();
            return db.Products
                .Where(p => !p.IsDeleted)
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Include(p => p.Unit)
                .ToList();
        }

        public void AddProduct(Product p)
        {
            using var db = _contextFactory();
            db.Products.Add(p);
            db.SaveChanges();
        }

        public void UpdateProduct(Product updated)
        {
            using var db = _contextFactory();
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
            using var db = _contextFactory();
            var p = db.Products.Find(id);
            if (p == null) return;
            p.IsDeleted = true; // Soft delete
            db.SaveChanges();
        }

        public void AddInitialStock(int productId, List<string> serialNumbers)
        {
            using var db = _contextFactory();
            var product = db.Products.Find(productId);
            if (product == null) return;

            var service = new InventoryPostingService(
                new EfInventoryUnitOfWork(db),
                new DbDefaultWarehouseProvider(db),
                new SystemClock());

            service.PostStockIn(new PostStockInCommand(
                Guid.NewGuid(),
                StockInKind.OpeningBalance,
                StockDocumentStatus.Approved,
                productId,
                serialNumbers.Count,
                serialNumbers,
                PostedByUserId: 1));
        }

        private sealed class DbDefaultWarehouseProvider : IDefaultWarehouseProvider
        {
            private readonly AppDbContext _context;

            public DbDefaultWarehouseProvider(AppDbContext context)
            {
                _context = context;
            }

            public int GetDefaultWarehouseId()
            {
                var warehouseId = _context.Warehouses
                    .Where(warehouse => warehouse.IsDefault && warehouse.IsActive)
                    .Select(warehouse => warehouse.Id)
                    .FirstOrDefault();

                return warehouseId == 0 ? 1 : warehouseId;
            }
        }

        private sealed class SystemClock : IClock
        {
            public DateTime Now => DateTime.Now;
        }
    }
}
