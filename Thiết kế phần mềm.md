THIẾT KẾ PHẦN MỀM QUẢN LÝ HÀNG HÓA VÀ BẢO HÀNH

1. Mục tiêu hệ thống
	1.1 Quản lý danh mục nền: người dùng, đơn vị tính, loại hàng, thương hiệu, nhà cung cấp, khách hàng và kho.
	1.2 Quản lý sản phẩm theo hai nhóm:
		- Hàng không quản lý serial.
		- Hàng quản lý serial theo từng thiết bị/đơn vị bán.
	1.3 Quản lý nhập kho, nhập tồn đầu kỳ từ Excel/CSV, xuất kho, kiểm kê, điều chỉnh tồn, đảo nghiệp vụ và truy vết lịch sử kho.
	1.4 Quản lý hóa đơn mua và hóa đơn bán để theo dõi tiền trước thuế, tiền thuế và tổng thanh toán của giao dịch thương mại.
	1.5 Quản lý quyền bảo hành của sản phẩm đã bán và toàn bộ hồ sơ yêu cầu bảo hành phát sinh sau bán hàng.
	1.6 Cung cấp báo cáo tồn kho, nhập xuất tồn, doanh thu, tình trạng bảo hành và nhật ký thay đổi nghiệp vụ.
	1.7 Hỗ trợ tìm kiếm và sắp xếp dữ liệu trên các màn hình danh mục, sản phẩm, serial, chứng từ kho, import đầu kỳ, hóa đơn, bảo hành và báo cáo.

2. Phạm vi nghiệp vụ và quyết định nền tảng
	2.1 Hệ thống là ứng dụng desktop nội bộ cho doanh nghiệp bán hàng có kho và có tiếp nhận bảo hành.
	2.2 Hệ thống mục tiêu sử dụng WPF/C#, SQL Server, kiến trúc MVVM.
	2.3 Diagram là target baseline cho dự án; code hiện tại chỉ là nguyên mẫu tham chiếu.
	2.4 Mô hình phân quyền của phase thiết kế này là mô hình vai trò cố định:
		- Giữ AppUser.RoleCode làm vai trò chuẩn.
		- Không đưa thêm bảng Role/Permission vào ERD phase này.
		- Quyền chi tiết được chuẩn hóa bằng ma trận màn hình/hành động trong tài liệu.
	2.5 Mô hình kho được chốt là future-ready một kho mặc định:
		- Dùng bảng Warehouse để chuẩn bị mở rộng nhiều kho về sau.
		- Phase này chỉ có một kho mặc định đang hoạt động và bị ẩn trên giao diện người dùng.
		- Không dùng bảng WarehouseLocation ở phase này.
		- StockBalance được quản lý theo Product + Warehouse.
		- StockLedger lưu lịch sử biến động theo Product hoặc ProductSerial trong ngữ cảnh Warehouse.
	2.6 StockBalance là nguồn chuẩn cho tồn hiện tại theo sản phẩm và kho.
	2.7 StockLedger là nguồn chuẩn cho lịch sử biến động tồn theo kho và phục vụ audit mức nghiệp vụ kho.
	2.8 Product.Quantity không còn được coi là nguồn chuẩn cho tồn vật lý; nếu tồn tại ở code thì chỉ là giá trị cache hoặc trường hỗ trợ hiển thị.
	2.9 Hóa đơn thương mại và chứng từ kho là hai khái niệm riêng:
		- Chứng từ kho dùng để làm thay đổi tồn.
		- Hóa đơn dùng để ghi nhận giao dịch thương mại (giá trị hàng hóa, thuế).
		- Chứng từ nhập đầu kỳ `OpeningBalance` không mặc định sinh hóa đơn mua.
		- Chứng từ bảo hành/đổi mới không mặc định sinh hóa đơn thương mại.
		- Mọi hóa đơn được coi là đã thanh toán đầy đủ tại thời điểm phát sinh. Hệ thống không theo dõi thanh toán từng phần hay công nợ tồn đọng.
	2.11 Nghiệp vụ nhập tồn đầu kỳ từ Excel/CSV được ánh xạ vào chứng từ `StockIn` loại `OpeningBalance`.
		- Phase này không bổ sung các bảng ImportBatch, ImportRowError hay lưu file import gốc.
		- Import chỉ là workflow ở tầng ứng dụng để sinh dữ liệu chuẩn vào các bảng lõi hiện có.

