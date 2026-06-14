# Động cơ Xử lý Kho & Giao dịch (Inventory Core Engine)

Trái tim của hệ thống quản lý kho nằm trong thư mục **`QuanLyHangHoa/Inventory/`**. Lớp hạ tầng này chịu trách nhiệm trực tiếp trong việc thực thi các quy tắc nghiệp vụ kho, tính toán số lượng tồn kho khả dụng và đảm bảo không xảy ra hiện tượng tranh chấp dữ liệu (deadlock) khi nhiều nhân viên cùng thực hiện ghi sổ.

Tài liệu này giải thích chi tiết động cơ cốt lõi thông qua hai lớp chính: `InventoryPostingService` và `EfInventoryUnitOfWork`.

---

## 1. Các tập tin liên quan
* [InventoryPostingService.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Inventory/InventoryPostingService.cs): Dịch vụ chính chịu trách nhiệm tính toán và thực thi lệnh ghi sổ nhập/xuất kho.
* [EfInventoryUnitOfWork.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Inventory/EfInventoryUnitOfWork.cs): Lớp quản lý giao dịch và truy xuất dữ liệu độc lập dành riêng cho phân hệ kho (áp dụng mẫu thiết kế Unit of Work).
* **Inventory Models & Commands:** Định nghĩa các snapshot dữ liệu và tham số lệnh (`PostStockInCommand`, `PostStockOutCommand`).

---

## 2. Các Thuật toán Cốt lõi của Phân hệ Kho

### A. Thuật toán Sắp xếp ProductId để phòng chống Deadlock
Trong một hệ thống quản lý kho có nhiều người dùng ghi sổ đồng thời, deadlock (khóa chết) rất dễ xảy ra nếu hai giao dịch khóa các dòng sản phẩm theo thứ tự ngược nhau.
* *Ví dụ:* 
  * Giao dịch A muốn nhập sản phẩm X rồi sản phẩm Y. Nó sẽ khóa X $\rightarrow$ chờ khóa Y.
  * Giao dịch B muốn nhập sản phẩm Y rồi sản phẩm X. Nó sẽ khóa Y $\rightarrow$ chờ khóa X.
  * Hậu quả: Cả 2 giao dịch khóa lẫn nhau và SQL Server buộc phải kill 1 trong 2 giao dịch.
* **Giải pháp áp dụng:** Trước khi thực hiện bất kỳ lệnh ghi sổ nào, các dòng sản phẩm bắt buộc phải được sắp xếp theo thứ tự `ProductId` tăng dần:
  ```csharp
  // Thuật toán sắp xếp khóa tài nguyên cố định trong StockIn/StockOut Service
  var sortedLines = lines.OrderBy(l => l.ProductId).ToList();
  ```
  *Ý nghĩa:* Bằng việc luôn luôn khóa tài nguyên theo thứ tự tăng dần của ID khóa chính, hệ thống đảm bảo mọi giao dịch song song đều yêu cầu tài nguyên theo cùng một chiều X $\rightarrow$ Y. Do đó, giao dịch sau sẽ xếp hàng chờ giao dịch trước giải phóng thay vì tạo thành vòng lặp khóa chết (deadlock loop).

### B. Thuật toán Kiểm soát Tồn kho Khả dụng (OnHand vs Available)
Hệ thống quản lý chặt chẽ số lượng tồn kho thực tế và tồn kho có sẵn để bán thông qua hai tham số:
* `OnHandQuantity` (Tồn thực tế): Số lượng vật lý thực sự nằm trong kho.
* `AvailableQuantity` (Tồn khả dụng): Số lượng thực tế trừ đi số lượng đã bị đặt trước hoặc bị giữ lại cho các đơn hàng chưa xuất.
* **Thuật toán cộng tồn khi Nhập kho (`PostStockIn`):**
  ```csharp
  var balance = _unitOfWork.GetOrCreateBalance(command.ProductId, warehouseId);
  _unitOfWork.SaveBalance(balance with
  {
      OnHandQuantity = balance.OnHandQuantity + (int)command.Quantity,
      AvailableQuantity = balance.AvailableQuantity + (int)command.Quantity
  });
  ```
* **Thuật toán trừ tồn khi Xuất kho (`PostStockOut`):**
  ```csharp
  var balance = _unitOfWork.GetOrCreateBalance(command.ProductId, warehouseId);
  if (balance.AvailableQuantity < command.Quantity)
  {
      throw new InventoryDomainException("Insufficient available stock.");
  }
  _unitOfWork.SaveBalance(balance with
  {
      OnHandQuantity = balance.OnHandQuantity - (int)command.Quantity,
      AvailableQuantity = balance.AvailableQuantity - (int)command.Quantity
  });
  ```
  *Ý nghĩa:* Hàm kiểm tra `AvailableQuantity < command.Quantity` ngăn chặn tuyệt đối tình trạng xuất âm kho (xuất khống số lượng hàng hóa).

