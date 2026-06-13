# Giải thích chi tiết mã nguồn: Phân hệ Đăng nhập & Bảo mật

Tài liệu này giải thích chi tiết về thuật toán, cấu trúc dữ liệu và logic xử lý của các tập tin liên quan đến tính năng Đăng nhập và Khóa tài khoản tạm thời (Lockout).

---

## 1. Các tập tin liên quan
* [AuthenticationService.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Services/AuthenticationService.cs): Lớp dịch vụ xử lý logic xác thực và ghi nhật ký kiểm toán (Audit Log).
* [LoginResult.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Services/LoginResult.cs): Lớp mô hình chứa kết quả trả về sau khi xác thực.
* [LoginViewModel.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/ViewModels/LoginViewModel.cs): ViewModel quản lý trạng thái giao diện đăng nhập và hiển thị thông báo lỗi.

---

## 2. Các Thuật toán & Cơ chế đặc biệt

### A. Thuật toán băm mật khẩu BCrypt
* **Sử dụng:** Lớp `BCrypt.Net.BCrypt` được dùng để xác thực mật khẩu.
* **Cơ chế:** BCrypt là thuật toán băm mật khẩu một chiều an toàn (chống tấn công brute-force và cầu vồng nhờ tích hợp salt tự động).
* **Đoạn code kiểm tra định dạng:**
  ```csharp
  if (stored.StartsWith("$2") && stored.Contains('$'))
  {
      verified = BCrypt.Net.BCrypt.Verify(password, stored);
  }
  ```
  *Ý nghĩa:* Trước khi gọi hàm `Verify` (rất tốn tài nguyên CPU do cấu hình chi phí băm), hệ thống kiểm tra chuỗi băm lưu trữ trong DB (`stored`) có đúng định dạng chuẩn của BCrypt hay không (bắt đầu bằng `$2` và chứa ký tự `$`). Nếu không đúng, bỏ qua để tránh ném ra ngoại lệ (Exception) hệ thống.

### B. Cơ chế Khóa tài khoản tạm thời (Lockout Policy)
Để ngăn chặn tấn công dò mật khẩu (Brute-force), hệ thống thiết lập cơ chế khóa tạm thời theo 2 ngưỡng:
* **Ngưỡng Soft Lockout (Sai từ 5 lần):**
  ```csharp
  if (user.FailedLoginCount >= 5)
  {
      user.LockoutUntil = DateTime.Now.AddMinutes(5);
      db.SaveChanges();
      WriteAudit(db, "AppUser", user.Id, "LoginLocked", user.Id);
      return LoginResult.Locked(user.LockoutUntil);
  }
  ```
  *Ý nghĩa:* Nếu người dùng nhập sai mật khẩu liên tiếp 5 lần, tài khoản sẽ bị tạm khóa trong **5 phút**. Hệ thống ghi lại thời điểm khóa vào trường `LockoutUntil` và ghi log kiểm toán hành vi là `LoginLocked`.
* **Ngưỡng Hard Lockout (Sai từ 10 lần):**
  ```csharp
  if (user.FailedLoginCount >= 10)
  {
      user.LockoutUntil = DateTime.Now.AddMinutes(15);
      db.SaveChanges();
      WriteAudit(db, "AppUser", user.Id, "SuspiciousLoginAttempt", user.Id);
      return LoginResult.Locked(user.LockoutUntil);
  }
  ```
  *Ý nghĩa:* Nếu vẫn tiếp tục nhập sai đạt đến 10 lần, thời gian khóa tăng lên **15 phút**. Hành vi này được phân loại là nguy hiểm và ghi log kiểm toán là `SuspiciousLoginAttempt` để người quản trị dễ dàng lọc và phát hiện các cuộc tấn công brute-force.
* **Reset trạng thái:**
  Khi người dùng đăng nhập thành công, hệ thống tự động reset `FailedLoginCount = 0` và xóa bỏ thời gian khóa `LockoutUntil = null`.

