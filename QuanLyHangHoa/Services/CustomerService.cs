using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using System.Text.Json;

namespace QuanLyHangHoa.Services
{
    public class CustomerService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public CustomerService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // CUS-ADJ là khách kỹ thuật cho nghiệp vụ điều chỉnh, không hiển thị trong danh mục người dùng
        public List<Customer> GetAll()
        {
            using var db = _contextFactory();
            return db.Customers.AsNoTracking().Where(c => c.CustomerCode != "CUS-ADJ").OrderBy(c => c.CustomerCode).ToList();
        }

        // thay đổi khách hàng và audit được khóa trong cùng transaction
        public void Add(Customer customer, int performedBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            db.Customers.Add(customer);
            db.SaveChanges();
            AddAudit(db, "CREATE", customer.Id, null, Serialize(customer), performedBy);
            transaction.Commit();
        }

        public void Update(Customer customer, string beforeJson, int performedBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            db.Customers.Update(customer);
            db.SaveChanges();
            AddAudit(db, "UPDATE", customer.Id, beforeJson, Serialize(customer), performedBy);
            transaction.Commit();
        }

        public void Delete(int id, int performedBy)
        {
            using var db = _contextFactory();
            var customer = db.Customers.Find(id);
            if (customer != null)
            {
                using var transaction = db.Database.BeginTransaction();
                var beforeJson = Serialize(customer);
                // kiểm tra mọi bảng giữ lịch sử khách hàng; có tham chiếu thì chỉ vô hiệu hóa
                var hasDependencies = db.SalesInvoices.Any(invoice => invoice.CustomerId == id) ||
                                      db.StockOuts.Any(stockOut => stockOut.CustomerId == id) ||
                                      db.WarrantyCoverages.Any(coverage => coverage.CustomerId == id);
                if (hasDependencies)
                {
                    customer.IsActive = false;
                    db.SaveChanges();
                    AddAudit(db, "DEACTIVATE", id, beforeJson, Serialize(customer), performedBy);
                }
                else
                {
                    db.Customers.Remove(customer);
                    db.SaveChanges();
                    AddAudit(db, "DELETE", id, beforeJson, null, performedBy);
                }

                transaction.Commit();
            }
        }

        private string Serialize(Customer c)
        {
            return JsonSerializer.Serialize(new { c.Id, c.CustomerCode, c.DisplayName, c.Phone, c.Email, c.Address, c.IsActive });
        }

        // snapshot chỉ chứa trường nghiệp vụ cần đọc, không serialize navigation của EF
        private void AddAudit(AppDbContext db, string action, int entityId, string? before, string? after, int performedBy)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "Customer",
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
