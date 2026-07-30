# ERD Draw.io và MVVM DOCX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tạo một file Draw.io bảy trang cùng bảy PNG ERD đã kiểm toán quan hệ, đồng thời thêm hình MVVM và cập nhật các trường Word trong DOCX mới nhất.

**Architecture:** Quan hệ CSDL được trích từ `AppDbContext.cs` thành một manifest kiểm toán. Một trình tạo XML tối giản dùng manifest, cấu hình phân hệ và tọa độ cố định để sinh file Draw.io nhiều trang; Draw.io Desktop xuất từng trang thành PNG. DOCX được chỉnh bằng `python-docx`, còn Microsoft Word COM cập nhật trường SEQ/TOC/danh mục và xuất PDF để rà hình ảnh.

**Tech Stack:** Python 3, `python-docx`, `xml.etree.ElementTree`, Draw.io Desktop 31.0.2, Microsoft Word COM, Poppler.

## Global Constraints

- Thực hiện trực tiếp trên `main`; không tạo worktree.
- Không sửa hoặc stage các thay đổi Git không liên quan.
- ERD không được chèn vào DOCX.
- DOCX chỉ thêm `MVVM.png`, chú thích hình, câu giải thích và cập nhật trường Word.
- Sửa trực tiếp DOCX mới nhất nhưng phải sao lưu vào thư mục tạm trước khi ghi.
- Các đầu nối Draw.io phải vuông góc, không xuyên qua bảng và hạn chế tối đa giao cắt.
- Không thêm dependency mới.

---

### Task 1: Tạo manifest kiểm toán quan hệ

**Files:**
- Create: `.tmp/erd-mvvm-revision/audit_relationships.py`
- Create: `.tmp/erd-mvvm-revision/relationships.json`
- Read: `QuanLyHangHoa/Data/AppDbContext.cs`
- Read: `QuanLyHangHoa/Models/*.cs`

**Interfaces:**
- Consumes: mã nguồn EF Core hiện tại.
- Produces: `relationships.json` với `principal`, `dependent`, `foreign_key`, `multiplicity`, `optional` và `source_line`.

- [ ] **Step 1: Viết bộ trích quan hệ**

Tạo script đọc từng khối `modelBuilder.Entity<T>` và nhận diện cả `WithMany`, `WithOne`, khóa ngoại đơn và khóa ngoại kép:

```python
from pathlib import Path
import json
import re

SOURCE = Path("QuanLyHangHoa/Data/AppDbContext.cs")
text = SOURCE.read_text(encoding="utf-8")

blocks = re.findall(
    r"modelBuilder\.Entity<(?P<entity>\w+)>\(entity\s*=>\s*\{(?P<body>.*?)^\s*\}\);",
    text,
    re.M | re.S,
)

rows = []
for dependent, body in blocks:
    for match in re.finditer(
        r"entity\.HasOne(?:<(?P<generic>\w+)>)?\("
        r"(?:d\s*=>\s*d\.(?P<nav>\w+))?\)"
        r"\.(?P<with>WithMany|WithOne)\([^)]*\)"
        r".*?\.HasForeignKey(?:<[^>]+>)?\((?P<fk>.*?)\)",
        body,
        re.S,
    ):
        line = text[: text.index(match.group(0))].count("\n") + 1
        rows.append(
            {
                "dependent": dependent,
                "principal_navigation": match.group("nav") or match.group("generic"),
                "foreign_key_expression": " ".join(match.group("fk").split()),
                "relationship": match.group("with"),
                "source_line": line,
            }
        )

Path(".tmp/erd-mvvm-revision/relationships.json").write_text(
    json.dumps(rows, ensure_ascii=False, indent=2),
    encoding="utf-8",
)
print(f"RELATIONSHIPS={len(rows)}")
```

- [ ] **Step 2: Chạy bộ trích**

Run:

```powershell
rtk proxy "C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" -X utf8 .tmp/erd-mvvm-revision/audit_relationships.py
```

Expected: `RELATIONSHIPS` lớn hơn 70 và `relationships.json` được tạo.

- [ ] **Step 3: Đối chiếu các gateway quan trọng**

Run:

```powershell
rtk rg -n "SupplierId|CustomerId|StockInId|StockOutId|WarrantyCoverageId|ProductSerialId|ReplacementStockOutId|CreatedBy|ApprovedBy|PostedBy|ProcessedBy" QuanLyHangHoa/Data/AppDbContext.cs
```

Expected: thấy đầy đủ quan hệ nhà cung cấp, khách hàng, kho, hóa đơn, bảo hành và người dùng.

- [ ] **Step 4: Kiểm tra manifest**

Run:

```powershell
rtk proxy "C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" -X utf8 -c "import json; p=json.load(open(r'.tmp/erd-mvvm-revision/relationships.json',encoding='utf-8')); assert any(x['dependent']=='PurchaseInvoice' and 'SupplierId' in x['foreign_key_expression'] for x in p); assert any(x['dependent']=='WarrantyClaim' and 'WarrantyCoverageId' in x['foreign_key_expression'] for x in p); print('MANIFEST_OK')"
```

