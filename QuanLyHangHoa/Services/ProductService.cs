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

        public virtual List<Product> GetAllProducts(bool onlyActive = false)
        {
            using var db = _contextFactory();
            var query = db.Products.AsQueryable();
            if (onlyActive)
            {
                query = query.Where(p => p.IsActive);
            }
            return query
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Include(p => p.DefaultUnit)
                .Include(p => p.StockBalances)
                .ToList();
        }

        public virtual void AddProduct(Product p)
        {
            using var db = _contextFactory();
            db.Products.Add(p);
            db.SaveChanges();
        }

        public virtual void UpdateProduct(Product updated)
        {
            using var db = _contextFactory();
            var p = db.Products.Find(updated.Id);
            if (p == null) return;
            
            p.ProductCode = updated.ProductCode;
            p.DisplayName = updated.DisplayName;
            p.CategoryId = updated.CategoryId;
            p.BrandId = updated.BrandId;
            p.DefaultUnitId = updated.DefaultUnitId;
            p.DefaultPrice = updated.DefaultPrice;
            p.OriginCountry = updated.OriginCountry;
            p.WarrantyPeriodMonths = updated.WarrantyPeriodMonths;
            p.IsSerialTracked = updated.IsSerialTracked;
            p.IsActive = updated.IsActive;

            db.SaveChanges();
        }

        public virtual void DeactivateProduct(int id)
        {
            using var db = _contextFactory();
            var p = db.Products.Find(id);
            if (p == null) return;
            p.IsActive = false;
            db.SaveChanges();
        }

        public virtual void AddInitialStock(int productId, List<string> serialNumbers)
        {
            using var db = _contextFactory();
            var product = db.Products.Find(productId);
            if (product == null) return;

            var warehouseProvider = new DbDefaultWarehouseProvider(db);
            var service = new InventoryPostingService(
                new EfInventoryUnitOfWork(db),
                warehouseProvider,
                new SystemClock());

            service.PostStockIn(new PostStockInCommand(
                Guid.NewGuid(),
                warehouseProvider.GetDefaultWarehouseId(),
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
            public DateTime Now => DateTime.UtcNow;
        }
    }
}
