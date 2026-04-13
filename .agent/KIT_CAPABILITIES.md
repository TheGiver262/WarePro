# 🚀 Antigravity Kit - Hướng dẫn Chi tiết Năng lực & Chức năng

Antigravity Kit là một bộ công cụ mở rộng năng lực (Capability Expansion Toolkit) mạnh mẽ dành cho AI Agent, được thiết kế để chuẩn hóa và nâng cao quy trình phát triển phần mềm, thiết kế UI/UX, bảo mật và quản lý dự án.

---

## 📁 1. Cấu trúc Hệ thống (.agent/)

Tất cả các thành phần cốt lõi của kit được lưu trữ trong thư mục `.agent/`:

- **`agents/`**: Chứa định dạng cho 20 AI Agent chuyên gia.
- **`skills/`**: Chứa 36 mô-đun kiến thức và kỹ năng chuyên sâu.
- **`workflows/`**: Chứa các quy trình làm việc được kích hoạt bằng lệnh gạch chéo (`/`).
- **`scripts/`**: Các script Python master để kiểm tra và xác thực dự án.
- **`rules/`**: Các quy tắc toàn cục và giao thức hoạt động (GEMINI.md).

---

## 🤖 2. Hệ thống 20 Agent Chuyên gia (Specialist Agents)

Mỗi Agent đóng một vai trò chuyên biệt, có thể phối hợp với nhau để giải quyết các tác vụ phức tạp:

| Agent | Vai trò chi tiết |
| :--- | :--- |
| **`orchestrator`** | **Điều phối viên**: Quản lý nhiều agent cùng lúc cho các tác vụ đa ngành. |
| **`project-planner`** | **Lập kế hoạch**: Chuyên khám phá yêu cầu và lập sơ đồ nhiệm vụ chi tiết. |
| **`frontend-specialist`** | **Chuyên gia Frontend**: Thiết kế UI/UX hiện đại, tối ưu Performance và Accessibility. |
| **`backend-specialist`** | **Chuyên gia Backend**: Xây dựng API, Logic nghiệp vụ và quản lý Server. |
| **`database-architect`** | **Kiến trúc sư Database**: Thiết kế Schema, tối ưu truy vấn SQL/NoSQL. |
| **`debugger`** | **Chuyên gia Sửa lỗi**: Phân tích nguyên nhân gốc rễ (Root Cause) một cách hệ thống. |
| **`security-auditor`** | **Kiểm toán Bảo mật**: Quét lỗ hổng, đảm bảo tuân thủ tiêu chuẩn an toàn. |
| **`test-engineer`** | **Kỹ sư Kiểm thử**: Thiết kế chiến lược test (Unit, Integration, E2E). |
| **`performance-optimizer`** | **Tối ưu Hiệu suất**: Cải thiện tốc độ tải, Core Web Vitals và tài nguyên. |
| **`devops-engineer`** | **Kỹ sư DevOps**: Quản lý CI/CD, Docker và quy trình triển khai. |

*(Và các agent khác như: Mobile Developer, Game Developer, SEO Specialist, Documentation Writer...)*

---

## 🧩 3. Hệ thống 36 Skills (Kỹ năng Mô-đun)

Skills là các gói kiến thức mà Agent sẽ "nạp" vào tùy theo ngữ cảnh công việc.

### 🎨 Thiết kế & Giao diện (Frontend)
- **`frontend-design`**: Các nguyên tắc thiết kế UI/UX hiện đại.
- **`ui-ux-pro-max`**: Bộ thư viện khổng lồ với 50 style, 21 bảng màu và 50 font chữ cao cấp.
- **`tailwind-patterns`**: Các mẫu thiết kế sử dụng Tailwind CSS v4.
- **`web-design-guidelines`**: Hơn 100 quy tắc kiểm định giao diện từ Vercel.

