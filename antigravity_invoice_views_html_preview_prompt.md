# Antigravity Prompt — Tạo HTML Preview cho WarePro Invoice Views

Tạo **1 file HTML duy nhất** để preview giao diện 2 màn hình hóa đơn của phần mềm desktop nội bộ **WarePro — Quản lý hàng hóa & Bảo hành**.

Output mong muốn:

```txt
warepro_invoice_views_preview.html
```

Không dùng React, không dùng CDN, không dùng ảnh ngoài. Chỉ dùng:

```txt
HTML + CSS + JavaScript thuần
```

Giao diện là preview tĩnh nhưng phải có JS nhỏ để chuyển tab và mở/đóng dialog.

---

## 1. Tech stack mục tiêu của phần mềm thật

Thiết kế HTML preview phải mô phỏng giao diện sẽ triển khai bằng:

```txt
WPF
C#
MVVM
Material Design in XAML
SQL Server / PostgreSQL nội bộ
```

Vì vậy style phải giống app desktop quản trị nội bộ, không phải web landing page.

---

## 2. Phong cách UI

Thiết kế theo phong cách:

```txt
Modern desktop admin dashboard
Dark purple sidebar
Light gray workspace
White cards
Compact DataGrid
Material Design inspired
Data-heavy enterprise CRUD UI
```

Ưu tiên:

```txt
Gọn
Rõ dữ liệu
Dễ thao tác
Ít khoảng trắng thừa
Phù hợp phần mềm quản lý kho / hóa đơn / bảo hành
```

Không thiết kế kiểu màu mè, landing page, mobile-first hay SaaS quá nhiều whitespace.

---

## 3. Màu sắc bắt buộc

Không dùng tên màu như `Slate`, `BlueGrey`, `bluegrey`, `slate`.

Chỉ dùng HEX:

```css
:root {
  --primary: #7C3AED;
  --primary-hover: #6D28D9;

  --sidebar-bg: #211733;
  --sidebar-hover: #34264D;
  --sidebar-active: #7C3AED;
  --sidebar-text: #D8D0E6;
  --sidebar-muted: #8B7A9F;

  --page-bg: #F7F7FA;
  --surface: #FFFFFF;
  --surface-muted: #F3F1F7;
  --border: #DED9E8;

  --text-primary: #2A2533;
  --text-secondary: #756B82;
  --text-muted: #8A8095;

  --success-bg: #DCFCE7;
  --success-text: #16A34A;

  --info-bg: #DBEAFE;
  --info-text: #2563EB;

  --warning-bg: #FEF3C7;
  --warning-text: #D97706;

  --danger-bg: #FEE2E2;
  --danger-text: #EF4444;

  --neutral-bg: #E5E7EB;
  --neutral-text: #4B5563;
}
```

---

## 4. Layout tổng thể

Kích thước preview hướng tới:

```txt
1440 x 900
```

Bố cục:

```txt
┌──────────────────────────────────────────────────────────────┐
│ Topbar                                                       │
├───────────────┬──────────────────────────────────────────────┤
│ Sidebar 240px │ Page Content                                 │
│               │                                              │
│ Navigation    │ Header + Filter Card + Invoice DataGrid       │
└───────────────┴──────────────────────────────────────────────┘
```

CSS layout đề xuất:

```css
.app {
  height: 100vh;
  display: grid;
  grid-template-columns: 240px 1fr;
  grid-template-rows: 56px 1fr;
}
```

---

## 5. Sidebar

Sidebar width:

```txt
240px
```

Background:

```txt
#211733
```

Menu đầy đủ:

```txt
WarePro

Dashboard

DANH MỤC
- Loại hàng
- Thương hiệu
- Đơn vị tính
- Nhà cung cấp
- Khách hàng

SẢN PHẨM
- Danh sách sản phẩm
- Quản lý Serial

KHO
- Nhập kho
- Xuất kho
- Tồn kho
- Sổ kho
- Kiểm kê

HÓA ĐƠN
- Hóa đơn mua
- Hóa đơn bán

BẢO HÀNH
- Quyền bảo hành
- Yêu cầu bảo hành
- Sắp hết hạn BH

HỆ THỐNG
- Báo cáo
- Người dùng

Đăng xuất
```

Active item ban đầu:

```txt
Hóa đơn mua
```

Khi chuyển sang tab hóa đơn bán thì active item đổi sang:

```txt
Hóa đơn bán
```

Style menu:

```css
.nav-item {
  height: 36px;
  border-radius: 6px;
  padding: 0 10px;
  font-size: 13px;
  color: #D8D0E6;
}

.nav-item:hover {
  background: #34264D;
  color: white;
}

.nav-item.active {
  background: #7C3AED;
  color: white;
  font-weight: 700;
}
```

---

## 6. Topbar

Topbar:

```txt
Height: 56px
Background: white
Border-bottom: #DED9E8
```

Nội dung:

```txt
Phần mềm Quản lý Hàng hóa & Bảo hành                 Admin · Quản trị viên
```

---

## 7. Views cần preview

File HTML phải có 2 view:

```txt
PurchaseInvoiceView — Hóa đơn mua
SalesInvoiceView    — Hóa đơn bán
```

Có tab / button để chuyển qua lại:

```txt
[Hóa đơn mua] [Hóa đơn bán]
```

JavaScript cần có:

```js
switchView("purchase")
switchView("sales")
```

---

## 8. Header màn hình

### 8.1 Hóa đơn mua

Title:

```txt
Hóa đơn mua
```

Subtitle:

```txt
Quản lý hóa đơn từ nhà cung cấp, thuế, tổng tiền và trạng thái thanh toán
```

Actions bên phải:

```txt
[Tạo từ phiếu nhập] [Xuất Excel] [+ Tạo hóa đơn mua]
```

### 8.2 Hóa đơn bán

Title:

```txt
Hóa đơn bán
```

Subtitle:

```txt
Quản lý hóa đơn bán cho khách hàng, thuế, tổng tiền và trạng thái thanh toán
```

Actions bên phải:

```txt
[Tạo từ phiếu xuất] [Xuất Excel] [+ Tạo hóa đơn bán]
```

---

## 9. Ý nghĩa các nút tạo hóa đơn

### 9.1 `+ Tạo hóa đơn mua/bán`

Đây là tạo hóa đơn thủ công.

Bắt buộc dialog phải có phần:

```txt
Chi tiết sản phẩm
[+ Thêm dòng]
Sản phẩm | Đơn vị | Số lượng | Đơn giá | Thuế % | Thành tiền | Xóa
```

Người dùng phải thêm được nhiều dòng sản phẩm bằng nút:

```txt
+ Thêm dòng
```

Nút này thêm một row mới bằng JavaScript.

### 9.2 `Tạo từ phiếu nhập/xuất`

Đây là tạo nhanh từ chứng từ kho đã ghi sổ:

```txt
Tạo từ phiếu nhập → dùng cho hóa đơn mua
Tạo từ phiếu xuất → dùng cho hóa đơn bán
```

Ý nghĩa:

```txt
Tự lấy nhà cung cấp/khách hàng
Tự lấy sản phẩm
Tự lấy số lượng
Tự lấy đơn giá từ dòng phiếu nhập/xuất
Sau đó người dùng bổ sung thuế, hạn thanh toán, trạng thái thanh toán
```

Trong preview có thể để nút này là nút tĩnh, không cần xử lý thật.

---

## 10. Filter card

Filter nằm trong card trắng:

```txt
Background: white
Border: #DED9E8
Radius: 6px
Padding: 16px
```

### 10.1 Filter Hóa đơn mua

Fields:

```txt
SỐ HÓA ĐƠN      TextBox: Nhập số HĐ...
NHÀ CUNG CẤP    ComboBox: Tất cả nhà cung cấp
TỪ NGÀY         Date input
ĐẾN NGÀY        Date input
TRẠNG THÁI TT   ComboBox: Tất cả
PHIẾU NHẬP      TextBox: Mã phiếu nhập...
```

### 10.2 Filter Hóa đơn bán

Fields:

```txt
SỐ HÓA ĐƠN      TextBox: Nhập số HĐ...
KHÁCH HÀNG      ComboBox: Tất cả khách hàng
TỪ NGÀY         Date input
ĐẾN NGÀY        Date input
TRẠNG THÁI TT   ComboBox: Tất cả
PHIẾU XUẤT      TextBox: Mã phiếu xuất...
```

Input style:

```css
input,
select,
textarea {
  height: 34px;
  border: 1px solid #DED9E8;
  border-radius: 5px;
  padding: 0 10px;
  font-size: 13px;
  background: white;
  color: #2A2533;
}
```

Label style:

```css
.field label {
  display: block;
  font-size: 11px;
  font-weight: 800;
  color: #756B82;
  text-transform: uppercase;
  margin-bottom: 6px;
}
```

---

