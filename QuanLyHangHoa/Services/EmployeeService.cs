using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class EmployeeService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly Func<DateTime> _clock;

        public EmployeeService()
            : this(() => new AppDbContext(), () => DateTime.Now)
        {
        }

        public EmployeeService(Func<AppDbContext> contextFactory, Func<DateTime> clock)
        {
            _contextFactory = contextFactory;
            _clock = clock;
        }

        public List<Employee> GetAllEmployees()
        {
            using var db = _contextFactory();
            return db.Employees.ToList();
        }

        public void AddEmployee(Employee emp)
        {
            AddEmployee(emp, performedByUserId: null);
        }

        public void AddEmployee(Employee emp, int? performedByUserId)
        {
            using var db = _contextFactory();
            if (string.IsNullOrWhiteSpace(emp.PasswordHash))
            {
                emp.PasswordHash = emp.Username;
            }

            db.Employees.Add(emp);
            AddAuditIfNeeded(db, "CreateEmployee", performedByUserId);
            db.SaveChanges();
        }

        public void UpdateEmployee(Employee updatedEmp)
        {
            UpdateEmployee(updatedEmp, performedByUserId: null);
        }

        public void UpdateEmployee(Employee updatedEmp, int? performedByUserId)
        {
            using var db = _contextFactory();
            var employee = db.Employees.Find(updatedEmp.Id);
            if (employee == null)
            {
                return;
            }

            employee.FullName = updatedEmp.FullName;
            employee.DateOfBirth = updatedEmp.DateOfBirth;
            employee.Position = updatedEmp.Position;
            employee.Role = updatedEmp.Role;
            if (!string.IsNullOrWhiteSpace(updatedEmp.PasswordHash))
            {
                employee.PasswordHash = updatedEmp.PasswordHash;
                employee.Username = updatedEmp.Username;
            }

            AddAuditIfNeeded(db, "UpdateEmployee", performedByUserId);
            db.SaveChanges();
        }

        public void DeleteEmployee(int id)
        {
            DeleteEmployee(id, performedByUserId: null);
        }

        public void DeleteEmployee(int id, int? performedByUserId)
        {
            if (id == 1)
            {
                return;
            }

            using var db = _contextFactory();
            var employee = db.Employees.Find(id);
            if (employee == null)
            {
                return;
            }

            db.Employees.Remove(employee);
            AddAuditIfNeeded(db, "DeleteEmployee", performedByUserId);
            db.SaveChanges();
        }

        private void AddAuditIfNeeded(AppDbContext db, string actionCode, int? performedByUserId)
        {
            if (!performedByUserId.HasValue)
            {
                return;
            }

            db.AuditLogs.Add(new AuditLog
            {
                DocumentId = Guid.NewGuid(),
                ActionCode = actionCode,
                PerformedAt = _clock(),
                PerformedByUserId = performedByUserId.Value
            });
        }
    }
}