3. Thuật ngữ và quy ước dùng thống nhất
	3.1 Chứng từ kho: chứng từ nghiệp vụ làm phát sinh hoặc điều chỉnh tồn, gồm nhập, xuất, điều chỉnh.
	3.2 Lập chứng từ: nhập thông tin nghiệp vụ và lưu ở trạng thái Draft.
	3.3 Duyệt chứng từ: kiểm tra nội dung và chuyển sang trạng thái Approved.
	3.4 Ghi sổ chứng từ: thực hiện transaction cập nhật StockBalance, ProductSerial, StockLedger và AuditLog.
	3.5 Tồn khả dụng: lượng hàng có thể xuất bán ngay, không bao gồm hàng giữ chỗ, hàng lỗi bảo hành hoặc hàng đang chờ xử lý.
	3.6 Quyền bảo hành: thông tin phạm vi và thời hạn bảo hành của serial đã bán, được lưu ở WarrantyCoverage.
	3.7 Hồ sơ bảo hành: từng lần tiếp nhận và xử lý thực tế, được lưu ở WarrantyClaim.
	3.8 Serial thay thế: serial được xuất để đổi mới trong bảo hành khi serial cũ không sửa được.
	3.9 Nhập tồn đầu kỳ: nghiệp vụ đưa dữ liệu tồn hiện tại của doanh nghiệp vào hệ thống khi bắt đầu sử dụng phần mềm, có thể đọc từ file Excel/CSV và ghi nhận bằng chứng từ `StockIn` loại `OpeningBalance`.
	3.10 Thuế cơ bản: lớp dữ liệu thương mại tối thiểu trên hóa đơn và dòng hóa đơn, gồm `SubTotal`, `TaxRate`, `TaxAmount`, `GrandTotal`, chỉ dùng để tính tiền và in chứng từ.
	3.11 Quy ước vẽ use case: use case diagram chỉ thể hiện mục tiêu nghiệp vụ mà actor chủ động thực hiện hoặc yêu cầu hệ thống thực hiện.
	3.12 Trong bản Mermaid flowchart, các đường nét đứt ghi nhãn “kế thừa quyền” là quy ước trình bày để người đọc dễ hiểu, không phải actor generalization UML chuẩn.
	3.13 Bản PlantUML là bản chuẩn hơn về mặt ký pháp actor generalization và được ưu tiên khi cần soi tính đúng UML.
	3.14 Các bước kỹ thuật nội bộ như quy đổi đơn vị, khóa dữ liệu tồn, cập nhật StockBalance, cập nhật ProductSerial, ghi StockLedger, ghi AuditLog phải thể hiện ở activity diagram hoặc sequence diagram, không biểu diễn như use case độc lập.
	3.15 Activity diagram ở bản PlantUML nên dùng swimlane/partition để tách rõ trách nhiệm giữa actor, UI, application service và repository/database; bản Mermaid có thể giữ ở mức business-flow gọn hơn.

4. Tác nhân và ma trận phân quyền
	4.1 Quản trị viên
		- Kế thừa toàn bộ quyền của Quản lý, Nhân viên kho, Nhân viên bán hàng và Nhân viên bảo hành.
		- Quản lý AppUser, tạo tài khoản, đặt mật khẩu tạm, reset mật khẩu, vô hiệu hóa tài khoản và gán RoleCode cho mọi vai trò.
		- Quản lý danh mục nền, kho, sản phẩm và đơn vị.
		- Tra cứu, tìm kiếm, sắp xếp trên mọi màn hình được cấp.
		- Xem báo cáo tổng hợp, audit log và cấu hình hệ thống.
	4.2 Quản lý
		- Kế thừa toàn bộ quyền của Nhân viên kho, Nhân viên bán hàng và Nhân viên bảo hành.
		- Được tạo tài khoản, đặt mật khẩu tạm, reset mật khẩu và gán RoleCode cho cấp dưới gồm Nhân viên kho, Nhân viên bán hàng và Nhân viên bảo hành.
		- Không được tạo tài khoản Quản trị viên, không được tự nâng quyền thành Quản trị viên và không được sửa quyền của Quản trị viên.
		- Duyệt chứng từ nhập, xuất, kiểm kê, điều chỉnh.
		- Phê duyệt quyết định đặc biệt của bảo hành như đổi mới hoặc từ chối bảo hành.
		- Xem báo cáo tồn kho, doanh thu, bảo hành và audit.
	4.3 Nhân viên kho
		- Lập phiếu nhập kho, nhập tồn đầu kỳ từ Excel/CSV, phiếu xuất kho, phiên kiểm kê, chứng từ điều chỉnh và hóa đơn mua.
		- Ghi sổ chứng từ kho sau khi đã được duyệt.
		- Quét, chọn và cập nhật serial ở các nghiệp vụ kho.
		- Lưu trữ và tra cứu lịch sử mua hàng qua hóa đơn mua.
		- Xuất serial thay thế trong bảo hành đổi mới đã được phê duyệt.
	4.4 Nhân viên bán hàng
		- Lập phiếu xuất kho trực tiếp theo quy trình doanh nghiệp.
		- Lập hóa đơn bán và nhập chi tiết hóa đơn bán, bao gồm thuế cơ bản theo dòng.
		- Tra cứu khách hàng, serial đã bán và tình trạng bảo hành.
	4.5 Nhân viên bảo hành
		- Tiếp nhận hồ sơ bảo hành.
		- Kiểm tra điều kiện bảo hành.
		- Ghi nhận kết quả kỹ thuật.
		- Sửa nội bộ, gửi hãng, cập nhật trạng thái xử lý, trả khách và đóng hồ sơ.
	4.6 Quy tắc phân quyền theo hành động
		- Tạo/sửa/vô hiệu hóa tài khoản và gán RoleCode mọi vai trò: Quản trị viên.
		- Tạo/sửa/vô hiệu hóa tài khoản, đặt mật khẩu tạm, reset mật khẩu và gán RoleCode cho cấp dưới: Quản lý; phạm vi chỉ gồm Nhân viên kho, Nhân viên bán hàng và Nhân viên bảo hành.
		- Lập phiếu nhập kho, nhập tồn đầu kỳ từ Excel/CSV, kiểm kê và điều chỉnh tồn: Nhân viên kho, Quản lý, Quản trị viên.
		- Lập phiếu xuất kho trực tiếp: Nhân viên bán hàng, Nhân viên kho, Quản lý, Quản trị viên.
		- Duyệt chứng từ nhập/xuất/kiểm kê/điều chỉnh: Quản lý, Quản trị viên.
		- Ghi sổ chứng từ nhập/xuất/điều chỉnh: Nhân viên kho, Quản lý, Quản trị viên hoặc vai trò kho được ủy quyền.
		- Lập hóa đơn mua và nhập chi tiết hóa đơn mua: Nhân viên kho, Quản lý, Quản trị viên.
		- Lập hóa đơn bán và nhập chi tiết hóa đơn bán: Nhân viên bán hàng, Quản lý, Quản trị viên.
		- Phê duyệt đổi mới/từ chối bảo hành: Quản lý, Quản trị viên.
		- Xuất serial thay thế: Nhân viên kho, Quản lý, Quản trị viên.
		- Đóng hồ sơ bảo hành: Nhân viên bảo hành, Quản lý, Quản trị viên theo chính sách override của doanh nghiệp.
	4.7 Quy tắc kế thừa quyền
		- Quản trị viên kế thừa toàn bộ quyền của tất cả tác nhân còn lại.
		- Quản lý kế thừa toàn bộ quyền của Nhân viên kho, Nhân viên bán hàng và Nhân viên bảo hành.
		- Kế thừa quyền không làm mất hiệu lực các rule kiểm soát nội bộ như tách người duyệt và người ghi sổ khi doanh nghiệp bật kiểm soát nội bộ.
		- Người lập và người duyệt có thể là cùng một người, đặc biệt đối với vai trò Quản trị viên và Quản lý.

