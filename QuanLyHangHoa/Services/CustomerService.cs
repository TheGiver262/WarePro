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

        public List<Customer> GetAll()
        {
            using var db = _contextFactory();
            return db.Customers.AsNoTracking().OrderBy(c => c.CustomerCode).ToList();
        }

        public void Add(Customer customer, int performedBy)
        {
            using var db = _contextFactory();
            db.Customers.Add(customer);
            db.SaveChanges();
            AddAudit(db, "CREATE", customer.Id, null, Serialize(customer), performedBy);
        }

        public void Update(Customer customer, string beforeJson, int performedBy)
        {
            using var db = _contextFactory();
            db.Customers.Update(customer);
            db.SaveChanges();
            AddAudit(db, "UPDATE", customer.Id, beforeJson, Serialize(customer), performedBy);
        }

        public void Delete(int id, int performedBy)
        {
            using var db = _contextFactory();
            var customer = db.Customers.Find(id);
            if (customer != null)
            {
                var beforeJson = Serialize(customer);
                db.Customers.Remove(customer);
                db.SaveChanges();
                AddAudit(db, "DELETE", id, beforeJson, null, performedBy);
            }
        }

        private string Serialize(Customer c)
        {
            return JsonSerializer.Serialize(new { c.Id, c.CustomerCode, c.DisplayName, c.Phone, c.Email, c.Address, c.IsActive });
        }

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
