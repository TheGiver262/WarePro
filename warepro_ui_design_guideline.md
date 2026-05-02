# WarePro UI Design Guideline

Tài liệu này dùng làm chỉ dẫn cho Google Antigravity hoặc AI coding agent khi thiết kế giao diện WPF/C# MVVM cho phần mềm **Quản lý hàng hóa & Bảo hành**. Mục tiêu là tái tạo phong cách giao diện giống các ảnh preview đã cung cấp: desktop admin dashboard, sidebar tím đậm, nội dung trắng/xám nhạt, bảng dữ liệu mật độ cao, thao tác CRUD rõ ràng.

---

## 1. Định hướng tổng thể

Thiết kế theo phong cách **modern internal business dashboard**: gọn, phẳng, ít bo góc, ưu tiên dữ liệu, dễ thao tác trong môi trường doanh nghiệp. Không làm kiểu landing page hoặc SaaS quá nhiều khoảng trắng.

Giao diện cần nghiêng về **data-heavy** hơn là decorative UI, vì người dùng cần quản lý nhiều danh mục, phiếu nhập/xuất, hóa đơn, serial và bảo hành.

### Từ khóa phong cách

- Modern WPF desktop dashboard
- Purple dark sidebar
- Light gray workspace
- White bordered cards
- Compact data table
- Enterprise CRUD UI
- Minimal icon + text navigation
- High information density
- Clean, functional, not flashy

---

## 2. Bố cục shell chính

Toàn bộ ứng dụng dùng một **Main Shell Window** cố định gồm 3 vùng chính:

```text
┌──────────────────────────────────────────────────────────────┐
│ Top Bar                                                      │
├───────────────┬──────────────────────────────────────────────┤
│ Left Sidebar  │ Page Content                                 │
│               │                                              │
│ Navigation    │ Dashboard / CRUD table / Report screens       │
│               │                                              │
└───────────────┴──────────────────────────────────────────────┘
```

### Kích thước đề xuất

| Thành phần | Kích thước |
|---|---:|
| Window min width | 1100px |
| Window default width | 1440px - 1600px |
| Window default height | 850px - 900px |
| Sidebar width | 220px - 240px |
| Top bar height | 52px - 56px |
| Page content padding | 24px |
| Main background | `#F7F6FA` hoặc `#F8F7FB` |

### Quy tắc bố cục

- Sidebar cố định bên trái, không bị mất khi chuyển màn hình.
- Top bar cố định phía trên vùng content.
- Content có scroll riêng theo chiều dọc.
- Sidebar cũng có scroll riêng nếu menu dài.
- Không dùng popup/menu phức tạp cho navigation chính.
- Mỗi màn hình có header gồm: title, subtitle, vùng action bên phải.

---

## 3. Bảng màu chuẩn

### 3.1 Màu chính

| Token | Hex | Dùng cho |
|---|---|---|
| `PrimaryPurple` | `#7C3AED` | Button chính, menu active |
| `PrimaryPurpleHover` | `#6D28D9` | Hover button chính |
| `SidebarBg` | `#1E293B` | Nền sidebar |
| `SidebarBgDarker` | `#1A102A` | Nền footer/logout sidebar |
| `SidebarHover` | `#2E2144` | Hover menu sidebar |
| `SidebarActive` | `#7C3AED` | Menu đang chọn |
| `PageBg` | `#F7F6FA` | Nền vùng content |
| `Surface` | `#FFFFFF` | Card, table container |
| `SurfaceMuted` | `#F3F1F7` | Table header |
| `Border` | `#DDD8E7` | Viền card/input/table |
| `TextPrimary` | `#26212E` | Text chính |
| `TextSecondary` | `#6F667C` | Subtitle, label phụ |
| `TextMuted` | `#8B819A` | Group heading sidebar |

### 3.2 Màu trạng thái

| Status | Background | Text | Dùng cho |
|---|---|---|---|
| Success | `#DCFCE7` | `#15803D` | Hoạt động, đã thanh toán, đã sửa |
| Info | `#DBEAFE` | `#2563EB` | Đã duyệt, đã bán |
| Warning | `#FEF3C7` | `#B45309` | Đang xử lý, TT một phần |
| Danger | `#FEE2E2` | `#DC2626` | Chưa TT, từ chối |
| Neutral | `#E5E7EB` | `#4B5563` | Nháp, ngừng HĐ, đã đóng |

### 3.3 Màu dashboard card icon

| Card | Màu icon |
|---|---|
| Sản phẩm | `#2F80ED` |
| Phiếu nhập | `#22C55E` |
| Phiếu xuất | `#F97316` |
| Bảo hành đang xử lý | `#A855F7` |
| Hóa đơn mua chưa thanh toán | `#EF4444` |
| Hóa đơn bán chưa thanh toán | `#EAB308` |

