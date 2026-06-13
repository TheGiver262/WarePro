# Giải thích chi tiết mã nguồn: Phân hệ Bảo hành & Đổi mới

Tài liệu này giải thích chi tiết về thuật toán, cấu trúc dữ liệu và logic xử lý của chức năng duyệt đổi mới bảo hành sản phẩm, kế thừa hạn bảo hành cho sản phẩm thay thế và cập nhật cấu trúc cơ sở dữ liệu.

---

## 1. Các tập tin liên quan
* [WarrantyClaimService.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Services/WarrantyClaimService.cs): Lớp dịch vụ xử lý quy trình bảo hành (tiếp nhận bảo hành, gửi hãng, đổi mới).
* [AppDbContext.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Data/AppDbContext.cs): Cấu hình DbContext chứa Fluent API định nghĩa check constraints của database.

---

## 2. Các Thuật toán & Cơ chế đặc biệt

### A. Quy trình Đổi mới bảo hành tự động (Warranty Replacement Logic)
Quy trình đổi mới sản phẩm được xử lý trong hai tình huống:
1. `ReceiveFromManufacturerReplaced`: Nhận sản phẩm đổi mới từ hãng sản xuất gửi về.
2. `ReplaceSerial`: Đổi sản phẩm mới trực tiếp từ kho hàng của công ty.

Cả hai phương thức đều áp dụng thuật toán đóng quyền bảo hành cũ và cấp quyền bảo hành mới kế thừa thời gian còn lại:

```csharp
// 1. Lấy thông tin quyền bảo hành hiện tại của sản phẩm lỗi cũ
var oldCoverage = claim.WarrantyCoverage;
if (oldCoverage != null && oldCoverage.CoverageStatus == "Active")
{
    // 2. Chuyển trạng thái bảo hành của sản phẩm cũ sang Inactive (hết hiệu lực)
    oldCoverage.CoverageStatus = "Inactive";

    // 3. Tính toán số ngày bảo hành còn lại của sản phẩm lỗi cũ
    var remainingDays = (oldCoverage.WarrantyEndDate - DateTime.Now).TotalDays;
    
    // 4. Nếu sản phẩm cũ vẫn còn hạn bảo hành (remainingDays > 0)
    if (remainingDays > 0)
    {
        // 5. Khởi tạo một quyền bảo hành mới gán cho Serial thay thế mới
        var newCoverage = new WarrantyCoverage
        {
            ProductSerialId = newSerial.Id, // ID của serial mới
            CustomerId = oldCoverage.CustomerId, // Giữ nguyên khách hàng sở hữu
            SalesInvoiceId = oldCoverage.SalesInvoiceId, // Giữ tham chiếu đến hóa đơn bán gốc
            WarrantyStartDate = DateTime.Now, // Ngày bắt đầu bảo hành mới là hôm nay
            WarrantyEndDate = DateTime.Now.AddDays(remainingDays), // Ngày hết hạn cộng thêm số ngày còn lại
            CoverageStatus = "Active" // Trạng thái hoạt động
        };
        db.WarrantyCoverages.Add(newCoverage);
    }
}
```

* **Ý nghĩa thuật toán:**
  * **Đóng coverage cũ:** Đảm bảo sản phẩm lỗi (khi bị thu hồi hoặc gửi về hãng) không thể tiếp tục được sử dụng để làm căn cứ yêu cầu bảo hành lần sau.
  * **Kế thừa thời hạn bảo hành:** Tránh việc sản phẩm đổi mới được reset lại thời hạn bảo hành từ đầu (ví dụ: máy mua bảo hành 12 tháng, dùng đến tháng thứ 11 bị lỗi đổi máy mới, máy mới chỉ được bảo hành tiếp 1 tháng còn lại thay vì được tặng thêm 12 tháng mới). Điều này bảo vệ quyền lợi tài chính của doanh nghiệp và tuân thủ đúng chính sách bảo hành chuẩn.

### B. Khắc phục xung đột Check Constraints trong Database
* **Vấn đề check constraint của StockIn (`CK_StockIn_PurposeCode`):**
  Mã nguồn ban đầu sinh chứng từ nhập kho cho serial đổi mới từ hãng gán `PurposeCode = "WarrantyReceive"`. Tuy nhiên, database thực tế chỉ cho phép nhập các giá trị `Purchase`, `OpeningBalance`, `Adjustment`. Điều này gây ra lỗi crash hệ thống `CHECK constraint failed`.
  * **Giải pháp:** Cập nhật lại cấu hình check constraint trong `AppDbContext.cs` thông qua Fluent API:
    ```csharp
    entity.ToTable("StockIn", t => t.HasCheckConstraint("CK_StockIn_PurposeCode", "[PurposeCode] IN ('Purchase', 'OpeningBalance', 'Adjustment', 'WarrantyReceive')"));
    ```
    Đồng thời chạy script SQL để đồng bộ thay đổi này trên máy chủ SQL Server thực tế.
* **Vấn đề check constraint của StockOut (`CK_StockOut_PurposeCode`):**
  Mã nguồn ban đầu sinh chứng từ xuất kho đổi mới gán `PurposeCode = "WarrantyReplace"`. Tuy nhiên, database chỉ chấp nhận giá trị `WarrantyReplacement` (danh từ đầy đủ).
  * **Giải pháp:** Sửa đổi mã nguồn trong `WarrantyClaimService.cs` từ `"WarrantyReplace"` thành `"WarrantyReplacement"` để khớp hoàn toàn với quy chuẩn DB mà không cần hạ thấp rào cản kiểm tra của database.

### C. Tránh lỗi trùng lặp Serial (EF Core Tracking)
Trong phương thức `ReceiveFromManufacturerReplaced`, chứng từ nhập kho được tạo kèm theo dòng chi tiết `StockInLine` chứa danh sách `ProductSerials` khởi tạo cứng:
```csharp
// ĐOẠN CODE BỊ LỖI BAN ĐẦU:
new StockInLine
{
    // ...
    ProductSerials = new List<ProductSerial>
    {
        new ProductSerial { SerialNumber = newSerialNumber, ... }
    }
}
```
* **Lỗi xảy ra:** Khi gọi `db.SaveChanges()`, EF Core tự động chèn bản ghi `ProductSerial` này vào DB trước. Ngay sau đó, dịch vụ `postingService.PostStockIn` được gọi để thực hiện nghiệp vụ ghi sổ kho. Dịch vụ này thực hiện kiểm tra `_unitOfWork.SerialExists(newSerialNumber)`. Vì serial đã bị EF Core lưu ở bước trước, hàm kiểm tra trả về `true` và ném ra lỗi `Serial already exists`.
* **Giải pháp:** Loại bỏ việc khởi tạo danh sách `ProductSerials` cứng trong `StockInLine`. Chỉ khởi tạo dòng chứng từ rỗng.
  ```csharp
  // ĐOẠN CODE ĐÃ ĐƯỢC SỬA:
  Lines = new List<StockInLine>
  {
      new StockInLine
      {
          ProductId = product.Id,
          UnitId = unitId,
          Quantity = 1,
          BaseQuantity = 1,
          UnitPrice = 0
      }
  }
  ```
  Khi gọi `postingService.PostStockIn`, dịch vụ ghi sổ sẽ tự động tạo đối tượng `ProductSerial` mới và lưu vào database một cách an toàn và đúng quy trình nghiệp vụ kho.
