# Cau truc tep WarePro va cach tim code

```text
ProductManagement_Antigravity/
|-- QuanLyHangHoa/          Ung dung WPF
|   |-- Configuration/      Path, settings va SQL credential
|   |-- Data/               EF Core DbContext
|   |-- Inventory/          Loi ghi so kho
|   |-- Models/             Entity va enum
|   |-- Services/           Nghiep vu ung dung
|   |-- Startup/            Probe SQL va khoi tao database
|   |-- Updates/            Kiem tra, tai va xac minh update
|   |-- ViewModels/         State va command MVVM
|   |-- Views/              Man hinh va dialog
|   |-- Themes/             Style/resource WPF
|-- QuanLyHangHoa.Tests/    Test tu dong
|-- WarePro.Core/           Contract dung chung
|-- WarePro.SetupHelper/    CLI cho installer
|-- installer/              Inno Setup va SQL Express dependency
|-- scripts/release/        Build, sign, manifest va verify
|-- docs/                   Runbook, user guide, plan va spec
|-- code-explained/         Tai lieu hoc va giai thich code
|-- Diagram/                So do thiet ke cong khai
|-- graphify-out/           Chi muc code cuc bo, khong commit
`-- output/                 Sach Word/PDF sinh tu Markdown, local-only
```

## Tim code

```powershell
rtk rg -n "WarrantyClaimService|PrintCommand|CurrentView" QuanLyHangHoa QuanLyHangHoa.Tests
rtk rg --files QuanLyHangHoa\Views QuanLyHangHoa\ViewModels QuanLyHangHoa\Services
rtk rg --files | rtk rg "PurchaseInvoice(View|ViewModel)"
rtk rg -n "DocumentPrintService|InventoryPostingService" .
rtk rg -n "TODO|NotImplementedException|dang phat trien|placeholder" QuanLyHangHoa
```

Dung `rg` cho ket qua chinh xac. Dung Graphify khi can lan quan he nhieu module:

```powershell
rtk graphify query "How does warranty printing flow work?" --budget 1500
rtk graphify explain "InventoryPostingService"
rtk graphify path "StockInViewModel" "StockLedger"
rtk graphify . --update --code-only --no-viz
rtk graphify cluster-only . --no-viz
```

Chi muc hien tai: 9.957 node, 11.696 edge, 1.855 community.
