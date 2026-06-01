# Bộ diagram quản lý hàng hóa và bảo hành

Tài liệu này là phiên bản Mermaid đồng bộ với file PlantUML và nội dung trong `Thiết kế phần mềm.txt`.

Các quyết định đã khóa trong bản này:
- Giữ mô hình vai trò cố định qua `AppUser.RoleCode`.
- Dùng mô hình `future-ready Warehouse + StockBalance + StockLedger`, nhưng phase hiện tại chỉ vận hành trên một kho mặc định ẩn khỏi UI.
- Hỗ trợ nhập tồn đầu kỳ từ `Excel/CSV` bằng `StockIn` loại `OpeningBalance`, không thêm bảng import riêng trong phase này.
- `PurchaseInvoice` và `SalesInvoice` theo dõi công nợ tổng hợp kèm `SubTotal`, `TaxAmount`, `GrandTotal`; chi tiết thương mại nằm ở `PurchaseInvoiceLine` và `SalesInvoiceLine`.
- Phase này chưa có bảng `Payment`.
- Tách rõ `WarrantyCoverage` và `WarrantyClaim`.
- Serial thay thế trong bảo hành kế thừa thời hạn bảo hành còn lại của serial cũ.
- Khi đổi mới thành công, `WarrantyCoverage` của serial cũ phải bị đóng hiệu lực trong cùng transaction trước khi tạo coverage cho serial mới.
- Activity diagram bản Mermaid giữ ở mức business-flow gọn; bản PlantUML là bản có swimlane để soi rõ trách nhiệm giữa actor, UI, service và repository.

---

## 1. Kiến trúc WPF / MVVM / SQL Server

```mermaid
flowchart LR
    subgraph Presentation["Presentation"]
        View["Views<br/>WPF XAML"]
        VM["ViewModels<br/>Commands + Validation UI"]
    end

    subgraph Application["Application"]
        Auth["AuthService"]
        Authorization["AuthorizationService"]
        Approval["ApprovalService"]
        Catalog["Catalog Service"]
        Inventory["Inventory Service"]
        Sales["Sales Service"]
        Warranty["Warranty Service"]
        Report["Reporting Service"]
    end

    subgraph Domain["Domain"]
        Entities["Entities<br/>AppUser, Warehouse, Product,<br/>StockBalance, ProductSerial,<br/>WarrantyCoverage, WarrantyClaim"]
        Rules["Business Rules<br/>Posting, Approval,<br/>Stock Locking,<br/>Warranty Eligibility"]
    end

    subgraph Infrastructure["Infrastructure"]
        Repo["Repositories"]
        Tx["Transactions + Row Locking"]
        Audit["AuditLog + StockLedger"]
        DB[("SQL Server")]
    end

    View --> VM
    VM --> Auth
    VM --> Authorization
    VM --> Approval
    VM --> Catalog
    VM --> Inventory
    VM --> Sales
    VM --> Warranty
    VM --> Report

    Auth --> Entities
    Authorization --> Rules
    Approval --> Rules
    Catalog --> Entities
    Inventory --> Entities
    Sales --> Entities
    Warranty --> Entities
    Entities --> Rules

    Auth --> Repo
    Authorization --> Repo
    Approval --> Repo
    Catalog --> Repo
    Inventory --> Repo
    Sales --> Repo
    Warranty --> Repo
    Report --> Repo
    Repo --> Tx
    Tx --> Audit
    Audit --> DB
```

---

## 2. Use case tổng thể chi tiết

Đường nét đứt ghi nhãn `kế thừa quyền` trong bản Mermaid là quy ước trình bày để người đọc dễ theo dõi phạm vi quyền; đây không phải actor generalization UML chuẩn như bản PlantUML.

```mermaid
flowchart LR
    subgraph LeftActors[" "]
        direction TB
        Admin["Quản trị viên"]
        Manager["Quản lý"]
    end

    subgraph System["Hệ thống quản lý hàng hóa và bảo hành"]
        direction TB
        UC1(["Đăng nhập /<br/>đổi mật khẩu"])
        UC6(["Quản lý xuất kho"])
        UC10(["Quản lý bảo hành"])
        UC4A(["Quản lý sản phẩm<br/>và đơn vị"])
        UC4B(["Tra cứu sản phẩm<br/>và quản lý serial<br/>nghiệp vụ kho"])
        UC5(["Quản lý nhập kho"])
        UC5A(["Nhập tồn đầu kỳ<br/>từ Excel/CSV"])
        UC7(["Quản lý hóa đơn mua"])
        UC8(["Quản lý hóa đơn bán"])
        UC9(["Kiểm kê, điều chỉnh tồn<br/>và đảo nghiệp vụ"])
        UC11(["Tra cứu tồn kho<br/>và lịch sử kho"])
        UC14(["Tạo báo cáo<br/>thống kê"])
        UC2(["Quản lý người dùng,<br/>mật khẩu tạm<br/>và gán RoleCode"])
        UC3(["Quản lý danh mục nền"])
    end

    subgraph RightActors[" "]
        direction TB
        Storekeeper["Nhân viên kho"]
        Salesman["Nhân viên bán hàng"]
        Technician["Nhân viên bảo hành"]
    end

    Admin -. kế thừa quyền .-> Manager
    Manager -. kế thừa quyền .-> Storekeeper
    Manager -. kế thừa quyền .-> Salesman
    Manager -. kế thừa quyền .-> Technician

    Admin --- UC2
    Admin --- UC3
    Admin --- UC1
    Admin --- UC4A
    Admin --- UC4B
    Admin --- UC5
    Admin --- UC5A
    Admin --- UC6
    Admin --- UC7
    Admin --- UC8
    Admin --- UC9
    Admin --- UC10
    Admin --- UC11
    Admin --- UC14
    Manager --- UC1
    Manager --- UC2
    Manager --- UC4B
    Manager --- UC5
    Manager --- UC5A
    Manager --- UC6
    Manager --- UC7
    Manager --- UC8
    Manager --- UC9
    Manager --- UC10
    Manager --- UC11
    Manager --- UC14

    Storekeeper --- UC1
    Storekeeper --- UC4B
    Storekeeper --- UC5
    Storekeeper --- UC5A
    Storekeeper --- UC6
    Storekeeper --- UC7
    Storekeeper --- UC9
    Storekeeper --- UC11
    Salesman --- UC1
    Salesman --- UC6
    Salesman --- UC8

    Technician --- UC1
    Technician --- UC10
```

---

## 2A. Use case quản trị tài khoản và danh mục

```mermaid
flowchart LR
    subgraph LeftActors[" "]
        direction TB
        Admin["Quản trị viên"]
        Manager["Quản lý"]
        Employee["Nhân viên"]
    end

    subgraph System["Phân hệ quản trị và danh mục"]
        direction TB
        subgraph AdminFlow["Quản trị tài khoản & Phân quyền"]
            UC_Auth1(["Đăng nhập hệ thống"])
            UC_Auth2(["Đổi mật khẩu"])
            UC_Auth3(["Yêu cầu đổi mật khẩu lần đầu"])
            UC_User1(["Quản lý tài khoản người dùng<br/>(Tạo mới, khóa, mở khóa)"])
            UC_User2(["Thiết lập quyền hạn & vai trò"])
            UC_Audit(["Xem nhật ký hệ thống<br/>(Audit Log)"])
        end

        subgraph CatalogFlow["Quản lý danh mục nền"]
            UC_Cat1(["Quản lý sản phẩm<br/>(Thông tin, đơn vị, serial)"])
            UC_Cat2(["Quản lý đối tượng<br/>(Khách hàng, Nhà cung cấp)"])
            UC_Cat3(["Quản lý kho hàng"])
        end
    end

    Admin -.-> Manager
    Manager -.-> Employee

    Employee --- UC_Auth1
    Employee --- UC_Auth2

    Manager --- UC_Audit
    Manager --- UC_Cat1
    Manager --- UC_Cat2
    Manager --- UC_Cat3

    Admin --- UC_User1
    Admin --- UC_User2

    UC_Auth3 -. <<extend>> .-> UC_Auth1
```

---

## 3. Use case tra cứu, tìm kiếm, sắp xếp và báo cáo

```mermaid
flowchart LR
    subgraph LeftActors[" "]
        direction TB
        Admin["Quản trị viên"]
        Manager["Quản lý"]
    end

    subgraph Inquiry["Phân hệ tra cứu và báo cáo"]
        direction TB
        UC1(["Tìm kiếm theo mã,<br/>tên, serial"])
        UC2(["Lọc theo trạng thái,<br/>ngày, đối tượng"])
        UC3(["Sắp xếp tăng / giảm<br/>theo cột"])
        UC4(["Xem tồn theo sản phẩm<br/>và serial"])
        UC5(["Xem lịch sử kho"])
        UC6(["Tra cứu serial đã bán<br/>và tình trạng bảo hành"])
        UC7(["Tra cứu coverage,<br/>claim và lịch sử bảo hành"])
        UC8(["Xem audit log và<br/>nhật ký thay đổi"])
        UC9(["Tạo báo cáo tồn kho<br/>và nhập xuất tồn"])
        UC10(["Tạo báo cáo doanh thu,<br/>công nợ, bảo hành"])
    end

    subgraph RightActors[" "]
        direction TB
        Storekeeper["Nhân viên kho"]
        Salesman["Nhân viên bán hàng"]
        Technician["Nhân viên bảo hành"]
    end

    Admin -. kế thừa quyền .-> Manager
    Manager -. kế thừa quyền .-> Storekeeper
    Manager -. kế thừa quyền .-> Salesman
    Manager -. kế thừa quyền .-> Technician

    Admin --- UC8
    Manager --- UC8
    Manager --- UC9
    Manager --- UC10

    Storekeeper --- UC1
    Storekeeper --- UC2
    Storekeeper --- UC3
    Storekeeper --- UC4
    Storekeeper --- UC5

    Salesman --- UC1
    Salesman --- UC2
    Salesman --- UC3
    Salesman --- UC6

    Technician --- UC1
    Technician --- UC2
    Technician --- UC3
    Technician --- UC7
```

