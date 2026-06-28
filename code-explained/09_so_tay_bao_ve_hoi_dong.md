# Sổ tay bảo vệ đồ án: cách giải thích dự án trước hội đồng

File này không thay thế các tài liệu giải thích code chi tiết. Nó là bản ôn nhanh để trả lời câu hỏi khi bảo vệ đồ án.

## 1. Mô tả dự án trong 60 giây

Đề tài xây dựng phần mềm quản lý hàng hóa và bảo hành cho mô hình kho có quản lý serial. Hệ thống được phát triển bằng WPF .NET 8 theo mô hình MVVM, dùng Entity Framework Core kết nối SQL Server. Các nghiệp vụ chính gồm quản lý danh mục, sản phẩm, nhập kho, xuất kho, tồn kho, kiểm kê, điều chỉnh, hóa đơn mua/bán, bảo hành, báo cáo, phân quyền và audit log. Điểm trọng tâm của hệ thống là mọi biến động tồn kho đều đi qua nghiệp vụ ghi sổ, cập nhật `StockBalance`, ghi `StockLedger` và lưu audit để truy vết.

## 2. Kiến trúc tổng thể

```text
Views (XAML)
  -> ViewModels (ObservableObject, RelayCommand)
  -> Services (nghiệp vụ ứng dụng)
  -> Inventory domain layer (posting, adjustment, unit of work)
  -> AppDbContext (EF Core)
  -> SQL Server
```

Giải thích ngắn:

- View chỉ hiển thị và binding.
- ViewModel quản lý trạng thái màn hình, command và validate UI.
- Service xử lý nghiệp vụ và transaction.
- Inventory layer giữ quy tắc tồn kho cốt lõi.
- EF Core lưu dữ liệu vào SQL Server.

## 3. Các bảng quan trọng nhất

| Bảng | Ý nghĩa |
|---|---|
| `Product` | Sản phẩm, giá, đơn vị mặc định, có quản lý serial hay không |
| `ProductSerial` | Từng serial/IMEI cụ thể, trạng thái và kho hiện tại |
| `StockBalance` | Số dư tồn hiện tại theo sản phẩm và kho |
| `StockLedger` | Lịch sử biến động tồn kho |
| `StockIn`, `StockInLine` | Phiếu nhập kho và dòng chi tiết |
| `StockOut`, `StockOutLine` | Phiếu xuất kho và dòng chi tiết |
| `StockAdjustment`, `StockAdjustmentLine` | Phiếu điều chỉnh kho |
| `StockCountSession`, `StockCountLine` | Phiên kiểm kê và dòng kiểm kê |
| `SalesInvoice`, `PurchaseInvoice` | Hóa đơn bán/mua |
| `WarrantyCoverage`, `WarrantyClaim` | Quyền bảo hành và yêu cầu bảo hành |
| `AuditLog` | Nhật ký thao tác người dùng |
| `AppUser` | Tài khoản, vai trò, bảo mật đăng nhập |

## 4. Luồng ghi sổ nhập kho

```text
Người dùng nhập phiếu
  -> SaveDraft()
  -> ConfirmAndPost()
  -> StockInService.Post()
  -> BeginTransaction()
  -> validate dòng hàng và serial
  -> InventoryPostingService.PostStockIn()
  -> tăng StockBalance
  -> tạo ProductSerial nếu cần
  -> ghi StockLedger
  -> ghi AuditLog
  -> Commit()
```

Ý cần nhấn mạnh: trước khi post, phiếu là nháp và có thể sửa. Sau khi post, chứng từ bị khóa để đảm bảo lịch sử tồn kho không bị thay đổi tùy tiện.

## 5. Luồng ghi sổ xuất kho

```text
Người dùng lập phiếu xuất
  -> chọn khách hàng, kho, sản phẩm
  -> nhập serial nếu sản phẩm cần serial
  -> SaveDraft()
  -> StockOutService.Post()
  -> kiểm tra đủ tồn khả dụng
  -> kiểm tra serial đang InStock ở đúng kho
  -> giảm StockBalance
  -> cập nhật serial thành Sold/Out
  -> ghi StockLedger
  -> ghi AuditLog
```

Ý cần nhấn mạnh: hệ thống không cho xuất quá tồn và không cho xuất serial không hợp lệ.

