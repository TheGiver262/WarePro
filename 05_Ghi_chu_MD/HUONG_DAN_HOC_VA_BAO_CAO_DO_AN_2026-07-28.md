# Tài liệu học và báo cáo đồ án

## 1. Cách dùng tài liệu

Đây là bản ôn tập đồng bộ với báo cáo canonical ngày 01/08/2026, bộ slide bảo vệ và mã nguồn WarePro hiện tại. Học theo thứ tự:

1. Nắm bài toán và phạm vi.
2. Kể được một vòng đời serial từ nhập kho đến bảo hành/đổi mới.
3. Giải thích kiến trúc WPF/MVVM và dữ liệu.
4. Trình bày tính nhất quán: trạng thái, transaction, balance, ledger, audit.
5. Luyện câu hỏi phản biện; trả lời đúng phạm vi, không nhận những phần chưa triển khai.

## 2. Bài nói mở đầu 60–90 giây

Đồ án xây dựng ứng dụng desktop quản lý kho và bảo hành cho doanh nghiệp, tập trung vào việc nối liền số lượng tồn kho với lịch sử của từng thiết bị có số sê-ri. Hệ thống dùng WPF và MVVM ở tầng giao diện, EF Core kết nối SQL Server ở tầng dữ liệu. Sáu nhóm dữ liệu chính là danh mục–đối tác, luồng tồn kho, kiểm soát kho–sê-ri, hóa đơn, bảo hành, và người dùng–audit; dashboard/báo cáo đọc dữ liệu đã ghi sổ từ các nhóm này. Điểm cốt lõi là mọi nghiệp vụ ghi sổ đều cập nhật nguyên tử số dư hiện tại, lịch sử StockLedger, trạng thái serial và nhật ký audit; chứng từ đã ghi sổ không sửa như bản nháp mà phải đảo hoặc điều chỉnh. Nhờ đó người dùng có thể truy vết một serial từ nguồn nhập, kho hiện tại, lần bán, khách hàng và quyền bảo hành đến hồ sơ sửa chữa hoặc đổi thiết bị.

## 3. Phạm vi và giới hạn

### Có trong đồ án

- Danh mục sản phẩm, đơn vị, kho, đối tác.
- Nhập kho, xuất kho, chuyển kho, kiểm kê và điều chỉnh.
- Sản phẩm không serial và sản phẩm quản lý serial.
- Hóa đơn mua/bán ở mức nghiệp vụ cần cho truy vết.
- WarrantyCoverage, WarrantyClaim và luồng sửa chữa/đổi mới.
- Người dùng, vai trò, phân quyền, audit log.
- Dashboard, báo cáo và tra cứu lịch sử.
- Import tồn đầu kỳ từ Excel/CSV theo workflow OpeningBalance.

### Chưa tuyên bố đã triển khai

- Web/mobile, thanh toán online, kế toán đầy đủ.
- Tích hợp API trực tiếp với hãng bảo hành.
- Load test quy mô lớn và triển khai đa chi nhánh offline.
- RealDatabase integration tests cần cấu hình SQL Server riêng.

## 4. Kiến trúc cần nhớ

| Tầng | Trách nhiệm | Không nên đặt ở đây |
|---|---|---|
| View/XAML | Hiển thị, binding, trạng thái điều khiển | Quy tắc ghi sổ |
| ViewModel | State màn hình, command, validation mức UI | Nguồn chuẩn duy nhất của policy |
| Service/Application | Điều phối use case, quyền, transition, transaction | Chi tiết bố cục XAML |
| Domain/Model | Entity, enum trạng thái, quy tắc lõi | Truy vấn giao diện |
| Data/Infrastructure | EF Core, SQL Server, repository, transaction, logging | Quyết định UX |

MVVM được chọn vì WPF có data binding và commanding; ViewModel có thể kiểm thử mà không cần khởi động UI.

## 5. Dữ liệu và tính nhất quán

- `StockBalance` trả lời: hiện đang có bao nhiêu sản phẩm tại kho nào.
- `StockLedger` trả lời: vì sao số dư đó thay đổi.
- `ProductSerial` giữ trạng thái và vị trí hiện tại của từng thiết bị.
- `WarrantyCoverage` giữ quyền bảo hành; `WarrantyClaim` giữ từng lần tiếp nhận/xử lý.
- `AuditLog` giữ người thao tác, thời điểm, đối tượng, hành động và dữ liệu trước/sau khi cần.
- Unique/index tiêu biểu: serial number duy nhất; `(ProductId, WarehouseId)` cho số dư; chỉ một coverage active trên một serial.

