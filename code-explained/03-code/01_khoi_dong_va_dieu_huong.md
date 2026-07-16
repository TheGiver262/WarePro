# NHÓM 1: KHỞI ĐỘNG & ĐIỀU HƯỚNG

> Tài liệu này giải thích chi tiết từng dòng code của các file chịu trách nhiệm khởi động ứng dụng, đăng nhập và điều hướng giữa các màn hình.

> **Cập nhật kiến trúc từ commit `895a70a`:** `App.xaml.cs` không còn trực tiếp coi `EnsureCreated` + seed là toàn bộ startup. App xử lý first-run SQL credential, gọi `StartupCoordinator`, probe SQL và chạy `DatabaseInitializer` có compatibility gate, `SchemaUpgradeLock`, verified backup, schema update và seed. Phần mô tả `EnsureCreated`/SQL thủ công bên dưới chỉ giải thích cơ chế legacy còn được `DatabaseInitializer` bao bọc; xem [chương 14](./14_cai_dat_cap_nhat_phat_hanh_de_hieu.md) cho luồng hiện hành đầy đủ.

Luồng hiện hành:

```text
App.OnStartup -> FirstRunCredentialCoordinator -> StartupCoordinator
-> ProbeSqlAsync -> DatabaseInitializer -> LoginView
```

---

## 1. `App.xaml.cs` — Điểm khởi động đầu tiên

**Vai trò:** WPF gọi file này đầu tiên khi ứng dụng chạy. File này điều phối credential, startup và cửa sổ đăng nhập; logic database chi tiết nằm trong `StartupCoordinator` và `DatabaseInitializer`.

```csharp
protected override void OnStartup(StartupEventArgs e)
```
- `OnStartup` là phương thức WPF gọi ngay sau khi process khởi động, **trước khi bất kỳ cửa sổ nào hiện ra**.
- `base.OnStartup(e)` — gọi logic mặc định của WPF (bắt buộc).

### Bước 1: Đảm bảo Database tồn tại

```csharp
using (var db = new AppDbContext())
{
    db.Database.EnsureCreated();
```
- Tạo một instance `AppDbContext`.
- `EnsureCreated()` → EF Core kiểm tra DB trên SQL Server. **Nếu chưa có**, nó tạo toàn bộ bảng theo định nghĩa trong `OnModelCreating`. **Nếu đã có**, nó không làm gì cả (không chạy migration).

### Bước 2: Migration thủ công (Manual Migration)

```csharp
var connection = db.Database.GetDbConnection();
connection.Open();
using (var command = connection.CreateCommand())
{
    var migrations = new[] { ... };
    foreach (var sql in migrations) { command.ExecuteNonQuery(); }
}
```
- **Vấn đề cốt lõi:** `EnsureCreated()` chỉ tạo DB từ đầu. Nếu DB **đã tồn tại** nhưng thiếu cột mới (do cập nhật code), EF Core sẽ không thêm cột đó.
- **Giải pháp:** Mảng `migrations` chứa các câu SQL kiểu:
  - `IF COL_LENGTH('Product','Description') IS NULL ALTER TABLE Product ADD Description NVARCHAR(MAX)` → kiểm tra cột, nếu chưa có thì thêm.
  - `IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='StockTransfer') CREATE TABLE StockTransfer (...)` → kiểm tra bảng trước khi tạo.
- Mỗi SQL được bọc trong `try/catch` riêng → nếu một lệnh lỗi, các lệnh còn lại vẫn chạy.

### Bước 3: Seed dữ liệu mẫu

```csharp
string excelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "warepro_database_seed.xlsx");
if (!File.Exists(excelPath))
    excelPath = @"f:\Codex Project\...\warepro_database_seed.xlsx"; // fallback dev
```
- Tìm file Excel seed trong thư mục `Database/` cạnh file `.exe`.
- **Fallback:** Nếu không tìm thấy (môi trường dev), dùng đường dẫn tuyệt đối hardcode.

```csharp
var seeder = new Services.DataImport.DatabaseSeeder(db, excelPath);
var log = Task.Run(async () => await seeder.SeedAsync()).GetAwaiter().GetResult();
```
- `Task.Run(...).GetAwaiter().GetResult()` — chạy async code trong context đồng bộ (vì `OnStartup` không phải là `async`).
- `DatabaseSeeder.SeedAsync()` đọc file Excel và đồng bộ từng bảng theo mã nghiệp vụ. Dòng đã tồn tại được dùng để dựng lại ánh xạ ID; dòng còn thiếu mới được thêm vào DB.
- Sau khi đồng bộ `Product`, seeder đọc sheet `ProductUnit`, đổi `ProductId`/`UnitId` trong Excel sang ID thật trong DB rồi chỉ thêm cặp `(ProductId, UnitId)` còn thiếu.
- Dữ liệu quy đổi người dùng đã tạo không bị ghi đè. Seeder cũng bỏ qua đơn vị cơ sở mới nếu sản phẩm đã có đơn vị cơ sở và từ chối `ConversionFactor <= 0`.

