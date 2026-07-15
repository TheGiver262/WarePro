# 11 - WarrantyClaimService: bảo hành, gửi hãng, đổi serial dễ hiểu

File này giải thích phần khó trong:

- `QuanLyHangHoa/Services/WarrantyClaimService.cs`
- `QuanLyHangHoa/Models/WarrantyClaim.cs`
- `QuanLyHangHoa/Models/WarrantyCoverage.cs`
- `QuanLyHangHoa/Models/ProductSerial.cs`

Nếu chỉ nhớ một câu: bảo hành trong dự án bám theo **serial cụ thể**, không chỉ theo mã sản phẩm.

## 1. Hai khái niệm dễ nhầm

| Khái niệm | Dễ hiểu là | Ví dụ |
|---|---|---|
| `WarrantyCoverage` | Quyền bảo hành | Serial SN001 được bảo hành từ 01/01 đến 31/12 |
| `WarrantyClaim` | Một lần khách đem đi bảo hành | Ngày 15/03 khách báo SN001 bị lỗi màn hình |

Một serial có thể có một coverage đang active, nhưng có thể phát sinh nhiều claim theo thời gian.

## 2. Trạng thái serial trong bảo hành

`ProductSerial.CurrentStatus` có thể đổi qua các trạng thái:

```text
Sold
-> InWarrantyProcess
-> ReturnedToManufacturer
-> Sold / Replaced
```

Dễ hiểu:

- `Sold`: đã bán cho khách.
- `InWarrantyProcess`: đang xử lý bảo hành nội bộ.
- `ReturnedToManufacturer`: đã gửi về hãng.
- `Replaced`: serial cũ đã bị thay bằng serial khác.

## 3. Tạo claim từ serial

Method:

```csharp
public int CreateClaim(string claimCode, string serialNumber, string problemDescription, int userId)
```

Luồng:

```text
1. Tìm ProductSerial theo serialNumber
2. Kiểm tra serial có tồn tại không
3. Tìm WarrantyCoverage active, còn hạn
4. Tạo WarrantyClaim trạng thái Open
5. Đổi serial sang InWarrantyProcess
6. SaveChanges
7. Trả về claim.Id
```

Đoạn quan trọng:

```csharp
var serial = db.ProductSerials.FirstOrDefault(s => s.SerialNumber == serialNumber)
    ?? throw new InvalidOperationException($"Serial {serialNumber} không tồn tại.");
```

Dịch: nếu không tìm thấy serial thì dừng ngay, vì không thể bảo hành một sản phẩm không có trong hệ thống.

Kiểm tra còn bảo hành:

```csharp
var coverage = db.WarrantyCoverages.FirstOrDefault(c =>
    c.ProductSerialId == serial.Id &&
    c.CoverageStatus == "Active" &&
    c.WarrantyEndDate >= DateTime.Now)
```

Dịch: chỉ nhận bảo hành nếu coverage active và chưa hết hạn.

## 4. Hoàn tất sửa chữa nội bộ

Method:

```csharp
CompleteRepair(int claimId, string technicalConclusion, int userId)
```

Luồng:

```text
1. Tìm claim
2. Ghi kết luận kỹ thuật
3. ResolutionType = Repair
4. Status = Ready
5. ApprovedBy = userId
6. Cập nhật lại trạng thái serial nếu không còn claim mở khác
```

Điểm khó nằm ở hàm:

```csharp
UpdateSerialStatusOnClaimClosure(db, claim.ProductSerialId, claim.Id);
```

Hàm này kiểm tra serial đó có claim nào khác chưa đóng không. Nếu không còn claim mở, serial có thể quay về `Sold`.

## 5. Gửi sản phẩm lỗi về hãng

Method:

```csharp
SendToManufacturer(...)
```

Luồng:

```text
1. Tìm claim
2. Lưu tên hãng, mã tracking, ngày dự kiến trả
3. Status = ManufacturerWait
4. Serial lỗi -> ReturnedToManufacturer
5. SaveChanges
```

