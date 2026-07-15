# WarePro Reusable Engineering Playbook

Đây là phần kiến thức chắt lọc từ WarePro để dùng cho dự án mới. Không sao chép nguyên code; hãy sao chép cách ra quyết định, invariant, ranh giới transaction và chiến lược kiểm thử.

## 1. Trước khi viết code

Chốt một trang thiết kế tối thiểu gồm:

1. Người dùng và vai trò nào tồn tại?
2. Mười use case quan trọng nhất là gì?
3. Entity nào là dữ liệu nền, giao dịch, ledger và snapshot?
4. Nguồn sự thật của từng con số/trạng thái là bảng nào?
5. Trạng thái nào được phép chuyển sang trạng thái nào?
6. Một use case thay đổi những bảng nào và cần atomic đến đâu?
7. Dữ liệu nào không được hard delete?
8. Ai được thực hiện từng mutation?
9. Hành động nào phải audit?
10. Test nào chứng minh năm luồng quan trọng nhất?

Không bắt đầu từ danh sách màn hình. Bắt đầu từ vocabulary, workflow và invariant; màn hình chỉ là một cách thao tác lên chúng.

## 2. Blueprint kiến trúc mặc định

Cho ứng dụng nghiệp vụ desktop cỡ nhỏ–vừa:

```text
UI/View
  → Presentation Model/ViewModel
    → Application Service / Use Case
      → Domain policy + transaction boundary
        → ORM / Database
```

- View không chứa business rule.
- ViewModel điều phối, không phải authority cuối cùng.
- Application service kiểm tra quyền, invariant và transaction.
- Database lặp lại các invariant có thể biểu diễn bằng FK, unique index và check constraint.
- Ledger ghi lịch sử; snapshot/balance phục vụ truy vấn hiện tại.

Chỉ tách thêm project/domain layer khi coupling hoặc quy mô test chứng minh cần thiết. Một thư mục rõ ràng tốt hơn năm abstraction chỉ chuyển tiếp lời gọi.

## 3. Quy tắc nguồn sự thật

Với mỗi dữ liệu quan trọng, lập bảng:

| Câu hỏi | Nguồn chuẩn | Dữ liệu dẫn xuất |
|---|---|---|
| Tồn hiện tại? | Balance theo item + location | Tổng hiển thị trên product/dashboard |
| Lịch sử tồn? | Immutable ledger | Báo cáo nhập-xuất-tồn |
| Thiết bị đang ở đâu? | Serial/asset state | Badge trên danh sách |
| Tiền phải thu? | Invoice total/payment | KPI công nợ |
| Quyền bảo hành? | Coverage | Trạng thái hiệu lực tính theo ngày |

Không cho hai trường cùng đóng vai trò master. Nếu cần cache, đặt tên/ghi chú rõ và có một đường cập nhật duy nhất.

## 4. Transaction theo use case

Một transaction phải bao toàn bộ kết quả mà người dùng hiểu là “một việc”. Ví dụ `Post sale` có thể gồm:

- đổi trạng thái chứng từ;
- trừ balance;
- đổi trạng thái serial;
- ghi ledger;
- tạo/reconcile invoice hoặc warranty;
- ghi audit.

Nếu một bước lỗi, tất cả rollback. Không commit giữa chừng để lấy id trừ khi vẫn nằm trong cùng DB transaction.

Checklist:

- validate những gì có thể trước khi mở transaction;
- đọc lại state quan trọng trong transaction;
- dùng concurrency token/conditional update cho shared balance;
- tất cả service con dùng cùng context/transaction;
- commit một lần;
- side effect ngoài DB như file/email chỉ chạy theo chiến lược có thể phục hồi.

## 5. State machine thay cho chuỗi `if`

Mọi entity có workflow cần một bảng transition tập trung:

```text
Current state + Action + Role → Next state hoặc Reject
```

UI dùng bảng này để bật nút; service dùng cùng authority để enforce. Terminal state read-only. Action có side effect lớn phải idempotent hoặc có unique link chứng minh đã chạy.

Không cho màn hình “sửa trạng thái” dạng ComboBox đối với trạng thái có hệ quả nghiệp vụ.

## 6. Serial/asset tracking

Áp dụng không chỉ cho kho mà còn thiết bị, vé, license, xe hoặc tài sản:

- serial là entity có identity và lifecycle;
- quantity của dòng serial-tracked phải bằng số identity đã chọn;
- dialog chỉ thu thập selection; use case vẫn validate lại;
- derived quantity đi một chiều từ collection, tránh two-way handler ghi đè nhau;
- status/location chỉ đổi qua use case tạo ledger;
- unique constraint bảo vệ serial ở DB;
- retry không tạo serial hoặc movement thứ hai.

