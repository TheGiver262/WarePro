# Cơ sở Dữ liệu & Các Mô hình Thực thể (Database & Domain Models)

Dự án sử dụng cơ chế **Code-First** của Entity Framework Core. Cơ sở dữ liệu **ProductManagementDb** chạy trên SQL Server được cấu hình thông qua Fluent API để thiết lập các ràng buộc toàn vẹn, chỉ mục hiệu năng (Indexes) và ràng buộc kiểm tra (Check Constraints).

Tài liệu này giải thích chi tiết tệp cấu hình cơ sở dữ liệu `AppDbContext.cs` và ý nghĩa, cấu trúc của 31 Domain Models trong thư mục `Models/`.

---

## 1. Cấu hình Cơ sở Dữ liệu (`AppDbContext.cs`)

Tập tin [AppDbContext.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Data/AppDbContext.cs) chứa toàn bộ cấu hình Fluent API trong phương thức `OnModelCreating`. Các cấu hình nâng cao bao gồm:

### A. Ràng buộc Kiểm tra (Check Constraints)
Hệ thống sử dụng các ràng buộc CHECK ở mức cơ sở dữ liệu để đảm bảo các trường mã nghiệp vụ (`PurposeCode`) chỉ nhận các giá trị hợp lệ, ngăn chặn dữ liệu rác ghi vào DB:
* **Bảng StockIn:**
  ```csharp
  entity.ToTable("StockIn", t => t.HasCheckConstraint("CK_StockIn_PurposeCode", "[PurposeCode] IN ('Purchase', 'OpeningBalance', 'Adjustment', 'WarrantyReceive')"));
  ```
  *Ý nghĩa:* Chỉ chấp nhận PurposeCode là `Purchase` (Mua hàng), `OpeningBalance` (Tồn đầu kỳ), `Adjustment` (Điều chỉnh kho), hoặc `WarrantyReceive` (Nhận máy đổi mới từ hãng).
* **Bảng StockOut:**
  ```csharp
  entity.ToTable("StockOut", t => t.HasCheckConstraint("CK_StockOut_PurposeCode", "[PurposeCode] IN ('Sale', 'WarrantyReplacement', 'Adjustment')"));
  ```
  *Ý nghĩa:* Chỉ chấp nhận PurposeCode là `Sale` (Bán hàng), `WarrantyReplacement` (Xuất đổi mới bảo hành), hoặc `Adjustment` (Điều chỉnh kho).

### B. Chỉ mục Hiệu năng & Ràng buộc Duy nhất (Indexes & Constraints)
Để tối ưu hóa tốc độ truy vấn trên lượng dữ liệu lớn và đảm bảo tính duy nhất:
* **Chỉ mục phủ định duy nhất cho ProductSerial:**
  ```csharp
  entity.HasIndex(e => e.SerialNumber, "UX_ProductSerial_SerialNumber").IsUnique();
  ```
  *Ý nghĩa:* Đảm bảo không bao giờ có hai số Serial trùng nhau được tạo trong toàn bộ hệ thống.
* **Chỉ mục duy nhất cho tồn kho:**
  ```csharp
  entity.HasIndex(e => new { e.WarehouseId, e.ProductId }, "UX_StockBalance_Warehouse_Product").IsUnique();
  ```
  *Ý nghĩa:* Mỗi sản phẩm chỉ có duy nhất một dòng ghi nhận số lượng tồn kho tại một kho hàng cụ thể.
* **Chỉ mục lọc duy nhất (Filtered Unique Indexes) cho Bảo hành:**
  * **WarrantyCoverage:**
    ```csharp
    entity.HasIndex(e => e.ProductSerialId, "UX_WarrantyCoverage_Active")
          .IsUnique()
          .HasFilter("[CoverageStatus] = 'Active'");
    ```
    *Ý nghĩa:* Một số Serial chỉ được phép có duy nhất **một** quyền bảo hành đang hoạt động (`Active`) tại một thời điểm.
  * **WarrantyClaim:**
    ```csharp
    entity.HasIndex(e => e.ProductSerialId, "UX_WarrantyClaim_Open")
          .IsUnique()
          .HasFilter("[Status] IN ('Open', 'Checking', 'WaitingDecision', 'SentToManufacturer', 'WaitingManufacturerResult', 'Repairing')");
    ```
    *Ý nghĩa:* Một số Serial chỉ được phép có duy nhất **một** hồ sơ bảo hành đang mở (chưa đóng). Tránh trường hợp tiếp nhận bảo hành chồng chéo cho cùng một thiết bị đang xử lý.

---

## 2. Ý nghĩa của các Domain Models chính

31 Models trong dự án được chia thành 5 nhóm chức năng chính:

### Nhóm 1: Hệ thống & Kiểm toán (System & Audit)
1. **`AppUser`:** Lưu trữ thông tin tài khoản người dùng, băm mật khẩu (`PasswordHash`), cấp bậc quyền (`RoleCode`), số lần đăng nhập sai (`FailedLoginCount`) và thời hạn bị khóa (`LockoutUntil`).
2. **`AuditLog`:** Nhật ký kiểm toán. Lưu lại mọi hành động thay đổi dữ liệu của các tài khoản (Tên bảng, ID bản ghi, mã hành động, thời điểm, dữ liệu Json trước/sau khi thay đổi).

