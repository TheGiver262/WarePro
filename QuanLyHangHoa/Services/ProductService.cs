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

        public virtual List<Product> GetProductsPaged(
            string searchCode,
            string searchName,
            string searchStatus,
            string searchSerial,
            int? categoryId,
            int? brandId,
            decimal? priceMin,
            decimal? priceMax,
            int? warranty,
            int skip,
            int take)
        {
            using var db = _contextFactory();
            var query = db.Products.AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Include(p => p.DefaultUnit)
                .Include(p => p.StockBalances)
                .AsQueryable();

            query = ApplyProductFilters(query, searchCode, searchName, searchStatus, searchSerial, categoryId, brandId, priceMin, priceMax, warranty);

            return query
                .OrderByDescending(p => p.Id)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public virtual int GetProductsCount(
            string searchCode,
            string searchName,
            string searchStatus,
            string searchSerial,
            int? categoryId,
            int? brandId,
            decimal? priceMin,
            decimal? priceMax,
            int? warranty)
        {
            using var db = _contextFactory();
            var query = db.Products.AsNoTracking().AsQueryable();
            query = ApplyProductFilters(query, searchCode, searchName, searchStatus, searchSerial, categoryId, brandId, priceMin, priceMax, warranty);
            return query.Count();
        }

        private IQueryable<Product> ApplyProductFilters(
            IQueryable<Product> query,
            string searchCode,
            string searchName,
            string searchStatus,
            string searchSerial,
            int? categoryId,
            int? brandId,
            decimal? priceMin,
            decimal? priceMax,
            int? warranty)
        {
            if (!string.IsNullOrWhiteSpace(searchCode))
            {
                var term = searchCode.Trim();
                query = query.Where(p => p.ProductCode.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                var term = searchName.Trim();
                query = query.Where(p => p.DisplayName.Contains(term));
            }

            if (searchStatus != "Tất cả" && !string.IsNullOrEmpty(searchStatus))
            {
                bool active = searchStatus == "Hoạt động";
                query = query.Where(p => p.IsActive == active);
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (brandId.HasValue && brandId.Value > 0)
            {
                query = query.Where(p => p.BrandId == brandId.Value);
            }

            if (priceMin.HasValue)
            {
                query = query.Where(p => p.DefaultPrice >= priceMin.Value);
            }

            if (priceMax.HasValue)
            {
                query = query.Where(p => p.DefaultPrice <= priceMax.Value);
            }

            if (searchSerial != "Tất cả" && !string.IsNullOrEmpty(searchSerial))
            {
                bool tracked = searchSerial == "Có serial";
                query = query.Where(p => p.IsSerialTracked == tracked);
            }

            if (warranty.HasValue)
            {
                query = query.Where(p => p.WarrantyPeriodMonths == warranty.Value);
            }

            return query;
        }

        public virtual void AddProduct(Product p, int userId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            db.Products.Add(p);
            db.SaveChanges();

            AddAudit(db, "Product", p.Id, "CREATE", userId, null, new { p.ProductCode, p.DisplayName, p.IsActive });
            db.SaveChanges();
            transaction.Commit();
        }

        public virtual void UpdateProduct(Product updated, int userId)
        {
            using var db = _contextFactory();
            var p = db.Products.Find(updated.Id);
            if (p == null) return;
            using var transaction = db.Database.BeginTransaction();
            
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
            transaction.Commit();
        }

        public virtual void DeactivateProduct(int id, int userId)
        {
            using var db = _contextFactory();
            var p = db.Products.Find(id);
            if (p == null) return;
            using var transaction = db.Database.BeginTransaction();
            p.IsActive = false;
            db.SaveChanges();

            AddAudit(db, "Product", id, "DEACTIVATE", userId, new { Status = "Active" }, new { Status = "Inactive" });
            db.SaveChanges();
            transaction.Commit();
        }

        public virtual bool HasTransactionHistory(int id)
        {
            return GetDependencies(id).Any(dependency => dependency.Count > 0);
        }

        public virtual IReadOnlyList<(string Name, int Count)> GetDependencies(int productId)
        {
            using var db = _contextFactory();
            return GetDependencies(db, productId);
        }

        private static IReadOnlyList<(string Name, int Count)> GetDependencies(
            AppDbContext db,
            int productId)
        {
            return new List<(string Name, int Count)>
            {
                ("ProductUnit", db.ProductUnits.Count(row => row.ProductId == productId)),
                ("ProductSerial", db.ProductSerials.Count(row => row.ProductId == productId)),
                ("StockBalance", db.StockBalances.Count(row => row.ProductId == productId)),
                ("StockTransferLine", db.StockTransferLines.Count(row => row.ProductId == productId)),
                ("PurchaseInvoiceLine", db.PurchaseInvoiceLines.Count(row => row.ProductId == productId)),
                ("SalesInvoiceLine", db.SalesInvoiceLines.Count(row => row.ProductId == productId)),
                ("StockInLine", db.StockInLines.Count(row => row.ProductId == productId)),
                ("StockOutLine", db.StockOutLines.Count(row => row.ProductId == productId)),
                ("StockAdjustmentLine", db.StockAdjustmentLines.Count(row => row.ProductId == productId)),
                ("StockCountLine", db.StockCountLines.Count(row => row.ProductId == productId)),
                ("StockLedger", db.StockLedgers.Count(row => row.ProductId == productId))
            };
        }

        public virtual void DeleteProduct(int id, int userId)
        {
            using var db = _contextFactory();
            var p = db.Products.Find(id);
            if (p == null) return;
            using var transaction = db.Database.BeginTransaction();

            var oldState = new { p.ProductCode, p.DisplayName };
            var hasDependencies = GetDependencies(db, id).Any(dependency => dependency.Count > 0);

            if (hasDependencies)
            {
                p.IsActive = false;
                db.SaveChanges();
                AddAudit(db, "Product", id, "DEACTIVATE", userId, oldState, new { p.ProductCode, p.DisplayName, p.IsActive });
            }
            else
            {
                db.Products.Remove(p);
                db.SaveChanges();
                AddAudit(db, "Product", id, "DELETE", userId, oldState, null);
            }

            db.SaveChanges();
            transaction.Commit();
        }

        public virtual void AddInitialStock(int productId, List<string> serialNumbers, int userId)
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
                PostedByUserId: userId));
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

        public virtual List<Product> GetInventoryProductsPaged(
            string searchCode,
            string searchName,
            int? categoryId,
            string searchStatus,
            int skip,
            int take)
        {
            using var db = _contextFactory();
            var query = db.Products.AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.DefaultUnit)
                .Include(p => p.StockBalances)
                .AsQueryable();

            query = ApplyInventoryFilters(query, searchCode, searchName, categoryId, searchStatus);

            return query
                .OrderByDescending(p => p.Id)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public virtual int GetInventoryProductsCount(
            string searchCode,
            string searchName,
            int? categoryId,
            string searchStatus)
        {
            using var db = _contextFactory();
            var query = db.Products.AsNoTracking().AsQueryable();
            query = ApplyInventoryFilters(query, searchCode, searchName, categoryId, searchStatus);
            return query.Count();
        }

        public virtual (int lowStockCount, decimal totalValue) GetInventoryStats(
            string searchCode,
            string searchName,
            int? categoryId,
            string searchStatus)
        {
            using var db = _contextFactory();
            var query = db.Products.AsNoTracking().AsQueryable();
            query = ApplyInventoryFilters(query, searchCode, searchName, categoryId, searchStatus);

            var rawData = query
                .Select(p => new
                {
                    OnHandQuantity = p.StockBalances.Sum(b => (decimal?)b.OnHandQuantity) ?? 0m,
                    Price = p.CostPrice ?? p.DefaultPrice
                })
                .ToList();

            int lowStock = rawData.Count(x => x.OnHandQuantity < 10);
            decimal totalVal = rawData.Sum(x => x.OnHandQuantity * x.Price);

            return (lowStock, totalVal);
        }

        private IQueryable<Product> ApplyInventoryFilters(
            IQueryable<Product> query,
            string searchCode,
            string searchName,
            int? categoryId,
            string searchStatus)
        {
            if (!string.IsNullOrWhiteSpace(searchCode))
            {
                var term = searchCode.Trim();
                query = query.Where(p => p.ProductCode.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                var term = searchName.Trim();
                query = query.Where(p => p.DisplayName.Contains(term));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrEmpty(searchStatus) && searchStatus != "Tất cả")
            {
                if (searchStatus == "Còn hàng")
                {
                    query = query.Where(p => p.StockBalances.Sum(b => (decimal?)b.OnHandQuantity) > 0);
                }
                else if (searchStatus == "Sắp hết")
                {
                    query = query.Where(p => p.StockBalances.Sum(b => (decimal?)b.OnHandQuantity) < 10);
                }
                else if (searchStatus == "Hết hàng")
                {
                    query = query.Where(p => p.StockBalances.Sum(b => (decimal?)b.OnHandQuantity) <= 0);
                }
            }

            return query;
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