---

## 4. Use case nhập xuất kho và hóa đơn

```mermaid
flowchart LR
    subgraph LeftActors[" "]
        direction TB
        Admin["Quản trị viên"]
        Manager["Quản lý"]
    end

    subgraph System["Phân hệ Nhập Xuất Kho & Hóa Đơn"]
        subgraph Inbound["Nghiệp vụ Nhập Kho"]
            UC1_in(["Lập phiếu nhập kho"])
            UC2_in(["Quét / nhập serial nhập"])
            UC3_in(["Gửi duyệt phiếu nhập"])
            UC4_in(["Duyệt phiếu nhập"])
            UC5_in(["Ghi sổ phiếu nhập"])
            UC8_in(["Nhập tồn đầu kỳ từ Excel/CSV"])
        end

        subgraph Outbound["Nghiệp vụ Xuất Kho"]
            UC1_out(["Lập phiếu xuất kho"])
            UC2_out(["Chọn / quét serial xuất"])
            UC3_out(["Gửi duyệt phiếu xuất"])
            UC4_out(["Duyệt phiếu xuất"])
            UC5_out(["Ghi sổ phiếu xuất"])
        end

        subgraph Invoicing["Nghiệp vụ Hóa Đơn"]
            UC6_in(["Lập hóa đơn mua từ phiếu nhập"])
            UC7_in(["Nhập chi tiết hóa đơn mua"])
            UC6_out(["Lập hóa đơn bán từ phiếu xuất"])
            UC7_out(["Nhập chi tiết hóa đơn bán"])
        end
    end

    subgraph RightActors[" "]
        direction TB
        Storekeeper["Nhân viên kho"]
        Salesman["Nhân viên bán hàng"]
    end

    Admin -. kế thừa quyền .-> Manager
    Manager -. kế thừa quyền .-> Storekeeper
    Manager -. kế thừa quyền .-> Salesman

    Storekeeper --- UC1_in
    Storekeeper --- UC3_in
    Storekeeper --- UC5_in
    Storekeeper --- UC8_in
    Storekeeper --- UC6_in

    Storekeeper --- UC1_out
    Storekeeper --- UC3_out
    Storekeeper --- UC5_out

    Salesman --- UC1_out
    Salesman --- UC3_out
    Salesman --- UC6_out

    Manager --- UC4_in
    Manager --- UC4_out

    UC2_in -. extend .-> UC1_in
    UC2_out -. extend .-> UC1_out
    UC6_in -. include .-> UC7_in
    UC6_out -. include .-> UC7_out
```

Ghi chú:
- `Lập`, `Duyệt`, `Ghi sổ` là ba hành vi khác nhau.
- `Quét / nhập serial` chỉ phát sinh với sản phẩm có `IsSerialTracked = true`.
- `Nhập tồn đầu kỳ từ Excel/CSV` sinh `StockIn` loại `OpeningBalance` và dùng kho mặc định đang được ẩn khỏi UI.
- Các bước nội bộ như quy đổi đơn vị, cập nhật tồn và ghi ledger được thể hiện ở activity/sequence.

---

## 6. Use case kiểm kê, điều chỉnh tồn và đảo nghiệp vụ

```mermaid
flowchart LR
    subgraph LeftActors[" "]
        direction TB
        Admin["Quản trị viên"]
        Manager["Quản lý"]
    end

    subgraph Control["Phân hệ kiểm kê và điều chỉnh"]
        direction TB
        UC1(["Khởi tạo phiên kiểm kê"])
        UC2(["Nhập số lượng đếm thực tế<br/>theo sản phẩm / nhóm hàng / serial"])
        UC3(["Duyệt kết quả kiểm kê"])
        UC4(["Lập chứng từ điều chỉnh tồn"])
        UC5(["Tham chiếu chứng từ nguồn<br/>để đảo hoặc sửa nghiệp vụ"])
        UC6(["Duyệt chứng từ điều chỉnh"])
        UC7(["Ghi sổ điều chỉnh tồn"])
    end

    subgraph RightActors[" "]
        direction TB
        Storekeeper["Nhân viên kho"]
    end

    Admin -. kế thừa quyền .-> Manager
    Manager -. kế thừa quyền .-> Storekeeper

    Storekeeper --- UC1
    Storekeeper --- UC2
    Storekeeper --- UC4
    Storekeeper --- UC7
    Manager --- UC3
    Manager --- UC6

    UC1 -. include .-> UC2
    UC4 -. include .-> UC5
```

---

## 7. Use case bảo hành

```mermaid
flowchart LR
    subgraph LeftActors[" "]
        direction TB
        Admin["Quản trị viên"]
        Manager["Quản lý"]
    end

    subgraph Warranty["Phân hệ bảo hành"]
        direction TB
        UC1(["Tra cứu serial đã bán<br/>và tình trạng bảo hành"])
        UC2(["Kiểm tra quyền bảo hành<br/>và claim đang mở"])
        UC3(["Tạo hồ sơ bảo hành"])
        UC4(["Ghi nhận kết quả<br/>kiểm tra kỹ thuật"])
        UC5(["Phê duyệt quyết định đặc biệt<br/>đổi mới hoặc từ chối"])
        UC6(["Gửi hãng và ghi nhận<br/>kết quả từ hãng"])
        UC7(["Xuất serial thay thế"])
        UC8(["Trả khách và đóng hồ sơ"])
    end

    subgraph RightActors[" "]
        direction TB
        Technician["Nhân viên bảo hành"]
        Storekeeper["Nhân viên kho"]
        Salesman["Nhân viên bán hàng"]
    end

    Admin -. kế thừa quyền .-> Manager
    Manager -. kế thừa quyền .-> Technician
    Manager -. kế thừa quyền .-> Storekeeper
    Manager -. kế thừa quyền .-> Salesman

    Technician --- UC1
    Technician --- UC2
    Technician --- UC3
    Technician --- UC4
    Technician --- UC6
    Technician --- UC8
    Storekeeper --- UC7
    Salesman --- UC1
    Manager --- UC5

    UC3 -. include .-> UC1
    UC3 -. include .-> UC2
```

Ghi chú:
- Kho chỉ tham gia khi có đổi mới và cần xuất serial thay thế.
- Nếu hãng sửa được thì máy trả thẳng cho khách, không phát sinh xuất máy mới.
- Các bước cập nhật trạng thái serial, coverage và claim được mô tả ở activity/sequence thay vì vẽ thành use case riêng.

---

## 8. Activity ghi sổ phiếu nhập

```mermaid
flowchart TD
    A([Bắt đầu]) --> B[Lập phiếu nhập kho ở trạng thái Draft]
    B --> B1[Chọn PurposeCode = Purchase hoặc OpeningBalance]
    B1 --> C[Nhập nhà cung cấp nếu là Purchase,<br/>sản phẩm, đơn vị, số lượng, đơn giá]
    C --> C1[Gán WarehouseId = kho mặc định<br/>đang bị ẩn trên UI phase này]
    C1 --> D[Kiểm tra dữ liệu bắt buộc]
    D --> E{Dữ liệu hợp lệ?}
    E -- Không --> Z1[Hiển thị lỗi dữ liệu nhập] --> END([Kết thúc])
    E -- Có --> F[Gửi duyệt phiếu nhập]
    F --> G[Chuyển trạng thái = PendingApproval]
    G --> H[Quản lý duyệt phiếu]
    H --> I{Được duyệt?}
    I -- Không --> J[Trả phiếu về Draft để chỉnh sửa] --> END
    I -- Có --> K[Bắt đầu transaction]
    K --> K1[Sắp xếp dòng theo ProductId tăng dần<br/>để giữ thứ tự khóa cố định, giảm nguy cơ deadlock]
    K1 --> L[Khóa StockBalance theo đúng thứ tự ProductId<br/>trong Warehouse mặc định]
    L --> M[Quy đổi số lượng về đơn vị cơ sở]
    M --> N{Có dòng hàng quản lý serial?}
    N -- Có --> O[Quét / nhập danh sách serial cho các dòng có serial]
    O --> P[Kiểm tra serial không trùng và chưa tồn tại]
    P --> Q{Serial hợp lệ?}
    Q -- Không --> R[Rollback transaction] --> S[Giữ trạng thái = Approved và báo lỗi] --> END
    Q -- Có --> T[Lưu phiếu nhập và chi tiết]
    N -- Không --> T
    T --> U[Áp dụng biến động nhập tại Warehouse mặc định:<br/>tăng OnHandQuantity và AvailableQuantity]
    U --> V{Có dòng hàng quản lý serial?}
    V -- Có --> W[Tạo ProductSerial trạng thái InStock<br/>và gán CurrentWarehouseId]
    V -- Không --> X[Ghi StockLedger và AuditLog]
    W --> X
    X --> Y[Cập nhật trạng thái phiếu = Posted]
    Y --> Z[Commit transaction]
    Z --> AA[Thông báo thành công]
    AA --> END
```

