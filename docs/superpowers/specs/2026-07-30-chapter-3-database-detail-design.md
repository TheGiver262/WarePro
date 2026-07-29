# Thiết kế mở rộng phần cơ sở dữ liệu trong Chương 3

## Mục tiêu

Mở rộng mục 3.3 của báo cáo đồ án để người đọc thấy rõ cấu trúc dữ liệu, vai trò
của từng nhóm thực thể và quan hệ giữa các thực thể. Chương 3 phải đủ chi tiết để
giải thích thiết kế cơ sở dữ liệu mà không biến thành bản chép toàn bộ từ điển cột.

Thiết kế đã được thống nhất theo phương án:

- trình bày chi tiết ngay trong Chương 3;
- giữ một ERD tổng thể và sáu ERD theo phân hệ;
- diễn giải kỹ các quan hệ và ràng buộc quan trọng;
- không lặp lại nguyên khối sáu ERD ở phụ lục.

## Tệp nguồn và đầu ra

- Nguồn: `C:\Users\player\Desktop\DATN\final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh.docx`.
- Giữ nguyên tệp nguồn.
- Tạo bản mới:
  `C:\Users\player\Desktop\DATN\final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh_CHI_TIET_CSDL_20260730.docx`.
- Chưa cập nhật PDF cho tới khi bản DOCX mới được kiểm tra và người dùng duyệt.
- Không thay đổi mã nguồn, schema chạy thật hoặc dữ liệu ứng dụng trong nhiệm vụ
  chỉnh báo cáo này.

## Nguồn chuẩn của thiết kế dữ liệu

Thứ tự ưu tiên khi xác định bảng, khóa và quan hệ:

1. `QuanLyHangHoa/Data/AppDbContext.cs` và các model đang được ứng dụng sử dụng.
2. Các migration/script nâng cấp trong `Database/Schema/`.
3. `Database/database_schema.sql`.
4. ERD và nội dung đang có trong báo cáo.

Nếu các nguồn khác nhau, báo cáo phải theo mapping và ràng buộc hiện hành trong
ứng dụng; không giữ một quan hệ cũ chỉ vì nó đã xuất hiện trong ERD trước đó.

## Cấu trúc mới của mục 3.3

### 3.3.1 Nguyên tắc thiết kế

Giữ nội dung hiện có và bổ sung ngắn gọn:

- tách dữ liệu danh mục, chứng từ và dữ liệu phát sinh;
- mô hình đầu chứng từ–dòng chi tiết;
- chuẩn hóa quan hệ và sử dụng bảng liên kết cho quan hệ nhiều–nhiều;
- phân biệt `StockBalance` là số dư hiện tại và `StockLedger` là lịch sử biến động;
- dùng khóa ngoại, khóa duy nhất, ràng buộc kiểm tra và giao dịch để bảo vệ dữ liệu;
- dùng khóa ngoại có thể rỗng cho liên kết chỉ phát sinh ở một trạng thái nghiệp vụ.

### 3.3.2 Mô hình dữ liệu tổng thể

Giữ hoặc dựng lại một ERD tổng thể ở mức bảng, không liệt kê toàn bộ cột. Sơ đồ
phải thể hiện sáu vùng nghiệp vụ và các cầu nối chính:

- `Product` nối danh mục với mọi nghiệp vụ kho, hóa đơn và số sê-ri;
- `Warehouse` nối số dư, chứng từ kho, kiểm kê, điều chỉnh và điều chuyển;
- `ProductSerial` nối nhập–xuất kho với bán hàng và bảo hành;
- `StockIn`/`StockOut` nối nghiệp vụ kho với hóa đơn;
- `SalesInvoice` nối khách hàng và bán hàng với `WarrantyCoverage`;
- `AppUser` nối người tạo, duyệt, ghi sổ, xử lý và nhật ký thao tác.

Phần văn bản sau sơ đồ giải thích ranh giới sáu phân hệ và lý do các cầu nối trên
được xem là quan hệ xuyên phân hệ.

### 3.3.3 Danh mục và đối tác

ERD và nội dung tập trung vào:

- `Category` 1–N `Product`;
- `Brand` 1–N `Product`;
- `Unit` 1–N `Product` qua đơn vị mặc định;
- `Product` N–N `Unit` qua `ProductUnit`, kèm hệ số quy đổi;
- `Supplier` 1–N `StockIn` và 1–N `PurchaseInvoice`;
- `Customer` 1–N `StockOut`, 1–N `SalesInvoice` và 1–N `WarrantyCoverage`;
- `Warehouse` là điểm chứa hàng và là đầu mối của các chứng từ kho.

Diễn giải rõ `ProductUnit` là thực thể kết hợp, không mô tả trực tiếp quan hệ
N–N nếu thiếu bảng này.

### 3.3.4 Nhập, xuất và tồn kho

ERD và nội dung tập trung vào:

- `StockIn` 1–N `StockInLine`;
- `StockOut` 1–N `StockOutLine`;
- mỗi dòng chứng từ tham chiếu một `Product` và một `Unit`;
- `Warehouse` 1–N các chứng từ nhập/xuất;
- cặp `Warehouse`–`Product` được tổng hợp tại `StockBalance`;
- mỗi biến động đã ghi sổ tạo các dòng `StockLedger`;
- các liên kết người tạo, người duyệt và người ghi sổ từ chứng từ tới `AppUser`.

Phần luồng dữ liệu minh họa trình bày từ chứng từ nháp tới khi ghi sổ, cập nhật
`StockBalance`, thêm `StockLedger` và cập nhật số sê-ri trong cùng giao dịch.

### 3.3.5 Điều chuyển, kiểm kê và số sê-ri

ERD và nội dung tập trung vào:

- `StockTransfer` có một kho nguồn, một kho đích và nhiều `StockTransferLine`;
- mỗi dòng điều chuyển tham chiếu sản phẩm, đơn vị và các số sê-ri liên quan;
- `StockCountSession` 1–N `StockCountLine`;
- chênh lệch kiểm kê dẫn tới chứng từ nhập/xuất hoặc điều chỉnh theo mapping hiện hành;
- `StockAdjustment` 1–N `StockAdjustmentLine`;
- `Product` 1–N `ProductSerial`;
- `ProductSerial` bắt buộc tham chiếu dòng nhập gần nhất; kho hiện tại, dòng xuất
  gần nhất và dòng điều chuyển là các liên kết tùy trạng thái;
- `StockLedger` có thể tham chiếu `ProductSerial` để truy vết thiết bị cụ thể.

Không trình bày điều chuyển kho như “hướng phát triển” nếu model và mapping hiện
hành xác nhận chức năng đã được triển khai.

### 3.3.6 Hóa đơn mua và hóa đơn bán

ERD và nội dung tập trung vào:

- `PurchaseInvoice` 1–N `PurchaseInvoiceLine`;
- `SalesInvoice` 1–N `SalesInvoiceLine`;
- dòng hóa đơn tham chiếu `Product` và `Unit`;
- hóa đơn mua tham chiếu `Supplier`, hóa đơn bán tham chiếu `Customer`;
- `PurchaseInvoice` liên kết `StockIn`, từng dòng có thể liên kết `StockInLine`;
- `SalesInvoice` liên kết `StockOut`, từng dòng có thể liên kết `StockOutLine`;
- các khóa duy nhất bảo vệ quan hệ hóa đơn–chứng từ kho theo schema hiện hành.

Phần giải thích phải phân biệt chứng từ kho với hóa đơn: chứng từ kho quyết định
biến động vật lý; hóa đơn thể hiện giao dịch thương mại.

### 3.3.7 Bảo hành

ERD và nội dung tập trung vào:

- `ProductSerial` 1–N `WarrantyCoverage` theo mapping hiện hành;
- `Customer` 1–N `WarrantyCoverage`;
- `SalesInvoice` 1–N `WarrantyCoverage`;
- `WarrantyCoverage` 1–N `WarrantyClaim`;
- `WarrantyClaim` tham chiếu số sê-ri được bảo hành;
- hồ sơ đổi mới có thể tham chiếu số sê-ri thay thế và `StockOut` thay thế;
- người duyệt và người xử lý hồ sơ liên kết tới `AppUser`;
- khóa ngoại ghép bảo đảm hồ sơ yêu cầu bảo hành dùng đúng quyền bảo hành của số
  sê-ri tương ứng.

Luồng minh họa đi từ bán một số sê-ri, tạo quyền bảo hành, tiếp nhận yêu cầu,
xử lý và có thể đổi sang số sê-ri khác.

### 3.3.8 Người dùng, phân quyền và nhật ký

ERD và nội dung tập trung vào:

- `AppUser` tự tham chiếu qua người tạo tài khoản;
- các chứng từ nghiệp vụ tham chiếu `AppUser` theo vai trò tạo, duyệt và ghi sổ;
- `WarrantyClaim` tham chiếu người duyệt và người xử lý;
- `AuditLog` tham chiếu người thực hiện nhưng cho phép rỗng khi tài khoản bị xóa
  hoặc sự kiện không có người dùng đăng nhập;
- nhật ký được ghi trong cùng giao dịch với thay đổi nghiệp vụ quan trọng.

Phân quyền theo vai trò được giải thích ở mức thiết kế nghiệp vụ; không vẽ thành
bảng quan hệ nếu schema hiện hành không có bảng vai trò/quyền riêng.

### 3.3.9 Ràng buộc toàn vẹn, chỉ mục và giao dịch

Tổng hợp các cơ chế xuyên phân hệ:

- khóa chính và khóa ngoại;
- khóa duy nhất cho mã nghiệp vụ, số sê-ri và liên kết hóa đơn–chứng từ kho;
- ràng buộc kiểm tra số lượng, hệ số quy đổi và trạng thái;
- xóa hạn chế, xóa mềm hoặc `SET NULL` theo từng quan hệ;
- chỉ mục cho truy vấn theo sản phẩm, kho, chứng từ, ngày và số sê-ri;
- giao dịch nguyên tử khi ghi sổ;
- cơ chế xử lý cập nhật đồng thời và bế tắc ở lớp ghi dữ liệu.

Không đưa toàn bộ danh sách chỉ mục vào ERD. Chỉ nêu các chỉ mục/ràng buộc ảnh
hưởng trực tiếp tới tính đúng đắn hoặc luồng nghiệp vụ.

### 3.3.10 Kết luận thiết kế cơ sở dữ liệu

Tóm tắt ba đặc điểm:

1. dữ liệu hiện tại, lịch sử và đối tượng có số sê-ri được tách nhưng liên kết;
2. quan hệ xuyên phân hệ cho phép truy vết từ nhập kho tới bán hàng và bảo hành;
3. ràng buộc CSDL phối hợp với lớp dịch vụ để bảo vệ tính nhất quán.

## Khuôn trình bày cho mỗi phân hệ

Mỗi mục từ 3.3.3 tới 3.3.8 dùng cùng một khuôn:

1. đoạn giới thiệu mục đích phân hệ;
2. một ERD;
3. bảng quan hệ ngắn gồm thực thể nguồn, quan hệ, thực thể đích và ý nghĩa;
4. phần diễn giải các quan hệ/ràng buộc quan trọng;
5. một luồng dữ liệu minh họa;
6. đoạn kết nối sang phân hệ kế tiếp.

Bảng quan hệ chỉ chứa các quan hệ giúp người đọc hiểu thiết kế; không liệt kê lại
mọi cột đã có trong ERD.

## Quy tắc ERD và bố cục

- Dùng ký pháp chân quạ và ghi rõ `1`, `0..1`, `1..N`, `0..N`.
- Hiển thị tên bảng, PK, FK và các cột nghiệp vụ cần thiết để hiểu quan hệ.
- Không hiển thị danh sách chỉ mục trong từng bảng.
- Đường nối không đi xuyên bảng, không chồng nhãn và hạn chế giao nhau.
- Mỗi ERD phân hệ dùng một trang ngang khi cần; áp dụng lề xoay tương ứng:
  trên 3,5 cm, dưới 2,5 cm, trái 2 cm, phải 2 cm.
- Chú thích hình nằm cùng trang với hình khi có thể.
- Đánh lại số hình, mục, bảng, mục lục và danh mục hình bằng field của Word.
- Sáu ERD được chuyển từ Phụ lục B vào Chương 3; loại bỏ bản trùng ở phụ lục và
  cập nhật các tham chiếu liên quan.

## Phạm vi thay đổi

Được thay đổi:

- mục 3.3 và các tiêu đề con;
- sáu ERD đang nằm ở Phụ lục B;
- đoạn dẫn, bảng quan hệ và luồng minh họa mới;
- số thứ tự các mục tiếp theo trong Chương 3;
- mục lục, danh mục hình, chú thích và tham chiếu chéo bị ảnh hưởng.

Không được thay đổi:

- nội dung học thuật ngoài các đoạn cần nối lại sau khi đánh số;
- thông tin cá nhân;
- mã nguồn, schema và dữ liệu thật;
- các chương khác ngoài cập nhật số/tham chiếu bắt buộc;
- PDF hiện tại trước khi DOCX mới được duyệt.

## Kiểm tra hoàn thành

### Kiểm tra nội dung

- Mọi bảng và quan hệ trong ERD tồn tại trong model/mapping/schema hiện hành.
- Cardinality và tính tùy chọn khớp nullability cùng cấu hình EF Core.
- Sáu phân hệ có đủ ERD, bảng quan hệ, diễn giải và luồng minh họa.
- Các quan hệ xuyên phân hệ được mô tả nhất quán ở cả ERD tổng thể và ERD con.
- Không còn mô tả chức năng đã triển khai là “hướng phát triển”.
- Không còn sáu ERD trùng ở Phụ lục B.

### Kiểm tra cấu trúc DOCX

- Tệp DOCX mở được và ZIP integrity hợp lệ.
- Các tiêu đề dùng đúng Heading, số mục liên tục và mục lục nhận đủ mục mới.
- Chú thích hình, danh mục hình và tham chiếu chéo không bị mất hoặc trỏ sai.
- Các section ngang có kích thước và lề đúng.
- Nội dung ngoài phạm vi không thay đổi khi so sánh trước–sau.

### Kiểm tra trực quan

- Render toàn bộ DOCX sau chỉnh sửa.
- Kiểm tra từng trang mới và các trang chuyển section ở mức 100%.
- Không có chữ/hình bị cắt, chồng lấn, quá nhỏ hoặc đường nối ERD khó đọc.
- Kiểm tra lại bằng Word Print Preview trước khi coi bản DOCX hoàn chỉnh.
