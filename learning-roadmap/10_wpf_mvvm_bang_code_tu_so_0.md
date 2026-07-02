# WPF/MVVM bằng code từ số 0

File này giúp bạn hiểu một màn hình WPF được tạo ra thế nào, binding hoạt động ra sao, và vì sao dự án dùng ViewModel.

## 1. WPF app có những file nào?

Một app WPF tối thiểu thường có:

```text
App.xaml
App.xaml.cs
MainWindow.xaml
MainWindow.xaml.cs
```

Ý nghĩa:

- `App.xaml`: khai báo tài nguyên chung và startup.
- `App.xaml.cs`: code phía sau app.
- `MainWindow.xaml`: giao diện cửa sổ chính.
- `MainWindow.xaml.cs`: code phía sau cửa sổ.

Trong dự án thật, ngoài `MainWindow` còn có nhiều `UserControl` trong `Views`.

## 2. XAML tối giản

```xml
<Window x:Class="WarehouseMini.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Warehouse Mini"
        Width="800"
        Height="500">
    <Grid>
        <TextBlock Text="Xin chao WPF"
                   FontSize="24"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center" />
    </Grid>
</Window>
```

Giải thích:

- `Window`: cửa sổ.
- `Grid`: vùng layout.
- `TextBlock`: hiển thị text.
- `HorizontalAlignment`, `VerticalAlignment`: căn vị trí.

## 3. Code-behind tối giản

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

`InitializeComponent()` đọc file XAML và dựng UI. Hầu như mọi file `.xaml.cs` đều có dòng này.

## 4. Binding đầu tiên

Thay vì viết text cứng:

```xml
<TextBlock Text="Xin chao WPF" />
```

Ta binding:

```xml
<TextBlock Text="{Binding Title}" />
```

Binding sẽ tìm property `Title` trong `DataContext`.

ViewModel:

```csharp
public class MainViewModel
{
    public string Title { get; set; } = "Quan ly kho mini";
}
```

Gán DataContext:

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
```

Bây giờ XAML sẽ hiển thị `Quan ly kho mini`.

## 5. Vì sao cần ObservableObject?

Nếu `Title` đổi sau khi UI đã hiển thị, UI cần được thông báo.

ViewModel dùng Toolkit:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Quan ly kho mini";
}
```

Toolkit sinh property `Title`. Khi gán:

```csharp
Title = "Tieu de moi";
```

UI tự cập nhật.

## 6. Button và Command

XAML:

```xml
<StackPanel>
    <TextBlock Text="{Binding Title}" FontSize="24" />
    <Button Content="Doi tieu de"
            Command="{Binding ChangeTitleCommand}" />
</StackPanel>
```

ViewModel:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Quan ly kho mini";

    [RelayCommand]
    private void ChangeTitle()
    {
        Title = "Da doi tieu de";
    }
}
```

Bạn viết method `ChangeTitle`, Toolkit sinh `ChangeTitleCommand`.

## 7. TextBox binding hai chiều

XAML:

```xml
<TextBox Text="{Binding ProductName, UpdateSourceTrigger=PropertyChanged}" />
<TextBlock Text="{Binding ProductName}" />
```

ViewModel:

```csharp
[ObservableProperty]
private string _productName = "";
```

Khi gõ vào TextBox, TextBlock đổi theo.

`UpdateSourceTrigger=PropertyChanged` nghĩa là cập nhật ViewModel ngay khi user gõ, không chờ mất focus.

## 8. DataGrid hiển thị danh sách

Model:

```csharp
public class Product
{
    public string ProductCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public decimal DefaultPrice { get; set; }
}
```

ViewModel:

```csharp
public partial class ProductViewModel : ObservableObject
{
    public ObservableCollection<Product> Products { get; } = new();

    public ProductViewModel()
    {
        Products.Add(new Product
        {
            ProductCode = "SP001",
            DisplayName = "Laptop",
            DefaultPrice = 15000000m
        });
    }
}
```

XAML:

```xml
<DataGrid ItemsSource="{Binding Products}"
          AutoGenerateColumns="False">
    <DataGrid.Columns>
        <DataGridTextColumn Header="Ma" Binding="{Binding ProductCode}" />
        <DataGridTextColumn Header="Ten" Binding="{Binding DisplayName}" />
        <DataGridTextColumn Header="Gia" Binding="{Binding DefaultPrice}" />
    </DataGrid.Columns>
</DataGrid>
```

`ItemsSource` là danh sách cần hiển thị.

## 9. SelectedItem

XAML:

```xml
<DataGrid ItemsSource="{Binding Products}"
          SelectedItem="{Binding SelectedProduct}" />
