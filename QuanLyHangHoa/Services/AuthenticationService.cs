using System;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class AuthenticationService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public AuthenticationService()
            : this(() => new AppDbContext())
        {
        }

        public AuthenticationService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public Employee? Authenticate(string username, string password)
        {
            using var db = _contextFactory();
            return db.Employees
                .FirstOrDefault(user => user.Username == username && user.PasswordHash == password);
        }

        public void ChangePassword(string username, string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                throw new InvalidOperationException("New password is required.");
            }

            using var db = _contextFactory();
            var user = db.Employees.FirstOrDefault(employee => employee.Username == username)
                ?? throw new InvalidOperationException("User does not exist.");

            if (user.PasswordHash != currentPassword)
            {
                throw new InvalidOperationException("Current password is incorrect.");
            }

            user.PasswordHash = newPassword;
            db.SaveChanges();
        }
    }
}