5. Quy tắc nghiệp vụ cốt lõi
	5.1 Quy tắc chứng từ kho
		- Vòng đời chuẩn của chứng từ kho là: Draft -> PendingApproval -> Approved -> Posted -> Locked.
		- `StockIn` phải phân biệt tối thiểu các loại `Purchase` và `OpeningBalance`.
		- `StockOut` phải phân biệt tối thiểu các loại `Sale` và `WarrantyReplacement`.
		- Draft có thể bị Cancelled bởi người lập trước khi gửi duyệt.
		- PendingApproval có thể quay về Draft nếu bị từ chối và yêu cầu chỉnh sửa.
		- Chỉ trạng thái Posted mới được phép làm thay đổi tồn chính thức.
		- Sau khi Posted không được sửa trực tiếp chi tiết chứng từ; nếu sai phải dùng chứng từ điều chỉnh hoặc chứng từ đảo nghiệp vụ.
		- Locked là trạng thái đóng kỳ hoặc chốt chứng từ; không cho phép thay đổi tiếp.
		- Người lập và người duyệt không bắt buộc phải là hai người khác nhau.
		- Nếu bật kiểm soát nội bộ, người duyệt và người ghi sổ không được là cùng một người (ngoại trừ vai trò Quản trị viên và Quản lý có quyền override).
	5.2 Quy tắc kho và tồn
		- Mọi biến động tồn phải đồng thời cập nhật StockBalance, StockLedger và AuditLog trong cùng transaction.
		- Tồn phải được quản lý theo Product trong từng Warehouse.
		- Phase hiện tại luôn vận hành trên một kho mặc định; giao diện không bắt người dùng chọn kho nhưng application service vẫn phải gán `WarehouseId` mặc định một cách tường minh.
		- Tồn khả dụng và tồn giữ chỗ phải tách riêng.
		- Phase này không áp dụng quy trình giữ chỗ tự động cho bảo hành; việc đổi mới chỉ kiểm tra tồn khả dụng tại thời điểm xuất serial thay thế cho khách.
		- Nhập tồn đầu kỳ từ Excel/CSV phải được ghi nhận bằng `StockIn` loại `OpeningBalance`, không đi qua hóa đơn mua.
		- Khi xác nhận import tồn đầu kỳ, hệ thống phải tạo `StockIn`, `StockInLine`, `ProductSerial` nếu có, cập nhật `StockBalance`, ghi `StockLedger` và `AuditLog` trong cùng transaction.
		- Kiểm tra tồn và ghi sổ phải khóa StockBalance liên quan để tránh race condition.
		- Khi một transaction phải khóa nhiều sản phẩm hoặc nhiều serial, thứ tự khóa phải cố định theo ProductId tăng dần rồi ProductSerialId tăng dần để giảm nguy cơ deadlock.
	5.3 Quy tắc serial
		- Mỗi serial là duy nhất trong toàn hệ thống.
		- Với sản phẩm quản lý serial, số lượng giao dịch quy đổi phải khớp số serial hợp lệ được chọn hoặc quét.
		- Serial phải có trạng thái rõ ràng: InStock, Reserved, Sold, InWarrantyProcess, WarrantyDefective, ReturnedToManufacturer, Replaced, Inactive.
		- `Replaced` chỉ dùng cho serial thay thế đã được ghi sổ bằng `StockOut` loại `WarrantyReplacement` nhưng chưa xác nhận bàn giao xong cho khách; sau khi giao khách hoàn tất thì serial này phải chuyển về `Sold`.
		- Serial không theo dõi vị trí chi tiết trong kho; nếu đang còn trong kho thì phải gắn được `CurrentWarehouseId`, còn khi đã bán hoặc gửi hãng thì `CurrentWarehouseId` có thể để null.
	5.4 Quy tắc kiểm kê và điều chỉnh
		- Kiểm kê phải được thực hiện theo phiên kiểm kê riêng.
		- Chênh lệch kiểm kê không làm thay đổi tồn ngay; phải sinh chứng từ điều chỉnh và được duyệt/ghi sổ.
		- Điều chỉnh sau ghi sổ hoặc đảo nghiệp vụ phải tham chiếu chứng từ nguồn và lý do điều chỉnh.
	5.5 Quy tắc hóa đơn
		- Hóa đơn mua/hóa đơn bán theo dõi SubTotal, TaxAmount và GrandTotal.
		- PurchaseInvoiceLine và SalesInvoiceLine lưu ProductId, UnitId, Quantity, UnitPrice, SubTotal, TaxRate, TaxAmount, GrandTotal.
		- Dòng hóa đơn có thể tham chiếu ngược về StockInLine hoặc StockOutLine tương ứng nếu doanh nghiệp cần đối soát chi tiết.
		- Hệ thống mặc định hóa đơn đã được thanh toán đầy đủ.
		- Phase này chỉ hỗ trợ thuế cơ bản phục vụ tính tiền và in hóa đơn.
		- Hóa đơn mua có thể liên kết phiếu nhập đã ghi sổ.
		- Hóa đơn bán có thể liên kết phiếu xuất đã ghi sổ.
		- Nếu SalesInvoice được tạo sau StockOut loại Sale, hệ thống phải backfill WarrantyCoverage.SalesInvoiceId cho các coverage đã sinh từ phiếu xuất đó khi xác định được hóa đơn bán tương ứng.
		- Không bắt buộc mọi chứng từ kho đều có hóa đơn kèm theo.
	5.6 Quy tắc bảo hành
		- WarrantyCoverage lưu quyền bảo hành của serial đã bán.
		- Khi ghi sổ phiếu xuất loại `Sale` cho sản phẩm quản lý serial và có `WarrantyPeriodMonths > 0`, hệ thống phải tạo `WarrantyCoverage` ở trạng thái Active cho từng serial đã bán; `WarrantyStartDate = PostedAt`, `WarrantyEndDate = PostedAt + WarrantyPeriodMonths`, gắn `CustomerId` và `SalesInvoiceId` nếu đã có hóa đơn bán tại thời điểm đó.
		- WarrantyClaim lưu từng hồ sơ bảo hành thực tế và chỉ được tạo sau khi serial qua kiểm tra điều kiện bảo hành, xác định được WarrantyCoverage hợp lệ và không có claim đang mở khác.
		- Một serial có thể có nhiều WarrantyClaim theo thời gian, nhưng không được có hơn một WarrantyClaim đang mở tại cùng một thời điểm.
		- Khi tạo WarrantyClaim ở trạng thái Checking, serial nhận bảo hành phải chuyển sang InWarrantyProcess.
		- Khi kết luận lỗi hợp lệ và tiếp tục xử lý kỹ thuật hoặc gửi hãng, serial có thể chuyển sang WarrantyDefective theo policy hiển thị vận hành của doanh nghiệp; tối thiểu không được giữ nguyên Sold trong suốt thời gian claim đang mở.
		- Nếu sửa nội bộ được thì trả khách trực tiếp, không qua nhân viên kho.
		- Khi sửa xong và đã trả khách, serial được sửa phải quay về Sold hoặc trạng thái tương đương hàng đã bán đang hoạt động bình thường; không được chuyển về Sold trước khi xác nhận giao trả cho khách.
		- Nếu gửi hãng và hãng sửa được thì trả khách trực tiếp, không xuất serial mới.
		- Nếu hãng xác nhận không sửa được và phương án đổi mới được phê duyệt thì:
			+ Hệ thống phải sinh phiếu xuất kho loại WarrantyReplacement ở trạng thái Approved làm source document chuẩn cho chiều xuất thay thế.
			+ `ApprovedBy` của phiếu WarrantyReplacement là Quản lý đã phê duyệt quyết định đổi mới.
			+ `PostedBy` của phiếu WarrantyReplacement là tài khoản dịch vụ hệ thống hoặc system service account thực hiện ghi sổ trong transaction đổi mới; đây là ngoại lệ hợp lệ của rule tách người duyệt và người ghi sổ.
			+ Serial cũ chuyển ReturnedToManufacturer và được ghi nhận là gửi hãng theo luồng lỗi nặng/đổi mới.
			+ WarrantyCoverage của serial cũ phải được đóng hiệu lực ngay trong cùng transaction đổi mới, tối thiểu chuyển CoverageStatus sang Replaced hoặc Inactive do thay thế.
			+ Trước khi xuất serial thay thế cho khách, hệ thống phải kiểm tra tồn khả dụng hiện tại.
			+ Nếu không đủ tồn thay thế thì không được xuất cưỡng bức; hồ sơ giữ ở bước chờ xử lý tiếp theo và phải thông báo thiếu hàng thay thế.
			+ Nếu đủ tồn thì hệ thống ghi sổ phiếu xuất WarrantyReplacement để xuất serial thay thế từ tồn khả dụng.
			+ Trong transaction đổi mới, serial thay thế được chuyển sang `Replaced` để phản ánh đã được cấp cho claim nhưng chưa hoàn tất giao khách.
			+ WarrantyClaim phải lưu `ReplacementStockOutId` để truy vết trực tiếp tới phiếu xuất thay thế đã sinh.
			+ WarrantyCoverage của serial thay thế kế thừa thời hạn còn lại của serial cũ.
			+ WarrantyClaim lưu ReplacementSerialId.
			+ Ghi StockLedger và AuditLog cho chiều xuất thay thế với source document là StockOut WarrantyReplacement.
			+ Sau khi giao serial thay thế cho khách, hệ thống phải cập nhật `WarrantyClaim = ReturnedToCustomer`, chuyển serial thay thế từ `Replaced` sang `Sold`, rồi mới cập nhật `WarrantyClaim = Closed` trong một transaction nhỏ xác nhận giao nhận.
			+ Sau khi coverage cũ đã bị đóng hiệu lực do thay thế, serial cũ không được mở claim bảo hành mới nữa.
		- Nếu đổi mới nhưng thiếu hàng thay thế thì hồ sơ phải quay về WaitingDecision kèm ghi chú thiếu hàng; hồ sơ vẫn mở để chờ nhập thêm hàng hoặc quyết định nghiệp vụ khác, không được tự đóng.
		- Nếu từ chối bảo hành, hệ thống vẫn phải lưu lý do từ chối và luồng trả lại máy cho khách trước khi đóng hồ sơ.
		- Nếu từ chối bảo hành và đã trả lại máy cho khách, serial phải chuyển từ InWarrantyProcess hoặc WarrantyDefective về Sold hoặc trạng thái tương đương hàng đã bán đang hoạt động bình thường sau khi xác nhận giao trả.
	5.7 Quy tắc danh mục nền
		- Danh mục đã phát sinh giao dịch không được xóa cứng; chỉ được Inactive hoặc soft-delete.
		- Danh mục bị vô hiệu hóa không được chọn cho chứng từ mới nhưng vẫn phải hiện trên dữ liệu lịch sử.
		- Các thay đổi nhạy cảm trên AppUser.RoleCode, Product.DefaultPrice và trạng thái active/inactive của Product, Supplier, Customer phải được ghi AuditLog.
	5.8 Quy tắc tài khoản, mật khẩu và audit
		- Tài khoản nhân viên không tự đăng ký; chỉ Quản trị viên hoặc Quản lý trong phạm vi được cấp mới được tạo tài khoản.
		- Hệ thống chỉ lưu PasswordHash, không lưu mật khẩu thô và không ghi mật khẩu thô vào AuditLog.
		- Mật khẩu do Quản trị viên hoặc Quản lý đặt/reset là mật khẩu tạm; AppUser.MustChangePassword phải được bật để người dùng đổi mật khẩu ở lần đăng nhập đầu tiên hoặc sau khi reset.
		- Màn hình đăng nhập phải yêu cầu nhập đủ cả Username và Password trước khi cho submit; nếu thiếu một hoặc cả hai trường thì hiển thị lỗi validation ở UI và không đi vào luồng xác thực.
		- Đăng nhập luôn trả về thông báo chung như “Sai tài khoản hoặc mật khẩu”; không tiết lộ sai username, sai password hay tài khoản có tồn tại hay không.
		- Nếu AppUser.IsActive = false thì hệ thống từ chối đăng nhập ngay và vẫn dùng thông báo chung.
		- Nếu Username không tồn tại thì hệ thống không khóa tài khoản nào, không tăng FailedLoginCount trên AppUser, nhưng vẫn phải ghi AuditLog với ActionCode như LoginFailedUnknownUser; metadata có thể lưu AttemptedUsername và thời điểm thử đăng nhập.
		- Hệ thống theo dõi FailedLoginCount, LockoutUntil, LastFailedLoginAt, LastLoginAt trên AppUser để phục vụ khóa tạm thời và audit đăng nhập.
		- Chỉ trường hợp Username hợp lệ nhưng Password sai mới tăng FailedLoginCount và xét các ngưỡng khóa tạm thời.
		- Nếu Username hợp lệ và người dùng đã nhập sai Password quá 3 lần liên tiếp nhưng chưa tới ngưỡng khóa, UI hiển thị cảnh báo nhỏ: “Nhập sai tên đăng nhập/mật khẩu liên tiếp sẽ bị khóa tài khoản tạm thời”.
		- Nếu nhập sai 5 lần liên tiếp thì khóa đăng nhập 5 phút bằng LockoutUntil.
		- Nếu tiếp tục sai đến mốc 10 lần liên tiếp thì khóa đăng nhập 15 phút và ghi AuditLog với ActionCode như SuspiciousLoginAttempt để Quản trị viên thấy cảnh báo trên audit viewer; phase này chưa cần subsystem notification riêng.
		- Đăng nhập thành công phải reset FailedLoginCount về 0, xóa LockoutUntil và cập nhật LastLoginAt.
		- Quản lý chỉ được tạo/reset/gán RoleCode cho cấp dưới gồm Nhân viên kho, Nhân viên bán hàng và Nhân viên bảo hành.
		- Mọi thao tác tạo tài khoản, đổi RoleCode, reset mật khẩu, vô hiệu hóa/kích hoạt lại tài khoản phải ghi AuditLog với ActionCode rõ ràng như CreateUser, AssignRoleCode, ResetPassword, DisableUser, EnableUser.
		- Mọi lần đăng nhập thất bại, khóa tạm thời và cảnh báo đăng nhập nghi ngờ phải được ghi AuditLog với ActionCode như LoginFailed, LoginFailedUnknownUser, LoginLocked, SuspiciousLoginAttempt.
		- Với các sự kiện đăng nhập không gắn được vào một AppUser hợp lệ như LoginFailedUnknownUser, AuditLog.PerformedBy dùng tài khoản dịch vụ hệ thống hoặc SystemServiceAccount.
		- AuditLog cho reset mật khẩu chỉ ghi nhận sự kiện và người thao tác; BeforeJson/AfterJson không chứa mật khẩu thô và không cần lưu PasswordHash mới.
		- Tài khoản đã phát sinh giao dịch không được xóa cứng; chỉ được chuyển IsActive = false.
	5.9 Quy tắc tìm kiếm và sắp xếp
		- Tất cả màn hình dạng bảng phải hỗ trợ tìm kiếm nhanh theo mã, tên, serial, trạng thái, khách hàng, nhà cung cấp và ngày chứng từ.
		- Các bảng phải hỗ trợ sắp xếp tăng/giảm theo cột chính và cột thời gian.
		- Tìm kiếm và sắp xếp không mở rộng thêm quyền xem dữ liệu ngoài phạm vi được cấp.