---

## 4. ResourceDictionary WPF đề xuất

Nếu dùng WPF + Material Design in XAML, không dùng `SecondaryColor="Slate"`. Material Design in XAML không có palette `Slate`. Muốn màu giống slate thì tự khai báo brush bằng hex.

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Core palette -->
    <SolidColorBrush x:Key="PrimaryPurpleBrush" Color="#7C3AED" />
    <SolidColorBrush x:Key="PrimaryPurpleHoverBrush" Color="#6D28D9" />

    <SolidColorBrush x:Key="SidebarBgBrush" Color="#1E293B" />
    <SolidColorBrush x:Key="SidebarBgDarkerBrush" Color="#1A102A" />
    <SolidColorBrush x:Key="SidebarHoverBrush" Color="#2E2144" />
    <SolidColorBrush x:Key="SidebarActiveBrush" Color="#7C3AED" />

    <SolidColorBrush x:Key="PageBgBrush" Color="#F7F6FA" />
    <SolidColorBrush x:Key="SurfaceBrush" Color="#FFFFFF" />
    <SolidColorBrush x:Key="SurfaceMutedBrush" Color="#F3F1F7" />
    <SolidColorBrush x:Key="BorderBrush" Color="#DDD8E7" />

    <SolidColorBrush x:Key="TextPrimaryBrush" Color="#26212E" />
    <SolidColorBrush x:Key="TextSecondaryBrush" Color="#6F667C" />
    <SolidColorBrush x:Key="TextMutedBrush" Color="#8B819A" />
    <SolidColorBrush x:Key="SidebarTextBrush" Color="#DCD5EA" />

    <!-- Status -->
    <SolidColorBrush x:Key="SuccessBgBrush" Color="#DCFCE7" />
    <SolidColorBrush x:Key="SuccessTextBrush" Color="#15803D" />
    <SolidColorBrush x:Key="InfoBgBrush" Color="#DBEAFE" />
    <SolidColorBrush x:Key="InfoTextBrush" Color="#2563EB" />
    <SolidColorBrush x:Key="WarningBgBrush" Color="#FEF3C7" />
    <SolidColorBrush x:Key="WarningTextBrush" Color="#B45309" />
    <SolidColorBrush x:Key="DangerBgBrush" Color="#FEE2E2" />
    <SolidColorBrush x:Key="DangerTextBrush" Color="#DC2626" />
    <SolidColorBrush x:Key="NeutralBgBrush" Color="#E5E7EB" />
    <SolidColorBrush x:Key="NeutralTextBrush" Color="#4B5563" />

</ResourceDictionary>
```

---

## 5. Typography

Dùng font mặc định của Windows để hợp WPF:

```text
FontFamily: Segoe UI
```

### Kích thước chữ

| Loại text | Size | Weight | Màu |
|---|---:|---:|---|
| App name trong sidebar | 14-15 | 700 | White |
| Page title | 20-22 | 700 | TextPrimary |
| Page subtitle | 13-14 | 400/500 | TextSecondary |
| Menu item | 14 | 500 | SidebarText |
| Group heading sidebar | 11-12 | 700 | TextMuted |
| Table header | 13 | 600 | TextSecondary |
| Table cell | 13-14 | 400/500 | TextPrimary |
| Input label | 12 | 500 | TextSecondary |
| Button text | 13 | 600 | White/TextPrimary |
| Dashboard stat number | 22-26 | 700 | TextPrimary |

### Quy tắc

- Không dùng font quá tròn hoặc decorative.
- Không dùng size chữ quá lớn trong bảng.
- Page title ngắn, rõ: `Sản phẩm`, `Nhập kho`, `Hóa đơn bán`.
- Subtitle mô tả một dòng: `Quản lý danh mục sản phẩm`, `Quản lý phiếu nhập kho`.

---

## 6. Sidebar navigation

Sidebar là điểm nhận diện chính của app. Dùng nền tím đen, active item màu tím sáng.

### 6.1 Cấu trúc sidebar

```text
Logo WarePro
────────────────
Dashboard

DANH MỤC
  Loại hàng
  Thương hiệu
  Đơn vị tính
  Nhà cung cấp
  Khách hàng

SẢN PHẨM
  Danh sách sản phẩm
  Quản lý Serial

KHO
  Nhập kho
  Xuất kho
  Tồn kho
  Sổ kho

HÓA ĐƠN
  Hóa đơn mua
  Hóa đơn bán

BẢO HÀNH
  Quyền bảo hành
  Yêu cầu bảo hành
  Sắp hết hạn BH

HỆ THỐNG
  Báo cáo
  Người dùng