Regression test bắt buộc: chọn, bỏ chọn, xác nhận, mở lại, thay đổi số lượng, serial trùng, serial sai kho và hai request cạnh tranh.

## 7. Import an toàn

Pipeline chuẩn:

```text
Parse → Normalize → Validate all → Resolve references → Stage → Transactional write → Summary
```

- lỗi dòng nào báo đúng dòng/cột và cách sửa;
- không ghi DB khi còn đang parse các dòng sau;
- không để hai component cùng tạo entity phụ như serial;
- idempotency key hoặc unique business key chống import lặp;
- file import lớn mới cần chunk; đừng chunk sớm nếu phá atomicity nghiệp vụ;
- import phải đi qua cùng service/policy như thao tác UI.

## 8. Authorization và identity

Ba vòng kiểm soát:

1. navigation/visibility;
2. command availability;
3. service guard.

Vòng 3 là bắt buộc. Identity phải truyền rõ ràng, không dùng `userId = 1`, admin giả hoặc default role. Khi role/status thay đổi, reload session hoặc buộc đăng xuất. Các invariant như “ít nhất một admin hoạt động” nằm trong transaction tại service.

## 9. Audit và lịch sử

Audit là dữ liệu nghiệp vụ, không phải log debug. Ghi actor, timestamp UTC, action, entity, id, before/after và correlation/source document khi cần.

- mutation và audit cùng transaction;
- login failure không mạo danh tài khoản bị thử;
- archive có manifest/hash/row count/actor;
- reversal ghi bút toán ngược, không xóa ledger cũ;
- clock abstraction giúp test thời gian ổn định.

## 10. Dữ liệu nền và xóa

Mặc định:

- chưa được tham chiếu: có thể hard delete;
- đã được tham chiếu: deactivate;
- pháp lý/audit/ledger: append hoặc archive có kiểm soát.

Service trả dependency summary để UI nói: “Đơn vị đang được dùng bởi 12 sản phẩm; hãy ngừng hoạt động thay vì xóa.” Đây vừa đúng nghiệp vụ vừa hướng dẫn người dùng sửa thao tác.

## 11. UI nghiệp vụ

### 11.1 Design system

Tạo token/resource cho màu, typography, spacing, button, input, DataGrid, status chip và dialog trước khi nhân màn hình. Chọn một accent chính và bộ màu semantic; không dùng màu tùy hứng theo từng view.

### 11.2 Layout chuẩn

Một CRUD/business view có ba vùng ổn định:

1. context + primary action;
2. search/filter/export;
3. data + row actions + empty/error/loading state.

### 11.3 Lỗi phải có tính hướng dẫn

Thông báo tốt gồm:

- người dùng vừa làm sai gì;
- vì sao hệ thống không cho phép;
- dữ liệu nào đang thiếu/mâu thuẫn;
- bước đúng tiếp theo;
- câu hỏi bổ sung nếu hệ thống chưa đủ thông tin.

Ví dụ: “Bạn đã chọn 3 serial nhưng số lượng xuất là 4. Hãy chọn thêm 1 serial hoặc đổi số lượng xuất thành 3.”

### 11.4 WPF-specific

- xác nhận resource key và icon enum trước khi dùng;
- khai báo local resource trước `StaticResource` use;
- giữ code-behind thuần UI;
- pack URI phụ thuộc assembly name, phải sửa đồng bộ khi rebrand;
- build không bắt hết binding/resource runtime, cần XAML contract và smoke test.

## 12. Cache và đồng bộ giữa view

Cache view để giữ layout/state không được làm dữ liệu nghiệp vụ cũ. Mỗi ViewModel dữ liệu cung cấp `RefreshData()`:

1. tải vào collection tạm;
2. nếu thành công mới swap collection;
3. nếu lỗi giữ dữ liệu cũ và hiện trạng thái retry;
4. invalidate reference cache khi master data thay đổi.

Khi một mutation ảnh hưởng từ ba view trở lên, cân nhắc event/message invalidation. Với ít view, refresh-on-navigation đơn giản và dễ kiểm soát hơn.

## 13. Migration và tương thích dữ liệu

- model, mapping, migration, snapshot, initializer, seed và report phải đổi cùng nhau;
- migration cần xử lý schema/data cũ có thể tồn tại, không giả định DB luôn sạch;
- `Down` phải khả thi hoặc ghi rõ giới hạn;
- thử upgrade trên bản restore/disposable DB trước;
- constraint mới cần truy vấn chẩn đoán và chiến lược sửa dữ liệu cũ;
- không chạy migration phá dữ liệu trên production chỉ vì test in-memory xanh.

