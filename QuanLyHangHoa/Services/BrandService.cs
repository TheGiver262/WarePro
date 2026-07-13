using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using System.Text.Json;

namespace QuanLyHangHoa.Services
{
    public class BrandService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public BrandService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public List<Brand> GetAll()
        {
            using var db = _contextFactory();
            return db.Brands.AsNoTracking().OrderBy(b => b.BrandCode).ToList();
        }

        public void Add(Brand brand, int performedBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            db.Brands.Add(brand);
            db.SaveChanges();
            AddAudit(db, "CREATE", brand.Id, null, Serialize(brand), performedBy);
            transaction.Commit();
        }

        public void Update(Brand brand, string beforeJson, int performedBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            db.Brands.Update(brand);
            db.SaveChanges();
            AddAudit(db, "UPDATE", brand.Id, beforeJson, Serialize(brand), performedBy);
            transaction.Commit();
        }

        public void Delete(int id, int performedBy)
        {
            using var db = _contextFactory();
            var brand = db.Brands.Find(id);
            if (brand != null)
            {
                using var transaction = db.Database.BeginTransaction();
                var beforeJson = Serialize(brand);
                if (db.Products.Any(product => product.BrandId == id))
                {
                    brand.IsActive = false;
                    db.SaveChanges();
                    AddAudit(db, "DEACTIVATE", id, beforeJson, Serialize(brand), performedBy);
                }
                else
                {
                    db.Brands.Remove(brand);
                    db.SaveChanges();
                    AddAudit(db, "DELETE", id, beforeJson, null, performedBy);
                }

                transaction.Commit();
            }
        }

        private string Serialize(Brand b)
        {
            return JsonSerializer.Serialize(new { b.Id, b.BrandCode, b.DisplayName, b.OriginCountry, b.IsActive });
        }

        private void AddAudit(AppDbContext db, string action, int entityId, string? before, string? after, int performedBy)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "Brand",
                EntityId = entityId,
                ActionCode = action,
                BeforeJson = before,
                AfterJson = after,
                PerformedBy = performedBy,
                PerformedAt = DateTime.Now
            });
            db.SaveChanges();
        }
    }
}
