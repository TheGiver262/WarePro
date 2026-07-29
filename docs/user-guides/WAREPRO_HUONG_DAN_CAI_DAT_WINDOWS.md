# WarePro - Hướng dẫn cài đặt và cập nhật trên Windows

> Multi-client safety: workstations install WarePro only and connect to central SQL Server; never create a local WarePro database. On `DB-WRITE-CONFLICT`, reload instead of overwriting. See [DATABASE_CONCURRENCY.md](../DATABASE_CONCURRENCY.md).

## 1. Chọn cách cài phù hợp

Bộ cài WarePro có hai lựa chọn:

| Lựa chọn | Dùng khi | Bộ cài thực hiện |
|---|---|---|
| **Cài đầy đủ một-click (WarePro + SQL Server Express)** | một máy dùng độc lập hoặc máy chủ nhỏ chưa có SQL Server | cài WarePro, tải/cài SQL Server Express SQLEXPRESS nếu cần, tạo cấu hình mặc định |
| **Chỉ cài WarePro** | máy trạm dùng SQL Server có sẵn trên máy khác hoặc máy hiện tại | cài ứng dụng, nhận server/database/auth mode, không tải và không cài SQL Server |

Chọn **Cài đầy đủ một-click** cho một máy độc lập. Chọn **Chỉ cài WarePro** cho hệ thống nhiều máy dùng chung dữ liệu.

> không cài SQL Server/database riêng trên từng máy trạm nếu các máy phải nhìn thấy cùng dữ liệu.

## 2. Yêu cầu trước khi cài

Yêu cầu chung:

- Windows 10/11 x64; ưu tiên Windows 11 x64.
- RAM tối thiểu 4 GB, khuyến nghị 8 GB.
- tài khoản có quyền Administrator trong lúc cài.
- đủ dung lượng cho ứng dụng, SQL Server, database và backup.
- nhận `WarePro-Setup.exe` từ nguồn phát hành của đơn vị.
- bộ cài chính thức phải có chữ ký số WarePro hợp lệ.

Bản WarePro phát hành là self-contained, không yêu cầu cài .NET Desktop Runtime riêng.

Yêu cầu riêng:

- **cài đầy đủ:** cần internet để tải SQL Server Express chính chủ của Microsoft.
- **chỉ cài WarePro:** cần tên server/instance, tên database, auth mode và quyền kết nối.
- **máy trạm:** phải truy cập được SQL Server qua mạng nội bộ/VPN; firewall và TCP/IP do quản trị viên SQL cấu hình.
- không dùng tài khoản `sa` cho máy trạm.

## 3. Kiểm tra bộ cài trước khi chạy

1. nhấp phải `WarePro-Setup.exe`, chọn **Properties**.
2. mở tab **Digital Signatures**.
3. kiểm tra chữ ký hợp lệ và đúng nhà phát hành WarePro đã được đơn vị thông báo.
4. nếu không có chữ ký, chữ ký lỗi hoặc nguồn file không rõ, không chạy bộ cài.
5. chỉ dùng một bản cài có version/hash được quản trị viên xác nhận.

Windows SmartScreen có thể vẫn cảnh báo với certificate mới. Không chọn bỏ qua nếu chưa kiểm tra chữ ký.

## 4. Cài đầy đủ một-click

Dùng cho máy độc lập hoặc máy chủ nhỏ.

1. chạy `WarePro-Setup.exe` bằng quyền Administrator.
2. chọn **Cài đầy đủ một-click (WarePro + SQL Server Express)**.
3. đọc lại lựa chọn trước khi nhấn **Cài đặt**.
4. giữ kết nối internet trong lúc bộ cài tải SQL Server Express.
5. nếu máy đã có SQLEXPRESS tương thích, bộ cài dùng lại instance đó; không cài lần hai.
6. bộ cài kiểm tra SQLEXPRESS, ghi cấu hình máy và kiểm tra quyền tạo/kết nối database.
7. nếu Windows yêu cầu restart, restart máy rồi mở WarePro từ Start Menu.
8. không tắt máy hoặc dừng bộ cài trong lúc SQL Server đang cài.

Giá trị mặc định:

    server: .\SQLEXPRESS
    database: ProductManagementDb
    auth: Windows Authentication

Lần mở đầu, WarePro chỉ seed khi database trống. Không seed đè lên database đã có dữ liệu.

## 5. Chỉ cài WarePro

### 5.1. Chuẩn bị thông tin

Nhận từ quản trị viên:

- server/instance, ví dụ `SERVER01\SQLEXPRESS`.
- database, thường là `ProductManagementDb`.
- **Windows Authentication** hoặc **SQL Authentication**.
- xác nhận tài khoản có quyền đọc/ghi và quyền schema/backup phù hợp khi update yêu cầu migration.

### 5.2. Windows Authentication