## 14. Chiến lược test mặc định

Mỗi bug phải có test tái hiện ở tầng thấp nhất vẫn chứng minh được lỗi.

| Rủi ro | Test phù hợp |
|---|---|
| Hàm tính/state machine | unit/table-driven |
| Transaction/constraint | SQLite relational |
| Command/refresh | ViewModel |
| Binding/resource | XAML contract |
| Provider/migration | disposable SQL Server |
| Hành vi người dùng | UI smoke/automation |

Đừng mock DbContext để chứng minh transaction hoặc constraint. Đừng dùng UI automation để kiểm tra mọi nhánh domain. Chọn tầng gần lỗi nhất, rồi thêm một smoke cho đường xuyên tầng quan trọng.

## 15. Definition of Done cho thay đổi nghiệp vụ

- [ ] Quy tắc và source of truth đã được viết rõ.
- [ ] Tất cả caller/view liên quan đã được tìm.
- [ ] Transaction bao đủ các bảng bị ảnh hưởng.
- [ ] Authorization được enforce tại service.
- [ ] Retry/concurrency/idempotency đã được xét.
- [ ] Audit và migration đã được cập nhật nếu cần.
- [ ] UI giữ layout hiện tại nếu không có yêu cầu thiết kế.
- [ ] Error message giải thích sai và chỉ cách đúng.
- [ ] Regression test tái hiện lỗi cũ đã xanh.
- [ ] Full non-real-DB tests và build xanh.
- [ ] SQL Server/UI smoke chạy khi rủi ro yêu cầu.
- [ ] Diff chỉ chứa file thuộc phạm vi.

## 16. Checklist khởi tạo dự án mới

### Nghiệp vụ

- [ ] Glossary và actor/use-case map.
- [ ] Entity relationship và ownership.
- [ ] Source-of-truth matrix.
- [ ] State-transition table.
- [ ] Authorization matrix.
- [ ] Audit/retention policy.

### Kỹ thuật

- [ ] Architecture boundaries và dependency direction.
- [ ] Transaction/concurrency/idempotency policy.
- [ ] Migration + seed strategy.
- [ ] Error/result vocabulary.
- [ ] Design tokens và screen patterns.
- [ ] Test pyramid, disposable DB và CI commands.
- [ ] Logging/crash handling và backup/restore.

### Trước phát hành

- [ ] Fresh install và upgrade từ bản cũ.
- [ ] Role-by-role smoke.
- [ ] Luồng chính có dữ liệu thật trên DB tạm.
- [ ] Đối chiếu source-of-truth sau mỗi mutation lớn.
- [ ] Rebrand check: title, icon, assembly, pack URI, docs.
- [ ] Không có secret, DB thật hoặc tài liệu local trong commit.

## 17. Những thứ không nên sao chép máy móc từ WarePro

- WPF nếu dự án cần web/mobile hoặc đa nền tảng.
- SQL Server nếu workload/triển khai phù hợp database khác.
- Role cố định nếu khách hàng cần permission tùy biến.
- View cache nếu dữ liệu cần real-time hoặc screen rẻ để tạo lại.
- Một project lớn nếu nhiều team cần ownership/deploy độc lập.
- Serial model nếu hàng hóa chỉ có số lượng tổng.
- Mọi library chỉ vì WarePro đang dùng; chọn dependency theo nhu cầu thật.

Phần nên tái sử dụng là tư duy: một nguồn sự thật, workflow có state machine, mutation atomic, quyền ở boundary, audit có nguồn gốc, test sở hữu dữ liệu và UI giải thích nghiệp vụ.

## 18. Cách dùng playbook ở dự án tiếp theo

1. Đọc yêu cầu và điền các checklist mục 1, 3, 4, 5, 8.
2. Đánh dấu phần nào giống WarePro và phần nào khác bản chất.
3. Viết design ngắn, gồm source-of-truth matrix và state transition.
4. Dựng vertical slice nhỏ nhất đi xuyên UI → service → DB → test.
5. Chỉ sau khi slice đúng mới nhân pattern sang module khác.
6. Mỗi lỗi mới được bổ sung vào bảng “sai lầm → nguyên nhân → guard/test”, không chỉ vá code.

Mục tiêu không phải làm dự án sau giống WarePro. Mục tiêu là không lặp lại những lỗi WarePro đã phải trả chi phí để phát hiện.
