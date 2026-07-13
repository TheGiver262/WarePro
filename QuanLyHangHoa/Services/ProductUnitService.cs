using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
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
                .Include(pu => pu.Unit)
                .Where(pu => pu.ProductId == productId)
                .ToList();

            if (includeDefault)
            {
                var product = db.Products.Include(p => p.DefaultUnit).FirstOrDefault(p => p.Id == productId);
                if (product != null && product.DefaultUnit != null)
                {
                    if (!list.Any(pu => pu.UnitId == product.DefaultUnitId))
                    {
                        list.Insert(0, new ProductUnit
                        {
                            ProductId = productId,
                            UnitId = product.DefaultUnitId,
                            Unit = product.DefaultUnit,
                            ConversionFactor = 1,
                            IsBaseUnit = true,
                            IsPurchaseUnit = true,
                            IsSalesUnit = true
                        });
                    }
                }
            }
            return list;
        }

        public virtual void Add(ProductUnit pu)
        {
            ValidateConversionFactor(pu.ConversionFactor);
            using var db = _contextFactory();
            db.ProductUnits.Add(pu);
            db.SaveChanges();
        }

        public virtual void Update(ProductUnit updated)
        {
            ValidateConversionFactor(updated.ConversionFactor);
            using var db = _contextFactory();
            var pu = db.ProductUnits.Find(updated.Id);
            if (pu == null) return;

            pu.UnitId = updated.UnitId;
            pu.ConversionFactor = updated.ConversionFactor;
            pu.IsBaseUnit = updated.IsBaseUnit;
            pu.IsPurchaseUnit = updated.IsPurchaseUnit;
            pu.IsSalesUnit = updated.IsSalesUnit;

            db.SaveChanges();
        }

        public virtual void Delete(int id)
        {
            using var db = _contextFactory();
            var pu = db.ProductUnits.Find(id);
            if (pu == null) return;
            db.ProductUnits.Remove(pu);
            db.SaveChanges();
        }

        public virtual decimal ConvertToBaseUnit(int productId, int sourceUnitId, decimal quantity)
        {
            using var db = _contextFactory();
            var units = db.ProductUnits.Where(pu => pu.ProductId == productId).ToList();
            var source = units.FirstOrDefault(u => u.UnitId == sourceUnitId);
            
            if (source == null) return quantity;
            return quantity * source.ConversionFactor;
        }

        public virtual decimal ConvertFromBaseUnit(int productId, int targetUnitId, decimal quantity)
        {
            using var db = _contextFactory();
            var units = db.ProductUnits.Where(pu => pu.ProductId == productId).ToList();
            var target = units.FirstOrDefault(u => u.UnitId == targetUnitId);
            
            if (target == null || target.ConversionFactor == 0) return quantity;
            return quantity / target.ConversionFactor;
        }

        private static void ValidateConversionFactor(decimal conversionFactor)
        {
            if (conversionFactor <= 0)
            {
                throw new InvalidOperationException("Conversion factor must be greater than zero.");
            }
        }
    }
}
