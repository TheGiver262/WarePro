# Nhóm 6: Kiểm kê, chuyển kho và điều chỉnh kho

File này giải thích ba nghiệp vụ kho nâng cao. Đây là nhóm nghiệp vụ dễ được hội đồng hỏi vì nó thể hiện cách hệ thống bảo vệ tính đúng đắn của tồn kho sau khi đã có nhập kho và xuất kho cơ bản.

## 1. Bản đồ file liên quan

| Nghiệp vụ | View | ViewModel | Service | Inventory layer | Model |
|---|---|---|---|---|---|
| Kiểm kê kho | `Views/StockCountView.xaml` | `ViewModels/StockCountViewModel.cs` | `Services/StockCountService.cs` | Gián tiếp qua `StockInService`, `StockOutService` | `StockCountSession`, `StockCountLine` |
| Điều chỉnh kho | `Views/StockAdjustmentView.xaml` | `ViewModels/StockAdjustmentViewModel.cs` | `Services/StockAdjustmentService.cs` | `InventoryAdjustmentService` | `StockAdjustment`, `StockAdjustmentLine` |
| Chuyển kho | `Views/StockTransferView.xaml` | `ViewModels/StockTransferViewModel.cs` | `Services/StockTransferService.cs` | `InventoryPostingService.PostStockTransfer` | `StockTransfer`, `StockTransferLine` |

Lưu ý trạng thái UI hiện tại: `MainViewModel` có command mở `StockTransferView` và `StockAdjustmentView`, nhưng hai menu tương ứng đang bị comment trong `MainWindow.xaml`. Nghĩa là code nghiệp vụ đã có, nhưng trải nghiệm menu chính chưa bật đầy đủ.

## 2. Kiểm kê kho

### 2.1 Mục tiêu nghiệp vụ

Kiểm kê kho dùng để so sánh số lượng hệ thống đang ghi nhận với số lượng thực tế đếm được. Kết quả kiểm kê không nên cập nhật tồn kho ngay lập tức, vì số kiểm kê có thể nhập sai, thiếu serial hoặc cần người quản lý kiểm tra lại.

Thiết kế hiện tại chọn hướng an toàn:

1. Người dùng tạo phiên kiểm kê.
2. Hệ thống ghi nhận số lượng hệ thống và số lượng thực tế.
3. Nếu có chênh lệch, hệ thống tạo phiếu nhập/xuất điều chỉnh dạng nháp.
4. Người dùng kiểm tra phiếu nháp rồi mới ghi sổ để cập nhật tồn kho.

### 2.2 Các trạng thái chính

`StockCountSession.Status` đang dùng các giá trị tiếng Việt:

- `nháp`: phiên còn chỉnh sửa được.
- `đã kiểm kê`: đã chốt số đếm, có thể xử lý chênh lệch.
- `hoàn thành`: đã xử lý chênh lệch và tạo chứng từ điều chỉnh.

Trong code terminal có thể thấy lỗi encoding, nhưng về mặt nghiệp vụ đây là ba trạng thái trên.

### 2.3 Luồng tạo phiên kiểm kê

Luồng từ UI:

```text
StockCountView
  -> StockCountViewModel.ShowCreateNew()
  -> AddLine()
  -> StockCountLineEditor.SelectedProduct
  -> GetStockQuantity(productId)
  -> SaveDraft() hoặc SaveStockCount()
  -> StockCountService.CreateSession()
  -> AuditLog CREATE
```

`StockCountLineEditor` giữ dữ liệu nhập từng dòng:

- `SelectedProduct`: sản phẩm đang kiểm kê.
- `SystemQuantity`: tồn hệ thống tại kho đang chọn.
- `CountedQuantity`: số lượng thực tế người dùng đếm.
- `VarianceQuantity`: chênh lệch, được tính khi lưu vào model.
- `SerialNumbers`: danh sách serial nếu sản phẩm có quản lý serial.

Khi chọn sản phẩm, ViewModel gọi `GetStockQuantity(productId)` để lấy số tồn từ `StockBalance`. Đây là dữ liệu hệ thống dùng làm mốc so sánh.

### 2.4 Luồng chốt kiểm kê

`StockCountViewModel.SaveStockCount()` kiểm tra:

- Có mã phiên và có ít nhất một dòng.
- Mỗi dòng đã chọn sản phẩm.
- `CountedQuantity` hợp lệ và không âm.
- Nếu sản phẩm quản lý serial và có chênh lệch, số serial nhập phải bằng trị tuyệt đối của chênh lệch.

Sau đó ViewModel tạo `StockCountSession` với `Status = "đã kiểm kê"` và các dòng `StockCountLine`.

