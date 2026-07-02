# 12 - Report, Audit, Import: đọc các phần hậu trường hệ thống

File này giải thích các phần khó trong:

- `QuanLyHangHoa/Services/ReportTraceService.cs`
- `QuanLyHangHoa/ViewModels/AuditLogViewModel.cs`
- `QuanLyHangHoa/Services/DataImport/DynamicImportService.cs`
- `QuanLyHangHoa/Services/DataImport/DataImportManager.cs`

Đây là nhóm code không trực tiếp tạo phiếu kho, nhưng giúp hệ thống trả lời các câu hỏi rất quan trọng:

```text
Sản phẩm/serial này đi qua đâu?
Ai đã sửa dữ liệu nào?
File Excel/CSV được đưa vào database thế nào?
```

## 1. ReportTraceService dùng để làm gì?

`ReportTraceService` có hai nghiệp vụ chính:

| Method | Trả lời câu hỏi |
|---|---|
| `GetProductTimeline` | Một sản phẩm tăng/giảm tồn theo thời gian như thế nào? |
| `SearchSerialTrace` | Một serial đã nhập từ đâu, bán cho ai, còn bảo hành không? |

## 2. `GetProductTimeline`: thẻ kho theo sản phẩm

Method:

```csharp
GetProductTimeline(int productId, DateTime fromDate, DateTime toDate)
```

Nó đọc từ `StockLedger`, vì ledger là lịch sử biến động kho.

Luồng dễ hiểu:

```text
1. Lấy toàn bộ ledger của productId đến ngày kết thúc
2. Tính tồn đầu kỳ = tổng ledger trước fromDate
3. Lấy ledger trong khoảng fromDate -> toDate
4. Chạy từng ledger theo thứ tự thời gian
5. Nếu MovementType = In thì cộng tồn
6. Nếu MovementType = Out thì trừ tồn
7. Ghép thêm thông tin chứng từ: mã phiếu, mục đích, đối tác, kho
8. Trả về StartQuantity, EndQuantity và danh sách dòng timeline
```

Đoạn tính tồn đầu kỳ:

```csharp
var currentQty = ledgers
    .Where(l => l.PostedAt < start)
    .Sum(l => l.MovementType == "In" ? l.Quantity : -l.Quantity);
```

Dịch: trước kỳ báo cáo, cứ dòng nhập thì cộng, dòng xuất thì trừ. Kết quả là tồn đầu kỳ.

Đoạn chạy số dư lũy kế:

```csharp
currentQty += inQty - outQty;
```

Dễ hiểu: tồn sau mỗi dòng = tồn trước đó + nhập - xuất.

## 3. Vì sao cần `LoadDocumentContexts`?

`StockLedger` biết chứng từ gốc qua:

```text
SourceDocumentType
SourceDocumentId
```

Ví dụ:

```text
SourceDocumentType = StockIn
SourceDocumentId = 15
```

Nhưng để hiển thị cho người dùng, ta cần thêm:

- Mã chứng từ: `SI-...`
- Mục đích: nhập mua, xuất bán, chuyển kho...
- Đối tác: nhà cung cấp hoặc khách hàng
- Kho

`LoadDocumentContexts` gom các Id từ ledger, query từng bảng chứng từ, rồi lưu vào dictionary:

```csharp
Dictionary<(string Type, int Id), DocumentContext>
```

Dễ hiểu: đây là bảng tra nhanh. Có `(StockIn, 15)` thì trả ra mã phiếu, nhà cung cấp, kho.

## 4. `SearchSerialTrace`: truy vết một serial

Method này phức tạp vì một serial liên quan nhiều bảng:

```text
ProductSerial
-> Product
-> CurrentWarehouse
-> LastStockInLine -> StockIn -> Supplier/Warehouse
-> LastStockOutLine -> StockOut -> Customer/Warehouse/SalesInvoice
-> WarrantyCoverage -> Customer/SalesInvoice
```

Code dùng nhiều `Include` để load đủ dữ liệu:

```csharp
.Include(s => s.Product)
.Include(s => s.CurrentWarehouse)
.Include(s => s.LastStockInLine).ThenInclude(l => l!.StockIn).ThenInclude(si => si!.Supplier)
.Include(s => s.LastStockOutLine).ThenInclude(l => l!.StockOut).ThenInclude(so => so!.Customer)
.Include(s => s.WarrantyCoverage).ThenInclude(w => w!.Customer)
```

Dễ hiểu: muốn hiển thị hành trình serial thì phải kéo theo các bảng liên quan.

## 5. Filter trong `SearchSerialTrace`

