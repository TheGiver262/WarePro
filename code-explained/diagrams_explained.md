# Giải thích chi tiết Hệ thống Sơ đồ (Diagrams Explanation)

Hệ thống quản lý hàng hóa và bảo hành **QuanLyHangHoa** (WareHousePro) được thiết kế và mô hình hóa thông qua bộ sơ đồ chuẩn UML (triển khai bằng Mermaid và PlantUML). Các sơ đồ này mô tả chi tiết từ kiến trúc phân lớp, các ca sử dụng (Use Cases), quy trình nghiệp vụ (Activity), tương tác giữa các thành phần (Sequence), cho đến cấu trúc dữ liệu thực thể (ERD) và vòng đời của các đối tượng cốt lõi (State Machine).

Tài liệu này giải thích chi tiết ý nghĩa, luồng xử lý và các quyết định thiết kế then chốt đằng sau mỗi sơ đồ.

---

## 1. Sơ đồ Kiến trúc Hệ thống (WPF / MVVM / SQL Server)

Sơ đồ kiến trúc thể hiện cách tổ chức mã nguồn thành 4 phân lớp chức năng độc lập (Layered Architecture) và luồng giao tiếp một chiều từ trên xuống:

* **Presentation Layer (Lớp hiển thị):** Gồm WPF XAML Views và ViewModels. View chỉ nhận tương tác từ người dùng và liên kết dữ liệu (Data Binding) với ViewModel. ViewModel hoàn toàn không biết về các phần tử UI cụ thể, giúp tách biệt giao diện và dễ dàng viết Unit Test.
* **Application Layer (Lớp dịch vụ ứng dụng):** Chứa các Business Services (như `AuthService`, `InventoryService`, `WarrantyService`). Lớp này điều phối các quy tắc nghiệp vụ (Business Rules) và làm cầu nối giữa giao diện và cơ sở dữ liệu.
* **Domain Layer (Lớp nghiệp vụ lõi):** Định nghĩa các thực thể dữ liệu (Entities) và các quy tắc nghiệp vụ bất biến (Business Rules) của hệ thống.
* **Infrastructure Layer (Lớp hạ tầng):** Quản lý Repositories, DbContext, Transactions, Audit Logs và tương tác trực tiếp với cơ sở dữ liệu SQL Server.

**Quyết định thiết kế then chốt:** Luồng giao tiếp là một chiều và đi qua lớp dịch vụ trung gian. ViewModel không bao giờ gọi trực tiếp DbContext hay Repository mà bắt buộc phải đi qua Service thích hợp. Điều này đảm bảo toàn bộ quy tắc kiểm tra nghiệp vụ và ghi nhật ký kiểm toán (Audit Log) luôn được thực thi một cách thống nhất.

---

## 2. Các Sơ đồ Ca sử dụng (Use Case Diagrams)

Hệ thống phân chia quyền hạn chặt chẽ dựa trên 5 Actor chính. Sơ đồ áp dụng cơ chế **Kế thừa quyền hạn** (Role Generalization) để giảm thiểu trùng lặp thiết kế:
* **Quản trị viên (Admin):** Kế thừa toàn bộ quyền của Quản lý, đồng thời có quyền quản trị tài khoản, gán `RoleCode` và xem nhật ký hệ thống (`Audit Log`).
* **Quản lý (Manager):** Kế thừa quyền của tất cả nhân viên (Kho, Bán hàng, Bảo hành) và có quyền duyệt các báo cáo thống kê doanh thu, lợi nhuận hoặc import tồn kho đầu kỳ.
* **Nhân viên kho (Storekeeper):** Thực hiện các nghiệp vụ nhập kho, xuất kho, kiểm kê, điều chuyển kho và tra cứu thẻ kho.
* **Nhân viên bán hàng (Salesman):** Thực hiện lập hóa đơn bán lẻ và xuất kho tương ứng cho khách hàng.
* **Nhân viên bảo hành (Technician):** Thực hiện tiếp nhận sản phẩm bảo hành, cập nhật trạng thái xử lý lỗi, gửi hãng và thực hiện đổi trả máy mới.

