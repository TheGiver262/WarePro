using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using System.Text.Json;

namespace QuanLyHangHoa.Services
{
    public class SupplierService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public SupplierService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public List<Supplier> GetAll()
        {
            using var db = _contextFactory();
            return db.Suppliers.AsNoTracking().OrderBy(s => s.SupplierCode).ToList();
        }

        public void Add(Supplier supplier, int performedBy)
        {
            using var db = _contextFactory();
            db.Suppliers.Add(supplier);
            db.SaveChanges();
            AddAudit(db, "CREATE", supplier.Id, null, Serialize(supplier), performedBy);
        }

        public void Update(Supplier supplier, string beforeJson, int performedBy)
        {
            using var db = _contextFactory();
            db.Suppliers.Update(supplier);
            db.SaveChanges();
            AddAudit(db, "UPDATE", supplier.Id, beforeJson, Serialize(supplier), performedBy);
        }

        public void Delete(int id, int performedBy)
        {
            using var db = _contextFactory();
            var supplier = db.Suppliers.Find(id);
            if (supplier != null)
            {
                var beforeJson = Serialize(supplier);
                db.Suppliers.Remove(supplier);
                db.SaveChanges();
                AddAudit(db, "DELETE", id, beforeJson, null, performedBy);
            }
        }

        private string Serialize(Supplier s)
        {
            return JsonSerializer.Serialize(new { s.Id, s.SupplierCode, s.DisplayName, s.Phone, s.Email, s.Address, s.IsActive });
        }

        private void AddAudit(AppDbContext db, string action, int entityId, string? before, string? after, int performedBy)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "Supplier",
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