Đăng xuất
```

### 6.2 Kích thước sidebar

| Thành phần | Giá trị |
|---|---:|
| Width | 220-240px |
| Logo row height | 56px |
| Menu item height | 36px |
| Menu item margin | `8,2` |
| Menu item border radius | 5-6px |
| Icon column | 26px |
| Group heading margin top | 8-12px |
| Footer logout height | 52-60px |

### 6.3 Style menu item

- Normal: transparent background, text `#DCD5EA`.
- Hover: background `#2E2144`.
- Active: background `#7C3AED`, text white.
- Group heading: uppercase, màu `#8B819A`, letter spacing nhẹ nếu làm được.
- Icon và text cùng màu trạng thái.

### 6.4 Icon Material Design đề xuất

Dùng `materialDesign:PackIcon` nếu project đã cài Material Design in XAML.

| Menu | PackIcon Kind |
|---|---|
| Dashboard | `ViewDashboardOutline` |
| Loại hàng | `TagOutline` |
| Thương hiệu | `Domain` hoặc `OfficeBuildingOutline` |
| Đơn vị tính | `CubeOutline` hoặc `RulerSquare` |
| Nhà cung cấp | `TruckOutline` |
| Khách hàng | `AccountGroupOutline` |
| Danh sách sản phẩm | `PackageVariantClosed` |
| Quản lý Serial | `BarcodeScan` |
| Nhập kho | `TrayArrowDown` |
| Xuất kho | `TrayArrowUp` |
| Tồn kho | `ChartBar` |
| Sổ kho | `ClipboardTextClockOutline` |
| Hóa đơn mua | `CartOutline` |
| Hóa đơn bán | `FileDocumentOutline` |
| Quyền bảo hành | `ShieldCheckOutline` |
| Yêu cầu bảo hành | `WrenchOutline` |
| Sắp hết hạn BH | `AlertTriangleOutline` |
| Báo cáo | `ChartBoxOutline` |
| Người dùng | `AccountMultipleOutline` |
| Đăng xuất | `LogoutVariant` |

---

## 7. Top bar

Top bar trong app chỉ nên là thanh tiêu đề nội bộ, không bao gồm browser chrome/toolbar của Base44 trong ảnh.

### Bố cục

```text
[× hoặc menu toggle]  Phần mềm Quản lý Hàng hóa & Bảo hành                 [optional user/avatar]
```

### Style

| Thuộc tính | Giá trị |
|---|---|
| Height | 52-56px |
| Background | White |
| Bottom border | `#E4DFEA`, 1px |
| Text color | `#7C728C` |
| Title font size | 14px |
| Title weight | 600 |

### Ghi chú

- Nếu window custom chrome, nút `×` trong ảnh có thể là nút đóng hoặc collapse sidebar. Cần thống nhất hành vi.
- Nếu ứng dụng production, nên có đủ Minimize/Maximize/Close ở góc phải hoặc giữ native window chrome.
- Không nên copy phần thanh trình duyệt ở trên ảnh.

---

## 8. Page header

Mỗi màn hình bắt đầu bằng header gồm title, subtitle và actions bên phải.

```text
Title
Subtitle                                         [Filter dropdown] [Button chính]
```

### Kích thước

| Thành phần | Giá trị |
|---|---:|
| Header margin bottom | 20-24px |
| Title font | 20-22px, bold |
| Subtitle font | 13-14px |
| Action button height | 32-36px |
| Dropdown height | 32-36px |

### Ví dụ

```text
Sản phẩm
Quản lý danh mục sản phẩm                         [Tất cả loại] [Tất cả] [Tất cả TT] [+ Thêm mới]
```

---

## 9. Dashboard screen

Dashboard gồm 2 phần: stat cards hàng đầu và biểu đồ/tổng quan bên dưới.

### 9.1 Stat cards

Trong ảnh, dashboard có 6 card ngang:

```text
[Sản phẩm 30] [Phiếu nhập 60] [Phiếu xuất 30] [BH đang xử lý 8] [HD mua chưa TT 15] [HD bán chưa TT 12]
```

#### Card style

| Thuộc tính | Giá trị |
|---|---:|
| Width | 180-220px, responsive wrap |
| Height | 80-112px |
| Background | White |
| Border | `#DDD8E7`, 1px |
| Radius | 5-6px |
| Padding | 16-20px |
| Icon box size | 42-48px |
| Icon radius | 8px |
| Gap icon-text | 12px |

#### Text

- Label: 14px, `TextSecondary`.
- Value: 24-26px, bold, `TextPrimary`.

### 9.2 Chart cards

Dashboard có các card biểu đồ:

- `Doanh thu & Chi phí nhập (6 tháng gần nhất)` - line/area chart.
- `Top 5 sản phẩm bán chạy nhất` - horizontal bar chart.
- `Tỷ lệ nhập - xuất kho` - donut chart.

