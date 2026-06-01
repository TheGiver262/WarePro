import os
import subprocess

# Set working directory to the script's directory (Diagram)
script_dir = os.path.dirname(os.path.abspath(__file__))
if script_dir:
    os.chdir(script_dir)

puml_dir = "plantuml"
erd_module_dir = "plantuml-erd-module"
os.makedirs(puml_dir, exist_ok=True)
os.makedirs(erd_module_dir, exist_ok=True)

diagrams = {
    # 1. Architecture
    "Architecture_MVVM_WPF_SQLServer.puml": """@startuml Architecture_MVVM_WPF_SQLServer
title Kiến trúc WPF / MVVM / SQL Server

package "Presentation" {
    [Views\\nWPF XAML] as View
    [ViewModels\\nCommands + Validation UI] as VM
}

package "Application" {
    [AuthService] as Auth
    [AuthorizationService] as Authz
    [ApprovalService] as Approval
    [Catalog Service] as Catalog
    [Inventory Service] as Inventory
    [Sales Service] as Sales
    [Warranty Service] as Warranty
    [Reporting Service] as Report
}

package "Domain" {
    [Entities\\nAppUser, Warehouse, Product,\\nStockBalance, ProductSerial,\\nWarrantyCoverage, WarrantyClaim] as Entities
    [Business Rules\\nPosting, Approval,\\nStock Locking,\\nWarranty Eligibility] as Rules
}

package "Infrastructure" {
    [Repositories] as Repo
    [Transactions + Row Locking] as Tx
    [AuditLog + StockLedger] as Audit
    database "SQL Server" as DB
}

View --> VM
VM --> Auth
VM --> Authz
VM --> Approval
VM --> Catalog
VM --> Inventory
VM --> Sales
VM --> Warranty
VM --> Report

Auth --> Entities
Authz --> Rules
Approval --> Rules
Catalog --> Entities
Inventory --> Entities
Sales --> Entities
Warranty --> Entities
Entities --> Rules

Auth --> Repo
Authz --> Repo
Approval --> Repo
Catalog --> Repo
Inventory --> Repo
Sales --> Repo
Warranty --> Repo
Report --> Repo
Repo --> Tx
Tx --> Audit
Audit --> DB

@enduml""",

    # 2. Use Case Diagrams
    "UseCase_TongThe.puml": """@startuml UseCase_TongThe
left to right direction
skinparam packageStyle rect

actor "Quản trị viên" as Admin
actor "Quản lý" as Manager
actor "Nhân viên kho" as Storekeeper
actor "Nhân viên bán hàng" as Salesman
actor "Nhân viên bảo hành" as Technician

Admin --|> Manager
Manager --|> Storekeeper
Manager --|> Salesman
Manager --|> Technician

rectangle "Hệ thống quản lý hàng hóa và bảo hành" {
    usecase "Quản lý Quản trị & Danh mục\\n(Tài khoản, phân quyền, danh mục nền)" as UC_AdminCatalog
    usecase "Quản lý Kho & Tồn kho\\n(Nhập/xuất, kiểm kê, điều chỉnh)" as UC_Inventory
    usecase "Quản lý Hóa đơn & Báo cáo\\n(Hóa đơn mua/bán, báo cáo thống kê)" as UC_InvoicingReport
    usecase "Quản lý Bảo hành\\n(Tiếp nhận claim, duyệt đổi mới, trả khách)" as UC_Warranty
}

Admin --> UC_AdminCatalog
Manager --> UC_AdminCatalog

Storekeeper --> UC_Inventory
Salesman --> UC_Inventory

Storekeeper --> UC_InvoicingReport
Salesman --> UC_InvoicingReport
Manager --> UC_InvoicingReport

Technician --> UC_Warranty
Storekeeper --> UC_Warranty
Salesman --> UC_Warranty
Manager --> UC_Warranty

@enduml""",

    "UseCase_AdminCatalog.puml": """@startuml UseCase_AdminCatalog
left to right direction
skinparam packageStyle rect

actor "Quản trị viên" as Admin
actor "Quản lý" as Manager
actor "Nhân viên" as Employee

Admin --|> Manager

rectangle "Phân hệ quản trị và danh mục" {
    package "Quản trị tài khoản & Phân quyền" {
        usecase "Đăng nhập hệ thống" as UC_Auth1
        usecase "Đổi mật khẩu" as UC_Auth2
        usecase "Yêu cầu đổi mật khẩu lần đầu" as UC_Auth3
        usecase "Quản lý tài khoản người dùng\\n(Tạo mới, khóa, mở khóa)" as UC_User1
        usecase "Thiết lập quyền hạn & vai trò" as UC_User2
        usecase "Xem nhật ký hệ thống (Audit Log)" as UC_Audit
    }

    package "Quản lý danh mục nền" {
        usecase "Quản lý sản phẩm\\n(Thông tin, đơn vị, serial)" as UC_Cat1
        usecase "Quản lý đối tượng\\n(Khách hàng, Nhà cung cấp)" as UC_Cat2
        usecase "Quản lý kho hàng" as UC_Cat3
    }
}

Employee --> UC_Auth1
Employee --> UC_Auth2

Manager --> UC_Audit
Manager --> UC_Cat1
Manager --> UC_Cat2
Manager --> UC_Cat3

Admin --> UC_User1
Admin --> UC_User2

UC_Auth1 <.. UC_Auth3 : <<extend>>
@enduml""",

    "UseCase_TraCuu_BaoCao.puml": """@startuml UseCase_TraCuu_BaoCao
left to right direction
skinparam packageStyle rect

actor "Quản trị viên" as Admin
actor "Quản lý" as Manager
actor "Nhân viên kho" as Storekeeper
actor "Nhân viên bán hàng" as Salesman
actor "Nhân viên bảo hành" as Technician

Admin --|> Manager
Manager --|> Storekeeper
Manager --|> Salesman
Manager --|> Technician

rectangle "Phân hệ tra cứu và báo cáo" {
    usecase "Tìm kiếm theo mã, tên, serial" as UC1
    usecase "Lọc theo trạng thái, ngày, đối tượng" as UC2
    usecase "Sắp xếp tăng / giảm theo cột" as UC3
    usecase "Xem tồn theo sản phẩm và serial" as UC4
    usecase "Xem lịch sử kho" as UC5
    usecase "Tra cứu serial đã bán và tình trạng bảo hành" as UC6
    usecase "Tra cứu coverage, claim và lịch sử bảo hành" as UC7
    usecase "Xem audit log và nhật ký thay đổi" as UC8
    usecase "Tạo báo cáo tồn kho và nhập xuất tồn" as UC9
    usecase "Tạo báo cáo doanh thu, công nợ, bảo hành" as UC10
}

Storekeeper --> UC1
Storekeeper --> UC2
Storekeeper --> UC3
Storekeeper --> UC4
Storekeeper --> UC5

Salesman --> UC1
Salesman --> UC2
Salesman --> UC3
Salesman --> UC6

Technician --> UC1
Technician --> UC2
Technician --> UC3
Technician --> UC7

Manager --> UC8
Manager --> UC9
Manager --> UC10

Admin --> UC8

@enduml""",

    "UseCase_NhapXuatKho_HoaDon.puml": """@startuml UseCase_NhapXuatKho_HoaDon
left to right direction
skinparam packageStyle rect

actor "Quản trị viên" as Admin
actor "Quản lý" as Manager
actor "Nhân viên kho" as Storekeeper
actor "Nhân viên bán hàng" as Salesman

Admin --|> Manager
Manager --|> Storekeeper
Manager --|> Salesman

rectangle "Phân hệ nhập xuất kho" {
    package "Quy trình nhập kho" {
        usecase "Lập phiếu nhập kho" as UC_In1
        usecase "Quét / nhập serial" as UC_In2
        usecase "Gửi duyệt phiếu nhập" as UC_In3
        usecase "Duyệt phiếu nhập" as UC_In4
        usecase "Ghi sổ phiếu nhập" as UC_In5
        usecase "Lập hóa đơn mua từ phiếu nhập" as UC_In6
        usecase "Nhập chi tiết hóa đơn mua\\n(sản phẩm, đơn giá, số lượng, thuế)" as UC_In7
        usecase "Nhập tồn đầu kỳ từ Excel/CSV" as UC_In8
    }

    package "Quy trình xuất kho" {
        usecase "Lập phiếu xuất kho" as UC_Out1
        usecase "Chọn / quét serial xuất kho" as UC_Out2
        usecase "Gửi duyệt phiếu xuất" as UC_Out3
        usecase "Duyệt phiếu xuất" as UC_Out4
        usecase "Ghi sổ phiếu xuất" as UC_Out5
        usecase "Lập hóa đơn bán từ phiếu xuất" as UC_Out6
        usecase "Nhập chi tiết hóa đơn bán\\n(sản phẩm, đơn giá, số lượng, thuế)" as UC_Out7
    }
}

Storekeeper --> UC_In1
Storekeeper --> UC_In3
Storekeeper --> UC_In5
Storekeeper --> UC_In6
Storekeeper --> UC_In8

Storekeeper --> UC_Out1
Storekeeper --> UC_Out3
Storekeeper --> UC_Out5

Salesman --> UC_Out1
Salesman --> UC_Out3
Salesman --> UC_Out6

Manager --> UC_In4
Manager --> UC_Out4

UC_In1 <.. UC_In2 : <<extend>>
UC_In6 ..> UC_In7 : <<include>>

UC_Out1 <.. UC_Out2 : <<extend>>
UC_Out6 ..> UC_Out7 : <<include>>

@enduml""",

    "UseCase_KiemKe_DieuChinh.puml": """@startuml UseCase_KiemKe_DieuChinh
left to right direction
skinparam packageStyle rect

actor "Quản trị viên" as Admin
actor "Quản lý" as Manager
actor "Nhân viên kho" as Storekeeper

Admin --|> Manager
Manager --|> Storekeeper

rectangle "Phân hệ kiểm kê và điều chỉnh" {
    usecase "Khởi tạo phiên kiểm kê" as UC1
    usecase "Nhập số lượng đếm thực tế\\n(theo sản phẩm / nhóm hàng / serial)" as UC2
    usecase "Duyệt kết quả kiểm kê" as UC3
    usecase "Lập chứng từ điều chỉnh tồn\\n(sinh StockIn/StockOut Adjustment)" as UC4
    usecase "Tham chiếu chứng từ nguồn\\nđể đảo hoặc sửa nghiệp vụ" as UC5
    usecase "Duyệt chứng từ điều chỉnh" as UC6
    usecase "Ghi sổ điều chỉnh tồn" as UC7
}

Storekeeper --> UC1
Storekeeper --> UC4
Storekeeper --> UC7

Manager --> UC3
Manager --> UC6

UC1 ..> UC2 : <<include>>
UC4 ..> UC5 : <<include>>

@enduml""",

    "UseCase_BaoHanh.puml": """@startuml UseCase_BaoHanh
left to right direction
skinparam packageStyle rect

actor "Quản trị viên" as Admin
actor "Quản lý" as Manager
actor "Nhân viên bảo hành" as Technician
actor "Nhân viên kho" as Storekeeper
actor "Nhân viên bán hàng" as Salesman

Admin --|> Manager
Manager --|> Technician
Manager --|> Storekeeper
Manager --|> Salesman

rectangle "Phân hệ bảo hành" {
    usecase "Tra cứu serial đã bán\\nvà tình trạng bảo hành" as UC1
    usecase "Kiểm tra quyền bảo hành\\nvà claim đang mở" as UC2
    usecase "Tạo hồ sơ bảo hành" as UC3
    usecase "Ghi nhận kết quả kiểm tra kỹ thuật" as UC4
    usecase "Phê duyệt quyết định đặc biệt\\n(đổi mới hoặc từ chối)" as UC5
    usecase "Gửi hãng và ghi nhận kết quả" as UC6
    usecase "Xuất serial thay thế" as UC7
    usecase "Trả khách và đóng hồ sơ" as UC8
}

Technician --> UC3
Technician --> UC4
Technician --> UC6
Technician --> UC8

Storekeeper --> UC7
Salesman --> UC1
Manager --> UC5

UC3 ..> UC1 : <<include>>
UC3 ..> UC2 : <<include>>

@enduml""",

    "Activity_NhapKho_GhiSo.puml": """@startuml Activity_NhapKho_GhiSo
title Sơ đồ hoạt động Ghi sổ phiếu nhập kho (Rút gọn)

start
:Lập phiếu nhập kho nháp (Draft);
:Kiểm tra thông tin bắt buộc và hợp lệ;

if (Thông tin hợp lệ?) then (Không)
  :Báo lỗi nhập liệu;
  stop
else (Có)
  :Duyệt phiếu nhập kho;
  :Bắt đầu ghi sổ (Post);
  
  partition "Ghi sổ & Cập nhật kho" {
    :Quy đổi số lượng giao dịch về đơn vị cơ sở;
    
    if (Có dòng hàng quản lý Serial?) then (Có)
      :Kiểm tra serial không trùng lặp và chưa tồn tại;
      if (Serial hợp lệ?) then (Không)
        :Báo lỗi serial;
        stop
      else (Có)
        :Tạo/Cập nhật các ProductSerial ở trạng thái 'InStock';
      endif
    endif
    
    :Cộng số lượng tồn kho (StockBalance);
    :Ghi nhận thẻ kho (StockLedger) & Audit Log;
    :Chuyển trạng thái phiếu sang 'Đã ghi sổ' (Posted);
  }
  
  :Thông báo ghi sổ thành công;
  stop
endif
@enduml""",

    "Activity_ImportTonDauKy_ExcelCsv.puml": """@startuml Activity_ImportTonDauKy_ExcelCsv
title Activity nhập tồn đầu kỳ từ Excel/CSV

start
:Chọn file Excel/CSV theo mẫu tồn đầu kỳ;
:Đọc file và ánh xạ (map) cột dữ liệu;
:Kiểm tra mã sản phẩm, đơn vị, số lượng và serial;
if (Dữ liệu hợp lệ?) then (Không)
  :Hiển thị xem trước lỗi theo dòng\\nvà yêu cầu chỉnh sửa file;
  stop
else (Có)
  :Gán WarehouseId = kho mặc định;
  :Bắt đầu transaction database;
  :Sinh StockIn loại OpeningBalance (trạng thái Posted);
  :Sinh StockInLine theo từng dòng hàng;
  if (Có dòng hàng quản lý serial?) then (Có)
    :Tạo ProductSerial với trạng thái InStock\\nvà gán CurrentWarehouseId = kho mặc định;
  else (Không)
  endif
  :Cập nhật StockBalance (tăng OnHandQuantity và AvailableQuantity);
  :Ghi StockLedger và AuditLog;
  :Commit transaction database;
  :Thông báo nhập tồn đầu kỳ thành công;
  stop
endif
@enduml""",

    "Activity_XuatKho_GhiSo.puml": """@startuml Activity_XuatKho_GhiSo
title Sơ đồ hoạt động Ghi sổ phiếu xuất kho (Rút gọn)

start
:Lập phiếu xuất kho nháp (Draft);
:Kiểm tra thông tin bắt buộc và hợp lệ;

if (Thông tin hợp lệ?) then (Không)
  :Báo lỗi nhập liệu;
  stop
else (Có)
  :Duyệt phiếu xuất kho;
  :Bắt đầu ghi sổ (Post);
  
  partition "Ghi sổ & Cập nhật kho" {
    :Quy đổi số lượng giao dịch về đơn vị cơ sở;
    :Kiểm tra tồn kho khả dụng;
    
    if (Đủ tồn kho?) then (Không)
      :Báo lỗi không đủ tồn kho;
      stop
    else (Có)
      if (Có quản lý Serial?) then (Có)
        :Kiểm tra và cập nhật trạng thái các Serial sang 'Sold';
        if (Có bảo hành?) then (Có)
          :Tạo thông tin bảo hành (WarrantyCoverage);
        endif
      endif
      
      :Trừ số lượng tồn kho (StockBalance);
      :Ghi nhận thẻ kho (StockLedger) & Audit Log;
      :Chuyển trạng thái phiếu sang 'Đã ghi sổ' (Posted);
    endif
  }
  
  :Thông báo xuất kho thành công;
  stop
endif
@enduml""",

    "Activity_KiemKe_DieuChinh.puml": """@startuml Activity_KiemKe_DieuChinh
title Activity kiểm kê và điều chỉnh tồn kho

start
:Khởi tạo phiên kiểm kê (StockCountSession);
:Gán WarehouseId = kho mặc định;
:Chọn phạm vi kiểm kê (sản phẩm / nhóm hàng / serial);
:Nhập số lượng đếm thực tế;
:Tính toán chênh lệch (VarianceQuantity = Counted - System);
if (Có chênh lệch?) then (Không)
  :Cập nhật phiên kiểm kê sang hoàn thành\\n(Không sinh điều chỉnh);
  stop
else (Có)
  :Xác nhận xử lý chênh lệch;
  :Bắt đầu tạo chứng từ điều chỉnh (Draft);\\n- Nếu Variance > 0: Sinh StockIn (PurposeCode = Adjustment)\\n- Nếu Variance < 0: Sinh StockOut (PurposeCode = Adjustment, gán Khách hàng mặc định);
  :Gửi duyệt các chứng từ điều chỉnh;
  :Quản lý phê duyệt các chứng từ điều chỉnh;
  if (Được duyệt?) then (Không)
    :Trả chứng từ về Draft để chỉnh sửa;
    stop
  else (Có)
    :Bắt đầu transaction database;
    :Sắp xếp các dòng hàng theo ProductId tăng dần;
    :Khóa StockBalance theo thứ tự ProductId;
    :Cập nhật StockBalance (tăng/giảm theo chênh lệch);
    if (Có sản phẩm quản lý serial?) then (Có)
      :Cập nhật ProductSerial theo trạng thái nghiệp vụ mới;
    else (Không)
    endif
    :Ghi StockLedger và AuditLog;
    :Cập nhật trạng thái chứng từ điều chỉnh sang Posted;
    :Commit transaction database;
    :Cập nhật phiên kiểm kê sang hoàn thành (hoàn tất);
    :Thông báo xử lý chênh lệch thành công;
    stop
  endif
endif
@enduml""",

    "Activity_BaoHanh_DoiMoi.puml": """@startuml Activity_BaoHanh_DoiMoi
title Sơ đồ hoạt động Xử lý bảo hành và Đổi mới sản phẩm (Rút gọn)

start
:Tiếp nhận thiết bị bảo hành từ khách hàng;
:Nhập số Serial và kiểm tra hạn bảo hành;

if (Hợp lệ bảo hành?) then (Không)
  :Thông báo từ chối bảo hành & Trả thiết bị;
  stop
else (Có)
  :Tạo hồ sơ bảo hành (WarrantyClaim);
  :Kỹ thuật viên kiểm tra lỗi;
  
  if (Sửa chữa được?) then (Có)
    :Tiến hành sửa chữa thiết bị;
    :Trả lại thiết bị đã sửa cho khách;
  else (Không - Cần đổi mới)
    :Trình phương án Đổi mới sản phẩm;
    if (Được phê duyệt?) then (Không)
      :Trả máy cũ hoặc xử lý khác;
    else (Có)
      partition "Quy trình Đổi mới hàng" {
        :Kiểm tra tồn kho sản phẩm thay thế;
        if (Đủ tồn kho?) then (Không)
          :Giữ hồ sơ mở để chờ bổ sung kho;
          stop
        else (Có)
          :Thu hồi serial cũ (ReturnedToManufacturer);
          :Xuất kho thiết bị thay thế (WarrantyReplacement);
          :Tạo WarrantyCoverage mới (kế thừa hạn bảo hành);
          :Giao thiết bị thay thế cho khách hàng;
        endif
      }
    endif
  endif
  
  :Cập nhật hồ sơ bảo hành sang 'Đã đóng' (Closed);
  stop
endif
@enduml""",

    # 4. Sequence Diagrams
    "Sequence_DangNhap.puml": """@startuml Sequence_DangNhap
actor User as "Người dùng"
participant UI as "LoginView"
participant VM as "LoginViewModel"
participant Auth as "AuthService"
participant UserRepo as "AppUserRepository"
participant AuditRepo as "AuditLogRepository"
database DB as "SQL Server"

User -> UI : Nhập username và password
UI -> VM : Login(request)
VM -> VM : Validate required fields
alt Thiếu username hoặc password
    VM --> UI : Hiển thị lỗi bắt buộc nhập đủ
else Đủ dữ liệu
    VM -> Auth : Login(request)
    Auth -> UserRepo : FindByUsername(username)
    UserRepo -> DB : SELECT AppUser by Username
    DB --> UserRepo : User data hoặc null
    UserRepo --> Auth : User data

    alt Username không tồn tại
        Auth -> AuditRepo : WriteAuditLog(LoginFailedUnknownUser, SystemServiceAccount)
        AuditRepo -> DB : INSERT AuditLog
        DB --> AuditRepo : Success
        Auth --> VM : Generic failure
        VM --> UI : Hiển thị "Sai tài khoản hoặc mật khẩu"
    else Tài khoản bị vô hiệu hóa (IsActive = false)
        Auth -> AuditRepo : WriteAuditLog(LoginFailed)
        AuditRepo -> DB : INSERT AuditLog
        DB --> AuditRepo : Success
        Auth --> VM : Generic failure
        VM --> UI : Hiển thị "Sai tài khoản hoặc mật khẩu"
    else Tài khoản đang bị khóa (LockoutUntil > Now)
        Auth --> VM : Locked until LockoutUntil
        VM --> UI : Hiển thị thông tin thời gian bị khóa
    else Mật khẩu không đúng
        Auth -> UserRepo : Increment FailedLoginCount + update LastFailedLoginAt
        UserRepo -> DB : UPDATE AppUser
        DB --> UserRepo : Success

        opt FailedLoginCount > 3 và < 5
            Auth --> VM : Show soft lockout warning
            VM --> UI : Hiển thị cảnh báo khóa tạm thời nếu tiếp tục sai
        end

        alt FailedLoginCount >= 10
            Auth -> UserRepo : Set LockoutUntil = now + 15 minutes
            UserRepo -> DB : UPDATE AppUser
            DB --> UserRepo : Success
            Auth -> AuditRepo : WriteAuditLog(SuspiciousLoginAttempt)
            AuditRepo -> DB : INSERT AuditLog
            DB --> AuditRepo : Success
            Auth --> VM : Locked 15 minutes
            VM --> UI : Hiển thị thời gian bị khóa
        else FailedLoginCount >= 5
            Auth -> UserRepo : Set LockoutUntil = now + 5 minutes
            UserRepo -> DB : UPDATE AppUser
            DB --> UserRepo : Success
            Auth -> AuditRepo : WriteAuditLog(LoginLocked)
            AuditRepo -> DB : INSERT AuditLog
            DB --> AuditRepo : Success
            Auth --> VM : Locked 5 minutes
            VM --> UI : Hiển thị thời gian bị khóa
        else Chưa đạt ngưỡng khóa
            Auth -> AuditRepo : WriteAuditLog(LoginFailed)
            AuditRepo -> DB : INSERT AuditLog
            DB --> AuditRepo : Success
            Auth --> VM : Generic failure
            VM --> UI : Hiển thị "Sai tài khoản hoặc mật khẩu"
        end
    else Đăng nhập thành công
        Auth -> UserRepo : Reset FailedLoginCount = 0, LockoutUntil = null, update LastLoginAt
        UserRepo -> DB : UPDATE AppUser
        DB --> UserRepo : Success

        alt MustChangePassword = true
            Auth --> VM : Require password change
            VM --> UI : Chuyển sang màn hình đổi mật khẩu bắt buộc
        else Đăng nhập bình thường
            Auth --> VM : Login success
            VM --> UI : Mở màn hình chính
        end
    end
end
@enduml""",

    "Sequence_NhapKho_GhiSo.puml": """@startuml Sequence_NhapKho_GhiSo
actor Storekeeper as "Nhân viên kho"
participant UI as "StockInView"
participant VM as "StockInViewModel"
participant Approval as "ApprovalService"
participant Authorization as "AuthorizationService"
participant Service as "InventoryService"
participant BalanceRepo as "StockBalanceRepository"
participant StockRepo as "StockInRepository"
participant SerialRepo as "SerialRepository"
participant LedgerRepo as "StockLedgerRepository"
participant AuditRepo as "AuditLogRepository"
database DB as "SQL Server"

Storekeeper -> UI : Nhập phiếu nhập
UI -> VM : SubmitStockIn(request)
VM -> Service : PostStockIn(request)
Service -> Approval : ValidateStatusTransition()
Approval --> Service : OK
Service -> Authorization : ValidatePostingPermission()
Authorization --> Service : OK
Service -> Approval : CheckApproverPosterSeparationIfEnabled()
Approval --> Service : OK

Service -> Service : ResolveDefaultWarehouse()
Service -> Service : SortLinesByProductId()
Service -> DB : BEGIN TRAN

Service -> BalanceRepo : LockProductBalances(order by ProductId asc, warehouseId = default)
BalanceRepo -> DB : SELECT ... WITH (UPDLOCK)
DB --> BalanceRepo : Current balance
BalanceRepo --> Service : Locked

Service -> Service : ConvertToBaseQuantity(lines)

alt Có dòng quản lý serial
    Service -> SerialRepo : ValidateIncomingSerials(serials)
    SerialRepo -> DB : SELECT duplicate serials
    DB --> SerialRepo : Result
    SerialRepo --> Service : Serial valid
end

Service -> StockRepo : InsertHeaderAndLines(warehouseId, purposeCode)
StockRepo -> DB : INSERT StockIn / StockInLine
DB --> StockRepo : Success

Service -> BalanceRepo : ApplyInboundBalanceChange(warehouseId)
BalanceRepo -> DB : UPDATE StockBalance
DB --> BalanceRepo : Success

alt Có dòng quản lý serial
    Service -> SerialRepo : InsertSerials(InStock, currentWarehouseId = default)
    SerialRepo -> DB : INSERT ProductSerial
    DB --> SerialRepo : Success
end

Service -> LedgerRepo : WriteInboundLedger(warehouseId)
LedgerRepo -> DB : INSERT StockLedger
DB --> LedgerRepo : Success

Service -> AuditRepo : WriteAuditLog()
AuditRepo -> DB : INSERT AuditLog
DB --> AuditRepo : Success

Service -> DB : COMMIT
Service --> VM : Success
VM --> UI : Show success
@enduml""",

    "Sequence_ImportTonDauKy_ExcelCsv.puml": """@startuml Sequence_ImportTonDauKy_ExcelCsv
actor Storekeeper as "Nhân viên kho"
participant UI as "OpeningBalanceImportView"
participant VM as "OpeningBalanceImportViewModel"
participant Import as "InitialStockImportService"
participant StockRepo as "StockInRepository"
participant BalanceRepo as "StockBalanceRepository"
participant SerialRepo as "SerialRepository"
participant LedgerRepo as "StockLedgerRepository"
participant AuditRepo as "AuditLogRepository"
database DB as "SQL Server"

Storekeeper -> UI : Chọn file Excel/CSV
UI -> VM : ImportOpeningBalance(file)
VM -> Import : ParseAndValidate(file)
Import -> Import : Validate ProductCode, UnitCode, Quantity, SerialNumber
alt Dữ liệu không hợp lệ
    Import --> VM : Validation errors by row
    VM --> UI : Show preview errors
else Dữ liệu hợp lệ
    Import -> Import : ResolveDefaultWarehouse()
    Import -> DB : BEGIN TRAN
    Import -> StockRepo : Insert StockIn(warehouseId, purposeCode = OpeningBalance, status = Posted)
    StockRepo -> DB : INSERT StockIn
    DB --> StockRepo : stockInId
    Import -> StockRepo : Insert StockInLine(stockInId, rows)
    StockRepo -> DB : INSERT StockInLine
    DB --> StockRepo : Success
    alt Có dòng quản lý serial
        Import -> SerialRepo : InsertSerials(InStock, currentWarehouseId = default)
        SerialRepo -> DB : INSERT ProductSerial
        DB --> SerialRepo : Success
    end
    Import -> BalanceRepo : ApplyInboundBalanceChange(warehouseId)
    BalanceRepo -> DB : UPDATE StockBalance
    DB --> BalanceRepo : Success
    Import -> LedgerRepo : WriteOpeningBalanceLedger(warehouseId)
    LedgerRepo -> DB : INSERT StockLedger
    DB --> LedgerRepo : Success
    Import -> AuditRepo : WriteAuditLog()
    AuditRepo -> DB : INSERT AuditLog
    DB --> AuditRepo : Success
    Import -> DB : COMMIT
    Import --> VM : Import success
    VM --> UI : Show success
end
@enduml""",

    "Sequence_XuatKho_GhiSo.puml": """@startuml Sequence_XuatKho_GhiSo
actor Storekeeper as "Nhân viên kho"
participant UI as "StockOutView"
participant VM as "StockOutViewModel"
participant Approval as "ApprovalService"
participant Authorization as "AuthorizationService"
participant Service as "InventoryService"
participant BalanceRepo as "StockBalanceRepository"
participant StockRepo as "StockOutRepository"
participant SerialRepo as "SerialRepository"
participant CoverageRepo as "WarrantyCoverageRepository"
participant LedgerRepo as "StockLedgerRepository"
participant AuditRepo as "AuditLogRepository"
database DB as "SQL Server"

Storekeeper -> UI : Nhập phiếu xuất
UI -> VM : SubmitStockOut(request)
VM -> Service : PostStockOut(request)
Service -> Approval : ValidateStatusTransition()
Approval --> Service : OK
Service -> Authorization : ValidatePostingPermission()
Authorization --> Service : OK
Service -> Approval : CheckApproverPosterSeparationIfEnabled()
Approval --> Service : OK

Service -> Service : ResolveDefaultWarehouse()
Service -> Service : SortLinesByProductId()
Service -> DB : BEGIN TRAN

Service -> BalanceRepo : LockProductBalances(order by ProductId asc, warehouseId = default)
BalanceRepo -> DB : SELECT ... WITH (UPDLOCK)
DB --> BalanceRepo : Current balance
BalanceRepo --> Service : Locked

Service -> Service : ConvertToBaseQuantity(lines)

alt Có dòng quản lý serial
    Service -> SerialRepo : LockAndValidateSelectedSerials(order by ProductSerialId asc)
    SerialRepo -> DB : SELECT serials WHERE status = InStock WITH (UPDLOCK)
    DB --> SerialRepo : Valid serials
    SerialRepo --> Service : Serial valid
end

Service -> Service : CheckAvailableQuantity(warehouseId)

alt Đủ tồn
    Service -> StockRepo : InsertHeaderAndLines(warehouseId, purposeCode)
    StockRepo -> DB : INSERT StockOut / StockOutLine
    DB --> StockRepo : Success

    Service -> BalanceRepo : ApplyOutboundBalanceChange(warehouseId)
    BalanceRepo -> DB : UPDATE StockBalance
    DB --> BalanceRepo : Success

    alt Có dòng quản lý serial
        alt PurposeCode = Sale
            Service -> SerialRepo : UpdateSerialStatus(Sold)
            SerialRepo -> DB : UPDATE ProductSerial
            DB --> SerialRepo : Success

            opt WarrantyPeriodMonths > 0
                Service -> CoverageRepo : CreateCoverageForSoldSerials(start = PostedAt, end = PostedAt + WarrantyPeriodMonths, status = Active, salesInvoiceId)
                CoverageRepo -> DB : INSERT WarrantyCoverage
                DB --> CoverageRepo : Success
            end
        else WarrantyReplacement hoặc Adjustment
            Note over Service,SerialRepo: WarrantyReplacement được xử lý riêng
        end
    end

    Service -> LedgerRepo : WriteOutboundLedger(warehouseId)
    LedgerRepo -> DB : INSERT StockLedger
    DB --> LedgerRepo : Success

    Service -> AuditRepo : WriteAuditLog()
    AuditRepo -> DB : INSERT AuditLog
    DB --> AuditRepo : Success

    Service -> DB : COMMIT
    Service --> VM : Success
    VM --> UI : Show success
else Không đủ tồn
    Service -> DB : ROLLBACK
    Service --> VM : Error not enough stock
    VM --> UI : Show error
end
@enduml""",

    "Sequence_BaoHanh_DoiMoi.puml": """@startuml Sequence_BaoHanh_DoiMoi
actor Technician as "Nhân viên bảo hành"
actor Manager as "Quản lý"
participant UI as "WarrantyView"
participant VM as "WarrantyViewModel"
participant Warranty as "WarrantyService"
participant Approval as "ApprovalService"
participant Inventory as "InventoryService"
participant StockRepo as "StockOutRepository"
participant BalanceRepo as "StockBalanceRepository"
participant CoverageRepo as "WarrantyCoverageRepository"
participant ClaimRepo as "WarrantyClaimRepository"
participant SerialRepo as "SerialRepository"
participant LedgerRepo as "StockLedgerRepository"
participant AuditRepo as "AuditLogRepository"
database DB as "SQL Server"

Technician -> UI : Nhập serial và mô tả lỗi
UI -> VM : OpenWarrantyClaim(request)
VM -> Warranty : OpenClaim(request)
Warranty -> CoverageRepo : FindActiveCoverageBySerial()
CoverageRepo -> DB : SELECT coverage by serial
DB --> CoverageRepo : Coverage data
CoverageRepo --> Warranty : Coverage found
Warranty -> Warranty : CheckWarrantyEligibility()
Warranty -> ClaimRepo : CheckOpenClaimBySerial()
ClaimRepo -> DB : SELECT open claim by serial
DB --> ClaimRepo : Open claim result

alt Đủ điều kiện và chưa có claim đang mở
    Warranty -> ClaimRepo : Insert claim(status = Checking)
    ClaimRepo -> DB : INSERT WarrantyClaim
    DB --> ClaimRepo : Success
    Warranty -> SerialRepo : Update serial = InWarrantyProcess
    SerialRepo -> DB : UPDATE ProductSerial
    DB --> SerialRepo : Success

    alt Sửa nội bộ được
        Warranty -> ClaimRepo : Update claim = WaitingDecision -> Repairing
        ClaimRepo -> DB : UPDATE WarrantyClaim
        DB --> ClaimRepo : Success
        Warranty -> SerialRepo : Update serial = WarrantyDefective
        SerialRepo -> DB : UPDATE ProductSerial
        DB --> SerialRepo : Success
        Warranty -> ClaimRepo : Update claim = Repaired
        ClaimRepo -> DB : UPDATE WarrantyClaim
        DB --> ClaimRepo : Success
        Warranty -> Warranty : Confirm customer handover
        Warranty -> ClaimRepo : Update claim = ReturnedToCustomer -> Closed
        ClaimRepo -> DB : UPDATE WarrantyClaim
        DB --> ClaimRepo : Success
        Warranty -> SerialRepo : Update serial = Sold
        SerialRepo -> DB : UPDATE ProductSerial
        DB --> SerialRepo : Success
        Warranty --> VM : Success repaired
    else Cần gửi hãng
        Warranty -> ClaimRepo : Update claim = SentToManufacturer
        ClaimRepo -> DB : UPDATE WarrantyClaim
        DB --> ClaimRepo : Success
        Warranty -> SerialRepo : Update serial = WarrantyDefective
        SerialRepo -> DB : UPDATE ProductSerial
        DB --> SerialRepo : Success
        Warranty -> ClaimRepo : Update claim = WaitingManufacturerResult -> WaitingDecision
        ClaimRepo -> DB : UPDATE WarrantyClaim
        DB --> ClaimRepo : Success

        alt Hãng sửa được
            Warranty -> ClaimRepo : Update claim = Repaired
            ClaimRepo -> DB : UPDATE WarrantyClaim
            DB --> ClaimRepo : Success
            Warranty -> Warranty : Confirm handover
            Warranty -> ClaimRepo : Update claim = ReturnedToCustomer -> Closed
            ClaimRepo -> DB : UPDATE WarrantyClaim
            DB --> ClaimRepo : Success
            Warranty -> SerialRepo : Update serial = Sold
            SerialRepo -> DB : UPDATE ProductSerial
            DB --> SerialRepo : Success
        else Hãng không sửa được, đổi mới
            Warranty -> ClaimRepo : Record manufacturer result = Replace
            ClaimRepo -> DB : UPDATE WarrantyClaim
            DB --> ClaimRepo : Success
            VM -> Manager : Trình quyết định đổi mới
            Manager --> VM : Xác nhận phê duyệt
            VM -> Warranty : RequestReplacementApproval(claimId, managerDecision)
            Warranty -> Approval : Record replacement approval
            Approval --> Warranty : Approved
            
            Warranty -> DB : BEGIN TRAN
            Warranty -> ClaimRepo : LockWarrantyClaim()
            ClaimRepo -> DB : SELECT ... WITH (UPDLOCK)
            DB --> ClaimRepo : Locked
            Warranty -> CoverageRepo : LockWarrantyCoverage()
            CoverageRepo -> DB : SELECT ... WITH (UPDLOCK)
            DB --> CoverageRepo : Locked

            Warranty -> Inventory : LockProductBalanceForReplacement(order by ProductId asc, warehouseId = default)
            Inventory -> BalanceRepo : SELECT ... WITH (UPDLOCK)
            DB --> BalanceRepo : Locked
            
            Warranty -> SerialRepo : Lock old and candidate replacement serials(order by ProductSerialId asc)
            SerialRepo -> DB : SELECT ... WITH (UPDLOCK)
            DB --> SerialRepo : Locked

            Warranty -> Inventory : CheckAvailableQuantity(warehouseId = default)
            alt Đủ tồn thay thế
                Warranty -> SerialRepo : Update old serial = ReturnedToManufacturer
                SerialRepo -> DB : UPDATE ProductSerial
                DB --> SerialRepo : Success

                Warranty -> CoverageRepo : Close old coverage = Replaced / Inactive
                CoverageRepo -> DB : UPDATE WarrantyCoverage
                DB --> CoverageRepo : Success

                Warranty -> StockRepo : Insert StockOut(Status=Approved, PurposeCode=WarrantyReplacement, WarehouseId=default, ApprovedBy=ManagerId)
                StockRepo -> DB : INSERT StockOut
                DB --> StockRepo : Success

                Warranty -> Inventory : PostWarrantyReplacementStockOut(stockOutId, PostedBy=SystemServiceAccount)
                Inventory -> BalanceRepo : UPDATE StockBalance
                DB --> BalanceRepo : Success
                
                Inventory -> SerialRepo : Update replacement serial = Replaced
                SerialRepo -> DB : UPDATE ProductSerial
                DB --> SerialRepo : Success

                Warranty -> CoverageRepo : Create replacement coverage with remaining term
                CoverageRepo -> DB : INSERT WarrantyCoverage
                DB --> CoverageRepo : Success

                Warranty -> LedgerRepo : Write warranty replacement ledger
                LedgerRepo -> DB : INSERT StockLedger
                DB --> LedgerRepo : Success

                Warranty -> AuditRepo : WriteAuditLog()
                AuditRepo -> DB : INSERT AuditLog
                DB --> AuditRepo : Success

                Warranty -> ClaimRepo : Update claim = Replaced + ReplacementSerialId + ReplacementStockOutId
                ClaimRepo -> DB : UPDATE WarrantyClaim
                DB --> ClaimRepo : Success
                
                Warranty -> DB : COMMIT
                
                Warranty -> Warranty : Confirm replacement handover
                Warranty -> DB : BEGIN TRAN
                Warranty -> ClaimRepo : Update claim = ReturnedToCustomer -> Closed
                ClaimRepo -> DB : UPDATE WarrantyClaim
                DB --> ClaimRepo : Success
                Warranty -> SerialRepo : Update replacement serial = Sold
                SerialRepo -> DB : UPDATE ProductSerial
                DB --> SerialRepo : Success
                Warranty -> DB : COMMIT
                Warranty --> VM : Success replaced
            else Không đủ tồn thay thế
                Warranty -> DB : ROLLBACK
                Warranty -> DB : BEGIN TRAN
                Warranty -> ClaimRepo : Update claim = WaitingDecision + insufficient stock note
                ClaimRepo -> DB : UPDATE WarrantyClaim
                DB --> ClaimRepo : Success
                Warranty -> DB : COMMIT
                Warranty --> VM : Need replacement stock
            end
        end
    end
end
@endluml""",

    # 5. State Diagrams
    "State_VongDoi_ChungTuKho.puml": """@startuml State_VongDoi_ChungTuKho
title Vòng đời chứng từ kho

[*] --> Draft
Draft --> PendingApproval: Gửi duyệt [Người lập]
Draft --> Cancelled: Hủy nháp [Người lập/Quản lý]

PendingApproval --> Draft: Từ chối / Yêu cầu sửa [Quản lý]
PendingApproval --> Approved: Duyệt [Quản lý]

Approved --> Posted: Ghi sổ [Nhân viên kho]
Approved --> Cancelled: Hủy trước ghi sổ [Quản lý]

Posted --> Locked: Khóa kỳ [Quản lý/Hệ thống]
Locked --> [*]
Cancelled --> [*]
@enduml""",

    "State_VongDoi_HoSoBaoHanh.puml": """@startuml State_VongDoi_HoSoBaoHanh
title Vòng đời hồ sơ bảo hành

[*] --> Checking
Checking --> WaitingDecision: Có kết luận KT nội bộ
Checking --> SentToManufacturer: Gửi hãng
SentToManufacturer --> WaitingManufacturerResult: Hãng nhận
WaitingManufacturerResult --> WaitingDecision: Có kết quả từ hãng

WaitingDecision --> Repairing: Quyết định sửa nội bộ
WaitingDecision --> Repaired: Hãng sửa xong
WaitingDecision --> Replaced: Quyết định đổi mới
WaitingDecision --> Rejected: Quyết định từ chối

Repairing --> Repaired: Sửa xong
Repaired --> ReturnedToCustomer: Trả máy đã sửa
Replaced --> ReturnedToCustomer: Trả máy đổi mới
Rejected --> ReturnedToCustomer: Trả máy từ chối

ReturnedToCustomer --> Closed: Hoàn tất
Closed --> [*]
@enduml""",

    # 6. Detailed ERD
    "ERD_QuanLyHangHoaBaoHanh_ChiTiet.puml": """@startuml ERD_QuanLyHangHoaBaoHanh_ChiTiet
title Sơ đồ ERD quản lý hàng hóa và bảo hành (Rút gọn)

entity AppUser {
    * Id : int [PK]
    --
    * Username : nvarchar(100) [UQ]
    * FullName : nvarchar(255)
    * RoleCode : nvarchar(50)
    * IsActive : bit
}

entity Category {
    * Id : int [PK]
    --
    * CategoryCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    * IsActive : bit
}

entity Brand {
    * Id : int [PK]
    --
    * BrandCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    * IsActive : bit
}

entity Unit {
    * Id : int [PK]
    --
    * UnitCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    * IsActive : bit
}

entity Supplier {
    * Id : int [PK]
    --
    * SupplierCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    * IsActive : bit
}

entity Customer {
    * Id : int [PK]
    --
    * CustomerCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    * IsActive : bit
}

entity Warehouse {
    * Id : int [PK]
    --
    * WarehouseCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    * IsDefault : bit
    * IsActive : bit
}

entity Product {
    * Id : int [PK]
    --
    * ProductCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    * CategoryId : int [FK]
    * BrandId : int [FK]
    * DefaultUnitId : int [FK]
    * IsSerialTracked : bit
    * IsActive : bit
}

entity ProductUnit {
    * Id : int [PK]
    --
    * ProductId : int [FK]
    * UnitId : int [FK]
    * ConversionFactor : decimal(18,4)
    * IsBaseUnit : bit
}

entity StockBalance {
    * Id : int [PK]
    --
    * WarehouseId : int [FK]
    * ProductId : int [FK]
    * OnHandQuantity : decimal(18,2)
    * AvailableQuantity : decimal(18,2)
}

entity StockIn {
    * Id : int [PK]
    --
    * DocumentCode : nvarchar(100) [UQ]
    SupplierId : int [FK, nullable]
    * WarehouseId : int [FK]
    * PurposeCode : nvarchar(50)
    * Status : nvarchar(50)
    * CreatedBy : int [FK]
    * CreatedAt : datetime
}

entity StockInLine {
    * Id : int [PK]
    --
    * StockInId : int [FK]
    * ProductId : int [FK]
    * UnitId : int [FK]
    * Quantity : decimal(18,2)
    * BaseQuantity : decimal(18,2)
}

entity StockOut {
    * Id : int [PK]
    --
    * DocumentCode : nvarchar(100) [UQ]
    * CustomerId : int [FK]
    * WarehouseId : int [FK]
    * PurposeCode : nvarchar(50)
    * Status : nvarchar(50)
    * CreatedBy : int [FK]
    * CreatedAt : datetime
}

entity StockOutLine {
    * Id : int [PK]
    --
    * StockOutId : int [FK]
    * ProductId : int [FK]
    * UnitId : int [FK]
    * Quantity : decimal(18,2)
    * BaseQuantity : decimal(18,2)
}

entity StockCountSession {
    * Id : int [PK]
    --
    * SessionCode : nvarchar(100) [UQ]
    * WarehouseId : int [FK]
    * Status : nvarchar(50)
    * CreatedBy : int [FK]
    * CountDate : datetime
}

entity StockCountLine {
    * Id : int [PK]
    --
    * SessionId : int [FK]
    * ProductId : int [FK]
    * SystemQuantity : decimal(18,2)
    * CountedQuantity : decimal(18,2)
    * VarianceQuantity : decimal(18,2)
}

entity ProductSerial {
    * Id : int [PK]
    --
    * ProductId : int [FK]
    * SerialNumber : nvarchar(100) [UQ]
    * CurrentStatus : nvarchar(50)
    CurrentWarehouseId : int [FK, nullable]
}

entity StockLedger {
    * Id : int [PK]
    --
    * WarehouseId : int [FK]
    * ProductId : int [FK]
    ProductSerialId : int [FK, nullable]
    * SourceDocumentType : nvarchar(100)
    * SourceDocumentId : int
    * MovementType : nvarchar(50)
    * Quantity : decimal(18,2)
}

entity PurchaseInvoice {
    * Id : int [PK]
    --
    * InvoiceCode : nvarchar(100) [UQ]
    * SupplierId : int [FK]
    StockInId : int [FK, nullable]
    * InvoiceDate : datetime
    * GrandTotal : decimal(18,2)
    * PaymentStatus : nvarchar(50)
}

entity PurchaseInvoiceLine {
    * Id : int [PK]
    --
    * PurchaseInvoiceId : int [FK]
    * ProductId : int [FK]
    * UnitId : int [FK]
    * Quantity : decimal(18,2)
    * GrandTotal : decimal(18,2)
}

entity SalesInvoice {
    * Id : int [PK]
    --
    * InvoiceCode : nvarchar(100) [UQ]
    * CustomerId : int [FK]
    StockOutId : int [FK, nullable]
    * InvoiceDate : datetime
    * GrandTotal : decimal(18,2)
    * PaymentStatus : nvarchar(50)
}

entity SalesInvoiceLine {
    * Id : int [PK]
    --
    * SalesInvoiceId : int [FK]
    * ProductId : int [FK]
    * UnitId : int [FK]
    * Quantity : decimal(18,2)
    * GrandTotal : decimal(18,2)
}

entity WarrantyCoverage {
    * Id : int [PK]
    --
    * ProductSerialId : int [FK]
    * CustomerId : int [FK]
    SalesInvoiceId : int [FK, nullable]
    * WarrantyStartDate : datetime
    * WarrantyEndDate : datetime
    * CoverageStatus : nvarchar(50)
}

entity WarrantyClaim {
    * Id : int [PK]
    --
    * ClaimCode : nvarchar(100) [UQ]
    * WarrantyCoverageId : int [FK]
    * ProductSerialId : int [FK]
    ReplacementSerialId : int [FK, nullable]
    ReplacementStockOutId : int [FK, nullable]
    * ProblemDescription : nvarchar(max)
    * Status : nvarchar(50)
    * ProcessedBy : int [FK]
}

entity AuditLog {
    * Id : int [PK]
    --
    * EntityName : nvarchar(100)
    * EntityId : int
    * ActionCode : nvarchar(100)
    PerformedBy : int [FK]
    * PerformedAt : datetime
}

Category ||--o{ Product
Brand ||--o{ Product
Unit ||--o{ Product
Product ||--o{ ProductUnit
Unit ||--o{ ProductUnit

Warehouse ||--o{ StockBalance
Product ||--o{ StockBalance

Supplier |o--o{ StockIn
Warehouse ||--o{ StockIn
StockIn ||--o{ StockInLine
Product ||--o{ StockInLine
Unit ||--o{ StockInLine

Warehouse ||--o{ StockOut
Customer ||--o{ StockOut
StockOut ||--o{ StockOutLine
Product ||--o{ StockOutLine
Unit ||--o{ StockOutLine

Warehouse ||--o{ StockCountSession
StockCountSession ||--o{ StockCountLine
Product ||--o{ StockCountLine

Supplier ||--o{ PurchaseInvoice
StockIn |o--o| PurchaseInvoice
PurchaseInvoice ||--o{ PurchaseInvoiceLine
Product ||--o{ PurchaseInvoiceLine
Unit ||--o{ PurchaseInvoiceLine
StockInLine |o--o{ PurchaseInvoiceLine

Customer ||--o{ SalesInvoice
StockOut |o--o| SalesInvoice
SalesInvoice ||--o{ SalesInvoiceLine
Product ||--o{ SalesInvoiceLine
Unit ||--o{ SalesInvoiceLine
StockOutLine |o--o{ SalesInvoiceLine

Warehouse |o--o{ ProductSerial
Product ||--o{ ProductSerial
StockInLine ||--o{ ProductSerial
StockOutLine |o--o{ ProductSerial

Warehouse ||--o{ StockLedger
Product ||--o{ StockLedger
ProductSerial |o--o{ StockLedger

ProductSerial ||--o{ WarrantyCoverage
Customer ||--o{ WarrantyCoverage
SalesInvoice |o--o{ WarrantyCoverage

WarrantyCoverage ||--o{ WarrantyClaim
ProductSerial ||--o{ WarrantyClaim
ProductSerial |o--o{ WarrantyClaim : replacement
StockOut |o--o| WarrantyClaim : replacement

AppUser |o--o{ AppUser : creator
AppUser ||--o{ StockIn : creator
AppUser |o--o{ StockIn : approver
AppUser |o--o{ StockIn : poster
AppUser ||--o{ StockOut : creator
AppUser |o--o{ StockOut : approver
AppUser |o--o{ StockOut : poster
AppUser ||--o{ StockCountSession : creator
AppUser |o--o{ StockCountSession : approver
AppUser |o--o{ StockCountSession : poster
AppUser ||--o{ StockLedger : poster
AppUser |o--o{ WarrantyClaim : approver
AppUser ||--o{ WarrantyClaim : processor
AppUser ||--o{ AuditLog : performer

@enduml"""
}