### Chi tiết các phân hệ Use Case:
1. **Phân hệ Quản trị & Danh mục:** Quản lý tài khoản người dùng, gán phân quyền động, cấu hình danh mục nền (Sản phẩm, Thương hiệu, Đơn vị tính, Đối tác, Kho hàng) và theo dõi dấu vết sửa đổi thông qua `Audit Log`.
2. **Phân hệ Kho hàng:** Xử lý các luồng nhập/xuất kho vật lý, chuyển kho nội bộ giữa các chi nhánh, phiên kiểm kê đối soát chênh lệch và tra cứu lịch sử thẻ kho của từng sản phẩm.
3. **Phân hệ Thương mại:** Lập hóa đơn mua hàng từ nhà cung cấp (`PurchaseInvoice`) và hóa đơn bán hàng cho khách hàng (`SalesInvoice`), tự động tính toán thuế, thành tiền và cập nhật trạng thái công nợ.
4. **Phân hệ Bảo hành:** Tiếp nhận yêu cầu bảo hành từ khách hàng, quản lý thời hạn bảo hành của từng số Serial thiết bị, theo dõi quá trình sửa chữa hoặc đổi mới sản phẩm.

---

## 3. Các Sơ đồ Luồng hoạt động (Activity Diagrams)

Sơ đồ Activity mô tả quy trình thực thi các nghiệp vụ từ lúc bắt đầu cho tới khi hoàn thành, phân tích các rẽ nhánh logic và cách xử lý lỗi.

### AC-01: Ghi sổ phiếu nhập kho (StockIn Posting)
Quy trình ghi sổ nhập kho đảm bảo dữ liệu đầu vào hoàn toàn sạch trước khi ghi nhận tồn kho:
* **Kiểm tra trạng thái:** Phiếu phải đang ở trạng thái Nháp (`Draft`). Nếu đã ghi sổ (`Posted`), hệ thống từ chối xử lý ngay lập tức.
* **Khóa chống Deadlock:** Sắp xếp danh sách sản phẩm theo ID để khóa tài nguyên tuần tự.
* **Xác thực Serial:** Đối với sản phẩm quản lý bằng số Serial, hệ thống kiểm tra danh sách số Serial đầu vào:
  * Không được phép trùng lặp trong chính phiếu nhập.
  * Không được phép tồn tại trong cơ sở dữ liệu từ trước.
* **Ghi sổ đồng thời:** Trong cùng một Database Transaction, hệ thống thực hiện đồng thời:
  * Tăng tồn kho thực tế (`OnHandQuantity`) và tồn khả dụng (`AvailableQuantity`) trong bảng `StockBalance`.
  * Tạo mới các bản ghi số Serial với trạng thái `InStock` và gán kho hiện tại.
  * Ghi nhận Thẻ kho (`StockLedger`) để làm căn cứ tính tồn lũy kế.
  * Ghi nhật ký kiểm toán (`AuditLog`) ghi nhận tài khoản thực hiện.
  * Chuyển trạng thái phiếu nhập sang `Posted` và commit transaction.

### AC-02: Ghi sổ phiếu xuất kho (StockOut Posting)
Quy trình xuất kho chú trọng việc kiểm tra tính khả dụng của hàng hóa:
* **Kiểm tra tồn khả dụng:** Hệ thống kiểm tra số lượng tồn khả dụng (`AvailableQuantity`) tại kho xuất phải lớn hơn hoặc bằng số lượng yêu cầu xuất. Nếu không đủ, giao dịch bị từ chối (tránh xuất khống hàng hóa).
* **Kiểm tra trạng thái Serial:** Các số Serial được chọn phải tồn tại, đang ở trạng thái `InStock` và nằm đúng tại kho thực hiện xuất.
* **Ghi sổ xuất:** Trừ tồn kho thực tế và khả dụng, chuyển trạng thái Serial sang `Sold` (Đã bán), xóa định vị kho hiện tại của Serial, ghi Thẻ kho, Nhật ký kiểm toán và chuyển phiếu xuất sang `Posted`.

