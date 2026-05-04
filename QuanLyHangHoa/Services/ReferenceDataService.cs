using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class ReferenceDataService
    {
        // ── UNITS ──────────────────────────────────────────────────────────────
        public virtual List<Unit> GetAllUnits()
        {
            using var db = new AppDbContext();
            return db.Units.Where(u => u.IsActive).ToList();
        }
        public virtual void AddUnit(Unit u) { using var db = new AppDbContext(); db.Units.Add(u); db.SaveChanges(); }
        public virtual void UpdateUnit(Unit updated)
        {
            using var db = new AppDbContext();
            var u = db.Units.Find(updated.Id);
            if (u == null) return;
            u.DisplayName = updated.DisplayName;
            u.UnitCode = updated.UnitCode;
            db.SaveChanges();
        }
        public virtual void DeactivateUnit(int id)
        {
            using var db = new AppDbContext();
            var u = db.Units.Find(id);
            if (u == null) return;
            u.IsActive = false;
            db.SaveChanges();
        }

        // ── CATEGORIES ────────────────────────────────────────────────────────
        public virtual List<Category> GetAllCategories(bool onlyActive = true)
        {
            using var db = new AppDbContext();
            var query = db.Categories.AsQueryable();
            if (onlyActive) query = query.Where(c => c.IsActive);
            return query.ToList();
        }
        public virtual void AddCategory(Category c) { using var db = new AppDbContext(); db.Categories.Add(c); db.SaveChanges(); }
        public virtual void UpdateCategory(Category updated)
        {
            using var db = new AppDbContext();
            var c = db.Categories.Find(updated.Id);
            if (c == null) return;
            c.DisplayName = updated.DisplayName;
            c.CategoryCode = updated.CategoryCode;
            db.SaveChanges();
        }
        public virtual void DeactivateCategory(int id)
        {
            using var db = new AppDbContext();
            var c = db.Categories.Find(id);
            if (c == null) return;
            c.IsActive = false;
            db.SaveChanges();
        }

        // ── BRANDS ────────────────────────────────────────────────────────────
        public virtual List<Brand> GetAllBrands()
        {
            using var db = new AppDbContext();
            return db.Brands.Where(b => b.IsActive).ToList();
        }
        public virtual void AddBrand(Brand b) { using var db = new AppDbContext(); db.Brands.Add(b); db.SaveChanges(); }
        public virtual void UpdateBrand(Brand updated)
        {
            using var db = new AppDbContext();
            var b = db.Brands.Find(updated.Id);
            if (b == null) return;
            b.DisplayName = updated.DisplayName;
            b.BrandCode = updated.BrandCode;
            b.OriginCountry = updated.OriginCountry;
            db.SaveChanges();
        }
        public virtual void DeactivateBrand(int id)
        {
            using var db = new AppDbContext();
            var b = db.Brands.Find(id);
            if (b == null) return;
            b.IsActive = false;
            db.SaveChanges();
        }

        // ── SUPPLIERS ─────────────────────────────────────────────────────────
        public virtual List<Supplier> GetAllSuppliers()
        {
            using var db = new AppDbContext();
            return db.Suppliers.Where(s => s.IsActive).ToList();
        }
        public virtual void AddSupplier(Supplier s) { using var db = new AppDbContext(); db.Suppliers.Add(s); db.SaveChanges(); }
        public virtual void UpdateSupplier(Supplier updated)
        {
            using var db = new AppDbContext();
            var s = db.Suppliers.Find(updated.Id);
            if (s == null) return;
            s.SupplierCode = updated.SupplierCode;
            s.DisplayName = updated.DisplayName; 
            s.Address = updated.Address; 
            s.Phone = updated.Phone;
            s.Email = updated.Email;
            db.SaveChanges();
        }
        public virtual void DeactivateSupplier(int id)
        {
            using var db = new AppDbContext();
            var s = db.Suppliers.Find(id);
            if (s == null) return;
            s.IsActive = false;
            db.SaveChanges();
        }

        // ── CUSTOMERS ─────────────────────────────────────────────────────────
        public virtual List<Customer> GetAllCustomers()
        {
            using var db = new AppDbContext();
            return db.Customers.Where(c => c.IsActive).ToList();
        }
        public virtual void AddCustomer(Customer c) { using var db = new AppDbContext(); db.Customers.Add(c); db.SaveChanges(); }
        public virtual void UpdateCustomer(Customer updated)
        {
            using var db = new AppDbContext();
            var c = db.Customers.Find(updated.Id);
            if (c == null) return;
            c.CustomerCode = updated.CustomerCode;
            c.DisplayName = updated.DisplayName; 
            c.Address = updated.Address; 
            c.Phone = updated.Phone;
            c.Email = updated.Email;
            db.SaveChanges();
        }
        public virtual void DeactivateCustomer(int id)
        {
            using var db = new AppDbContext();
            var c = db.Customers.Find(id);
            if (c == null) return;
            c.IsActive = false;
            db.SaveChanges();
        }
    }
}
