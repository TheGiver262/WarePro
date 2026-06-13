# Giải thích chi tiết mã nguồn: Phân hệ Kiểm thử Bảo hành

Tài liệu này giải thích chi tiết các phương thức kiểm thử (Unit Test) mới được bổ sung nhằm xác minh nghiệp vụ bảo hành đổi mới.

---

## 1. Tập tin liên quan
* [WarrantyClaimServiceTests.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa.Tests/Services/WarrantyClaimServiceTests.cs): Tập tin chứa các ca kiểm thử dành cho dịch vụ bảo hành.

---

## 2. Giải thích chi tiết các Ca kiểm thử (Test Cases)

### Ca kiểm thử 1: `ReceiveFromManufacturerReplaced_updates_old_coverage_to_inactive_and_creates_new_coverage_with_remaining_days`

* **Mục tiêu:** Xác minh khi nhận máy đổi mới từ hãng: máy cũ bị khóa bảo hành, máy mới được cấp bảo hành mới kế thừa đúng số ngày còn lại (20 ngày).
* **Thiết lập dữ liệu giả lập (Arrange):**
  ```csharp
  var serial = new ProductSerial { SerialNumber = "OLD-WARRANTY-SERIAL-1", ProductId = 3000, CurrentStatus = "ReturnedToManufacturer" };
  seedContext.ProductSerials.Add(serial);
  
  var coverage = new WarrantyCoverage 
  { 
      ProductSerialId = serial.Id, 
      CustomerId = 1, 
      WarrantyStartDate = DateTime.Now.AddDays(-10), // Bắt đầu 10 ngày trước
      WarrantyEndDate = DateTime.Now.AddDays(20),  // Hết hạn sau 20 ngày nữa
      CoverageStatus = "Active" 
  };
  seedContext.WarrantyCoverages.Add(coverage);
  ```
  *Ý nghĩa:* Giả lập một kịch bản thực tế khi máy lỗi được bảo hành trong 30 ngày, người dùng đã sử dụng được 10 ngày (còn lại đúng 20 ngày).
* **Thực thi hành động (Act):**
  ```csharp
  service.ReceiveFromManufacturerReplaced(claimId, newSerialNo, "Replaced by manufacturer", userId: 4);
  ```
  *Ý nghĩa:* Chạy luồng nghiệp vụ nhận máy đổi mới từ hãng với số serial thay thế là `newSerialNo`.
* **Kiểm tra kết quả (Assert):**
  ```csharp
  var oldCoverage = assertContext.WarrantyCoverages.FirstOrDefault(c => c.ProductSerial.SerialNumber == "OLD-WARRANTY-SERIAL-1");
  Assert.Equal("Inactive", oldCoverage.CoverageStatus);

  var newCoverage = assertContext.WarrantyCoverages.FirstOrDefault(c => c.ProductSerialId == newSerial.Id);
  Assert.Equal("Active", newCoverage.CoverageStatus);
  
  var remainingDays = (newCoverage.WarrantyEndDate - DateTime.Now).TotalDays;
  Assert.True(remainingDays > 19 && remainingDays <= 20);
  ```
  *Ý nghĩa:* 
  1. Đảm bảo bảo hành của máy cũ đã bị chuyển sang `Inactive`.
  2. Đảm bảo máy mới đã được cấp bảo hành `Active`.
  3. Đảm bảo số ngày bảo hành còn lại của máy mới xấp xỉ đúng 20 ngày (tính sai số mili-giây khi chạy code).

---

### Ca kiểm thử 2: `ReplaceSerial_updates_old_coverage_to_inactive_and_creates_new_coverage_with_remaining_days`

* **Mục tiêu:** Xác minh khi đổi mới trực tiếp từ kho: máy cũ bị khóa bảo hành, máy mới trong kho được xuất và cấp bảo hành mới kế thừa thời hạn còn lại (15 ngày).
* **Thiết lập dữ liệu đặc biệt (Stock Balance Seeding):**
  Khác với việc nhận từ hãng (hãng tự chuyển máy mới cho doanh nghiệp), việc đổi mới trực tiếp đòi hỏi phải có sẵn sản phẩm trong kho của công ty. Do đó ta phải tạo sẵn tồn kho:
  ```csharp
  var replacementSerial = new ProductSerial { SerialNumber = replacementSerialNo, ProductId = 3001, CurrentStatus = "InStock", CurrentWarehouseId = 1 };
  seedContext.ProductSerials.Add(replacementSerial);
  seedContext.StockBalances.Add(new StockBalance
  {
      ProductId = 3001,
      WarehouseId = 1,
      OnHandQuantity = 1,
      AvailableQuantity = 1
  });
  ```
  *Ý nghĩa:* Ghi nhận tồn kho khả dụng bằng 1 cho sản phẩm `3001` tại kho `1` để hệ thống không quăng lỗi thiếu hàng `Insufficient available stock` khi thực hiện xuất kho đổi mới.
* **Thực thi hành động (Act):**
  ```csharp
  service.ReplaceSerial(claimId, replacementSerialNo, "Direct replacement", userId: 4);
  ```
* **Kiểm tra kết quả (Assert):**
  ```csharp
  var oldCoverage = assertContext.WarrantyCoverages.FirstOrDefault(c => c.ProductSerial.SerialNumber == "OLD-WARRANTY-SERIAL-2");
  Assert.Equal("Inactive", oldCoverage.CoverageStatus);

  var newCoverage = assertContext.WarrantyCoverages.FirstOrDefault(c => c.ProductSerialId == newSerial.Id);
  Assert.Equal("Active", newCoverage.CoverageStatus);
  
  var remainingDays = (newCoverage.WarrantyEndDate - DateTime.Now).TotalDays;
  Assert.True(remainingDays > 14 && remainingDays <= 15);
  ```
  *Ý nghĩa:* Bảo hành cũ đã tắt, bảo hành mới kế thừa đúng hạn bảo hành còn lại là 15 ngày của máy cũ.
