# WarePro — Toàn cảnh dự án từ nghiệp vụ đến mã nguồn

Tài liệu này là điểm bắt đầu để hiểu WarePro như một hệ thống hoàn chỉnh. Nó mô tả code đang chạy, các quy tắc nghiệp vụ quan trọng và lý do đằng sau kiến trúc; không thay thế tài liệu giải thích từng file trong `code-explained/`.

## 1. WarePro giải quyết bài toán gì?

WarePro là ứng dụng desktop nội bộ dành cho doanh nghiệp bán hàng có kho và dịch vụ bảo hành. Hệ thống quản lý:

- danh mục người dùng, sản phẩm, loại hàng, thương hiệu, đơn vị, nhà cung cấp, khách hàng và kho;
- nhập, xuất, chuyển, kiểm kê, điều chỉnh, đảo nghiệp vụ và tồn đầu kỳ;
- hàng quản lý theo số lượng và hàng quản lý đến từng serial;
- hóa đơn mua, hóa đơn bán, thanh toán và hạn thanh toán;
- quyền bảo hành phát sinh từ bán hàng và các lần tiếp nhận bảo hành;
- dashboard, báo cáo, truy vết serial, in chứng từ và nhật ký audit.

Điểm quan trọng nhất: WarePro không phải một tập màn hình CRUD độc lập. Các màn hình cùng biểu diễn một sự thật nghiệp vụ. Một lần xuất bán có thể đồng thời ảnh hưởng chứng từ xuất, tồn kho, trạng thái serial, sổ kho, hóa đơn, quyền bảo hành và audit log.

## 2. Công nghệ và lý do lựa chọn

| Thành phần | Công nghệ | Vai trò |
|---|---|---|
| Ngôn ngữ | C# trên .NET 8 | Kiểu dữ liệu chặt, tooling tốt, phù hợp ứng dụng nghiệp vụ Windows |
| Giao diện | WPF và XAML | Desktop Windows, data binding, style/resource dùng chung |
| Tổ chức presentation | MVVM với CommunityToolkit.Mvvm | Tách trạng thái/command khỏi visual tree, giảm code-behind |
| Dữ liệu | EF Core 8 và SQL Server | Mapping quan hệ, migration, transaction và constraint |
| UI library | MaterialDesignThemes/Colors | Control, icon và theme nền |
| Báo cáo biểu đồ | LiveChartsCore | Dashboard và trực quan hóa |
| Import/export | ClosedXML, CsvHelper | Excel và CSV |
| Mật khẩu | BCrypt.Net-Next | Lưu hash thay cho mật khẩu thô |
| Test | xUnit, Moq, SQLite relational | Unit/contract/integration test không phụ thuộc DB dev |

Tên sản phẩm là **WarePro**. `AssemblyName` cũng là `WarePro`, nhưng root namespace và tên project kỹ thuật vẫn là `QuanLyHangHoa` để tránh đổi namespace hàng loạt không mang lại giá trị nghiệp vụ.

## 3. Kiến trúc thực tế

```text
Views (XAML)
    ↓ binding, event UI
ViewModels (state, command, validation, orchestration)
    ↓ gọi use case
Services + Inventory domain
    ↓ transaction, policy, posting
AppDbContext / EF Core
    ↓
SQL Server
```

### 3.1 Trách nhiệm từng tầng

- `Views/`: bố cục, binding, dialog và hành vi thuần giao diện. Code-behind chỉ xử lý việc gắn với WPF như scroll, focus, mở cửa sổ.
- `ViewModels/`: trạng thái màn hình, command, lọc/tìm, validation nhập liệu và điều phối use case. Không được là nơi duy nhất bảo vệ quy tắc nghiệp vụ.
- `Services/`: ranh giới nghiệp vụ ứng dụng, authorization, transaction, truy vấn, import, hóa đơn, bảo hành, report và audit.
- `Inventory/`: lõi ghi sổ kho, vòng đời chứng từ, policy tồn kho, port và unit of work.
- `Data/AppDbContext.cs`: mapping, quan hệ, index, unique key và check constraint.
- `Models/`: entity và các giá trị trạng thái chuẩn.
- `Migrations/`: lịch sử tiến hóa schema và tương thích dữ liệu cũ.
- `Themes/`, `Converters/`, `Helpers/`: design token, style và tiện ích presentation dùng chung.
- `QuanLyHangHoa.Tests/`: test nghiệp vụ, relational test, ViewModel test, XAML contract và real-database smoke tách biệt.

