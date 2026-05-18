using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Data;
using System;
using Microsoft.EntityFrameworkCore;

namespace QuanLyHangHoa.Services
{
    public class ReferenceDataService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public ReferenceDataService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public virtual List<Category> GetAllCategories(bool onlyActive = true)
        {
            using var db = _contextFactory();
            var query = db.Categories.AsNoTracking().AsQueryable();
            if (onlyActive) query = query.Where(c => c.IsActive);
            return query.OrderBy(c => c.DisplayName).ToList();
        }

        public virtual List<Brand> GetAllBrands(bool onlyActive = true)
        {
            using var db = _contextFactory();
            var query = db.Brands.AsNoTracking().AsQueryable();
            if (onlyActive) query = query.Where(b => b.IsActive);
            return query.OrderBy(b => b.DisplayName).ToList();
        }

        public virtual List<Unit> GetAllUnits(bool onlyActive = true)
        {
            using var db = _contextFactory();
            var query = db.Units.AsNoTracking().AsQueryable();
            if (onlyActive) query = query.Where(u => u.IsActive);
            return query.OrderBy(u => u.DisplayName).ToList();
        }

        public virtual List<Warehouse> GetAllWarehouses(bool onlyActive = true)
        {
            using var db = _contextFactory();
            var query = db.Warehouses.AsNoTracking().AsQueryable();
            if (onlyActive) query = query.Where(w => w.IsActive);
            return query.OrderBy(w => w.DisplayName).ToList();
        }

        public virtual List<Supplier> GetAllSuppliers(bool onlyActive = true)
        {
            using var db = _contextFactory();
            var query = db.Suppliers.AsNoTracking().AsQueryable();
            if (onlyActive) query = query.Where(s => s.IsActive);
            return query.OrderBy(s => s.DisplayName).ToList();
        }

        public virtual List<Customer> GetAllCustomers(bool onlyActive = true)
        {
            using var db = _contextFactory();
            var query = db.Customers.AsNoTracking().AsQueryable();
            if (onlyActive) query = query.Where(c => c.IsActive);
            return query.OrderBy(c => c.DisplayName).ToList();
        }
    }
}
