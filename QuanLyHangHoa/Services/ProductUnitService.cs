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
        public List<ProductUnit> GetByProductId(int productId)
        {
            using var db = new AppDbContext();
            return db.ProductUnits
                .Include(pu => pu.Unit)
                .Where(pu => pu.ProductId == productId)
                .ToList();
        }

        public void Add(ProductUnit pu)
        {
            using var db = new AppDbContext();
            db.ProductUnits.Add(pu);
            db.SaveChanges();
        }

        public void Update(ProductUnit updated)
        {
            using var db = new AppDbContext();
            var pu = db.ProductUnits.Find(updated.Id);
            if (pu == null) return;

            pu.UnitId = updated.UnitId;
            pu.ConversionFactor = updated.ConversionFactor;
            pu.IsBaseUnit = updated.IsBaseUnit;
            pu.IsPurchaseUnit = updated.IsPurchaseUnit;
            pu.IsSalesUnit = updated.IsSalesUnit;

            db.SaveChanges();
        }

        public void Delete(int id)
        {
            using var db = new AppDbContext();
            var pu = db.ProductUnits.Find(id);
            if (pu == null) return;
            db.ProductUnits.Remove(pu);
            db.SaveChanges();
        }

        public decimal ConvertToBaseUnit(int productId, int sourceUnitId, decimal quantity)
        {
            using var db = new AppDbContext();
            var units = db.ProductUnits.Where(pu => pu.ProductId == productId).ToList();
            var source = units.FirstOrDefault(u => u.UnitId == sourceUnitId);
            
            if (source == null) return quantity;
            return quantity * source.ConversionFactor;
        }

        public decimal ConvertFromBaseUnit(int productId, int targetUnitId, decimal quantity)
        {
            using var db = new AppDbContext();
            var units = db.ProductUnits.Where(pu => pu.ProductId == productId).ToList();
            var target = units.FirstOrDefault(u => u.UnitId == targetUnitId);
            
            if (target == null || target.ConversionFactor == 0) return quantity;
            return quantity / target.ConversionFactor;
        }
    }
}
