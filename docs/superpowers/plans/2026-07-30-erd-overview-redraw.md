# ERD Overview Redraw Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Vẽ lại trang ERD tổng quan trong tệp Draw.io mới nhất, giữ nguyên sáu trang chi tiết.

**Architecture:** Sinh một `mxGraphModel` mới từ danh sách phân hệ, tọa độ bảng và manifest FK đã kiểm tra với `AppDbContext`. Ghép theo byte để chỉ thay khối `<diagram>` đầu tiên, sau đó chạy kiểm tra XML, quan hệ và hình học.

**Tech Stack:** Python 3 chuẩn, `xml.etree.ElementTree`, `unittest`, Draw.io XML.

## Global Constraints

- Làm trực tiếp trên `main`, không tạo worktree.
- Chỉ ghi đè `C:\Users\player\Desktop\DATN\final\WarePro_ERD_Tong_20260730.drawio`.
- Không sửa sáu trang chi tiết, DOCX hoặc tạo PNG.
- Không thêm dependency.
- `AppDbContext.cs` là nguồn chuẩn quan hệ.

---

### Task 1: Khóa manifest và bố cục tổng quan

**Files:**
- Create: `.tmp/erd-overview-redraw/redraw_overview.py`
- Create: `.tmp/erd-overview-redraw/test_overview.py`

**Interfaces:**
- Consumes: `extract_relationships(Path)` và tệp Draw.io hiện tại.
- Produces: `build_overview()` và `replace_overview_page()`.

- [ ] Viết kiểm thử số phân hệ, 31 bảng, 11 cạnh `ProductId`, quan hệ giả và tính bảo toàn trang 2–7.
- [ ] Chạy kiểm thử để xác nhận thất bại trước khi có bộ dựng.
- [ ] Cài manifest, tọa độ bảng, connector ER và lane vuông góc.
- [ ] Chạy kiểm thử đến khi tất cả PASS.

### Task 2: Ghép trang tổng quan và kiểm tra

**Files:**
- Create: `.tmp/erd-overview-redraw/verify_overview.py`
- Modify in place: `C:\Users\player\Desktop\DATN\final\WarePro_ERD_Tong_20260730.drawio`

**Interfaces:**
- Consumes: trang tổng quan mới và sáu trang chi tiết hiện tại.
- Produces: tệp Draw.io cuối cùng và báo cáo QA.

- [ ] Sao lưu tệp nguồn vào `.tmp/erd-overview-redraw/backup/`.
- [ ] Tạo candidate và xác nhận trang 2–7 không đổi byte.
- [ ] Kiểm tra XML, manifest FK, waypoint, số trang và số bảng.
- [ ] Ghi đè tệp Desktop sau khi candidate đạt QA.
- [ ] Chạy lại toàn bộ kiểm tra trên tệp cuối.
- [ ] Mở tệp bằng Draw.io và rà trực quan trang tổng quan.
