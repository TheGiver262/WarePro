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
            using var transaction = db.Database.BeginTransaction();
            db.Suppliers.Add(supplier);
            db.SaveChanges();
            AddAudit(db, "CREATE", supplier.Id, null, Serialize(supplier), performedBy);
            transaction.Commit();
        }

        public void Update(Supplier supplier, string beforeJson, int performedBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            db.Suppliers.Update(supplier);
            db.SaveChanges();
            AddAudit(db, "UPDATE", supplier.Id, beforeJson, Serialize(supplier), performedBy);
            transaction.Commit();
        }

        public void Delete(int id, int performedBy)
        {
            using var db = _contextFactory();
            var supplier = db.Suppliers.Find(id);
            if (supplier != null)
            {
                using var transaction = db.Database.BeginTransaction();
                var beforeJson = Serialize(supplier);
                var hasDependencies = db.PurchaseInvoices.Any(invoice => invoice.SupplierId == id) ||
                                      db.StockIns.Any(stockIn => stockIn.SupplierId == id);
                if (hasDependencies)
                {
                    supplier.IsActive = false;
                    db.SaveChanges();
                    AddAudit(db, "DEACTIVATE", id, beforeJson, Serialize(supplier), performedBy);
                }
                else
                {
                    db.Suppliers.Remove(supplier);
                    db.SaveChanges();
                    AddAudit(db, "DELETE", id, beforeJson, null, performedBy);
                }

                transaction.Commit();
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
