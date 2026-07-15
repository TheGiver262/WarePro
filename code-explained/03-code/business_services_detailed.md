# Giải thích chi tiết mã nguồn: Phân hệ Dịch vụ Nghiệp vụ (Business Services)

**WarePro** (project kỹ thuật `QuanLyHangHoa`) tổ chức toàn bộ logic xử lý nghiệp vụ tại thư mục `Services/`. Các lớp dịch vụ này nhận dữ liệu từ lớp Presentation (ViewModels), thực hiện kiểm tra quy tắc nghiệp vụ (Business Rules), điều phối dữ liệu qua các Unit of Work và ghi nhận kết quả xuống cơ sở dữ liệu.

Tài liệu này giải thích chi tiết các dịch vụ nghiệp vụ cốt lõi của toàn bộ dự án.

---

## 1. Dịch vụ Nhập kho (`StockInService`) và Xuất kho (`StockOutService`)

Đây là hai dịch vụ cốt lõi xử lý các giao dịch trực tiếp làm biến động số lượng hàng hóa trong kho. Cả hai đều được thiết kế theo mô hình **Draft-Post** (Lưu nháp - Ghi sổ).

### A. Cơ chế Lưu nháp (`SaveDraft`)
* **Mục đích:** Cho phép thủ kho lưu tạm tiến độ nhập/xuất mà không làm thay đổi tồn kho thực tế, không yêu cầu kiểm tra các số Serial có hợp lệ hay không.
* **Cơ chế xử lý Serial tạm thời (`DraftSerials`):**
  * Trong EF Core, nếu ta trực tiếp thêm thực thể `ProductSerial` vào danh sách `ProductSerials` của dòng chứng từ nháp, EF Core sẽ theo dõi (track) và bắt buộc kiểm tra các khóa duy nhất (`SerialNumber`). Nếu người dùng nhập tạm một serial bị trùng, hệ thống sẽ báo lỗi ngay ở bước nháp.
  * **Giải pháp:** Hệ thống tách số serial ra khỏi luồng theo dõi của EF Core bằng cách lưu toàn bộ danh sách serial dưới dạng chuỗi văn bản phân tách bằng dấu phẩy vào trường `DraftSerials` trong bảng `StockInLine` hoặc `StockOutLine`. Danh sách thực thể `ProductSerials` thật được gán rỗng:
    ```csharp
    var serials = line.ProductSerials?.Select(ps => ps.SerialNumber.Trim()).ToList();
    if (serials.Any())
    {
        line.DraftSerials = string.Join(",", serials);
    }
    line.ProductSerials = new List<ProductSerial>(); // Làm rỗng để tránh EF Core tracking
    ```
* **Thuật toán quy đổi đơn vị tự động:**
  * Dữ liệu gửi lên từ giao diện có thể sử dụng đơn vị quy đổi (ví dụ: Nhập 10 "Thùng", mỗi thùng chứa 10 "Cái").
  * Trước khi lưu nháp, dịch vụ tra cứu bảng `ProductUnit` để lấy hệ số quy đổi (`ConversionFactor`) và tính toán `BaseQuantity` (Số lượng cơ sở):
    ```csharp
    var pu = unitMap.FirstOrDefault(u => u.ProductId == line.ProductId && u.UnitId == line.UnitId);
    line.BaseQuantity = line.Quantity * (pu?.ConversionFactor ?? 1m);
    ```

### B. Cơ chế Ghi sổ (`Post`)
Khi thủ kho bấm duyệt/ghi sổ, hệ thống thực hiện một chuỗi kiểm tra nghiêm ngặt:
1. **Kiểm tra số lượng Serial:** Đảm bảo số lượng serial được quét/nhập khớp chính xác với số lượng sản phẩm ghi trên dòng chứng từ (đối với sản phẩm quản lý bằng serial).
2. **Kiểm tra trùng lặp cơ sở dữ liệu:**
   * Đối với phiếu nhập: Quét DB để đảm bảo chưa có thiết bị nào mang các số serial này (`db.ProductSerials.Any(...)`).
   * Đối với phiếu xuất: Kiểm tra các serial được chọn phải đang ở trạng thái `InStock` (Trong kho) và nằm đúng kho xuất.
3. **Kiểm tra trùng lặp trong chính tài liệu:**
   ```csharp
   var duplicateDocumentSerials = allDocumentSerials
       .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
       .Where(g => g.Count() > 1)
       .Select(g => g.Key)
       .ToList();
   ```
   *Ý nghĩa:* Ngăn chặn lỗi sơ suất khi thủ kho vô tình quét trùng một mã vạch serial hai lần trên cùng một phiếu.