### 3.2 Vì sao vừa có `Services` vừa có `Inventory`?

`Services` trả lời “use case này cần làm gì”; `Inventory` trả lời “một biến động kho hợp lệ được ghi như thế nào”. Nhờ đó nhập kho, xuất kho, điều chỉnh, chuyển kho, kiểm kê và đảo nghiệp vụ dùng chung quy tắc tồn thay vì mỗi màn hình tự cập nhật số lượng.

Đây là một kiến trúc phân lớp thực dụng, chưa phải Clean Architecture tuyệt đối. EF Core vẫn xuất hiện trong service và object graph chưa được tách thành nhiều project. Với quy mô hiện tại, cách này cân bằng tốt giữa tính rõ ràng và chi phí bảo trì.

## 4. Luồng khởi động, đăng nhập và điều hướng

1. `App.xaml.cs` khởi tạo crash logging và điều phối SQL credential lần đầu nếu cấu hình dùng SQL Authentication.
2. `StartupCoordinator` đọc runtime settings, tạo connection string, probe SQL rồi gọi `DatabaseInitializer` trên worker.
3. `DatabaseInitializer` kiểm tra compatibility, lấy `SchemaUpgradeLock`, tạo verified backup khi cần, cập nhật schema idempotent và seed theo policy.
4. Startup thành công mới mở `LoginView`; lỗi được map thành mã ổn định và log đã che secret.
5. `LoginViewModel` gọi `AuthenticationService` để xác thực.
6. Đăng nhập thành công tạo `MainWindow` và `MainViewModel` với `AppUser` thật.
7. `MainViewModel` kiểm tra `AuthorizationService` trước khi mở module.
8. Mỗi view được tạo lần đầu rồi cache; khi quay lại, ViewModel có dữ liệu phải thực thi `IRefreshable.RefreshData()`.
9. Service nhận `Func<AppDbContext>` và tạo context ngắn hạn cho từng thao tác.
10. `CrashLogger` thu lỗi từ WPF Dispatcher, AppDomain và TaskScheduler.

Không dùng user giả, id mặc định hoặc “admin fallback”. Thiếu identity phải thất bại đóng (`fail closed`), vì fallback biến lỗi lập trình thành lỗ hổng quyền.

Phần cài đặt và cập nhật là một biên kiến trúc riêng gồm `Configuration`, `Startup`, `Updates`, `WarePro.SetupHelper`, Inno Setup và release scripts. Xem [chương cài đặt, cập nhật và phát hành](../03-code/14_cai_dat_cap_nhat_phat_hanh_de_hieu.md).

## 5. Mô hình dữ liệu cốt lõi

### 5.1 Dữ liệu nền

- `Product` thuộc `Category`, `Brand` và có đơn vị cơ sở.
- `ProductUnit` mô tả đơn vị thay thế và `ConversionFactor`; hệ số phải lớn hơn 0.
- `Supplier`, `Customer`, `Warehouse`, `AppUser` là các đối tượng tham chiếu của giao dịch.
- Mã nghiệp vụ quan trọng có unique index để ngăn trùng ở tầng DB, không chỉ ở UI.

### 5.2 Ba biểu diễn của tồn kho