Dễ hiểu: hồ sơ đang chờ hãng xử lý, nên serial không nằm ở trạng thái bảo hành nội bộ nữa mà là đã gửi hãng.

## 6. Nhận lại hàng đã sửa từ hãng

Method:

```csharp
ReceiveFromManufacturerRepaired(int claimId, string conclusion, int userId)
```

Trường hợp này hãng trả lại **chính serial cũ** sau khi sửa.

Luồng:

```text
1. Claim phải đang ManufacturerWait
2. Ghi kết luận
3. ResolutionType = ManufacturerRepair
4. Status = Closed
5. ClosedDate = Now
6. Serial quay về Sold nếu không còn claim mở khác
```

Không phát sinh nhập/xuất kho vì serial cũ vẫn là sản phẩm của khách, chỉ thay đổi trạng thái xử lý.

## 7. Nhận serial mới từ hãng: phần khó nhất

Method:

```csharp
ReceiveFromManufacturerReplaced(int claimId, string newSerialNumber, string conclusion, int userId)
```

Đây là luồng phức tạp nhất vì có nhiều việc xảy ra trong một transaction:

```text
1. Tìm claim + serial lỗi + product + coverage
2. Kiểm tra claim đang ManufacturerWait
3. Lấy kho mặc định
4. Lấy đơn vị gốc của sản phẩm
5. Đổi serial lỗi thành Replaced
6. Tạo phiếu nhập kho cho serial mới từ hãng
7. Gọi InventoryPostingService.PostStockIn để nhập serial mới vào kho
8. Tìm serial mới vừa tạo
9. Tạo phiếu xuất kho bảo hành cho khách
10. Gọi InventoryPostingService.PostStockOut để xuất serial mới
11. Tắt coverage cũ
12. Tạo coverage mới cho serial mới với số ngày bảo hành còn lại
13. Cập nhật claim: replacement serial, stock out, status Closed
14. Commit transaction
```

Vì sao cần transaction? Vì nếu nhập serial mới thành công nhưng xuất cho khách lỗi, dữ liệu sẽ bị kẹt. Transaction đảm bảo tất cả cùng thành công hoặc cùng rollback.

## 8. Vì sao vừa StockIn vừa StockOut khi hãng đổi mới?

Khi hãng gửi serial mới về, hệ thống phải ghi nhận hai sự kiện thật:

```text
Sự kiện 1: Kho nhận serial mới từ hãng
-> StockIn PurposeCode = WarrantyReceive
-> StockBalance tăng 1
-> ProductSerial mới thành InStock
-> Ledger IN

Sự kiện 2: Kho xuất serial mới trả khách
-> StockOut PurposeCode = WarrantyReplacement
-> StockBalance giảm 1
-> ProductSerial mới thành Sold
-> Ledger OUT
```

Nếu bỏ StockIn, serial mới tự nhiên xuất hiện. Nếu bỏ StockOut, serial mới vẫn nằm trong kho dù đã trả khách. Vì vậy cần cả hai.

## 9. Đổi serial trực tiếp từ kho

Method:

```csharp
ReplaceSerial(int claimId, string replacementSerial, string conclusion, int userId)
```

Trường hợp này không chờ hãng gửi serial mới. Kho đã có sẵn serial thay thế.

Luồng:

```text
1. Tìm claim + serial lỗi + product + coverage
2. Lấy kho mặc định
3. Tìm replacementSerial trong kho, trạng thái InStock
4. Kiểm tra serial thay thế thuộc cùng product
5. Đổi serial lỗi thành Replaced
6. Tạo StockOut bảo hành
7. Gọi InventoryPostingService.PostStockOut
8. Tắt coverage cũ
9. Tạo coverage mới cho serial thay thế
10. Đóng claim
11. Commit transaction
```

Khác với nhận từ hãng:

| Luồng | Có StockIn? | Có StockOut? | Vì sao |
|---|---|---|---|
| Hãng gửi serial mới | Có | Có | Serial mới trước đó chưa có trong kho |
| Đổi trực tiếp từ kho | Không | Có | Serial thay thế đã có sẵn trong kho |