### ⚙️ Lập trình & Logic (Backend/API)
- **`api-patterns`**: Thiết kế REST, GraphQL, tRPC chuẩn quốc tế.
- **`clean-code`**: Quy tắc viết code sạch, dễ bảo trì (Quan trọng nhất).
- **`nodejs-best-practices`**: Các khuôn mẫu lập trình Node.js an toàn và hiệu quả.
- **`python-patterns`**: Tiêu chuẩn lập trình Python và FastAPI.

### 🛡️ Bảo mật & Chất lượng
- **`vulnerability-scanner`**: Quét mã độc và lỗ hổng bảo mật (OWASP).
- **`red-team-tactics`**: Mô phỏng các cuộc tấn công để thử nghiệm phòng thủ.
- **`systematic-debugging`**: Quy trình 4 bước để xử lý mọi loại lỗi.
- **`testing-patterns`**: Các mô hình kiểm thử chuyên nghiệp.

---

## 🔄 4. Workflows - Các lệnh Slash (/)

Bạn có thể kích hoạt các quy trình làm việc tự động bằng cách gõ các lệnh sau:

- **`/plan`**: Khởi động Agent `project-planner` để lập kế hoạch chi tiết cho một tính năng mới.
- **`/create`**: Xây dựng một ứng dụng hoặc tính năng từ đầu (Sử dụng `app-builder`).
- **`/debug`**: Phân tích và sửa lỗi một cách khoa học.
- **`/orchestrate`**: Khi bạn cần sự góp ý từ nhiều chuyên gia (ví dụ: Security + Frontend).
- **`/ui-ux-pro-max`**: Thiết kế lại giao diện với độ thẩm mỹ cao nhất.
- **`/deploy`**: Chạy các bước kiểm tra cuối cùng trước khi đẩy sản phẩm lên server.

---

## 🛠️ 5. Scripts Tự động hóa & Kiểm định

Bộ kit đi kèm với các script Python để đảm bảo chất lượng code:

1.  **`checklist.py`**:
    - **Nhiệm vụ**: Kiểm tra nhanh các lỗi bảo mật, linting, schema và UX cơ bản.
    - **Khi nào dùng**: Trong quá trình lập trình hàng ngày trước khi commit code.
    - **Cách dùng**: `python .agent/scripts/checklist.py .`

2.  **`verify_all.py`**:
    - **Nhiệm vụ**: Xác thực toàn diện bao gồm: Hiệu suất (Lighthouse), Kiểm thử E2E (Playwright), Phân tích Bundle, và Kiểm tra Mobile.
    - **Khi nào dùng**: Trước khi release sản phẩm hoặc triển khai (Production).
    - **Cách dùng**: `python .agent/scripts/verify_all.py . --url <URL_CUA_BAN>`

---

## 💡 6. Cách vận dụng vào dự án hiện tại (QuanLyHangHoa)

Trong dự án C# / WPF / MVVM này, Antigravity Kit giúp ích cụ thể như sau:

- **Thiết kế UI**: Sử dụng `frontend-design` và `ui-ux-pro-max` để nâng cấp giao diện XAML từ cơ bản lên chuẩn "Premium".
- **Kiến trúc**: Agent `database-architect` có thể giúp bạn tối ưu file `AppDbContext.cs` và các câu truy vấn Entity Framework.
- **Lập kế hoạch**: Trước khi code thêm chức năng như "Quản lý Bảo hành", hãy dùng `/plan` để AI vẽ ra roadmap và các Edge Cases cần xử lý.
- **Sửa lỗi**: Khi gặp lỗi Crash trong WPF, hãy dùng `/debug` để AI truy vết qua các tầng ViewModel.

---
> **Lưu ý**: Để bộ kit hoạt động tốt nhất, AI luôn tuân thủ nguyên tắc **"Read → Understand → Apply"**. Trước mỗi nhiệm vụ, AI sẽ đọc file Agent và Skill tương ứng để đảm bảo kết quả đạt chất lượng cao nhất.