### AC-03: Điều chuyển kho nội bộ (Stock Transfer)
Quy trình di chuyển hàng hóa giữa các địa điểm:
* Yêu cầu kho đi và kho đến phải khác nhau.
* Thực hiện đồng thời lệnh xuất kho tại kho nguồn và lệnh nhập kho tại kho đích trong một transaction duy nhất.
* Cập nhật lại kho quản lý của các số Serial chuyển đi sang kho đích.

### AC-04: Đổi mới bảo hành (Warranty Replacement)
Mô tả quy trình xử lý đổi mới thiết bị lỗi cho khách hàng (trực tiếp từ kho hoặc nhận từ hãng sản xuất):
* **Đóng bảo hành cũ:** Tìm quyền bảo hành (`WarrantyCoverage`) đang hoạt động của thiết bị lỗi cũ và cập nhật trạng thái thành `Inactive` để vô hiệu hóa thiết bị lỗi.
* **Tính thời hạn kế thừa:** Tính toán số ngày bảo hành còn lại của thiết bị cũ:
  $$\text{RemainingDays} = \text{WarrantyEndDate}_{\text{Old}} - \text{DateTime.Now}$$
* **Cấp bảo hành mới:** Nếu thiết bị cũ vẫn còn hạn bảo hành, hệ thống tự động tạo quyền bảo hành mới cho số Serial thay thế mới. Ngày hết hạn mới được kế thừa chính xác từ số ngày còn lại của máy cũ:
  $$\text{WarrantyEndDate}_{\text{New}} = \text{DateTime.Now} + \text{RemainingDays}$$
* **Xuất kho đổi mới:** Thực hiện xuất kho sản phẩm thay thế ra khỏi kho hàng của công ty, cập nhật trạng thái Serial mới sang `Sold` và gán thông tin khách hàng sở hữu.

### AC-05: Kiểm kê và cân đối kho (Stock Count & Adjustment)
Quy trình kiểm tra chênh lệch thực tế:
* Tạo phiên kiểm kê, hệ thống tự động lưu số lượng tồn sổ sách hiện thời làm mốc đối chiếu.
* Sau khi nhập số đếm thực tế, hệ thống tính toán chênh lệch (`VarianceQuantity = CountedQuantity - SystemQuantity`).
* Khi duyệt kiểm kê, hệ thống tự động sinh chứng từ điều chỉnh kho (`StockAdjustment`). Phiếu này sẽ phát lệnh nhập điều chỉnh cho chênh lệch dương (thừa hàng) hoặc xuất điều chỉnh cho chênh lệch âm (thiếu hàng) để đồng bộ tồn kho sổ sách về đúng thực tế.

---

## 4. Các Sơ đồ Tuần tự (Sequence Diagrams)

Sơ đồ Sequence mô tả sự tương tác theo trình tự thời gian giữa các lớp đối tượng, từ giao diện người dùng đến cơ sở dữ liệu.

### Seq 13: Ghi sổ phiếu nhập kho (StockIn Posting Flow)
1. **Thủ kho** click nút "Duyệt phiếu" trên giao diện `StockInView`.
2. `StockInView` truyền yêu cầu đến `StockInViewModel` thông qua `PostCommand`.
3. `StockInViewModel` gọi phương thức `Post(stockInId, userId)` của lớp `StockInService`.
4. `StockInService` mở kết nối và khởi tạo một Database Transaction thông qua `AppDbContext`.
5. `StockInService` thực hiện kiểm tra tính hợp lệ của số Serial đầu vào và tính toán số lượng quy đổi đơn vị.
6. `StockInService` khởi tạo lớp `InventoryPostingService` và chuyển giao quyền xử lý ghi sổ kho bằng cách gọi phương thức `PostStockIn()`.
7. `InventoryPostingService` tương tác với `EfInventoryUnitOfWork` để nạp tồn kho hiện thời (`GetOrCreateBalance`), thực hiện cộng tồn kho, lưu lại tồn mới (`SaveBalance`), và lưu số Serial mới vào database.
8. `InventoryPostingService` ghi nhận Thẻ kho và Nhật ký hệ thống thông qua Unit of Work.
9. Sau khi tất cả các bước thành công, `StockInService` gọi `transaction.Commit()` để lưu vĩnh viễn dữ liệu xuống SQL Server.
10. Hệ thống phản hồi trạng thái thành công về giao diện và cập nhật trạng thái hiển thị của phiếu trên UI sang màu xanh (Đã ghi sổ).