### Quan hệ cơ sở dữ liệu cần giải thích được

- `Product` là thực thể trung tâm của danh mục: mỗi sản phẩm bắt buộc thuộc một `Category`, một `Brand` và có một `DefaultUnit`; bảng nối `ProductUnit` cho phép một sản phẩm dùng nhiều đơn vị quy đổi. `UNIQUE(ProductId, UnitId)` ngăn khai báo trùng, filtered unique trên `ProductId` khi `IsBaseUnit = 1` bảo đảm tối đa một đơn vị cơ sở.
- `StockIn`/`StockOut` là header, còn `StockInLine`/`StockOutLine` là dòng chi tiết. Mỗi dòng bắt buộc thuộc đúng một header; không có FK trực tiếp `StockOutLine → StockIn` hoặc `StockOutLine → StockInLine`.
- `StockBalance` là ảnh chụp số dư hiện tại theo cặp kho–sản phẩm. `StockLedger` có FK vật lý tới `Warehouse`, `Product` và tùy chọn tới `ProductSerial`; cặp `SourceDocumentType + SourceDocumentId` chỉ là tham chiếu logic đa hình tới chứng từ nguồn.
- `ProductSerial.LastStockInLineId`, `LastStockOutLineId` và `StockTransferLineId` là con trỏ nguồn/trạng thái gần nhất để truy vấn nhanh. Chúng không thay thế lịch sử đầy đủ, vì lịch sử biến động nằm trong `StockLedger`.
- `PurchaseInvoice.StockInId` và `SalesInvoice.StockOutId` là FK nullable có filtered unique index, nên một phiếu kho liên kết tối đa một hóa đơn cùng loại. FK dòng hóa đơn tới dòng kho nullable và không unique, nên chỉ dùng đối chiếu chi tiết, không áp đặt quan hệ 1–1.
- `WarrantyClaim` dùng FK kép `(WarrantyCoverageId, ProductSerialId)` tới alternate key cùng cặp trên `WarrantyCoverage`; database vì vậy chặn claim gắn coverage của serial khác. Filtered unique index còn chặn nhiều coverage Active hoặc nhiều claim đang mở trên cùng serial.
- `AuditArchiveManifest` là biên nhận của một lần xuất log: giữ operation id, phạm vi thời gian, số dòng, tên file và SHA-256. Nó không phải bảng log thứ hai và không thay thế `AuditLog`.
- Trong SQL Server, `RowVersion` là kiểu `rowversion` cố định 8 byte; trong EF Core nó được ánh xạ thành `byte[]`. `TaxRate` của dòng hóa đơn dùng `decimal(9,4)`, còn các trường tiền dùng `decimal(18,2)`.

### Ghi sổ nhập

Validate → mở transaction → khóa/cập nhật balance → tạo serial nếu cần → ghi ledger → ghi audit → commit. Lỗi ở bất kỳ bước nào thì rollback.

### Ghi sổ xuất

Validate tồn và serial → khóa theo thứ tự ổn định → giảm balance → cập nhật serial → nếu là Sale thì tạo coverage → ghi ledger/audit → commit.

### Đổi mới bảo hành

Xác nhận kết luận không sửa được → kiểm tra serial thay thế còn tồn → khóa các bản ghi liên quan → tạo phiếu xuất WarrantyReplacement → chuyển serial cũ về trạng thái phù hợp → đóng coverage cũ → tạo coverage mới theo thời hạn còn lại → ghi ledger/audit.

## 6. Trạng thái phải giải thích được

- Chứng từ: `Draft → PendingApproval → Approved → Posted → Locked`; có nhánh `Cancelled`.
- Serial: `InStock`, `Reserved`, `Sold`, `InWarrantyProcess`, `WarrantyDefective`, `ReturnedToManufacturer`, `Replaced`, `Inactive`.
- Bảo hành: `Checking`, `SentToManufacturer`, `WaitingManufacturerResult`, `WaitingDecision`, `Repairing`, `Repaired`, `Replaced`, `Rejected`, `ReturnedToCustomer`, `Closed`.

Nguyên tắc: Posted không sửa/xóa tùy tiện; sửa sai bằng chứng từ đảo hoặc điều chỉnh để bảo toàn lịch sử.

