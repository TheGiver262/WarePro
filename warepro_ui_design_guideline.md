# WarePro UI Design Guideline

Tài liệu này dùng làm chỉ dẫn cho Google Antigravity hoặc AI coding agent khi thiết kế giao diện WPF/C# MVVM cho phần mềm **Quản lý hàng hóa & Bảo hành**. Mục tiêu là tái tạo phong cách giao diện giống các ảnh preview đã cung cấp: desktop admin dashboard, sidebar tím đậm, nội dung trắng/xám nhạt, bảng dữ liệu mật độ cao, thao tác CRUD rõ ràng.

---

## 0. Quy tắc bắt buộc — Tránh lỗi WPF / Material Design

### 0.1 Không dùng Tailwind/CSS token trực tiếp trong XAML

Các token web-style (`slate`, `bg-card`, `rounded-lg`, `margin.bottom.md`) **không phải giá trị WPF hợp lệ**.

```xml
<!-- SAI -->
<Border Background="slate" />
<TextBlock Margin="margin.bottom.md" />

<!-- ĐÚNG -->
<Border Background="{StaticResource SidebarBgBrush}" />
<TextBlock Margin="{StaticResource SectionBottomMargin}" />
```

### 0.2 Không dùng `SecondaryColor="BlueGrey"` hoặc `SecondaryColor="Slate"`

Material Design in XAML không có palette `Slate`. Dùng màu hex tự khai báo:

```xml
<materialDesign:BundledTheme
    BaseTheme="Light"
    PrimaryColor="DeepPurple"
    SecondaryColor="Purple" />
```

### 0.3 Không dùng style key chưa xác nhận tồn tại

Nếu dùng `BasedOn="{StaticResource MaterialDesignFlatMidPrimaryButton}"` hay bất kỳ key nào không có trong project, app sẽ throw `XamlParseException` lúc runtime. Chỉ dùng các style key đã được định nghĩa trong `Themes/`.

Các style key hợp lệ hiện tại: `AppPrimaryButton`, `AppSecondaryButton`, `AppDangerButton`, `AppCard`, `AppTextBoxStyle`, `AppComboBoxStyle`, `AppDatePickerStyle`.

- **CẤM**: Không dùng `MaterialDesignOutlinedTextBox`, `MaterialDesignOutlinedComboBox`, `MaterialDesignOutlinedDatePicker` hoặc các style dạng hộp (boxed) trừ khi có yêu cầu đặc biệt.
- **MẶC ĐỊNH (Implicit)**: Từ phiên bản hiện tại, các style `AppTextBoxStyle`, `AppComboBoxStyle`, `AppDatePickerStyle` đã được thiết lập làm **Style mặc định (Implicit Style)** cho toàn bộ ứng dụng. Các control `TextBox`, `ComboBox`, `DatePicker` sẽ tự động nhận style gạch chân (no-box) mà không cần khai báo `Style="{StaticResource ...}"`.

### 0.4 Dùng PackIcon với fallback strategy

Nếu một `Kind` không tồn tại trong phiên bản MaterialDesignThemes đang cài, thay bằng `CircleSmall`, `InformationOutline`, hoặc xóa icon. Danh sách icon an toàn:

```
ViewDashboard, PackageVariant, TagOutline, Domain, CubeOutline,
AccountGroup, Account, TruckDelivery, ArrowDownBold, ArrowUpBold,
ChartBar, FileDocumentOutline, ShieldCheckOutline, WrenchOutline,
AlertOutline, Logout, Magnify, Printer, Plus, Pencil, Delete,
EyeOutline, CheckCircleOutline, Download, Upload
```

### 0.5 Đồng bộ Icon Header và Sidebar

Icon sử dụng trong Header của mỗi View phải trùng khớp hoàn toàn với Icon được sử dụng cho Menu tương ứng trong Sidebar của MainWindow. Điều này giúp người dùng nhận diện nhanh chóng phân hệ đang thao tác.

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
| `PrimaryPurpleSoft` | `#EEE7FF` | Background nhạt cho icon/badge tím |
| `SidebarBg` | `#211733` | Nền sidebar |
| `SidebarBgDarker` | `#1A102A` | Nền footer/logout sidebar |
| `SidebarHover` | `#34264D` | Hover menu sidebar |
| `SidebarActive` | `#7C3AED` | Menu đang chọn |
| `PageBg` | `#F7F7FA` | Nền vùng content |
| `Surface` | `#FFFFFF` | Card, table container |
| `SurfaceMuted` | `#F3F1F7` | Table header |
| `Border` | `#DED9E8` | Viền card/input/table |
| `TextPrimary` | `#2A2533` | Text chính |
| `TextSecondary` | `#756B82` | Subtitle, label phụ |
| `TextMuted` | `#8A8095` | Group heading sidebar |