Điểm bảo vệ trước hội đồng: hệ thống không chỉ lưu số lượng đếm được, mà còn lưu cả số lượng hệ thống tại thời điểm kiểm kê. Nhờ đó về sau có thể giải thích vì sao phát sinh chênh lệch.

### 2.5 Luồng xử lý chênh lệch

`StockCountService.ProcessResults(sessionId, userId)` xử lý phiên có trạng thái `đã kiểm kê`.

Với từng dòng:

- Nếu `VarianceQuantity == 0`: bỏ qua.
- Nếu `VarianceQuantity > 0`: tạo phiếu nhập kho nháp `StockIn` với `PurposeCode = "Adjustment"`.
- Nếu `VarianceQuantity < 0`: tạo phiếu xuất kho nháp `StockOut` với `PurposeCode = "Adjustment"`.

Vì phiếu được tạo ở dạng nháp, tồn kho chưa đổi ngay. Người dùng phải đi qua luồng ghi sổ nhập/xuất kho đã có để cập nhật `StockBalance`, `StockLedger`, serial và audit.

Đây là thiết kế hai bước:

```text
Kiểm kê phát hiện chênh lệch
  -> tạo chứng từ điều chỉnh nháp
  -> kiểm tra lại chứng từ
  -> ghi sổ chứng từ
  -> tồn kho thay đổi
```

### 2.6 Vì sao cần khách hàng hệ thống `CUS-ADJ`?

Khi kiểm kê thiếu hàng, hệ thống phải tạo phiếu xuất kho điều chỉnh. Model `StockOut` yêu cầu `CustomerId`, nên service tạo hoặc dùng khách hàng đặc biệt:

```text
CustomerCode = "CUS-ADJ"
DisplayName = "Khách hàng điều chỉnh (Hệ thống)"
```

Đây không phải khách hàng thật, mà là đối tượng kỹ thuật để chứng từ xuất kho điều chỉnh vẫn hợp lệ theo schema.

## 3. Điều chỉnh kho trực tiếp

### 3.1 Khi nào dùng điều chỉnh kho?

Điều chỉnh kho trực tiếp dùng khi đã có lý do nghiệp vụ rõ ràng như hỏng, mất, hết hạn hoặc điều chỉnh thủ công. Khác với kiểm kê, luồng này ghi nhận một phiếu điều chỉnh riêng và khi post sẽ gọi trực tiếp `InventoryAdjustmentService`.

### 3.2 Luồng lưu nháp

```text
StockAdjustmentView
  -> StockAdjustmentViewModel.CreateNew()
  -> AddLine()
  -> SaveDraft()
  -> StockAdjustmentService.SaveDraft()
```

`StockAdjustmentLineEditor` chứa:

- `SelectedProduct`.
- `Direction`: `In` hoặc `Out`.
- `Quantity`.
- `SelectedUnit` và `BaseQuantity`.
- `SelectedSerial` nếu sản phẩm quản lý serial.

ViewModel kiểm tra:

- Có mã chứng từ.
- Có ít nhất một dòng.
- Mỗi dòng có sản phẩm.
- Số lượng lớn hơn 0.
- Nếu sản phẩm quản lý serial thì phải chọn serial.

### 3.3 Luồng ghi sổ điều chỉnh

```text
ConfirmAndPost()
  -> SaveDraft()
  -> StockAdjustmentService.Post(adjustmentId, userId)
  -> BeginTransaction()
  -> set Status = Posted
  -> InventoryAdjustmentService.PostAdjustment(...)
  -> Commit()
```

`StockAdjustmentService.Post()` chuyển các dòng `StockAdjustmentLine` thành `StockAdjustmentLineCommand`:

- `ProductId`.
- `Direction` chuyển thành `StockLedgerDirection`.
- `BaseQuantityDelta` chuyển thành số lượng tuyệt đối.
- Serial lấy từ `ProductSerialId` nếu có.

Sau đó `InventoryAdjustmentService` thực hiện cập nhật tồn kho và ghi ledger.

### 3.4 Điểm cần giải thích khi hội đồng hỏi

Điều chỉnh kho và kiểm kê khác nhau ở mức kiểm soát:

- Kiểm kê là phát hiện chênh lệch, sau đó tạo phiếu điều chỉnh nháp.
- Điều chỉnh kho là chứng từ điều chỉnh trực tiếp, phù hợp khi lý do đã xác định.

Thiết kế này tách hai câu hỏi:

- "Tồn thực tế lệch bao nhiêu?" thuộc kiểm kê.
- "Có cho phép thay đổi tồn kho không?" thuộc ghi sổ chứng từ điều chỉnh.

## 4. Chuyển kho nội bộ

### 4.1 Mục tiêu nghiệp vụ

