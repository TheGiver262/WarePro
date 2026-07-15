# 10 - Ghi sổ nhập kho / xuất kho: đọc code sao cho dễ hiểu

File này giải thích các phần khó trong:

- `QuanLyHangHoa/Services/StockInService.cs`
- `QuanLyHangHoa/Services/StockOutService.cs`
- `QuanLyHangHoa/Inventory/InventoryPostingService.cs`

Nếu chỉ nhớ một câu: `StockInService` và `StockOutService` quản lý chứng từ, còn `InventoryPostingService` thật sự cập nhật tồn kho, serial, ledger.

## 1. Ba lớp đang phối hợp với nhau

Hãy tưởng tượng nghiệp vụ kho có 3 tầng:

```text
ViewModel
-> StockInService / StockOutService
-> InventoryPostingService
-> AppDbContext / SQL Server
```

Vai trò:

| Lớp | Dễ hiểu là | Việc chính |
|---|---|---|
| `StockInViewModel`, `StockOutViewModel` | Người nhận thao tác từ màn hình | Lấy dữ liệu form, gọi service, báo lỗi/thành công |
| `StockInService`, `StockOutService` | Người quản lý phiếu | Lưu nháp, sửa phiếu, validate phiếu, mở transaction, gọi lõi kho |
| `InventoryPostingService` | Kế toán kho thật sự | Tăng/giảm `StockBalance`, đổi trạng thái serial, ghi `StockLedger`, ghi audit |
| `EfInventoryUnitOfWork` | Cầu nối database cho lõi kho | Đọc/ghi balance, serial, ledger bằng EF Core |

## 2. Vì sao không để `StockInService` tự tăng tồn kho?

Vì nhập kho, xuất kho, chuyển kho, điều chỉnh kho, bảo hành đều cần cập nhật tồn kho. Nếu mỗi service tự viết logic tăng/giảm tồn, code sẽ bị lặp và dễ sai.

Dự án tách ra:

```text
StockInService.Post
-> gọi InventoryPostingService.PostStockIn

StockOutService.Post
-> gọi InventoryPostingService.PostStockOut

StockTransferService.Post
-> gọi InventoryPostingService.PostStockTransfer
```

Nhờ vậy quy tắc lõi như kiểm tra serial, chặn tồn âm, ghi ledger được gom một nơi.

## 3. `SaveDraft` nghĩa là gì?

`SaveDraft` là lưu phiếu nháp. Phiếu nháp chưa làm thay đổi tồn kho.

Ví dụ trong `StockInService.SaveDraft`:

```csharp
stockIn.Status = DocumentStatus.Draft;
db.StockIns.Add(stockIn);
db.SaveChanges();
```

Ý nghĩa:

1. Tạo phiếu nhập.
2. Gắn trạng thái nháp.
3. Lưu phiếu và dòng phiếu vào database.
4. Chưa tăng `StockBalance`.
5. Chưa tạo `ProductSerial` chính thức.
6. Chưa ghi `StockLedger`.

Nguyên tắc: **Draft là giấy nháp, Posted mới là sổ chính thức.**

## 4. Vì sao có `DraftSerials`?

Trong phiếu nháp, người dùng có thể nhập serial nhưng chưa ghi sổ. Serial này chưa nên trở thành `ProductSerial` thật trong kho, vì phiếu vẫn có thể sửa hoặc xóa.

Vì vậy code gom serial tạm vào chuỗi:

```csharp
line.DraftSerials = string.Join(",", serials);
line.ProductSerials = new List<ProductSerial>();
```

Dễ hiểu:

```text
Trước khi ghi sổ:
StockInLine.DraftSerials = "SN001,SN002,SN003"
ProductSerial chưa tạo thật

Sau khi ghi sổ:
ProductSerial được insert thật
StockLedger được ghi
StockBalance được tăng
```

Lý do `line.ProductSerials = new List<ProductSerial>()`: tránh EF Core hiểu nhầm rằng mình muốn insert/update `ProductSerial` ngay lúc lưu nháp.

## 5. `BaseQuantity` là gì?

Một sản phẩm có thể nhập theo thùng, hộp, cái. Nhưng tồn kho nên quy về đơn vị gốc.

Ví dụ:

```text
1 thùng = 10 cái
Nhập 2 thùng
Quantity = 2
BaseQuantity = 20
```

Code:

```csharp
var pu = unitMap.FirstOrDefault(u => u.ProductId == line.ProductId && u.UnitId == line.UnitId);
line.BaseQuantity = line.Quantity * (pu?.ConversionFactor ?? 1m);
```

