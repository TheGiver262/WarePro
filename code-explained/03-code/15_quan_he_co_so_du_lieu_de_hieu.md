# Quan hệ cơ sở dữ liệu WarePro – bản dễ hiểu

Tài liệu này đọc cùng `QuanLyHangHoa/Data/AppDbContext.cs`. ERD mô tả cấu trúc database; `AppDbContext` là lớp ánh xạ EF Core, không phải bảng hay khóa.

## 1. Danh mục và đơn vị

`Product` là trục chính. Mỗi sản phẩm bắt buộc có `CategoryId`, `BrandId` và `DefaultUnitId`. `Supplier`, `Customer` và `Warehouse` không nối trực tiếp vào `Product`; chúng được các chứng từ nghiệp vụ tham chiếu.

`ProductUnit` là bảng nối giữa `Product` và `Unit`:

- `UNIQUE(ProductId, UnitId)` ngăn khai báo cùng đơn vị hai lần cho một sản phẩm.
- filtered unique trên `ProductId` khi `IsBaseUnit = 1` bảo đảm tối đa một đơn vị cơ sở.
- `ConversionFactor` đổi số lượng giao dịch về số lượng cơ sở.

## 2. Chứng từ kho, số dư và sổ kho

Quan hệ header–line luôn là `1 → 0..N` ở mức database:

- `StockIn.Id → StockInLine.StockInId`;
- `StockOut.Id → StockOutLine.StockOutId`;
- `StockTransfer.Id → StockTransferLine.StockTransferId`;
- `StockCountSession.Id → StockCountLine.SessionId`;
- `StockAdjustment.Id → StockAdjustmentLine.AdjustmentId`.

Mỗi line bắt buộc thuộc đúng một header. Database cho phép header chưa có line khi đang soạn; service yêu cầu đủ line trước khi duyệt/ghi sổ.

Không có FK trực tiếp `StockOutLine → StockIn` hoặc `StockOutLine → StockInLine`. Nhập và xuất chỉ gặp nhau qua cùng sản phẩm/serial và lịch sử `StockLedger`.

`StockBalance` giữ số hiện tại, duy nhất theo `(WarehouseId, ProductId)`. `StockLedger` giữ từng biến động và có FK vật lý tới:

- `WarehouseId → Warehouse.Id`;
- `ProductId → Product.Id`;
- `ProductSerialId → ProductSerial.Id` khi biến động theo serial;
- `PostedBy → AppUser.Id`.

`SourceDocumentType + SourceDocumentId` là tham chiếu logic đa hình tới phiếu nhập, xuất, chuyển hoặc điều chỉnh. Nó không thể là một FK SQL duy nhất tới nhiều bảng.

## 3. ProductSerial và các con trỏ gần nhất

`ProductSerial.ProductId` là FK bắt buộc. `CurrentWarehouseId` nullable: có giá trị khi serial đang ở kho; có thể null sau khi bán, gửi hãng hoặc nằm ngoài kho.

Ba trường sau phục vụ truy vấn nhanh trạng thái/nguồn gần nhất:

- `LastStockInLineId` – dòng nhập gần nhất/nguồn nhập tương thích schema;
- `LastStockOutLineId` – dòng xuất gần nhất, nullable;
- `StockTransferLineId` – dòng chuyển gần nhất, nullable.

Chúng không phải lịch sử đầy đủ. Sau giao dịch tiếp theo, con trỏ có thể đổi; lịch sử bất biến vẫn nằm trong `StockLedger` và chứng từ đã ghi sổ.

## 4. Hóa đơn và chứng từ kho

`PurchaseInvoice.StockInId` và `SalesInvoice.StockOutId` là FK nullable có filtered unique index. Vì vậy:

- hóa đơn có thể được lập độc lập trước khi gắn phiếu kho;
- một phiếu nhập liên kết tối đa một hóa đơn mua;
- một phiếu xuất liên kết tối đa một hóa đơn bán.

`PurchaseInvoiceLine.StockInLineId` và `SalesInvoiceLine.StockOutLineId` nullable nhưng không unique. Chúng dùng để đối chiếu dòng, không ép quan hệ 1–1.

Mỗi dòng hóa đơn bắt buộc gắn một sản phẩm và đơn vị. `TaxRate` dùng `decimal(9,4)`; tiền, đơn giá và tổng tiền dùng `decimal(18,2)`.

## 5. Bảo hành

`WarrantyCoverage` gắn `ProductSerial`, `Customer` và tùy chọn `SalesInvoice`. Filtered unique index chỉ cho tối đa một coverage trạng thái `Active` trên một serial, nhưng vẫn giữ coverage đã hết hiệu lực.

`WarrantyClaim` có cả `WarrantyCoverageId` và `ProductSerialId`. FK kép `(WarrantyCoverageId, ProductSerialId)` trỏ tới alternate key cùng cặp trên `WarrantyCoverage`, nên claim không thể dùng coverage của serial khác. Một filtered unique index khác chặn nhiều claim chưa `Closed`/`Rejected` trên cùng serial.

`ReplacementSerialId` và `ReplacementStockOutId` nullable vì chỉ có khi nghiệp vụ đổi thiết bị phát sinh.

## 6. Người dùng, audit và archive manifest

Các FK `CreatedBy`, `ApprovedBy`, `PostedBy`, `UpdatedBy` và `ProcessedBy` giữ từng vai trò riêng của `AppUser`; không gom chúng thành một đường FK mơ hồ trong ERD chi tiết.

`AuditLog` là nhật ký thao tác. `EntityName + EntityId` là tham chiếu logic tới đối tượng bị tác động, không phải FK đa bảng.

`AuditArchiveManifest` là biên nhận của một lần xuất log: `OperationId`, khoảng UTC, số dòng, tên file và `Sha256Hash`. Nó không chứa bản sao các dòng log và không phải bảng audit thứ hai.

## 7. Kiểu dữ liệu cần ghi đúng trên ERD

- SQL Server `rowversion` có kích thước cố định 8 byte; EF Core biểu diễn bằng `byte[]`.
- Không ghi `RowVersion : varbinary(max)` trên ERD vật lý.
- `TaxRate : decimal(9,4)`.
- Giá trị tiền: `decimal(18,2)`.
- Chuỗi phải ghi đủ độ dài khi ERD là vật lý, ví dụ `nvarchar(50)` hoặc `nvarchar(max)`.

## 8. Cách đọc cardinality

`1 → 0..N` nghĩa là bản ghi con bắt buộc có một cha, nhưng cha có thể chưa có con. FK nullable tạo đầu `0..1` ở phía bản ghi tham chiếu. Unique index quyết định phía còn lại có tối đa một hay có thể nhiều bản ghi tham chiếu; chỉ nhìn FK là chưa đủ để kết luận quan hệ 1–1.