---

## 2. `Data/AppDbContext.cs` — Cầu nối tới SQL Server

**Vai trò:** Lớp trung gian giữa C# và SQL Server. Mọi thao tác đọc/ghi DB đều đi qua class này.

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    if (!optionsBuilder.IsConfigured)
        optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=ProductManagementDb;...");
}
```
- Connection string mặc định: SQL Server Express cài local, dùng Windows Authentication (`Trusted_Connection=True`).
- `if (!optionsBuilder.IsConfigured)` — chỉ áp dụng khi không có config từ bên ngoài (tương thích với Unit Test).

### Các DbSet (= bảng trong DB)

```csharp
public virtual DbSet<Product> Products { get; set; }
public virtual DbSet<StockLedger> StockLedgers { get; set; }
// ... và 20+ DbSet khác
```
- Mỗi `DbSet<T>` tương ứng với một bảng. Ví dụ: `db.Products` = SELECT từ bảng `Product`.
- Gọi `db.Products.ToList()` → EF Core tự tạo câu SQL `SELECT * FROM Product` và trả về List.

### `OnModelCreating` — Cấu hình bảng nâng cao

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    var isSqlite = Database.ProviderName?.Contains("Sqlite") ?? false;
    var defaultDateTime = isSqlite ? "CURRENT_TIMESTAMP" : "sysutcdatetime()";
```
- Phần này cấu hình chi tiết: tên bảng, khóa chính, index, ràng buộc FK, giá trị mặc định.
- Hỗ trợ cả SQLite (dùng cho test) và SQL Server (production).

---

## 3. `Models/AppUser.cs` — Model người dùng hệ thống

**Vai trò:** Ánh xạ bảng `AppUser` trong DB. Lưu thông tin tài khoản, quyền hạn, trạng thái khóa.

| Property | Kiểu | Ý nghĩa |
|---|---|---|
| `Username` | string | Tên đăng nhập (unique, case-sensitive) |
| `PasswordHash` | string | Hash BCrypt của mật khẩu (không bao giờ lưu plaintext) |
| `RoleCode` | string | `"Admin"` hoặc `"Staff"` — phân quyền |
| `MustChangePassword` | bool | `true` = bắt đổi mật khẩu lần đăng nhập tiếp |
| `FailedLoginCount` | int | Số lần nhập sai liên tiếp |
| `LockoutUntil` | DateTime? | Thời điểm hết khóa (null = không bị khóa) |
| `IsActive` | bool | `false` = tài khoản bị vô hiệu hóa |
| `LastLoginAt` | DateTime? | Lần đăng nhập thành công gần nhất |

**Navigation properties** (EF Core tự JOIN):
```csharp
public virtual ICollection<StockIn> StockInCreators { get; set; }
public virtual ICollection<StockIn> StockInApprovers { get; set; }
public virtual ICollection<StockIn> StockInPosters { get; set; }
```
- Một user có thể là người tạo, phê duyệt hoặc ghi sổ của nhiều phiếu nhập kho khác nhau.

---

## 4. `Services/AuthenticationService.cs` — Logic đăng nhập

**Vai trò:** Xử lý toàn bộ logic xác thực. ViewModel không trực tiếp query DB — nó gọi service này.

### Hàm `Authenticate(username, password)`

**Bước 1: Tìm user**
```csharp
var user = db.AppUsers.FirstOrDefault(u => u.Username == username);
if (user == null || user.Username != username)
    return LoginResult.Invalid(0);
```
- Query DB lấy user theo username. Sau đó **kiểm tra lại case-sensitive** trong C# vì SQL Server mặc định không phân biệt hoa/thường.

**Bước 2: Kiểm tra trạng thái tài khoản**
```csharp
if (!user.IsActive) return LoginResult.Inactive();
if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.Now)
    return LoginResult.Locked(user.LockoutUntil);
```
- Tài khoản bị vô hiệu hóa → trả về `Inactive`.
- Đang trong thời gian khóa → trả về `Locked`.

**Bước 3: Xác minh mật khẩu BCrypt**
```csharp
if (stored.StartsWith("$2") && stored.Contains('$'))
    verified = BCrypt.Net.BCrypt.Verify(password, stored);
```
- Hash BCrypt bắt đầu bằng `$2a$` hoặc `$2b$`. Hàm `Verify()` tính lại hash từ mật khẩu nhập vào và so sánh với hash lưu trong DB.

