# WarePro Defense Blueprint Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Thay deck hiện tại bằng bản 20 slide 16:9 theo WarePro Technical Blueprint, mỗi slide giao diện chỉ có một ảnh.

**Architecture:** Tạo một deck mới bằng `@oai/artifact-tool` thay vì sửa lớp hình thức cũ. Script đọc content lock mới, nhúng các asset đã tồn tại trong đồ án, xuất speaker notes và render từng slide để QA.

**Tech Stack:** JavaScript ES modules, `@oai/artifact-tool`, PowerPoint PPTX, Python QA scripts bundled.

## Global Constraints

- Nội dung chỉ lấy từ DOCX/PDF trong `E:\Minh\DATN\Final`.
- Visual system bám `C:\Users\player\Downloads\WarePro_Technical_Blueprint.pdf`.
- 20 slide, 16:9, khoảng 15 phút.
- Mỗi slide giao diện chỉ có một ảnh.
- Không dùng tím/violet.
- Có `[Sources]` trong speaker notes của mọi slide.

---

### Task 1: Khóa nội dung 20 slide

**Files:**
- Create: `.tmp/defense-ppt-blueprint/content-lock.json`
- Create: `.tmp/defense-ppt-blueprint/source-notes.txt`

- [ ] Tách slide vấn đề/mục tiêu và năm slide giao diện theo spec.
- [ ] Kiểm tra mọi claim với locator DOCX/PDF đã có.
- [ ] Xác nhận đúng 20 slide và không có thuật ngữ ngoài nguồn.

### Task 2: Dựng visual system Blueprint

**Files:**
- Create: `.tmp/defense-ppt-blueprint/build-blueprint-deck.mjs`

- [ ] Tạo slide 16:9 với nền giấy kem, lưới mờ và palette trích từ PDF.
- [ ] Tạo helper typography, caption, page marker và image frame dùng chung.
- [ ] Dựng 20 layout với silhouette thay đổi nhưng cùng hệ thống thị giác.

### Task 3: Nhúng ảnh và notes

**Files:**
- Modify: `.tmp/defense-ppt-blueprint/build-blueprint-deck.mjs`

- [ ] Nhúng sơ đồ đúng slide và dùng fit `contain`.
- [ ] Nhúng đúng một ảnh giao diện vào từng slide 13-17.
- [ ] Ghi talk track và `[Sources]` cho 20 slide.

### Task 4: Xuất và kiểm tra

**Files:**
- Create: `E:\Minh\DATN\Final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh_thuyet_trinh.pptx`
- Create: `.tmp/defense-ppt-blueprint/final-render/`
- Create: `.tmp/defense-ppt-blueprint/final-inspect.ndjson`

- [ ] Render đủ 20 slide và kiểm tra từng trang.
- [ ] Chạy `slides_test.py`; yêu cầu `Test passed. No overflow detected.`
- [ ] Kiểm tra 20 notes, 20 `[Sources]`, 20 tiêu đề và ảnh slide 13-17.
- [ ] Kiểm tra hash DOCX/PDF nguồn không đổi.