| Mô hình | Ý nghĩa | Có phải nguồn chuẩn? |
|---|---|---|
| `StockBalance` | Tồn hiện tại theo `Warehouse + Product` | Có, cho số dư hiện tại |
| `ProductSerial` | Danh tính và trạng thái từng thiết bị | Có, cho hàng serial |
| `StockLedger` | Lịch sử mọi biến động và nguồn chứng từ | Có, cho truy vết lịch sử |

`Product.Quantity` không được dùng làm nguồn chuẩn cho tồn vật lý. Nếu còn trường này, nó chỉ nên là cache/hiển thị và phải được suy ra từ nguồn chuẩn.

### 5.3 Chứng từ kho và hóa đơn là hai khái niệm khác nhau

- `StockIn`, `StockOut`, `StockTransfer`, `StockAdjustment`, `StockCountSession` thay đổi hoặc xác nhận tồn vật lý.
- `PurchaseInvoice`, `SalesInvoice` ghi nhận giá trị thương mại, thuế, thanh toán và công nợ.
- Hai bên có thể liên kết nhưng không được tự do mâu thuẫn. Hóa đơn liên kết phải khớp đối tác, dòng hàng, đơn vị và số lượng cơ sở với chứng từ đã ghi sổ.
- Tồn đầu kỳ là `StockIn` có purpose `OpeningBalance`, không mặc định là mua hàng.
- Xuất đổi bảo hành là `StockOut` có purpose `WarrantyReplacement`, không mặc định là bán hàng.

### 5.4 Bảo hành

- `WarrantyCoverage`: quyền bảo hành của một serial, gồm khách hàng, thời hạn và trạng thái hiệu lực.
- `WarrantyClaim`: một lần tiếp nhận/xử lý cụ thể dựa trên quyền bảo hành.
- Trạng thái claim được điều khiển bằng state machine; không cho nhảy trạng thái tùy ý.
- Đổi serial là thao tác một lần (`idempotent`): gọi lại không được trừ kho hoặc tạo quyền bảo hành lần hai.

## 6. Các bất biến nghiệp vụ phải luôn đúng

### 6.1 Tồn kho

1. Mọi số lượng nghiệp vụ được quy đổi sang `BaseQuantity` kiểu `decimal` trước khi ghi sổ.
2. `StockBalance` không âm, trừ khi có chính sách rõ ràng cho phép.
3. Số lượng serial đã chọn phải bằng số lượng cơ sở của dòng hàng quản lý serial.
4. Serial nhập phải duy nhất; serial xuất phải đang `InStock` tại đúng kho.
5. Một nghiệp vụ thành công phải cập nhật đồng bộ balance, serial, ledger, chứng từ và audit.
6. Một nghiệp vụ thất bại không được để lại cập nhật một phần.
7. Đảo nghiệp vụ chỉ chạy một lần và phải ghi bút toán ngược, không xóa lịch sử gốc.
8. Cập nhật cạnh tranh phải được phát hiện; không âm thầm ghi đè balance mới hơn.

### 6.2 Vòng đời chứng từ

```text
Draft → Approved → Posted
```

Mỗi chuyển trạng thái phải kiểm tra cả trạng thái hiện tại và quyền hành động. ViewModel ẩn/khóa nút để UX tốt; service vẫn phải guard vì service có thể được gọi từ import, test hoặc màn hình khác.

### 6.3 Dữ liệu nền

- Bản ghi đã được tham chiếu thường phải ngừng hoạt động thay vì hard delete.
- Trước khi xóa/ngừng hoạt động phải trả về dependency có ý nghĩa để UI giải thích cho người dùng.
- Thay đổi entity và ghi audit phải cùng transaction.
- Luôn còn ít nhất một quản trị viên hoạt động; không cho tự hạ quyền hoặc tự vô hiệu hóa nếu làm mất invariant này.

## 7. Các luồng nghiệp vụ quan trọng

### 7.1 Nhập kho có serial

