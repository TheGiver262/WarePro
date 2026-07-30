# Thiết kế chỉnh sửa sáu ERD chi tiết

Ngày: 2026-07-30  
Phạm vi: `C:\Users\player\Desktop\DATN\final\WarePro_ERD_Tong_20260730.drawio`

## 1. Mục tiêu và giới hạn

- Chỉ sửa sáu trang ERD chi tiết, từ trang 2 đến trang 7.
- Giữ nguyên tuyệt đối XML của trang 1 (ERD tổng quan) trong đợt này.
- Không xuất PNG và không sửa DOCX.
- Giữ phong cách của các ERD cũ: bảng trong phân hệ hiển thị đầy đủ thuộc tính, PK/FK và đường nối trực tiếp.
- Bổ sung các bảng tham chiếu ngoài phân hệ ở dạng hộp thu gọn, nhưng vẫn hiển thị PK và các FK liên quan.
- `AppDbContext.cs` là nguồn chuẩn cho quan hệ; các model chỉ dùng để lấy tên và kiểu thuộc tính.

## 2. Quy tắc biểu diễn quan hệ

- Mỗi FK thật phải có một đường nối trực tiếp từ khóa chính/khóa ứng viên của bảng cha đến FK của bảng con.
- Nhãn đường nối dùng dạng `BangCha.PK → BangCon.FK`.
- Với FK bắt buộc, mỗi bản ghi con trỏ tới đúng `1` bản ghi cha; với FK tùy chọn, đầu cha là `0..1`. Chiều ngược lại từ cha tới tập bản ghi con là `0..N`.
- FK tổng hợp của bảo hành được vẽ bằng một đường duy nhất và ghi đủ hai cột:
  `(WarrantyCoverage.Id, WarrantyCoverage.ProductSerialId) → (WarrantyClaim.WarrantyCoverageId, WarrantyClaim.ProductSerialId)`.
- Không suy diễn quan hệ từ các cột dữ liệu không được cấu hình là FK. Đặc biệt, `StockLedger.SourceDocumentId` không được vẽ như FK.
- Không tạo quan hệ trực tiếp giả giữa `Product` với `Supplier`, `Customer` hoặc `Warehouse`.
- `WareProClientSession` được ghi rõ là bảng độc lập, không có FK trong `AppDbContext`.

## 3. Quy tắc bố cục

- Bảng nội bộ phân hệ là hộp đầy đủ: tên bảng, thuộc tính, PK/FK.
- Bảng ngoài phân hệ là hộp thu gọn: tên bảng, PK và các FK có liên quan đến trang hiện tại.
- Dùng đường gấp khúc vuông góc, đi theo các hành lang quanh bảng.
- Không cho đường nối đi xuyên qua bảng, không dùng đường chéo và không chồng nhiều đường lên cùng một đoạn.
- Tách các đường có chung bảng đích bằng các cổng nối và hành lang riêng.
- Có thể mở rộng kích thước trang để bảo đảm dễ đọc; không ép vào khổ A4.
- Không thay quan hệ trực tiếp bằng chú thích chân trang hoặc danh sách chữ.

## 4. Nội dung từng ERD chi tiết

### 4.1. Danh mục và đối tác

Bảng đầy đủ:

- `Category`, `Brand`, `Unit`, `Product`, `ProductUnit`
- `Supplier`, `Customer`, `Warehouse`

Quan hệ nội bộ:

- `Category.Id → Product.CategoryId`
- `Brand.Id → Product.BrandId`
- `Unit.Id → Product.DefaultUnitId`
- `Product.Id → ProductUnit.ProductId`
- `Unit.Id → ProductUnit.UnitId`

`Product` đặt ở vị trí trung tâm và nối tới toàn bộ bảng thật sự chứa `ProductId`:

- `ProductUnit`
- `ProductSerial`
- `PurchaseInvoiceLine`
- `SalesInvoiceLine`
- `StockAdjustmentLine`
- `StockBalance`
- `StockCountLine`
- `StockInLine`
- `StockLedger`
- `StockOutLine`
- `StockTransferLine`

Các kết nối ngoài phân hệ khác:

- `Supplier.Id → StockIn.SupplierId`
- `Supplier.Id → PurchaseInvoice.SupplierId`
- `Customer.Id → StockOut.CustomerId`
- `Customer.Id → SalesInvoice.CustomerId`
- `Customer.Id → WarrantyCoverage.CustomerId`
- `Warehouse.Id → ProductSerial.CurrentWarehouseId`
- `Warehouse.Id → StockIn.WarehouseId`
- `Warehouse.Id → StockOut.WarehouseId`
- `Warehouse.Id → StockAdjustment.WarehouseId`
- `Warehouse.Id → StockCountSession.WarehouseId`
- `Warehouse.Id → StockBalance.WarehouseId`
- `Warehouse.Id → StockLedger.WarehouseId`
- `Warehouse.Id → StockTransfer.FromWarehouseId`
- `Warehouse.Id → StockTransfer.ToWarehouseId`

