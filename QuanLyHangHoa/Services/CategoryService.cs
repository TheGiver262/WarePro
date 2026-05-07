using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using System.Text.Json;

namespace QuanLyHangHoa.Services
{
    public class CategoryService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public CategoryService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public List<Category> GetAll()
        {
            using var db = _contextFactory();
            return db.Categories.AsNoTracking().OrderBy(c => c.CategoryCode).ToList();
        }

        public void Add(Category category, int performedBy)
        {
            using var db = _contextFactory();
            db.Categories.Add(category);
            db.SaveChanges();
            AddAudit(db, "CREATE", category.Id, null, Serialize(category), performedBy);
        }

        public void Update(Category category, string beforeJson, int performedBy)
        {
            using var db = _contextFactory();
            db.Categories.Update(category);
            db.SaveChanges();
            AddAudit(db, "UPDATE", category.Id, beforeJson, Serialize(category), performedBy);
        }

        public void Delete(int id, int performedBy)
        {
            using var db = _contextFactory();
            var category = db.Categories.Find(id);
            if (category != null)
            {
                var beforeJson = Serialize(category);
                db.Categories.Remove(category);
                db.SaveChanges();
                AddAudit(db, "DELETE", id, beforeJson, null, performedBy);
            }
        }

        private string Serialize(Category c)
        {
            return JsonSerializer.Serialize(new { c.Id, c.CategoryCode, c.DisplayName, c.IsActive });
        }

        private void AddAudit(AppDbContext db, string action, int entityId, string? before, string? after, int performedBy)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "Category",
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
