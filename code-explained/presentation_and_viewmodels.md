# Giải thích chi tiết mã nguồn: Lớp Giao diện & ViewModels (WPF Presentation & MVVM)

Hệ thống **QuanLyHangHoa** sử dụng công nghệ **WPF (Windows Presentation Foundation)** trên nền .NET 8. Giao diện được thiết kế theo mẫu thiết kế **MVVM (Model-View-ViewModel)** giúp tách rời hoàn toàn mã xử lý logic hiển thị (trong ViewModels) khỏi khai báo giao diện (trong các tệp XAML của Views). Sự tương tác giữa View và ViewModel được thực hiện hoàn toàn thông qua cơ chế **Data Binding** và **Commands** của thư viện **CommunityToolkit.Mvvm**.

Tài liệu này giải thích chi tiết hoạt động của các lớp Presentation và ViewModels của toàn bộ dự án.

---

## 1. Lớp điều phối chính (`MainWindow` & `MainViewModel`)

Đây là trung tâm điều phối của toàn bộ ứng dụng sau khi đăng nhập thành công.

### A. Cơ chế Navigation & View Cache (Bộ đệm giao diện)
Để tối ưu hiệu năng hiển thị và tránh việc khởi tạo lại các giao diện WPF phức tạp (gây trễ UI và lãng phí bộ nhớ), `MainViewModel` sử dụng một cơ chế cache nội bộ:
* **Cấu trúc bộ đệm:** Sử dụng một `Dictionary<string, UserControl> _viewCache` để lưu trữ các View đã từng được mở.
* **Thuật toán điều hướng (`NavigateToView`):**
  ```csharp
  private void NavigateToView<TView>(string cacheKey, Func<TView> viewFactory, string title, string subtitle) where TView : UserControl
  {
      if (!_viewCache.TryGetValue(cacheKey, out var view))
      {
          view = viewFactory();
          _viewCache[cacheKey] = view;
      }
      else
      {
          // Nếu View đã tồn tại trong Cache, kiểm tra xem ViewModel của nó có cần làm mới dữ liệu không
          if (view.DataContext is IRefreshable refreshable)
          {
              refreshable.RefreshData();
          }
      }
      CurrentView = view;
      CurrentViewTitle = title;
      CurrentViewSubtitle = subtitle;
  }
  ```
* **Ý nghĩa:** Khi người dùng chuyển đổi qua lại giữa các menu (ví dụ từ Kho Hàng $\rightarrow$ Nhập Kho $\rightarrow$ Kho Hàng), màn hình Kho Hàng sẽ hiển thị lập tức mà không phải truy vấn lại danh sách sản phẩm từ DB. Tuy nhiên, nhờ interface `IRefreshable`, ViewModel vẫn được kích hoạt hàm `RefreshData()` để nạp thêm các thay đổi mới nhất (nếu có).

### B. Kiểm soát hiển thị theo quyền hạn (Sidebar Security Binding)
Sidebar hiển thị các nút chức năng một cách linh hoạt dựa trên quyền hạn của tài khoản đang đăng nhập. `MainViewModel` định nghĩa các thuộc tính boolean:
* `IsAdmin`: `AuthorizationService.CanPerform(CurrentUser, PermissionAction.ManageUsers)`
* `CanViewLogs`: `AuthorizationService.CanPerform(CurrentUser, PermissionAction.ManageAuditLogs)`
Trên XAML, các menu tương ứng được liên kết hiển thị thông qua bộ chuyển đổi Boolean sang Visibility:
```xml
Visibility="{Binding IsAdmin, Converter={StaticResource BooleanToVisibilityConverter}}"
```
Điều này đảm bảo nhân viên kho thông thường sẽ không nhìn thấy menu "Quản lý người dùng" hoặc "Nhật ký hệ thống".

---

## 2. Giao diện Đăng nhập (`LoginViewModel`)

Lớp [LoginViewModel.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/ViewModels/LoginViewModel.cs) quản lý trạng thái của màn hình đăng nhập, thực thi lệnh xác thực qua `AuthenticationService` và xử lý các cảnh báo bảo mật.