Chuyển kho không làm thay đổi tổng tồn toàn hệ thống, nhưng làm thay đổi tồn theo từng kho. Vì vậy một phiếu chuyển kho phải có:

- Kho đi.
- Kho đến.
- Danh sách sản phẩm.
- Số lượng quy đổi về đơn vị cơ sở.
- Serial nếu sản phẩm quản lý serial.

### 4.2 Luồng lưu nháp

```text
StockTransferView
  -> StockTransferViewModel.SaveDraft()
  -> CreateModel()
  -> CreateLines()
  -> StockTransferService.SaveDraft()
  -> AuditLog CREATE/UPDATE
```

Trong `SaveDraft`, service có đoạn map serial tạm sang serial đang được EF tracking:

```text
line.ProductSerials chứa serial nhập từ UI
  -> tìm ProductSerial thật trong DB theo ProductId + SerialNumber
  -> thay collection bằng entity từ DB
```

Mục đích là tránh lỗi unique key do EF tưởng serial nhập từ UI là serial mới cần insert.

### 4.3 Luồng ghi sổ chuyển kho

```text
ConfirmAndPost()
  -> SaveDraft()
  -> StockTransferService.Post()
  -> BeginTransaction()
  -> validate kho đi khác kho đến
  -> validate đủ serial
  -> Status = Posted
  -> InventoryPostingService.PostStockTransfer()
  -> AuditLog UPDATE
  -> Commit()
```

`StockTransferService.Post()` duyệt dòng theo `ProductId` tăng dần. Đây là chiến lược giảm nguy cơ deadlock nếu nhiều người cùng ghi sổ các sản phẩm giống nhau.

### 4.4 Ràng buộc serial khi chuyển kho

Khi sản phẩm `IsSerialTracked = true`, số serial phải bằng số lượng dòng. Dialog nhập serial chỉ lấy serial đang `InStock` tại kho đi. Điều này ngăn người dùng chuyển một serial không thuộc kho nguồn.

Khi post, `PostStockTransferCommand` nhận:

- `StockTransferId`.
- `FromWarehouseId`.
- `ToWarehouseId`.
- `ProductId`.
- `Quantity`.
- `serials`.
- `userId`.

Inventory layer sẽ giảm tồn kho đi, tăng tồn kho đến và cập nhật vị trí hiện tại của serial.

## 5. So sánh ba nghiệp vụ

| Tiêu chí | Kiểm kê | Điều chỉnh kho | Chuyển kho |
|---|---|---|---|
| Mục đích | So sánh thực tế với hệ thống | Tăng/giảm tồn do lý do cụ thể | Di chuyển hàng giữa kho |
| Có làm đổi tổng tồn? | Chỉ khi phiếu điều chỉnh sau đó được post | Có | Không |
| Có đổi tồn theo kho? | Có, gián tiếp | Có | Có |
| Ghi ledger trực tiếp? | Không, tạo phiếu nháp nhập/xuất | Có | Có |
| Cần serial? | Khi sản phẩm serial-tracked và có chênh lệch | Khi sản phẩm serial-tracked | Khi sản phẩm serial-tracked |
| Rủi ro chính | Nhập sai số kiểm kê | Điều chỉnh sai lý do/số lượng | Chuyển nhầm kho hoặc serial |

## 6. Câu trả lời mẫu khi bảo vệ

**Hỏi:** Vì sao kiểm kê không cập nhật tồn kho ngay?

**Trả lời:** Vì kiểm kê là bước xác nhận thực tế, có thể có sai sót khi đếm hoặc nhập serial. Hệ thống chỉ tạo phiếu nhập/xuất điều chỉnh dạng nháp. Sau đó người có quyền kiểm tra và ghi sổ chứng từ. Cách này giữ audit trail rõ ràng và tránh làm sai tồn kho ngay lập tức.

**Hỏi:** Chuyển kho có làm thay đổi giá trị tồn kho không?

**Trả lời:** Không làm thay đổi tổng số lượng và tổng giá trị toàn hệ thống. Nó chỉ chuyển số lượng từ kho đi sang kho đến. Tuy nhiên hệ thống vẫn ghi ledger để truy vết lịch sử theo kho và serial.

**Hỏi:** Điều chỉnh kho khác phiếu xuất kho điều chỉnh như thế nào?

**Trả lời:** `StockAdjustment` là chứng từ điều chỉnh trực tiếp qua `InventoryAdjustmentService`. Còn phiếu xuất kho điều chỉnh có thể được sinh từ kiểm kê, đi theo luồng `StockOutService` quen thuộc. Hai hướng này cùng cập nhật tồn kho nhưng phục vụ hai tình huống nghiệp vụ khác nhau.