---

## 8A. Activity nhập tồn đầu kỳ từ Excel/CSV

```mermaid
flowchart TD
    A([Bắt đầu]) --> B[Chọn file Excel/CSV theo template tồn đầu kỳ]
    B --> C[Đọc file và map dữ liệu import]
    C --> D[Kiểm tra ProductCode, UnitCode,<br/>Quantity và SerialNumber]
    D --> E{Dữ liệu hợp lệ?}
    E -- Không --> F[Hiển thị preview lỗi theo dòng<br/>và yêu cầu sửa file] --> END([Kết thúc])
    E -- Có --> G[Gán WarehouseId = kho mặc định]
    G --> H[Bắt đầu transaction]
    H --> I[Sinh StockIn loại OpeningBalance]
    I --> J[Sinh StockInLine theo từng dòng sản phẩm]
    J --> K{Có dòng quản lý serial?}
    K -- Có --> L[Tạo ProductSerial trạng thái InStock<br/>và gán CurrentWarehouseId]
    K -- Không --> M[Cập nhật StockBalance theo ProductId + WarehouseId]
    L --> M
    M --> N[Ghi StockLedger và AuditLog]
    N --> O[Commit transaction]
    O --> P[Thông báo import thành công]
    P --> END
```

---

## 9. Activity ghi sổ phiếu xuất

```mermaid
flowchart TD
    A([Bắt đầu]) --> B[Lập phiếu xuất kho ở trạng thái Draft]
    B --> C[Nhập PurposeCode, khách hàng,<br/>sản phẩm, đơn vị, số lượng, đơn giá]
    C --> C1[Gán WarehouseId = kho mặc định<br/>đang bị ẩn trên UI phase này]
    C1 --> D[Kiểm tra dữ liệu bắt buộc]
    D --> E{Dữ liệu hợp lệ?}
    E -- Không --> Z1[Hiển thị lỗi dữ liệu nhập] --> END([Kết thúc])
    E -- Có --> F[Gửi duyệt phiếu xuất]
    F --> G[Chuyển trạng thái = PendingApproval]
    G --> H[Quản lý duyệt phiếu]
    H --> I{Được duyệt?}
    I -- Không --> J[Trả phiếu về Draft để chỉnh sửa] --> END
    I -- Có --> K[Bắt đầu transaction]
    K --> K1[Sắp xếp dòng theo ProductId tăng dần<br/>để giữ thứ tự khóa cố định, giảm nguy cơ deadlock]
    K1 --> L[Khóa StockBalance theo đúng thứ tự ProductId<br/>trong Warehouse mặc định]
    L --> M[Quy đổi số lượng về đơn vị cơ sở]
    M --> N{Có dòng hàng quản lý serial?}
    N -- Có --> O[Khóa và chọn serial trạng thái InStock<br/>theo thứ tự ProductSerialId tăng dần để giảm nguy cơ deadlock]
    O --> P[Kiểm tra số serial khớp số lượng quy đổi]
    P --> Q{Serial hợp lệ?}
    Q -- Không --> R[Rollback transaction] --> S[Giữ trạng thái = Approved và báo lỗi] --> END
    Q -- Có --> T[Kiểm tra tồn khả dụng hiện tại<br/>trong Warehouse mặc định]
    N -- Không --> T
    T --> U{Đủ tồn?}
    U -- Không --> V[Rollback transaction] --> W[Giữ trạng thái = Approved và báo lỗi không đủ tồn] --> END
    U -- Có --> X[Lưu phiếu xuất và chi tiết]
    X --> Y[Áp dụng biến động xuất: giảm OnHandQuantity và AvailableQuantity]
    Y --> Z{Có dòng hàng quản lý serial?}
    Z -- Có --> AA{PurposeCode = Sale?}
    AA -- Có --> AA1[Cập nhật ProductSerial = Sold đối với phiếu xuất bán]
    AA1 --> AA2{Có WarrantyPeriodMonths > 0?}
    AA2 -- Có --> AA3[Tạo WarrantyCoverage = Active<br/>StartDate = PostedAt<br/>EndDate = PostedAt + WarrantyPeriodMonths<br/>gắn CustomerId và SalesInvoiceId nếu có]
    AA3 --> AB[Ghi StockLedger và AuditLog]
    AA2 -- Không --> AB[Ghi StockLedger và AuditLog]
    AA -- Không --> AA4[Không cập nhật serial theo luồng xuất bán chung;<br/>WarrantyReplacement xử lý riêng ở AC-04 / SEQ-03]
    AA4 --> AB
    Z -- Không --> AB[Ghi StockLedger và AuditLog]
    AB --> AC[Cập nhật trạng thái phiếu = Posted]
    AC --> AD[Commit transaction]
    AD --> AE[Thông báo thành công]
    AE --> END
```

---

## 10. Activity kiểm kê và điều chỉnh tồn

```mermaid
flowchart TD
    A([Bắt đầu]) --> B[Khởi tạo phiên kiểm kê]
    B --> B1[Gán WarehouseId = kho mặc định]
    B1 --> C[Chọn phạm vi kiểm kê theo sản phẩm / nhóm hàng / serial]
    C --> D[Nhập số lượng hệ thống và số lượng đếm thực tế]
    D --> E[Tính chênh lệch]
    E --> F{Có chênh lệch?}
    F -- Không --> G[Đóng phiên kiểm kê không phát sinh điều chỉnh] --> END([Kết thúc])
    F -- Có --> H[Lập chứng từ điều chỉnh hoặc đảo nghiệp vụ]
    H --> I[Ghi rõ chứng từ nguồn và lý do điều chỉnh]
    I --> J[Gửi duyệt chứng từ điều chỉnh]
    J --> K[Quản lý duyệt]
    K --> L{Được duyệt?}
    L -- Không --> M[Trả chứng từ về Draft để chỉnh sửa] --> END
    L -- Có --> N[Bắt đầu transaction]
    N --> N1[Sắp xếp dòng theo ProductId tăng dần<br/>để giữ thứ tự khóa cố định, giảm nguy cơ deadlock]
    N1 --> O[Khóa StockBalance theo đúng thứ tự ProductId<br/>trong Warehouse mặc định]
    O --> P[Cập nhật StockBalance tăng hoặc giảm theo chênh lệch]
    P --> Q{Chứng từ điều chỉnh có yêu cầu cập nhật serial?}
    Q -- Có --> R[Cập nhật ProductSerial theo trạng thái nghiệp vụ mới]
    Q -- Không --> S[Ghi StockLedger và AuditLog]
    R --> S
    S --> T[Cập nhật trạng thái chứng từ = Posted]
    T --> U[Commit transaction]
    U --> V[Thông báo thành công]
    V --> END
```

---

## 11. Activity xử lý bảo hành và đổi mới

