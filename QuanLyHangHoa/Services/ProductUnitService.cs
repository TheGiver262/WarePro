using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    /// <summary>
    /// quản lý hệ số quy đổi; mỗi hệ số là số đơn vị gốc trên một đơn vị được chọn.
    /// </summary>
    public class ProductUnitService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor? _writeExecutor;

        public ProductUnitService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _writeExecutor = contextFactory is null ? null : new DatabaseWriteExecutor(contextFactory);
        }

        public virtual List<ProductUnit> GetByProductId(int productId, bool includeDefault = false)
        {
            using var db = _contextFactory();
            var list = db.ProductUnits
                .AsNoTracking()
                .Include(productUnit => productUnit.Product)
                .Include(productUnit => productUnit.Unit)
                .Where(productUnit => productUnit.ProductId == productId)
                .ToList();

            // dựng mapping factor 1 tạm cho dữ liệu cũ chưa có ProductUnit của DefaultUnit.
            if (includeDefault)
            {
                var product = db.Products
                    .AsNoTracking()
                    .Include(item => item.DefaultUnit)
                    .FirstOrDefault(item => item.Id == productId);
                if (product?.DefaultUnit != null
                    && !list.Any(productUnit => productUnit.UnitId == product.DefaultUnitId))
                {
                    list.Insert(0, new ProductUnit
                    {
                        ProductId = productId,
                        UnitId = product.DefaultUnitId,
                        Product = product,
                        Unit = product.DefaultUnit,
                        ConversionFactor = 1,
                        IsBaseUnit = true,
                        IsPurchaseUnit = true,
                        IsSalesUnit = true
                    });
                }
            }

            return list;
        }

        // các mutation dùng Serializable cùng unique index để chống base unit/mapping trùng do cạnh tranh.
        public virtual Task AddAsync(
            ProductUnit productUnit, int actorId, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ValidateConversionFactor(productUnit.ConversionFactor);
            // chụp scalar trước khi executor retry để lần thử sau không đọc lại model đầu vào đã thay đổi
            var productId = productUnit.ProductId;
            var unitId = productUnit.UnitId;
            var factor = productUnit.ConversionFactor;
            var isBase = productUnit.IsBaseUnit;
            var isPurchase = productUnit.IsPurchaseUnit;
            var isSales = productUnit.IsSalesUnit;

            return WriteExecutor.ExecuteAsync(
                new DatabaseWriteRequest("product-unit.add", operationId, IsolationLevel.Serializable),
                (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, actorId, PermissionAction.ManageMasterData);
                    db.ProductUnits.Add(new ProductUnit
                    {
                        ProductId = productId,
                        UnitId = unitId,
                        ConversionFactor = factor,
                        IsBaseUnit = isBase,
                        IsPurchaseUnit = isPurchase,
                        IsSalesUnit = isSales
                    });
                    return Task.CompletedTask;
                },
                (db, token) => db.ProductUnits.AnyAsync(item =>
                    item.ProductId == productId && item.UnitId == unitId &&
                    item.ConversionFactor == factor && item.IsBaseUnit == isBase &&
                    item.IsPurchaseUnit == isPurchase && item.IsSalesUnit == isSales,
                    token),
                cancellationToken: cancellationToken);
        }

        public virtual Task UpdateAsync(
            int id, ProductUnit updated, byte[] expectedRowVersion, int actorId,
            Guid operationId, CancellationToken cancellationToken = default)
        {
            ValidateConversionFactor(updated.ConversionFactor);
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();
            var unitId = updated.UnitId;
            var factor = updated.ConversionFactor;
            var isBase = updated.IsBaseUnit;
            var isPurchase = updated.IsPurchaseUnit;
            var isSales = updated.IsSalesUnit;

            return WriteExecutor.ExecuteAsync(
                new DatabaseWriteRequest("product-unit.update", operationId, IsolationLevel.Serializable),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, actorId, PermissionAction.ManageMasterData);
                    var entity = await db.ProductUnits.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null)
                        throw new InventoryDomainException("Dữ liệu đã bị xóa hoặc không còn tồn tại. Vui lòng tải lại dữ liệu.");
                    // rowversion chặn ghi đè; callback sau đó chỉ nhận commit thành công khi toàn bộ mapping đúng và token đã đổi
                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    entity.UnitId = unitId;
                    entity.ConversionFactor = factor;
                    entity.IsBaseUnit = isBase;
                    entity.IsPurchaseUnit = isPurchase;
                    entity.IsSalesUnit = isSales;
                },
                (db, token) => db.ProductUnits.AnyAsync(item => item.Id == id &&
                    item.UnitId == unitId && item.ConversionFactor == factor &&
                    item.IsBaseUnit == isBase && item.IsPurchaseUnit == isPurchase &&
                    item.IsSalesUnit == isSales && item.RowVersion != rowVersion,
                    token),
                cancellationToken: cancellationToken);
        }

        public virtual Task DeleteAsync(
            int id, byte[] expectedRowVersion, int actorId, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();

            return WriteExecutor.ExecuteAsync(
                new DatabaseWriteRequest("product-unit.delete", operationId, IsolationLevel.Serializable),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, actorId, PermissionAction.ManageMasterData);
                    var entity = await db.ProductUnits.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null) return;
                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    db.ProductUnits.Remove(entity);
                },
                (db, token) => db.ProductUnits.AllAsync(item => item.Id != id, token),
                cancellationToken: cancellationToken);
        }

        private DatabaseWriteExecutor WriteExecutor =>
            _writeExecutor ?? throw new InvalidOperationException("A database context factory is required for mutations.");
        // sang đơn vị gốc dùng phép nhân; thiếu mapping được coi là factor 1 để tương thích dữ liệu cũ.
        public virtual decimal ConvertToBaseUnit(int productId, int sourceUnitId, decimal quantity)
        {
            using var db = _contextFactory();
            var units = db.ProductUnits.Where(productUnit => productUnit.ProductId == productId).ToList();
            var source = units.FirstOrDefault(unit => unit.UnitId == sourceUnitId);

            return source == null ? quantity : quantity * source.ConversionFactor;
        }

        // từ đơn vị gốc dùng phép chia; factor 0 bị chặn cả ở service và check constraint database.
        public virtual decimal ConvertFromBaseUnit(int productId, int targetUnitId, decimal quantity)
        {
            using var db = _contextFactory();
            var units = db.ProductUnits.Where(productUnit => productUnit.ProductId == productId).ToList();
            var target = units.FirstOrDefault(unit => unit.UnitId == targetUnitId);

            return target == null || target.ConversionFactor == 0
                ? quantity
                : quantity / target.ConversionFactor;
        }

        private static void ValidateConversionFactor(decimal conversionFactor)
        {
            if (conversionFactor <= 0)
                throw new InvalidOperationException("Conversion factor must be greater than zero.");
        }
    }
}