Filter gồm:

- `SearchText`: tìm rộng theo serial, sản phẩm, chứng từ, hóa đơn.
- `ProductText`: lọc theo mã/tên sản phẩm.
- `DocumentText`: lọc theo mã phiếu hoặc hóa đơn.
- `PartnerText`: lọc theo nhà cung cấp/khách hàng.
- `Status`: lọc trạng thái serial.
- `FromDate`, `ToDate`: lọc theo ngày liên quan.

Điểm cần hiểu: một serial có nhiều ngày liên quan, không chỉ một ngày:

```csharp
var dates = new[] { item.ImportDate, item.ExportDate, item.SalesInvoiceDate, item.WarrantyStartDate, item.WarrantyEndDate }
```

Vì vậy `IsWithinDateRange` kiểm tra xem **bất kỳ ngày nào** của serial có nằm trong khoảng lọc không.

## 6. `ToSerialTraceItem`: gom dữ liệu về một DTO dễ hiển thị

`ProductSerial` là entity database. UI không nên tự lục từng navigation property. Service gom thành `SerialTraceItem`:

```csharp
return new SerialTraceItem
{
    SerialNumber = serial.SerialNumber,
    ProductCode = serial.Product?.ProductCode ?? string.Empty,
    ImportDocCode = stockIn?.DocumentCode ?? "-",
    ExportDocCode = stockOut?.DocumentCode ?? "-",
    CustomerName = stockOut?.Customer?.DisplayName ?? serial.WarrantyCoverage?.Customer?.DisplayName ?? "-",
    WarrantyStatus = GetWarrantyStatus(serial.LastStockOutLine != null, serial.WarrantyCoverage)
};
```

Dễ hiểu: DTO là bản dữ liệu đã được “dọn sẵn” cho màn hình báo cáo.

## 7. AuditLogViewModel dùng để làm gì?

Audit log trả lời câu hỏi:

```text
Ai đã làm gì, lúc nào, trên dữ liệu nào, trước/sau ra sao?
```

`AuditLogViewModel` làm các việc:

```text
1. Load danh sách entity/action/user để làm bộ lọc
2. Load logs theo filter
3. Export logs ra Excel
4. Archive logs cũ: export rồi xóa khỏi DB
5. Khi chọn một log, sinh mô tả dễ đọc từ JSON before/after
```

## 8. Các property filter trong AuditLogViewModel

```csharp
[ObservableProperty] private string? _selectedEntity;
[ObservableProperty] private string? _selectedAction;
[ObservableProperty] private AppUser? _selectedUser;
[ObservableProperty] private DateTime? _fromDate;
[ObservableProperty] private DateTime? _toDate;
[ObservableProperty] private string _searchText = string.Empty;
```

Các property này binding với UI. Khi người dùng đổi filter, partial method tự gọi `LoadLogs()`:

```csharp
partial void OnSelectedEntityChanged(string? value) => LoadLogs();
partial void OnSearchTextChanged(string value) => LoadLogs();
```

Đây là cơ chế của CommunityToolkit.Mvvm: với `[ObservableProperty]`, bạn có thể viết `On<PropertyName>Changed` để bắt sự kiện đổi giá trị.

## 9. GenerateDiff: so sánh JSON trước/sau

Audit log lưu:

- `BeforeJson`: dữ liệu trước khi sửa.
- `AfterJson`: dữ liệu sau khi sửa.

`GenerateDiff` đọc hai JSON thành dictionary rồi so từng key:

```csharp
var allKeys = beforeDict.Keys.Union(afterDict.Keys);

foreach (var key in allKeys)
{
    beforeDict.TryGetValue(key, out var oldVal);
    afterDict.TryGetValue(key, out var newVal);

    if (!Equals(oldVal?.ToString(), newVal?.ToString()))
    {
        changes.Add($"{key}: {oldVal ?? "NULL"} -> {newVal ?? "NULL"}");
    }
}
```

Dễ hiểu: lấy tất cả field có thể có, field nào giá trị khác thì ghi ra một dòng thay đổi.

## 10. Archive audit log

`ConfirmArchive` làm 2 bước:

```text
1. Export log cũ ra Excel
2. Xóa các log đó khỏi database
```

Nó chỉ cho archive dữ liệu cũ hơn năm hiện tại:

```csharp
if (ArchiveFromDate.Year >= DateTime.Now.Year || ArchiveToDate.Year >= DateTime.Now.Year)
```

Mục đích: tránh người dùng vô tình xóa log mới, còn cần tra cứu.

## 11. DynamicImportService: import động là gì?

