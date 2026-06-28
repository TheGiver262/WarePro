# Nhóm 8: Audit, import dữ liệu và kiểm thử

File này gom ba năng lực hỗ trợ chất lượng hệ thống: truy vết thay đổi, nhập dữ liệu hàng loạt và kiểm thử tự động.

## 1. Audit và truy vết

### 1.1 Hai loại lịch sử trong hệ thống

Hệ thống có hai nguồn lịch sử khác nhau:

| Nguồn | Bảng | Mục đích |
|---|---|---|
| Nhật ký thao tác | `AuditLog` | Ai đã tạo/sửa/xóa/ghi sổ đối tượng nào, trước/sau ra sao |
| Sổ kho | `StockLedger` | Tồn kho thay đổi thế nào, sản phẩm nào, kho nào, số lượng bao nhiêu |

Khi hội đồng hỏi "hệ thống truy vết được không?", cần trả lời rõ: audit log truy vết hành động người dùng, còn stock ledger truy vết biến động tồn kho. Hai nguồn này bổ sung cho nhau.

### 1.2 `ReportTraceService / AuditLogService`

`ReportTraceService / AuditLogService` cung cấp các nhóm truy vấn:

- `GetEntityTimeline(entityName, entityId)`: gom `AuditLog` và `StockLedger` theo một chứng từ.
- `GetProductLedger(productId)`: lấy lịch sử ledger của một sản phẩm.
- `GetDocumentTimeline(Guid documentId)`: truy vấn timeline theo document id dạng Guid.
- `GetAllAuditLogs(...)`: lọc audit log theo entity, action, user, ngày, từ khóa.
- `GetLogsBefore`, `GetLogsBetween`, `DeleteLogs`: phục vụ lưu trữ/xóa log cũ.

DTO trung gian là `AuditTimelineEntry`, gồm:

- `Kind`: `Audit` hoặc `StockLedger`.
- `EntityId`.
- `OccurredAt`.
- `Action`.
- `UserId`.
- `ProductId`.
- `WarehouseId`.
- `Quantity`.

### 1.3 `AuditLogViewModel`

Màn nhật ký hệ thống cho phép:

- Lọc theo đối tượng.
- Lọc theo hành động.
- Lọc theo người thực hiện.
- Lọc theo khoảng ngày.
- Tìm kiếm nhanh.
- Xem chi tiết thay đổi trước/sau.
- Xuất Excel.
- Lưu trữ log cũ ra Excel rồi xóa khỏi DB.

Khi chọn một log, `GenerateDetailedResult()` cố gắng diễn giải log thành câu dễ hiểu, ví dụ:

```text
Nguyễn Văn A đã chỉnh sửa sản phẩm: SP001
--- CHI TIẾT ---
DisplayName: Cũ -> Mới
```

Nếu `BeforeJson` và `AfterJson` đều có dữ liệu, hàm `GenerateDiff()` so sánh từng key để chỉ ra trường nào thay đổi.

### 1.4 Lưu trữ audit log

`ConfirmArchive()` kiểm tra:

- Chỉ lưu trữ dữ liệu cũ hơn năm hiện tại.
- Ngày bắt đầu không lớn hơn ngày kết thúc.
- Có log trong khoảng đã chọn.

Sau đó luồng là:

```text
GetLogsBetween()
  -> ExportLogsToExcel()
  -> DeleteLogs()
  -> LoadLogs()
```

Đây là thiết kế phù hợp cho demo đồ án vì vừa giữ khả năng xuất báo cáo kiểm toán, vừa tránh bảng log tăng mãi.

### 1.5 `ReportViewModel`

Màn truy vấn audit theo timeline tập trung vào:

- Lịch sử ledger của một sản phẩm.
- Timeline theo document id.

