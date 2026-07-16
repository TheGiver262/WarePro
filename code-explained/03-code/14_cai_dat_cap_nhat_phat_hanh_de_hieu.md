# 14 - Cài đặt, cập nhật và phát hành WarePro dễ hiểu

Chương này giải thích phần đưa WarePro từ source code thành phần mềm Windows có thể cài đặt và cập nhật an toàn. Đây là kiến trúc hiện hành từ commit `895a70a`.

## 1. Các khối chính

| Khối | Trách nhiệm |
|---|---|
| `QuanLyHangHoa/Configuration` | đường dẫn runtime, JSON cấu hình, connection string và Windows Credential Manager |
| `QuanLyHangHoa/Startup` | đọc cấu hình, probe SQL, khởi tạo database và chuẩn hóa lỗi startup |
| `QuanLyHangHoa/Updates` | đọc release, kiểm tra phiên bản, tải và xác minh installer |
| `WarePro.Core` | contract dùng chung không phụ thuộc WPF |
| `WarePro.SetupHelper` | lệnh ổn định để Inno Setup kiểm tra SQL và ghi cấu hình |
| `installer` | định nghĩa hai mode cài, SQL Express dependency và quy tắc upgrade/uninstall |
| `scripts/release` | test, build, publish, ký, tạo manifest và verify artifact |

## 2. Dữ liệu được đặt ở đâu?

WarePro không ghi dữ liệu thay đổi vào `Program Files`.

| Dữ liệu | Vị trí |
|---|---|
| binary ứng dụng | `C:\Program Files\WarePro` |
| cấu hình máy | `%ProgramData%\WarePro\Config\warepro.settings.json` |
| log installer/helper | `%ProgramData%\WarePro\InstallerLogs` |
| crash log theo user | `%LocalAppData%\WarePro\Logs` |
| cache update | `%LocalAppData%\WarePro\Updates` |
| trạng thái update | `%LocalAppData%\WarePro\State\update-state.json` |
| SQL password | Windows Credential Manager, target `WarePro/Database` |

JSON chỉ giữ server, database, auth mode và update channel. Password không nằm trong JSON, log hay command line.

## 3. Connection string được chọn như thế nào?

`ConnectionStringFactory` dùng thứ tự:

1. `WAREPRO_CONNECTION_STRING` nếu quản trị viên đã đặt;
2. `warepro.settings.json`;
3. cấu hình mặc định `\.\SQLEXPRESS` và `ProductManagementDb`.

Với Windows Authentication, SqlClient dùng danh tính Windows. Với SQL Authentication, factory đọc `SqlCredential` từ Credential Manager, chỉ mở `SecureString` trong thời gian dựng connection string rồi xóa vùng nhớ unmanaged trong `finally`.

## 4. Luồng startup hiện hành

```text
App.OnStartup
  -> FirstRunCredentialCoordinator
  -> StartupCoordinator
      -> LoadSettings
      -> ResolveConnectionString
      -> ProbeSqlAsync
      -> DatabaseInitializer.Initialize
          -> compatibility check
          -> SchemaUpgradeLock trên master
          -> EnsureCreated nếu database mới
          -> backup + RESTORE VERIFYONLY nếu nâng schema có dữ liệu
          -> schema update trong transaction
          -> seed nếu cần
  -> LoginView
```

`EnsureCreated` chỉ là một bước cho database mới, không phải toàn bộ chiến lược nâng cấp. Database hiện hữu được quản lý bằng metadata `__WareProSchemaVersion`, compatibility gate và các script idempotent.

## 5. Vì sao cần khóa nâng schema?

Nhiều máy có thể mở WarePro cùng lúc và cùng thấy schema cũ. `SchemaUpgradeLock` gọi `sp_getapplock` với resource `WarePro.SchemaUpgrade:<database>` trên connection tới `master`.

Chỉ một client giữ exclusive lock. Client còn lại chờ, sau đó đọc lại schema dưới khóa. Nhờ vậy không có hai tiến trình cùng chạy DDL hoặc cùng seed.

## 6. Khi nào backup được tạo?

