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

        public ProductUnitService()
            : this(() => new AppDbContext())
        {
        }

        public ProductUnitService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public List<ProductUnit> GetProductUnits(int productId)
        {
            using var db = _contextFactory();
            return db.ProductUnits
                .Include(productUnit => productUnit.Product)
                .Include(productUnit => productUnit.Unit)
                .Where(productUnit => productUnit.ProductId == productId && !productUnit.IsDeleted)
                .OrderByDescending(productUnit => productUnit.IsBaseUnit)
                .ThenBy(productUnit => productUnit.Unit!.Name)
                .ToList();
        }

        public void AddProductUnit(ProductUnit productUnit)
        {
            Validate(productUnit);

            using var db = _contextFactory();
            if (!db.Products.Any(product => product.Id == productUnit.ProductId && !product.IsDeleted))
            {
                throw new InvalidOperationException($"Product {productUnit.ProductId} does not exist.");
            }

            if (!db.Units.Any(unit => unit.Id == productUnit.UnitId && !unit.IsDeleted))
            {
                throw new InvalidOperationException($"Unit {productUnit.UnitId} does not exist.");
            }

            if (db.ProductUnits.Any(existing =>
                    existing.ProductId == productUnit.ProductId &&
                    existing.UnitId == productUnit.UnitId &&
                    !existing.IsDeleted))
            {
                throw new InvalidOperationException("Product already has this unit conversion.");
            }

            if (productUnit.IsBaseUnit && db.ProductUnits.Any(existing =>
                    existing.ProductId == productUnit.ProductId &&
                    existing.IsBaseUnit &&
                    !existing.IsDeleted))
            {
                throw new InvalidOperationException("Product already has a base unit.");
            }

            db.ProductUnits.Add(productUnit);
            db.SaveChanges();
        }

        public void DeleteProductUnit(int id)
        {
            using var db = _contextFactory();
            var productUnit = db.ProductUnits.Find(id);
            if (productUnit == null)
            {
                return;
            }

            productUnit.IsDeleted = true;
            db.SaveChanges();
        }

        private static void Validate(ProductUnit productUnit)
        {
            if (productUnit.ConversionRateToBaseUnit <= 0)
            {
                throw new InvalidOperationException("Conversion rate must be greater than zero.");
            }

            if (productUnit.IsBaseUnit && productUnit.ConversionRateToBaseUnit != 1m)
            {
                throw new InvalidOperationException("Base unit conversion rate must be 1.");
            }
        }
    }
}