### 3.2 Màu trạng thái

| Status | Background | Text | Dùng cho |
|---|---|---|---|
| Success | `#DCFCE7` | `#16A34A` | Hoạt động, đã thanh toán, đã sửa |
| Info | `#DBEAFE` | `#2563EB` | Đã duyệt, đã bán |
| Warning | `#FEF3C7` | `#D97706` | Đang xử lý, TT một phần |
| Danger | `#FEE2E2` | `#EF4444` | Chưa TT, từ chối |
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
    <SolidColorBrush x:Key="PrimaryPurpleSoftBrush" Color="#EEE7FF" />

    <SolidColorBrush x:Key="SidebarBgBrush" Color="#211733" />
    <SolidColorBrush x:Key="SidebarBgDarkerBrush" Color="#1A102A" />
    <SolidColorBrush x:Key="SidebarHoverBrush" Color="#34264D" />
    <SolidColorBrush x:Key="SidebarActiveBrush" Color="#7C3AED" />

    <SolidColorBrush x:Key="PageBgBrush" Color="#F7F7FA" />
    <SolidColorBrush x:Key="SurfaceBrush" Color="#FFFFFF" />
    <SolidColorBrush x:Key="SurfaceMutedBrush" Color="#F3F1F7" />
    <SolidColorBrush x:Key="BorderBrush" Color="#DED9E8" />

    <SolidColorBrush x:Key="TextPrimaryBrush" Color="#2A2533" />
    <SolidColorBrush x:Key="TextSecondaryBrush" Color="#756B82" />
    <SolidColorBrush x:Key="TextMutedBrush" Color="#8A8095" />
    <SolidColorBrush x:Key="SidebarTextBrush" Color="#D8D0E6" />
    <SolidColorBrush x:Key="SidebarMutedTextBrush" Color="#8B7A9F" />

    <!-- Status -->
    <SolidColorBrush x:Key="SuccessBgBrush" Color="#DCFCE7" />
    <SolidColorBrush x:Key="SuccessTextBrush" Color="#16A34A" />
    <SolidColorBrush x:Key="InfoBgBrush" Color="#DBEAFE" />
    <SolidColorBrush x:Key="InfoTextBrush" Color="#2563EB" />
    <SolidColorBrush x:Key="WarningBgBrush" Color="#FEF3C7" />
    <SolidColorBrush x:Key="WarningTextBrush" Color="#D97706" />
    <SolidColorBrush x:Key="DangerBgBrush" Color="#FEE2E2" />
    <SolidColorBrush x:Key="DangerTextBrush" Color="#EF4444" />
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

### Kích thước và Quy tắc Typography (Pro Max Standard)

Hệ thống Typography được thiết kế để tạo cảm giác chuyên nghiệp, "locked-in" cho ứng dụng doanh nghiệp.

| Thành phần | Size | Weight | Casing (Hoa/Thường) | Màu sắc |
|---|---:|---:|---|---|
| **Page Title** | 22px | 700 (Bold) | Sentence Case | `TextPrimary` |
| **Page Subtitle** | 13px | 400 (Regular) | Sentence Case | `TextSecondary` |
| **Sidebar Group Heading** | 11px | 700 (Bold) | **UPPERCASE** | `TextMuted` |
| **Sidebar Menu Item** | 14px | 500 (Medium) | Sentence Case | `SidebarText` |
| **DataGrid Header** | 13px | 600 (SemiBold) | **UPPERCASE** | `TextSecondary` |
| **DataGrid Cell** | 13px | 400 (Regular) | Sentence Case | `TextPrimary` |
| **Input Label (Search/Form)** | 12px | 600 (SemiBold) | **UPPERCASE** | `TextSecondary` |
| **Button Text** | 13px | 600 (SemiBold) | **UPPERCASE** | White / `TextPrimary` |
| **Status Badge** | 10px | 700 (Bold) | **UPPERCASE** | Theo trạng thái |
| **Dashboard Stat Number** | 24px | 700 (Bold) | N/A | `TextPrimary` |