## 10. Cập nhật coverage khi đổi serial

Khi đổi serial, code tắt coverage cũ:

```csharp
oldCoverage.CoverageStatus = "Inactive";
```

Sau đó tính số ngày còn lại:

```csharp
var remainingDays = (oldCoverage.WarrantyEndDate - DateTime.Now).TotalDays;
```

Nếu còn hạn, tạo coverage mới cho serial thay thế:

```csharp
var newCoverage = new WarrantyCoverage
{
    ProductSerialId = newSerial.Id,
    CustomerId = oldCoverage.CustomerId,
    SalesInvoiceId = oldCoverage.SalesInvoiceId,
    WarrantyStartDate = DateTime.Now,
    WarrantyEndDate = DateTime.Now.AddDays(remainingDays),
    CoverageStatus = "Active"
};
```

Dễ hiểu: khách không được bảo hành lại từ đầu, mà được chuyển phần thời hạn còn lại sang serial mới.

## 11. Hàm `UpdateSerialStatusOnClaimClosure`

Đây là hàm nhỏ nhưng quan trọng.

Nó trả lời câu hỏi: sau khi một claim đóng, serial nên ở trạng thái nào?

Luồng:

```text
1. Tìm serial
2. Kiểm tra còn claim mở khác của serial này không
3. Nếu không còn claim mở:
   - Nếu serial đang InWarrantyProcess hoặc ReturnedToManufacturer -> chuyển về Sold
4. Nếu còn claim mở:
   - Nếu có claim đang ManufacturerWait -> ReturnedToManufacturer
   - Ngược lại -> InWarrantyProcess
```

Vì sao cần? Vì một serial có thể có nhiều hồ sơ bảo hành chưa đóng. Đóng một hồ sơ chưa chắc serial đã xong toàn bộ.

## 12. Xóa claim

Method:

```csharp
DeleteClaim(int claimId)
```

Nó không cho xóa nếu đã phát sinh chứng từ kho:

```csharp
bool hasRelatedStockIn = db.StockIns.Any(si => si.Notes != null && si.Notes.Contains(claim.ClaimCode));
bool hasRelatedStockOut = db.StockOuts.Any(so => so.Notes != null && so.Notes.Contains(claim.ClaimCode)) || claim.ReplacementStockOutId.HasValue;
```

Dễ hiểu: nếu claim đã tạo phiếu nhập/xuất bảo hành, xóa claim sẽ làm mất liên kết lịch sử. Vì vậy hệ thống chặn.

## 13. Những câu hỏi dễ gặp

### Vì sao bảo hành bám theo serial?

Vì mỗi sản phẩm vật lý có serial riêng. Hai laptop cùng mã sản phẩm nhưng ngày bán, khách hàng, thời hạn bảo hành có thể khác nhau.

### Vì sao đổi serial phải tạo coverage mới?

Vì serial cũ không còn là sản phẩm khách đang giữ. Quyền bảo hành phải chuyển sang serial mới.

### Vì sao replacement từ hãng cần nhập kho rồi xuất kho?

Để sổ kho phản ánh đúng: hàng mới đi vào kho, sau đó đi ra cho khách.

### Vì sao dùng transaction ở các method đổi serial?

Vì một lần đổi serial đụng nhiều bảng: `ProductSerial`, `StockIn`, `StockOut`, `StockBalance`, `StockLedger`, `WarrantyCoverage`, `WarrantyClaim`. Chỉ một bước lỗi cũng phải rollback toàn bộ.

## 14. Cách đọc code theo thứ tự

1. `CreateClaim`: tạo hồ sơ bảo hành từ serial.
2. `SendToManufacturer`: chuyển sang chờ hãng.
3. `ReceiveFromManufacturerRepaired`: hãng sửa xong, trả serial cũ.
4. `ReceiveFromManufacturerReplaced`: hãng đổi serial mới.
5. `ReplaceSerial`: đổi ngay bằng serial có sẵn trong kho.
6. `UpdateSerialStatusOnClaimClosure`: hiểu cách serial quay về trạng thái đúng.