Expected: `MANIFEST_OK`.

---

### Task 2: Sinh file Draw.io bảy trang và xuất PNG

**Files:**
- Create: `.tmp/erd-mvvm-revision/generate_drawio.py`
- Create: `.tmp/erd-mvvm-revision/WarePro_ERD_Tong_20260730.drawio`
- Create: `.tmp/erd-mvvm-revision/png/01_ERD_Tong_Quan.png`
- Create: `.tmp/erd-mvvm-revision/png/02_ERD_Danh_Muc_Doi_Tac.png`
- Create: `.tmp/erd-mvvm-revision/png/03_ERD_Nhap_Xuat_So_Du.png`
- Create: `.tmp/erd-mvvm-revision/png/04_ERD_Dieu_Chuyen_Kiem_Ke_Serial.png`
- Create: `.tmp/erd-mvvm-revision/png/05_ERD_Hoa_Don.png`
- Create: `.tmp/erd-mvvm-revision/png/06_ERD_Bao_Hanh.png`
- Create: `.tmp/erd-mvvm-revision/png/07_ERD_Nguoi_Dung_Nhat_Ky.png`

**Interfaces:**
- Consumes: `relationships.json`, model property declarations và thiết kế đã duyệt.
- Produces: một `<mxfile>` gồm bảy `<diagram>` và bảy PNG.

- [ ] **Step 1: Viết trình tạo Draw.io**

Script dùng các hàm:

```python
def add_table(parent, table_id, name, fields, x, y, width, height, external=False):
    """Tạo một bảng; external=True chỉ hiện tên và dùng màu xám."""

def add_edge(parent, edge_id, source, target, label="", cross_module=False):
    """Tạo connector entityRelationEdgeStyle; liên phân hệ dùng dashed=1."""

def build_overview():
    """Tạo sáu container; node chỉ có tên bảng."""

def build_detail(page_name, primary_entities, external_entities, edges, positions):
    """Tạo một trang chi tiết với bảng chính đầy đủ trường và node ngoài phân hệ màu xám."""

def write_mxfile(path):
    """Ghi bảy trang theo đúng thứ tự nghiệm thu."""
```

Mọi edge dùng:

```text
edgeStyle=orthogonalEdgeStyle;orthogonalLoop=1;jettySize=auto;
rounded=0;html=1;endArrow=none;
```

Quan hệ liên phân hệ thêm:

```text
dashed=1;dashPattern=6 4;strokeColor=#64748B;
```

Tọa độ phải dành hành lang 60–100 px giữa các bảng. Không đặt bảng nằm giữa nguồn và đích của một connector. Các edge song song phải xuất phát từ các cạnh khác nhau hoặc được tách bằng làn trống.

- [ ] **Step 2: Chạy trình tạo**

Run:

```powershell
rtk proxy "C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" -X utf8 .tmp/erd-mvvm-revision/generate_drawio.py
```

Expected: `PAGES=7`, không có lỗi XML.

- [ ] **Step 3: Kiểm tra cấu trúc file**

Run:

```powershell
rtk proxy "C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" -X utf8 -c "import xml.etree.ElementTree as ET; r=ET.parse(r'.tmp/erd-mvvm-revision/WarePro_ERD_Tong_20260730.drawio').getroot(); d=r.findall('diagram'); assert len(d)==7; print([x.get('name') for x in d])"
```

Expected: đúng bảy tên trang trong đặc tả.

- [ ] **Step 4: Xuất từng trang bằng Draw.io**

Run cho trang 1–7:

```powershell
rtk proxy "C:\Program Files\draw.io\draw.io.exe" --export --format png --border 20 --scale 2 --page-index 1 --output ".tmp\erd-mvvm-revision\png\01_ERD_Tong_Quan.png" ".tmp\erd-mvvm-revision\WarePro_ERD_Tong_20260730.drawio"
```

Lặp lại với `--page-index 2` đến `7` và tên PNG tương ứng.

Expected: bảy file PNG có kích thước lớn hơn 100 KB.

- [ ] **Step 5: Rà hình ảnh**

Mở từng PNG và kiểm tra:

- Không connector nào đi xuyên qua bảng hoặc chữ.
- Không có hai connector chồng lên nhau.
- Giao cắt được loại bỏ bằng cách đổi vị trí bảng hoặc hành lang.
- Tên bảng và thuộc tính đọc được ở 100%.
- Supplier/Customer có kết nối liên phân hệ.

Nếu có lỗi, chỉnh tọa độ trong `generate_drawio.py`, sinh lại và xuất lại toàn bộ trang bị ảnh hưởng.

---

### Task 3: Thêm hình MVVM và cập nhật DOCX