```mermaid
flowchart TD
    A([Bắt đầu]) --> B[Tiếp nhận sản phẩm bảo hành]
    B --> C[Nhập serial và mô tả lỗi]
    C --> D[Tra cứu serial đã bán]
    D --> E{Tìm thấy serial hợp lệ?}
    E -- Không --> F[Thông báo không tìm thấy serial đã bán] --> END([Kết thúc])
    E -- Có --> G[Kiểm tra quyền bảo hành còn hiệu lực]
    G --> H{Còn hạn bảo hành?}
    H -- Không --> I[Thông báo ngoài bảo hành và trả lại máy] --> END
    H -- Có --> J[Kiểm tra serial chưa có WarrantyClaim đang mở]
    J --> J1{Đã có claim đang mở?}
    J1 -- Có --> J2[Thông báo claim đang mở và dừng tạo hồ sơ] --> END
    J1 -- Không --> K[Tạo WarrantyClaim và chuyển trạng thái = Checking]
    K --> K1[Cập nhật ProductSerial = InWarrantyProcess]
    K1 --> L[Ghi nhận kết quả kiểm tra kỹ thuật]
    L --> M{Quyết định xử lý ban đầu?}
    M -- Sửa nội bộ được --> L1[Cập nhật trạng thái = WaitingDecision]
    L1 --> N[Cập nhật trạng thái = Repairing<br/>và ProductSerial = WarrantyDefective]
    N --> O[Hoàn tất sửa và cập nhật = Repaired]
    O --> P[Trả khách]
    P --> P1[Cập nhật WarrantyClaim = ReturnedToCustomer]
    P1 --> P2[Cập nhật ProductSerial = Sold]
    P2 --> Q[Cập nhật WarrantyClaim = Closed]
    Q --> END
    M -- Cần gửi hãng --> R[Gửi hãng và cập nhật = SentToManufacturer<br/>và ProductSerial = WarrantyDefective]
    R --> R1[Cập nhật trạng thái = WaitingManufacturerResult]
    R1 --> S[Nhận kết quả từ hãng]
    S --> S1[Cập nhật trạng thái = WaitingDecision]
    S1 --> T{Kết luận từ hãng / quyết định nghiệp vụ?}
    T -- Hãng sửa được --> U[Nhận máy đã sửa từ hãng<br/>và cập nhật = Repaired]
    U --> V[Trả khách]
    V --> W[Cập nhật WarrantyClaim = ReturnedToCustomer]
    W --> W1[Cập nhật ProductSerial = Sold]
    W1 --> X[Cập nhật WarrantyClaim = Closed]
    X --> END
    T -- Hãng không sửa được, đổi mới --> Y[Ghi nhận kết luận không sửa được<br/>và hãng chấp nhận đổi mới cho doanh nghiệp]
    Y --> Z[Quản lý phê duyệt đổi mới cho khách]
    Z --> AA[Bắt đầu transaction]
    AA --> AB[Khóa WarrantyClaim, WarrantyCoverage,<br/>StockBalance và serial liên quan<br/>theo ProductId rồi ProductSerialId tăng dần để giảm nguy cơ deadlock]
    AB --> AC[Kiểm tra tồn khả dụng cho serial thay thế<br/>trong Warehouse mặc định]
    AC --> AD{Đủ serial thay thế?}
    AD -- Không --> AE[Rollback transaction đổi mới]
    AE --> AF[BEGIN transaction nhỏ cập nhật claim]
    AF --> AF1[Cập nhật claim = WaitingDecision<br/>và ghi chú thiếu hàng thay thế]
    AF1 --> AF2[COMMIT transaction nhỏ cập nhật claim]
    AF2 --> AG_NOTE[Hồ sơ vẫn mở để chờ nhập thêm hàng<br/>hoặc quyết định nghiệp vụ khác]
    AG_NOTE --> END
    AD -- Có --> AG[Cập nhật serial cũ = ReturnedToManufacturer<br/>và ghi nhận gửi hãng đổi mới]
    AG --> AH[Đóng WarrantyCoverage cũ<br/>CoverageStatus = Replaced hoặc Inactive]
    AH --> AI[Sinh StockOut WarrantyReplacement<br/>ở trạng thái Approved<br/>WarehouseId = kho mặc định<br/>ApprovedBy = Manager]
    AI --> AJ[Ghi sổ StockOut WarrantyReplacement<br/>PostedBy = SystemServiceAccount]
    AJ --> AJ1[Note: ngoại lệ hợp lệ của rule<br/>tách approver/poster]
    AJ1 --> AK[Cập nhật serial mới = Replaced]
    AK --> AL[Tạo hoặc cập nhật WarrantyCoverage<br/>cho serial mới theo thời hạn còn lại]
    AL --> AM[Ghi StockLedger và AuditLog]
    AM --> AN[Cập nhật WarrantyClaim = Replaced<br/>và lưu ReplacementSerialId + ReplacementStockOutId]
    AN --> AO[Commit transaction đổi mới]
    AO --> AP_CLOSE[Trả serial thay thế cho khách]
    AP_CLOSE --> AQ_TX[BEGIN transaction nhỏ xác nhận giao nhận]
    AQ_TX --> AQ1[Cập nhật WarrantyClaim = ReturnedToCustomer]
    AQ1 --> AQ2[Cập nhật serial thay thế = Sold]
    AQ2 --> AQ3[Cập nhật WarrantyClaim = Closed]
    AQ3 --> AQ4[COMMIT transaction nhỏ xác nhận giao nhận]
    AQ4 --> AQ_CLOSE[Đóng hồ sơ]
    AQ_CLOSE --> END
    T -- Từ chối --> AO_REJECT[Quản lý phê duyệt từ chối]
    M -- Từ chối --> AO_WAIT[Cập nhật trạng thái = WaitingDecision]
    AO_WAIT --> AO_REJECT
    AO_REJECT --> AP_REJECT[Cập nhật trạng thái = Rejected]
    AP_REJECT --> AQ_REJECT[Lưu lý do từ chối]
    AQ_REJECT --> AR_REJECT[Trả lại máy cho khách]
    AR_REJECT --> AS_REJECT[Cập nhật WarrantyClaim = ReturnedToCustomer]
    AS_REJECT --> AT_REJECT[Cập nhật ProductSerial = Sold]
    AT_REJECT --> AU_REJECT[Cập nhật WarrantyClaim = Closed]
    AU_REJECT --> AV_REJECT[Đóng hồ sơ]
    AV_REJECT --> END
```

---

## 12. Sequence đăng nhập

```mermaid
sequenceDiagram
    actor User as Người dùng
    participant UI as LoginView
    participant VM as LoginViewModel
    participant Auth as AuthService
    participant UserRepo as AppUserRepository
    participant AuditRepo as AuditLogRepository
    participant DB as SQL Server

    User->>UI: Nhập username và password
    UI->>VM: Login(request)
    VM->>VM: Validate required fields

    alt Thiếu username hoặc password
        VM-->>UI: Hiển thị lỗi bắt buộc nhập đủ 2 trường
    else Đủ dữ liệu
        VM->>Auth: Login(request)
        Auth->>UserRepo: FindByUsername(username)
        UserRepo->>DB: SELECT AppUser by Username
        DB-->>UserRepo: User or null
        UserRepo-->>Auth: User data

        alt Username không tồn tại
            Auth->>AuditRepo: WriteAuditLog(LoginFailedUnknownUser, performedBy = SystemServiceAccount)
            AuditRepo->>DB: INSERT AuditLog
            DB-->>AuditRepo: Success
            Auth-->>VM: Generic failure
            VM-->>UI: Hiển thị "Sai tài khoản hoặc mật khẩu"
        else Tài khoản bị vô hiệu hóa
            Auth->>AuditRepo: WriteAuditLog(LoginFailed)
            AuditRepo->>DB: INSERT AuditLog
            DB-->>AuditRepo: Success
            Auth-->>VM: Generic failure
            VM-->>UI: Hiển thị "Sai tài khoản hoặc mật khẩu"
        else Tài khoản đang bị khóa
            Auth-->>VM: Locked until LockoutUntil
            VM-->>UI: Hiển thị thời gian còn bị khóa
        else Mật khẩu không đúng
            Auth->>UserRepo: Increment FailedLoginCount + update LastFailedLoginAt
            UserRepo->>DB: UPDATE AppUser
            DB-->>UserRepo: Success

            opt FailedLoginCount > 3 và < 5
                Auth-->>VM: Show soft lockout warning
                VM-->>UI: Hiển thị cảnh báo nhỏ "Nhập sai tên đăng nhập/mật khẩu liên tiếp sẽ bị khóa tài khoản tạm thời"
            end

            alt FailedLoginCount >= 10
                Auth->>UserRepo: Set LockoutUntil = now + 15 minutes
                UserRepo->>DB: UPDATE AppUser
                DB-->>UserRepo: Success
                Auth->>AuditRepo: WriteAuditLog(SuspiciousLoginAttempt)
                AuditRepo->>DB: INSERT AuditLog
                DB-->>AuditRepo: Success
                Note over Auth,AuditRepo: Quản trị viên xem cảnh báo qua AuditLog hoặc audit viewer
                Auth-->>VM: Locked 15 minutes
                VM-->>UI: Hiển thị thời gian bị khóa
            else FailedLoginCount >= 5
                Auth->>UserRepo: Set LockoutUntil = now + 5 minutes
                UserRepo->>DB: UPDATE AppUser
                DB-->>UserRepo: Success
                Auth->>AuditRepo: WriteAuditLog(LoginLocked)
                AuditRepo->>DB: INSERT AuditLog
                DB-->>AuditRepo: Success
                Auth-->>VM: Locked 5 minutes
                VM-->>UI: Hiển thị thời gian bị khóa
            else Chưa đạt ngưỡng khóa
                Auth->>AuditRepo: WriteAuditLog(LoginFailed)
                AuditRepo->>DB: INSERT AuditLog
                DB-->>AuditRepo: Success
                Auth-->>VM: Generic failure
                VM-->>UI: Hiển thị "Sai tài khoản hoặc mật khẩu"
            end
        else Đăng nhập thành công
            Auth->>UserRepo: Reset FailedLoginCount = 0, LockoutUntil = null, update LastLoginAt
            UserRepo->>DB: UPDATE AppUser
            DB-->>UserRepo: Success

            alt MustChangePassword = true
                Auth-->>VM: Require password change
                VM-->>UI: Chuyển sang màn hình đổi mật khẩu bắt buộc
            else Đăng nhập bình thường
                Auth-->>VM: Login success
                VM-->>UI: Mở màn hình chính
            end
        end
    end
```

---

## 13. Sequence ghi sổ phiếu nhập