#### Chart card style

| Thuộc tính | Giá trị |
|---|---:|
| Background | White |
| Border | `#DDD8E7`, 1px |
| Radius | 6px |
| Padding | 16px |
| Title size | 14-15px, semibold |
| Grid line color | `#E7E2EF` |

---

## 10. CRUD list screen pattern

Các màn hình danh mục, sản phẩm, phiếu, hóa đơn, bảo hành dùng chung pattern.

```text
Page Header
┌──────────────────────────────────────────────────────────────┐
│ Filter/Search bar                                             │
├──────────────────────────────────────────────────────────────┤
│ Table header                                                  │
├──────────────────────────────────────────────────────────────┤
│ Table rows                                                    │
├──────────────────────────────────────────────────────────────┤
│ Footer: 0 bản ghi / paging                                    │
└──────────────────────────────────────────────────────────────┘
```

### 10.1 Container

| Thuộc tính | Giá trị |
|---|---:|
| Background | White |
| Border | `#DDD8E7`, 1px |
| Radius | 5-6px |
| Margin top | 20-24px |
| Width | Stretch |

### 10.2 Search/filter bar

Trong ảnh, filter bar nằm trong card table, phía trên header bảng.

- Label nhỏ nằm trên input.
- Input height 32px.
- Các input xếp ngang.
- Button `Tìm kiếm` màu tím.
- Button `In` dạng outline/light.
- Date range dùng 2 DatePicker có dấu `→` ở giữa.

#### Kích thước

| Thành phần | Giá trị |
|---|---:|
| Filter bar padding | 14px 16px |
| Input width nhỏ | 160-190px |
| Input width trung bình | 190-230px |
| Button height | 32px |
| Gap | 8px |
| Label font | 12px |
| Input font | 13px |

#### TextBox style

| State | Style |
|---|---|
| Normal | White bg, border `#DDD8E7` |
| Focus | Border `#7C3AED` |
| Placeholder | `#9A90A8` |
| Radius | 4-5px |
| Padding | horizontal 10-12px |

### 10.3 Button style

#### Primary button

Dùng cho `Thêm mới`, `Tạo phiếu nhập`, `Tìm kiếm`.

```text
Background: #7C3AED
Hover: #6D28D9
Text: White
Height: 32-36px
Radius: 4-5px
Padding horizontal: 14-18px
Font: 13px semibold
Icon size: 14-16px
```

#### Secondary/outline button

Dùng cho `In`, `Nhập CSV`, `Xuất CSV`.

```text
Background: White
Border: #DDD8E7
Text: #4B4458
Hover background: #F4F1F8
Height: 32-36px
Radius: 4-5px
```

---

## 11. Table design

Bảng là thành phần quan trọng nhất. Cần giữ mật độ giống ảnh.

### 11.1 DataGrid visual

| Thuộc tính | Giá trị |
|---|---:|
| Header height | 36-38px |
| Row height | 42-46px |
| Header background | `#F3F1F7` |
| Row background | White |
| Row hover | `#FAF8FE` |
| Grid line | `#E5E0EE` |
| Cell padding horizontal | 14-16px |
| Border radius container | 5-6px |
| Font size | 13-14px |

### 11.2 Header

- Header text màu `#6F667C`.
- Font semibold.
- Có sort indicator nhỏ cạnh tên cột nếu làm được.
- Không dùng header màu đậm.

### 11.3 Row actions

Cột action nằm bên phải, icon nhỏ:

| Action | Icon | Màu |
|---|---|---|
| Xem | Eye | `#26212E` |
| Sửa | Pencil | `#26212E` hoặc `#4B4458` |
| Duyệt/Ghi sổ | CheckCircle | `#16A34A` |
| Xóa | TrashCanOutline | `#EF4444` |

Quy tắc:

- Icon button không cần background mặc định.
- Hover mới có nền rất nhạt `#F4F1F8`.
- Icon size 14-16px.
- Khoảng cách giữa icon 10-12px.

### 11.4 Empty state

Nếu không có dữ liệu:

```text
Không có dữ liệu
0 bản ghi
```

- Empty text căn giữa trong vùng table body.
- Màu `#6F667C`.
- Footer `0 bản ghi` căn trái, padding 14px.

---

## 12. Badge/status chip

Status chip xuất hiện trong nhiều bảng. Cần nhất quán.

### Style chung

```text
Height: 20-22px
Padding: 4px 8px
Radius: 4-5px
Font size: 12px
Font weight: 600
```

### Mapping trạng thái