* **Thuộc tính quan trọng:** `Username`, `Password`, `ErrorMessage`, `IsLoading`.
* **Cơ chế thông báo bảo mật mềm (Soft Lockout Warning):**
  Khi người dùng nhập sai thông tin đăng nhập, hệ thống sẽ kiểm tra số lần nhập sai được trả về từ service:
  ```csharp
  if (result.FailedLoginCount >= 3 && result.FailedLoginCount < 5)
  {
      ErrorMessage = "Tên tài khoản hoặc mật khẩu không đúng!\n(Nhập sai tên đăng nhập/mật khẩu liên tiếp sẽ bị khóa tài khoản tạm thời)";
  }
  else
  {
      ErrorMessage = "Tên tài khoản hoặc mật khẩu không đúng!";
  }
  ```
  Cơ chế này cảnh báo trước cho người dùng thật biết họ sắp bị khóa tài khoản, đồng thời không cung cấp thông tin chi tiết về sự tồn tại của tài khoản cho kẻ tấn công (Brute-force attacker).

---

## 3. Giao diện Lập phiếu giao dịch (`StockInViewModel` & `StockOutViewModel`)

Đây là các màn hình nghiệp vụ phức tạp hỗ trợ thủ kho tạo mới và quản lý các phiếu xuất/nhập hàng.

* **Quản lý dòng chi tiết động:** Các dòng sản phẩm được liên kết với một `ObservableCollection<StockInLine>` (hoặc `StockOutLine`). Bất kỳ thao tác thêm/xóa sản phẩm trên bảng hiển thị đều tự động cập nhật vào danh sách này.
* **Cơ chế bắt sự kiện quét mã vạch (Barcode Scanner Integration):**
  Khi thủ kho đặt con trỏ tại ô quét Serial và bấm nút quét (hoặc nhập phím Enter), ViewModel thực thi Command bắt sự kiện quét:
  ```csharp
  [RelayCommand]
  private void AddSerialFromScanner(string serialNo)
  {
      if (string.IsNullOrWhiteSpace(serialNo)) return;
      var cleanSerial = serialNo.Trim();
      
      // Kiểm tra trùng lặp ngay trên giao diện trước khi lưu
      if (CurrentLineSerials.Contains(cleanSerial))
      {
          WarningMessage = "Số serial này đã có trong danh sách quét.";
          return;
      }
      
      CurrentLineSerials.Add(cleanSerial);
      UpdateLineQuantity();
  }
  ```
* **Điều khiển trạng thái giao diện theo Trạng thái chứng từ:**
  Khi một phiếu đang ở trạng thái `Posted` (Đã ghi sổ), toàn bộ các control nhập liệu, nút bấm "Lưu nháp", "Ghi sổ", "Xóa" trên giao diện WPF sẽ tự động chuyển sang trạng thái Disable (vô hiệu hóa) bằng cách liên kết thuộc tính `IsEnabled` với thuộc tính `IsEditable` trong ViewModel:
  ```csharp
  public bool IsEditable => Document?.Status == DocumentStatus.Draft;
  ```

---

## 4. Phân hệ Báo cáo Đa chiều (`ReportViewModel`)

Lớp [ReportViewModel.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/ViewModels/ReportViewModel.cs) điều khiển giao diện báo cáo bao gồm 4 tab độc lập:

### Tab 1: Doanh thu & Lợi nhuận (LiveCharts2 Integration)
* **Xử lý đồ thị:** Sử dụng thư viện **LiveCharts2** để vẽ biểu đồ đường biểu diễn sự biến động của Doanh thu và Chi phí theo thời gian.
* Các thuộc tính `RevenueExpenseSeries` (kiểu `ISeries[]`) và `RevenueExpenseXAxes` (kiểu `Axis[]`) được cấu hình màu sắc, kích thước điểm vẽ (`GeometrySize = 6`), và nhãn xoay nghiêng 15 độ để tránh chồng chéo chữ trên giao diện nhỏ.

### Tab 2: Báo cáo Xuất-Nhập-Tồn tổng hợp
* **Tính toán số liệu trong kỳ:** Với mỗi sản phẩm, hệ thống lọc Sổ kho (`StockLedger`) để chia thành 3 giai đoạn:
  * *Tồn đầu kỳ:* Tổng lượng nhập trừ lượng xuất trước ngày `FromDate`.
  * *Nhập/Xuất trong kỳ:* Phát sinh từ ngày `FromDate` đến ngày `ToDate`.
  * *Tồn cuối kỳ:* Bằng Tồn đầu kỳ + Nhập trong kỳ - Xuất trong kỳ.
* Số tiền tương ứng được tính toán bằng cách nhân số lượng với giá trị kho (Giá vốn trung bình hoặc giá bán mặc định).

