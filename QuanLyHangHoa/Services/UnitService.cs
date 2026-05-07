using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using System.Text.Json;

namespace QuanLyHangHoa.Services
{
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
            db.Units.Add(unit);
            db.SaveChanges();
            AddAudit(db, "CREATE", unit.Id, null, Serialize(unit), performedBy);
        }

        public void Update(Unit unit, string beforeJson, int performedBy)
        {
            using var db = _contextFactory();
            db.Units.Update(unit);
            db.SaveChanges();
            AddAudit(db, "UPDATE", unit.Id, beforeJson, Serialize(unit), performedBy);
        }

        public void Delete(int id, int performedBy)
        {
            using var db = _contextFactory();
            var unit = db.Units.Find(id);
            if (unit != null)
            {
                var beforeJson = Serialize(unit);
                db.Units.Remove(unit);
                db.SaveChanges();
                AddAudit(db, "DELETE", id, beforeJson, null, performedBy);
            }
        }

        private string Serialize(Unit u)
        {
            return JsonSerializer.Serialize(new { u.Id, u.DisplayName, u.IsActive });
        }

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
