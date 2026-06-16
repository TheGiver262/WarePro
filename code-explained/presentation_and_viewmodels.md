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
* **Cơ chế tích hợp máy quét mã vạch vật lý (Barcode Scanner Integration):**
  Trong mã nguồn thực tế, hệ thống không dùng một Command nhận diện máy quét riêng biệt mà tận dụng cơ chế giả lập bàn phím (**Keyboard Emulation**) của các máy quét mã vạch thông dụng:
  * Khi thủ kho nhấn nút nhập serial của dòng sản phẩm, hệ thống mở cửa sổ `SerialInputWindow` chứa ô nhập liệu `SerialTextBox` (cấu hình `AcceptsReturn="True"` để nhận phím Enter xuống dòng).
  * Khi đặt con trỏ vào ô này và bấm quét, máy quét sẽ tự động điền chuỗi ký tự số Serial và gửi kèm phím `Enter` ảo, làm con trỏ tự động xuống dòng tiếp theo để chờ quét mã kế tiếp.
  * Sự kiện `SerialTextBox_TextChanged` trong [SerialInputWindow.xaml.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Views/SerialInputWindow.xaml.cs#L156-L179) tự động kích hoạt hàm phân tích cú pháp dải số Serial `StockInService.ParseSerialRange()` để đếm và hiển thị số lượng serial đã nhận diện thời gian thực trên nhãn Preview.
  * **Cách kiểm thử mà không cần máy quét:** Người dùng chỉ cần mở cửa sổ nhập, gõ tay một số serial, nhấn phím `Enter` trên bàn phím máy tính để xuống dòng, rồi gõ tiếp serial thứ hai... Hành động gõ tay và nhấn Enter này giả lập chính xác 100% hành vi của máy quét mã vạch vật lý đối với ô nhập liệu.
* **Điều khiển trạng thái giao diện theo Trạng thái chứng từ:**
  Khi một phiếu đang ở trạng thái `Posted` (Đã ghi sổ) hoặc đang ở chế độ chỉ xem (`IsViewMode`), toàn bộ các control nhập liệu, nút bấm "Lưu", "Ghi sổ", "Xóa" trên giao diện WPF sẽ tự động chuyển sang trạng thái Disable (vô hiệu hóa) bằng cách liên kết thuộc tính `IsEnabled` với thuộc tính `CanEdit` trong ViewModel:
  ```csharp
  public bool CanEdit => !IsPosted && !IsViewMode;
  ```

---

## 3A. Giao diện Hóa đơn Mua/Bán (`PurchaseInvoiceViewModel` & `SalesInvoiceViewModel`)

Các lớp này quản lý trạng thái hiển thị, tìm kiếm và thanh toán của hóa đơn mua hàng từ nhà cung cấp và hóa đơn bán hàng cho khách hàng.

* **Cơ chế chống Race Condition khi làm mới bộ lọc (Filter Reset):**
  Trong ViewModels hóa đơn, các thuộc tính lọc (như `SearchInvoiceCode`, `SearchSupplierName`, `SelectedFilterPaymentStatus`...) đều kích hoạt phương thức nạp dữ liệu `LoadData()` tự động mỗi khi giá trị thay đổi (`OnSearchInvoiceCodeChanged`...). 
  Khi người dùng bấm nút "Làm mới", hệ thống reset đồng loạt tất cả các tham số này về trạng thái trống. Để tránh việc kích hoạt nhiều luồng `LoadData()` bất đồng bộ chạy song song gây ra tranh chấp tài nguyên (Race Condition), hệ thống sử dụng cờ hiệu `_isInitialized`:
  ```csharp
  [RelayCommand]
  private void Refresh()
  {
      _isInitialized = false; // Tạm ngắt cơ chế nạp tự động
      SearchInvoiceCode = string.Empty;
      SearchSupplierName = string.Empty;
      FilterStartDate = null;
      FilterEndDate = null;
      SelectedFilterPaymentStatus = "Tất cả";
      FilterLinkDocCode = string.Empty;
      FilterMinTotal = null;
      FilterMaxTotal = null;
      _isInitialized = true;  // Bật lại cơ chế

      LoadData(); // Chỉ thực thi tải dữ liệu duy nhất một lần
  }
  ```
  Nhờ cờ này, việc reset hàng loạt thuộc tính diễn ra đồng bộ trên UI mà không tạo ra các truy vấn SQL dư thừa lên database.

---

## 3B. Thiết kế Layout DataGrid & Khắc phục lệch cột (UI Layout Stability)

Để đảm bảo độ ổn định của giao diện người dùng (WPF DataGrid) khi làm mới dữ liệu hoặc khi danh sách tạm thời trống:
* **Vấn đề:** Việc thiết lập độ rộng cột là `Width="Auto"` kết hợp với cơ chế ảo hóa dòng (`EnableRowVirtualization="True"`) của WPF sẽ khiến DataGrid liên tục tính toán lại độ rộng cột khi dữ liệu rỗng hoặc khi thêm từng dòng dữ liệu mới. Điều này dẫn đến hiện tượng giật màn hình hoặc lệch hàng giữa tiêu đề cột (Header) và ô dữ liệu (Cells).
* **Giải pháp:** Cố định độ rộng (Width) cho các cột chính trong DataGrid trên toàn bộ hệ thống:
  * *Hóa đơn Mua/Bán (`PurchaseInvoiceView` & `SalesInvoiceView`):* Số HĐ (`140`), Ngày (`120`), Trước thuế (`120`), Thuế (`100`), Tổng tiền (`130`), Trạng thái (`130`), Thao tác (`130`), còn lại cột Tên đối tác co giãn tỷ lệ (`*`).
  * *Phiếu điều chỉnh (`AdjustmentView`):* Cố định cột Thời gian (`150`).
  * *Hồ sơ bảo hành (`WarrantyClaimView`):* Cố định Mã phiếu (`120`), Ngày nhận (`140`), Trạng thái (`120`).
* **Kết quả:** Layout DataGrid được cố định chắc chắn, không bị co cụm về kích thước tiêu đề khi danh sách trống, giải quyết triệt để lỗi lệch cột.

---

## 3C. Tối ưu hóa truy vấn CSDL đếm số lượng (Database Counts Optimization)

Nhằm giảm tải kết nối cơ sở dữ liệu và cải thiện tốc độ phản hồi của giao diện:
* **Vấn đề cũ:** Tại các màn hình danh sách sản phẩm hoặc danh sách số Serial, để hiển thị bộ đếm số lượng theo trạng thái ở các Tab (ví dụ: Tất cả, Hoạt động, Ngừng hoạt động; hoặc InStock, Sold, Scrapped), hệ thống thực hiện gọi nhiều lệnh truy vấn `.Count()` liên tiếp lên Entity Framework Core. Điều này sinh ra 3-4 câu lệnh SQL `SELECT COUNT(*)` riêng biệt gửi tới SQL Server.
* **Giải pháp tối ưu:** Sử dụng gom nhóm `GroupBy` trong một câu truy vấn LINQ duy nhất để lấy toàn bộ số lượng theo trạng thái, sau đó ánh xạ vào bộ đếm:
  ```csharp
  // Ví dụ trong ProductViewModel.cs
  var counts = await db.Products
      .GroupBy(p => p.IsActive)
      .Select(g => new { IsActive = g.Key, Count = g.Count() })
      .ToListAsync();

  ActiveCount = counts.FirstOrDefault(c => c.IsActive)?.Count ?? 0;
  InactiveCount = counts.FirstOrDefault(c => !c.IsActive)?.Count ?? 0;
  TotalCount = ActiveCount + InactiveCount;
  ```
* **Ý nghĩa:** Tiết kiệm đến 66% - 75% số lượng kết nối Database (từ 3-4 query giảm xuống còn 1 query duy nhất), tối ưu hóa đáng kể tốc độ tải ban đầu của View.

---

## 4. Phân hệ Báo cáo Đa chiều (`ReportViewModel`)

Lớp [ReportViewModel.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/ViewModels/ReportViewModel.cs) điều khiển giao diện báo cáo bao gồm 4 tab độc lập:

### Tab 1: Doanh thu & Lợi nhuận (LiveCharts2 Integration)
* **Xử lý đồ thị:** Sử dụng thư viện **LiveCharts2** để vẽ biểu đồ đường biểu diễn sự biến động của Doanh thu và Chi phí theo thời gian.
* Các thuộc tính `RevenueExpenseSeries` (kiểu `ISeries[]`) và `RevenueExpenseXAxes` (kiểu `Axis[]`) được cấu hình màu sắc, kích thước điểm vẽ (`GeometrySize = 6`), và nhãn xoay nghiêng 15 độ để tránh chồng chéo chữ trên giao diện nhỏ.

### Tab 2: Báo cáo Xuất-Nhập-Tồn tổng hợp
* **Đầy đủ dữ liệu sản phẩm:** Báo cáo Xuất-Nhập-Tồn được thiết kế để không bỏ sót các sản phẩm đã ngừng hoạt động (Inactive) nhưng vẫn còn số dư tồn kho hoặc có phát sinh giao dịch nhập xuất trong kỳ báo cáo (loại bỏ lọc cứng `IsActive = true` trong truy vấn sản phẩm).
* **Tính toán số liệu trong kỳ:** Với mỗi sản phẩm, hệ thống lọc Sổ kho (`StockLedger`) để chia thành 3 giai đoạn:
  * *Tồn đầu kỳ:* Tổng lượng nhập trừ lượng xuất trước ngày `FromDate`.
  * *Nhập/Xuất trong kỳ:* Phát sinh từ ngày `FromDate` đến ngày `ToDate`.
  * *Tồn cuối kỳ:* Bằng Tồn đầu kỳ + Nhập trong kỳ - Xuất trong kỳ.
* **Bộ lọc và Tự động làm mới:** 
  * Cung cấp tùy chọn lọc `"Tất cả danh mục"` (bằng cách chèn bản ghi giả với `Id = 0` vào đầu danh sách) giúp người dùng dễ dàng xem lại toàn bộ sản phẩm của mọi danh mục.
  * Tự động kích hoạt tải lại báo cáo ngay khi người dùng thay đổi lựa chọn danh mục (`SelectedCategory`) hoặc nhập ô tìm kiếm tên sản phẩm (`SearchProductText`) thông qua các phương thức lắng nghe thay đổi của CommunityToolkit.Mvvm, mang lại trải nghiệm mượt mà và trực quan.
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