| Trạng thái | Background | Text |
|---|---|---|
| Hoạt động | `#DCFCE7` | `#15803D` |
| Ngừng HĐ | `#E5E7EB` | `#4B5563` |
| Trong kho | `#DCFCE7` | `#15803D` |
| Đã bán | `#DBEAFE` | `#2563EB` |
| Nháp | `#E5E7EB` | `#4B5563` |
| Đã duyệt | `#DBEAFE` | `#2563EB` |
| Đã TT | `#DCFCE7` | `#15803D` |
| Chưa TT | `#FEE2E2` | `#DC2626` |
| TT một phần | `#FEF3C7` | `#B45309` |
| Tiếp nhận | `#DBEAFE` | `#2563EB` |
| Đang xử lý | `#FEF3C7` | `#B45309` |
| Đã sửa | `#DCFCE7` | `#15803D` |
| Từ chối | `#FEE2E2` | `#DC2626` |
| Đã đóng | `#E5E7EB` | `#4B5563` |

---

## 13. Màn hình theo module

### 13.1 Dashboard

Header:

```text
Dashboard
```

Cards:

- Sản phẩm
- Phiếu nhập
- Phiếu xuất
- BH đang xử lý
- HD mua chưa TT
- HD bán chưa TT

Charts:

- Doanh thu & Chi phí nhập.
- Top sản phẩm bán chạy.
- Tỷ lệ nhập - xuất kho.

### 13.2 Loại hàng

Title: `Loại hàng`
Subtitle: `Quản lý danh mục loại hàng`

Top actions:

- Dropdown `Tất cả`
- Primary button `+ Thêm mới`

Filters:

- Mã loại
- Tên loại hàng
- Button `Tìm kiếm`
- Button `In`

Columns:

- Mã loại
- Tên loại hàng
- Trạng thái
- Actions

### 13.3 Thương hiệu

Title: `Thương hiệu`
Subtitle: `Quản lý danh mục thương hiệu`

Filters:

- Mã thương hiệu
- Tên thương hiệu

Columns:

- Mã thương hiệu
- Tên thương hiệu
- Xuất xứ
- Trạng thái
- Actions

### 13.4 Đơn vị tính

Title: `Đơn vị tính`
Subtitle: `Quản lý đơn vị tính`

Filters:

- Mã đơn vị
- Tên đơn vị tính

Columns:

- Mã đơn vị
- Tên đơn vị tính
- Trạng thái
- Actions

### 13.5 Nhà cung cấp

Title: `Nhà cung cấp`
Subtitle: `Quản lý danh mục nhà cung cấp`

Filters:

- Mã NCC
- Tên nhà cung cấp
- Điện thoại

Columns:

- Mã NCC
- Tên nhà cung cấp
- Điện thoại
- Email
- Trạng thái
- Actions

### 13.6 Khách hàng

Title: `Khách hàng`
Subtitle: `Quản lý danh mục khách hàng`

Filters:

- Mã KH
- Tên khách hàng
- Điện thoại

Columns:

- Mã KH
- Tên khách hàng
- Điện thoại
- Email
- Trạng thái
- Actions

### 13.7 Sản phẩm

Title: `Sản phẩm`
Subtitle: `Quản lý danh mục sản phẩm`

Top filters/actions:

- Dropdown `Tất cả loại`
- Dropdown `Tất cả`
- Dropdown `Tất cả TT`
- Primary button `+ Thêm mới`

Filters:

- Mã SP
- Tên sản phẩm
- Button `Tìm kiếm`
- Button `In`

Columns:

- Mã SP
- Tên sản phẩm
- Loại hàng
- Thương hiệu
- Giá bán
- Serial
- BH (tháng)
- TT
- Actions

Special:

- Cột Serial dùng badge `Có` hoặc ký tự `—`.
- Giá dùng format Việt Nam: `7.800.000` hoặc `7.800.000đ` tùy màn hình.

### 13.8 Quản lý Serial

Title: `Quản lý Serial`
Subtitle: `Tra cứu trạng thái serial sản phẩm`

Top filter:

- Dropdown `Tất cả`

Filters:

- Serial
- Ngày tạo từ
- Ngày tạo đến
- Button `Tìm kiếm`
- Button `In`

Columns:

- Serial
- Sản phẩm
- Trạng thái
- Ghi chú
- Ngày tạo

### 13.9 Nhập kho

Title: `Nhập kho`
Subtitle: `Quản lý phiếu nhập kho`

Top actions:

- Dropdown `Tất cả loại`
- Dropdown `Tất cả TT`
- Button `Xuất CSV`
- Button `Nhập CSV`
- Primary button `+ Tạo phiếu nhập`

Filters:

- Số phiếu
- Loại
- Nhà cung cấp
- Ngày nhập từ
- Ngày nhập đến
- Button `Tìm kiếm`
- Button `In`

Columns:

- Số phiếu
- Ngày nhập
- Loại
- Nhà cung cấp
- Trạng thái
- Ghi chú
- Actions: xem, duyệt/ghi sổ, sửa, xóa tùy trạng thái