### 4.2. Nhập xuất kho và số dư

Bảng đầy đủ:

- `StockIn`, `StockInLine`
- `StockOut`, `StockOutLine`
- `StockBalance`, `StockLedger`

Quan hệ nội bộ:

- `StockIn.Id → StockInLine.StockInId`
- `StockOut.Id → StockOutLine.StockOutId`

Bảng tham chiếu ngoài phân hệ và quan hệ cần vẽ:

- `Supplier → StockIn.SupplierId`
- `Customer → StockOut.CustomerId`
- `Warehouse → StockIn.WarehouseId`, `StockOut.WarehouseId`, `StockBalance.WarehouseId`, `StockLedger.WarehouseId`
- `Product → StockInLine.ProductId`, `StockOutLine.ProductId`, `StockBalance.ProductId`, `StockLedger.ProductId`
- `Unit → StockInLine.UnitId`, `StockOutLine.UnitId`
- `AppUser → StockIn.CreatedBy`, `StockIn.ApprovedBy`, `StockIn.PostedBy`
- `AppUser → StockOut.CreatedBy`, `StockOut.ApprovedBy`, `StockOut.PostedBy`
- `AppUser → StockLedger.PostedBy`
- `StockCountSession → StockIn.StockCountSessionId`, `StockOut.StockCountSessionId`
- `StockCountLine → StockIn.StockCountLineId`, `StockOut.StockCountLineId`
- `ProductSerial → StockLedger.ProductSerialId`
- `StockIn.Id → PurchaseInvoice.StockInId`
- `StockInLine.Id → PurchaseInvoiceLine.StockInLineId`
- `StockOut.Id → SalesInvoice.StockOutId`
- `StockOutLine.Id → SalesInvoiceLine.StockOutLineId`
- `StockOut.Id → WarrantyClaim.ReplacementStockOutId`

Các nhãn ở năm dòng cuối phải thể hiện đúng chiều FK thực tế: FK nằm ở `PurchaseInvoice`, `PurchaseInvoiceLine`, `SalesInvoice`, `SalesInvoiceLine` và `WarrantyClaim`, không nằm ở bảng kho.

### 4.3. Điều chuyển, kiểm kê và số sê-ri

Bảng đầy đủ:

- `StockTransfer`, `StockTransferLine`
- `StockCountSession`, `StockCountLine`
- `StockAdjustment`, `StockAdjustmentLine`
- `ProductSerial`

Quan hệ nội bộ:

- `StockTransfer.Id → StockTransferLine.StockTransferId`
- `StockCountSession.Id → StockCountLine.SessionId`
- `StockAdjustment.Id → StockAdjustmentLine.AdjustmentId`
- `StockTransferLine.Id → ProductSerial.StockTransferLineId`

Bảng tham chiếu ngoài phân hệ và quan hệ cần vẽ:

- `Warehouse → StockTransfer.FromWarehouseId`, `StockTransfer.ToWarehouseId`
- `Warehouse → StockCountSession.WarehouseId`, `StockAdjustment.WarehouseId`, `ProductSerial.CurrentWarehouseId`
- `Product → StockTransferLine.ProductId`, `StockCountLine.ProductId`, `StockAdjustmentLine.ProductId`, `ProductSerial.ProductId`
- `Unit → StockTransferLine.UnitId`
- `AppUser → StockTransfer.CreatedBy`, `StockTransfer.ApprovedBy`, `StockTransfer.PostedBy`
- `AppUser → StockCountSession.CreatedBy`, `StockCountSession.ApprovedBy`, `StockCountSession.PostedBy`
- `AppUser → StockAdjustment.CreatedBy`, `StockAdjustment.ApprovedBy`, `StockAdjustment.PostedBy`
- `StockIn.Id → StockInLine.StockInId`
- `StockInLine.Id → ProductSerial.LastStockInLineId`
- `StockOut.Id → StockOutLine.StockOutId`
- `StockOutLine.Id → ProductSerial.LastStockOutLineId`
- `ProductSerial.Id → StockAdjustmentLine.ProductSerialId`
- `ProductSerial.Id → StockLedger.ProductSerialId`
- `ProductSerial.Id → WarrantyCoverage.ProductSerialId`
- `ProductSerial.Id → WarrantyClaim.ProductSerialId`
- `ProductSerial.Id → WarrantyClaim.ReplacementSerialId`

Không vẽ đường trực tiếp `StockIn → ProductSerial` hoặc `StockOut → ProductSerial`; chỉ vẽ chuỗi quan hệ thật qua `StockInLine` và `StockOutLine`.

### 4.4. Hóa đơn

Bảng đầy đủ:

- `PurchaseInvoice`, `PurchaseInvoiceLine`
- `SalesInvoice`, `SalesInvoiceLine`

Quan hệ nội bộ:

- `PurchaseInvoice.Id → PurchaseInvoiceLine.PurchaseInvoiceId`
- `SalesInvoice.Id → SalesInvoiceLine.SalesInvoiceId`