Lưu ý hiện tại `LoadDocumentTimeline()` yêu cầu `Guid`, trong khi nhiều chứng từ trong hệ thống đang dùng `int Id`. Vì vậy phần truy vấn document timeline dạng Guid là hướng mở rộng, còn phần ổn định hơn là truy vết theo sản phẩm qua `GetProductLedger(productId)`.

## 2. Import dữ liệu

### 2.1 Các file chính

| File | Vai trò |
|---|---|
| `CsvImportService.cs` | Đọc CSV thành dữ liệu có cấu trúc |
| `ExcelImportService.cs` | Đọc Excel bằng ClosedXML |
| `FileClassificationService.cs` | Nhận diện loại file import |
| `DynamicImportService.cs` | Import động theo định nghĩa field và mapping |
| `DataImportManager.cs` | Điều phối import |
| `DatabaseSeeder.cs` | Seed dữ liệu mẫu từ Excel |
| `OpeningBalanceImportService.cs` | Import tồn đầu kỳ và ghi vào chứng từ kho |
| `OpeningBalanceImportViewModel.cs` | UI mapping cột và chạy import tồn đầu kỳ |

### 2.2 Import Excel/CSV cơ bản

`ExcelImportService` và `CsvImportService` có trách nhiệm đọc file ngoài thành model trung gian. Việc tách reader ra khỏi nghiệp vụ giúp:

- Dễ test parsing file.
- Dễ thay đổi định dạng import.
- Không trộn logic đọc file với logic ghi database.

### 2.3 Import động

`DynamicImportService` dùng metadata field để map cột file vào entity. Luồng ý tưởng:

```text
File Excel/CSV
  -> đọc header
  -> map header với field định nghĩa
  -> validate từng dòng
  -> mở transaction
  -> insert/update dữ liệu
  -> commit hoặc rollback
```

Ưu điểm của hướng này là có thể mở rộng import nhiều loại danh mục mà không phải viết lại toàn bộ code cho từng file.

### 2.4 Import tồn đầu kỳ

Tồn đầu kỳ là loại import nhạy cảm hơn import danh mục, vì nó ảnh hưởng tồn kho. `OpeningBalanceImportService` xử lý theo hướng chứng từ:

1. Đọc các dòng tồn đầu kỳ.
2. Tạo chứng từ nhập kho với `PurposeCode = "OpeningBalance"`.
3. Tạo dòng nhập kho tương ứng.
4. Gọi inventory posting để tăng tồn.
5. Nếu sản phẩm có serial, tạo/cập nhật serial và liên kết với dòng nhập.
6. Nếu lỗi, rollback và cleanup.

Thiết kế này tốt hơn việc update thẳng `StockBalance` vì mọi số dư đầu kỳ đều có chứng từ và ledger để truy vết.

## 3. Kiểm thử tự động

### 3.1 Tổng quan test project

Test project nằm ở `QuanLyHangHoa.Tests`. Các nhóm test chính:

- `Inventory/*`: kiểm thử lõi tồn kho, posting, serial validation, unit of work.
- `Services/*`: kiểm thử service nghiệp vụ như auth, authorization, stock in/out, count, adjustment, invoice, warranty.
- `ViewModels/*`: kiểm thử logic ViewModel không phụ thuộc UI thật.
- `CsvImportServiceTests.cs`, `ExcelImportServiceTests.cs`: kiểm thử đọc file.

### 3.2 Kết quả chạy gần nhất

Lệnh dùng để lấy kết quả gọn:

```powershell
rtk dotnet test .\QuanLyHangHoa.Tests\QuanLyHangHoa.Tests.csproj --no-build --logger "trx;LogFileName=codex-test-results.trx" --verbosity quiet
```

Kết quả:

```text
Total: 100
Passed: 93
Failed: 7
Skipped: 0
```

7 test fail cùng nguyên nhân môi trường:

```text
The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.
```

Các test này thuộc nhóm kết nối SQL Server thật như:

- `RealDatabaseIntegrationTests`
- `Test_WarrantyDatabaseConstraints`
- `SeedTestData`
- `UnitTest1`

