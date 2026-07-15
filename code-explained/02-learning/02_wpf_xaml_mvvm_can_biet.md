# WPF, XAML và MVVM cần biết

Dự án dùng WPF để làm ứng dụng desktop Windows. WPF chia giao diện và logic thành hai phần chính:

- XAML: mô tả giao diện.
- C#: xử lý logic phía sau.

Dự án áp dụng MVVM để View không chứa nghiệp vụ.

## 1. WPF là gì?

WPF là framework làm giao diện desktop của .NET. Nó mạnh ở:

- Data binding.
- Template và style.
- Layout linh hoạt.
- Tách UI khỏi logic bằng MVVM.

Trong dự án:

- `MainWindow.xaml`: cửa sổ chính.
- `Views/*.xaml`: các màn hình như sản phẩm, nhập kho, bảo hành.
- `Themes/*.xaml`: style dùng chung.
- `ViewModels/*.cs`: trạng thái và command cho UI.

## 2. XAML là gì?

XAML là ngôn ngữ markup để khai báo giao diện.

Ví dụ tư duy:

```xml
<Button Content="Lưu" Command="{Binding SaveCommand}" />
```

Nghĩa là nút hiển thị chữ "Lưu", khi bấm sẽ gọi `SaveCommand` trong ViewModel.

Bạn đọc XAML như đọc cấu trúc màn hình:

- `Grid`: chia vùng.
- `StackPanel`: xếp dọc hoặc ngang.
- `TextBox`: ô nhập.
- `ComboBox`: chọn dữ liệu.
- `DataGrid`: bảng.
- `Button`: nút thao tác.

## 3. MVVM là gì?

MVVM gồm 3 phần:

- Model: dữ liệu, ví dụ `Product`, `StockIn`, `Customer`.
- View: giao diện XAML, ví dụ `ProductView.xaml`.
- ViewModel: trạng thái và hành động của View, ví dụ `ProductViewModel.cs`.

Luồng chuẩn:

```text
User bấm nút
-> View gọi Command
-> ViewModel xử lý
-> ViewModel gọi Service
-> Service đọc/ghi database
-> ViewModel cập nhật property
-> View tự cập nhật qua Binding
```

## 4. DataContext

`DataContext` là object mà View sẽ binding vào.

Ví dụ trong `MainWindow.xaml.cs`:

```csharp
this.DataContext = new MainViewModel(user, contextFactory);
```

Từ đó, trong XAML của `MainWindow`, `{Binding CurrentView}` sẽ tìm property `CurrentView` trong `MainViewModel`.

Khi một View không binding được, hãy kiểm tra đầu tiên: DataContext là gì?

## 5. Binding

Binding nối UI với property trong ViewModel.

Ví dụ:

```xml
<TextBox Text="{Binding SearchKeyword}" />
```

Khi user nhập text, `SearchKeyword` trong ViewModel thay đổi. Khi ViewModel đổi `SearchKeyword`, UI cũng cập nhật.

Các binding hay gặp:

- `Text="{Binding Name}"`
- `ItemsSource="{Binding Products}"`
- `SelectedItem="{Binding SelectedProduct}"`
- `Command="{Binding SaveCommand}"`
- `Visibility="{Binding IsLoading, Converter=...}"`

## 6. ObservableObject

ViewModel kế thừa `ObservableObject` để báo cho UI biết property đã đổi.

```csharp
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentViewTitle = "DASHBOARD";
}
```

Toolkit sinh ra:

```csharp
public string CurrentViewTitle { get; set; }
```

và tự gọi `OnPropertyChanged`.

## 7. RelayCommand

Thay vì viết event click trong code-behind, dự án dùng command.

```csharp
[RelayCommand]
private void OpenStockInView()
{
    NavigateToView("StockIn", () => new StockInView
    {
        DataContext = new StockInViewModel(CurrentUser, ContextFactory)
    }, "NHẬP KHO", "Lập phiếu nhập kho và quản lý hàng nhập");
}
```

Toolkit sinh ra `OpenStockInViewCommand`. XAML chỉ cần binding vào command đó.

## 8. Code-behind nên làm gì?

File `.xaml.cs` gọi là code-behind. Trong MVVM tốt, code-behind nên rất mỏng.

Nên đặt ở code-behind:

- Khởi tạo component.
- Gán DataContext.
- Xử lý thao tác thuần UI khó binding, ví dụ scroll, focus, animation.

Không nên đặt ở code-behind:

- Tính tồn kho.
- Ghi database.
- Validate nghiệp vụ.
- Tạo hóa đơn.

## 9. Điều hướng màn hình trong dự án

`MainViewModel` có property:

```csharp
private UserControl? _currentView;
```

Khi user chọn menu, command sẽ gọi `NavigateToView`. Hàm này:

1. Kiểm tra view đã có trong `_viewCache` chưa.
2. Nếu chưa có thì tạo view và viewmodel.
3. Nếu có rồi thì refresh nếu viewmodel hỗ trợ `IRefreshable`.
4. Gán `CurrentView`.
5. Cập nhật title và subtitle.

Đây là pattern quan trọng để bạn tự thêm màn hình mới.

## 10. Tự thêm màn hình mới

Ví dụ muốn thêm màn hình `WarehouseView`.

Các bước:

1. Tạo model nếu chưa có: `Warehouse`.
2. Tạo service: `WarehouseService`.
3. Tạo viewmodel: `WarehouseViewModel`.
4. Tạo view: `WarehouseView.xaml`.
5. Trong `MainViewModel`, thêm command `OpenWarehouseView`.
6. Trong menu XAML, thêm button binding đến `OpenWarehouseViewCommand`.

## 11. Converters

Converter chuyển dữ liệu từ ViewModel sang dạng UI cần.

Ví dụ:

- `NullToVisibilityConverter`: null thì ẩn/hiện.
- `InverseBooleanConverter`: đảo true/false.
- `StatusToBrushConverter`: trạng thái thành màu.

Khi đọc XAML gặp `Converter=...`, hiểu rằng dữ liệu không hiển thị trực tiếp mà được biến đổi trước.

## 12. Themes và style

Thư mục `QuanLyHangHoa/Themes` chứa style chung:

- `Colors.xaml`
- `Buttons.xaml`
- `DataGridStyles.xaml`
- `Inputs.xaml`
- `Typography.xaml`
- `Navigation.xaml`

Khi sửa UI, ưu tiên dùng style có sẵn. Dự án có chuẩn giao diện riêng: Pro Max, glassmorphism, màu HSL tự thiết kế, không dùng tím/violet chuẩn.

## 13. Bài tập WPF/MVVM

1. Mở `MainWindow.xaml.cs`, giải thích vì sao constructor cần `AppUser` và `contextFactory`.
2. Mở `MainViewModel.cs`, tìm 5 command điều hướng.
3. Chọn một View bất kỳ, tìm `DataContext` của nó được tạo ở đâu.
4. Trong một file XAML, tìm 5 binding và chỉ ra property/command tương ứng trong ViewModel.
5. Tự tạo một ViewModel nhỏ có `SearchText`, `Items`, `SearchCommand`.

## 14. Mốc đạt yêu cầu

Bạn hiểu WPF/MVVM của dự án khi có thể:

- Biết một button trong XAML gọi command nào.
- Biết UI lấy dữ liệu từ ViewModel nào.
- Biết ViewModel gọi Service nào.
- Tự thêm một property binding lên UI.
- Tự thêm một command cho nút mới.
