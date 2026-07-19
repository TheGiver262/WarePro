using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class ProductService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor? _writeExecutor;

        public ProductService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _writeExecutor = contextFactory is null ? null : new DatabaseWriteExecutor(contextFactory);
        }

        // Include nạp đủ dữ liệu hiển thị và số dư trong một lần; AsNoTracking vì kết quả không sửa trực tiếp
        private DatabaseWriteExecutor WriteExecutor =>
            _writeExecutor ?? throw new InvalidOperationException("A database context factory is required for mutations.");

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

        // query phân trang và query đếm dùng chung ApplyProductFilters để tổng số luôn khớp danh sách
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

        // chỉ nối điều kiện khi người dùng nhập bộ lọc; IQueryable giữ mọi phép lọc ở phía database
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

        // cần SaveChanges lần đầu để lấy Product.Id rồi mới tạo audit tham chiếu đúng id
        public virtual Task<int> AddProductAsync(
            Product product, int userId, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var code = product.ProductCode.Trim();
            var name = product.DisplayName.Trim();
            var description = product.Description?.Trim();
            var costPrice = product.CostPrice;
            var categoryId = product.CategoryId;
            var brandId = product.BrandId;
            var defaultUnitId = product.DefaultUnitId;
            var defaultPrice = product.DefaultPrice;
            var country = product.OriginCountry?.Trim();
            var warrantyMonths = product.WarrantyPeriodMonths;
            var serialTracked = product.IsSerialTracked;
            var isActive = product.IsActive;

            return WriteExecutor.ExecuteAsync(
                new DatabaseWriteRequest("product.add", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, userId, PermissionAction.ManageMasterData);
                    if (await db.Products.AnyAsync(item => item.ProductCode == code, token))
                    {
                        throw new InvalidOperationException($"Product code '{code}' already exists.");
                    }

                    var created = new Product
                    {
                        ProductCode = code,
                        DisplayName = name,
                        Description = description,
                        CostPrice = costPrice,
                        CategoryId = categoryId,
                        BrandId = brandId,
                        DefaultUnitId = defaultUnitId,
                        DefaultPrice = defaultPrice,
                        OriginCountry = country,
                        WarrantyPeriodMonths = warrantyMonths,
                        IsSerialTracked = serialTracked,
                        IsActive = isActive
                    };
                    db.Products.Add(created);
                    await db.SaveChangesAsync(token);
                    AddAudit(db, "Product", created.Id, "CREATE", userId, null,
                        new { created.ProductCode, created.DisplayName, created.IsActive });
                    return created.Id;
                },
                (db, token) => db.Products.AnyAsync(item =>
                    item.ProductCode == code && item.DisplayName == name &&
                    item.CategoryId == categoryId && item.BrandId == brandId &&
                    item.DefaultUnitId == defaultUnitId && item.DefaultPrice == defaultPrice &&
                    item.IsActive == isActive, token),
                cancellationToken: cancellationToken);
        }
        public virtual Task UpdateProductAsync(
            int id, Product updated, byte[] expectedRowVersion, int userId,
            Guid operationId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();
            var code = updated.ProductCode.Trim();
            var name = updated.DisplayName.Trim();
            var categoryId = updated.CategoryId;
            var brandId = updated.BrandId;
            var unitId = updated.DefaultUnitId;
            var price = updated.DefaultPrice;
            var country = updated.OriginCountry?.Trim();
            var warranty = updated.WarrantyPeriodMonths;
            var serialTracked = updated.IsSerialTracked;
            var isActive = updated.IsActive;

            return WriteExecutor.ExecuteAsync(
                new DatabaseWriteRequest("product.update", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, userId, PermissionAction.ManageMasterData);
                    var entity = await db.Products.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null) return;
                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    var before = new { entity.ProductCode, entity.DisplayName, entity.IsActive };
                    entity.ProductCode = code;
                    entity.DisplayName = name;
                    entity.CategoryId = categoryId;
                    entity.BrandId = brandId;
                    entity.DefaultUnitId = unitId;
                    entity.DefaultPrice = price;
                    entity.OriginCountry = country;
                    entity.WarrantyPeriodMonths = warranty;
                    entity.IsSerialTracked = serialTracked;
                    entity.IsActive = isActive;
                    AddAudit(db, "Product", id, "UPDATE", userId, before,
                        new { entity.ProductCode, entity.DisplayName, entity.IsActive });
                },
                (db, token) => db.Products.AnyAsync(item => item.Id == id &&
                    item.ProductCode == code && item.DisplayName == name &&
                    item.CategoryId == categoryId && item.BrandId == brandId &&
                    item.DefaultUnitId == unitId && item.DefaultPrice == price &&
                    item.OriginCountry == country && item.WarrantyPeriodMonths == warranty &&
                    item.IsSerialTracked == serialTracked && item.IsActive == isActive &&
                    item.RowVersion != rowVersion, token),
                cancellationToken: cancellationToken);
        }
        public virtual Task DeactivateProductAsync(
            int id, byte[] expectedRowVersion, int userId, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();

            return WriteExecutor.ExecuteAsync(
                new DatabaseWriteRequest("product.deactivate", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, userId, PermissionAction.ManageMasterData);
                    var entity = await db.Products.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null) return;
                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    entity.IsActive = false;
                    AddAudit(db, "Product", id, "DEACTIVATE", userId,
                        new { Status = "Active" }, new { Status = "Inactive" });
                },
                (db, token) => db.Products.AnyAsync(item =>
                    item.Id == id && !item.IsActive && item.RowVersion != rowVersion, token),
                cancellationToken: cancellationToken);
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

        // liệt kê đủ bảng nghiệp vụ có ProductId; tên bảng và số dòng còn được dùng để giải thích lý do không xóa cứng
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

        public virtual Task DeleteProductAsync(
            int id, byte[] expectedRowVersion, int userId, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();

            return WriteExecutor.ExecuteAsync(
                new DatabaseWriteRequest("product.delete", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, userId, PermissionAction.ManageMasterData);
                    var entity = await db.Products.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null) return;
                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    var before = new { entity.ProductCode, entity.DisplayName };
                    if (await HasDependenciesAsync(db, id, token))
                    {
                        entity.IsActive = false;
                        AddAudit(db, "Product", id, "DEACTIVATE", userId, before,
                            new { entity.ProductCode, entity.DisplayName, entity.IsActive });
                    }
                    else
                    {
                        db.Products.Remove(entity);
                        AddAudit(db, "Product", id, "DELETE", userId, before, null);
                    }
                },
                (db, token) => db.Products.AllAsync(item => item.Id != id || !item.IsActive, token),
                cancellationToken: cancellationToken);
        }

        private static async Task<bool> HasDependenciesAsync(
            AppDbContext db, int productId, CancellationToken token) =>
            await db.ProductUnits.AnyAsync(item => item.ProductId == productId, token) ||
            await db.ProductSerials.AnyAsync(item => item.ProductId == productId, token) ||
            await db.StockBalances.AnyAsync(item => item.ProductId == productId, token) ||
            await db.StockTransferLines.AnyAsync(item => item.ProductId == productId, token) ||
            await db.PurchaseInvoiceLines.AnyAsync(item => item.ProductId == productId, token) ||
            await db.SalesInvoiceLines.AnyAsync(item => item.ProductId == productId, token) ||
            await db.StockInLines.AnyAsync(item => item.ProductId == productId, token) ||
            await db.StockOutLines.AnyAsync(item => item.ProductId == productId, token) ||
            await db.StockAdjustmentLines.AnyAsync(item => item.ProductId == productId, token) ||
            await db.StockCountLines.AnyAsync(item => item.ProductId == productId, token) ||
            await db.StockLedgers.AnyAsync(item => item.ProductId == productId, token);
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

        // rawData chỉ lấy hai giá trị cần tính; CostPrice được ưu tiên, thiếu giá vốn mới dùng giá bán mặc định
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

            // ngưỡng 10 phải giống bộ lọc sắp hết để thẻ thống kê và danh sách không lệch nhau
            int lowStock = rawData.Count(x => x.OnHandQuantity < 10);
            decimal totalVal = rawData.Sum(x => x.OnHandQuantity * x.Price);

            return (lowStock, totalVal);
        }

        // tổng tồn dùng nullable decimal để sản phẩm chưa có StockBalance vẫn được xem là tồn 0
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

        // chỉ thêm log vào context; method gọi quyết định thời điểm SaveChanges và transaction
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
