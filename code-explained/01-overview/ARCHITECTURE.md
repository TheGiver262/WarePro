# Kien truc WarePro hien tai

Ung dung desktop WPF .NET 8 theo MVVM.

```text
Views -> ViewModels -> Services + Inventory -> AppDbContext / EF Core / SQL Server
```

## Khoi dong va dieu huong

1. `App.xaml.cs` thu credential lan dau neu cau hinh dung SQL Authentication.
2. `StartupCoordinator` doc cau hinh, tao connection string, probe SQL va goi `DatabaseInitializer` tren worker.
3. `DatabaseInitializer` kiem tra compatibility, lay `SchemaUpgradeLock`, backup co verify neu can, cap nhat schema va seed.
4. Startup thanh cong moi hien `LoginView`; loi duoc map thanh ma on dinh va log da redact secret.
5. Dang nhap thanh cong mo `MainWindow` voi `MainViewModel`.
6. `MainViewModel` kiem tra `AuthorizationService`, tao view lan dau va cache theo khoa.
7. ViewModel goi service qua `Func<AppDbContext>` de dung context ngan han.
8. `CrashLogger` nhan loi tu Dispatcher, AppDomain va TaskScheduler.

Cai dat va cap nhat la mot bien kien truc rieng: `Configuration` + `Startup` + `Updates` + `WarePro.SetupHelper` + Inno/release scripts. Doc [chuong cai dat, cap nhat va phat hanh](../03-code/14_cai_dat_cap_nhat_phat_hanh_de_hieu.md) de xem luong day du.

## Bien module

- `Views/`: XAML, binding, dialog va hanh vi UI.
- `ViewModels/`: state, command, validation, loc va dieu phoi.
- `Services/`: nghiep vu ung dung, truy van, report, print, quyen va startup.
- `Inventory/`: loi ghi so kho, command, policy va unit of work.
- `Data/`: `AppDbContext` va mapping EF Core.
- `Models/`: entity va enum.
- `Migrations/`: lich su schema.
- `Themes/`, `Converters/`, `Helpers/`: tai nguyen UI va tien ich dung chung.
- `QuanLyHangHoa.Tests/`: test service, ViewModel, view contract, inventory va seed.

## Luong chinh

### Kho

ViewModel chung tu -> service -> `InventoryPostingService`/policy -> `EfInventoryUnitOfWork` -> `StockBalance`, `ProductSerial`, `StockLedger`, audit trong transaction.

`StockBalance` la ton hien tai. `StockLedger` la lich su bien dong. Khong cap nhat rieng `Product.Quantity` cho nghiep vu ton kho.

### Bao hanh

`WarrantyCoverage` la quyen/thoi han. `WarrantyClaim` la mot lan tiep nhan. `WarrantyClaimService` xu ly chuyen trang thai; `WarrantyViewModel` dieu phoi UI.

### Bao cao va in

`ReportTraceService` ghep ledger, chung tu, hoa don, kho, khach va bao hanh thanh lich su serial. In an dung chung theo luong `DocumentPrintModel` -> `DocumentPrintService` -> `DocumentPrintWindow`.

## Quy tac sua

- UI: doc `Views/AGENTS.md`, view, code-behind va ViewModel cung ten.
- Nghiep vu: tim caller cua service va test cung module.
- Schema: dong bo model, `AppDbContext`, migration/initializer, seed va report.
- Quyen: dong bo menu, command `CanExecute` va guard service.
- Moi task: build Release va test; khong deploy neu chua duoc yeu cau.

```powershell
rtk dotnet build QuanLyHangHoa.Tests\QuanLyHangHoa.Tests.csproj -c Release --no-restore
rtk dotnet test QuanLyHangHoa.Tests\QuanLyHangHoa.Tests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal"
```
