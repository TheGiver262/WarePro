# INDEX — Tài liệu Giải thích Chi tiết Code

> Bộ tài liệu này giải thích **từng file, từng hàm, từng dòng code** của phần mềm WareHouse Pro. Đọc theo thứ tự từ 01 → 05 để hiểu từ tầng nền lên tầng giao diện.

---

## Danh sách tài liệu

| File | Nhóm | Nội dung |
|---|---|---|
| [01_khoi_dong_va_dieu_huong.md](./01_khoi_dong_va_dieu_huong.md) | Khởi động & Navigation | `App.xaml.cs`, `AppDbContext`, `AppUser`, `AuthenticationService`, `LoginViewModel`, `LoginView`, `MainWindow`, `MainViewModel` |
| [02_models_va_database.md](./02_models_va_database.md) | Models & Database | Tất cả Models, quan hệ giữa các bảng, ý nghĩa từng field |
| [03_services_nghiep_vu.md](./03_services_nghiep_vu.md) | Services | `StockInService`, `AuthorizationService`, `DashboardService`, pattern chung của các service |
| [04_viewmodels_logic_giaodien.md](./04_viewmodels_logic_giaodien.md) | ViewModels | MVVM Toolkit internals, `StockInLineEditor`, `StockInViewModel` chi tiết, pattern chung |
| [05_views_va_inventory_layer.md](./05_views_va_inventory_layer.md) | Views & Inventory | Code-behind Views, `SerialInputWindow`, `InventoryPostingService`, interface ports |

---

## Sơ đồ kiến trúc tổng quan