## 11. DataGrid Hóa đơn mua

Columns:

```txt
Số HĐ
Ngày HĐ
Nhà cung cấp
Phiếu nhập
Trước thuế
Thuế
Tổng tiền
Đã TT
Trạng thái TT
Hạn TT
Thao tác
```

Mock data:

```txt
HDM-0001 | 03/05/2026 | Công ty TNHH Minh Phát | PN-0001 | 18.000.000 | 1.800.000 | 19.800.000 | 19.800.000 | Đã TT | 10/05/2026
HDM-0002 | 05/05/2026 | FPT Trading | PN-0002 | 32.500.000 | 3.250.000 | 35.750.000 | 15.000.000 | TT một phần | 20/05/2026
HDM-0003 | 08/05/2026 | Digiworld | PN-0003 | 12.000.000 | 1.200.000 | 13.200.000 | 0 | Chưa TT | 18/05/2026
HDM-0004 | 10/05/2026 | Synnex FPT | — | 8.500.000 | 850.000 | 9.350.000 | 0 | Quá hạn | 12/05/2026
```

---

## 12. DataGrid Hóa đơn bán

Columns:

```txt
Số HĐ
Ngày HĐ
Khách hàng
Phiếu xuất
Trước thuế
Thuế
Tổng tiền
Đã TT
Trạng thái TT
Hạn TT
Thao tác
```

Mock data:

```txt
HDB-0001 | 04/05/2026 | Nguyễn Văn An | PX-0001 | 25.000.000 | 2.500.000 | 27.500.000 | 27.500.000 | Đã TT | 04/05/2026
HDB-0002 | 06/05/2026 | Công ty ABC | PX-0002 | 42.000.000 | 4.200.000 | 46.200.000 | 20.000.000 | TT một phần | 16/05/2026
HDB-0003 | 09/05/2026 | Trần Minh Khang | PX-0003 | 7.800.000 | 780.000 | 8.580.000 | 0 | Chưa TT | 19/05/2026
HDB-0004 | 11/05/2026 | Lê Thu Hà | — | 3.200.000 | 320.000 | 3.520.000 | 3.520.000 | Đã TT | 11/05/2026
```

---

## 13. Table style

```css
.table-card {
  background: #fff;
  border: 1px solid #DED9E8;
  border-radius: 6px;
  overflow: hidden;
}

table {
  width: 100%;
  border-collapse: collapse;
  min-width: 1160px;
}

thead {
  background: #F3F1F7;
}

th {
  height: 38px;
  padding: 0 14px;
  font-size: 12px;
  font-weight: 800;
  color: #756B82;
  text-align: left;
  white-space: nowrap;
}

td {
  height: 46px;
  padding: 0 14px;
  font-size: 13px;
  color: #2A2533;
  border-top: 1px solid #E5E0EE;
  white-space: nowrap;
}

tr:hover {
  background: #FAF8FE;
}

.money {
  text-align: right;
  font-variant-numeric: tabular-nums;
}
```

---

## 14. Badge trạng thái thanh toán

Mapping:

```txt
Đã TT       → success
TT một phần → warning
Chưa TT     → danger
Quá hạn     → danger
```

CSS:

```css
.badge {
  display: inline-flex;
  align-items: center;
  height: 22px;
  padding: 0 8px;
  border-radius: 5px;
  font-size: 12px;
  font-weight: 800;
}

.badge-success {
  background: #DCFCE7;
  color: #16A34A;
}

.badge-warning {
  background: #FEF3C7;
  color: #D97706;
}

.badge-danger {
  background: #FEE2E2;
  color: #EF4444;
}
```

---

## 15. Action icons

Cột thao tác có các nút nhỏ 28x28:

```txt
👁 Xem
✎ Sửa
🖨 In
⊘ Hủy
```

Không dùng nút `Xóa` chính cho hóa đơn.

Lý do:

```txt
Hóa đơn đã phát sinh nghiệp vụ thì nên Hủy/Void, không xóa cứng.
```

CSS:

```css
.icon-btn {
  width: 28px;
  height: 28px;
  border-radius: 5px;
  border: 0;
  background: transparent;
  cursor: pointer;
}

.icon-btn:hover {
  background: #F4F1F8;
}

.icon-btn.danger {
  color: #EF4444;
}
```

---

## 16. Dialog tạo hóa đơn

Khi bấm:

```txt
+ Tạo hóa đơn mua
+ Tạo hóa đơn bán
```

Mở modal/dialog preview.

Dialog size:

```txt
Width: 760px
Radius: 8px
Background: white
Shadow rõ
```

### 16.1 Header dialog

Nếu đang ở Hóa đơn mua:

```txt
Tạo hóa đơn mua
```

Nếu đang ở Hóa đơn bán:

```txt
Tạo hóa đơn bán
```

### 16.2 Fields chung

```txt
Số hóa đơn *
Ngày hóa đơn *
Nhà cung cấp * / Khách hàng *
Phiếu nhập liên kết / Phiếu xuất liên kết
Hạn thanh toán
Trạng thái thanh toán
Ghi chú
```

### 16.3 Chi tiết sản phẩm — bắt buộc phải có

Phần này bắt buộc có trong dialog:

```txt
Chi tiết sản phẩm                         [+ Thêm dòng]
```

Bảng dòng sản phẩm:

```txt
Sản phẩm | Đơn vị | Số lượng | Đơn giá | Thuế % | Thành tiền | Xóa
```

Mock rows ban đầu:

```txt
Laptop Dell XPS 13 | Chiếc | 1 | 25.000.000 | 10% | 27.500.000 | ×
Chuột Logitech M331 | Cái | 2 | 350.000 | 10% | 770.000 | ×
```

### 16.4 Nút `+ Thêm dòng`

Bắt buộc có.

Vị trí:

```txt
Cùng hàng với title "Chi tiết sản phẩm", căn phải
```

Khi click, JS thêm một row mới vào tbody.

Row mới nên có input/select:

```html
<select>
  <option>Chọn sản phẩm...</option>
  <option>Laptop Dell XPS 13</option>
  <option>Chuột Logitech M331</option>
  <option>Tai nghe Sony WH-1000XM5</option>
</select>

<select>
  <option>Chiếc</option>
  <option>Cái</option>
  <option>Bộ</option>
</select>

<input type="number" value="1" min="1" />
<input type="text" value="0" />

<select>
  <option>0%</option>
  <option selected>10%</option>
</select>
```

JS bắt buộc có hàm:

```js
function addInvoiceLine() {
  const tbody = document.getElementById("invoiceLineBody");
  const row = document.createElement("tr");
  row.innerHTML = `...`;
  tbody.appendChild(row);
}
```

`tbody` phải có:

```html
<tbody id="invoiceLineBody">
```

Nút xóa dòng:

```html
<button onclick="this.closest('tr').remove()">×</button>
```

### 16.5 Tổng tiền

Cuối dialog, căn phải:

```txt
Tạm tính:        25.700.000 đ
Thuế:             2.570.000 đ
Tổng thanh toán: 28.270.000 đ
Đã thanh toán:   0 đ
Còn lại:         28.270.000 đ
```

### 16.6 Footer dialog

```txt
[Hủy] [Lưu hóa đơn]
```

---

## 17. Quy tắc nghiệp vụ cần thể hiện

Trong preview, thêm note nhỏ cuối trang:

```txt
Ghi chú: Hóa đơn không làm thay đổi tồn kho. Tồn kho được cập nhật từ phiếu nhập/xuất đã ghi sổ.
```

Nghiệp vụ đúng:

```txt
Phiếu nhập / Phiếu xuất làm thay đổi tồn kho.
Hóa đơn mua / Hóa đơn bán ghi nhận giao dịch thương mại, tiền, thuế, công nợ.
Hóa đơn có thể liên kết với phiếu nhập/xuất đã Posted.
Hóa đơn không tự làm tăng/giảm tồn kho.
```

---

## 18. JavaScript bắt buộc

Phải có các hàm:

```js
function switchView(type) {
  // type = "purchase" hoặc "sales"
  // đổi view, đổi tab active, đổi sidebar active
}

function openModal(type) {
  // type = "purchase" hoặc "sales"
  // đổi title dialog, label nhà cung cấp/khách hàng,
  // label phiếu nhập/phiếu xuất,
  // dữ liệu select tương ứng
}

function closeModal() {
  // đóng dialog
}

function addInvoiceLine() {
  // thêm dòng sản phẩm trong dialog
}
```

---

## 19. Yêu cầu output

Chỉ tạo **một file HTML hoàn chỉnh**.

File phải có:

```txt
<!DOCTYPE html>
<html lang="vi">
<head>
  <style>...</style>
</head>
<body>
  ...
  <script>...</script>
</body>
</html>
```

Không giải thích dài. Không tạo nhiều file. Không dùng CDN. Không dùng ảnh ngoài.
