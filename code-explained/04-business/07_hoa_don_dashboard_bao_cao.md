# Nhóm 7: Hóa đơn, dashboard và báo cáo

File này giải thích ba nhóm chức năng phục vụ quản trị sau khi dữ liệu kho đã phát sinh: hóa đơn mua/bán, dashboard KPI và báo cáo phân tích.

## 1. Bản đồ file liên quan

| Chức năng | ViewModel | Service | Model |
|---|---|---|---|
| Hóa đơn bán | `SalesInvoiceViewModel.cs` | `InvoiceService.cs` | `SalesInvoice`, `SalesInvoiceLine` |
| Hóa đơn mua | `PurchaseInvoiceViewModel.cs` | `InvoiceService.cs` | `PurchaseInvoice`, `PurchaseInvoiceLine` |
| Dashboard | `DashboardViewModel.cs` | `DashboardService.cs` | DTO trong `DashboardService.cs` |
| Báo cáo | `ReportViewModel.cs` | Truy vấn trực tiếp `AppDbContext` | DTO trong `ReportViewModel.cs` |

## 2. Hóa đơn mua và hóa đơn bán

### 2.1 Vai trò trong hệ thống

Phiếu nhập/xuất kho trả lời câu hỏi "hàng đã vào/ra kho chưa?". Hóa đơn trả lời câu hỏi "giá trị thương mại và công nợ của giao dịch là bao nhiêu?".

Vì vậy hệ thống tách chứng từ kho và hóa đơn:

- `StockIn`, `StockOut`: cập nhật tồn kho.
- `PurchaseInvoice`, `SalesInvoice`: cập nhật tiền hàng, thuế, thanh toán, công nợ.

Hóa đơn có thể liên kết với chứng từ kho qua:

- `PurchaseInvoice.StockInId`
- `SalesInvoice.StockOutId`

### 2.2 Cấu trúc dòng hóa đơn

Mỗi dòng hóa đơn có:

- Sản phẩm.
- Đơn vị tính.
- Số lượng.
- Đơn giá.
- Thuế suất.
- Thành tiền trước thuế.
- Tiền thuế.
- Tổng tiền sau thuế.

`InvoiceService.CalculateLine()` áp dụng công thức:

```text
SubTotal = Quantity * UnitPrice
TaxAmount = SubTotal * TaxRate
GrandTotal = SubTotal + TaxAmount
```

Service cũng kiểm tra:

- Số lượng phải lớn hơn 0.
- Đơn giá không âm.
- Thuế suất không âm.

### 2.3 Trạng thái thanh toán

Sau khi tính tổng hóa đơn, service cập nhật `PaymentStatus`:

| Điều kiện | Trạng thái lưu trong DB | Nhãn tiếng Việt ở UI |
|---|---|---|
| Đã trả đủ và tổng tiền > 0 | `Paid` | `Đã TT` |
| Đã trả một phần | `Partial` | `TT 1 phần` |
| Chưa trả | `Unpaid` | `Chưa TT` |
| Chưa trả đủ và quá hạn | `Overdue` | `Quá hạn` |

Điểm cần nhớ: `Overdue` có thể ghi đè lên `Unpaid` hoặc `Partial` nếu `DueDate` đã qua.

### 2.4 Lưu hóa đơn và tránh lỗi EF tracking

Khi sửa hóa đơn, `InvoiceService` không cố cập nhật từng dòng cũ một cách phức tạp. Nó dùng chiến lược rõ ràng:

1. Load hóa đơn hiện có kèm các dòng.
2. Xóa các dòng cũ.
3. Cập nhật header.
4. Thêm lại danh sách dòng mới.

Trong ViewModel, trước khi lưu, navigation properties như `Customer`, `StockOut`, `Creator` được set về `null` để EF không nhầm giữa entity đang track và entity chỉ dùng để hiển thị.

Đây là câu trả lời tốt nếu hội đồng hỏi: "Tại sao phải clear navigation property?". Vì ViewModel lấy object từ UI, nếu đưa thẳng object có navigation sang EF, EF có thể attach trùng hoặc insert/update nhầm entity liên quan.

### 2.5 Hóa đơn bán tự tạo quyền bảo hành

`InvoiceService.SaveSalesInvoice()` có logic đặc biệt:

1. Lưu hóa đơn bán.
2. Nếu hóa đơn có `StockOutId`, load phiếu xuất kho tương ứng.
3. Lấy từng serial đã xuất bán.
4. Tạo `WarrantyCoverage` cho serial đó nếu chưa tồn tại.
5. Thời hạn bảo hành lấy từ `Product.WarrantyPeriodMonths`, nếu không có thì mặc định 12 tháng.

