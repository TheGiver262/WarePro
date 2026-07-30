# Thiết kế vá tại chỗ sáu ERD phân hệ cũ

Ngày: 2026-07-30

## Mục tiêu

Sửa sáu trang `PL.D.1`–`PL.D.6` trong:

`F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\Diagram\Drawio\DO_AN_TAT_CA_SO_DO_2026-07-27.drawio`

Giữ nguyên phong cách, bảng và bố cục tương đối của hình cũ. Không thay sáu trang bằng ERD đã dựng lại trong `WarePro_ERD_Tong_20260730.drawio`; không xuất PNG và không sửa DOCX.

## Nguồn chuẩn

- Quan hệ và chiều PK–FK: `QuanLyHangHoa/Data/AppDbContext.cs`.
- Cột vật lý, PK/FK và kiểu SQL: `.tmp/current-schema.json`.
- Danh sách quan hệ đầy đủ: `docs/superpowers/specs/2026-07-30-erd-detail-correction-design.md`, mục 4.1–4.6.

## Cách sửa

- Tạo backup trước khi ghi.
- Chỉ sửa sáu khối `<diagram>` có tên bắt đầu bằng `PL.D.`; 17 trang còn lại phải giữ nguyên từng byte.
- Giữ nguyên mọi bảng và tọa độ hiện hữu nếu không gây cản trở đường mới.
- Chỉ thêm hộp tham chiếu ngoài phân hệ khi trang chưa có đầu quan hệ cần thiết; dùng đúng kiểu hộp tham chiếu hiện tại.
- Thêm hoặc sửa connector trực tiếp; không sinh lại toàn bộ `mxGraphModel` của trang.
- Mỗi connector ghi metadata `data-principal`, `data-dependent`, `data-foreign-keys` và nhãn `BangCha.PK → BangCon.FK` để kiểm chứng tự động.
- Đường vuông góc, tách lane; không xuyên bảng, đè chữ hoặc dùng chung toàn bộ tuyến.
- FK kép bảo hành dùng một connector và ghi đủ hai cột.
- `WareProClientSession` là bảng độc lập; chỉ bổ sung bảng này vào `PL.D.6`, không tạo cạnh giả.

## Phạm vi sửa theo kết quả kiểm tra

- `PL.D.1`: bổ sung 29 quan hệ liên phân hệ còn thiếu.
- `PL.D.2`: bổ sung 7 quan hệ còn thiếu.
- `PL.D.3`: bổ sung 8 quan hệ còn thiếu.
- `PL.D.4`: bổ sung `SalesInvoice.Id → WarrantyCoverage.SalesInvoiceId`.
- `PL.D.5`: giữ đủ 9 quan hệ; sửa nhãn FK kép nếu chưa rõ.
- `PL.D.6`: bổ sung 20 quan hệ nghiệp vụ của `AppUser` và bảng độc lập `WareProClientSession`.

Không thêm `StockLedger.SourceDocumentId` như FK và không tạo quan hệ trực tiếp giả giữa `Product` với `Supplier`, `Customer` hoặc `Warehouse`.

## Nghiệm thu

- File XML hợp lệ, vẫn đủ 23 trang và đúng thứ tự.
- 17 trang không phải `PL.D.*` giữ nguyên từng byte.
- Sáu trang có đủ lần lượt `34, 33, 34, 15, 9, 23` quan hệ chuẩn theo bảng lõi từng phân hệ; các quan hệ ngữ cảnh hiện hữu không được tính thay cho quan hệ còn thiếu.
- Không có quan hệ giả, trùng khóa hoặc thiếu đầu nối.
- Không có connector xuyên qua bảng; kiểm tra trực quan lại cả sáu trang bằng Draw.io.
- Không thay đổi PNG, DOCX hay sáu file Draw.io riêng lẻ.