## 7. Kịch bản demo 5–7 phút

1. Đăng nhập bằng vai trò nhân viên kho.
2. Nhập serial `SN-001`, ghi sổ, kiểm tra `InStock`.
3. Lập bán hàng, xuất serial, kiểm tra `Sold` và coverage.
4. Tra cứu serial: nguồn nhập, kho, khách hàng, thời hạn.
5. Tạo claim bảo hành, chuyển sang xử lý.
6. Đổi sang `SN-002`, kiểm tra phiếu xuất thay thế, coverage mới và audit.
7. Mở dashboard/báo cáo để chứng minh số liệu khớp.

## 8. Bộ câu hỏi giáo viên có thể hỏi

### A. Bài toán và phạm vi

1. Vì sao chọn bài toán quản lý kho và bảo hành?
2. Điểm khác biệt của đồ án so với CRUD kho thông thường là gì?
3. Ai là người dùng chính?
4. Một serial cần truy vết những thông tin nào?
5. Vì sao phải tách hàng serial và không serial?
6. Phạm vi nào đã loại khỏi đồ án và vì sao?
7. Giả định một kho mặc định ảnh hưởng thiết kế ra sao?
8. Nếu mở rộng đa kho thì bảng nào phải thay đổi?
9. Vì sao chưa làm web/mobile?
10. Tiêu chí nào để nói đồ án đạt mục tiêu?

### B. Phân tích nghiệp vụ

11. Tác nhân và quyền của từng tác nhân?
12. Use case nhập kho bắt đầu/kết thúc ở đâu?
13. Điều kiện ghi sổ phiếu nhập?
14. Điều kiện xuất serial?
15. Khi hết tồn hệ thống phản ứng thế nào?
16. OpeningBalance khác Purchase ở đâu?
17. Kiểm kê tạo chênh lệch như thế nào?
18. Vì sao không sửa trực tiếp chứng từ Posted?
19. Hóa đơn và chứng từ kho liên hệ thế nào?
20. Khi bán hàng, bảo hành được kích hoạt lúc nào?
21. Coverage khác Claim thế nào?
22. Một serial có thể có nhiều claim không?
23. Khi đổi mới, serial cũ và mới chuyển trạng thái gì?
24. Nếu khách không đủ điều kiện bảo hành thì lưu gì?
25. Quy trình đóng claim?

### C. Kiến trúc và mã nguồn

26. Vì sao WPF?
27. MVVM giải quyết vấn đề gì?
28. Binding và Commanding hoạt động ra sao?
29. Vì sao không đặt nghiệp vụ trong code-behind?
30. ViewModel được phép validate gì?
31. Service re-validate những gì?
32. Repository/Unit of Work dùng để làm gì?
33. EF Core đóng vai trò nào?
34. SQL Server phù hợp ở điểm nào?
35. Nếu đổi sang web API, tầng nào tái sử dụng được?
36. View caching có lợi ích gì?
37. Cách xử lý lỗi và thông báo cho người dùng?
38. Cách kiểm thử ViewModel không cần UI?
39. Vì sao không dùng một lớp “God service”?
40. Điểm phụ thuộc chính giữa các tầng?

### D. Cơ sở dữ liệu

41. Vì sao cần cả StockBalance và StockLedger?
42. Nếu balance sai nhưng ledger đúng thì xử lý thế nào?
43. Khóa ngoại quan trọng nhất?
44. Vì sao serial number unique?
45. Vì sao coverage active phải unique theo serial?
46. `SourceDocumentType + SourceDocumentId` của ledger có phải FK không?
47. Nullability của `CurrentWarehouseId` có ý nghĩa gì?
48. RowVersion dùng để làm gì?
49. Chỉ mục nào phục vụ tra cứu?
50. Làm sao ngăn tồn âm?
51. Làm sao bảo đảm đơn vị quy đổi đúng?
52. Vì sao cần audit trước/sau?
53. Có nên xóa dữ liệu lịch sử không?
54. Nếu thêm nhiều kho, khóa duy nhất nào cần giữ?
55. Nếu dữ liệu lớn, tối ưu báo cáo thế nào?

### E. Transaction và đồng thời