**Bước 4: Xử lý kết quả**
```csharp
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
    user.FailedLoginCount++;
    if (user.FailedLoginCount >= 10) user.LockoutUntil = DateTime.Now.AddMinutes(15);
    else if (user.FailedLoginCount >= 5) user.LockoutUntil = DateTime.Now.AddMinutes(5);
    db.SaveChanges();
}
```
- Thành công → reset `FailedLoginCount = 0`, cập nhật `LastLoginAt`.
- Thất bại → tăng `FailedLoginCount`. Sai ≥5 lần → khóa 5 phút. Sai ≥10 lần → khóa 15 phút.

### Hàm `ChangePassword(userId, currentPassword, newPassword)`
```csharp
if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
    throw new InvalidOperationException("Mật khẩu hiện tại không chính xác.");
if (currentPassword == newPassword)
    throw new InvalidOperationException("Mật khẩu mới không được trùng với mật khẩu cũ.");
user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
user.MustChangePassword = false;
```
- Bắt buộc xác minh mật khẩu cũ trước khi cho đổi.
- Hash mật khẩu mới bằng BCrypt trước khi lưu.

### Hàm `WriteAudit(...)` — Ghi log bảo mật
```csharp
db.AuditLogs.Add(new AuditLog { ActionCode = "LoginFailed", ... });
db.SaveChanges();
```
- Mọi sự kiện đăng nhập đều được ghi vào bảng `AuditLog`.
- Bọc trong `try/catch` rỗng → lỗi ghi log không được phép làm sập chức năng đăng nhập.

---

## 5. `ViewModels/LoginViewModel.cs` — Logic giao diện đăng nhập

**Vai trò:** Cầu nối giữa form đăng nhập và `AuthenticationService`.

### Khai báo thuộc tính

```csharp
[ObservableProperty] private string _username = string.Empty;
[ObservableProperty] private string _password = string.Empty;
[ObservableProperty] private string _errorMessage = string.Empty;
```
- `[ObservableProperty]` → MVVM Toolkit tự sinh ra property `Username`, `Password`, `ErrorMessage` cùng với code `OnPropertyChanged` để View tự cập nhật khi giá trị thay đổi.
- `_username` (private, underscore) = backing field. `Username` (public, PascalCase) = property mà View bind vào.

### Constructor

```csharp
public LoginViewModel()
{
    _authService = new AuthenticationService(() => new Data.AppDbContext());
}
```
- Khởi tạo service với **factory function** `() => new AppDbContext()`.
- Mỗi lần `_contextFactory()` được gọi, nó tạo một `AppDbContext` **mới** — mỗi thao tác DB dùng một connection riêng biệt.

### Command `Login(Window currentWindow)`

```csharp
[RelayCommand]
private void Login(Window currentWindow)
```
- `[RelayCommand]` → MVVM Toolkit tự sinh ra `LoginCommand` (kiểu `ICommand`). View bind nút "Đăng nhập" vào command này.
- Nhận `currentWindow` để sau khi login thành công có thể đóng cửa sổ Login.

**Flow xử lý:**
```csharp
var result = _authService.Authenticate(Username, Password);
switch (result.Status)
{
    case LoginStatus.Success:
        MainWindow main = new MainWindow(result.User, () => new Data.AppDbContext());
        main.Show();
        if (result.User.MustChangePassword)
            mainVm.OpenChangePasswordViewCommand.Execute(null); // tự động chuyển màn hình đổi MK
        currentWindow?.Close();
        break;
    case LoginStatus.LockedOut:
        ErrorMessage = "...tài khoản đang tạm khóa!";
        break;
}
```

---

## 6. `Views/LoginView.xaml.cs` — Code-behind màn hình Login

**Vai trò:** Giải quyết giới hạn của WPF: `PasswordBox` không cho phép Data Binding vì lý do bảo mật.

```csharp
private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
{
    if (this.DataContext != null)
    {
        var vm = (LoginViewModel)this.DataContext;
        vm.Password = txtPassword.Password;  // gán mật khẩu sang ViewModel
    }
}
```
- Mỗi khi người dùng gõ ký tự, code lấy giá trị từ `PasswordBox.Password` và set vào `vm.Password`.

```csharp
private void BtnClose_Click(object sender, RoutedEventArgs e)
{
    Application.Current.Shutdown(); // tắt toàn bộ ứng dụng
}
```

---

## 7. `MainWindow.xaml.cs` — Code-behind cửa sổ chính

```csharp
public MainWindow(AppUser user, Func<Data.AppDbContext> contextFactory)
{
    InitializeComponent();
    this.DataContext = new MainViewModel(user, contextFactory);
}
```
- `InitializeComponent()` → parse XAML, tạo toàn bộ UI object.
- `DataContext = new MainViewModel(...)` → liên kết ViewModel với View. Mọi binding trong XAML đọc từ `MainViewModel`.

---

## 8. `ViewModels/MainViewModel.cs` — Bộ điều phối trung tâm

**Vai trò:** Quản lý toàn bộ điều hướng (Navigation). Đây là "controller" trung tâm của ứng dụng.