4. **Thuật toán sắp xếp `ProductId` phòng tránh Deadlock:**
   Sắp xếp các dòng chứng từ theo thứ tự ID sản phẩm tăng dần trước khi thực hiện ghi sổ kho:
   ```csharp
   foreach (var line in stockIn.Lines.OrderBy(l => l.ProductId)) { ... }
   ```
5. **Đồng bộ hóa liên kết nguồn gốc Serial (`LastStockInLineId`):**
   Sau khi dịch vụ kho tạo thành công các serial mới, hệ thống cập nhật trường `LastStockInLineId` trỏ trực tiếp đến dòng chi tiết của phiếu nhập vừa ghi sổ để hỗ trợ tính năng truy vết nguồn gốc.

### C. Thuật toán phân tách khoảng Serial (`ParseSerialRange`)
Để hỗ trợ thủ kho nhập nhanh số lượng lớn serial có quy luật tăng dần (ví dụ: quét serial đầu `SN001`, nhập khoảng đến `SN010`), hệ thống tích hợp biểu thức chính quy (Regex):
* **Regex sử dụng:** `^(.+?)(\d+)-[^\d]*(\d+)$`
* **Nguyên lý hoạt động:** Tách chuỗi thành 3 phần: Tiền tố (Prefix), số bắt đầu, và số kết thúc. Hệ thống tự động bù số 0 ở đầu (`PadLeft`) dựa trên độ dài của số bắt đầu để đảm bảo tính đồng dạng mã:
  ```csharp
  int padLen = startStr.Length;
  for (long i = start; i <= end; i++)
      result.Add(prefix + i.ToString().PadLeft(padLen, '0'));
  ```
  *Ví dụ:* `LAPTOP-A009-015` sẽ được mở rộng tự động thành: `LAPTOP-A009`, `LAPTOP-A010`, `LAPTOP-A011`, `LAPTOP-A012`, `LAPTOP-A013`, `LAPTOP-A014`, `LAPTOP-A015`.

---

## 2. Dịch vụ Hóa đơn Thương mại (`InvoiceService`)

Dịch vụ này quản lý việc mua hàng từ nhà cung cấp (`PurchaseInvoice`) và bán hàng cho khách hàng (`SalesInvoice`).

### A. Thuật toán tính toán dòng hóa đơn (`CalculateLine`)
Mỗi dòng chi tiết hóa đơn được tính toán tự động các giá trị tài chính theo công thức:
$$\text{SubTotal} = \text{Quantity} \times \text{UnitPrice}$$
$$\text{TaxAmount} = \text{SubTotal} \times \text{TaxRate}$$
$$\text{GrandTotal} = \text{SubTotal} + \text{TaxAmount}$$
Các giá trị này sau đó được cộng dồn lên cấp hóa đơn cha:
* $\text{SubTotal}_{\text{Invoice}} = \sum \text{SubTotal}_{\text{Line}}$
* $\text{TaxAmount}_{\text{Invoice}} = \sum \text{TaxAmount}_{\text{Line}}$
* $\text{GrandTotal}_{\text{Invoice}} = \sum \text{GrandTotal}_{\text{Line}}$

### B. Cơ chế cập nhật trạng thái công nợ (`UpdatePaymentStatus`)
Trạng thái thanh toán của hóa đơn được phân loại tự động dựa trên số tiền đã thanh toán (`PaidAmount`) và hạn thanh toán (`DueDate`):
* `Paid` (Đã thanh toán): $\text{PaidAmount} \ge \text{GrandTotal}$.
* `Partial` (Thanh toán một phần): $0 < \text{PaidAmount} < \text{GrandTotal}$.
* `Unpaid` (Chưa thanh toán): $\text{PaidAmount} = 0$.
* `Overdue` (Quá hạn): Hóa đơn chưa thanh toán đầy đủ và ngày hiện tại đã vượt quá hạn thanh toán (`DueDate`).

