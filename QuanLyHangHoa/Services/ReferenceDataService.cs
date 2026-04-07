using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class ReferenceDataService
    {
        // ── UNITS ──────────────────────────────────────────────────────────────
        public List<Unit> GetAllUnits()
        {
            using var db = new AppDbContext();
            return db.Units.Where(u => !u.IsDeleted).ToList();
        }
        public void AddUnit(Unit u) { using var db = new AppDbContext(); db.Units.Add(u); db.SaveChanges(); }
        public void UpdateUnit(Unit updated)
        {
            using var db = new AppDbContext();
            var u = db.Units.Find(updated.Id);
            if (u == null) return;
            u.Name = updated.Name;
            db.SaveChanges();
        }
        public void DeleteUnit(int id)
        {
            using var db = new AppDbContext();
            var u = db.Units.Find(id);
            if (u == null) return;
            u.IsDeleted = true;
            db.SaveChanges();
        }

        // ── CATEGORIES ────────────────────────────────────────────────────────
        public List<Category> GetAllCategories()
        {
            using var db = new AppDbContext();
            return db.Categories.Where(c => !c.IsDeleted).ToList();
        }
        public void AddCategory(Category c) { using var db = new AppDbContext(); db.Categories.Add(c); db.SaveChanges(); }
        public void UpdateCategory(Category updated)
        {
            using var db = new AppDbContext();
            var c = db.Categories.Find(updated.Id);
            if (c == null) return;
            c.Name = updated.Name;
            db.SaveChanges();
        }
        public void DeleteCategory(int id)
        {
            using var db = new AppDbContext();
            var c = db.Categories.Find(id);
            if (c == null) return;
            c.IsDeleted = true;
            db.SaveChanges();
        }

        // ── BRANDS ────────────────────────────────────────────────────────────
        public List<Brand> GetAllBrands()
        {
            using var db = new AppDbContext();
            return db.Brands.Where(b => !b.IsDeleted).ToList();
        }
        public void AddBrand(Brand b) { using var db = new AppDbContext(); db.Brands.Add(b); db.SaveChanges(); }
        public void UpdateBrand(Brand updated)
        {
            using var db = new AppDbContext();
            var b = db.Brands.Find(updated.Id);
            if (b == null) return;
            b.Name = updated.Name;
            db.SaveChanges();
        }
        public void DeleteBrand(int id)
        {
            using var db = new AppDbContext();
            var b = db.Brands.Find(id);
            if (b == null) return;
            b.IsDeleted = true;
            db.SaveChanges();
        }

        // ── SUPPLIERS ─────────────────────────────────────────────────────────
        public List<Supplier> GetAllSuppliers()
        {
            using var db = new AppDbContext();
            return db.Suppliers.Where(s => !s.IsDeleted).ToList();
        }
        public void AddSupplier(Supplier s) { using var db = new AppDbContext(); db.Suppliers.Add(s); db.SaveChanges(); }
        public void UpdateSupplier(Supplier updated)
        {
            using var db = new AppDbContext();
            var s = db.Suppliers.Find(updated.Id);
            if (s == null) return;
            s.Name = updated.Name; s.Address = updated.Address; s.Phone = updated.Phone;
            db.SaveChanges();
        }
        public void DeleteSupplier(int id)
        {
            using var db = new AppDbContext();
            var s = db.Suppliers.Find(id);
            if (s == null) return;
            s.IsDeleted = true;
            db.SaveChanges();
        }

        // ── CUSTOMERS ─────────────────────────────────────────────────────────
        public List<Customer> GetAllCustomers()
        {
            using var db = new AppDbContext();
            return db.Customers.Where(c => !c.IsDeleted).ToList();
        }
        public void AddCustomer(Customer c) { using var db = new AppDbContext(); db.Customers.Add(c); db.SaveChanges(); }
        public void UpdateCustomer(Customer updated)
        {
            using var db = new AppDbContext();
            var c = db.Customers.Find(updated.Id);
            if (c == null) return;
            c.Name = updated.Name; c.Address = updated.Address; c.Phone = updated.Phone;
            db.SaveChanges();
        }
        public void DeleteCustomer(int id)
        {
            using var db = new AppDbContext();
            var c = db.Customers.Find(id);
            if (c == null) return;
            c.IsDeleted = true;
            db.SaveChanges();
        }
    }
}