Ý nghĩa nghiệp vụ:

- Bảo hành bắt đầu khi bán hàng, không phải khi nhập kho.
- Bảo hành bám theo serial cụ thể, không chỉ theo mã sản phẩm.
- Khi khách mang serial tới bảo hành, hệ thống truy được hóa đơn và khách hàng.

### 2.6 Hóa đơn mua không tự cập nhật kho

Hóa đơn mua chỉ ghi nhận công nợ và giá trị mua. Việc tăng tồn kho thuộc về phiếu nhập kho. Nếu hóa đơn mua liên kết `StockInId`, nó giúp đối chiếu giữa chứng từ kho và chứng từ kế toán, nhưng không thay thế nghiệp vụ ghi sổ nhập kho.

## 3. ViewModel hóa đơn

`PurchaseInvoiceViewModel` và `SalesInvoiceViewModel` có cấu trúc rất giống nhau:

- Danh sách hóa đơn.
- Bộ lọc nâng cao.
- Tab form tạo/sửa/xem.
- Danh sách dòng hóa đơn.
- Tính tổng tiền form theo thời gian thực.
- Phân trang/lazy load với `_skip` và `PageSize = 100`.

Các property quan trọng:

- `SelectedTabIndex`: chuyển giữa danh sách và form.
- `IsViewMode`, `IsEditMode`: kiểm soát form đang xem hay sửa.
- `FormSubTotal`, `FormTaxAmount`, `FormTotalAmount`, `FormRemainingAmount`: số liệu tức thời trên form.
- `SelectedFilterPaymentStatus`: nhãn tiếng Việt, được chuyển sang trạng thái tiếng Anh trước khi query.

Luồng lưu hóa đơn bán:

```text
SalesInvoiceView
  -> SalesInvoiceViewModel.SaveInvoice()
  -> Validate customer và dòng hàng
  -> Map form sang SalesInvoice + SalesInvoiceLine
  -> InvoiceService.SaveSalesInvoice()
  -> CalculateSalesInvoice()
  -> Save DB
  -> Tạo WarrantyCoverage nếu có StockOutId
```

## 4. Dashboard

### 4.1 Dashboard khác báo cáo như thế nào?

Dashboard là màn hình tóm tắt nhanh tình hình hiện tại. Nó trả lời câu hỏi: "Hôm nay/tháng này hệ thống đang ra sao?".

Báo cáo là màn phân tích sâu. Nó trả lời câu hỏi: "Trong kỳ đã phát sinh gì, lợi nhuận bao nhiêu, tồn đầu/cuối ra sao, serial đi qua những chứng từ nào?".

### 4.2 `DashboardService.GetStatsAsync()`

Service tính các chỉ số:

- Tổng tồn kho hiện tại từ `StockBalances`.
- Số phiếu nhập trong tháng.
- Số phiếu xuất trong tháng.
- Số hóa đơn bán tháng/năm.
- Doanh thu tháng/năm.
- Số hóa đơn bán/mua chưa thanh toán đủ.
- Số yêu cầu bảo hành đang hoạt động/xử lý.
- Hoạt động gần đây từ `StockIn` và `StockOut`.

Sau đó service chạy song song bốn truy vấn biểu đồ:

- Doanh thu và chi phí 6 tháng.
- Cơ cấu giá trị tồn kho theo danh mục.
- Top sản phẩm bán chạy.
- Xu hướng nhập/xuất kho 7 ngày.

Điểm kỹ thuật quan trọng: `Task.WhenAll()` giúp dashboard tải nhanh hơn vì các dữ liệu biểu đồ độc lập.

### 4.3 `DashboardViewModel.UpdateCharts()`

ViewModel chuyển DTO từ service sang series của LiveCharts2:

- `ColumnSeries` cho doanh thu/chi phí.
- `LineSeries` cho xu hướng nhập/xuất.
- `PieSeries` cho cơ cấu tồn kho.
- `RowSeries` cho top sản phẩm.

`DashboardViewModel` cũng có command điều hướng nhanh:

- `NavigateToProducts`
- `NavigateToStockIn`
- `NavigateToSalesInvoices`
- `NavigateToPurchaseInvoices`
- `NavigateToWarranty`

Điều này giúp dashboard không chỉ là màn xem số liệu, mà là điểm vào nhanh tới các phân hệ cần xử lý.

## 5. Báo cáo phân tích

