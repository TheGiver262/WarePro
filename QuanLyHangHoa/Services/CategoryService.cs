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

        // danh sách được sắp theo mã để màn hình và file xuất có thứ tự ổn định
        public List<Category> GetAll()
        {
            using var db = _contextFactory();
            return db.Categories.AsNoTracking().OrderBy(c => c.CategoryCode).ToList();
        }

        // insert và audit phải cùng thành công hoặc cùng rollback
        public void Add(Category category, int performedBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            db.Categories.Add(category);
            db.SaveChanges();
            AddAudit(db, "CREATE", category.Id, null, Serialize(category), performedBy);
            transaction.Commit();
        }

        public void Update(Category category, string beforeJson, int performedBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            db.Categories.Update(category);
            db.SaveChanges();
            AddAudit(db, "UPDATE", category.Id, beforeJson, Serialize(category), performedBy);
            transaction.Commit();
        }

        public void Delete(int id, int performedBy)
        {
            using var db = _contextFactory();
            var category = db.Categories.Find(id);
            if (category != null)
            {
                using var transaction = db.Database.BeginTransaction();
                var beforeJson = Serialize(category);
                // nhóm hàng đang có sản phẩm được chuyển inactive thay vì xóa khóa ngoại
                if (db.Products.Any(product => product.CategoryId == id))
                {
                    category.IsActive = false;
                    db.SaveChanges();
                    AddAudit(db, "DEACTIVATE", id, beforeJson, Serialize(category), performedBy);
                }
                else
                {
                    db.Categories.Remove(category);
                    db.SaveChanges();
                    AddAudit(db, "DELETE", id, beforeJson, null, performedBy);
                }

                transaction.Commit();
            }
        }

        private string Serialize(Category c)
        {
            return JsonSerializer.Serialize(new { c.Id, c.CategoryCode, c.DisplayName, c.IsActive });
        }

        // lưu người thao tác và hai trạng thái để có thể đối chiếu khi cần
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
