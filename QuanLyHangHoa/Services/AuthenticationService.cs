using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    // Lớp Dịch vụ xác thực (Authentication) để xử lý logic tìm và kiểm tra tài khoản
    public class AuthenticationService
    {
        // Hàm đăng nhập, nhận vào username và password, trả về Employee nếu thành công, trả về null nếu thất bại
        public Employee? Authenticate(string username, string password)
        {
            using (var db = new AppDbContext())
            {
                // Tìm kiếm nhân viên có username và password khớp với DB 
                // Cần dùng ToList hoặc FirstOrDefault để thực thi query
                var user = db.Employees
                    .FirstOrDefault(u => u.Username == username && u.PasswordHash == password);
                
                return user; // Gửi lại thông tin user cho ViewModel
            }
        }
    }
}
