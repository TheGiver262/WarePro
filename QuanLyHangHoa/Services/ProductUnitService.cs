using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    /// <summary>
    /// quản lý hệ số quy đổi; mỗi hệ số là số đơn vị gốc trên một đơn vị được chọn.
    /// </summary>
    public class ProductUnitService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public ProductUnitService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
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
        public virtual void Add(ProductUnit productUnit, int actorId)
        {
            ValidateConversionFactor(productUnit.ConversionFactor);
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
            AuthorizationService.RequireFreshActor(
                db,
                actorId,
                PermissionAction.ManageMasterData);
            db.ProductUnits.Add(productUnit);
            db.SaveChanges();
            transaction.Commit();
        }

        public virtual void Update(ProductUnit updated, int actorId)
        {
            ValidateConversionFactor(updated.ConversionFactor);
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
            AuthorizationService.RequireFreshActor(
                db,
                actorId,
                PermissionAction.ManageMasterData);
            var productUnit = db.ProductUnits.Find(updated.Id);
            if (productUnit == null)
                return;

            productUnit.UnitId = updated.UnitId;
            productUnit.ConversionFactor = updated.ConversionFactor;
            productUnit.IsBaseUnit = updated.IsBaseUnit;
            productUnit.IsPurchaseUnit = updated.IsPurchaseUnit;
            productUnit.IsSalesUnit = updated.IsSalesUnit;
            db.SaveChanges();
            transaction.Commit();
        }

        public virtual void Delete(int id, int actorId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
            AuthorizationService.RequireFreshActor(
                db,
                actorId,
                PermissionAction.ManageMasterData);
            var productUnit = db.ProductUnits.Find(id);
            if (productUnit == null)
                return;

            db.ProductUnits.Remove(productUnit);
            db.SaveChanges();
            transaction.Commit();
        }

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