### Quy tắc chi tiết

1. **UPPERCASE (Viết hoa toàn bộ)**:
    - Chỉ dùng cho các nhãn cấu trúc: Header bảng, Label ô nhập liệu, Tiêu đề nhóm Sidebar, và Chữ trên Button.
    - Giúp tạo sự phân biệt rõ ràng giữa "Khung giao diện" và "Dữ liệu người dùng".
    - **Lưu ý Tiếng Việt**: Kiểm tra kỹ các dấu tiếng Việt khi viết hoa (ví dụ: `HÓA ĐƠN`, `NHẬP KHO`).

2. **Sentence Case (Viết hoa chữ cái đầu)**:
    - Dùng cho toàn bộ dữ liệu hiển thị trong bảng (DataGrid Cells).
    - Dùng cho Page Title và Subtitle.
    - Giúp giảm mỏi mắt khi đọc lượng lớn dữ liệu.

3. **Font Family**:
    - Mặc định là `Segoe UI`. Không thay đổi font trừ khi có yêu cầu đặc biệt.

4. **Độ đậm (Font Weight)**:
    - Không dùng Medium (500) cho dữ liệu trong bảng, dùng Regular (400) để tăng độ thanh thoát.
    - Header và Label luôn dùng SemiBold (600) hoặc Bold (700) để nhấn mạnh.

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

- Normal: transparent background, text `#D8D0E6`.
- Hover: background `#34264D`.
- Active: background `#7C3AED`, text white.
- Group heading: uppercase, màu `#8B7A9F`, letter spacing nhẹ nếu làm được.
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

### 6.5 Sidebar item XAML template

Tránh hardcode `PackIconKind` trực tiếp nhời, bind từ `NavItemViewModel`:

```xml
<Button Command="{Binding DataContext.NavigateCommand, RelativeSource={RelativeSource AncestorType=Window}}"
        CommandParameter="{Binding}"
        Height="36" Margin="8,2"
        Background="Transparent" BorderThickness="0"
        Foreground="{StaticResource SidebarTextBrush}">
    <Grid Margin="10,0">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="26" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>
        <materialDesign:PackIcon Grid.Column="0" Kind="{Binding IconKind}"
                                 Width="16" Height="16" VerticalAlignment="Center" />
        <TextBlock Grid.Column="1" Text="{Binding Title}"
                   VerticalAlignment="Center" FontSize="14"
                   TextTrimming="CharacterEllipsis" />
    </Grid>
</Button>
```

Active/hover state xử lý qua Style Trigger theo `IsSelected`.

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

### 10.2 Search/filter bar (Thanh tìm kiếm & Xuất dữ liệu)

Trong thiết kế "Pro Max", thanh tìm kiếm nằm trong card table, phía trên header bảng và được chia thành nhiều trường nhập liệu để lọc nhanh (Mã, Tên, SĐT/Email...).

- **Đồng bộ 1-1 với Grid (Bắt buộc)**: Thanh tìm kiếm phải có đủ các trường nhập liệu tương ứng với các cột hiển thị trong DataGrid. Quy tắc: **Có bao nhiêu cột có thể lọc/tìm kiếm trong bảng thì phải có bấy nhiêu trường tìm kiếm tương ứng trên thanh lọc.**
- **Cấu trúc Grid**: Chia `Grid.ColumnDefinitions` thành các cột tương ứng với số lượng filter. Cột đầu tiên (thường là Search Box chính) để `Width="*"`, các cột filter khác và nút hành động để `Width="Auto"` hoặc `Width="*"` tùy theo độ dài nội dung.
- **Label-Aligned Layout**: Mỗi trường tìm kiếm được đặt trong một `StackPanel`. Nhãn (`TextBlock`) nằm ở trên (dùng `TypographyLabel`), Input (`TextBox`, `ComboBox`, `DatePicker`) nằm ở dưới.
- **Loại bỏ nút Reset/Lọc**: Tuyệt đối không dùng nút "Lọc" hoặc "Reset". Dữ liệu phải được tự động lọc ngay khi người dùng nhập liệu bằng cách sử dụng Binding `UpdateSourceTrigger=PropertyChanged` kết hợp với logic xử lý trong `OnSearch...Changed` của ViewModel.
- **Đồng bộ chiều cao & Style**: 
  - Tất cả các control (`TextBox`, `ComboBox`, `DatePicker`, `Button`) phải có `Height="32"`.
  - Sử dụng style **Underline** (không có viền hộp - boxed), gán `materialDesign:TextFieldAssist.HasClearButton="True"` cho TextBox. Đây là style mặc định cho toàn bộ ứng dụng.
