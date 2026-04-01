using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    // Bảng Nhân viên
    public class Employee
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Position { get; set; } = string.Empty;

        // Thuộc tính phục vụ đăng nhập
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        // Role: "Admin" hoặc "Staff"
        public string Role { get; set; } = "Staff";

        // Navigation properties
        public virtual ICollection<Invoice>? Invoices { get; set; }
        public virtual ICollection<ImportReceipt>? ImportReceipts { get; set; }
    }
}