Import động nghĩa là không bắt file Excel phải có đúng tên cột database. Người dùng có thể map:

```text
Cột Excel "Mã SP" -> Field ProductCode
Cột Excel "Tên hàng" -> Field DisplayName
Cột Excel "Giá bán" -> Field DefaultPrice
```

`GetFieldDefinitions` định nghĩa mỗi loại file cần field nào:

```csharp
new() { Key = "ProductCode", DisplayName = "Mã sản phẩm", IsRequired = true }
new() { Key = "DefaultPrice", DisplayName = "Giá bán mặc định", IsRequired = true, DataType = "decimal" }
```

Dễ hiểu: đây là “hợp đồng dữ liệu” cho từng loại import.

## 12. Đọc Excel/CSV về dạng chung

`ReadFile` chọn reader theo đuôi file:

```text
.xlsx/.xls -> ReadExcel
.csv       -> ReadCsv
```

Cả hai đều trả về:

```csharp
(List<string> headers, List<Dictionary<string, string>> rows)
```

Dễ hiểu:

- `headers`: danh sách tên cột.
- `rows`: mỗi dòng là dictionary, key là tên cột, value là giá trị ô.

Ví dụ:

```text
headers = ["Mã SP", "Tên hàng"]
row = { "Mã SP": "SP001", "Tên hàng": "Laptop" }
```

## 13. ExecuteImport: bộ điều phối import

Method:

```csharp
ExecuteImport(rawRows, type, mappings, userId, autoCreateReferences)
```

Luồng:

```text
1. Lấy định nghĩa field theo type
2. Mở db và transaction
3. Switch theo loại file
4. Gọi hàm import tương ứng
5. SaveChanges
6. Commit
7. Nếu lỗi hệ thống thì rollback và ghi lỗi vào result.Errors
```

Điểm hay: lỗi từng dòng được gom vào `result.Errors`, không làm app crash ngay.

## 14. Parser an toàn

Các hàm như `GetMappedDecimal`, `GetMappedDateTime`, `GetMappedBool` giúp chuyển text từ Excel sang kiểu C#.

Ví dụ số tiền có thể viết:

```text
1,234.56
1.234,56
1234,56
```

`NormalizeNumberString` cố chuẩn hóa về dạng invariant culture để parse được.

Đây là lý do import không nên parse số/ngày rải rác ở mọi nơi. Gom parser lại một nơi giúp dễ sửa.

## 15. DataImportManager: import generic bằng reflection

`DataImportManager` là cơ chế import chung hơn. Nó dùng:

- Generic type `T`
- Reflection để đọc property
- `[ImportKey]` để biết field nào là khóa
- Expression tree để tạo điều kiện tìm record đã tồn tại

Đoạn khó:

```csharp
var parameter = Expression.Parameter(typeof(T), "x");
var left = Expression.Property(parameter, prop);
var right = Expression.Constant(value, prop.PropertyType);
var equal = Expression.Equal(left, right);
```

Dễ hiểu: code đang tự xây câu điều kiện kiểu:

```csharp
x => x.ProductCode == "SP001"
```

Nhưng vì `T` là generic, nó không biết trước property là gì, nên phải dùng reflection/expression.

## 16. Khi nào đọc file nào?

| Muốn hiểu | Đọc |
|---|---|
| Thẻ kho sản phẩm | `ReportTraceService.GetProductTimeline` |
| Truy vết serial | `ReportTraceService.SearchSerialTrace` và `ToSerialTraceItem` |
| Xem log ai sửa gì | `AuditLogViewModel.LoadLogs`, `GenerateDetailedResult`, `GenerateDiff` |
| Xuất/archiving log | `ExportLogsToExcel`, `ConfirmArchive` |
| Import file có mapping cột | `DynamicImportService.GetFieldDefinitions`, `ExecuteImport` |
| Import generic bằng attribute | `DataImportManager.UpsertToDatabase` |

## 17. Những điểm người mới dễ nhầm

### `Include` không phải filter

`Include` dùng để load bảng liên quan, không phải lọc dữ liệu.

### DTO không phải entity

`SerialTraceItem` là DTO để hiển thị báo cáo, không phải bảng database.

### Audit log không phải backup đầy đủ

Audit log lưu trước/sau ở dạng JSON cho hành động nghiệp vụ. Nó giúp truy vết, nhưng không thay thế backup database.

### Import thành công một phần không có nghĩa file hoàn hảo

`SuccessCount` và `Errors` phải đọc cùng nhau. Một file có thể import được 90 dòng và lỗi 10 dòng.