- **Nút Làm mới (Refresh)**:
  - Style: `{StaticResource AppRefreshButton}` (Thừa kế từ SecondaryButton, không cần đặt cứng `Width="32"` hay `Padding="0"`).
  - Icon: `Refresh` (Kích thước `Width="18" Height="18"`).
  - Vị trí: Đặt bên phải nút Bộ lọc nâng cao (nếu có) và trước nút Xuất Excel, `Margin="0,0,8,0"`.
- **Nút Xuất Excel**:
  - Style: `{StaticResource AppExcelButton}` (Thừa kế từ PrimaryButton, không cần đặt cứng `Width="140"` hay `Height="32"`).
  - Content: "XUẤT EXCEL" (In hoa).
  - Icon: `FileExcelOutline` hoặc `FileExcel` (Kích thước `Width="18" Height="18"`).
  - Vị trí: Đặt ở góc phải cùng của thanh tìm kiếm, `VerticalAlignment="Bottom"`.

#### 10.2.1 Cấu trúc XAML mẫu (Audit Log)

```xml
<materialDesign:Card Style="{StaticResource AppCard}" Padding="{StaticResource Spacing.md}">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>         <!-- Ô tìm kiếm chính -->
            <ColumnDefinition Width="Auto"/>      <!-- Filter 1 -->
            <ColumnDefinition Width="Auto"/>      <!-- Nút hành động -->
        </Grid.ColumnDefinitions>

        <!-- Search Box -->
        <StackPanel Grid.Column="0" Margin="{StaticResource Margin.Right.md}">
            <TextBlock Text="NỘI DUNG TÌM KIẾM" Style="{StaticResource TypographyLabel}" Margin="{StaticResource Margin.Bottom.xs}"/>
            <TextBox materialDesign:HintAssist.Hint="Nhập nội dung..." Height="32"/>
        </StackPanel>

        <!-- Filter ComboBox -->
        <StackPanel Grid.Column="1" Margin="{StaticResource Margin.Right.md}" Width="160">
            <TextBlock Text="ĐỐI TƯỢNG" Style="{StaticResource TypographyLabel}" Margin="{StaticResource Margin.Bottom.xs}"/>
            <ComboBox Height="32"/>
        </StackPanel>

        <!-- Action Buttons -->
        <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Bottom">
            <!-- Nút Làm mới -->
            <Button Style="{StaticResource AppRefreshButton}" 
                    Command="{Binding RefreshCommand}"
                    ToolTip="Làm mới bộ lọc"
                    Margin="0,0,8,0">
                <materialDesign:PackIcon Kind="Refresh" Width="18" Height="18"/>
            </Button>
            
            <!-- Nút Xuất Excel -->
            <Button Command="{Binding ExportLogsCommand}" Style="{StaticResource AppExcelButton}">
                <StackPanel Orientation="Horizontal">
                    <materialDesign:PackIcon Kind="FileExcelOutline" Width="18" Height="18" VerticalAlignment="Center" Margin="0,0,8,0"/>
                    <TextBlock Text="XUẤT EXCEL" VerticalAlignment="Center" Style="{StaticResource TypographyButtonText}"/>
                </StackPanel>
            </Button>
        </StackPanel>
    </Grid>
</materialDesign:Card>
```

#### Kích thước chuẩn

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

### 11.2 Căn lề dữ liệu (Bắt buộc)

Để đảm bảo tính chuyên nghiệp và dễ đọc cho dữ liệu tài chính/kho bãi, áp dụng các style căn lề sau:

- **Số lượng, Đơn giá, Thành tiền**: Luôn căn lề **PHẢI** (`ElementStyle="{StaticResource DataGridTextRight}"`).
- **Mã, Ngày tháng, Đơn vị tính, Trạng thái**: Luôn căn lề **GIỮA** (`ElementStyle="{StaticResource DataGridTextCenter}"`).
- **Tên, Diễn giải, Nội dung**: Căn lề **TRÁI** (Mặc định).
- **Căn dọc**: Toàn bộ các ô trong hàng phải được căn giữa theo chiều dọc (`VerticalAlignment="Center"`). Các `ElementStyle` chuẩn (`DataGridTextCenter`, `DataGridTextRight`) đã bao gồm thiết lập này.