Nếu tìm thấy quy đổi đơn vị thì nhân theo `ConversionFactor`. Nếu không có thì mặc định nhân `1`.

## 6. Luồng `StockInService.Post`

Đọc method `Post` theo 8 khối:

```text
1. Mở db và transaction
2. Load phiếu + dòng phiếu
3. Chặn phiếu không tồn tại hoặc đã posted
4. Tính BaseQuantity
5. Lấy serial từ DraftSerials
6. Validate serial
7. Đổi phiếu sang Posted
8. Gọi InventoryPostingService cho từng dòng
9. Bind serial với LastStockInLineId
10. Ghi audit và commit transaction
```

Code mở đầu:

```csharp
using var db = _contextFactory();
using var transaction = db.Database.BeginTransaction();
```

Dịch sang tiếng người: “Từ đây trở xuống, nếu có lỗi thì tất cả thay đổi phải quay lại như cũ.”

## 7. Validate serial khi nhập kho

Trong nhập kho, serial phải thỏa các điều kiện:

1. Nếu sản phẩm quản lý serial thì số serial phải bằng số lượng nhập.
2. Serial không được trùng trong cùng phiếu.
3. Serial chưa được tồn tại trong hệ thống.

Code đếm serial:

```csharp
if (serials.Count != (int)line.Quantity)
{
    throw new Exception($"Sản phẩm {product.DisplayName} yêu cầu {(int)line.Quantity} serial, nhưng hiện có {serials.Count}.");
}
```

Code kiểm tra serial đã có trong DB:

```csharp
var existingDbSerials = db.ProductSerials
    .Where(ps => serials.Contains(ps.SerialNumber))
    .Select(ps => ps.SerialNumber)
    .ToList();
```

Code kiểm tra trùng trong cùng phiếu:

```csharp
var duplicateDocumentSerials = allDocumentSerials
    .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
    .Where(g => g.Count() > 1)
    .Select(g => g.Key)
    .ToList();
```

Dễ hiểu: gom các serial giống nhau thành nhóm, nhóm nào có hơn 1 phần tử là trùng.

## 8. Vì sao set `stockIn.Status = Posted` trước khi gọi lõi kho?

`InventoryPostingService.PostStockIn` chỉ cho ghi sổ phiếu có trạng thái hợp lệ:

```csharp
if (command.Status != StockDocumentStatus.Approved && command.Status != StockDocumentStatus.Posted)
{
    throw new InventoryDomainException("Only approved or ready-to-post stock-in documents can be posted.");
}
```

Vì vậy service cấp trên đổi trạng thái phiếu sang `Posted`, lưu lại, rồi gọi lõi kho.

Nếu lõi kho lỗi, transaction rollback, trạng thái `Posted` cũng rollback.

## 9. `InventoryPostingService.PostStockIn` làm gì?

Rút gọn:

```text
Validate command
-> lấy Product
-> chuẩn hóa serial
-> kiểm tra serial
-> lấy/tạo StockBalance
-> tăng OnHand và Available
-> tạo ProductSerial trạng thái InStock
-> thêm StockLedger hướng In
-> thêm AuditLog
-> MarkDocumentPosted
-> Commit
```

Đoạn tăng tồn:

```csharp
_unitOfWork.SaveBalance(balance with
{
    OnHandQuantity = balance.OnHandQuantity + (int)command.Quantity,
    AvailableQuantity = balance.AvailableQuantity + (int)command.Quantity
});
```

Dễ hiểu:

```text
Trước: tồn 5
Nhập: 3
Sau: tồn 8
```

Đoạn tạo serial:

```csharp
_unitOfWork.SaveSerial(new ProductSerialSnapshot(
    serialNumber,
    command.ProductId,
    warehouseId,
    SerialStatus.InStock));
```

Dễ hiểu: serial mới vừa nhập đang nằm trong kho, trạng thái `InStock`.

## 10. Luồng `StockOutService.Post`

Xuất kho giống nhập kho nhưng kiểm tra chặt hơn vì có nguy cơ tồn âm.

Các khối chính:

```text
1. Mở transaction
2. Load phiếu xuất + dòng
3. Chặn phiếu đã posted
4. Tính BaseQuantity
5. Lấy serial từ DraftSerials
6. Validate serial thuộc đúng sản phẩm, đúng kho, đang InStock
7. Chặn serial trùng trong phiếu
8. Đổi phiếu sang Posted
9. Gọi InventoryPostingService.PostStockOut
10. Bind LastStockOutLineId
11. Ghi audit và commit
```

## 11. Validate serial khi xuất kho

Code kiểm tra serial:

```csharp
if (dbSerial == null || 
    dbSerial.ProductId != line.ProductId || 
    dbSerial.CurrentWarehouseId != stockOut.WarehouseId || 
    dbSerial.CurrentStatus != "InStock")
{
    invalidSerials.Add(sn);
}
```

Dịch từng dòng:

| Điều kiện | Nghĩa |
|---|---|
| `dbSerial == null` | Serial không tồn tại |
| `ProductId != line.ProductId` | Serial không thuộc sản phẩm đang xuất |
| `CurrentWarehouseId != stockOut.WarehouseId` | Serial không nằm ở kho xuất |
| `CurrentStatus != "InStock"` | Serial không còn sẵn để xuất |

## 12. `InventoryPostingService.PostStockOut` làm gì?

Rút gọn:

```text
Validate command
-> lấy Product
-> chuẩn hóa serial
-> chặn serial trùng
-> kiểm tra đủ tồn khả dụng
-> kiểm tra serial hợp lệ
-> giảm StockBalance
-> đổi serial thành Sold, CurrentWarehouseId = null
-> thêm StockLedger hướng Out
-> thêm AuditLog
-> Commit
```

Đoạn chặn tồn âm:

```csharp
var balance = _unitOfWork.FindBalance(command.ProductId, warehouseId);
if (balance is null || balance.AvailableQuantity < command.Quantity)
{
    throw new InventoryDomainException("Insufficient available stock.");
}
```

Dễ hiểu:

```text
Nếu kho còn 5 mà muốn xuất 7 -> chặn.
Nếu không chặn -> StockBalance sẽ âm, báo cáo sai.
```

Đoạn đổi serial sau khi bán:

```csharp
_unitOfWork.SaveSerial(serial with
{
    Status = SerialStatus.Sold,
    CurrentWarehouseId = null
});
```

Dễ hiểu: serial đã bán ra khỏi kho, không còn thuộc kho nào trong hệ thống.

## 13. Vì sao `OrderBy(l => l.ProductId)` trước khi post?

Trong `StockInService.Post`:

```csharp
foreach (var line in stockIn.Lines.OrderBy(l => l.ProductId))
```

Đây là chiến lược giảm deadlock. Nếu nhiều người cùng ghi sổ nhiều sản phẩm, việc luôn xử lý theo cùng thứ tự `ProductId` giúp database khóa record theo thứ tự ổn định hơn.

Người mới chỉ cần nhớ: **xử lý theo thứ tự cố định giúp giảm xung đột khi nhiều giao dịch chạy cùng lúc.**

## 14. `StockBalance` và `StockLedger` khác nhau thế nào?

| Bảng | Trả lời câu hỏi | Ví dụ |
|---|---|---|
| `StockBalance` | Hiện còn bao nhiêu? | Laptop ở kho chính còn 12 |
| `StockLedger` | Vì sao số tồn thay đổi? | Ngày 01 nhập 10, ngày 02 xuất 3 |

Khi ghi sổ, hệ thống phải cập nhật cả hai:

```text
StockBalance để màn hình tồn kho chạy nhanh.
StockLedger để truy vết lịch sử và báo cáo.
```

## 15. Những lỗi người mới dễ hiểu sai

### Lỗi 1: Tưởng SaveDraft đã nhập kho thật

Sai. SaveDraft chỉ lưu phiếu nháp. Tồn kho chỉ đổi khi `Post` thành công.

### Lỗi 2: Tưởng `ProductSerials` trong line luôn là serial thật

Không hẳn. Khi nháp, serial được lưu trong `DraftSerials`. Khi posted, `InventoryPostingService` mới tạo/cập nhật serial thật.

### Lỗi 3: Tưởng audit log là ledger

Sai. Audit log ghi ai làm gì. Ledger ghi kho tăng/giảm bao nhiêu.

### Lỗi 4: Tưởng `db.SaveChanges()` là đủ an toàn

Không đủ cho nghiệp vụ nhiều bước. Cần transaction để nếu bước sau lỗi thì bước trước rollback.

## 16. Cách đọc lại code nhanh

Đọc theo thứ tự này:

1. `StockInService.SaveDraft`: hiểu phiếu nháp.
2. `StockInService.Post`: hiểu duyệt/ghi sổ nhập.
3. `InventoryPostingService.PostStockIn`: hiểu tăng tồn.
4. `StockOutService.SaveDraft`: hiểu phiếu xuất nháp.
5. `StockOutService.Post`: hiểu duyệt/ghi sổ xuất.
6. `InventoryPostingService.PostStockOut`: hiểu giảm tồn.
7. `QuanLyHangHoa.Tests/Inventory/*`: xem test xác nhận hành vi.
