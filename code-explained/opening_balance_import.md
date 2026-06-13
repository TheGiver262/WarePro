# Giải thích chi tiết mã nguồn: Phân hệ Nhập tồn đầu kỳ

Tài liệu này giải thích chi tiết về thuật toán, cấu trúc dữ liệu và logic xử lý của chức năng nhập kho tồn đầu kỳ từ tập tin dữ liệu (Excel/CSV).

---

## 1. Tập tin liên quan
* [OpeningBalanceImportService.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Services/OpeningBalanceImportService.cs): Lớp dịch vụ chịu trách nhiệm phân tích tệp và thực hiện lưu trữ thông tin tồn đầu kỳ vào database.

---

## 2. Các Thuật toán & Cơ chế đặc biệt

### A. Mô hình Master-Detail (Chứng từ Cha - Dòng Chi tiết)
Để đảm bảo tất cả hàng hóa tồn đầu kỳ được nhập vào hệ thống đều có thể truy vết lịch sử kho một cách minh bạch, hệ thống không chỉ tăng số lượng tồn kho mà bắt buộc phải sinh ra một chứng từ nhập kho (`StockIn`) và các dòng chi tiết tương ứng (`StockInLine`).
* **Khởi tạo chứng từ Cha (`StockIn`):**
  ```csharp
  var stockIn = new StockIn
  {
      DocumentCode = $"SI-OB-{DateTime.Now:yyyyMMddHHmmss}",
      WarehouseId = warehouseId,
      PurposeCode = "OpeningBalance",
      Status = DocumentStatus.Posted,
      ImportDate = DateTime.Now,
      Notes = $"Import tồn đầu kỳ từ Excel/CSV",
      CreatedBy = postedByUserId,
      CreatedAt = DateTime.Now,
      PostedBy = postedByUserId,
      PostedAt = DateTime.Now
  };
  db.StockIns.Add(stockIn);
  db.SaveChanges();
  stockInId = stockIn.Id;
  ```
  *Ý nghĩa:* Trước khi duyệt qua các dòng dữ liệu để import, hệ thống tạo sẵn một phiếu nhập kho có mã `SI-OB-[Thời gian]` loại `OpeningBalance` (Tồn đầu kỳ) với trạng thái đã ghi sổ `Posted`. Hàm `db.SaveChanges()` chạy trước để SQL Server tự động tạo ra một `Id` tăng tự động hợp lệ và gán vào `stockIn.Id`.
* **Khởi tạo dòng chi tiết (`StockInLine`):**
  Với mỗi sản phẩm được duyệt trong vòng lặp Excel/CSV, một bản ghi dòng chứng từ được liên kết với ID cha (`stockInId`):
  ```csharp
  var line = new StockInLine
  {
      StockInId = stockInId,
      ProductId = row.ProductId,
      UnitId = unitId,
      Quantity = row.Quantity,
      BaseQuantity = row.Quantity,
      UnitPrice = product?.DefaultPrice ?? 0
  };
  db.StockInLines.Add(line);
  db.SaveChanges();
  ```
  *Ý nghĩa:* Lưu chi tiết dòng sản phẩm và số lượng tương ứng để sau này làm căn cứ đối chiếu và báo cáo Xuất-Nhập-Tồn đa chiều.

### B. Liên kết Sổ kho chính xác (Inventory Posting)
Thay vì truyền cứng giá trị `0` cho ID chứng từ gốc (gây mất liên kết thẻ kho), hệ thống truyền `stockInId` thực tế vừa tạo ở bước trên vào dịch vụ ghi sổ tồn kho:
```csharp
postingService.PostStockIn(new PostStockInCommand(
    stockInId,
    warehouseId,
    StockInKind.OpeningBalance,
    StockDocumentStatus.Approved,
    row.ProductId,
    row.Quantity,
    StockInService.ParseSerialRange(row.SerialNumbers),
    postedByUserId));
```
*Ý nghĩa:* Lệnh `PostStockInCommand` nhận vào `stockInId` giúp `InventoryPostingService` ghi nhận lịch sử biến động sổ kho (`StockLedger`) gắn chặt với mã phiếu nhập `SI-OB-...`, cho phép truy vết lịch sử thẻ kho chi tiết của từng sản phẩm.

### C. Gán liên kết dòng chứng từ cho số Serial (LastStockInLineId)
Đối với các sản phẩm quản lý theo số Serial (ví dụ: điện thoại, laptop), việc biết từng số Serial được nhập vào từ dòng chứng từ nào là vô cùng quan trọng:
```csharp
var sns = StockInService.ParseSerialRange(row.SerialNumbers);
if (sns.Any())
{
    var dbSerials = db.ProductSerials.Where(ps => sns.Contains(ps.SerialNumber)).ToList();
    foreach (var s in dbSerials)
    {
        s.LastStockInLineId = line.Id;
    }
    db.SaveChanges();
}
```
*Ý nghĩa:* Sau khi `PostStockIn` tự động sinh các số Serial mới trong database (trạng thái `InStock`), hệ thống tìm ngược lại các Serial đó trong DB và cập nhật trường `LastStockInLineId` trỏ trực tiếp đến `line.Id` (dòng chứng từ nhập đầu kỳ). Điều này tạo ra liên kết chặt chẽ cho phép tính năng "Truy vết lịch sử Serial" hiển thị chính xác nguồn gốc nhập của từng Serial.

### D. Cơ chế dọn dẹp (Rollback & Cleanup) khi lỗi
Hệ thống xử lý lỗi linh hoạt nhằm tránh sinh ra các chứng từ rác trống rỗng:
```csharp
if (result.SuccessCount == 0 && stockInId > 0)
{
    try
    {
        using var db = _contextFactory();
        var stockIn = db.StockIns.Find(stockInId);
        if (stockIn != null)
        {
            db.StockIns.Remove(stockIn);
            db.SaveChanges();
        }
    }
    catch
    {
        // Bỏ qua lỗi dọn dẹp để không che lấp ngoại lệ chính
    }
}
```
*Ý nghĩa:* Mỗi dòng import được thực thi trong một Database Transaction riêng biệt để đảm bảo lỗi ở dòng này không làm mất dữ liệu của dòng đã import thành công trước đó. Tuy nhiên, nếu **toàn bộ các dòng** trong tệp import đều bị lỗi (ví dụ file Excel sai cấu trúc hoặc ProductId không tồn tại) và không có dòng nào được nhập thành công (`SuccessCount == 0`), hệ thống sẽ tự động tìm và xóa chứng từ cha `StockIn` trống vừa tạo để giữ cơ sở dữ liệu sạch sẽ.
