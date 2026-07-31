# Thiết kế chính sách đổi mới trong tháng đầu

Ngày duyệt thiết kế: 2026-07-31

## 1. Mục tiêu

WarePro phải tách rõ hai quyền lợi:

- Bảo hành thông thường có hiệu lực trong toàn bộ thời hạn bảo hành của sản phẩm.
- Đổi nguyên máy chỉ có hiệu lực trong tháng đầu sử dụng của từng serial và chỉ khi kỹ thuật viên xác nhận lỗi do nhà sản xuất.

Khách hàng đủ điều kiện đổi mới vẫn có thể chọn sửa chữa. Sau khi hết tháng đầu, hệ thống chỉ cho phép sửa chữa, thay linh kiện hoặc xử lý phần mềm; không cho đổi nguyên máy từ kho hay từ hãng.

## 2. Quy tắc nghiệp vụ đã duyệt

### 2.1. Mốc thời gian

- Mốc bắt đầu là `WarrantyCoverage.WarrantyStartDate` của serial đang được bảo hành.
- Với sản phẩm bán lần đầu, ngày này bằng ngày mua trên hóa đơn.
- Với sản phẩm đã được đổi mới, ngày này bằng ngày thực hiện đổi máy.
- Ngày cuối được đổi mới là `WarrantyStartDate.Date.AddMonths(1)`, tính bao gồm cả ngày đó.
- Điều kiện thời gian được xét bằng `WarrantyClaim.ReceivedDate.Date`, không dùng ngày kỹ thuật viên xử lý.

Ví dụ: coverage bắt đầu ngày 31/07 thì quyền đổi mới còn hiệu lực hết ngày 31/08. Claim tiếp nhận ngày 31/08 vẫn đủ điều kiện; claim tiếp nhận ngày 01/09 không đủ điều kiện.

### 2.2. Điều kiện đổi nguyên máy

Phiếu mới chỉ được đổi nguyên máy khi đồng thời thỏa mãn:

1. Claim được tiếp nhận không muộn hơn ngày cuối đổi mới đã chụp tại thời điểm tạo claim.
2. Kỹ thuật viên đã xác nhận lỗi do nhà sản xuất.
3. Khách hàng chọn phương án đổi mới.
4. Claim đang ở trạng thái cho phép hành động đổi máy.
5. Claim chưa từng phát sinh serial hoặc chứng từ thay thế.

Việc chọn hành động đổi máy và `ResolutionType` tương ứng là bằng chứng lựa chọn của khách hàng trong nghiệp vụ hiện tại; không thêm một cờ lựa chọn trùng lặp.

### 2.3. Sau tháng đầu

Claim vẫn được tiếp nhận nếu coverage bảo hành còn hiệu lực. Các hành động sửa tại cửa hàng, gửi hãng, nhận máy cũ đã sửa, thay linh kiện và xử lý phần mềm vẫn hoạt động. Mọi hành động tạo serial thay thế hoặc đổi nguyên máy phải bị từ chối.

### 2.4. Bảo hành của máy mới

Khi đổi máy:

- Coverage cũ chuyển sang `Inactive`.
- Serial cũ chuyển sang trạng thái đã thay thế theo luồng hiện có.
- Coverage mới bắt đầu từ ngày đổi.
- Coverage mới kết thúc tại `ngày đổi + Product.WarrantyPeriodMonths`.
- Serial mới có một tháng đổi mới riêng tính từ ngày bắt đầu coverage mới.

Không chuyển phần thời hạn còn lại của coverage cũ sang máy mới.

## 3. Dữ liệu

`WarrantyClaim` cần lưu:

- `ReplacementEligibleThrough` (`DateTime?`): ngày cuối được đổi mới, đã chụp khi tạo claim.
- `IsManufacturerDefect` (`bool`): kết luận lỗi nhà sản xuất.
- `ManufacturerDefectConfirmedBy` (`int?`): người xác nhận.
- `ManufacturerDefectConfirmedAt` (`DateTime?`): thời điểm xác nhận.

`ReplacementEligibleThrough` không được tính lại khi sửa coverage hoặc khi xử lý claim. Điều này bảo vệ quyền của khách đã tiếp nhận đúng hạn và tránh thay đổi kết quả do xử lý muộn.

Khi `IsManufacturerDefect` là `true`, người và thời điểm xác nhận phải có giá trị. Khi chưa xác nhận lỗi nhà sản xuất, hai trường này để trống.

Các trường mới tham gia cơ chế `RowVersion` hiện có của `WarrantyClaim`; mọi cập nhật vẫn phải đi qua service và kiểm tra đồng thời.

## 4. Tương thích dữ liệu cũ

Tại thời điểm migration:

- Tất cả claim đã tồn tại giữ `ReplacementEligibleThrough = null`.
- Claim có giá trị `null` được coi là claim theo chính sách cũ: tiếp tục được đổi máy theo state machine hiện tại, không bắt buộc cờ lỗi nhà sản xuất.
- Claim do ứng dụng tạo sau nâng cấp luôn có `ReplacementEligibleThrough` khác `null` và phải tuân thủ chính sách mới.
- Claim cũ đã đóng hoặc từ chối không thay đổi.

Cách dùng giá trị `null` tránh thêm một cờ phiên bản chính sách chỉ phục vụ một lần chuyển đổi.

## 5. Luồng service

### 5.1. Tạo claim

Trong cùng transaction tạo claim:

1. Tải active coverage còn hiệu lực của serial.
2. Chụp `ReplacementEligibleThrough = coverage.WarrantyStartDate.Date.AddMonths(1)`.
3. Lưu `ReceivedDate` và deadline cùng claim.
4. Khởi tạo trạng thái chưa xác nhận lỗi nhà sản xuất.

### 5.2. Kết luận kỹ thuật

Service nhận kết luận kỹ thuật và lựa chọn lỗi nhà sản xuất. Nếu xác nhận lỗi nhà sản xuất, service lưu người xác nhận và thời điểm xác nhận. Nếu chọn phương án `Replace`, service kiểm tra đầy đủ điều kiện đổi mới trước khi chuyển claim sang trạng thái sẵn sàng đổi.

Khách đủ điều kiện vẫn có thể chọn `Repair`; cờ lỗi nhà sản xuất không tự động buộc claim sang `Replace`.

### 5.3. Các điểm bắt buộc kiểm tra

Một hàm chính sách dùng chung phải được gọi tại mọi đường tạo máy thay thế:

- Chọn/kết luận phương án `Replace`.
- Đổi serial trực tiếp từ kho.
- Nhận máy mới do hãng đổi.

Kiểm tra tại UI chỉ để hướng dẫn người dùng. Service là biên thực thi cuối cùng và phải từ chối cả lời gọi trực tiếp không đi qua UI.

### 5.4. Tạo coverage mới

Luồng đổi từ kho và đổi từ hãng dùng chung một quy tắc tạo coverage mới:

- `WarrantyStartDate = ngày đổi`.
- `WarrantyEndDate = ngày đổi.AddMonths(product.WarrantyPeriodMonths)`.
- Giữ khách hàng và liên kết hóa đơn gốc để truy vết.
- `CoverageStatus = Active`.

Việc reconcile hóa đơn cũ không được kích hoạt lại coverage cũ hoặc ghi đè ngày bảo hành của serial thay thế.

## 6. UI

Màn hình xử lý bảo hành cần:

- Hiển thị ngày cuối được đổi mới.
- Hiển thị một trạng thái rõ ràng: còn quyền đổi mới, đã hết hạn đổi mới hoặc phiếu áp dụng chính sách cũ.
- Cho kỹ thuật viên xác nhận lỗi nhà sản xuất cùng kết luận kỹ thuật.
- Chỉ bật lựa chọn đổi máy khi claim đủ điều kiện.
- Luôn giữ lựa chọn sửa chữa khi coverage bảo hành còn hiệu lực.
- Khi không đủ điều kiện, hiển thị lý do cụ thể thay vì chỉ ẩn toàn bộ thông tin.

Phiếu tiếp nhận/in bảo hành bổ sung điều khoản:

- Lỗi nhà sản xuất được đổi máy trong tháng đầu nếu khách chọn đổi mới.
- Sau tháng đầu chỉ áp dụng bảo hành sửa chữa, thay linh kiện hoặc phần mềm.
- Máy đổi mới bắt đầu một thời hạn bảo hành đầy đủ mới.

## 7. Lỗi và an toàn giao dịch

- Service trả lỗi nghiệp vụ rõ ràng khi quá hạn, chưa xác nhận lỗi nhà sản xuất hoặc trạng thái không cho phép đổi.
- Không thay đổi tồn kho, serial, coverage hay claim nếu kiểm tra điều kiện thất bại.
- Kiểm tra điều kiện và ghi chứng từ đổi máy phải nằm trong cùng transaction `Serializable` hiện có.
- Cơ chế idempotency, unique link, `RowVersion` và kiểm tra đã thay serial tiếp tục được giữ.
- Không suy luận lỗi nhà sản xuất từ nội dung văn bản của `TechnicalConclusion`.

## 8. Kiểm thử

Kiểm thử service tối thiểu:

- Claim mới trong tháng đầu, đã xác nhận lỗi nhà sản xuất, được đổi từ kho.
- Claim mới trong tháng đầu, đã xác nhận lỗi nhà sản xuất, được nhận máy mới từ hãng.
- Claim trong tháng đầu nhưng chưa xác nhận lỗi nhà sản xuất bị chặn.
- Claim sau ngày cuối một ngày bị chặn ở mọi đường đổi máy.
- Claim đúng ngày cuối vẫn được đổi.
- Claim tiếp nhận đúng hạn nhưng xử lý sau hạn vẫn được đổi.
- Claim đủ điều kiện vẫn chọn sửa chữa được.
- Claim cũ có deadline `null` giữ cơ chế đổi hiện tại.
- Ngày 31 cuối tháng và năm nhuận dùng đúng `DateTime.AddMonths`.
- Đổi máy tạo coverage đầy đủ mới theo `WarrantyPeriodMonths`.
- Coverage cũ `Inactive`; coverage mới `Active`; tồn kho và liên kết thay thế nhất quán.
- Lỗi điều kiện không để lại thay đổi một phần.

Kiểm thử ViewModel/XAML tối thiểu:

- Trạng thái đủ điều kiện và lý do không đủ điều kiện hiển thị đúng.
- Lệnh đổi máy chỉ khả dụng khi chính sách và state machine cùng cho phép.
- Lựa chọn sửa chữa không bị mất sau tháng đầu.
- Điều khoản in chứa chính sách mới.

## 9. Phạm vi

Bao gồm model, EF Core migration/model configuration, schema cài đặt mới, service, ViewModel, XAML, phiếu in và kiểm thử liên quan.

Không bao gồm hoàn tiền, đổi sang sản phẩm khác model, cấu hình số tháng đổi mới theo từng sản phẩm hoặc thay đổi quy trình phân quyền hiện tại.