```mermaid
sequenceDiagram
    actor Storekeeper as Nhân viên kho
    participant UI as StockInView
    participant VM as StockInViewModel
    participant Approval as ApprovalService
    participant Authorization as AuthorizationService
    participant Service as InventoryService
    participant BalanceRepo as StockBalanceRepository
    participant StockRepo as StockInRepository
    participant SerialRepo as SerialRepository
    participant CoverageRepo as WarrantyCoverageRepository
    participant LedgerRepo as StockLedgerRepository
    participant AuditRepo as AuditLogRepository
    participant DB as SQL Server

    Storekeeper->>UI: Nhập phiếu nhập
    UI->>VM: SubmitStockIn(request)
    VM->>Service: PostStockIn(request)
    Service->>Approval: ValidateStatusTransition()
    Approval-->>Service: OK
    Service->>Authorization: ValidatePostingPermission()
    Authorization-->>Service: OK
    Service->>Approval: CheckApproverPosterSeparationIfEnabled()
    Approval-->>Service: OK
    Note over Service,Approval: Nếu validation hoặc policy fail thì dừng trước BEGIN TRAN và trả lỗi nghiệp vụ cho UI
    Service->>Service: ResolveDefaultWarehouse()
    Service->>Service: SortLinesByProductId()
    Service->>DB: BEGIN TRAN
    Service->>BalanceRepo: LockProductBalances(order by ProductId asc, warehouseId = default)
    Note over Service,BalanceRepo: Giữ thứ tự khóa ProductId tăng dần trong cùng Warehouse để giảm nguy cơ deadlock khi nhiều transaction ghi sổ đồng thời
    BalanceRepo->>DB: SELECT ... WITH (UPDLOCK)
    DB-->>BalanceRepo: Current balance
    BalanceRepo-->>Service: Locked
    Service->>Service: ConvertToBaseQuantity(lines)

    alt Có dòng quản lý serial
        Service->>SerialRepo: ValidateIncomingSerials(serials)
        SerialRepo->>DB: SELECT duplicate serials
        DB-->>SerialRepo: Result
        SerialRepo-->>Service: Serial valid
    end

    Service->>StockRepo: InsertHeaderAndLines(warehouseId, purposeCode)
    StockRepo->>DB: INSERT StockIn / StockInLine
    DB-->>StockRepo: Success

    Service->>BalanceRepo: ApplyInboundBalanceChange(warehouseId)
    BalanceRepo->>DB: UPDATE StockBalance
    DB-->>BalanceRepo: Success

    alt Có dòng quản lý serial
        Service->>SerialRepo: InsertSerials(InStock, currentWarehouseId = default)
        SerialRepo->>DB: INSERT ProductSerial
        DB-->>SerialRepo: Success
    end

    Service->>LedgerRepo: WriteInboundLedger(warehouseId)
    LedgerRepo->>DB: INSERT StockLedger
    DB-->>LedgerRepo: Success

    Service->>AuditRepo: WriteAuditLog()
    AuditRepo->>DB: INSERT AuditLog
    DB-->>AuditRepo: Success

    Service->>DB: COMMIT
    Service-->>VM: Success
    VM-->>UI: Show success
    Note over Service,DB: On any exception -> ROLLBACK transaction
```

---

## 13A. Sequence nhập tồn đầu kỳ từ Excel/CSV

```mermaid
sequenceDiagram
    actor Storekeeper as Nhân viên kho
    participant UI as OpeningBalanceImportView
    participant VM as OpeningBalanceImportViewModel
    participant Import as InitialStockImportService
    participant StockRepo as StockInRepository
    participant BalanceRepo as StockBalanceRepository
    participant SerialRepo as SerialRepository
    participant LedgerRepo as StockLedgerRepository
    participant AuditRepo as AuditLogRepository
    participant DB as SQL Server

    Storekeeper->>UI: Chọn file Excel/CSV
    UI->>VM: ImportOpeningBalance(file)
    VM->>Import: ParseAndValidate(file)
    Import->>Import: Validate ProductCode, UnitCode, Quantity, SerialNumber
    alt Dữ liệu không hợp lệ
        Import-->>VM: Validation errors by row
        VM-->>UI: Show preview errors
    else Dữ liệu hợp lệ
        Import->>Import: ResolveDefaultWarehouse()
        Import->>DB: BEGIN TRAN
        Import->>StockRepo: Insert StockIn(warehouseId, purposeCode = OpeningBalance, status = Posted)
        StockRepo->>DB: INSERT StockIn
        DB-->>StockRepo: stockInId
        Import->>StockRepo: Insert StockInLine(stockInId, rows)
        StockRepo->>DB: INSERT StockInLine
        DB-->>StockRepo: Success
        alt Có dòng quản lý serial
            Import->>SerialRepo: InsertSerials(InStock, currentWarehouseId = default)
            SerialRepo->>DB: INSERT ProductSerial
            DB-->>SerialRepo: Success
        end
        Import->>BalanceRepo: ApplyInboundBalanceChange(warehouseId)
        BalanceRepo->>DB: UPDATE StockBalance
        DB-->>BalanceRepo: Success
        Import->>LedgerRepo: WriteOpeningBalanceLedger(warehouseId)
        LedgerRepo->>DB: INSERT StockLedger
        DB-->>LedgerRepo: Success
        Import->>AuditRepo: WriteAuditLog()
        AuditRepo->>DB: INSERT AuditLog
        DB-->>AuditRepo: Success
        Import->>DB: COMMIT
        Import-->>VM: Import success
        VM-->>UI: Show success
    end
    Note over Import,DB: On any exception -> ROLLBACK transaction
```

---

## 14. Sequence ghi sổ phiếu xuất

```mermaid
sequenceDiagram
    actor Storekeeper as Nhân viên kho
    participant UI as StockOutView
    participant VM as StockOutViewModel
    participant Approval as ApprovalService
    participant Authorization as AuthorizationService
    participant Service as InventoryService
    participant BalanceRepo as StockBalanceRepository
    participant StockRepo as StockOutRepository
    participant SerialRepo as SerialRepository
    participant CoverageRepo as WarrantyCoverageRepository
    participant LedgerRepo as StockLedgerRepository
    participant AuditRepo as AuditLogRepository
    participant DB as SQL Server

    Storekeeper->>UI: Nhập phiếu xuất
    UI->>VM: SubmitStockOut(request)
    VM->>Service: PostStockOut(request)
    Service->>Approval: ValidateStatusTransition()
    Approval-->>Service: OK
    Service->>Authorization: ValidatePostingPermission()
    Authorization-->>Service: OK
    Service->>Approval: CheckApproverPosterSeparationIfEnabled()
    Approval-->>Service: OK
    Note over Service,Approval: Nếu validation hoặc policy fail thì dừng trước BEGIN TRAN và trả lỗi nghiệp vụ cho UI
    Service->>Service: ResolveDefaultWarehouse()
    Service->>Service: SortLinesByProductId()
    Service->>DB: BEGIN TRAN
    Service->>BalanceRepo: LockProductBalances(order by ProductId asc, warehouseId = default)
    Note over Service,BalanceRepo: Luôn khóa StockBalance theo ProductId tăng dần trong cùng Warehouse để giữ thứ tự cố định giữa các transaction
    BalanceRepo->>DB: SELECT ... WITH (UPDLOCK)
    DB-->>BalanceRepo: Current balance
    BalanceRepo-->>Service: Locked
    Service->>Service: ConvertToBaseQuantity(lines)

    alt Có dòng quản lý serial
        Service->>SerialRepo: LockAndValidateSelectedSerials(order by ProductSerialId asc)
        Note over Service,SerialRepo: Với serial, tiếp tục khóa theo ProductSerialId tăng dần để tránh deadlock khi nhiều người xuất cùng lúc
        SerialRepo->>DB: SELECT serials WHERE status = InStock
        DB-->>SerialRepo: Valid serials
        SerialRepo-->>Service: Serial valid
    end

    Service->>Service: CheckAvailableQuantity(warehouseId)

    alt Đủ tồn
        Service->>StockRepo: InsertHeaderAndLines(warehouseId, purposeCode)
        StockRepo->>DB: INSERT StockOut / StockOutLine
        DB-->>StockRepo: Success

        Service->>BalanceRepo: ApplyOutboundBalanceChange(warehouseId)
        BalanceRepo->>DB: UPDATE StockBalance
        DB-->>BalanceRepo: Success

        alt Có dòng quản lý serial
            alt PurposeCode = Sale
                Service->>SerialRepo: UpdateSerialStatus(Sold)
                SerialRepo->>DB: UPDATE ProductSerial
                DB-->>SerialRepo: Success

                opt WarrantyPeriodMonths > 0
                    Service->>CoverageRepo: CreateCoverageForSoldSerials(start = PostedAt, end = PostedAt + WarrantyPeriodMonths, status = Active, salesInvoiceId nullable)
                    CoverageRepo->>DB: INSERT WarrantyCoverage
                    DB-->>CoverageRepo: Success
                end
            else WarrantyReplacement hoặc purpose khác
                Note over Service,SerialRepo: WarrantyReplacement xử lý ở SEQ-03
            end
        end

        Service->>LedgerRepo: WriteOutboundLedger(warehouseId)
        LedgerRepo->>DB: INSERT StockLedger
        DB-->>LedgerRepo: Success

        Service->>AuditRepo: WriteAuditLog()
        AuditRepo->>DB: INSERT AuditLog
        DB-->>AuditRepo: Success

        Service->>DB: COMMIT
        Service-->>VM: Success
        VM-->>UI: Show success
    else Không đủ tồn
        Service->>DB: ROLLBACK
        Service-->>VM: Error not enough stock
        VM-->>UI: Show error
    end
    Note over Service,DB: On any exception -> ROLLBACK transaction
```