erd_modules = {
    # 1. Core & Catalog
    "ERD_Module_01_Core_Catalog.puml": """@startuml ERD_Module_01_Core_Catalog
title Core & Catalog Module ERD

entity Category {
    * Id : int [PK]
    --
    * CategoryCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    * IsActive : bit
}

entity Brand {
    * Id : int [PK]
    --
    * BrandCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    OriginCountry : nvarchar(100)
    * IsActive : bit
}

entity Unit {
    * Id : int [PK]
    --
    * UnitCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    * IsActive : bit
}

entity Supplier {
    * Id : int [PK]
    --
    * SupplierCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    Phone : nvarchar(50)
    Email : nvarchar(100)
    * IsActive : bit
}

entity Customer {
    * Id : int [PK]
    --
    * CustomerCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    Phone : nvarchar(50)
    Email : nvarchar(100)
    * IsActive : bit
}

entity Warehouse {
    * Id : int [PK]
    --
    * WarehouseCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    * IsDefault : bit
    * IsActive : bit
}

entity Product {
    * Id : int [PK]
    --
    * ProductCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    * CategoryId : int [FK]
    * BrandId : int [FK]
    * DefaultUnitId : int [FK]
    * DefaultPrice : decimal(18,2)
    CostPrice : decimal(18,2) [nullable]
    OriginCountry : nvarchar(100)
    * WarrantyPeriodMonths : int
    * IsSerialTracked : bit
    * IsActive : bit
}

entity ProductUnit {
    * Id : int [PK]
    --
    * ProductId : int [FK]
    * UnitId : int [FK]
    * ConversionFactor : decimal(18,4)
    * IsBaseUnit : bit
    * IsPurchaseUnit : bit
    * IsSalesUnit : bit
    --
    *UQ_ProductUnit* : UNIQUE(ProductId, UnitId)
}

Category ||--o{ Product
Brand ||--o{ Product
Unit ||--o{ Product
Product ||--o{ ProductUnit
Unit ||--o{ ProductUnit
@enduml""",

    # 2. Inventory Flow
    "ERD_Module_02_Inventory_Flow.puml": """@startuml ERD_Module_02_Inventory_Flow
title Inventory Flow Module ERD

entity AppUser {
    * Id : int [PK]
    --
    * Username : nvarchar(100)
    * RoleCode : nvarchar(50)
    * IsActive : bit
}

entity Warehouse {
    * Id : int [PK]
    --
    * WarehouseCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    * IsDefault : bit
    * IsActive : bit
}

entity Supplier {
    * Id : int [PK]
    --
    * SupplierCode : nvarchar(100)
}

entity Customer {
    * Id : int [PK]
    --
    * CustomerCode : nvarchar(100)
}

entity Product {
    * Id : int [PK]
    --
    * ProductCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
    * IsSerialTracked : bit
}

entity Unit {
    * Id : int [PK]
    --
    * UnitCode : nvarchar(100)
}

entity StockBalance {
    * Id : int [PK]
    --
    * WarehouseId : int [FK]
    * ProductId : int [FK]
    * OnHandQuantity : decimal(18,2)
    * AvailableQuantity : decimal(18,2)
    * ReservedQuantity : decimal(18,2)
    --
    *UQ_StockBalance* : UNIQUE(WarehouseId, ProductId)
}

entity StockIn {
    * Id : int [PK]
    --
    * DocumentCode : nvarchar(100) [UQ]
    SupplierId : int [FK, nullable]
    * WarehouseId : int [FK]
    * PurposeCode : nvarchar(50) [CK: Purchase, OpeningBalance, Adjustment]
    * Status : nvarchar(50)
    * CreatedBy : int [FK]
    ApprovedBy : int [FK, nullable]
    PostedBy : int [FK, nullable]
    * CreatedAt : datetime
    ApprovedAt : datetime [nullable]
    PostedAt : datetime [nullable]
}

entity StockInLine {
    * Id : int [PK]
    --
    * StockInId : int [FK]
    * ProductId : int [FK]
    * UnitId : int [FK]
    * Quantity : decimal(18,2)
    * BaseQuantity : decimal(18,2)
    * UnitPrice : decimal(18,2)
    DraftSerials : nvarchar(max)
}

entity StockOut {
    * Id : int [PK]
    --
    * DocumentCode : nvarchar(100) [UQ]
    * CustomerId : int [FK]
    * WarehouseId : int [FK]
    * PurposeCode : nvarchar(50) [CK: Sale, WarrantyReplacement, Adjustment]
    * Status : nvarchar(50)
    * CreatedBy : int [FK]
    ApprovedBy : int [FK, nullable]
    PostedBy : int [FK, nullable]
    * CreatedAt : datetime
    ApprovedAt : datetime [nullable]
    PostedAt : datetime [nullable]
}

entity StockOutLine {
    * Id : int [PK]
    --
    * StockOutId : int [FK]
    * ProductId : int [FK]
    * UnitId : int [FK]
    * Quantity : decimal(18,2)
    * BaseQuantity : decimal(18,2)
    * UnitPrice : decimal(18,2)
    DraftSerials : nvarchar(max)
}

entity StockCountSession {
    * Id : int [PK]
    --
    * SessionCode : nvarchar(100) [UQ]
    * WarehouseId : int [FK]
    * Status : nvarchar(50)
    * CreatedBy : int [FK]
    ApprovedBy : int [FK, nullable]
    PostedBy : int [FK, nullable]
    * CountDate : datetime
}

entity StockCountLine {
    * Id : int [PK]
    --
    * SessionId : int [FK]
    * ProductId : int [FK]
    * SystemQuantity : decimal(18,2)
    * CountedQuantity : decimal(18,2)
    * VarianceQuantity : decimal(18,2)
}

entity ProductSerial {
    * Id : int [PK]
    --
    * ProductId : int [FK]
    * SerialNumber : nvarchar(100) [UQ]
    * CurrentStatus : nvarchar(50)
    CurrentWarehouseId : int [FK, nullable]
}

entity StockLedger {
    * Id : int [PK]
    --
    * WarehouseId : int [FK]
    * ProductId : int [FK]
    ProductSerialId : int [FK, nullable]
    * SourceDocumentType : nvarchar(100)
    * SourceDocumentId : int
    * MovementType : nvarchar(50)
    * Quantity : decimal(18,2)
    * PostedBy : int [FK]
    * PostedAt : datetime
}

Warehouse ||--o{ StockBalance
Product ||--o{ StockBalance

Supplier |o--o{ StockIn
Warehouse ||--o{ StockIn
StockIn ||--o{ StockInLine
Product ||--o{ StockInLine
Unit ||--o{ StockInLine

Warehouse ||--o{ StockOut
Customer ||--o{ StockOut
StockOut ||--o{ StockOutLine
Product ||--o{ StockOutLine
Unit ||--o{ StockOutLine

Warehouse ||--o{ StockCountSession
StockCountSession ||--o{ StockCountLine
Product ||--o{ StockCountLine

Warehouse |o--o{ ProductSerial
Product ||--o{ ProductSerial

Warehouse ||--o{ StockLedger
Product ||--o{ StockLedger
ProductSerial |o--o{ StockLedger

AppUser ||--o{ StockIn : creator
AppUser |o--o{ StockIn : approver
AppUser |o--o{ StockIn : poster
AppUser ||--o{ StockOut : creator
AppUser |o--o{ StockOut : approver
AppUser |o--o{ StockOut : poster
AppUser ||--o{ StockLedger : poster
@enduml""",

    # 3. Invoicing
    "ERD_Module_03_Invoicing.puml": """@startuml ERD_Module_03_Invoicing
title Invoicing Module ERD

entity Supplier {
    * Id : int [PK]
    --
    * SupplierCode : nvarchar(100)
}

entity Customer {
    * Id : int [PK]
    --
    * CustomerCode : nvarchar(100)
}

entity Warehouse {
    * Id : int [PK]
    --
    * WarehouseCode : nvarchar(100)
}

entity Product {
    * Id : int [PK]
    --
    * ProductCode : nvarchar(100) [UQ]
    * DisplayName : nvarchar(255)
}

entity Unit {
    * Id : int [PK]
    --
    * UnitCode : nvarchar(100)
}

entity StockIn {
    * Id : int [PK]
    --
    * DocumentCode : nvarchar(100) [UQ]
}

entity StockInLine {
    * Id : int [PK]
    --
    * StockInId : int [FK]
    * ProductId : int [FK]
    * UnitId : int [FK]
    * Quantity : decimal(18,2)
}

entity StockOut {
    * Id : int [PK]
    --
    * DocumentCode : nvarchar(100) [UQ]
}

entity StockOutLine {
    * Id : int [PK]
    --
    * StockOutId : int [FK]
    * ProductId : int [FK]
    * UnitId : int [FK]
    * Quantity : decimal(18,2)
}

entity PurchaseInvoice {
    * Id : int [PK]
    --
    * InvoiceCode : nvarchar(100) [UQ]
    * SupplierId : int [FK]
    StockInId : int [FK, nullable]
    * InvoiceDate : datetime
    * SubTotal : decimal(18,2)
    * TaxAmount : decimal(18,2)
    * GrandTotal : decimal(18,2)
    * PaidAmount : decimal(18,2)
    * PaymentStatus : nvarchar(50)
    * DueDate : datetime
}

entity PurchaseInvoiceLine {
    * Id : int [PK]
    --
    * PurchaseInvoiceId : int [FK]
    * ProductId : int [FK]
    * UnitId : int [FK]
    StockInLineId : int [FK, nullable]
    * Quantity : decimal(18,2)
    * UnitPrice : decimal(18,2)
    * SubTotal : decimal(18,2)
    * TaxRate : decimal(18,4)
    * TaxAmount : decimal(18,2)
    * GrandTotal : decimal(18,2)
}

entity SalesInvoice {
    * Id : int [PK]
    --
    * InvoiceCode : nvarchar(100) [UQ]
    * CustomerId : int [FK]
    StockOutId : int [FK, nullable]
    * InvoiceDate : datetime
    * SubTotal : decimal(18,2)
    * TaxAmount : decimal(18,2)
    * GrandTotal : decimal(18,2)
    * PaidAmount : decimal(18,2)
    * PaymentStatus : nvarchar(50)
    * DueDate : datetime
}

entity SalesInvoiceLine {
    * Id : int [PK]
    --
    * SalesInvoiceId : int [FK]
    * ProductId : int [FK]
    * UnitId : int [FK]
    StockOutLineId : int [FK, nullable]
    * Quantity : decimal(18,2)
    * UnitPrice : decimal(18,2)
    * SubTotal : decimal(18,2)
    * TaxRate : decimal(18,4)
    * TaxAmount : decimal(18,2)
    * GrandTotal : decimal(18,2)
}

Supplier |o--o{ StockIn
Warehouse ||--o{ StockIn
StockIn ||--o{ StockInLine
Product ||--o{ StockInLine
Unit ||--o{ StockInLine

Customer ||--o{ StockOut
Warehouse ||--o{ StockOut
StockOut ||--o{ StockOutLine
Product ||--o{ StockOutLine
Unit ||--o{ StockOutLine

Supplier ||--o{ PurchaseInvoice
StockIn |o--o| PurchaseInvoice
PurchaseInvoice ||--o{ PurchaseInvoiceLine
Product ||--o{ PurchaseInvoiceLine
Unit ||--o{ PurchaseInvoiceLine
StockInLine |o--o{ PurchaseInvoiceLine

Customer ||--o{ SalesInvoice
StockOut |o--o| SalesInvoice
SalesInvoice ||--o{ SalesInvoiceLine
Product ||--o{ SalesInvoiceLine
Unit ||--o{ SalesInvoiceLine
StockOutLine |o--o{ SalesInvoiceLine
@enduml""",

    # 4. Warranty
    "ERD_Module_04_Warranty.puml": """@startuml ERD_Module_04_Warranty
title Warranty Module ERD

entity AppUser {
    * Id : int [PK]
    --
    * Username : nvarchar(100)
}

entity Customer {
    * Id : int [PK]
    --
    * CustomerCode : nvarchar(100)
}

entity Product {
    * Id : int [PK]
    --
    * ProductCode : nvarchar(100)
    * DisplayName : nvarchar(255)
}

entity ProductSerial {
    * Id : int [PK]
    --
    * ProductId : int [FK]
    * SerialNumber : nvarchar(100) [UQ]
    * CurrentStatus : nvarchar(50)
    CurrentWarehouseId : int [nullable]
}

entity StockOut {
    * Id : int [PK]
    --
    * DocumentCode : nvarchar(100)
}

entity SalesInvoice {
    * Id : int [PK]
    --
    * InvoiceCode : nvarchar(100)
}

entity WarrantyCoverage {
    * Id : int [PK]
    --
    * ProductSerialId : int [FK]
    * CustomerId : int [FK]
    SalesInvoiceId : int [FK, nullable]
    * WarrantyStartDate : datetime
    * WarrantyEndDate : datetime
    * CoverageStatus : nvarchar(50)
}

entity WarrantyClaim {
    * Id : int [PK]
    --
    * ClaimCode : nvarchar(100) [UQ]
    * WarrantyCoverageId : int [FK]
    * ProductSerialId : int [FK]
    ReplacementSerialId : int [FK, nullable]
    ReplacementStockOutId : int [FK, nullable]
    * ReceivedDate : datetime
    * ProblemDescription : nvarchar(max)
    TechnicalConclusion : nvarchar(max) [nullable]
    ManufacturerResult : nvarchar(max) [nullable]
    RejectionReason : nvarchar(max) [nullable]
    ProcessingNote : nvarchar(max) [nullable]
    * Status : nvarchar(50)
    ApprovedBy : int [FK, nullable]
    * ProcessedBy : int [FK]
    ClosedDate : datetime [nullable]
}

entity StockLedger {
    * Id : int [PK]
    --
    * ProductId : int [FK]
    ProductSerialId : int [FK, nullable]
    * Quantity : decimal(18,2)
}

Product ||--o{ ProductSerial
ProductSerial ||--o{ WarrantyCoverage
Customer ||--o{ WarrantyCoverage
SalesInvoice |o--o{ WarrantyCoverage

WarrantyCoverage ||--o{ WarrantyClaim
ProductSerial ||--o{ WarrantyClaim
ProductSerial |o--o{ WarrantyClaim : replacement
StockOut |o--o| WarrantyClaim : replacement

ProductSerial |o--o{ StockLedger
AppUser |o--o{ WarrantyClaim : approvedBy
AppUser ||--o{ WarrantyClaim : processedBy
@enduml""",

    # 5. User & Audit
    "ERD_Module_05_User_Audit.puml": """@startuml ERD_Module_05_User_Audit
title User & Audit Module ERD

entity AppUser {
    * Id : int [PK]
    --
    * Username : nvarchar(100) [UQ]
    * PasswordHash : nvarchar(max)
    * FullName : nvarchar(255)
    * RoleCode : nvarchar(50)
    * MustChangePassword : bit
    * FailedLoginCount : int
    CreatedBy : int [FK, nullable]
    * CreatedAt : datetime
    LockoutUntil : datetime [nullable]
    LastFailedLoginAt : datetime [nullable]
    LastPasswordChangedAt : datetime [nullable]
    LastLoginAt : datetime [nullable]
    * IsActive : bit
}

entity AuditLog {
    * Id : int [PK]
    --
    * EntityName : nvarchar(100)
    * EntityId : int
    * ActionCode : nvarchar(100)
    BeforeJson : nvarchar(max)
    AfterJson : nvarchar(max)
    PerformedBy : int [FK]
    * PerformedAt : datetime
}

entity StockIn {
    * Id : int [PK]
    --
    * DocumentCode : nvarchar(100)
    * CreatedBy : int [FK]
    ApprovedBy : int [FK, nullable]
    PostedBy : int [FK, nullable]
}

entity StockOut {
    * Id : int [PK]
    --
    * DocumentCode : nvarchar(100)
    * CreatedBy : int [FK]
    ApprovedBy : int [FK, nullable]
    PostedBy : int [FK, nullable]
}

AppUser |o--o{ AppUser : creator
AppUser ||--o{ AuditLog : performer
AppUser ||--o{ StockIn : creator
AppUser |o--o{ StockIn : approver
AppUser |o--o{ StockIn : poster
AppUser ||--o{ StockOut : creator
AppUser |o--o{ StockOut : approver
AppUser |o--o{ StockOut : poster
@enduml"""
}