`DatabaseCompatibilityService.RequiresBackup` chỉ yêu cầu backup khi:

- database đã có bảng nghiệp vụ;
- schema thấp hơn phiên bản hiện hành;
- app chuẩn bị thay đổi schema.

`DatabaseBackupService` tạo `COPY_ONLY` backup với `CHECKSUM`, sau đó chạy `RESTORE VERIFYONLY WITH CHECKSUM`. Nếu backup hoặc verify lỗi, startup dừng trước DDL.

## 7. Hai chế độ cài đặt

### Cài đầy đủ một-click

Dùng cho máy độc lập hoặc máy chủ nhỏ. Installer cài WarePro, tải SQL Server Express 2022 từ URL Microsoft đã ghim, kiểm tra SHA-256/chữ ký, cài hoặc dùng lại `SQLEXPRESS`, rồi ghi cấu hình mặc định.

### Chỉ cài WarePro

Dùng cho máy trạm hoặc máy đã có SQL Server. Người dùng nhập server, database và auth mode. Windows Authentication được test ngay; SQL Authentication được test sau khi user nhập credential ở lần mở đầu.

Upgrade chỉ thay binary ứng dụng, giữ machine config, credential và database.

## 8. Kiểm tra cập nhật

`UpdateService.CheckAsync` đọc stable release và `warepro-update.json`. Release bị từ chối nếu draft/prerelease, version không khớp, asset name/size sai hoặc schema nằm ngoài dải hỗ trợ.

Automatic check tối đa một lần trong 24 giờ. Mất mạng trả trạng thái offline nhưng không chặn app nếu database vẫn tương thích.

## 9. Tải và xác minh installer

Installer tải vào file `.partial`. Trước khi launch, WarePro kiểm tra theo thứ tự:

1. kích thước từ GitHub API và manifest;
2. SHA-256;
3. Authenticode signature;
4. certificate chain;
5. trusted timestamp countersigner;
6. publisher thumbprint đã ghim.

Hash trả lời “file có đúng từng byte không”. Authenticode và thumbprint trả lời “ai đã ký file”. Timestamp chứng minh chữ ký được tạo khi certificate còn hiệu lực. Cần đủ cả ba lớp.

## 10. Phát hành phiên bản mới

`Build-WareProRelease.ps1` chạy tuần tự:

```text
591 test ngoài RealDatabase
-> Release build
-> self-contained app publish
-> single-file setup helper publish
-> ký app/helper (bản phát hành thật)
-> Inno Setup compile
-> ký installer
-> tạo manifest/checksum/release notes
-> Verify-WareProRelease
-> GitHub draft release
```

Compile-only pipeline đã đạt ở commit `895a70a`: 591/591 test, build 0 warning/0 error và Inno Setup 6.7.3 compile thành công.

## 11. Implementation complete khác release ready

Implementation complete nghĩa là code, test, installer script, updater, release script và tài liệu qua automated gate.

Release ready cần thêm Gate C:

- certificate phát hành thật;
- Windows VM sạch cho cả hai mode;
- update 1.0.0 -> 1.0.1;
- shared-database lock;
- backup/restore và rollback drill;
- phê duyệt release notes và hướng dẫn.

Gate C hiện vẫn HOLD, vì vậy không được mô tả artifact compile-only là bản stable sẵn sàng phát hành.

## 12. Câu hỏi bảo vệ ngắn

**Tại sao không lưu SQL password trong JSON?** Vì ProgramData là cấu hình dùng chung máy; password phải được bảo vệ theo từng Windows user bằng Credential Manager.

**Tại sao vừa hash vừa kiểm tra chữ ký?** Hash kiểm tra nội dung, chữ ký kiểm tra danh tính nhà phát hành; một lớp không thay thế lớp còn lại.

**Tại sao backup trước migration?** Transaction bảo vệ các lệnh trong migration, còn verified backup tạo đường phục hồi khi lỗi vận hành hoặc schema mới không phù hợp.

**Tại sao app cũ bị chặn khi database mới hơn?** Client cũ không hiểu schema mới có thể ghi sai dữ liệu. Compatibility gate dừng trước login để bảo vệ database dùng chung.