### C. Tự động kích hoạt quyền Bảo hành (`WarrantyCoverage`)
Một trong những tự động hóa quan trọng nhất của hệ thống nằm ở phương thức `SaveSalesInvoice`:
* **Nguyên lý:** Khi một hóa đơn bán hàng (`SalesInvoice`) được lưu và liên kết với một phiếu xuất kho bán hàng (`StockOutId`), hệ thống sẽ quét toàn bộ các dòng xuất kho để tìm các số serial được xuất bán.
* **Xử lý:** Với mỗi serial tìm thấy, hệ thống tự động kiểm tra xem đã có bản ghi bảo hành chưa. Nếu chưa, hệ thống tự động tạo mới quyền bảo hành `WarrantyCoverage`:
  ```csharp
  var coverage = new WarrantyCoverage
  {
      ProductSerialId = serial.Id,
      CustomerId = invoice.CustomerId,
      SalesInvoiceId = invoice.Id,
      WarrantyStartDate = invoice.InvoiceDate,
      WarrantyEndDate = invoice.InvoiceDate.AddMonths(months), // Lấy từ cấu hình sản phẩm
      CoverageStatus = "Active"
  };
  db.WarrantyCoverages.Add(coverage);
  ```
  Cơ chế này đảm bảo nhân viên bán hàng không cần thao tác kích hoạt bảo hành thủ công, giảm thiểu sai sót dữ liệu và đảm bảo thiết bị bán ra luôn được theo dõi bảo hành ngay lập tức.

---

## 3. Dịch vụ Chuyển kho nội bộ (`StockTransferService`)

Dịch vụ này điều phối việc di chuyển hàng hóa giữa các kho hàng vật lý khác nhau trong hệ thống.

* **Quy tắc kiểm tra:** Đảm bảo kho xuất và kho nhập phải khác nhau (`FromWarehouseId != ToWarehouseId`).
* **Tránh lỗi EF Core Duplicate Tracking:**
  Khi người dùng chọn các số Serial để chuyển kho, các thực thể Serial này ở trạng thái tạm thời (transient). Nếu trực tiếp gán vào dòng phiếu chuyển kho, EF Core sẽ cố gắng chèn mới và gây ra lỗi trùng khóa chính. Dịch vụ giải quyết bằng cách ánh xạ ngược lại các thực thể đang được theo dõi bởi DbContext:
  ```csharp
  var dbSerials = db.ProductSerials
      .Where(ps => ps.ProductId == line.ProductId && serials.Contains(ps.SerialNumber))
      .ToList();
  line.ProductSerials = dbSerials;
  ```
* **Thực thi ghi sổ chuyển kho:**
  Khi ghi sổ, hệ thống gọi `postingService.PostStockTransfer` để:
  * Trừ tồn kho tại kho xuất và cộng tồn kho tương ứng tại kho nhập.
  * Cập nhật lại trường `CurrentWarehouseId` của từng số serial sang ID kho nhập mới.

---

## 4. Dịch vụ Kiểm kê (`StockCountService`) và Điều chỉnh kho (`StockAdjustmentService`)

Quy trình kiểm kê kho giúp đồng bộ hóa số lượng tồn thực tế ngoài đời và số lượng tồn trên sổ sách hệ thống.

### A. Kiểm kê kho (`StockCountService`)
1. Nhân viên tạo một phiên kiểm kê (`StockCountSession`). Hệ thống sẽ tự động chụp lại số lượng tồn sổ sách hiện tại (`SystemQuantity`).
2. Nhân viên tiến hành đếm thực tế và cập nhật số lượng đếm được (`CountedQuantity`).
3. Hệ thống tự động tính toán chênh lệch chênh lệch:
   $$\text{VarianceQuantity} = \text{CountedQuantity} - \text{SystemQuantity}$$
4. Khi ghi sổ phiên kiểm kê, hệ thống tự động tạo ra một chứng từ điều chỉnh kho `StockAdjustment` chứa các dòng chênh lệch.

### B. Điều chỉnh kho (`StockAdjustmentService`)
* **Mục đích:** Xử lý chênh lệch kiểm kê để đồng bộ lại tồn kho.
* **Cơ chế hoạt động:**
  Duyệt qua các dòng điều chỉnh, nếu chênh lệch dương (thực tế nhiều hơn hệ thống), hệ thống phát lệnh nhập kho điều chỉnh. Nếu chênh lệch âm (thực tế ít hơn hệ thống), hệ thống phát lệnh xuất kho điều chỉnh:
  * **Chênh lệch dương:** Gọi `PostStockIn` với loại `StockInKind.Adjustment`.
  * **Chênh lệch âm:** Gọi `PostStockOut` với loại `StockOutKind.Adjustment`.
  * Quá trình này tự động cập nhật lại `OnHandQuantity` và `AvailableQuantity` về đúng giá trị thực tế đếm được.

---

## 5. Dịch vụ Thống kê Dashboard (`DashboardService`)

Dịch vụ này chịu trách nhiệm cung cấp dữ liệu báo cáo nhanh cho màn hình chính của ứng dụng.