### Seq 15: Quy trình bảo hành và đổi mới (Warranty Claim & Replacement Flow)
Sơ đồ tuần tự này mô tả sự phối hợp liên phân hệ giữa phân hệ bảo hành và phân hệ kho:
1. **Nhân viên bảo hành** yêu cầu đổi mới thiết bị lỗi trên `WarrantyClaimView`.
2. `WarrantyClaimViewModel` kích hoạt lệnh xử lý và gọi phương thức `ReplaceSerial()` trong `WarrantyClaimService`.
3. `WarrantyClaimService` khởi tạo transaction và tìm hồ sơ bảo hành (`WarrantyClaim`) kèm theo quyền bảo hành (`WarrantyCoverage`) hiện có.
4. Dịch vụ thực hiện vô hiệu hóa quyền bảo hành cũ (`CoverageStatus = "Inactive"`) và tính toán số ngày còn lại.
5. `WarrantyClaimService` gọi `StockOutService` để tự động lập và ghi sổ phiếu xuất kho đổi mới bảo hành (`PurposeCode = "WarrantyReplacement"`).
6. Phiếu xuất kho này tự động trừ tồn kho thiết bị thay thế và chuyển trạng thái Serial thay thế sang `Sold`.
7. Sau khi xuất kho thành công, `WarrantyClaimService` tạo quyền bảo hành mới cho Serial thay thế với thời hạn kế thừa số ngày còn lại.
8. Trạng thái của claim bảo hành được cập nhật thành `Closed` (Đã xử lý xong) và commit transaction để lưu lại toàn bộ chuỗi thay đổi.

---

## 5. Các Sơ đồ Trạng thái (State Machine Diagrams)

Sơ đồ State Machine định nghĩa tất cả các trạng thái có thể có của một thực thể và các sự kiện kích hoạt chuyển dịch trạng thái (State Transitions).

### State 16: Vòng đời của chứng từ kho (Document Lifecycle)
Để đảm bảo quy trình tinh gọn cho doanh nghiệp vừa và nhỏ, vòng đời chứng từ được giản lược tối đa:
* `Draft` (Nháp): Trạng thái ban đầu của chứng từ khi mới tạo. Ở trạng thái này, người dùng có toàn quyền chỉnh sửa thông tin, thêm/xóa dòng sản phẩm, quét lại số serial.
* Sự kiện `Post` (Ghi sổ) được kích hoạt bởi thủ kho hoặc quản lý.
* `Posted` (Đã ghi sổ): Chứng từ chuyển sang trạng thái đã ghi sổ và bị đóng băng hoàn toàn. Người dùng không thể chỉnh sửa, xóa hay thay đổi bất kỳ thông tin nào để bảo vệ tính toàn vẹn số liệu kế toán.