### 11.3 Typography trong bảng

- **Header Text**: Phải dùng `FontWeight="SemiBold"` và `CharacterCasing="Upper"`. Màu `#6F667C`.
- **Cell Text**: Dùng `FontSize="13"` và `FontWeight="Regular"`. Căn lề theo quy tắc tại mục 11.2.
- **Information Density**: Giữ khoảng cách dòng vừa phải (RowHeight 42-46px) để đảm bảo hiển thị được nhiều bản ghi nhưng vẫn dễ đọc.

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
Padding: 2px 8px
CornerRadius: 4px
FontSize: 10px
FontWeight: Bold
CharacterCasing: Upper
VerticalAlignment: Center
```

Quy tắc: Badge phải ôm sát nội dung, không quá to so với text trong bảng. Sử dụng `StatusToBgBrushConverter` và `StatusToTextConverter` để đồng bộ màu sắc và ngôn ngữ (Tiếng Việt).

### Mapping trạng thái

| Trạng thái | Background | Text |
|---|---|---|
| Hoạt động | `#DCFCE7` | `#16A34A` |
| Ngừng HĐ | `#E5E7EB` | `#4B5563` |
| Trong kho | `#DCFCE7` | `#16A34A` |
| Đã bán | `#DBEAFE` | `#2563EB` |
| Nháp | `#E5E7EB` | `#4B5563` |
| Đã duyệt | `#DBEAFE` | `#2563EB` |
| Đã TT | `#DCFCE7` | `#16A34A` |
| Chưa TT | `#FEE2E2` | `#EF4444` |
| TT một phần | `#FEF3C7` | `#D97706` |
| Tiếp nhận | `#DBEAFE` | `#2563EB` |
| Đang xử lý | `#FEF3C7` | `#D97706` |
| Đã sửa | `#DCFCE7` | `#16A34A` |
| Từ chối | `#FEE2E2` | `#EF4444` |
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
Subtitle: `Quản lý danh mục sản phẩm`

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
| `SpaceMd` | 16px |
| `SpaceLg` | 24px |
| `SpaceXl` | 32px |
| `Space2Xl` | 48px |

Trong WPF:

```xml
<Thickness x:Key="PagePadding">24</Thickness>
<Thickness x:Key="SectionBottomMargin">0,0,0,24</Thickness>
<Thickness x:Key="ControlRightMargin">0,0,8,0</Thickness>
<Thickness x:Key="TableCellPadding">14,10</Thickness>
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

Với app trong ảnh, sidebar nên dùng tím đen `#211733`, không dùng slate thuần `#1E293B`, vì slate sẽ làm mất chất tím chủ đạo.

---

## 23. App.xaml template chuẩn

```xml
<Application x:Class="WarePro.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
             StartupUri="Views/MainWindow.xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <materialDesign:BundledTheme
                    BaseTheme="Light"
                    PrimaryColor="DeepPurple"
                    SecondaryColor="Purple" />

                <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml" />

                <ResourceDictionary Source="Themes/Colors.xaml" />
                <ResourceDictionary Source="Themes/Spacing.xaml" />
                <ResourceDictionary Source="Themes/Typography.xaml" />
                <ResourceDictionary Source="Themes/Radius.xaml" />
                <ResourceDictionary Source="Themes/Buttons.xaml" />
                <ResourceDictionary Source="Themes/Cards.xaml" />
                <ResourceDictionary Source="Themes/Inputs.xaml" />
                <ResourceDictionary Source="Themes/Tables.xaml" />
                <ResourceDictionary Source="Themes/Navigation.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

---

## 24. MVVM Navigation Rules

`MainWindow` không chứa business logic. Dùng cấu trúc:

```txt
MainViewModel
├── ObservableCollection<NavGroupViewModel> MenuGroups
├── NavItemViewModel SelectedNavItem
├── object CurrentViewModel
└── ICommand NavigateCommand
```

Navigation flow:

```txt
User click sidebar item
→ NavigateCommand nhận NavItemViewModel
→ Clear tất cả IsSelected
→ item được chọn: IsSelected = true
→ CurrentViewModel = ViewModelFactory.Create(PageKey)
→ ContentControl hiển thị View tương ứng qua DataTemplate
```

DataTemplate trong App.xaml hoặc MainWindow.Resources:

```xml
<DataTemplate DataType="{x:Type vm:CategoryViewModel}">
    <views:CategoryView />