**Files:**
- Create: `.tmp/erd-mvvm-revision/backup/DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh_CHI_TIET_CSDL_20260730.docx`
- Create: `.tmp/erd-mvvm-revision/update_docx_mvvm.py`
- Modify: `C:\Users\player\Desktop\DATN\final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh_CHI_TIET_CSDL_20260730.docx`
- Read: `C:\Users\player\Desktop\DATN\final\MVVM.png`

**Interfaces:**
- Consumes: DOCX mới nhất và hình MVVM 1448 x 1086.
- Produces: DOCX có hình mới, Caption/SEQ đúng và trường Word đã cập nhật.

- [ ] **Step 1: Sao lưu DOCX**

Copy DOCX hiện tại vào thư mục backup và ghi SHA-256 của cả bản gốc lẫn backup.

- [ ] **Step 2: Viết script chèn hình**

Script phải:

1. Tìm heading `3.1.1 Trách nhiệm của các tầng`.
2. Tìm đoạn văn ngay sau heading.
3. Chèn một đoạn giải thích sau đoạn đó:

```text
Hình dưới minh họa luồng tương tác MVVM ở mức khái niệm. Khối Model bao quát dữ liệu và nghiệp vụ; trong WarePro, lớp dịch vụ, Entity Framework Core và SQL Server vẫn được tách riêng như Hình 3.1.
```

4. Chèn `MVVM.png` với chiều rộng tối đa bằng vùng nội dung trang.
5. Chèn Caption:

```text
Hình 3.2 – Luồng tương tác giữa View, ViewModel và Model trong MVVM
```

6. Dùng style `Caption` và trường Word `SEQ Hình \* ARABIC`.
7. Đặt hình và caption trước heading `3.2 Thiết kế các phân hệ`.

- [ ] **Step 3: Chạy script**

Run:

```powershell
rtk proxy "C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" -X utf8 .tmp/erd-mvvm-revision/update_docx_mvvm.py
```

Expected: `MVVM_INSERTED=1`.

- [ ] **Step 4: Cập nhật trường bằng Word COM**

Mở DOCX bằng Word COM, gọi:

```powershell
$doc.Fields.Update()
$doc.TablesOfContents | ForEach-Object { $_.Update() }
$doc.TablesOfFigures | ForEach-Object { $_.Update() }
$doc.Repaginate()
$doc.Save()
```

Sau đó xuất PDF QA vào `.tmp/erd-mvvm-revision/render/report-word.pdf`.

Expected: Word lưu DOCX không lỗi và PDF được tạo.

- [ ] **Step 5: Kiểm tra số hình và danh mục**

Run kiểm tra bằng `python-docx` và `pypdf`:

- Hình Chương 3 liên tục từ `3.1` đến `3.18`.
- `Hình 3.2` là hình MVVM.
- Danh mục hình chứa Hình 3.2.
- Danh mục bảng giữ đủ Bảng 3.1–3.10 và Bảng 4.1.
- Số trang trong danh mục khớp PDF Word.

---

### Task 4: QA cuối và chuyển đầu ra

**Files:**
- Copy: `.tmp/erd-mvvm-revision/WarePro_ERD_Tong_20260730.drawio` → `C:\Users\player\Desktop\DATN\final\WarePro_ERD_Tong_20260730.drawio`
- Copy: `.tmp/erd-mvvm-revision/png/*.png` → `C:\Users\player\Desktop\DATN\final\ERD_WarePro_20260730\`

**Interfaces:**
- Consumes: bảy PNG, file Draw.io và DOCX đã cập nhật.
- Produces: bộ đầu ra cuối trong `Desktop\DATN\final`.

- [ ] **Step 1: Rà toàn bộ trang DOCX**

Raster PDF Word bằng Poppler và kiểm tra tất cả trang, tập trung vào:

- Trang chứa mục 3.1.1.
- Trang Danh mục hình.
- Trang Danh mục bảng.
- Các trang Chương 3 bị dịch số.

- [ ] **Step 2: Kiểm tra tính toàn vẹn**

Run:

```powershell
rtk proxy "C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" -X utf8 -c "import zipfile; p=r'C:\Users\player\Desktop\DATN\final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh_CHI_TIET_CSDL_20260730.docx'; assert zipfile.ZipFile(p).testzip() is None; print('DOCX_ZIP_OK')"
```

Expected: `DOCX_ZIP_OK`.

- [ ] **Step 3: Chép Draw.io và PNG ra Desktop**

Chỉ chép sau khi bảy PNG đã đạt kiểm tra hình ảnh.

- [ ] **Step 4: Xác minh đầu ra**

Kiểm tra:

- File Draw.io có bảy trang.
- Thư mục PNG có đúng bảy ảnh.
- DOCX có hash mới, backup có hash cũ.
- Git chỉ còn các thay đổi không liên quan có sẵn; `.tmp` không bị stage.

- [ ] **Step 5: Báo cáo**

Bàn giao đúng ba đầu ra:

- DOCX đã sửa tại chỗ.
- File Draw.io tổng.
- Thư mục bảy PNG.

Không xuất PDF vào thư mục `final`.