56. Vì sao ghi sổ phải là transaction?
57. Nếu tạo ledger thành công nhưng cập nhật balance lỗi thì sao?
58. Deadlock phát sinh khi nào?
59. Vì sao khóa theo thứ tự ProductId?
60. Isolation level mặc định là gì?
61. Khi nào cần optimistic concurrency?
62. RowVersion xử lý xung đột ra sao?
63. Hai người cùng xuất một serial thì kết quả nào được chấp nhận?
64. Làm sao retry deadlock an toàn?
65. Transaction kéo dài gây hại gì?

### F. Bảo mật và kiểm soát

66. Phân quyền theo RoleCode hay theo từng nút UI?
67. Vì sao UI ẩn nút không đủ bảo mật?
68. Service kiểm tra quyền ở đâu?
69. Audit những hành động nào?
70. Ai được xem/xuất audit?
71. Mật khẩu được lưu thế nào?
72. Tài khoản mới phải đổi mật khẩu ra sao?
73. Nếu người lập cũng là người duyệt thì policy nào áp dụng?
74. Làm sao chống sửa log?
75. Dữ liệu nhạy cảm nào cần hạn chế?

### G. Kiểm thử, triển khai, đánh giá

76. Các nhóm test chính?
77. Test nào chứng minh không tồn âm?
78. Test nào chứng minh transition hợp lệ?
79. Test đổi mới bảo hành kiểm tra gì?
80. Vì sao integration test SQL Server có thể thất bại dù unit test pass?
81. 95/95 test trên slide là phạm vi nào?
82. 891 test hiện tại khác 95/95 trên slide thế nào?
83. Cách tạo dữ liệu demo có thể reset?
84. Yêu cầu cài đặt?
85. Nếu mất kết nối SQL Server thì UI phản ứng thế nào?
86. Có backup/restore không?
87. Đã load test chưa?
88. Hạn chế lớn nhất hiện tại?
89. Hướng phát triển ưu tiên?
90. Nếu làm lại, bạn thay đổi điều gì?

### H. Câu hỏi phản biện sâu

91. Tại sao không chỉ tính tồn bằng cách cộng trừ ledger?
92. Tại sao không chỉ lưu trạng thái serial trong invoice?
93. Điều gì xảy ra nếu commit xong nhưng audit thất bại?
94. Làm sao phân biệt điều chỉnh hợp lệ và gian lận?
95. Vì sao một coverage active là ràng buộc dữ liệu chứ không chỉ là code?
96. Nếu hãng trả về máy đã sửa sau nhiều tháng, thời hạn bảo hành tính thế nào?
97. Nếu serial thay thế đã có coverage cũ thì sao?
98. Nếu người dùng sửa số lượng sau khi chọn serial?
99. Nếu import Excel có serial trùng một phần?
100. Nếu chuyển kho giữa hai kho cùng lúc?
101. Nếu hóa đơn bán không gắn ngay phiếu xuất?
102. Ranh giới giữa lỗi dữ liệu và lỗi nghiệp vụ?
103. Số liệu dashboard lấy từ bảng nào?
104. Làm sao chứng minh báo cáo khớp ledger?
105. Có thể mở rộng sang barcode/RFID như thế nào?

## 9. Mẫu trả lời an toàn khi bị hỏi ngoài phạm vi

“Trong phạm vi đồ án hiện tại, em đã triển khai/kiểm thử phần … bằng … . Phần … chưa được triển khai nên em không khẳng định là đã hoàn thiện; hướng xử lý tiếp theo là … .”

## 10. Checklist trước khi báo cáo

- [ ] Nói được bài toán trong 60 giây.
- [ ] Vẽ lại được kiến trúc 5 tầng.
- [ ] Giải thích Balance, Ledger, Serial, Coverage, Claim, Audit.
- [ ] Kể được demo từ nhập → bán → bảo hành → đổi.
- [ ] Nêu rõ Posted không sửa trực tiếp.
- [ ] Nói đúng giới hạn: desktop, SQL Server tập trung, chưa load test lớn.
- [ ] Không đọc số liệu slide nếu chưa xác nhận nó thuộc phạm vi test nào.
- [ ] Chuẩn bị database reset và video dự phòng.


## 11. Đáp án ngắn gọn cho 105 câu hỏi

Học theo công thức: trả lời kết luận trước, nêu cơ chế sau, chỉ thêm ví dụ khi giáo viên hỏi tiếp.

### A. Bài toán và phạm vi