### 13.10 Xuất kho

Title: `Xuất kho`
Subtitle: `Quản lý phiếu xuất kho`

Top actions giống nhập kho, nhưng primary button là `+ Tạo phiếu xuất`.

Filters:

- Số phiếu
- Loại
- Khách hàng
- Ngày xuất từ
- Ngày xuất đến

Columns:

- Số phiếu
- Ngày xuất
- Loại
- Khách hàng
- Trạng thái
- Ghi chú
- Actions

### 13.11 Tồn kho

Title: `Tồn kho`
Subtitle: `Số lượng tồn hiện tại theo sản phẩm`

Filters:

- Mã SP
- Tên sản phẩm
- Button `Tìm kiếm`
- Button `In`

Columns:

- Mã SP
- Tên sản phẩm
- Định mức tối thiểu
- Tồn kho
- Cập nhật lúc

Nếu không có data thì hiển thị empty state.

### 13.12 Hóa đơn mua

Title: `Hóa đơn mua`
Subtitle: `Quản lý hóa đơn mua hàng và công nợ`

Top actions:

- Dropdown `Tất cả NCC`
- Dropdown `Tất cả TT`
- Button `Xuất CSV`
- Button `Nhập CSV`
- Primary button `+ Tạo hóa đơn`

Filters:

- Số hóa đơn
- Nhà cung cấp
- Ngày hóa đơn từ/đến
- Hạn thanh toán từ/đến
- Button `Tìm kiếm`
- Button `In`

Columns:

- Số hóa đơn
- Ngày
- Nhà cung cấp
- Tổng tiền
- Đã TT
- Hạn TT
- Trạng thái
- Actions

### 13.13 Hóa đơn bán

Title: `Hóa đơn bán`
Subtitle: `Quản lý hóa đơn bán hàng và công nợ`

Giống hóa đơn mua, nhưng filter đối tượng là khách hàng.

Columns:

- Số hóa đơn
- Ngày
- Khách hàng
- Tổng tiền
- Đã TT
- Hạn TT
- Trạng thái
- Actions

### 13.14 Quyền bảo hành

Title: `Quyền bảo hành`
Subtitle: `Quản lý quyền bảo hành sản phẩm đã bán`

Top action:

- Primary button `+ Thêm mới`

Filters:

- Serial
- Sản phẩm
- Khách hàng
- Ngày bán từ/đến
- Hết hạn BH từ/đến
- Button `Tìm kiếm`
- Button `In`

Columns:

- Serial
- Sản phẩm
- Khách hàng
- Ngày bán
- Hết hạn BH
- TT

### 13.15 Yêu cầu bảo hành

Title: `Yêu cầu bảo hành`
Subtitle: `Quản lý hồ sơ yêu cầu bảo hành`

Top actions:

- Dropdown `Tất cả`
- Primary button `+ Tạo yêu cầu`

Filters:

- Mã yêu cầu
- Serial
- Khách hàng
- Mô tả lỗi
- Ngày yêu cầu từ/đến
- Ngày giải quyết từ/đến
- Button `Tìm kiếm`
- Button `In`

Columns:

- Mã yêu cầu
- Ngày YC
- Serial
- Khách hàng
- Mô tả lỗi
- Trạng thái
- Actions

### 13.16 Báo cáo

Title: `Báo cáo`
Subtitle: `Tổng hợp dữ liệu kinh doanh`

Top stat cards:

- Tổng doanh thu
- Đã thu
- Còn nợ (bán)
- YC bảo hành

Charts:

- Doanh thu theo tháng
- Tình trạng bảo hành

### 13.17 Người dùng

Title: `Người dùng`
Subtitle: `Quản lý tài khoản và phân quyền`

Top action:

- Primary button `Mời người dùng` hoặc `+ Thêm người dùng`

Filters:

- Họ tên
- Email
- Button `Tìm kiếm`
- Button `In`

Columns:

- Họ tên
- Email
- Vai trò
- Ngày tham gia
- Actions nếu cần

---

## 14. Dialog/form thiết kế thêm/sửa

Khi bấm `Thêm mới`, `Tạo phiếu`, `Tạo hóa đơn`, nên dùng dialog hoặc màn hình form riêng. Với app desktop WPF, form riêng trong content hoặc modal dialog đều ổn.

### Dialog style

| Thuộc tính | Giá trị |
|---|---:|
| Width small | 420-520px |
| Width medium | 720-900px |
| Background | White |
| Radius | 8px |
| Padding | 20-24px |
| Header font | 18-20px bold |
| Footer actions | Right aligned |

### Field style

- Label trên input.
- Height input 34-38px.
- Required field có dấu `*` màu đỏ.
- Validation error hiển thị dưới input, font 12px, màu đỏ.
- Form nhiều cột dùng grid 2 cột.