6. Mô hình dữ liệu thiết kế
	6.1 Danh mục và người dùng
		- AppUser: tài khoản đăng nhập, FullName, PasswordHash, RoleCode, MustChangePassword, FailedLoginCount, LockoutUntil, LastFailedLoginAt, CreatedBy, CreatedAt, LastPasswordChangedAt, LastLoginAt, trạng thái hoạt động.
		- Category: loại sản phẩm.
		- Brand: thương hiệu.
		- Unit: đơn vị tính.
		- Supplier: nhà cung cấp.
		- Customer: khách hàng.
	6.2 Kho
		- Warehouse: kho vật lý hoặc kho logic; phase này phải seed sẵn một kho mặc định như `MAIN`, `IsDefault = true`, `IsActive = true`.
		- StockBalance: tồn hiện tại theo Product + Warehouse, bao gồm OnHandQuantity, AvailableQuantity, ReservedQuantity.
		- StockLedger: lịch sử biến động kho, có chứng từ nguồn, WarehouseId, chiều tăng/giảm, người ghi nhận và thời điểm ghi nhận.
	6.3 Sản phẩm và serial
		- Product: mã sản phẩm, tên, loại, thương hiệu, đơn vị mặc định, giá mặc định, xuất xứ, thời hạn bảo hành, cờ quản lý serial.
		- ProductUnit: cấu hình đơn vị theo từng sản phẩm, hệ số quy đổi, đơn vị cơ sở, đơn vị mua, đơn vị bán.
		- ProductSerial: serial, sản phẩm, trạng thái hiện tại, kho hiện tại nếu còn trong kho, dòng nhập gần nhất, dòng xuất gần nhất.
	6.4 Chứng từ kho
		- StockIn / StockInLine: nhập kho; `StockIn` phải có `WarehouseId` và `PurposeCode` để phân biệt tối thiểu `Purchase` và `OpeningBalance`.
		- StockOut / StockOutLine: xuất kho; `StockOut` phải có `WarehouseId` và `PurposeCode` để phân biệt ít nhất các loại `Sale` và `WarrantyReplacement`.
		- StockCountSession / StockCountLine: phiên kiểm kê và số liệu chênh lệch, gồm `WarehouseId`, CreatedBy, ApprovedBy, PostedBy, CountDate, ApprovedAt, PostedAt.
		- StockAdjustment / StockAdjustmentLine: chứng từ điều chỉnh tồn hoặc đảo nghiệp vụ sau ghi sổ, gồm `WarehouseId`, CreatedBy, ApprovedBy, PostedBy, ApprovedAt, PostedAt.
	6.5 Hóa đơn
		- PurchaseInvoice: hóa đơn mua, liên kết phiếu nhập nếu có, lưu SubTotal, TaxAmount, GrandTotal.
		- PurchaseInvoiceLine: dòng chi tiết hóa đơn mua, có thể tham chiếu StockInLine khi cần đối soát, lưu SubTotal, TaxRate, TaxAmount, GrandTotal.
		- SalesInvoice: hóa đơn bán, liên kết phiếu xuất nếu có, lưu SubTotal, TaxAmount, GrandTotal.
		- SalesInvoiceLine: dòng chi tiết hóa đơn bán, có thể tham chiếu StockOutLine khi cần đối soát, lưu SubTotal, TaxRate, TaxAmount, GrandTotal.
	6.6 Bảo hành
		- WarrantyCoverage: quyền bảo hành của serial đã bán.
		- WarrantyClaim: hồ sơ bảo hành phát sinh, có thể tham chiếu serial thay thế và phiếu xuất thay thế; phải lưu tối thiểu ProblemDescription, TechnicalConclusion, ManufacturerResult, RejectionReason và ProcessingNote.
	6.7 Audit
		- AuditLog: nhật ký thay đổi thực thể nghiệp vụ, trước/sau thay đổi, người thao tác, thời điểm thao tác.
	6.8 Quy ước nullability cần khóa rõ trước khi map sang entity/database
		- `StockIn.SupplierId`: nullable khi `PurposeCode = OpeningBalance`.
		- `ProductSerial.CurrentWarehouseId`: nullable khi serial đã bán, đang bảo hành ngoài kho hoặc đã gửi hãng.
		- `PurchaseInvoice.StockInId`: nullable, vì hóa đơn mua có thể chưa gắn ngay với một phiếu nhập cụ thể.
		- `SalesInvoice.StockOutId`: nullable, vì hóa đơn bán có thể được lập độc lập với một phiếu xuất cụ thể theo policy doanh nghiệp.
		- `WarrantyCoverage.SalesInvoiceId`: nullable, vì coverage của serial thay thế không bắt buộc phát sinh từ một hóa đơn bán thương mại mới.
		- `WarrantyClaim.ReplacementSerialId`: nullable, chỉ có giá trị khi claim đi vào nhánh đổi mới.
		- `WarrantyClaim.ReplacementStockOutId`: nullable, chỉ có giá trị khi hệ thống đã sinh phiếu xuất thay thế.
		- `ApprovedBy`, `PostedBy`, `ApprovedAt`, `PostedAt`: nullable cho đến khi chứng từ được duyệt/ghi sổ.
		- `ClosedDate`: nullable cho đến khi hồ sơ hoặc chứng từ thực sự đóng.
	6.9 Ràng buộc dữ liệu mức database nên khóa trước khi triển khai
		- Warehouse phải unique theo WarehouseCode và chỉ có tối đa một bản ghi `IsDefault = true` tại một thời điểm.
		- StockBalance phải unique theo cặp (ProductId, WarehouseId) để phản ánh đúng mô hình nhiều kho mở rộng.
		- ProductUnit phải unique theo cặp (ProductId, UnitId).
		- Mỗi Product chỉ có một ProductUnit với IsBaseUnit = true tại một thời điểm.
		- WarrantyClaim phải chặn hơn một claim đang mở cho cùng ProductSerialId tại cùng một thời điểm bằng filtered unique index hoặc cơ chế tương đương.
		- WarrantyCoverage nên chặn hơn một coverage active cho cùng ProductSerialId tại cùng một thời điểm bằng filtered unique index hoặc cơ chế tương đương.