### C. Cơ chế Kiểm tra & Đồng bộ trạng thái số Serial
Hệ thống áp dụng các kiểm tra nghiêm ngặt đối với sản phẩm quản lý bằng số Serial:
* **Khi Nhập kho (`PostStockIn`):**
  Hệ thống kiểm tra từng số Serial xem đã tồn tại trong DB chưa:
  ```csharp
  foreach (var serialNumber in serialNumbers)
  {
      if (_unitOfWork.SerialExists(serialNumber))
      {
          throw new InventoryDomainException($"Serial {serialNumber} already exists.");
      }
  }
  ```
  Nếu hợp lệ, tạo mới bản ghi `ProductSerial` ở trạng thái `InStock` (Trong kho) và gán `CurrentWarehouseId`.
* **Khi Xuất kho (`PostStockOut`):**
  Hệ thống kiểm tra xem các số Serial được chọn có thực sự tồn tại, đang ở trạng thái `InStock` và nằm đúng kho xuất hay không:
  ```csharp
  var serial = _unitOfWork.GetSerial(serialNumber);
  if (serial.Status != SerialStatus.InStock || serial.CurrentWarehouseId != warehouseId)
  {
      throw new InventoryDomainException($"Serial {serialNumber} is not available in warehouse {warehouseId}.");
  }
  ```
  Nếu hợp lệ, cập nhật trạng thái Serial sang `Sold` (Đã bán) và xóa `CurrentWarehouseId = null` (vì hàng đã rời kho).

---

## 3. Cơ chế Ghi sổ Thẻ kho (`StockLedger`) & Nhật ký Kiểm toán (`AuditLog`)

Một giao dịch ghi sổ kho chỉ được coi là hoàn tất khi ghi nhận đầy đủ thẻ kho và nhật ký để phục vụ báo cáo kế toán:
* **Thẻ kho (`StockLedger`):**
  ```csharp
  _unitOfWork.AddLedger(new StockLedgerEntry(
      command.DocumentId,
      command.ProductId,
      warehouseId,
      StockLedgerDirection.In, // Hoặc Out
      (int)command.Quantity,
      _clock.Now,
      command.PostedByUserId));
  ```
  *Ý nghĩa:* Lưu lại nhật ký biến động kho chi tiết để phục vụ báo cáo Thẻ kho lũy kế (cho biết tại ngày X kho đã biến động bao nhiêu và tồn cộng dồn là bao nhiêu).
* **Nhật ký kiểm toán (`AuditLog`):**
  ```csharp
  _unitOfWork.AddAudit(new AuditLogEntry(
      command.DocumentId,
      AuditActionCode.PostStockIn, // Hoặc PostStockOut
      _clock.Now,
      command.PostedByUserId));
  ```
  *Ý nghĩa:* Ghi nhận vết hệ thống để phục vụ công tác thanh tra dữ liệu (Ai đã ghi sổ phiếu nào, vào thời điểm nào).

---

## 4. Mẫu thiết kế Unit of Work (`EfInventoryUnitOfWork`)

Lớp `EfInventoryUnitOfWork` đóng vai trò bao bọc DbContext và cung cấp giao diện tương tác thuần túy dạng Snapshot.
* **Mục đích:** Cách ly hoàn toàn logic xử lý kho khỏi cấu trúc bảng cơ sở dữ liệu của EF Core. `InventoryPostingService` chỉ làm việc với các Snapshot bất biến (ví dụ: `ProductSerialSnapshot`, `StockBalanceSnapshot`) thông qua Record của C#.
* **Cơ chế Lưu thay đổi (Commit):**
  Mọi thay đổi dữ liệu (thêm thẻ kho, sửa tồn kho, sửa Serial) chỉ được lưu tạm thời vào bộ nhớ theo dõi của DbContext. Khi toàn bộ các bước xử lý không xảy ra lỗi, dịch vụ gọi phương thức:
  ```csharp
  _unitOfWork.Commit();
  ```
  Lúc này, EF Core mới kích hoạt lệnh `DbContext.SaveChanges()` để đẩy toàn bộ các thay đổi xuống database SQL Server trong một phiên làm việc duy nhất, đảm bảo tính nguyên tử cao.