---

## 15. WPF implementation guidance

### 15.1 Kiến trúc View

Tách rõ:

```text
Views/
  MainWindow.xaml
  Shared/
    SidebarView.xaml
    TopBarView.xaml
    DataTableToolbar.xaml
    StatusBadge.xaml
  Dashboard/
    DashboardView.xaml
  Catalog/
    CategoryListView.xaml
    BrandListView.xaml
    UnitListView.xaml
    SupplierListView.xaml
    CustomerListView.xaml
  Products/
    ProductListView.xaml
    SerialListView.xaml
  Inventory/
    StockInListView.xaml
    StockOutListView.xaml
    StockBalanceView.xaml
    StockLedgerView.xaml
  Invoices/
    PurchaseInvoiceListView.xaml
    SalesInvoiceListView.xaml
  Warranty/
    WarrantyCoverageListView.xaml
    WarrantyClaimListView.xaml
  Reports/
    ReportDashboardView.xaml
  Users/
    UserListView.xaml
```

### 15.2 MainWindow không chứa từng màn hình

MainWindow chỉ chứa shell:

```text
Sidebar + TopBar + ContentControl
```

ContentControl bind vào `CurrentViewModel`:

```xml
<ContentControl Content="{Binding CurrentViewModel}" />
```

Dùng DataTemplate map ViewModel sang View.

### 15.3 Không hard-code dữ liệu trong View

Các bảng bind vào ViewModel collection:

```text
ObservableCollection<CategoryRowViewModel>
ObservableCollection<ProductRowViewModel>
ObservableCollection<StockInRowViewModel>
```

Các filter bind vào property:

```text
SearchCode
SearchName
SelectedStatus
FromDate
ToDate
```

Button bind command:

```text
SearchCommand
CreateCommand
EditCommand
DeleteCommand
PrintCommand
ExportCsvCommand
ImportCsvCommand
```

---

## 16. WPF style snippets

### 16.1 Primary button

```xml
<Style x:Key="PrimaryButtonStyle" TargetType="Button">
    <Setter Property="Height" Value="34" />
    <Setter Property="MinWidth" Value="88" />
    <Setter Property="Padding" Value="14,0" />
    <Setter Property="Foreground" Value="White" />
    <Setter Property="Background" Value="{StaticResource PrimaryPurpleBrush}" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="FontSize" Value="13" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="Cursor" Value="Hand" />
</Style>
```

Nếu XAML parser báo lỗi `Padding` trong `Setter`, dùng trực tiếp trên Button hoặc dùng `Control.Padding`:

```xml
<Setter Property="Control.Padding" Value="14,0" />
```

### 16.2 Secondary button

```xml
<Style x:Key="SecondaryButtonStyle" TargetType="Button">
    <Setter Property="Height" Value="34" />
    <Setter Property="MinWidth" Value="64" />
    <Setter Property="Padding" Value="12,0" />
    <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}" />
    <Setter Property="Background" Value="White" />
    <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="FontSize" Value="13" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="Cursor" Value="Hand" />
</Style>
```

### 16.3 Input TextBox

```xml
<Style x:Key="SearchTextBoxStyle" TargetType="TextBox">
    <Setter Property="Height" Value="34" />
    <Setter Property="MinWidth" Value="180" />
    <Setter Property="Padding" Value="10,0" />
    <Setter Property="FontSize" Value="13" />
    <Setter Property="VerticalContentAlignment" Value="Center" />
    <Setter Property="Background" Value="White" />
    <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
</Style>
```

### 16.4 Table container

```xml
<Border Background="{StaticResource SurfaceBrush}"
        BorderBrush="{StaticResource BorderBrush}"
        BorderThickness="1"
        CornerRadius="6">
    <!-- Filter bar + DataGrid -->
</Border>
```

### 16.5 DataGrid baseline

```xml
<Style x:Key="WareProDataGridStyle" TargetType="DataGrid">
    <Setter Property="AutoGenerateColumns" Value="False" />
    <Setter Property="CanUserAddRows" Value="False" />
    <Setter Property="HeadersVisibility" Value="Column" />
    <Setter Property="GridLinesVisibility" Value="Horizontal" />
    <Setter Property="RowHeight" Value="44" />
    <Setter Property="ColumnHeaderHeight" Value="38" />
    <Setter Property="Background" Value="White" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="HorizontalGridLinesBrush" Value="#E5E0EE" />
    <Setter Property="FontSize" Value="13" />
</Style>
```

---

## 17. Spacing system

Dùng spacing nhất quán để AI không thiết kế lệch.