1. Người dùng tạo phiếu và các dòng hàng.
2. Serial nháp được giữ ở `DraftSerials`, tránh để EF theo dõi entity serial chưa hợp lệ.
3. Khi ghi sổ, service kiểm tra trạng thái/quyền, quy đổi đơn vị và validate toàn bộ serial.
4. Transaction cập nhật chứng từ, `StockBalance`, tạo `ProductSerial`, ghi `StockLedger` và audit.
5. Chỉ commit khi tất cả bước thành công.

### 7.2 Xuất kho có serial

1. UI chỉ cho chọn serial `InStock` của đúng sản phẩm và kho.
2. Sau khi xác nhận dialog, số lượng dòng được suy ra/đồng bộ từ số serial đã chọn; không reset về 0 khi property thay đổi theo thứ tự khác nhau.
3. Service kiểm tra lại vì dữ liệu có thể đổi từ lúc màn hình tải.
4. Transaction trừ balance, đổi trạng thái serial, ghi ledger, chứng từ và audit.

### 7.3 Bán hàng, hóa đơn và bảo hành

1. Chứng từ xuất bán được ghi sổ trước hoặc được liên kết theo quy tắc đã định.
2. Hóa đơn lấy dòng hàng chuẩn từ chứng từ liên kết thay vì cho nhập một bản sao độc lập.
3. Trạng thái thanh toán được tính từ `PaidAmount`, `GrandTotal`, `DueDate` bằng một tập hằng chuẩn.
4. Quyền bảo hành của serial được tạo/cập nhật/vô hiệu hóa trong cùng transaction với hóa đơn.
5. Khi sửa khách hàng, ngày bán hoặc danh sách serial, coverage phải được reconcile, không để bản ghi mồ côi.

### 7.4 Kiểm kê và điều chỉnh

Kết quả kiểm kê không hoàn tất chỉ bằng cách đổi status session. Chênh lệch phải sinh chứng từ điều chỉnh, ghi sổ thành công, cập nhật balance/serial/ledger rồi session mới được `Completed`. Liên kết unique từ dòng kiểm kê đến chứng từ sửa giúp retry không tạo điều chỉnh trùng.

### 7.5 Import

Import dùng chiến lược `validate → stage in memory → write in one transaction`. Không tạo sản phẩm, serial hoặc chứng từ lẻ tẻ trong lúc còn đang validate các dòng sau. Import tồn đầu kỳ phải đi qua service riêng và chung inventory posting engine.

## 8. Phân quyền và audit

WarePro dùng vai trò cố định qua `AppUser.RoleCode`, không có bảng permission động. Ba lớp bảo vệ cần đồng bộ:

1. menu/view visibility;
2. `CanExecute` của command;
3. service authorization guard — lớp quyết định cuối cùng.

Audit phải trả lời: ai, lúc nào, hành động gì, entity nào, trước/sau ra sao. Đăng nhập thất bại lưu username được thử trong payload và dùng actor hệ thống/null; không gán hành vi thất bại cho tài khoản nạn nhân. Khi archive audit, lưu manifest gồm khoảng thời gian, số dòng, file, SHA-256 và người thực hiện trước khi xóa dữ liệu nguồn.

## 9. Thiết kế giao diện

WarePro dùng desktop business UI mật độ vừa phải, shell sidebar + top bar, nền sáng trung tính, accent xanh và màu trạng thái có ngữ nghĩa. Chuẩn Pro Max áp dụng bố cục ba hàng:

1. header: tiêu đề, mô tả, hành động chính;
2. filter/search: các điều kiện lọc và xuất dữ liệu;
3. content: DataGrid/card/empty state.

Nguyên tắc quan trọng:

- dùng resource/token trong `Themes`, không sao chép màu, margin và style giữa các view;
- không đưa token CSS/Tailwind vào XAML;
- chỉ dùng resource key và `PackIconKind` đã xác nhận tồn tại;
- khai báo resource trước lần `StaticResource` đầu tiên trong visual tree;
- DataGrid nhất quán về STT, căn số/phải, text/trái, status chip và row actions;
- nút bị khóa phải có lý do nghiệp vụ dễ hiểu, không chỉ `IsEnabled=false` bí ẩn;
- dialog serial phải hiện số đã chọn, số cần chọn, trùng/không hợp lệ và cách sửa;
- cache view phải refresh khi điều hướng lại nhưng không xóa dữ liệu đang hiển thị trước khi load mới thành công;
- không thay đổi layout hiện hữu khi chỉ sửa logic, trừ khi yêu cầu UI nói rõ.