Bảng tham chiếu ngoài phân hệ và quan hệ cần vẽ:

- `Supplier.Id → PurchaseInvoice.SupplierId`
- `Customer.Id → SalesInvoice.CustomerId`
- `Product.Id → PurchaseInvoiceLine.ProductId`
- `Product.Id → SalesInvoiceLine.ProductId`
- `Unit.Id → PurchaseInvoiceLine.UnitId`
- `Unit.Id → SalesInvoiceLine.UnitId`
- `AppUser.Id → PurchaseInvoice.CreatedBy`
- `AppUser.Id → SalesInvoice.CreatedBy`
- `StockIn.Id → PurchaseInvoice.StockInId`
- `StockInLine.Id → PurchaseInvoiceLine.StockInLineId`
- `StockOut.Id → SalesInvoice.StockOutId`
- `StockOutLine.Id → SalesInvoiceLine.StockOutLineId`
- `SalesInvoice.Id → WarrantyCoverage.SalesInvoiceId`

### 4.5. Bảo hành

Bảng đầy đủ:

- `WarrantyCoverage`
- `WarrantyClaim`

Quan hệ nội bộ:

- FK tổng hợp:
  `(WarrantyCoverage.Id, WarrantyCoverage.ProductSerialId) → (WarrantyClaim.WarrantyCoverageId, WarrantyClaim.ProductSerialId)`

Bảng tham chiếu ngoài phân hệ và quan hệ cần vẽ:

- `ProductSerial.Id → WarrantyCoverage.ProductSerialId`
- `ProductSerial.Id → WarrantyClaim.ProductSerialId`
- `ProductSerial.Id → WarrantyClaim.ReplacementSerialId`
- `Customer.Id → WarrantyCoverage.CustomerId`
- `SalesInvoice.Id → WarrantyCoverage.SalesInvoiceId`
- `StockOut.Id → WarrantyClaim.ReplacementStockOutId`
- `AppUser.Id → WarrantyClaim.ProcessedBy`
- `AppUser.Id → WarrantyClaim.ApprovedBy`

Hai vai trò của `ProductSerial` trên `WarrantyClaim` phải được ghi nhãn riêng: sê-ri bảo hành bắt buộc và sê-ri thay thế tùy chọn.

### 4.6. Người dùng và nhật ký

Bảng đầy đủ:

- `AppUser`
- `AuditLog`
- `AuditArchiveManifest`
- `WareProClientSession`

Quan hệ nội bộ:

- `AppUser.Id → AppUser.CreatedBy`
- `AppUser.Id → AuditLog.PerformedBy`
- `AppUser.Id → AuditArchiveManifest.ActorId`

Các bảng nghiệp vụ ngoài phân hệ phải xuất hiện thành từng hộp riêng và có từng đường FK:

- `StockIn`: `CreatedBy`, `ApprovedBy`, `PostedBy`
- `StockOut`: `CreatedBy`, `ApprovedBy`, `PostedBy`
- `StockTransfer`: `CreatedBy`, `ApprovedBy`, `PostedBy`
- `StockCountSession`: `CreatedBy`, `ApprovedBy`, `PostedBy`
- `StockAdjustment`: `CreatedBy`, `ApprovedBy`, `PostedBy`
- `PurchaseInvoice`: `CreatedBy`
- `SalesInvoice`: `CreatedBy`
- `StockLedger`: `PostedBy`
- `WarrantyClaim`: `ProcessedBy`, `ApprovedBy`

Không gộp các bảng nghiệp vụ thành một hộp chung và không thay các đường FK bằng danh sách chữ.

## 5. Cách cập nhật tệp

- Sao lưu tệp DrawIO hiện tại vào thư mục tạm trong workspace trước khi sửa.
- Tính và lưu dấu vân tay XML của trang 1 trước khi sửa.
- Chỉ thay nội dung `mxGraphModel` của các trang 2–7.
- Ghi đè đúng tệp DrawIO hiện tại sau khi sáu trang đã qua kiểm tra.
- Không tạo tệp DrawIO mới làm kết quả cuối.

## 6. Tiêu chí nghiệm thu

- Tệp vẫn có đúng 7 trang.
- Dấu vân tay XML của trang 1 trước và sau giống nhau.
- Sáu trang chi tiết đều có bảng nội bộ đầy đủ và bảng ngoài phân hệ thu gọn.
- Mọi quan hệ liệt kê trong đặc tả đều có đường nối; không có đường nối giả.
- `Product` nối đủ 11 bảng chứa `ProductId`, kể cả `ProductUnit`.
- FK tổng hợp của `WarrantyClaim` được biểu diễn đúng.
- Không có đường nối xuyên qua bảng, đường chéo hoặc đoạn đường chồng khít.
- Tệp mở được bằng Draw.io và sáu trang được kiểm tra trực quan.
- Không có PNG mới và không có thay đổi DOCX.
