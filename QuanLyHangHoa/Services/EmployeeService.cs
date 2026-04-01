using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    // Dịch vụ quản lý thông tin Nhân sự / Tài khoản đăng nhập
    public class EmployeeService
    {
        // Lấy danh sách toàn bộ nhân viên/tài khoản
        public List<Employee> GetAllEmployees()
        {
            using (var db = new AppDbContext())
            {
                return db.Employees.ToList();
            }
        }

        // Tạo tài khoản mới, phân quyền chức vụ
        public void AddEmployee(Employee emp)
        {
            using (var db = new AppDbContext())
            {
                // Mặc định gán mật khẩu an toàn theo Username để hệ thống demo chạy dễ dàng
                if (string.IsNullOrWhiteSpace(emp.PasswordHash))
                {
                    emp.PasswordHash = emp.Username; 
                }
                
                db.Employees.Add(emp);
                db.SaveChanges(); // Lệnh này giúp đẩy tài khoản lên server để họ đăng nhập
            }
        }

        // Cập nhật thông tin nhân viên hoặc đổi mật khẩu/quyền nếu Admin muốn
        public void UpdateEmployee(Employee updatedEmp)
        {
            using (var db = new AppDbContext())
            {
                var p = db.Employees.Find(updatedEmp.Id);
                if (p != null)
                {
                    p.FullName = updatedEmp.FullName;
                    p.DateOfBirth = updatedEmp.DateOfBirth;
                    p.Position = updatedEmp.Position;
                    p.Role = updatedEmp.Role;
                    // Không cần đổi Mật khẩu nếu ô này trống, chỉ đổi nếu chủ đích cấp mật khẩu mới
                    if (!string.IsNullOrWhiteSpace(updatedEmp.PasswordHash))
                    {
                        p.PasswordHash = updatedEmp.PasswordHash;
                        p.Username = updatedEmp.Username;
                    }

                    db.SaveChanges();
                }
            }
        }

        // Sa thải nhân viên / Khoá tài khoản
        public void DeleteEmployee(int id)
        {
            using (var db = new AppDbContext())
            {
                // Tuyệt đối không xóa tài khoản Admin cao cấp nhất tránh hệ thống vô chủ
                if (id == 1) return; 

                var emp = db.Employees.Find(id);
                if (emp != null)
                {
                    db.Employees.Remove(emp);
                    db.SaveChanges();
                }
            }
        }
    }
}