## 10. Kiểm thử và xác minh

| Lớp test | Bắt lỗi gì |
|---|---|
| Unit/domain | state transition, quantity, policy, mapping |
| Service relational (SQLite) | transaction, unique/FK/check constraint, rollback |
| ViewModel | command, validation, refresh, trạng thái UI |
| XAML contract | binding/command/resource bị sai nhưng compiler không bắt hết |
| SQL Server smoke | khác biệt provider, migration và luồng tích hợp thật |
| UI smoke | app khởi động, đăng nhập, điều hướng và màn hình nạp dữ liệu |

Test bình thường loại `Category=RealDatabase`. Test DB thật phải dùng database dùng một lần, có guard tên/connection string và không bao giờ trỏ vào DB dev/prod. Quy trình hoàn tất tối thiểu:

```powershell
dotnet test QuanLyHangHoa.Tests/QuanLyHangHoa.Tests.csproj --filter "Category!=RealDatabase"
dotnet build
git diff --check
```

Với thay đổi migration hoặc luồng nghiệp vụ nhiều bảng, phải kiểm tra thêm trên SQL Server tạm và so sánh tất cả biểu diễn: chứng từ, balance, serial, ledger, invoice/coverage/claim và audit.

## 11. Những sai lầm lớn đã gặp và bài học

| Sai lầm | Vì sao sai | Cách WarePro sửa | Bài học tổng quát |
|---|---|---|---|
| Chọn serial xong số lượng về 0 | Hai property liên quan được cập nhật theo thứ tự không ổn định; handler ghi đè giá trị đúng | Đồng bộ quantity từ danh sách serial và thêm regression test cho mọi view nhập serial | Một dữ liệu chỉ có một nguồn chủ; derived state phải được tính, không cập nhật hai chiều tùy tiện |
| Nhiều nguồn tồn kho | `Product.Quantity`, balance và serial có thể lệch | Chọn `StockBalance`, `ProductSerial`, `StockLedger` làm nguồn chuẩn theo từng mục đích | Chốt source of truth trước khi làm UI/report |
| Ghi nhiều bảng rời rạc | Exception giữa chừng để lại dữ liệu nửa vời | Transaction bao toàn use case | Transaction boundary phải theo nghiệp vụ, không theo từng repository call |
| Import vừa đọc vừa ghi | Dòng sau lỗi nhưng dòng trước đã tồn tại | Validate/stage toàn bộ rồi ghi atomic | Import là một use case nghiệp vụ, không phải vòng lặp `Add()` |
| Khóa quyền ở UI | Caller khác vẫn gọi service được | Guard tại service và fail closed | UI là tiện ích; domain/application boundary mới là bảo mật thực thi |
| Cho sửa trực tiếp trạng thái serial | Bỏ qua ledger và balance | Chỉ cho sửa ghi chú; trạng thái đổi qua inventory/warranty service | Trạng thái có hệ quả phải đổi qua transition/use case chính thức |
| Status là chuỗi rải rác | Sai chính tả và logic lọc không đồng nhất | Hằng chuẩn/state machine/check constraint | Chuẩn hóa vocabulary ở code, DB và UI cùng lúc |
| Cache view không refresh | Màn hình sau hiển thị dữ liệu cũ từ bảng khác | `IRefreshable`, load vào list tạm rồi swap | Cache visual không đồng nghĩa cache business data vô hạn |
| Xóa master data đã được dùng | Vỡ FK hoặc mất ý nghĩa lịch sử | Dependency check và deactivate | Dữ liệu lịch sử ưu tiên tính truy vết hơn “xóa sạch” |
| Audit ngoài transaction | Entity đổi nhưng audit lỗi hoặc ngược lại | Entity + audit cùng transaction | Audit là một phần kết quả nghiệp vụ |
| XAML resource đặt sau nơi dùng | `XamlParseException` chỉ xuất hiện runtime | Đặt resources trước visual tree và contract test | WPF cần runtime/contract test, build xanh chưa đủ |
| Test chạm DB thật | Có nguy cơ phá dữ liệu và kết quả không lặp lại | Trait tách biệt, DB disposable và guard lifecycle | Test phải sở hữu dữ liệu nó tạo và có quyền xóa chỉ dữ liệu đó |