```

ViewModel:

```csharp
[ObservableProperty]
private Product? _selectedProduct;
```

Khi user chọn một dòng, `SelectedProduct` trong ViewModel nhận object tương ứng.

## 10. Màn hình CRUD mini bằng ViewModel

ViewModel:

```csharp
public partial class ProductViewModel : ObservableObject
{
    public ObservableCollection<Product> Products { get; } = new();

    [ObservableProperty]
    private string _productCode = "";

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private decimal _defaultPrice;

    [RelayCommand]
    private void AddProduct()
    {
        if (string.IsNullOrWhiteSpace(ProductCode))
        {
            MessageBox.Show("Ma san pham la bat buoc");
            return;
        }

        Products.Add(new Product
        {
            ProductCode = ProductCode,
            DisplayName = DisplayName,
            DefaultPrice = DefaultPrice
        });

        ProductCode = "";
        DisplayName = "";
        DefaultPrice = 0;
    }
}
```

XAML rút gọn:

```xml
<Grid Margin="16">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
    </Grid.RowDefinitions>

    <StackPanel Orientation="Horizontal" Margin="0,0,0,12">
        <TextBox Width="120" Text="{Binding ProductCode}" />
        <TextBox Width="220" Text="{Binding DisplayName}" />
        <TextBox Width="120" Text="{Binding DefaultPrice}" />
        <Button Content="Them" Command="{Binding AddProductCommand}" />
    </StackPanel>

    <DataGrid Grid.Row="1"
              ItemsSource="{Binding Products}"
              AutoGenerateColumns="False">
        <DataGrid.Columns>
            <DataGridTextColumn Header="Ma" Binding="{Binding ProductCode}" />
            <DataGridTextColumn Header="Ten" Binding="{Binding DisplayName}" />
            <DataGridTextColumn Header="Gia" Binding="{Binding DefaultPrice}" />
        </DataGrid.Columns>
    </DataGrid>
</Grid>
```

Đây là bản mini của pattern mà các màn hình CRUD trong dự án đang dùng.

## 11. Tách Service ra khỏi ViewModel

Không nên để toàn bộ nghiệp vụ trong ViewModel. Tách service:

```csharp
public class ProductService
{
    private readonly List<Product> _products = new();

    public List<Product> GetAll()
    {
        return _products.ToList();
    }

    public void Create(Product product)
    {
        if (_products.Any(p => p.ProductCode == product.ProductCode))
        {
            throw new InvalidOperationException("Ma san pham da ton tai.");
        }

        _products.Add(product);
    }
}
```

ViewModel gọi service:

```csharp
public partial class ProductViewModel : ObservableObject
{
    private readonly ProductService _service;

    public ObservableCollection<Product> Products { get; } = new();

    public ProductViewModel(ProductService service)
    {
        _service = service;
        LoadData();
    }

    private void LoadData()
    {
        Products.Clear();
        foreach (var product in _service.GetAll())
        {
            Products.Add(product);
        }
    }
}
```

Đây là tư duy của dự án thật: ViewModel điều phối UI, Service xử lý nghiệp vụ.

## 12. Điều hướng bằng CurrentView

Shell ViewModel tối giản:

```csharp
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private UserControl? _currentView;

    [ObservableProperty]
    private string _currentViewTitle = "Dashboard";

    [RelayCommand]
    private void OpenProductView()
    {
        CurrentView = new ProductView
        {
            DataContext = new ProductViewModel(new ProductService())
        };
        CurrentViewTitle = "San pham";
    }
}
```

MainWindow XAML:

```xml
<DockPanel>
    <StackPanel DockPanel.Dock="Left" Width="180">
        <Button Content="San pham"
                Command="{Binding OpenProductViewCommand}" />
    </StackPanel>

    <ContentControl Content="{Binding CurrentView}" />
</DockPanel>
```

Dự án thật dùng ý tưởng này trong `MainViewModel`, có thêm `_viewCache` để không tạo lại View nhiều lần.

## 13. Lỗi binding thường gặp

1. Sai tên property: `Productname` trong khi ViewModel là `ProductName`.
2. Chưa gán DataContext.
3. Quên kế thừa `ObservableObject`, UI không cập nhật khi property đổi.
4. Quên `[RelayCommand]`, XAML không tìm thấy `SaveCommand`.
5. Binding vào field private thay vì property public.

## 14. Bài tập

Tự tạo màn hình `WarehouseView` mini:

```csharp
public class Warehouse
{
    public string WarehouseCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
}
```

Yêu cầu UI:

- TextBox nhập mã kho.
- TextBox nhập tên kho.
- Button thêm.
- DataGrid hiển thị danh sách kho.
- Khi thêm xong, clear form.

Yêu cầu kiến trúc:

- Có `WarehouseViewModel`.
- Có `[ObservableProperty]`.
- Có `[RelayCommand]`.
- Không xử lý thêm kho trong code-behind.