1. Vì kho và bảo hành rời rạc; đồ án nối tồn kho với lịch sử serial.
2. Khác CRUD ở trạng thái, ledger, audit và vòng đời bảo hành.
3. Quản trị, quản lý, kho, bán hàng, bảo hành.
4. Nguồn nhập, kho, trạng thái, bán hàng, khách hàng, bảo hành.
5. Serial truy từng thiết bị; hàng thường chỉ cần số lượng.
6. Loại web/mobile, thanh toán online, kế toán đầy đủ và load test lớn.
7. Một kho giúp triển khai phase đầu; vẫn giữ WarehouseId để mở rộng.
8. Tồn khóa theo ProductId–WarehouseId và thêm luồng chuyển kho.
9. Vì mục tiêu hiện tại là desktop nội bộ; web/mobile là hướng phát triển.
10. Truy vết đúng, tồn khớp ledger, nghiệp vụ an toàn, kiểm thử đạt.

### B. Nghiệp vụ

11. Quyền được cấp theo vai trò.
12. Từ lập phiếu đến validate, duyệt, ghi sổ.
13. Dòng hợp lệ, đủ quyền, trạng thái cho phép.
14. Serial tồn tại, đúng sản phẩm, chưa bán/khóa.
15. Từ chối ghi sổ và giữ nguyên dữ liệu.
16. OpeningBalance là tồn đầu kỳ; Purchase là nhập mua.
17. So sánh thực tế với sổ rồi lập điều chỉnh.
18. Để bảo toàn lịch sử; sửa sai bằng đảo/điều chỉnh.
19. Hóa đơn là giao dịch; chứng từ kho là biến động tồn.
20. Sau khi bán và ghi sổ thành công.
21. Coverage là quyền; Claim là một lần xử lý.
22. Có nhiều claim theo lịch sử, tối đa một claim mở theo policy.
23. Serial cũ trả hãng/hỏng; serial mới xuất thay thế.
24. Lưu lý do từ chối và audit.
25. Giao trả khách, ghi kết quả, đóng claim.

### C. Kiến trúc

26. WPF phù hợp ứng dụng Windows nội bộ.
27. MVVM tách UI khỏi trạng thái và nghiệp vụ.
28. Binding đồng bộ dữ liệu; Command đóng gói thao tác.
29. Code-behind chỉ hiển thị; nghiệp vụ nằm ở service.
30. ViewModel kiểm tra nhập liệu và lỗi hiển thị.
31. Service kiểm tra lại quyền, tồn, serial, transition, transaction.
32. Gom thao tác dữ liệu trong cùng ngữ cảnh/transaction.
33. EF Core ánh xạ C# với SQL Server.
34. SQL Server có transaction, khóa và index phù hợp.
35. Tái sử dụng Domain, Service, Data; thay lớp View.
36. Giảm thời gian khởi tạo lại màn hình.
37. Service bắt lỗi, rollback, log và báo lỗi dễ hiểu.
38. Gọi command với dependency giả rồi kiểm tra state.
39. Tách service theo use case để giảm phụ thuộc.
40. View → ViewModel → Service → Domain/Data.

### D. Cơ sở dữ liệu

41. Balance là số hiện tại; Ledger là lịch sử giải thích số đó.
42. Đối soát ledger và tạo điều chỉnh có audit.
43. Quan trọng nhất là Product làm trục, dòng chứng từ gắn header/sản phẩm, serial gắn sản phẩm và các bảng tồn–bảo hành cùng tham chiếu lại trục đó.
44. Một serial đại diện một thiết bị nên phải duy nhất.
45. Filtered unique index ngăn hai coverage trạng thái Active cùng một serial nhưng vẫn cho phép giữ lịch sử coverage đã đóng.
46. Không; đây là tham chiếu logic đa hình. FK vật lý của ledger chỉ tới kho, sản phẩm, serial tùy chọn và người ghi sổ.
47. Serial đã bán/đang bảo hành có thể không ở kho.
48. SQL Server `rowversion` 8 byte phát hiện sửa đồng thời; EF Core biểu diễn token đó bằng `byte[]`.
49. Index serial, sản phẩm–kho–trạng thái và ledger theo thời gian.
50. Kiểm tra tồn trong transaction trước khi giảm.
51. `UNIQUE(ProductId, UnitId)` ngăn đơn vị trùng và filtered unique bảo đảm tối đa một `IsBaseUnit = 1` cho mỗi sản phẩm.
52. `AuditLog` cho biết ai làm gì, lúc nào, trước/sau ra sao; `AuditArchiveManifest` chỉ chứng minh file log đã xuất có phạm vi, số dòng và SHA-256 nào.
53. Không xóa lịch sử; chỉ archive theo policy.
54. Giữ unique ProductId–WarehouseId và thêm kho nguồn/đích.
55. Index, phân trang, tổng hợp và summary theo kỳ.