### Tab 3: Sổ kho / Thẻ kho chi tiết
* **Thuật toán tính tồn lũy kế (Running Balance):**
  Khi lập báo cáo thẻ kho chi tiết cho một sản phẩm cụ thể, hệ thống lấy toàn bộ các dòng Ledger được sắp xếp theo thời gian tăng dần. Hệ thống xuất phát từ giá trị Tồn đầu kỳ (`LedgerStartQty`), sau đó lặp qua từng dòng phát sinh nhập/xuất để tính toán số lượng tồn cộng dồn tại đúng thời điểm đó:
  ```csharp
  decimal currentQty = LedgerStartQty;
  foreach (var ledger in currentLedgers)
  {
      decimal inQty = ledger.MovementType == "In" ? ledger.Quantity : 0;
      decimal outQty = ledger.MovementType == "Out" ? ledger.Quantity : 0;
      currentQty += (inQty - outQty);
      
      ledger.BalanceQty = currentQty; // Tồn lũy kế sau giao dịch
  }
  ```
  Kết quả được hiển thị ngược lại (thời gian mới nhất ở trên) để thủ kho dễ theo dõi.

### Tab 4: Truy vết Serial (Serial Lifecycle Trace)
* Người dùng chỉ cần nhập số Serial, hệ thống sẽ thực hiện truy vấn kết hợp (Join) nhiều bảng:
  `ProductSerial` $\rightarrow$ `StockIn` (biết ngày nhập, ai nhập, mua của nhà cung cấp nào) $\rightarrow$ `StockOut` (ngày xuất, ai bán, bán cho khách hàng nào, giá bao nhiêu) $\rightarrow$ `WarrantyCoverage` (trạng thái bảo hành hiện tại, ngày hết hạn bảo hành).
* **Kết quả:** Hiển thị toàn bộ lịch sử vòng đời của thiết bị một cách trực quan trên một dòng dữ liệu duy nhất.

---

## 5. Các bộ chuyển đổi dữ liệu WPF (`Converters/`)

Các file trong thư mục `Converters/` đóng vai trò vô cùng quan trọng giúp định dạng dữ liệu thô từ database thành các thành phần trực quan sinh động trên giao diện người dùng.

### A. Bộ chuyển đổi màu sắc trạng thái (`StatusToFgBrushConverter` & `StatusToBgBrushConverter`)
* **Nguyên lý hoạt động:** Ánh xạ các chuỗi trạng thái nghiệp vụ (không phân biệt chữ hoa thường) sang các tài nguyên màu sắc (Brush) tương ứng đã định nghĩa trong file giao diện hệ thống:
  * Trạng thái thành công/tốt (`Paid`, `Posted`, `Approved`, `Active`, `InStock`): Trả về màu xanh lá (`SuccessBgBrush` / `SuccessTextBrush`).
  * Trạng thái cảnh báo (`Partial`, `Open`, `Repaired`): Trả về màu cam/vàng (`WarningBgBrush` / `WarningTextBrush`).
  * Trạng thái nguy hiểm/lỗi (`Unpaid`, `Overdue`, `LowStock`, `Rejected`, `Expired`): Trả về màu đỏ (`DangerBgBrush` / `DangerTextBrush`).
  * Trạng thái nháp/tạm thời (`Draft`, `Locked`): Trả về màu xám/trung tính (`NeutralBgBrush` / `NeutralTextBrush`).
* **Sử dụng:** Giúp người dùng lướt qua danh sách phiếu có thể nhận biết ngay phiếu nào đã được duyệt (xanh), phiếu nào chưa thanh toán (đỏ), phiếu nào còn là nháp (xám).

### B. Bộ chuyển đổi nhãn thân thiện (`StatusToTextConverter`)
* Việt hóa toàn bộ các mã trạng thái kỹ thuật lưu trữ trong DB thành các nhãn tiếng Việt dễ hiểu trên giao diện (ví dụ: `manufacturerwait` $\rightarrow$ "Đang gửi hãng", `instock` $\rightarrow$ "Còn hàng", `lowstock` $\rightarrow$ "Sắp hết hàng").

### C. Các bộ chuyển đổi logic phụ trợ khác
* `BooleanToIconConverter`: Chuyển đổi trạng thái đúng/sai thành các biểu tượng Material Design (ví dụ: dấu tick xanh lá cho true, dấu nhân đỏ cho false).
* `CountedQuantityConverter`: Hiển thị chênh lệch kiểm kê (nếu chênh lệch bằng 0 hiển thị màu đen bình thường, nếu chênh lệch âm hiển thị màu đỏ kèm dấu trừ, chênh lệch dương hiển thị màu xanh kèm dấu cộng).