### Nhóm 2: Danh mục Nền (Catalog Metadata)
3. **`Product`:** Thông tin sản phẩm (mã, tên, giá bán, giá vốn, thời hạn bảo hành mặc định, và cờ `IsSerialTracked` để xác định sản phẩm có quản lý theo số Serial hay không).
4. **`ProductUnit`:** Bảng trung gian quy đổi đơn vị. Cho phép một sản phẩm có nhiều đơn vị đo lường (ví dụ: cái, hộp, thùng) liên kết qua `ConversionFactor` (Hệ số quy đổi so với đơn vị cơ sở).
5. **`Unit`:** Danh mục đơn vị tính (Cái, Chiếc, Thùng...).
6. **`Brand`** & **`Category`:** Thương hiệu và Danh mục phân loại sản phẩm.
7. **`Warehouse`:** Danh mục kho hàng (có cờ `IsDefault` để chọn kho mặc định).
8. **`Customer`** & **`Supplier`:** Danh mục Khách hàng và Nhà cung cấp.

### Nhóm 3: Quản lý Kho & Giao dịch Kho (Inventory Transactions)
9. **`StockIn` & `StockInLine`:** Chứng từ Nhập kho (Master-Detail). Lưu thông tin người lập, ngày nhập, mục đích nhập và danh sách chi tiết các sản phẩm nhập.
10. **`StockOut` & `StockOutLine`:** Chứng từ Xuất kho (Master-Detail).
11. **`StockBalance`:** Bảng lưu lượng tồn kho khả dụng hiện tại của từng sản phẩm tại từng kho. Lưu 3 biến số lượng:
    * `OnHandQuantity`: Tồn kho thực tế trong kho.
    * `AvailableQuantity`: Tồn kho khả dụng (sẵn sàng bán).
    * `ReservedQuantity`: Tồn kho đã được đặt trước (giữ hàng).
12. **`ProductSerial`:** Danh sách toàn bộ các số Serial của sản phẩm trong hệ thống. Lưu trạng thái hiện tại của Serial (`InStock`, `Sold`, `InWarrantyProcess`...) và liên kết với dòng chứng từ nhập/xuất cuối cùng để phục vụ truy vết.
13. **`StockLedger`:** Thẻ kho / Sổ kho chi tiết. Ghi nhận mọi sự kiện tăng/giảm số lượng của sản phẩm (hoặc cụ thể của từng số Serial) kèm theo mã chứng từ nguồn. Đây là căn cứ để tính tồn kho lũy kế cộng dồn qua mọi thời điểm.
14. **`StockTransfer` & `StockTransferLine`:** Chứng từ chuyển kho nội bộ (giữa kho nguồn và kho đích).
15. **`StockAdjustment` & `StockAdjustmentLine`:** Chứng từ điều chỉnh kho (tăng/giảm số lượng tồn) phát sinh sau khi kiểm kê.
16. **`StockCountSession` & `StockCountLine`:** Phiên kiểm kê kho. Lưu số lượng tồn hệ thống (`SystemQuantity`), số lượng đếm thực tế (`CountedQuantity`) và chênh lệch chênh lệch (`VarianceQuantity`).

### Nhóm 4: Hóa đơn Thương mại (Commercial Invoices)
17. **`PurchaseInvoice` & `PurchaseInvoiceLine`:** Hóa đơn mua hàng từ Nhà cung cấp. Tham chiếu đến phiếu nhập kho tương ứng, lưu tổng tiền (`SubTotal`), thuế (`TaxAmount`), tổng thanh toán (`GrandTotal`) và công nợ thanh toán (`PaymentStatus`).
18. **`SalesInvoice` & `SalesInvoiceLine`:** Hóa đơn bán hàng cho Khách hàng. Tham chiếu đến phiếu xuất kho tương ứng. **Lưu ý nghiệp vụ:** Khi hóa đơn bán hàng được lưu thành công, hệ thống sẽ tự động quét các dòng và tạo ra các bản ghi `WarrantyCoverage` (quyền bảo hành) cho các số Serial đã bán.

### Nhóm 5: Nghiệp vụ Bảo hành (Warranty Management)
19. **`WarrantyCoverage`:** Quyền bảo hành của sản phẩm. Ghi nhận thời gian hiệu lực bảo hành (`WarrantyStartDate` $\rightarrow$ `WarrantyEndDate`), liên kết với khách hàng sở hữu và số Serial thiết bị. Trạng thái gồm `Active` (Hoạt động), `Inactive` (Hết hạn hoặc máy lỗi bị thu hồi), `Replaced` (Đã đổi mới).
20. **`WarrantyClaim`:** Phiếu yêu cầu bảo hành / Hồ sơ sự cố bảo hành. Lưu trữ chi tiết lỗi, kết luận kỹ thuật, kết quả xử lý của hãng, tham chiếu đến Serial thay thế mới (`ReplacementSerialId`) và chứng từ xuất kho đổi mới (`ReplacementStockOutId`). Trạng thái của claim chuyển dịch qua các bước: `Open` $\rightarrow$ `Checking` $\rightarrow$ `SentToManufacturer` $\rightarrow$ `Repaired` / `Replaced` / `Rejected` $\rightarrow$ `Closed`.
