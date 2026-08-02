# Eliminate N+1 Queries Design

## Goal

Loại bỏ các truy vấn tăng tuyến tính theo số dòng hoặc số serial trong nhập kho, xuất kho, điều chuyển, đảo chứng từ và Dynamic Import mà không đổi public API, schema hay quy tắc nghiệp vụ.

## Current Problem

Các màn hình đọc dữ liệu đã dùng `Include`, projection và truy vấn theo lô. Tuy nhiên một số luồng ghi dữ liệu vẫn gọi `Find`, `FirstOrDefault`, `SingleOrDefault` hoặc truy vấn serial/số dư bên trong vòng lặp. Số câu SQL vì vậy tăng theo số dòng, nhóm tồn hoặc serial nhập vào.

## Chosen Approach

Áp dụng batch-loading ngay trong từng service hiện có:

1. Thu thập tập ID, mã hoặc serial khác nhau trước vòng lặp.
2. Truy vấn một lần cho từng loại thực thể bằng `Contains`.
3. Chuyển kết quả thành `Dictionary`, `Lookup` hoặc `HashSet`.
4. Trong vòng lặp chỉ đọc cấu trúc bộ nhớ.
5. Khi import tự tạo danh mục, cập nhật map tại chỗ sau khi có ID để các dòng sau không truy vấn lại.

Không tạo `BatchDataLoader`, repository mới, raw SQL hoặc dependency mới.

## Components

### Stock posting

- `StockInService`: tải sản phẩm và serial đã tồn tại theo lô; nhóm serial cũ theo dòng nhập.
- `StockOutService`: tải sản phẩm, serial hiện tại và serial truy vết cũ theo lô.
- `StockTransferService`: tải toàn bộ sản phẩm của phiếu một lần.

Validation số lượng, trạng thái serial, kho, sản phẩm và transaction giữ nguyên.

### Stock reversal

`StockReversalService` tải tất cả `StockBalance` cần đảo bằng tập `ProductId` và `WarehouseId`, sau đó tra theo khóa `(ProductId, WarehouseId)` trong dictionary. Không thay đổi quy tắc chống âm tồn.

### Dynamic Import

Mỗi loại import tạo các map cần thiết từ dữ liệu đầu vào: danh mục, hãng, đơn vị, sản phẩm, đối tác, kho, serial và số dư. Các truy vấn kiểm tra replay vẫn thực hiện theo chứng từ vì số lượng truy vấn phụ thuộc số chứng từ, nhưng truy vấn tham chiếu theo từng dòng hoặc từng serial phải được gom theo lô.

Import vẫn chạy trong transaction hiện tại, giữ idempotency, atomicity, kiểm tra quyền và thông báo lỗi theo dòng.

## Query-count Tests

Dùng `DbCommandInterceptor` trong test để đếm câu `SELECT`. Với cùng một nghiệp vụ, chạy dữ liệu một dòng và nhiều dòng; số truy vấn ở bản nhiều dòng không được tăng tuyến tính theo số dòng/serial.

Test tập trung vào:

- ghi sổ nhập kho;
- ghi sổ xuất kho;
- ghi sổ điều chuyển;
- đảo chứng từ nhiều sản phẩm;
- import danh mục/sản phẩm/chứng từ nhiều dòng và nhiều serial.

Các test nghiệp vụ hiện có vẫn phải vượt qua.

## Success Criteria

- Không còn truy vấn EF đọc dữ liệu tham chiếu bên trong các vòng lặp thuộc phạm vi trên.
- Query-count test chứng minh số `SELECT` bị chặn theo số loại dữ liệu hoặc chứng từ, không theo số dòng/serial.
- Không đổi kết quả nghiệp vụ, transaction, public API, schema hoặc dependency.
- Focused tests, full test suite và solution build đều thành công.

## Non-goals

- Không tối ưu các truy vấn đã có số lượng cố định như `ReportTraceService`.
- Không thay eager loading bằng raw SQL.
- Không thay đổi UI, database schema hoặc chính sách nghiệp vụ.