| Token | Giá trị |
|---|---:|
| `SpaceXs` | 4px |
| `SpaceSm` | 8px |
| `SpaceMd` | 12px |
| `SpaceLg` | 16px |
| `SpaceXl` | 24px |
| `Space2Xl` | 32px |

Trong WPF:

```xml
<Thickness x:Key="PagePadding">24</Thickness>
<Thickness x:Key="SectionBottomMargin">0,0,0,24</Thickness>
<Thickness x:Key="ControlRightMargin">0,0,8,0</Thickness>
<Thickness x:Key="TableCellPadding">14,0</Thickness>
```

---

## 18. UX rules quan trọng

### 18.1 Navigation

- Khi click menu, item active đổi nền tím.
- Content đổi title/subtitle tương ứng.
- Sidebar không reload hoặc nhảy vị trí.
- Nếu sidebar scroll, footer logout vẫn nằm cuối hoặc sticky bottom.

### 18.2 CRUD list

- `Thêm mới` ở góc phải page header.
- Search nằm trong table card, không nằm rời rạc.
- Mỗi bảng nên có filter tối thiểu theo mã/tên/trạng thái.
- Cột action luôn nằm cuối.
- Không dùng icon quá nhiều màu trừ trạng thái quan trọng.

### 18.3 Data density

- Row height khoảng 44px là hợp lý.
- Không dùng row height > 56px trong màn hình quản lý dữ liệu.
- Không thêm avatar/hình ảnh sản phẩm vào bảng trừ khi thật cần.
- Ưu tiên hiển thị nhiều dòng hơn.

### 18.4 Responsive desktop

- Với màn hình nhỏ hơn, content horizontal scroll trong table được phép.
- Sidebar giữ width cố định.
- Stat cards dashboard wrap xuống dòng.
- Filter bar có thể wrap nhưng vẫn giữ label-input theo cặp.

---

## 19. Những thứ không nên làm

- Không dùng nền gradient lớn trong content.
- Không làm card quá bo tròn kiểu mobile app.
- Không dùng shadow mạnh. Ảnh mẫu gần như chỉ dùng border nhẹ, shadow rất ít hoặc không có.
- Không dùng màu sidebar xanh/đen thuần; phải giữ tím đen.
- Không đặt button quá cao hoặc quá to.
- Không để bảng có khoảng trắng quá nhiều.
- Không copy thanh trình duyệt/Base44 preview vào app WPF.
- Không dùng Tailwind token như `slate` trực tiếp trong WPF Material Design.

---

## 20. Acceptance criteria cho AI agent

Một giao diện được xem là đạt nếu:

1. Có sidebar tím đậm cố định bên trái, active item tím sáng.
2. Có top bar trắng cao khoảng 56px.
3. Content background xám rất nhạt.
4. Page title/subtitle giống pattern ảnh.
5. Table card trắng, border mảnh, header xám nhạt.
6. Filter bar nằm trên bảng, input thấp gọn, label nhỏ.
7. Button chính màu `#7C3AED`.
8. Status badge đúng màu theo mapping.
9. Dashboard có 6 stat cards và chart cards.
10. Giao diện không giống web landing page, mà giống internal admin desktop app.

---

## 21. Prompt ngắn cho Antigravity

Khi yêu cầu AI coding agent tạo màn hình mới, dùng prompt dạng:

```text
Thiết kế màn hình WPF MVVM theo WarePro UI Design Guideline. 
Giữ shell gồm sidebar tím đậm, topbar trắng, content nền #F7F6FA. 
Màn hình phải dùng pattern Page Header + Filter Bar + DataGrid card. 
Button chính màu #7C3AED, input compact 34px, table row 44px, header #F3F1F7. 
Không tạo landing page. Không dùng màu khác guideline. Không hard-code nghiệp vụ trong code-behind.
```

---

## 22. Ghi chú triển khai với Material Design in XAML

Trong `App.xaml`, có thể dùng theme Material Design nhưng màu custom vẫn nên khai báo riêng:

```xml
<materialDesign:BundledTheme BaseTheme="Light"
                             PrimaryColor="DeepPurple"
                             SecondaryColor="Purple" />
```

Sau đó override bằng brush custom của WarePro. Không dùng `SecondaryColor="Slate"` vì không hợp lệ trong Material Design palette.

Nếu cần màu slate, dùng hex:

```xml
<SolidColorBrush x:Key="Slate900Brush" Color="#0F172A" />
<SolidColorBrush x:Key="Slate800Brush" Color="#1E293B" />
<SolidColorBrush x:Key="Slate700Brush" Color="#334155" />
<SolidColorBrush x:Key="Slate600Brush" Color="#475569" />
```

Với app trong ảnh, sidebar nên dùng tím đen `#1F1532`, không dùng slate thuần `#1E293B`, vì slate sẽ làm mất chất tím chủ đạo.