7. Trạng thái chính cần triển khai
	7.1 Trạng thái chứng từ kho
		- Draft.
		- PendingApproval.
		- Approved.
		- Posted.
		- Locked.
		- Cancelled.
	7.2 Trạng thái serial
		- InStock.
		- Reserved.
		- Sold.
		- InWarrantyProcess.
		- WarrantyDefective.
		- ReturnedToManufacturer.
		- Replaced.
		- Inactive.
	7.3 Trạng thái hồ sơ bảo hành
		- Checking.
		- SentToManufacturer.
		- WaitingManufacturerResult.
		- WaitingDecision.
		- Repairing.
		- Repaired.
		- Replaced.
		- Rejected.
		- ReturnedToCustomer.
		- Closed.

8. Bảng quy tắc chuyển trạng thái chính
	8.1 Chứng từ kho
		- Draft -> PendingApproval: Người lập gửi duyệt.
		- PendingApproval -> Draft: Quản lý từ chối và trả về chỉnh sửa.
		- PendingApproval -> Approved: Quản lý duyệt; có thể trùng người lập nếu doanh nghiệp cho phép.
		- Draft -> Cancelled: Người lập hoặc quản lý hủy trước khi duyệt.
		- Approved -> Posted: Nhân viên kho ghi sổ.
		- Approved -> Cancelled: Quản lý hoặc kho hủy trước khi ghi sổ theo chính sách doanh nghiệp.
		- Posted -> Locked: Quản lý hoặc hệ thống khóa kỳ.
	8.2 Hồ sơ bảo hành
		- Checking -> SentToManufacturer: Kết luận cần gửi hãng.
		- Checking -> WaitingDecision: Có đủ dữ liệu để quyết định sửa, đổi mới hoặc từ chối.
		- SentToManufacturer -> WaitingManufacturerResult: Hãng đã tiếp nhận hồ sơ.
		- WaitingManufacturerResult -> WaitingDecision: Đã có kết luận từ hãng.
		- WaitingDecision -> Repairing: Quyết định sửa nội bộ.
		- WaitingDecision -> Repaired: Chấp nhận kết quả máy đã được hãng sửa xong.
		- WaitingDecision -> Replaced: Quyết định đổi mới đã được phê duyệt.
		- WaitingDecision -> Rejected: Quyết định từ chối bảo hành.
		- Repaired -> ReturnedToCustomer: Giao trả máy đã sửa.
		- Replaced -> ReturnedToCustomer: Giao serial thay thế.
		- Rejected -> ReturnedToCustomer: Trả lại máy bị từ chối bảo hành.
		- ReturnedToCustomer -> Closed: Đóng hồ sơ.

