using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using System.Text.Json;

namespace QuanLyHangHoa.Services
{
    /// <summary>
    /// quản lý đơn vị đo và giữ an toàn lịch sử bằng cách deactivate khi đã có dữ liệu tham chiếu.
    /// </summary>
    public class UnitService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public UnitService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public List<Unit> GetAll()
        {
            using var db = _contextFactory();
            return db.Units.AsNoTracking().OrderBy(u => u.DisplayName).ToList();
        }

        public void Add(Unit unit, int performedBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            db.Units.Add(unit);
            db.SaveChanges();
            AddAudit(db, "CREATE", unit.Id, null, Serialize(unit), performedBy);
            transaction.Commit();
        }

        public void Update(Unit unit, string beforeJson, int performedBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            db.Units.Update(unit);
            db.SaveChanges();
            AddAudit(db, "UPDATE", unit.Id, beforeJson, Serialize(unit), performedBy);
            transaction.Commit();
        }

        public IReadOnlyList<(string Name, int Count)> GetDependencies(int unitId)
        {
            using var db = _contextFactory();
            return GetDependencies(db, unitId);
        }

        // kiểm tra cả master data và line chứng từ vì mọi quan hệ đều dùng delete restriction.
        private static IReadOnlyList<(string Name, int Count)> GetDependencies(
            AppDbContext db,
            int unitId)
        {
            return new List<(string Name, int Count)>
            {
                ("Product", db.Products.Count(row => row.DefaultUnitId == unitId)),
                ("ProductUnit", db.ProductUnits.Count(row => row.UnitId == unitId)),
                ("PurchaseInvoiceLine", db.PurchaseInvoiceLines.Count(row => row.UnitId == unitId)),
                ("SalesInvoiceLine", db.SalesInvoiceLines.Count(row => row.UnitId == unitId)),
                ("StockInLine", db.StockInLines.Count(row => row.UnitId == unitId)),
                ("StockOutLine", db.StockOutLines.Count(row => row.UnitId == unitId)),
                ("StockTransferLine", db.StockTransferLines.Count(row => row.UnitId == unitId))
            };
        }

        public void Delete(int id, int performedBy)
        {
            using var db = _contextFactory();
            var unit = db.Units.Find(id);
            if (unit != null)
            {
                using var transaction = db.Database.BeginTransaction();
                var beforeJson = Serialize(unit);
                // có tham chiếu thì soft-delete bằng IsActive; chỉ record chưa từng dùng mới được xóa vật lý.
                var hasDependencies = GetDependencies(db, id).Any(dependency => dependency.Count > 0);
                if (hasDependencies)
                {
                    unit.IsActive = false;
                    db.SaveChanges();
                    AddAudit(db, "DEACTIVATE", id, beforeJson, Serialize(unit), performedBy);
                }
                else
                {
                    db.Units.Remove(unit);
                    db.SaveChanges();
                    AddAudit(db, "DELETE", id, beforeJson, null, performedBy);
                }

                transaction.Commit();
            }
        }

        private string Serialize(Unit u)
        {
            return JsonSerializer.Serialize(new { u.Id, u.DisplayName, u.IsActive });
        }

        // audit được SaveChanges trong transaction của mutation để trạng thái và lịch sử cùng commit.
        private void AddAudit(AppDbContext db, string action, int entityId, string? before, string? after, int performedBy)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "Unit",
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