1. chạy bộ cài bằng quyền Administrator.
2. chọn **Chỉ cài WarePro**.
3. nhập server và database.
4. chọn **Windows Authentication**.
5. bộ cài thử kết nối trước khi hoàn tất.
6. nếu lỗi, quay lại sửa server/database; không lưu cấu hình lỗi.

Windows user mở WarePro phải được SQL Server cấp quyền.

### 5.3. SQL Authentication

1. chọn **Chỉ cài WarePro**.
2. nhập server và database.
3. chọn **SQL Authentication**.
4. bộ cài chỉ lưu auth mode; không hỏi và không truyền password qua command line.
5. mở WarePro lần đầu.
6. cửa sổ **Đăng nhập SQL Server** xuất hiện trước màn hình đăng nhập WarePro.
7. nhập tài khoản SQL được cấp và chọn **Lưu và tiếp tục**.

Credential được lưu trong **Windows Credential Manager** với target `WarePro/Database` cho Windows user hiện tại. Password không nằm trong `warepro.settings.json`.

Mỗi Windows user dùng máy phải nhập SQL credential của mình lần đầu. Nếu đổi password SQL, quản trị viên xóa/cập nhật entry `WarePro/Database` rồi mở lại WarePro.

## 6. Lần mở đầu và kiểm tra database

Thứ tự startup:

1. đọc đường dẫn và cấu hình máy.
2. lấy credential/connection string theo nguồn được phép.
3. kết nối SQL Server.
4. kiểm tra schema compatibility.
5. nếu schema cũ, dừng với `DB-UPGRADE-REQUIRED` và yêu cầu chạy bộ cài.
6. nếu database yêu cầu client mới hơn, dừng với `DB-CLIENT-UPDATE-REQUIRED` và yêu cầu cập nhật WarePro.
7. chỉ seed một lần khi bộ cài chọn dữ liệu mẫu, vai trò là Server/Standalone và database chưa có dữ liệu.
8. đăng ký phiên client rồi mở màn hình đăng nhập WarePro.

Với môi trường được quản trị tập trung, biến `WAREPRO_CONNECTION_STRING` có ưu tiên cao nhất. Chỉ quản trị viên hệ thống được thiết lập biến này; không gửi giá trị có password qua email/chat và không ghi nó vào tài liệu hỗ trợ.

Ứng dụng chỉ kiểm tra trạng thái database, không tự lấy lock nâng cấp, backup hoặc chạy migration. Bộ cài sở hữu toàn bộ quy trình nâng cấp. Không tắt bộ cài trong lúc database đang nâng cấp; nếu có nhiều máy, chỉ một quản trị viên chạy nâng cấp và các client mở lại sau khi hoàn tất.

## 7. Cập nhật phiên bản trong WarePro

WarePro tự kiểm tra tối đa một lần trong 24 giờ. Mất internet không chặn đăng nhập nếu database vẫn tương thích.

Cập nhật thủ công:

1. mở **Hệ thống > Cập nhật WarePro**.
2. chọn **Kiểm tra cập nhật**.
3. đọc version mới, dung lượng và nội dung thay đổi.
4. chọn **Tải và cài đặt**.
5. WarePro tải vào file `.partial` và kiểm tra size, SHA-256, Authenticode, certificate chain, timestamp và publisher.
6. chỉ khi mọi kiểm tra đạt, WarePro mở installer bằng quyền Administrator.
7. lưu công việc đang làm và đóng các cửa sổ WarePro khi được yêu cầu.
8. sau cài, mở lại app và kiểm tra version.

Nếu update là bắt buộc do schema, không dùng app cũ để cố mở database mới hơn.

## 8. Cập nhật thủ công bằng installer

Dùng khi update trong app không truy cập được máy chủ phát hành:

1. nhận đúng `WarePro-Setup.exe` của version mới.
2. kiểm tra chữ ký như mục 3.
3. backup database nếu quản trị viên xác nhận release có thay đổi schema.
4. đóng WarePro trên các máy đang dùng chung database.
5. chạy installer mới; AppId cố định giúp Inno nâng cấp bản đang cài.
6. không gỡ bản cũ trước, trừ khi runbook sự cố yêu cầu.
7. mở lại WarePro và kiểm tra login, dashboard, nhập/xuất/tồn kho, bảo hành, báo cáo và print preview.

Upgrade giữ machine config, Windows credential và database.

## 9. Vị trí file và log

| Nội dung | Đường dẫn |
|---|---|
| ứng dụng | `C:\Program Files\WarePro` |
| cấu hình máy | `%ProgramData%\WarePro\Config\warepro.settings.json` |
| installer/setup helper log | `%ProgramData%\WarePro\InstallerLogs` |
| app/crash log | `%LocalAppData%\WarePro\Logs` |
| update cache | `%LocalAppData%\WarePro\Updates` |
| update state | `%LocalAppData%\WarePro\State\update-state.json` |
| SQL credential | Windows Credential Manager, target `WarePro/Database` |