## 12. Điểm mạnh và giới hạn hiện tại

### Điểm mạnh

- Lõi tồn kho có source of truth, ledger và transaction rõ ràng.
- Serial được coi là domain entity, không phải chuỗi phụ của sản phẩm.
- Hóa đơn, tồn kho và bảo hành được reconcile thay vì cập nhật độc lập.
- Authorization, audit, migration và test đã được đưa vào ranh giới service.
- UI có guideline và contract test, phù hợp ứng dụng desktop nghiệp vụ.

### Giới hạn cần hiểu đúng

- Ứng dụng phụ thuộc Windows/WPF; không tái sử dụng trực tiếp UI cho web/mobile.
- Role cố định phù hợp phạm vi hiện tại nhưng không đáp ứng permission động phức tạp.
- Context factory được tạo thủ công; nếu số service và dependency tăng mạnh có thể cân nhắc DI container.
- Một solution/project lớn giúp đơn giản triển khai nhưng ranh giới compile-time giữa domain và UI chưa mạnh.
- SQLite relational test rất hữu ích nhưng không thay thế SQL Server smoke cho migration/provider-specific behavior.
- View cache cần kỷ luật refresh; nếu dữ liệu thời gian thực tăng, cần event/message hoặc invalidation rõ ràng hơn.

## 13. Thứ tự đọc dự án hiệu quả

1. `Thiết kế phần mềm.md`: mục tiêu, phạm vi và quyết định nghiệp vụ.
2. `ARCHITECTURE.md` và `MODULE_MAP.md` trong cùng thư mục: bản đồ nhanh.
3. `Models/` và `Data/AppDbContext.cs`: vocabulary, quan hệ, constraint.
4. `Inventory/`: source of truth và posting engine.
5. `StockInService`, `StockOutService`, `InvoiceService`, `WarrantyClaimService`: các use case liên kết nhiều bảng.
6. ViewModel/View cùng tên: cách nghiệp vụ đi ra giao diện.
7. Test cùng module: đặc tả hành vi chính xác và các regression đã gặp.
8. `../INDEX.md`: chọn lộ trình đọc sâu theo mục tiêu.
9. `../06-lessons/WAREPRO_REUSABLE_ENGINEERING_PLAYBOOK.md`: chuyển bài học sang dự án khác.

## 14. Mô hình tư duy cô đọng

Khi đánh giá bất kỳ thay đổi nào trong WarePro, hỏi theo thứ tự:

1. Quy tắc nghiệp vụ thật là gì?
2. Dữ liệu nào là nguồn sự thật?
3. Những bảng/màn hình nào cùng biểu diễn sự thật đó?
4. Transaction phải bao đến đâu?
5. Ai được phép thực hiện và guard ở đâu?
6. Khi retry/concurrent call thì điều gì xảy ra?
7. Audit và migration có giữ được lịch sử không?
8. Test nào chứng minh lỗi cũ không quay lại?
9. UI giải thích sai ở đâu và cách làm đúng cho người dùng chưa?

Nếu chín câu này có câu trả lời rõ ràng, thay đổi thường an toàn. Nếu chưa, viết thêm code thường chỉ làm lỗi khó thấy hơn.