---

## 15. Sequence bảo hành đổi mới

```mermaid
sequenceDiagram
    actor Technician as Nhân viên bảo hành
    actor Manager as Quản lý
    participant UI as WarrantyView
    participant VM as WarrantyViewModel
    participant Warranty as WarrantyService
    participant Approval as ApprovalService
    participant Inventory as InventoryService
    participant StockRepo as StockOutRepository
    participant BalanceRepo as StockBalanceRepository
    participant CoverageRepo as WarrantyCoverageRepository
    participant ClaimRepo as WarrantyClaimRepository
    participant SerialRepo as SerialRepository
    participant LedgerRepo as StockLedgerRepository
    participant AuditRepo as AuditLogRepository
    participant DB as SQL Server

    Technician->>UI: Nhập serial và mô tả lỗi
    UI->>VM: OpenWarrantyClaim(request)
    VM->>Warranty: OpenClaim(request)
    Warranty->>CoverageRepo: FindActiveCoverageBySerial()
    CoverageRepo->>DB: SELECT coverage by serial
    DB-->>CoverageRepo: Coverage data
    CoverageRepo-->>Warranty: Coverage found
    Warranty->>Warranty: CheckWarrantyEligibility()
    Warranty->>ClaimRepo: CheckOpenClaimBySerial()
    ClaimRepo->>DB: SELECT open claim by serial
    DB-->>ClaimRepo: Open claim result

    alt Đủ điều kiện bảo hành
        alt Chưa có claim đang mở
                Warranty->>ClaimRepo: Insert claim(status = Checking)
                ClaimRepo->>DB: INSERT WarrantyClaim
                DB-->>ClaimRepo: Success
                Warranty->>SerialRepo: Update serial = InWarrantyProcess
                SerialRepo->>DB: UPDATE ProductSerial
                DB-->>SerialRepo: Success

            alt Sửa nội bộ được
                Warranty->>ClaimRepo: Update claim = WaitingDecision
                ClaimRepo->>DB: UPDATE WarrantyClaim
                DB-->>ClaimRepo: Success
                Warranty->>ClaimRepo: Update claim = Repairing
                ClaimRepo->>DB: UPDATE WarrantyClaim
                DB-->>ClaimRepo: Success
                Warranty->>SerialRepo: Update serial = WarrantyDefective
                SerialRepo->>DB: UPDATE ProductSerial
                DB-->>SerialRepo: Success
                Warranty->>ClaimRepo: Update claim = Repaired
                ClaimRepo->>DB: UPDATE WarrantyClaim
                DB-->>ClaimRepo: Success
                Warranty->>Warranty: Confirm customer handover
                Warranty->>ClaimRepo: Update claim = ReturnedToCustomer
                ClaimRepo->>DB: UPDATE WarrantyClaim
                DB-->>ClaimRepo: Success
                Warranty->>SerialRepo: Update serial = Sold
                SerialRepo->>DB: UPDATE ProductSerial
                DB-->>SerialRepo: Success
                Warranty->>ClaimRepo: Update claim = Closed
                ClaimRepo->>DB: UPDATE WarrantyClaim
                DB-->>ClaimRepo: Success
                Warranty-->>VM: Success repaired
            else Cần gửi hãng
                Warranty->>ClaimRepo: Update claim = SentToManufacturer
                ClaimRepo->>DB: UPDATE WarrantyClaim
                DB-->>ClaimRepo: Success
                Warranty->>SerialRepo: Update serial = WarrantyDefective
                SerialRepo->>DB: UPDATE ProductSerial
                DB-->>SerialRepo: Success
                Warranty->>ClaimRepo: Update claim = WaitingManufacturerResult
                ClaimRepo->>DB: UPDATE WarrantyClaim
                DB-->>ClaimRepo: Success
                Warranty->>ClaimRepo: Save manufacturer result + update claim = WaitingDecision
                ClaimRepo->>DB: UPDATE WarrantyClaim
                DB-->>ClaimRepo: Success

                alt Hãng sửa được
                    Warranty->>ClaimRepo: Update claim = Repaired
                    ClaimRepo->>DB: UPDATE WarrantyClaim
                    DB-->>ClaimRepo: Success
                    Warranty->>Warranty: Confirm customer handover
                    Warranty->>ClaimRepo: Update claim = ReturnedToCustomer
                    ClaimRepo->>DB: UPDATE WarrantyClaim
                    DB-->>ClaimRepo: Success
                    Warranty->>SerialRepo: Update serial = Sold
                    SerialRepo->>DB: UPDATE ProductSerial
                    DB-->>SerialRepo: Success
                    Warranty->>ClaimRepo: Update claim = Closed
                    ClaimRepo->>DB: UPDATE WarrantyClaim
                    DB-->>ClaimRepo: Success
                    Warranty-->>VM: Success repaired by manufacturer
                else Hãng không sửa được, đổi mới
                    Warranty->>ClaimRepo: Record manufacturer result = Replace
                    ClaimRepo->>DB: UPDATE WarrantyClaim
                    DB-->>ClaimRepo: Success
                    VM->>Manager: Trình quyết định đổi mới
                    Manager-->>VM: Xác nhận phê duyệt
                    VM->>Warranty: RequestReplacementApproval(claimId, managerDecision)
                    Warranty->>Approval: Record replacement approval
                    Approval-->>Warranty: Approved
                    Warranty->>DB: BEGIN TRAN
                    Warranty->>ClaimRepo: LockWarrantyClaim()
                    ClaimRepo->>DB: SELECT ... WITH (UPDLOCK)
                    DB-->>ClaimRepo: Locked
                    Warranty->>CoverageRepo: LockWarrantyCoverage()
                    CoverageRepo->>DB: SELECT ... WITH (UPDLOCK)
                    DB-->>CoverageRepo: Locked

                    Warranty->>Inventory: LockProductBalanceForReplacement(order by ProductId asc, warehouseId = default)
                    Inventory->>BalanceRepo: SELECT ... WITH (UPDLOCK)
                    DB-->>BalanceRepo: Current balance
                    BalanceRepo-->>Inventory: Locked

                    Warranty->>SerialRepo: Lock old serial and candidate replacement serials(order by ProductSerialId asc)
                    Note over Warranty,SerialRepo: Luồng đổi mới cũng giữ thứ tự khóa ProductId rồi ProductSerialId tăng dần để giảm nguy cơ deadlock
                    SerialRepo->>DB: SELECT ... WITH (UPDLOCK)
                    DB-->>SerialRepo: Locked

                    Warranty->>Inventory: CheckAvailableQuantity(warehouseId = default)
                    alt Đủ tồn thay thế
                        Warranty->>SerialRepo: Update old serial = ReturnedToManufacturer
                        SerialRepo->>DB: UPDATE ProductSerial old
                        DB-->>SerialRepo: Success

                        Warranty->>CoverageRepo: Close old coverage = Replaced / Inactive
                        CoverageRepo->>DB: UPDATE WarrantyCoverage old
                        DB-->>CoverageRepo: Success

                        Warranty->>ClaimRepo: Mark manufacturer replacement accepted
                        ClaimRepo->>DB: UPDATE WarrantyClaim
                        DB-->>ClaimRepo: Success

                        Warranty->>StockRepo: Insert StockOut(Status=Approved, PurposeCode=WarrantyReplacement, WarehouseId=default, ApprovedBy=ManagerId)
                        StockRepo->>DB: INSERT StockOut / StockOutLine
                        DB-->>StockRepo: Success

                        Warranty->>Inventory: PostWarrantyReplacementStockOut(stockOutId, PostedBy=SystemServiceAccount)
                        Inventory->>BalanceRepo: UPDATE StockBalance
                        BalanceRepo->>DB: UPDATE StockBalance
                        DB-->>BalanceRepo: Success
                        Note over StockRepo,Inventory: WarrantyReplacement là ngoại lệ hợp lệ của policy tách approver/poster
                        Inventory->>SerialRepo: Update replacement serial = Replaced
                        SerialRepo->>DB: UPDATE ProductSerial replacement
                        DB-->>SerialRepo: Success

                        Warranty->>CoverageRepo: Create replacement coverage with remaining term
                        CoverageRepo->>DB: INSERT WarrantyCoverage replacement
                        DB-->>CoverageRepo: Success

                        Warranty->>LedgerRepo: Write warranty replacement ledger
                        LedgerRepo->>DB: INSERT StockLedger
                        DB-->>LedgerRepo: Success

                        Warranty->>AuditRepo: WriteAuditLog()
                        AuditRepo->>DB: INSERT AuditLog
                        DB-->>AuditRepo: Success

                        Warranty->>ClaimRepo: Update claim = Replaced + ReplacementSerialId
                        ClaimRepo->>DB: UPDATE WarrantyClaim
                        DB-->>ClaimRepo: Success
                        Warranty->>ClaimRepo: Update claim = ReplacementStockOutId = stockOutId
                        ClaimRepo->>DB: UPDATE WarrantyClaim
                        DB-->>ClaimRepo: Success
                        Warranty->>DB: COMMIT
                        Warranty->>Warranty: Confirm replacement handover
                        Warranty->>DB: BEGIN TRAN
                        Warranty->>ClaimRepo: Update claim = ReturnedToCustomer
                        ClaimRepo->>DB: UPDATE WarrantyClaim
                        DB-->>ClaimRepo: Success
                        Warranty->>SerialRepo: Update replacement serial = Sold
                        SerialRepo->>DB: UPDATE ProductSerial replacement
                        DB-->>SerialRepo: Success
                        Warranty->>ClaimRepo: Update claim = Closed
                        ClaimRepo->>DB: UPDATE WarrantyClaim
                        DB-->>ClaimRepo: Success
                        Warranty->>DB: COMMIT
                        Warranty-->>VM: Success replaced
                    else Không đủ tồn thay thế
                        Warranty->>DB: ROLLBACK
                        Note over Warranty,DB: Transaction đổi mới đã kết thúc
                        Warranty->>DB: BEGIN TRAN
                        Warranty->>ClaimRepo: Update claim = WaitingDecision + insufficient replacement stock note
                        ClaimRepo->>DB: UPDATE WarrantyClaim
                        DB-->>ClaimRepo: Success
                        Warranty->>DB: COMMIT
                        Warranty-->>VM: Need replacement stock before customer exchange
                    end
                end
            else Từ chối
                Warranty->>ClaimRepo: Update claim = WaitingDecision
                ClaimRepo->>DB: UPDATE WarrantyClaim
                DB-->>ClaimRepo: Success
                VM->>Manager: Trình quyết định từ chối
                Manager-->>VM: Xác nhận phê duyệt
                VM->>Warranty: RequestRejectionApproval(claimId, managerDecision)
                Warranty->>Approval: Record rejection approval
                Approval-->>Warranty: Approved
                Warranty->>ClaimRepo: Update claim = Rejected + RejectionReason
                ClaimRepo->>DB: UPDATE WarrantyClaim
                DB-->>ClaimRepo: Success
                Warranty->>Warranty: Confirm customer handover
                Warranty->>ClaimRepo: Update claim = ReturnedToCustomer
                ClaimRepo->>DB: UPDATE WarrantyClaim
                DB-->>ClaimRepo: Success
                Warranty->>SerialRepo: Update serial = Sold
                SerialRepo->>DB: UPDATE ProductSerial
                DB-->>SerialRepo: Success
                Warranty->>ClaimRepo: Update claim = Closed
                ClaimRepo->>DB: UPDATE WarrantyClaim
                DB-->>ClaimRepo: Success
                Warranty-->>VM: Success rejected
            end
        else Đã có claim đang mở
            Warranty-->>VM: Reject because claim is already open
        end
    else Không đủ điều kiện
        Warranty-->>VM: Reject by eligibility
    end
```