`warepro.settings.json` chỉ lưu metadata như server, database, auth mode, chính sách mã hóa kết nối và update channel. SQL Express cục bộ dùng `encrypt=false`; máy chủ từ xa chỉ bật `encrypt=true` khi đã cấu hình chứng chỉ TLS hợp lệ. Khi cài silent, truyền `/WAREPROENCRYPT=true` cho trường hợp này. Không thêm password, token hoặc full connection string vào file.

Không sửa file trong `C:\Program Files\WarePro`. Khi cần đổi server/database, chạy lại bộ cài **Chỉ cài WarePro** hoặc nhờ quản trị viên cập nhật cấu hình đúng quyền.

## 10. Gỡ cài đặt

Vào **Settings > Apps > Installed apps > WarePro > Uninstall**.

Mặc định uninstall:

- xóa WarePro binary và shortcut.
- không xóa database.
- không gỡ SQL Server/SQLEXPRESS.
- giữ machine config.
- giữ credential `WarePro/Database`.

Tùy chọn xóa config/cache cục bộ vẫn không xóa database, SQL instance hoặc credential.

Muốn xóa database hay SQL Server phải có kế hoạch backup riêng và được quản trị viên xác nhận. Không coi uninstall WarePro là thao tác xóa dữ liệu.

## 11. Lỗi thường gặp

### `CFG-CREDENTIAL-MISSING`

SQL Authentication chưa có credential cho Windows user hiện tại.

Cách xử lý: mở lại app để nhập credential; nếu prompt không hiện, kiểm tra auth mode và entry `WarePro/Database` trong Windows Credential Manager.

### `CFG-CONFIG-INVALID`

Cấu hình thiếu, hỏng JSON hoặc sai schema.

Cách xử lý: không tự chèn connection string. Chạy lại bộ cài đúng mode hoặc gửi `warepro.settings.json` đã loại thông tin nhạy cảm cho quản trị viên.

### `SQL-CREDENTIAL-REJECTED`

Username/password SQL sai, hết hạn hoặc user bị khóa.

Cách xử lý: xác nhận credential với quản trị viên SQL, cập nhật entry Windows Credential Manager rồi mở lại app.

### `SQL-SERVICE-UNAVAILABLE`

Sai server/instance, SQL service dừng, mạng/firewall/TCP chưa sẵn sàng.

Cách xử lý: kiểm tra tên server, dịch vụ SQL Server, VPN/mạng nội bộ và firewall. Không tự mở firewall rộng ra internet.

### `DB-BACKUP-FAILED`

SQL account thiếu quyền backup, thư mục backup hết dung lượng hoặc verify thất bại. WarePro dừng trước migration.

Cách xử lý: quản trị viên SQL sửa quyền/dung lượng và chạy lại. Không bỏ qua backup gate.

### `DB-CLIENT-UPDATE-REQUIRED`

Database yêu cầu phiên bản WarePro mới hơn client hiện tại.

Cách xử lý: cập nhật WarePro. Không downgrade schema hoặc cài app cũ khi chưa có rollback plan.

### `UPD-OFFLINE`

Không kết nối được máy chủ cập nhật.

Cách xử lý: tiếp tục làm việc nếu app cho phép, kiểm tra internet rồi thử lại. Có thể dùng installer thủ công đã ký.

### `UPD-HASH-MISMATCH` / `UPD-SIGNATURE-INVALID` / `UPD-PUBLISHER-MISMATCH`

Artifact không qua kiểm tra an toàn.

Cách xử lý: không chạy file; xóa bản tải lỗi và liên hệ quản trị viên. Không tắt kiểm tra chữ ký.

### `INST-STARTUP-FAILED`

Startup gặp lỗi chưa phân loại.

Cách xử lý: ghi mã lỗi, version và thời gian; gửi app log/installer log tương ứng. Không gửi password, token hoặc connection string đầy đủ.

## 12. An toàn dữ liệu khi nhiều máy dùng chung

- trước update có migration, đóng hoặc thông báo tất cả client.
- không restore database khi còn client đang chạy.
- không xóa database để “cài lại cho sạch”.
- không copy file MDF/LDF bằng kéo-thả khi SQL Server đang dùng.
- chỉ restore backup đã verify theo quy trình của quản trị viên SQL.
- không test nhập/xuất/xóa dữ liệu trên production.
- giữ ít nhất ba installer stable để có đường rollback phù hợp schema.
- sau update kiểm tra ít nhất một tài khoản admin, quản lý và nhân viên.

## 13. Thông tin cần gửi khi yêu cầu hỗ trợ

Gửi:

- version WarePro.
- Windows version.
- mode đã cài: đầy đủ hay chỉ WarePro.
- server/instance và database name; không gửi password.
- mã lỗi hiển thị.
- thời gian xảy ra lỗi.
- log tương ứng ở mục 9.
- thao tác ngay trước lỗi.

Trước khi gửi log, kiểm tra lại không có password, token, credential hoặc full connection string.
