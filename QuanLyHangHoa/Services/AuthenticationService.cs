using System;
using System.Linq;
using System.Text.Json;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using BCrypt.Net;

namespace QuanLyHangHoa.Services
{
    public class AuthenticationService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public AuthenticationService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // log đăng nhập không gắn PerformedBy vì chưa có danh tính đã xác thực; chỉ lưu username đã thử
        private static void WriteLoginAudit(
            AppDbContext db,
            string actionCode,
            string attemptedUsername)
        {
            try
            {
                db.AuditLogs.Add(new AuditLog
                {
                    EntityName = "Authentication",
                    EntityId = 0,
                    ActionCode = actionCode,
                    PerformedBy = null,
                    PerformedAt = DateTime.Now,
                    AfterJson = JsonSerializer.Serialize(new { attemptedUsername })
                });
                db.SaveChanges();
            }
            catch
            {
                // lỗi audit không được làm lộ tài khoản có tồn tại hay chặn luồng đăng nhập
            }
        }

        // mọi nhánh thất bại trả LoginResult chung; mật khẩu không bao giờ được ghi log
        public LoginResult Authenticate(string username, string password)
        {
            using var db = _contextFactory();
            // database có thể so tên không phân biệt hoa thường, nên lấy ứng viên trước
            var user = db.AppUsers.FirstOrDefault(u => u.Username == username);
            
            // kiểm tra lại đúng chữ hoa thường tại ứng dụng để chính sách username không phụ thuộc collation
            if (user == null || user.Username != username)
            {
                WriteLoginAudit(db, "LoginFailed", username);
                return LoginResult.Invalid(0);
            }

            if (!user.IsActive)
            {
                WriteLoginAudit(db, "LoginFailed", username);
                return LoginResult.Inactive();
            }
            
            // tài khoản còn thời gian khóa được trả về ngay, không chạy BCrypt
            if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.Now)
                return LoginResult.Locked(user.LockoutUntil);

            var stored = user.PasswordHash ?? "";
            bool verified = false;

            try 
            {
                // chỉ verify chuỗi có hình dạng BCrypt; hash lỗi không được coi là mật khẩu hợp lệ
                if (stored.StartsWith("$2") && stored.Contains('$'))
                {
                    verified = BCrypt.Net.BCrypt.Verify(password, stored);
                }

                if (verified)
                {
                    user.LastLoginAt = DateTime.Now;
                    user.FailedLoginCount = 0;
                    user.LockoutUntil = null;
                    db.SaveChanges();
                    return LoginResult.Success(user);
                }
                else
                {
                    // đếm lỗi liên tiếp: từ 5 lần khóa 5 phút, từ 10 lần khóa 15 phút và đánh dấu đáng ngờ
                    user.FailedLoginCount++;
                    user.LastFailedLoginAt = DateTime.Now;
                    
                    if (user.FailedLoginCount >= 10)
                    {
                        user.LockoutUntil = DateTime.Now.AddMinutes(15);
                        db.SaveChanges();
                        WriteLoginAudit(db, "SuspiciousLoginAttempt", username);
                        return LoginResult.Locked(user.LockoutUntil);
                    }
                    else if (user.FailedLoginCount >= 5)
                    {
                        user.LockoutUntil = DateTime.Now.AddMinutes(5);
                        db.SaveChanges();
                        WriteLoginAudit(db, "LoginLocked", username);
                        return LoginResult.Locked(user.LockoutUntil);
                    }
                    else
                    {
                        db.SaveChanges();
                        WriteLoginAudit(db, "LoginFailed", username);
                        return LoginResult.Invalid(user.FailedLoginCount);
                    }
                }
            }
            catch
            {
                // hash hỏng hoặc lỗi verify được xử lý như sai thông tin, không đẩy chi tiết bảo mật ra giao diện
            }

            return LoginResult.Invalid(user?.FailedLoginCount ?? 0);
        }

        // phải verify mật khẩu hiện tại rồi tạo hash mới; cờ đổi mật khẩu lần đầu chỉ tắt sau khi lưu thành công
        public void ChangePassword(int userId, string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                throw new InvalidOperationException("New password is required.");
            }

            using var db = _contextFactory();
            var user = db.AppUsers.Find(userId)
                ?? throw new InvalidOperationException("User does not exist.");

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                throw new InvalidOperationException("Mật khẩu hiện tại không chính xác.");
            }

            if (currentPassword == newPassword)
            {
                throw new InvalidOperationException("Mật khẩu mới không được trùng với mật khẩu cũ.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.LastPasswordChangedAt = DateTime.Now;
            user.MustChangePassword = false;
            db.SaveChanges();
        }
    }
}