9. Danh sách transaction bắt buộc
	9.1 Ghi sổ phiếu nhập
		- Sắp xếp các dòng theo ProductId tăng dần.
		- Gán `WarehouseId` của chứng từ bằng kho mặc định ở phase hiện tại.
		- Khóa StockBalance theo đúng thứ tự ProductId đã sắp xếp trong Warehouse tương ứng.
		- Lưu chứng từ.
		- Tăng StockBalance.
		- Tạo ProductSerial nếu có.
		- Ghi StockLedger.
		- Ghi AuditLog.
	9.2 Ghi sổ phiếu xuất
		- Sắp xếp các dòng theo ProductId tăng dần.
		- Gán `WarehouseId` của chứng từ bằng kho mặc định ở phase hiện tại.
		- Khóa StockBalance theo đúng thứ tự ProductId đã sắp xếp trong Warehouse tương ứng.
		- Khóa serial được chọn theo ProductSerialId tăng dần nếu có.
		- Lưu chứng từ.
		- Giảm StockBalance.
		- Cập nhật ProductSerial.
		- Nếu là phiếu xuất loại `Sale` cho sản phẩm quản lý serial có thời hạn bảo hành thì tạo `WarrantyCoverage` Active cho các serial đã bán.
		- Ghi StockLedger.
		- Ghi AuditLog.
	9.3 Ghi sổ điều chỉnh tồn
		- Sắp xếp các dòng theo ProductId tăng dần.
		- Gán `WarehouseId` của chứng từ bằng kho mặc định ở phase hiện tại.
		- Khóa StockBalance liên quan theo đúng thứ tự ProductId đã sắp xếp trong Warehouse tương ứng.
		- Lưu chứng từ điều chỉnh.
		- Cập nhật StockBalance theo chiều tăng/giảm.
		- Cập nhật ProductSerial nếu có.
		- Ghi StockLedger.
		- Ghi AuditLog.
	9.4 Đổi mới trong bảo hành
		- Ghi nhận kết luận từ hãng là không sửa được và xử lý theo luồng đổi mới.
		- Khóa WarrantyClaim, WarrantyCoverage, StockBalance của sản phẩm thay thế và các serial liên quan theo thứ tự khóa cố định.
		- Kiểm tra tồn khả dụng trước khi xuất serial thay thế.
		- Sinh StockOut / StockOutLine loại WarrantyReplacement ở trạng thái Approved.
		- Cập nhật serial cũ sang ReturnedToManufacturer.
		- Đóng hiệu lực WarrantyCoverage cũ do thay thế trước khi tạo coverage mới.
		- Ghi sổ StockOut WarrantyReplacement để xuất serial thay thế nếu đủ tồn.
		- Tạo hoặc điều chỉnh WarrantyCoverage cho serial thay thế theo thời hạn còn lại.
		- Ghi StockLedger.
		- Ghi AuditLog.
	9.5 Nhập tồn đầu kỳ từ Excel/CSV
		- Đọc file Excel/CSV và map dữ liệu theo template import.
		- Validate ProductCode, UnitCode, số lượng, serial và dữ liệu trùng lặp trong file.
		- Gán `WarehouseId` mặc định cho toàn bộ dữ liệu import ở phase hiện tại.
		- Sinh `StockIn / StockInLine` loại `OpeningBalance`.
		- `OpeningBalance` là workflow ngoại lệ; khi người dùng xác nhận import hợp lệ, hệ thống sinh chứng từ ở trạng thái `Posted` và không đi qua quy trình duyệt chuẩn.
		- Tạo `ProductSerial` cho các dòng có serial.
		- Cập nhật `StockBalance`.
		- Ghi `StockLedger`.
		- Ghi `AuditLog`.