Ý nghĩa khi bảo vệ: test fail hiện không chứng minh nghiệp vụ sai, mà chứng minh nhóm real database test phụ thuộc cấu hình SQL Server/máy chạy. Các test dùng SQLite/test double vẫn chạy qua.

### 3.3 Vì sao dùng SQLite trong nhiều test?

SQLite in-memory giúp test nhanh, độc lập và không cần SQL Server thật. Nó phù hợp để kiểm tra:

- Logic service.
- Mapping EF cơ bản.
- Transaction đơn giản.
- Ràng buộc nghiệp vụ.

SQL Server thật vẫn cần cho một số test tích hợp vì SQL Server có behavior riêng về constraint, kiểu dữ liệu, collation, encryption và migration.

### 3.4 Test double cho inventory

Các test trong `Inventory` có `InventoryTestDoubles.cs` để mô phỏng unit of work. Mục tiêu là test domain logic mà không cần database:

- Nhập kho tăng tồn.
- Xuất kho giảm tồn.
- Không cho tồn âm.
- Serial không được trùng.
- Serial phải ở đúng trạng thái.
- Điều chỉnh kho tạo ledger đúng.

Đây là bằng chứng kiến trúc tốt: nghiệp vụ lõi được tách khỏi EF Core nên có thể test độc lập.

### 3.5 Một số test tiêu biểu để nhắc khi bảo vệ

| Nhóm test | Ý nghĩa |
|---|---|
| `PostStockInTests` | Chứng minh ghi sổ nhập kho cập nhật tồn, ledger và serial. |
| `PostStockOutTests` | Chứng minh xuất kho kiểm tra đủ tồn và trạng thái serial. |
| `PostStockAdjustmentTests` | Chứng minh điều chỉnh kho tăng/giảm tồn đúng. |
| `SerialValidationTests` | Chứng minh hệ thống bảo vệ serial không hợp lệ. |
| `StockDocumentLifecycleTests` | Chứng minh vòng đời chứng từ không cho chuyển trạng thái sai. |
| `InvoiceServiceTests` | Chứng minh tính tiền, thuế và trạng thái thanh toán. |
| `WarrantyClaimServiceTests` | Chứng minh đổi mới bảo hành cập nhật coverage. |
| `AuthenticationServiceTests` | Chứng minh đăng nhập và chính sách khóa tài khoản. |
| `AuthorizationServiceTests` | Chứng minh phân quyền theo vai trò. |
| `OpeningBalanceImportServiceTests` | Chứng minh import tồn đầu kỳ đi qua chứng từ và inventory. |

## 4. Câu trả lời mẫu khi bảo vệ

**Hỏi:** Audit log khác stock ledger như thế nào?

**Trả lời:** Audit log ghi nhận thao tác của người dùng lên dữ liệu, ví dụ tạo/sửa/xóa/ghi sổ. Stock ledger ghi nhận biến động tồn kho, ví dụ sản phẩm nào tăng/giảm bao nhiêu ở kho nào. Khi cần điều tra một chứng từ kho, hệ thống có thể kết hợp cả hai để biết ai thao tác và tồn kho thay đổi thế nào.

**Hỏi:** Import tồn đầu kỳ có update trực tiếp vào bảng tồn không?

**Trả lời:** Không. Import tồn đầu kỳ tạo chứng từ nhập kho với mục đích `OpeningBalance`, sau đó đi qua inventory posting. Nhờ vậy số dư đầu kỳ vẫn có ledger và audit, không bị mất dấu vết.

**Hỏi:** Vì sao test có 7 fail?

**Trả lời:** 7 fail nằm ở nhóm test kết nối SQL Server thật và lỗi là do cấu hình encryption của môi trường chạy. Những test nghiệp vụ chính dùng SQLite/test double vẫn pass. Khi triển khai CI, nên tách nhóm real DB test thành category riêng và cấu hình SQL Server test container hoặc connection string phù hợp.