---

## 16. State vòng đời chứng từ kho

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> PendingApproval: Gửi duyệt
    Draft --> Cancelled: Hủy nháp

    PendingApproval --> Draft: Từ chối / yêu cầu chỉnh sửa
    PendingApproval --> Approved: Duyệt

    Approved --> Posted: Ghi sổ
    Approved --> Cancelled: Hủy trước khi ghi sổ

    Posted --> Locked: Khóa chứng từ
    Locked --> [*]
    Cancelled --> [*]
```

| Transition | Người thực hiện |
| --- | --- |
| `Draft -> PendingApproval` | Người lập chứng từ |
| `Draft -> Cancelled` | Người lập hoặc quản lý |
| `PendingApproval -> Draft` | Quản lý |
| `PendingApproval -> Approved` | Quản lý |
| `Approved -> Posted` | Nhân viên kho hoặc người ghi sổ được ủy quyền |
| `Approved -> Cancelled` | Quản lý hoặc vai trò kho được phép hủy trước khi ghi sổ |
| `Posted -> Locked` | Quản lý hoặc hệ thống chốt kỳ |

Ghi chú:
- Người lập và người duyệt có thể là cùng một người nếu quy trình doanh nghiệp cho phép.
- Nếu doanh nghiệp bật kiểm soát nội bộ thì người duyệt và người ghi sổ không được là cùng một người.

---

## 17. State vòng đời hồ sơ bảo hành

```mermaid
stateDiagram-v2
    [*] --> Checking
    Checking --> WaitingDecision: Có kết luận kỹ thuật nội bộ
    Checking --> SentToManufacturer: Cần gửi hãng
    SentToManufacturer --> WaitingManufacturerResult: Hãng tiếp nhận
    WaitingManufacturerResult --> WaitingDecision: Có kết luận từ hãng
    WaitingDecision --> Repairing: Quyết định sửa nội bộ
    WaitingDecision --> Repaired: Chấp nhận máy đã được hãng sửa xong
    WaitingDecision --> Replaced: Quyết định đổi mới
    WaitingDecision --> Rejected: Quyết định từ chối
    Repairing --> Repaired: Sửa xong
    Repaired --> ReturnedToCustomer: Giao trả máy
    Replaced --> ReturnedToCustomer: Giao serial thay thế
    Rejected --> ReturnedToCustomer: Trả lại máy
    ReturnedToCustomer --> Closed: Hoàn tất
    Closed --> [*]