## 6. Vì sao cần `StockBalance` và `StockLedger`?

Trả lời mẫu:

`StockBalance` phục vụ truy vấn nhanh số tồn hiện tại. `StockLedger` phục vụ truy vết lịch sử. Nếu chỉ có `StockBalance`, ta biết còn bao nhiêu nhưng không biết vì sao còn như vậy. Nếu chỉ có `StockLedger`, mỗi lần xem tồn phải cộng lại toàn bộ lịch sử, chậm hơn. Vì vậy hệ thống dùng cả hai: một bảng tối ưu hiện tại, một bảng tối ưu lịch sử.

## 7. Vì sao quản lý serial phức tạp hơn quản lý số lượng?

Sản phẩm không serial chỉ cần kiểm tra số lượng. Sản phẩm có serial phải kiểm tra từng đơn vị hàng cụ thể:

- Serial có tồn tại không?
- Serial thuộc đúng sản phẩm không?
- Serial đang ở kho nào?
- Serial đang `InStock`, đã bán hay đang bảo hành?
- Serial đã có quyền bảo hành chưa?

Nhờ serial, hệ thống truy được vòng đời từng thiết bị từ nhập kho, bán hàng đến bảo hành.

## 8. Luồng bảo hành và đổi mới

```text
Hóa đơn bán tạo WarrantyCoverage theo serial
  -> khách gửi yêu cầu bảo hành
  -> WarrantyClaimService xử lý trạng thái
  -> nếu đổi mới, serial cũ bị ngưng coverage
  -> serial mới được tạo coverage với thời hạn còn lại
  -> audit lại thay đổi
```

Ý cần nhấn mạnh: quyền bảo hành đi theo serial và hóa đơn bán, không đi chung chung theo sản phẩm.

## 9. Kiểm kê và điều chỉnh

Trả lời mẫu:

Kiểm kê không tự cập nhật tồn kho ngay. Nó ghi nhận số hệ thống, số thực tế và chênh lệch. Khi xử lý chênh lệch, hệ thống tạo phiếu nhập/xuất điều chỉnh dạng nháp. Người dùng kiểm tra lại rồi mới ghi sổ. Cách này giảm rủi ro nhập nhầm số kiểm kê làm sai tồn kho.

Điều chỉnh kho trực tiếp dùng khi đã có lý do rõ như hỏng, mất, hết hạn. Khi ghi sổ, nó gọi `InventoryAdjustmentService` để cập nhật tồn và ghi ledger.

## 10. Hóa đơn và bảo hành

Trả lời mẫu:

Phiếu xuất kho xác nhận hàng rời kho, còn hóa đơn bán xác nhận giao dịch bán với khách. Khi lưu hóa đơn bán có liên kết phiếu xuất, hệ thống lấy các serial đã xuất và tạo quyền bảo hành cho từng serial. Như vậy bảo hành bắt đầu từ ngày hóa đơn và gắn với khách hàng cụ thể.

## 11. Dashboard và báo cáo

Dashboard:

- Hiển thị KPI nhanh.
- Dùng `DashboardService`.
- Có biểu đồ LiveCharts2.
- Có command điều hướng nhanh đến module liên quan.

Báo cáo:

- Phân tích theo kỳ.
- Có doanh thu/lợi nhuận.
- Có xuất nhập tồn.
- Có thẻ kho chi tiết.
- Có truy vết serial.

Trả lời mẫu:

Dashboard phục vụ quản lý nhìn nhanh tình hình hiện tại. Báo cáo phục vụ phân tích, đối chiếu và in/tra cứu chi tiết theo kỳ.

## 12. Bảo mật và phân quyền

Các ý chính:

- Mật khẩu dùng BCrypt.
- Đăng nhập sai nhiều lần có chính sách khóa tạm thời.
- `AuthorizationService` kiểm tra quyền theo hành động.
- Menu người dùng và audit log chỉ hiển thị theo quyền.
- Mọi thao tác quan trọng được ghi `AuditLog`.

Trả lời mẫu:

Hệ thống không chỉ kiểm tra đăng nhập, mà còn phân quyền theo vai trò và hành động. Ví dụ quản lý người dùng hoặc xem audit log chỉ dành cho user có quyền tương ứng.

## 13. Import dữ liệu

Các loại import:

- CSV/Excel danh mục.
- Import động theo mapping cột.
- Import tồn đầu kỳ.
- Seed dữ liệu mẫu.

Trả lời mẫu:

Import tồn đầu kỳ không cập nhật trực tiếp vào bảng tồn mà tạo chứng từ nhập kho mục đích `OpeningBalance`, sau đó đi qua inventory posting. Nhờ vậy số dư đầu kỳ vẫn có chứng từ và ledger để truy vết.

## 14. Kiểm thử

Các nhóm test:

- Unit test domain inventory.
- Service tests cho nghiệp vụ.
- ViewModel tests cho logic giao diện.
- Import tests.
- Một số real database integration tests.

Kết quả rà gần nhất:

```text
100 tests
93 passed
7 failed do lỗi môi trường SQL Server encryption ở nhóm real DB
```

Trả lời mẫu:

Phần lớn test nghiệp vụ chạy độc lập bằng SQLite hoặc test double. Một số test kết nối SQL Server thật phụ thuộc môi trường, nên khi chạy trên máy chưa cấu hình đúng encryption sẽ fail. Đây là vấn đề môi trường test, không phải lỗi assertion nghiệp vụ.

## 15. Các điểm cần thành thật nếu hội đồng hỏi hạn chế

- Một số màn đã có code nhưng chưa bật trên sidebar chính.
- Connection string SQL Server còn hardcode.
- Startup đang dùng cả `EnsureCreated`, SQL thủ công và seed Excel, phù hợp demo nhưng cần chuẩn hóa migration cho production.
- Còn nhiều compiler warnings cần triage trước khi bàn giao thương mại.
- Một số màu theme còn tên `PrimaryPurpleBrush`, chưa khớp hoàn toàn guideline "Purple Ban".

## 16. Mười câu hỏi thường gặp và câu trả lời ngắn

### Câu 1: Vì sao chọn WPF?

Vì bài toán là phần mềm desktop nội bộ, cần giao diện nhập liệu nhiều, DataGrid, in/xuất Excel và chạy trên Windows. WPF hỗ trợ binding, MVVM và UI desktop tốt.

### Câu 2: Vì sao dùng MVVM?

MVVM tách View khỏi logic. ViewModel có thể test độc lập, còn XAML chỉ binding dữ liệu và command.

### Câu 3: Vì sao dùng EF Core?

EF Core giúp map object C# sang bảng SQL Server, viết query LINQ, quản lý quan hệ và transaction dễ hơn so với SQL thuần.

### Câu 4: Transaction dùng ở đâu?

Các nghiệp vụ ghi sổ như nhập kho, xuất kho, chuyển kho, điều chỉnh kho dùng transaction để đảm bảo hoặc tất cả bảng cùng cập nhật thành công, hoặc rollback toàn bộ.

### Câu 5: Audit có lưu trước/sau không?

Có. Nhiều service serialize dữ liệu trước và sau thao tác vào `BeforeJson` và `AfterJson`, sau đó `AuditLogViewModel` hiển thị diff.

### Câu 6: Làm sao tránh xuất quá tồn?

Inventory layer kiểm tra `StockBalance.AvailableQuantity` và trạng thái serial trước khi cho post xuất kho.

### Câu 7: Làm sao biết một serial đang ở đâu?

`ProductSerial` lưu `CurrentWarehouseId`, `CurrentStatus`, link dòng nhập/xuất gần nhất và warranty coverage. Báo cáo truy vết serial đọc các link này.

### Câu 8: Vì sao ViewModel có `IRefreshable`?

MainViewModel cache view để không tạo lại màn hình liên tục. Khi quay lại màn hình cũ, nếu DataContext implement `IRefreshable`, hệ thống gọi `RefreshData()` để dữ liệu không bị cũ.

### Câu 9: Vì sao có `Func<AppDbContext>`?

Để mỗi thao tác tạo DbContext ngắn hạn, tránh giữ context lâu trong app desktop, giảm cache cũ và dễ test bằng context khác.

### Câu 10: Phần nào là đóng góp kỹ thuật nổi bật?

Nổi bật nhất là lõi tồn kho có ledger/balance/serial invariant, luồng ghi sổ có transaction, bảo hành tự gắn theo serial bán ra, và hệ thống báo cáo truy vết được lịch sử hàng hóa.