```
┌─────────────────────────────────────────────────────────────────┐
│                           SQL Server                            │
│  ProductManagementDb (StockIn, StockLedger, StockBalance, ...)  │
└───────────────────────────────┬─────────────────────────────────┘
                                │ EF Core ORM
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                   Data/AppDbContext.cs                          │
│  DbSet<Product>, DbSet<StockIn>, DbSet<StockLedger>, ...        │
└───────────────────────────────┬─────────────────────────────────┘
                                │ Func<AppDbContext> factory
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                       Services/                                 │
│  StockInService  StockOutService  AuthenticationService ...     │
│       └──────────── Inventory/InventoryPostingService ──────┘   │
└───────────────────────────────┬─────────────────────────────────┘
                                │ gọi hàm service
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                       ViewModels/                               │
│  MainViewModel  StockInViewModel  DashboardViewModel ...        │
│  (ObservableObject + RelayCommand + ObservableProperty)         │
└───────────────────────────────┬─────────────────────────────────┘
                                │ DataContext binding
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                         Views/                                  │
│  MainWindow.xaml  StockInView.xaml  SerialInputWindow.xaml ...  │
│           (XAML binding + minimal code-behind)                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Luồng thực thi cốt lõi khi "Ghi sổ phiếu nhập kho"

```
1. [User] Bấm nút "Xác nhận ghi sổ" trên StockInView.xaml
2. [WPF]  Binding gọi StockInViewModel.ConfirmAndPostCommand
3. [VM]   ValidateForm() — kiểm tra bắt buộc phải có kho, có dòng sản phẩm
4. [VM]   StockInService.SaveDraft() — lưu draft mới nhất
5. [VM]   MessageBox.Show("Xác nhận?") — hỏi người dùng
6. [VM]   StockInService.Post(stockInId, userId)
7. [Svc]  db.BeginTransaction()
8. [Svc]  Validate: serial đủ số lượng, serial chưa tồn tại, không trùng trong phiếu
9. [Svc]  stockIn.Status = "Posted"; db.SaveChanges()
10.[Svc]  foreach line: InventoryPostingService.PostStockIn(command)
11.[Inv]  GetOrCreateBalance() → StockBalance.OnHand += qty; Available += qty
12.[Inv]  foreach serial: ProductSerial(Status=InStock) → INSERT vào DB
13.[Inv]  StockLedger → INSERT record (SourceDoc=StockIn, Direction=IN)
14.[Inv]  AuditLog → INSERT record (PostStockIn)
15.[Inv]  Commit() → db.SaveChanges()
16.[Svc]  Bind serial.LastStockInLineId = line.Id
17.[Svc]  db.SaveChanges()
18.[Svc]  AddAudit(db, "UPDATE", stockInId, beforeJson, afterJson)
19.[Svc]  transaction.Commit() ← Toàn bộ commit vào SQL Server
20.[VM]   IsPosted = true → CanEdit = false → UI lock form
21.[WPF]  Binding cập nhật UI (nút Sửa bị disabled)
22.[VM]   MessageBox.Show("Đã ghi sổ thành công")
```

---

## Các pattern quan trọng cần ghi nhớ

| Pattern | Ví dụ | Mục đích |
|---|---|---|
| **Context Factory** | `Func<AppDbContext>` | Mỗi thao tác dùng connection riêng, tránh leak |
| **`using var db`** | `using var db = _contextFactory()` | Tự động đóng connection sau khi xong |
| **`AsNoTracking()`** | `.AsNoTracking().ToList()` | Tối ưu tốc độ đọc, không theo dõi thay đổi |
| **Deferred Execution** | `.Where(...).Skip(...).Take(...).ToList()` | SQL chỉ chạy khi gọi `.ToList()` |
| **Transaction** | `db.BeginTransaction(); ... transaction.Commit()` | Bảo đảm atomicity cho ghi sổ |
| **`_isInitialized`** | Cờ trong ViewModel | Tránh filter chạy trước khi khởi tạo xong |
| **`_isLoading`** | Cờ trong ViewModel | Tránh load trùng lặp khi đang load |
| **`Dispatcher.Invoke()`** | Cập nhật UI từ thread nền | WPF chỉ cho update UI từ UI thread |
| **`[ObservableProperty]`** | MVVM Toolkit attribute | Auto-gen property + PropertyChanged |
| **`[RelayCommand]`** | MVVM Toolkit attribute | Auto-gen ICommand từ method |
| **View Cache** | `Dictionary<string, UserControl>` | Tránh tạo lại View khi quay lại màn hình |
| **DraftSerials** | `string? DraftSerials` trong StockInLine | Lưu serial tạm dưới dạng CSV trước khi Post |
| **record with { }** | `balance with { OnHand = x + n }` | Cập nhật immutable record (copy-on-write) |
| **IClock abstraction** | `IClock.Now` | Dễ test bằng fake time |

---

## Bảng tra cứu: Khi bấm nút X → hàm Y được gọi

| Nút / Hành động | Command/Event | Hàm được gọi |
|---|---|---|
| Đăng nhập | `LoginCommand` | `LoginViewModel.Login()` → `AuthenticationService.Authenticate()` |
| Menu "Nhập kho" | `OpenStockInViewCommand` | `MainViewModel.OpenStockInView()` |
| Tạo phiếu mới | `CreateNewCommand` | `StockInViewModel.CreateNew()` |
| Nhập serial | `OpenSerialInputCommand` | `StockInViewModel.OpenSerialInput()` → `SerialInputWindow.ShowDialog()` |
| Lưu nháp | `SaveDraftCommand` | `StockInViewModel.SaveDraft()` → `StockInService.SaveDraft()` |
| Ghi sổ | `ConfirmAndPostCommand` | `StockInViewModel.ConfirmAndPost()` → `StockInService.Post()` → `InventoryPostingService.PostStockIn()` |
| Xuất Excel | `ExportExcelCommand` | `StockInViewModel.ExportExcel()` |
| Đăng xuất | `LogoutCommand` | `MainViewModel.Logout()` |
| Đổi mật khẩu | `OpenChangePasswordViewCommand` | `MainViewModel.OpenChangePasswordView()` |
| Scroll xuống đáy | `ScrollChanged` event | `StockInView.xaml.cs` → `LoadMoreCommand` |
| Gõ vào ô tìm kiếm | `OnSearchDocumentCodeChanged()` | `StockInViewModel.LoadData()` |