</DataTemplate>
```

---

## 25. Data Loading Rules

Mỗi ViewModel bảng dữ liệu phải expose:

```txt
ObservableCollection<TItemViewModel> Items
ICommand SearchCommand
ICommand ClearFilterCommand
ICommand AddCommand
ICommand EditCommand
ICommand DeleteCommand
ICommand PrintCommand
ICommand ExportCsvCommand
ICommand ImportCsvCommand
bool IsLoading
string SearchText (hoặc filter-specific properties)
int TotalCount
```

Không đặt database call trong View. Dùng services:

```txt
CategoryViewModel  → ICategoryService
ProductViewModel   → IProductService
StockInViewModel   → IInventoryService
InvoiceViewModel   → IInvoiceService
WarrantyViewModel  → IWarrantyService
```

---

## 26. Final Implementation Checklist

Trước khi generate XAML, kiểm tra:

```txt
[ ] App.xaml import MaterialDesign3.Defaults.xaml
[ ] Colors.xaml định nghĩa đầy đủ mọi brush dùng trong XAML
[ ] Không có Color="BlueGrey" hay Background="slate"
[ ] Không có SecondaryColor="BlueGrey"
[ ] Không có Tailwind class name trong XAML
[ ] Không có CSS variable trong XAML
[ ] Không dùng StaticResource key chưa xác nhận
[ ] Tất cả PackIcon Kind đã kiểm tra hoặc có fallback
[ ] Tables dùng DataGrid, không dùng HTML-like layout
[ ] Views chỉ bind vào ViewModels
[ ] Code-behind chỉ xử lý window behavior thuần túy
[ ] Mọi style key dùng phải có trong Themes/ của project
```

---

## 27. Thứ tự build đề xuất

Build theo thứ tự này để tạo design foundation trước khi implement màn hình phức tạp:

```txt
1. MainWindow Shell (Sidebar + Topbar + ContentControl)
2. Sidebar navigation + NavGroupViewModel
3. DashboardView với stat cards
4. Generic DataGrid container style
5. CategoryView / BrandView / UnitView
6. ProductView
7. StockInView / StockOutView
8. PurchaseInvoiceView / SalesInvoiceView
9. WarrantyClaimView
10. ReportView
```
## 14. "Pro Max" Design System (Update 2026)

Đây là các quy chuẩn thiết kế nâng cao đã được kiểm chứng và hoàn thiện qua hai module **Category** và **User Management**. Tất cả các View tiếp theo phải tuân thủ nghiêm ngặt các quy tắc này.

### 14.1 Bố cục 3 hàng (Standard 3-Row Layout)
Mỗi View chính (UserControl) phải chia Grid thành 3 hàng:
- **Hàng 0 (Auto)**: Header section (Tiêu đề trang, icon module, nút thêm mới nhanh).
- **Hàng 1 (Auto)**: Search/Filter section (Ô tìm kiếm, dropdown lọc, nút xuất báo cáo).
- **Hàng 2 (*)**: Content section (DataGrid chứa dữ liệu).

### 14.2 Quy chuẩn DataGrid "Pro Max"
Để đảm bảo tính ổn định và thẩm mỹ, DataGrid phải cấu hình như sau:

- **Row Virtualization**: Phải đặt `EnableRowVirtualization="False"`. Đây là bắt buộc để cột số thứ tự (STT) không bị nhảy số khi cuộn chuột.
- **AlternationCount**: Đặt `AlternationCount="10000"` để hỗ trợ đánh số thứ tự.
- **Tiêu đề cột (Column Headers)**:
    - **Căn giữa**: Phải đặt `HorizontalContentAlignment="Center"` trong `DataGrid.ColumnHeaderStyle`.
    - **Icon bộ lọc**: Mỗi tiêu đề cột phải chứa một `PackIcon` loại `Filter` (size 14x14, Opacity 0.6) nằm bên trái văn bản.
    - **XAML Template**:
      ```xml
      <DataGridTemplateColumn.Header>
          <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
              <materialDesign:PackIcon Kind="Filter" Width="14" Height="14" VerticalAlignment="Center" Margin="0,0,6,0" Opacity="0.6"/>
              <TextBlock Text="TÊN CỘT" VerticalAlignment="Center" FontWeight="SemiBold"/>
          </StackPanel>
      </DataGridTemplateColumn.Header>
      ```

### 14.3 Cột Số Thứ Tự (STT)
Mọi bảng dữ liệu phải có cột STT ở vị trí đầu tiên:
- **Đặc điểm**: Rộng 50-70px, không cho phép sắp xếp (`CanUserSort="False"`).
- **Binding**: Dùng `AlternationIndex` kết hợp với `RowIndexConverter`.
- **Style**: Căn giữa nội dung.

### 14.4 Trạng thái (Status Chip Pro Max)
Thiết kế chip trạng thái hiện đại hơn với chấm chỉ thị (Dot indicator):
- **Cấu trúc**: `Border` (bo góc 12px) -> `StackPanel` (Horizontal) -> `Ellipse` (Dot) + `TextBlock` (Text).
- **Màu sắc**:
    - **Đang hoạt động**: Nền `#DCFCE7`, Chữ `#166534`, Chấm `#22C55E`.
    - **Ngừng hoạt động**: Nền `#FEE2E2`, Chữ `#991B1B`, Chấm `#EF4444`.