### State 17: Vòng đời của thiết bị / Số Serial (Serial Number Lifecycle)
Mỗi số Serial đại diện cho một thiết bị vật lý cụ thể và có một vòng đời di chuyển liên tục:
```
+--------+   Nhập kho    +---------+     Bán hàng    +------+
| Unborn | -----------> | InStock | --------------> | Sold |
+--------+              +---------+                 +------+
                             ^                         |
                             |                         | Lỗi /
                             | Đổi mới                 | Tiếp nhận
                             |                         v
                        +---------+  Gửi hãng   +--------------------+
                        | InStock | <---------- | InWarrantyProcess  |
                        +---------+             +--------------------+
                                                       |
                                                       | Hỏng nặng
                                                       v
                                                 +-----------+
                                                 |  Scrapped | (Thanh lý)
                                                 +-----------+
```
* `InStock` (Trong kho): Thiết bị đang nằm tại một kho cụ thể, sẵn sàng bán hoặc điều chuyển.
* `Sold` (Đã bán): Thiết bị đã được xuất bán và bàn giao cho khách hàng (xóa thông tin kho hiện tại).
* `InWarrantyProcess` (Đang bảo hành): Thiết bị bị lỗi và được nhân viên bảo hành tiếp nhận xử lý.
* `ReturnedToManufacturer` (Đã gửi hãng): Thiết bị được chuyển về hãng sản xuất để bảo hành sửa chữa.
* `Scrapped` (Thanh lý/Hủy): Thiết bị hỏng nặng không thể sửa chữa và bị loại bỏ khỏi hệ thống.

### State 18: Vòng đời của quyền bảo hành (Warranty Coverage Lifecycle)
* `Active` (Hiệu lực): Quyền bảo hành đang hoạt động tốt và thời gian hiện tại nằm trong hạn bảo hành. Khách hàng có quyền yêu cầu sửa chữa miễn phí.
* `Inactive` (Vô hiệu/Hết hạn): Quyền bảo hành bị đóng do hết hạn thời gian hoặc do thiết bị cũ đã bị thu hồi/vô hiệu hóa trong quá trình đổi mới.

---

## 6. Sơ đồ Cấu trúc Thực thể (Entity Relationship Diagram - ERD)

Sơ đồ ERD mô tả cấu trúc bảng cơ sở dữ liệu và các mối quan hệ toàn vẹn dữ liệu (Foreign Keys).

### A. Phân hệ Danh mục & Quy đổi Đơn vị (Core & Catalog)
* `Product` (1) $\rightarrow$ (N) `ProductUnit`: Một sản phẩm có thể có nhiều đơn vị quy đổi (Cái, Hộp, Thùng).
* Bảng `ProductUnit` liên kết với `Unit` qua `UnitId` và chứa trường `ConversionFactor` (Hệ số quy đổi) để tính toán số lượng cơ sở khi làm việc với kho.

### B. Phân hệ Giao dịch Kho & Truy vết lịch sử
* `Warehouse` (1) $\rightarrow$ (N) `StockBalance`: Quản lý số lượng tồn kho khả dụng và tồn thực tế của từng sản phẩm tại mỗi kho hàng riêng biệt.
* Bảng `StockLedger` (Thẻ kho) là thực thể trung tâm ghi nhận mọi biến động kho. Mỗi khi có phiếu `StockIn`, `StockOut`, `StockTransfer` hoặc `StockAdjustment` được ghi sổ, một bản ghi tương ứng sẽ được chèn vào `StockLedger` để lưu trữ biến động tăng/giảm và làm căn cứ tính tồn lũy kế thời gian thực.

### C. Phân hệ Serial & Hóa đơn & Bảo hành
* `ProductSerial` (N) $\rightarrow$ (1) `Product`: Mỗi số Serial gắn chặt với một sản phẩm cụ thể.
* `ProductSerial` liên kết với dòng phiếu nhập cuối cùng (`LastStockInLineId`) và dòng phiếu xuất cuối cùng (`LastStockOutLineId`) để phục vụ việc truy vết vòng đời thiết bị.
* `SalesInvoice` (1) $\rightarrow$ (N) `WarrantyCoverage`: Khi hóa đơn bán hàng được lưu, hệ thống tự động sinh quyền bảo hành liên kết số Serial thiết bị với khách hàng sở hữu.
* `WarrantyClaim` (N) $\rightarrow$ (1) `WarrantyCoverage`: Mỗi hồ sơ khiếu nại bảo hành tham chiếu đến quyền bảo hành gốc để xác định tính hợp lệ của thiết bị. Đồng thời, claim liên kết với `ReplacementSerialId` (Serial thay thế mới) và `ReplacementStockOutId` (Phiếu xuất kho đổi mới) để hoàn thành quy trình đổi trả khép kín.