### `_viewCache` — Cache màn hình

```csharp
private readonly Dictionary<string, UserControl> _viewCache = new();
```
- Lưu các View đã tạo theo key (ví dụ: `"Dashboard"`, `"StockIn"`).
- **Tại sao cache?** Tránh tạo lại ViewModel và load data mỗi khi người dùng chuyển tab. Màn hình đang nhập dở được giữ nguyên trạng thái.

### Hàm `NavigateToView<TView>` — Logic điều hướng cốt lõi

```csharp
private void NavigateToView<TView>(string cacheKey, Func<TView> viewFactory, string title, string subtitle)
    where TView : UserControl
{
    if (!_viewCache.TryGetValue(cacheKey, out var view))
    {
        view = viewFactory();          // TẠO MỚI nếu chưa có trong cache
        _viewCache[cacheKey] = view;   // LƯU vào cache
    }
    else
    {
        if (view.DataContext is IRefreshable refreshable)
            refreshable.RefreshData(); // LÀM MỚI data nếu đã cache
    }
    CurrentView = view;                // HIỂN THỊ View mới (binding WPF tự cập nhật UI)
    CurrentViewTitle = title;
    CurrentViewSubtitle = subtitle;
}
```

**Logic:**
1. `TryGetValue(cacheKey, out var view)` → kiểm tra cache.
2. **Chưa có:** Gọi `viewFactory()` (lambda do mỗi command cung cấp) tạo View + ViewModel mới. Lưu cache.
3. **Đã có:** Kiểm tra `IRefreshable`. Nếu ViewModel implement interface này → gọi `RefreshData()` để cập nhật dữ liệu.
4. `CurrentView = view` → WPF binding trong `MainWindow.xaml` tự cập nhật `ContentControl`.

### Các Navigation Commands (pattern nhất quán)

```csharp
[RelayCommand]
private void OpenStockInView()
{
    NavigateToView("StockIn",
        () => new StockInView { DataContext = new StockInViewModel(CurrentUser, ContextFactory) },
        "NHẬP KHO", "Lập phiếu nhập kho và quản lý hàng nhập");
}
```
- Factory lambda chỉ chạy **1 lần** khi màn hình mở lần đầu.

**Kiểm tra quyền:**
```csharp
[RelayCommand]
private void OpenAppUserView()
{
    if (IsAdmin)
        NavigateToView("AppUser", ...);
    else
        MessageBox.Show("Bạn không có quyền truy cập!");
}
```

### `Logout()` Command

```csharp
[RelayCommand]
private void Logout()
{
    new LoginView().Show();   // mở màn hình Login mới
    foreach (Window window in Application.Current.Windows)
    {
        if (window is MainWindow) { window.Close(); break; } // đóng MainWindow
    }
}
```

---

## Sơ đồ luồng khởi động toàn bộ

```
App.xaml.cs::OnStartup()
    ├─ EnsureCreated()          → Tạo DB nếu chưa có
    ├─ Manual Migrations        → Thêm cột/bảng mới an toàn
    └─ DatabaseSeeder.SeedAsync() → Đồng bộ data mẫu còn thiếu theo mã
                ↓
    LoginView hiển thị (cửa sổ đầu tiên)
                ↓
    User gõ password → TxtPassword_PasswordChanged → vm.Password = ...
    User bấm Đăng nhập → LoginCommand
        → AuthenticationService.Authenticate()
            → AppDbContext.AppUsers.FirstOrDefault()
            → BCrypt.Verify()
        ← LoginResult.Success(user)
    new MainWindow(user, factory).Show()
    LoginView.Close()
                ↓
    MainWindow constructor
        → new MainViewModel(user, factory)
            → OpenDashboard()
                → NavigateToView("Dashboard", ...)
                    → new DashboardView { DataContext = new DashboardViewModel(...) }
                    → CurrentView = dashboardView  (WPF binding tự cập nhật UI)
```

---

## Tóm tắt nhanh

| File | Làm gì |
|---|---|
| `App.xaml.cs` | Khởi động app, tạo/migrate DB, seed data |
| `AppDbContext.cs` | Kết nối SQL Server, định nghĩa tất cả bảng |
| `AppUser.cs` | Model tài khoản người dùng |
| `AuthenticationService.cs` | BCrypt verify mật khẩu, lockout logic |
| `LoginViewModel.cs` | Logic form đăng nhập |
| `LoginView.xaml.cs` | Workaround PasswordBox binding |
| `MainWindow.xaml.cs` | Khởi tạo MainViewModel |
| `MainViewModel.cs` | Điều hướng toàn bộ màn hình, cache View |
| `StartupCoordinator.cs` | Đọc cấu hình, probe SQL và điều phối khởi tạo database |
| `Configuration/*` | Settings, path, connection string và Credential Manager |