```

Ghi chú:
- `WarrantyClaim` được tạo từ trạng thái `Checking`; các trường hợp không đủ điều kiện hoặc đã có claim mở sẽ bị chặn trước khi tạo hồ sơ.
- `Rejected` không đóng hồ sơ ngay; phải qua bước trả lại máy hoặc xác nhận giao nhận rồi mới `Closed`.
- Nếu đổi mới nhưng thiếu hàng thay thế thì claim quay về `WaitingDecision`, giữ mở để chờ nhập hàng hoặc quyết định nghiệp vụ khác.

---

## 18. ERD chi tiết đầy đủ mọi bảng và tất cả liên kết

```mermaid
erDiagram
    AppUser {
        int Id PK
        string Username
        string PasswordHash
        string FullName
        string RoleCode
        bool MustChangePassword
        int FailedLoginCount
        int CreatedBy FK "nullable"
        datetime CreatedAt
        datetime LockoutUntil "nullable"
        datetime LastFailedLoginAt "nullable"
        datetime LastPasswordChangedAt "nullable"
        datetime LastLoginAt "nullable"
        bool IsActive
    }

    Category {
        int Id PK
        string CategoryCode
        string DisplayName
        bool IsActive
    }

    Brand {
        int Id PK
        string BrandCode
        string DisplayName
        string OriginCountry
        bool IsActive
    }

    Unit {
        int Id PK
        string UnitCode
        string DisplayName
        bool IsActive
    }

    Supplier {
        int Id PK
        string SupplierCode
        string DisplayName
        string Phone
        string Email
        bool IsActive
    }

    Customer {
        int Id PK
        string CustomerCode
        string DisplayName
        string Phone
        string Email
        bool IsActive
    }

    Warehouse {
        int Id PK
        string WarehouseCode UK
        string DisplayName
        bool IsDefault
        bool IsActive
    }

    Product {
        int Id PK
        string ProductCode UK
        string DisplayName
        int CategoryId FK
        int BrandId FK
        int DefaultUnitId FK
        decimal DefaultPrice
        string OriginCountry
        int WarrantyPeriodMonths
        bool IsSerialTracked
        bool IsActive
    }

    ProductUnit {
        int Id PK
        int ProductId FK
        int UnitId FK
        decimal ConversionFactor
        bool IsBaseUnit
        bool IsPurchaseUnit
        bool IsSalesUnit
        string UQ_ProductUnit "UNIQUE (ProductId, UnitId)"
    }

    StockBalance {
        int Id PK
        int WarehouseId FK
        int ProductId FK
        decimal OnHandQuantity
        decimal AvailableQuantity
        decimal ReservedQuantity
        string UQ_StockBalance "UNIQUE (WarehouseId, ProductId)"
    }

    StockIn {
        int Id PK
        string DocumentCode UK
        int SupplierId FK "nullable"
        int WarehouseId FK
        string PurposeCode
        string Status
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime CreatedAt
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    StockInLine {
        int Id PK
        int StockInId FK
        int ProductId FK
        int UnitId FK
        decimal Quantity
        decimal BaseQuantity
        decimal UnitPrice
    }

    StockOut {
        int Id PK
        string DocumentCode UK
        int CustomerId FK
        int WarehouseId FK
        string PurposeCode
        string Status
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime CreatedAt
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    StockOutLine {
        int Id PK
        int StockOutId FK
        int ProductId FK
        int UnitId FK
        decimal Quantity
        decimal BaseQuantity
        decimal UnitPrice
    }

    StockCountSession {
        int Id PK
        string SessionCode UK
        int WarehouseId FK
        string Status
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime CountDate
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    StockCountLine {
        int Id PK
        int SessionId FK
        int ProductId FK
        decimal SystemQuantity
        decimal CountedQuantity
        decimal VarianceQuantity
    }

    StockAdjustment {
        int Id PK
        string DocumentCode UK
        int WarehouseId FK
        string AdjustmentType
        string Status
        string ReferenceDocumentType
        int ReferenceDocumentId
        string ReasonCode
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    StockAdjustmentLine {
        int Id PK
        int AdjustmentId FK
        int ProductId FK
        int ProductSerialId FK "nullable"
        decimal QuantityDelta
        decimal BaseQuantityDelta
        string Direction
    }

    PurchaseInvoice {
        int Id PK
        string InvoiceCode UK
        int SupplierId FK
        int StockInId FK "nullable"
        datetime InvoiceDate
        decimal SubTotal
        decimal TaxAmount
        decimal GrandTotal
        decimal PaidAmount
        string PaymentStatus
        datetime DueDate
    }

    PurchaseInvoiceLine {
        int Id PK
        int PurchaseInvoiceId FK
        int ProductId FK
        int UnitId FK
        int StockInLineId FK "nullable"
        decimal Quantity
        decimal UnitPrice
        decimal SubTotal
        decimal TaxRate
        decimal TaxAmount
        decimal GrandTotal
    }

    SalesInvoice {
        int Id PK
        string InvoiceCode UK
        int CustomerId FK
        int StockOutId FK "nullable"
        datetime InvoiceDate
        decimal SubTotal
        decimal TaxAmount
        decimal GrandTotal
        decimal PaidAmount
        string PaymentStatus
        datetime DueDate
    }

    SalesInvoiceLine {
        int Id PK
        int SalesInvoiceId FK
        int ProductId FK
        int UnitId FK
        int StockOutLineId FK "nullable"
        decimal Quantity
        decimal UnitPrice
        decimal SubTotal
        decimal TaxRate
        decimal TaxAmount
        decimal GrandTotal
    }

    ProductSerial {
        int Id PK
        int ProductId FK
        string SerialNumber UK
        string CurrentStatus
        int CurrentWarehouseId FK "nullable"
        int LastStockInLineId FK
        int LastStockOutLineId FK "nullable"
    }

    StockLedger {
        int Id PK
        int WarehouseId FK
        int ProductId FK
        int ProductSerialId FK "nullable"
        string SourceDocumentType
        int SourceDocumentId
        string MovementType
        decimal Quantity
        int PostedBy FK
        datetime PostedAt
    }

    WarrantyCoverage {
        int Id PK
        int ProductSerialId FK
        int CustomerId FK
        int SalesInvoiceId FK "nullable"
        datetime WarrantyStartDate
        datetime WarrantyEndDate
        string CoverageStatus
        string UX_ActiveCoverage "FILTERED UNIQUE Active/ProductSerialId"
    }

    WarrantyClaim {
        int Id PK
        string ClaimCode UK
        int WarrantyCoverageId FK
        int ProductSerialId FK
        int ReplacementSerialId FK "nullable"
        int ReplacementStockOutId FK "nullable"
        datetime ReceivedDate
        string ProblemDescription
        string TechnicalConclusion
        string ManufacturerResult
        string RejectionReason
        string ProcessingNote
        string ResolutionType
        string Status
        int ApprovedBy FK "nullable"
        int ProcessedBy FK
        datetime ClosedDate "nullable"
        string UX_OpenClaim "FILTERED UNIQUE Open/ProductSerialId"
    }

    AuditLog {
        int Id PK
        string EntityName
        int EntityId
        string ActionCode
        string BeforeJson
        string AfterJson
        int PerformedBy FK
        datetime PerformedAt
    }

    Category ||--o{ Product : categoryId
    Brand ||--o{ Product : brandId
    Unit ||--o{ Product : defaultUnitId
    Product ||--o{ ProductUnit : productId
    Unit ||--o{ ProductUnit : unitId

    Warehouse ||--o{ StockBalance : warehouseId
    Product ||--o{ StockBalance : productId

    Supplier |o--o{ StockIn : supplierId
    Warehouse ||--o{ StockIn : warehouseId
    StockIn ||--o{ StockInLine : stockInId
    Product ||--o{ StockInLine : productId
    Unit ||--o{ StockInLine : unitId

    Warehouse ||--o{ StockOut : warehouseId
    Customer ||--o{ StockOut : customerId
    StockOut ||--o{ StockOutLine : stockOutId
    Product ||--o{ StockOutLine : productId
    Unit ||--o{ StockOutLine : unitId

    Warehouse ||--o{ StockCountSession : warehouseId
    StockCountSession ||--o{ StockCountLine : sessionId
    Product ||--o{ StockCountLine : productId

    Warehouse ||--o{ StockAdjustment : warehouseId
    StockAdjustment ||--o{ StockAdjustmentLine : adjustmentId
    Product ||--o{ StockAdjustmentLine : productId
    ProductSerial |o--o{ StockAdjustmentLine : productSerialId

    Supplier ||--o{ PurchaseInvoice : supplierId
    StockIn |o--o| PurchaseInvoice : stockInId
    PurchaseInvoice ||--o{ PurchaseInvoiceLine : purchaseInvoiceId
    Product ||--o{ PurchaseInvoiceLine : productId
    Unit ||--o{ PurchaseInvoiceLine : unitId
    StockInLine |o--o{ PurchaseInvoiceLine : stockInLineId
    Customer ||--o{ SalesInvoice : customerId
    StockOut |o--o| SalesInvoice : stockOutId
    SalesInvoice ||--o{ SalesInvoiceLine : salesInvoiceId
    Product ||--o{ SalesInvoiceLine : productId
    Unit ||--o{ SalesInvoiceLine : unitId
    StockOutLine |o--o{ SalesInvoiceLine : stockOutLineId

    Warehouse |o--o{ ProductSerial : currentWarehouseId
    Product ||--o{ ProductSerial : productId
    StockInLine ||--o{ ProductSerial : lastStockInLineId
    StockOutLine |o--o{ ProductSerial : lastStockOutLineId

    Warehouse ||--o{ StockLedger : warehouseId
    Product ||--o{ StockLedger : productId
    ProductSerial |o--o{ StockLedger : productSerialId

    ProductSerial ||--o{ WarrantyCoverage : productSerialId
    Customer ||--o{ WarrantyCoverage : customerId
    SalesInvoice |o--o{ WarrantyCoverage : salesInvoiceId

    WarrantyCoverage ||--o{ WarrantyClaim : warrantyCoverageId
    ProductSerial ||--o{ WarrantyClaim : productSerialId
    ProductSerial |o--o{ WarrantyClaim : replacementSerialId
    StockOut |o--o| WarrantyClaim : replacementStockOutId

    AppUser ||--o{ StockIn : createdBy
    AppUser |o--o{ AppUser : createdBy
    AppUser |o--o{ StockIn : approvedBy
    AppUser |o--o{ StockIn : postedBy
    AppUser ||--o{ StockOut : createdBy
    AppUser |o--o{ StockOut : approvedBy
    AppUser |o--o{ StockOut : postedBy
    AppUser ||--o{ StockCountSession : createdBy
    AppUser |o--o{ StockCountSession : approvedBy
    AppUser |o--o{ StockCountSession : postedBy
    AppUser ||--o{ StockAdjustment : createdBy
    AppUser |o--o{ StockAdjustment : approvedBy
    AppUser |o--o{ StockAdjustment : postedBy
    AppUser ||--o{ StockLedger : postedBy
    AppUser |o--o{ WarrantyClaim : approvedBy
    AppUser ||--o{ WarrantyClaim : processedBy
    AppUser ||--o{ AuditLog : performedBy
```

Ghi chú:
- Đây là bản ERD chi tiết đầy đủ mọi bảng và mọi FK cứng đang có trong mô hình hiện tại.
- `StockLedger.SourceDocumentType + SourceDocumentId` là tham chiếu nghiệp vụ đa hình, không phải một FK cứng tới một bảng duy nhất nên không thể vẽ như một liên kết database chuẩn.
- Các trường mã nghiệp vụ như `DocumentCode`, `InvoiceCode`, `SessionCode`, `ClaimCode`, `ProductCode`, `SerialNumber` phải có unique constraint hoặc unique index tương ứng ở mức database.
- `Warehouse` được thiết kế sẵn cho khả năng mở rộng nhiều kho, nhưng phase hiện tại chỉ vận hành với một kho mặc định và UI không cho người dùng chọn kho.
- `StockBalance` phải có unique constraint theo cặp `ProductId + WarehouseId`.
- `ProductUnit` cần thêm unique constraint theo cặp (`ProductId`, `UnitId`) và mỗi `Product` chỉ có một dòng `IsBaseUnit = true`.
- `WarrantyClaim` cần filtered unique index hoặc cơ chế tương đương để chặn hơn một claim mở trên cùng `ProductSerialId`.
- `WarrantyCoverage` nên có rule hoặc filtered unique index để chặn hơn một coverage active trên cùng `ProductSerialId`.
- Các FK và cột workflow có ghi `nullable` trong ERD phải được map là quan hệ tùy chọn khi sinh entity/database, gồm ít nhất: `StockIn.SupplierId`, `ProductSerial.CurrentWarehouseId`, `PurchaseInvoice.StockInId`, `SalesInvoice.StockOutId`, `WarrantyCoverage.SalesInvoiceId`, `WarrantyClaim.ReplacementSerialId`, `WarrantyClaim.ReplacementStockOutId`, `ApprovedBy`, `PostedBy`, `ApprovedAt`, `PostedAt`, `ClosedDate`.