10. Kiến trúc phần mềm
	10.1 View
		- XAML chỉ chịu trách nhiệm hiển thị và binding.
	10.2 ViewModel
		- Điều phối dữ liệu màn hình, command, validation mức giao diện.
		- Không được là nguồn chuẩn duy nhất cho validation nghiệp vụ hoặc policy chuyển trạng thái.
	10.3 Application Service
		- AuthService.
		- AuthorizationService.
		- ApprovalService.
		- CatalogService.
		- InventoryService.
		- SalesService.
		- WarrantyService.
		- ReportingService.
		- Application Service là nơi điều phối use case và phải re-validate đầy đủ status transition, quyền ghi sổ, policy tách approver/poster và các rule nghiệp vụ quan trọng ngay cả khi ViewModel đã kiểm tra trước đó.
	10.4 Domain Model
		- Entity.
		- Enum trạng thái.
		- Quy tắc nghiệp vụ cốt lõi.
	10.5 Infrastructure
		- EF Core.
		- SQL Server.
		- Repository.
		- Transaction.
		- Logging.
		- Audit.

11. Mức độ ưu tiên triển khai
	11.1 Must-have for implementation
		- AppUser theo RoleCode cố định, mật khẩu tạm, bắt buộc đổi mật khẩu lần đầu và audit tạo/reset/gán role.
		- Warehouse với một kho mặc định ẩn trên UI phase hiện tại.
		- Product, ProductUnit, ProductSerial.
		- StockBalance, StockLedger.
		- StockIn, StockOut, StockCountSession, StockAdjustment.
		- Import tồn đầu kỳ từ Excel/CSV theo `StockIn` loại `OpeningBalance`.
		- WarrantyCoverage, WarrantyClaim.
		- PurchaseInvoice, PurchaseInvoiceLine, SalesInvoice, SalesInvoiceLine với thuế cơ bản.
		- AuditLog.
	11.2 Optional for phase 2
		- Mở rộng workflow giữ chỗ hoặc đặt hàng.
		- Mở rộng báo cáo đa chiều.
		- Mở rộng báo cáo đa chiều.
		- Tích hợp hãng bảo hành qua API.
		- Nếu dữ liệu báo cáo tăng lớn theo tháng/quý/năm, cân nhắc bổ sung indexed view hoặc bảng summary theo ngày cho ReportingService để tối ưu hiệu năng mà không khóa UI WPF.