# Write files
for name, content in diagrams.items():
    filepath = os.path.join(puml_dir, name)
    with open(filepath, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"Wrote {filepath}")

for name, content in erd_modules.items():
    filepath = os.path.join(erd_module_dir, name)
    with open(filepath, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"Wrote {filepath}")

# Compile main diagrams (SVG & PNG)
print("Compiling main diagrams using plantuml.jar to SVG...")
subprocess.run([
    "java", "-jar", ".tools/plantuml.jar", "-tsvg", 
    "-o", "../plantuml-svg", "plantuml/*.puml"
])
print("Compiling main diagrams using plantuml.jar to PNG...")
subprocess.run([
    "java", "-jar", ".tools/plantuml.jar", "-tpng", 
    "-o", "../plantuml-png", "plantuml/*.puml"
])

# Compile ERD module diagrams (SVG & PNG)
print("Compiling ERD module diagrams using plantuml.jar to SVG...")
subprocess.run([
    "java", "-jar", ".tools/plantuml.jar", "-tsvg", 
    "-o", "../plantuml-svg-erd-module", "plantuml-erd-module/*.puml"
])
print("Compiling ERD module diagrams using plantuml.jar to PNG...")
subprocess.run([
    "java", "-jar", ".tools/plantuml.jar", "-tpng", 
    "-o", "../plantuml-png-erd-module", "plantuml-erd-module/*.puml"
])

print("Finished compiling all diagrams.")