`ReportViewModel` gom bốn tab báo cáo.

### 5.1 Báo cáo doanh thu và lợi nhuận

Nguồn dữ liệu:

- Doanh thu từ `SalesInvoices.GrandTotal`.
- Chi phí từ `PurchaseInvoices.GrandTotal`.

Công thức:

```text
TotalRevenue = tổng hóa đơn bán trong kỳ
TotalCost = tổng hóa đơn mua trong kỳ
TotalProfit = TotalRevenue - TotalCost
```

ViewModel nhóm dữ liệu theo ngày để vẽ biểu đồ doanh thu/chi phí.

### 5.2 Báo cáo xuất nhập tồn tổng hợp

Nguồn dữ liệu chính là `StockLedger`. Với mỗi sản phẩm:

```text
Tồn đầu kỳ = tổng In - Out trước FromDate
Nhập trong kỳ = tổng In từ FromDate đến ToDate
Xuất trong kỳ = tổng Out từ FromDate đến ToDate
Tồn cuối kỳ = Tồn đầu kỳ + Nhập trong kỳ - Xuất trong kỳ
Giá trị = Số lượng * giá vốn, nếu không có giá vốn thì dùng giá bán mặc định
```

Đây là một trong các phần quan trọng nhất để bảo vệ vì nó chứng minh `StockLedger` là nguồn dữ liệu lịch sử cho báo cáo.

### 5.3 Sổ kho / thẻ kho chi tiết

Tab này cần chọn một sản phẩm. ViewModel:

1. Load toàn bộ ledger của sản phẩm đến cuối kỳ.
2. Tính tồn đầu kỳ từ ledger trước kỳ.
3. Duyệt ledger trong kỳ theo thời gian để tính tồn sau mỗi phát sinh.
4. Lookup chứng từ gốc để hiển thị mã chứng từ, mục đích và đối tác.

Ví dụ:

- `StockIn` -> nhà cung cấp.
- `StockOut` -> khách hàng.
- `StockAdjustment` -> hệ thống.

### 5.4 Truy vết serial

Tab này load `ProductSerial` kèm:

- Sản phẩm.
- Dòng nhập gần nhất và phiếu nhập.
- Dòng xuất gần nhất và phiếu xuất.
- Quyền bảo hành.

Kết quả trả lời được câu hỏi:

```text
Serial này nhập từ đâu?
Bán cho khách nào?
Bán theo phiếu nào?
Còn bảo hành không?
```

ViewModel giới hạn tối đa 100 serial để đảm bảo hiệu năng màn hình.

## 6. Các điểm mạnh để đưa vào đồ án

- Tách chứng từ kho và hóa đơn giúp hệ thống rõ giữa vật lý hàng hóa và giá trị thương mại.
- Hóa đơn bán tự sinh bảo hành theo serial, thể hiện liên kết liên phân hệ.
- Dashboard dùng truy vấn bất đồng bộ và chạy song song các chart độc lập.
- Báo cáo xuất nhập tồn dựa trên ledger nên có thể tái dựng lịch sử.
- Thẻ kho chi tiết không chỉ liệt kê số lượng mà còn nối ngược tới chứng từ nguồn.

## 7. Câu trả lời mẫu khi bảo vệ

**Hỏi:** Vì sao không lấy tồn cuối kỳ từ `StockBalance` cho báo cáo xuất nhập tồn?

**Trả lời:** `StockBalance` chỉ cho biết số dư hiện tại. Báo cáo theo kỳ cần biết tồn đầu kỳ, nhập trong kỳ, xuất trong kỳ và tồn cuối kỳ tại một mốc thời gian. Vì vậy phải dùng `StockLedger`, vì ledger lưu lịch sử từng phát sinh.

**Hỏi:** Vì sao hóa đơn bán tạo bảo hành chứ không phải phiếu xuất kho?

**Trả lời:** Phiếu xuất kho chỉ xác nhận hàng rời kho. Bảo hành với khách hàng bắt đầu theo giao dịch bán và ngày hóa đơn. Do đó `SalesInvoice` là điểm phù hợp để tạo `WarrantyCoverage`, còn serial lấy từ phiếu xuất kho liên kết.

**Hỏi:** Dashboard và Report có trùng chức năng không?

**Trả lời:** Không. Dashboard tối ưu cho xem nhanh KPI và điều hướng xử lý. Report tối ưu cho phân tích có kỳ, có bộ lọc và có dữ liệu chi tiết phục vụ kiểm tra, đối chiếu.
