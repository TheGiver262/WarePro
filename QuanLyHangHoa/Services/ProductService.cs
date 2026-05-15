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
            return query.AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Include(p => p.DefaultUnit)
                .Include(p => p.StockBalances)
                .ToList();
        }

        public virtual void AddProduct(Product p, int userId)
        {
            using var db = _contextFactory();
            db.Products.Add(p);
            db.SaveChanges();

            AddAudit(db, "Product", p.Id, "CREATE", userId, null, new { p.ProductCode, p.DisplayName, p.IsActive });
            db.SaveChanges();
        }

        public virtual void UpdateProduct(Product updated, int userId)
        {
            using var db = _contextFactory();
            var p = db.Products.Find(updated.Id);
            if (p == null) return;
            
            var oldState = new { p.ProductCode, p.DisplayName, p.IsActive };

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

            AddAudit(db, "Product", p.Id, "UPDATE", userId, oldState, new { p.ProductCode, p.DisplayName, p.IsActive });
            db.SaveChanges();
        }

        public virtual void DeactivateProduct(int id, int userId)
        {
            using var db = _contextFactory();
            var p = db.Products.Find(id);
            if (p == null) return;
            p.IsActive = false;
            db.SaveChanges();

            AddAudit(db, "Product", id, "DEACTIVATE", userId, new { Status = "Active" }, new { Status = "Inactive" });
            db.SaveChanges();
        }

        public virtual bool HasTransactionHistory(int id)
        {
            using var db = _contextFactory();
            return db.PurchaseInvoiceLines.Any(l => l.ProductId == id) ||
                   db.SalesInvoiceLines.Any(l => l.ProductId == id) ||
                   db.StockInLines.Any(l => l.ProductId == id) ||
                   db.StockOutLines.Any(l => l.ProductId == id) ||
                   db.StockAdjustmentLines.Any(l => l.ProductId == id) ||
                   db.StockCountLines.Any(l => l.ProductId == id) ||
                   db.StockLedgers.Any(l => l.ProductId == id);
        }

        public virtual void DeleteProduct(int id, int userId)
        {
            using var db = _contextFactory();
            var p = db.Products.Find(id);
            if (p == null) return;

            var oldState = new { p.ProductCode, p.DisplayName };
            db.Products.Remove(p);
            db.SaveChanges();

            AddAudit(db, "Product", id, "DELETE", userId, oldState, null);
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
                0,
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

        private void AddAudit(AppDbContext db, string entityName, int entityId, string action, int userId, object? oldValues = null, object? newValues = null)
        {
            var log = new AuditLog
            {
                EntityName = entityName,
                EntityId = entityId,
                ActionCode = action,
                PerformedBy = userId,
                PerformedAt = DateTime.Now,
                BeforeJson = oldValues != null ? System.Text.Json.JsonSerializer.Serialize(oldValues) : null,
                AfterJson = newValues != null ? System.Text.Json.JsonSerializer.Serialize(newValues) : null
            };
            db.AuditLogs.Add(log);
        }

        private sealed class SystemClock : IClock
        {
            public DateTime Now => DateTime.Now;
        }
    }
}