### C. So khớp Nhạy chữ (Case-Sensitive) cho Tên tài khoản
SQL Server mặc định sử dụng Collation không phân biệt chữ hoa/chữ thường (Case-Insensitive - ví dụ `SQL_Latin1_General_CP1_CI_AS`). Điều này khiến câu lệnh query của EF Core `db.AppUsers.FirstOrDefault(u => u.Username == username)` sẽ khớp cả chữ hoa và chữ thường.
Để đảm bảo tính bảo mật nghiêm ngặt cho tên đăng nhập, hệ thống thực hiện kiểm tra so khớp nhạy chữ trực tiếp trong bộ nhớ ứng dụng:
```csharp
if (user == null || user.Username != username)
{
    // ...
    return LoginResult.Invalid(0);
}
```
*Ý nghĩa:* So sánh chuỗi `user.Username != username` trong C# thực hiện so khớp chính xác từng ký tự hoa/thường. Nếu người dùng nhập sai định dạng chữ hoa/thường của tên tài khoản, hệ thống sẽ coi là không tìm thấy tài khoản.

---

## 3. Các Phương thức đặc biệt

### Lớp `AuthenticationService`

#### Phương thức `WriteAudit`
```csharp
private void WriteAudit(AppDbContext db, string entityName, int entityId, string actionCode, int performedBy)
{
    try
    {
        db.AuditLogs.Add(new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            ActionCode = actionCode,
            PerformedBy = performedBy,
            PerformedAt = DateTime.Now
        });
        db.SaveChanges();
    }
    catch
    {
        // Tránh lỗi ghi log làm gián đoạn luồng chính
    }
}
```
* **Mục đích:** Ghi nhận nhật ký kiểm toán cho các sự kiện đăng nhập.
* **Hàm đặc biệt:** Khối `try-catch` rỗng được bao bọc xung quanh lệnh `db.SaveChanges()`. Đây là một thiết kế phòng vệ quan trọng: việc ghi nhật ký hệ thống (Audit Log) là tác vụ phụ trợ, nếu database ghi log bị lỗi (ví dụ do khóa bảng tạm thời), nó không được phép làm crash hoặc gián đoạn luồng xử lý chính của người dùng (Đăng nhập).

#### Phương thức `Authenticate`
* **Mục đích:** Nhận vào `username` và `password`, thực hiện toàn bộ luồng kiểm tra: tìm user, so khớp nhạy chữ, kiểm tra trạng thái hoạt động (`IsActive`), kiểm tra thời gian khóa (`LockoutUntil`), kiểm tra mật khẩu qua BCrypt, tăng số lần sai và cập nhật trạng thái khóa.

---

## 4. Đồng bộ hóa thông báo lỗi bảo mật (LoginViewModel)
Trong lớp `LoginViewModel.cs`, luồng xử lý lỗi đăng nhập được thiết kế để ẩn giấu thông tin chi tiết hệ thống:
```csharp
case LoginStatus.LockedOut:
    ErrorMessage = "Tên tài khoản hoặc mật khẩu không đúng hoặc tài khoản đang tạm khóa!";
    break;

case LoginStatus.Inactive:
    ErrorMessage = "Tên tài khoản hoặc mật khẩu không đúng!";
    break;

case LoginStatus.InvalidCredentials:
default:
    if (result.FailedLoginCount >= 3 && result.FailedLoginCount < 5)
    {
        ErrorMessage = "Tên tài khoản hoặc mật khẩu không đúng!\n(Nhập sai tên đăng nhập/mật khẩu liên tiếp sẽ bị khóa tài khoản tạm thời)";
    }
    else
    {
        ErrorMessage = "Tên tài khoản hoặc mật khẩu không đúng!";
    }
    break;
```
* **Nguyên lý bảo mật:** Hệ thống tuyệt đối không thông báo kiểu *"Tài khoản này không tồn tại"* hoặc *"Sai mật khẩu"* vì kẻ tấn công có thể lợi dụng điều này để dò tìm (enumerate) danh sách tài khoản đang tồn tại trên hệ thống. 
* **Cảnh báo Soft Lockout:** Khi người dùng nhập sai liên tiếp từ 3 đến 4 lần (`FailedLoginCount >= 3`), giao diện hiển thị thêm dòng nhắc nhở nhỏ để cảnh báo rằng tài khoản sắp bị khóa nếu tiếp tục nhập sai, giúp cải thiện trải nghiệm người dùng thật mà không làm yếu bảo mật.