12. Thứ tự triển khai đề xuất
	12.1 Auth và phân quyền.
	12.2 Danh mục nền và kho mặc định.
	12.3 Sản phẩm, đơn vị và serial.
	12.4 Nhập tồn đầu kỳ từ Excel/CSV.
	12.5 Nhập kho.
	12.6 Xuất kho.
	12.7 Kiểm kê, điều chỉnh và đảo nghiệp vụ.
	12.8 Hóa đơn và thuế cơ bản.
	12.9 Bảo hành.
	12.10 Báo cáo và audit viewer.

13. Danh sách sơ đồ đi kèm
	13.1 Sơ đồ kiến trúc MVVM và lớp ứng dụng.
	13.2 Use case tổng thể chi tiết.
	13.3 Use case tra cứu, tìm kiếm, sắp xếp và báo cáo.
	13.4 Use case nhập kho và hóa đơn mua.
	13.5 Use case xuất kho và hóa đơn bán.
	13.6 Use case kiểm kê, điều chỉnh và đảo nghiệp vụ.
	13.7 Use case bảo hành.
	13.8 Activity nhập kho ghi sổ.
	13.9 Activity nhập tồn đầu kỳ từ Excel/CSV.
	13.10 Activity xuất kho ghi sổ.
	13.11 Activity kiểm kê và điều chỉnh tồn.
	13.12 Activity xử lý bảo hành và đổi mới.
	13.13 Sequence đăng nhập.
	13.14 Sequence nhập kho ghi sổ.
	13.15 Sequence nhập tồn đầu kỳ từ Excel/CSV.
	13.16 Sequence xuất kho ghi sổ.
	13.17 Sequence bảo hành đổi mới.
	13.18 State chứng từ kho.
	13.19 State hồ sơ bảo hành.
	13.20 ERD chi tiết đầy đủ mọi bảng và toàn bộ khóa ngoại cứng.

	14. Quy ước dùng ERD chuẩn
	14.1 Chỉ dùng một ERD chi tiết làm nguồn chuẩn duy nhất cho phase thiết kế này.
	14.2 ERD phải thể hiện đầy đủ tất cả bảng hiện có và toàn bộ khóa ngoại cứng giữa các bảng.
	14.3 Các tham chiếu nghiệp vụ đa hình như StockLedger.SourceDocumentType + SourceDocumentId được mô tả bằng ghi chú, không ép thành một FK cố định sai bản chất.
	14.4 Các mã nghiệp vụ như DocumentCode, InvoiceCode, SessionCode, ClaimCode, ProductCode và SerialNumber phải được thiết kế với unique constraint hoặc unique index tương ứng.