### A. Tối ưu hóa tải dữ liệu song song (`Task.WhenAll`)
Thay vì thực thi tuần tự từng câu lệnh SQL (gây trễ giao diện), dịch vụ khởi tạo các Task truy vấn độc lập và chạy song song, tận dụng khả năng xử lý đa luồng của hệ quản trị cơ sở dữ liệu:
```csharp
var revenueExpenseTask = GetRevenueAndExpenseChartDataAsync(6);
var inventoryStructureTask = GetInventoryStructureChartDataAsync();
var topSellingTask = GetTopSellingProductsAsync(5);
var stockMovementTask = GetStockMovementTrendAsync(7);

await Task.WhenAll(revenueExpenseTask, inventoryStructureTask, topSellingTask, stockMovementTask);
```

### B. Giải quyết giới hạn EF Client Projection
* **Vấn đề:** Khi truy vấn dữ liệu biểu đồ phân tích cơ cấu tồn kho hoặc doanh thu, một số phép toán chuỗi phức tạp (như ép kiểu ngày tháng sang chuỗi định dạng `"MM/yyyy"`) không thể chuyển dịch thành câu lệnh SQL thuần túy bởi EF Core Translator, dẫn đến lỗi runtime.
* **Giải pháp:** Hệ thống sử dụng phương thức `ToListAsync()` để tải trước tập dữ liệu thô đã được lọc từ SQL Server về bộ nhớ (RAM) của ứng dụng, sau đó mới thực hiện các phép chiếu dữ liệu, gom nhóm (GroupBy) và định dạng chuỗi bằng LINQ to Objects:
  ```csharp
  var balances = await context.StockBalances
      .Include(sb => sb.Product)
          .ThenInclude(p => p.Category)
      .ToListAsync(); // Tải về memory trước
  
  var grouped = balances
      .GroupBy(sb => sb.Product.Category?.DisplayName ?? "Chưa phân loại")
      .Select(g => new InventoryStructureData {
          CategoryName = g.Key,
          TotalValue = g.Sum(sb => sb.OnHandQuantity * sb.Product.DefaultPrice)
      }).ToList();
  ```

---

## 6. Phân hệ Nhập dữ liệu nâng cao (`Services/DataImport/`)

Đây là một phân hệ mạnh mẽ được thiết kế để xử lý việc nạp dữ liệu số lượng lớn từ các tệp Excel hoặc CSV.

### A. Dịch vụ Nhập dữ liệu động (`DynamicImportService`)
* **Tính năng:** Cho phép import bất kỳ bảng dữ liệu nào bằng cơ chế cấu hình ánh xạ động (Dynamic Column Mapping). Người dùng có thể tùy chọn cột A trong file Excel tương ứng với trường `ProductName` trong DB, cột B tương ứng với `DefaultPrice`...
* **Giải thích thuật toán Reflection:**
  Dịch vụ sử dụng cơ chế phản chiếu (Reflection) của C# để tự động đọc kiểu dữ liệu của thuộc tính thực thể và chuyển đổi giá trị chuỗi từ tệp Excel sang đúng kiểu dữ liệu trong DB (int, decimal, DateTime, bool...):
  ```csharp
  var prop = typeof(TEntity).GetProperty(propertyName);
  if (prop != null && prop.CanWrite)
  {
      object convertedValue = Convert.ChangeType(rawValue, prop.PropertyType);
      prop.SetValue(entity, convertedValue);
  }
  ```
  Điều này cho phép một lớp dịch vụ duy nhất có thể import cho mọi bảng dữ liệu khác nhau mà không cần viết lại mã nguồn.

### B. Dịch vụ Khởi tạo Dữ liệu Mẫu (`DatabaseSeeder`)
* **Vai trò:** Chứa thuật toán tự động sinh dữ liệu mẫu giả định rất chi tiết cho cơ sở dữ liệu khi hệ thống được cài đặt lần đầu.
* **Thuật toán sinh dữ liệu:**
  * Sinh ngẫu nhiên danh sách Danh mục, Thương hiệu, Nhà cung cấp, Khách hàng.
  * Tạo hơn 100 sản phẩm mẫu (bao gồm sản phẩm quản lý bằng serial và sản phẩm thông thường).
  * Sinh các hóa đơn mua hàng, phiếu nhập kho tương ứng, tự động sinh số serial hợp lệ.
  * Giả lập các giao dịch bán hàng, sinh hóa đơn bán hàng và tự động kích hoạt bảo hành cho khách hàng.
  * Giả lập phát sinh các hồ sơ bảo hành ở nhiều trạng thái khác nhau (`Open`, `Repairing`, `Closed`) để người dùng có thể trải nghiệm toàn bộ các tính năng báo cáo, biểu đồ ngay lập tức mà không cần tự nhập dữ liệu thủ công.