### 14.5 Thao tác (Row Actions)
Cột thao tác cuối cùng phải dùng `MaterialDesignIconButton` với icon rõ ràng:
- **Sửa**: `PencilOutline` hoặc `AccountEditOutline` (màu Tertiary/Secondary).
- **Xóa**: `TrashCanOutline` (màu Error/Danger).
- **Kích thước**: Button 36x36 hoặc 38x38, Icon 20-22px.

### 14.6 Form Chỉnh sửa (Edit Panel)
Đối với các form chỉnh sửa nhanh (Overlay/Popup):
- **Trạng thái**: Thêm CheckBox "Đang hoạt động" ở góc dưới bên trái, trên các nút lưu/hủy.
- **Vị trí nút**: Nút "LƯU THAY ĐỔI" (Primary) và "HUỶ" (Outlined) đặt ở góc dưới bên phải.
- **Input**: Độ cao chuẩn 50px, có nhãn (Label) phía trên.

### 14.7 Đồng bộ Icon (Icon Synchronization)
Mọi View khi được mở phải hiển thị đúng Icon đã khai báo trong Sidebar. Ví dụ: Nếu Sidebar dùng `TruckOutline` cho Nhà cung cấp, thì `SupplierView` cũng phải dùng `TruckOutline` trong Header.
### 14.8 Tương tác Tìm kiếm & Bộ lọc (Pro Max Search Standards)
 Để nâng cao trải nghiệm người dùng, thanh tìm kiếm trong các module hiện đại phải tuân thủ:
 - **Đồng bộ 1-1 (Search-to-Column Sync)**: Mỗi cột hiển thị trong DataGrid mà người dùng có nhu cầu tìm kiếm thì PHẢI có một trường nhập liệu tương ứng trên thanh lọc.
 - **Lọc ngầm định (Implicit Filtering)**: Loại bỏ hoàn toàn các nút "Lọc", "Tìm kiếm" hay "Reset". 
    - Sử dụng `UpdateSourceTrigger=PropertyChanged` cho mọi Binding trên thanh tìm kiếm.
    - ViewModel phải triển khai logic tải lại dữ liệu trong các phương thức `On[PropertyName]Changed` (ví dụ: `OnSearchNameChanged`).
 - **Giao diện chuẩn (Layout Standards)**:
    - **Chiều cao**: Tất cả control (TextBox, ComboBox, DatePicker) cố định `Height="32"`.
    - **TextBox**: Chỉ dùng style **Underline** (không dùng Boxed/Outlined) để thanh thoát và không chiếm diện tích. Gán `materialDesign:TextFieldAssist.HasClearButton="True"`.
    - **ComboBox/DatePicker**: Dùng `materialDesign:TextFieldAssist.TextFieldCornerRadius="2"` để tạo sự đồng nhất.
    - **Nhãn (Labels)**: Đặt ngay phía trên control, dùng `TypographyLabel` (Size 11-12, Bold, Uppercase).
 - **Nút Xuất dữ liệu**: Nút "XUẤT EXCEL" luôn là thành phần cuối cùng bên phải của thanh tìm kiếm. Sử dụng `AppPrimaryButton` với icon `FileExcel`.