### E. Đồng thời và transaction

56. Balance, serial, ledger, audit phải cùng thành công/thất bại.
57. Rollback toàn bộ.
58. Hai transaction khóa tài nguyên theo thứ tự khác nhau.
59. Khóa cùng thứ tự để giảm vòng chờ.
60. Thường dùng READ COMMITTED.
61. Dùng khi muốn tránh khóa dài.
62. So sánh RowVersion và yêu cầu xử lý xung đột.
63. Một transaction thành công; transaction kia bị từ chối/retry.
64. Retry giới hạn, chỉ với deadlock và thao tác có thể lặp.
65. Tăng block, deadlock và giảm throughput.

### F. Bảo mật và kiểm soát

66. Phân quyền theo RoleCode ở service.
67. Ẩn nút UI không thay thế kiểm tra quyền.
68. Kiểm tra ở application service trước thao tác nhạy cảm.
69. Audit đăng nhập, sửa/xóa, duyệt, ghi sổ, hủy, đổi quyền.
70. Chỉ vai trò được cấp quyền audit.
71. Lưu hash, không lưu mật khẩu thô.
72. Bắt buộc đổi mật khẩu lần đầu.
73. Áp dụng policy tách người lập và người duyệt nếu cần.
74. Hạn chế sửa/xóa và ghi audit.
75. Bảo vệ mật khẩu, khách hàng, bảo hành và audit.

### G. Kiểm thử và triển khai

76. Unit, ViewModel, service, transaction/state, quyền, integration.
77. Test xuất vượt tồn, serial đã bán và rollback.
78. Test transition hợp lệ và bị từ chối.
79. Test serial thay thế, coverage, ledger, audit.
80. Integration phụ thuộc SQL Server và cấu hình môi trường.
81. 95/95 là bộ test trình bày trên slide.
82. 891 là kết quả gate test đầy đủ vừa chạy; 95/95 là tập test được trình bày trên slide.
83. Dùng seed, reset script và backup.
84. Windows, .NET, SQL Server và connection string.
85. Không commit, báo lỗi và cho phép thử lại.
86. Cần backup/restore ở mức vận hành.
87. Chưa load test lớn; phải nói đúng giới hạn.
88. Desktop Windows, CSDL tập trung, chưa đo tải lớn.
89. Barcode/RFID, Web API, concurrency nâng cao, đa kho.
90. Chuẩn hóa policy, integration test và đo hiệu năng sớm.

### H. Phản biện sâu

91. Ledger giúp đối soát; chỉ cộng trừ khi truy vấn sẽ chậm.
92. Invoice không chứa đủ trạng thái vật lý và lịch sử bảo hành.
93. Audit nằm cùng transaction; audit lỗi thì rollback.
94. Bắt buộc quyền, lý do, chứng từ điều chỉnh và audit.
95. Đây là bất biến dữ liệu nên database phải hỗ trợ chặn lỗi.
96. Tính theo policy thời hạn còn lại lưu trong coverage/claim.
97. Kiểm tra coverage active và serial thay thế hợp lệ.
98. Validate lại trước commit; không tin riêng dữ liệu UI.
99. Validate toàn file, báo dòng lỗi, không ghi dở dang.
100. Khóa kho nguồn–đích theo thứ tự ổn định rồi ghi ledger.
101. Có thể liên kết sau theo policy nhưng không được sai tồn.
102. Dữ liệu sai là sai định dạng/quan hệ; nghiệp vụ sai là vi phạm rule.
103. Dashboard lấy dữ liệu tổng hợp từ bảng nghiệp vụ và ledger.
104. Đối chiếu biến động ledger với số dư đầu/cuối.
105. Thêm lớp đọc mã/ánh xạ serial, giữ nguyên transaction và truy vết.

### Ví dụ trả lời 20 giây

“Vì sao cần StockBalance và StockLedger?” — “Balance cho biết hiện còn bao nhiêu để tra cứu nhanh; Ledger giải thích số đó thay đổi từ phiếu nào. Ví dụ tồn 10 không đủ: hội đồng còn cần biết 10 được nhập, bán và điều chỉnh như thế nào.”
