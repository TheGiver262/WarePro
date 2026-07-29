# Chapter 3 Database Detail Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a new thesis DOCX whose Chapter 3 explains the database through one overview ERD and six detailed subsystem ERDs, with explicit entity relationships, cardinalities, constraints, and data flows.

**Architecture:** Preserve the Desktop source DOCX and perform all work in a task-local staging directory. Derive a checked relationship manifest from the current EF Core mapping and models, update/export the existing Draw.io sources, replace the current section 3.3 through stable text anchors, remove the duplicated Appendix B ERDs, then update Word fields and run structural plus visual QA before copying a new DOCX beside the source.

**Tech Stack:** Microsoft Word DOCX/OOXML, Python 3 with `python-docx`, Draw.io Desktop CLI, PowerShell Word COM automation, bundled Codex document/PDF QA scripts, EF Core model configuration and SQL Server schema scripts.

## Global Constraints

- Source DOCX: `C:\Users\player\Desktop\DATN\final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh.docx`.
- Final DOCX: `C:\Users\player\Desktop\DATN\final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh_CHI_TIET_CSDL_20260730.docx`.
- Never overwrite or modify the source DOCX or current final PDF.
- Use `QuanLyHangHoa/Data/AppDbContext.cs` and `QuanLyHangHoa/Models/` as relationship authority; use `Database/Schema/` and `Database/database_schema.sql` as verification sources.
- Keep all temporary work under `F:\Codex Project\ProductManagement_Antigravity\.tmp\chapter3-database-detail\`.
- Do not modify application code, the live database, personal thesis fields, or chapters outside necessary numbering/cross-reference updates.
- Keep Chapter 3 portrait except ERD pages; landscape margins must be top 3.5 cm, bottom 2.5 cm, left 2 cm, right 2 cm.
- Move the six detailed ERDs from Appendix B into Chapter 3 and remove the duplicated appendix copies.
- Do not publish, push, or replace the final PDF. Temporary PDFs are QA artifacts only.
- Do not commit DOCX, PNG, PDF, staging scripts, or Draw.io working copies unless the user explicitly asks.

---

### Task 1: Freeze the source and capture a structural baseline

**Files:**
- Read: `C:\Users\player\Desktop\DATN\final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh.docx`
- Read: `C:\Users\player\Desktop\DATN\final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh.pdf`
- Create: `.tmp/chapter3-database-detail/source.docx`
- Create: `.tmp/chapter3-database-detail/working.docx`
- Create: `.tmp/chapter3-database-detail/baseline.json`
- Create: `.tmp/chapter3-database-detail/inspect_baseline.py`

**Interfaces:**
- Consumes: Desktop source DOCX.
- Produces: immutable staged source, editable working copy, source SHA-256, and anchor/count baseline used by every later task.

- [ ] **Step 1: Create the isolated staging tree**

Run:

```powershell
rtk powershell -NoProfile -Command "New-Item -ItemType Directory -Force -Path '.tmp\chapter3-database-detail\drawio','.tmp\chapter3-database-detail\erd-png','.tmp\chapter3-database-detail\render' | Out-Null"
```

Expected: directories exist only under `.tmp\chapter3-database-detail`.

- [ ] **Step 2: Copy the source without changing it**

Run:

```powershell
rtk powershell -NoProfile -Command "Copy-Item -LiteralPath 'C:\Users\player\Desktop\DATN\final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh.docx' -Destination '.tmp\chapter3-database-detail\source.docx'"
rtk powershell -NoProfile -Command "Copy-Item -LiteralPath '.tmp\chapter3-database-detail\source.docx' -Destination '.tmp\chapter3-database-detail\working.docx'"
```

Expected: `source.docx` and `working.docx` have identical initial hashes.

- [ ] **Step 3: Write the baseline inspector**

Create `inspect_baseline.py` with:

```python
import hashlib
import json
import zipfile
from pathlib import Path

from docx import Document

root = Path(r"F:\Codex Project\ProductManagement_Antigravity\.tmp\chapter3-database-detail")
source = root / "source.docx"
source_pdf = Path(
    r"C:\Users\player\Desktop\DATN\final"
    r"\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh.pdf"
)
document = Document(source)

def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()

anchors = {}
for index, paragraph in enumerate(document.paragraphs):
    text = paragraph.text.strip()
    if text in {
        "3.3 Thiết kế cơ sở dữ liệu",
        "3.4 Thiết kế vòng đời chứng từ",
        "MỘT SỐ GIAO DIỆN BỔ SUNG",
    } or text.startswith("Hình PL.B."):
        anchors[text] = index

with zipfile.ZipFile(source) as archive:
    bad_member = archive.testzip()

result = {
    "source_sha256": digest(source),
    "source_pdf_sha256": digest(source_pdf),
    "paragraphs": len(document.paragraphs),
    "tables": len(document.tables),
    "inline_shapes": len(document.inline_shapes),
    "sections": len(document.sections),
    "anchors": anchors,
    "zip_bad_member": bad_member,
}
(root / "baseline.json").write_text(
    json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8"
)
print(json.dumps(result, ensure_ascii=False, indent=2))
assert result["zip_bad_member"] is None
assert anchors["3.3 Thiết kế cơ sở dữ liệu"] < anchors["3.4 Thiết kế vòng đời chứng từ"]
assert len([key for key in anchors if key.startswith("Hình PL.B.")]) == 6
```

- [ ] **Step 4: Run the baseline inspector**

Run:

```powershell
rtk C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8 .tmp\chapter3-database-detail\inspect_baseline.py
```

Expected: ZIP check passes; source baseline reports 711 paragraphs, 15 tables, 41 inline shapes, 28 sections, and all six `Hình PL.B.*` captions.

---

### Task 2: Build and validate the relationship manifest

**Files:**
- Read: `QuanLyHangHoa/Data/AppDbContext.cs`
- Read: `QuanLyHangHoa/Models/*.cs`
- Read: `Database/Schema/*.sql`
- Read: `Database/database_schema.sql`
- Create: `.tmp/chapter3-database-detail/relationships.json`
- Create: `.tmp/chapter3-database-detail/validate_relationships.py`

**Interfaces:**
- Consumes: current EF Core relationship configuration and FK nullability.
- Produces: `relationships.json`, the only source used to write cardinalities, relationship tables, and diagram corrections.

- [ ] **Step 1: Record the six subsystem groups**

Create `relationships.json` with these group keys and entity sets:

```json
{
  "master_data": ["Category", "Brand", "Unit", "Product", "ProductUnit", "Supplier", "Customer", "Warehouse"],
  "stock": ["StockIn", "StockInLine", "StockOut", "StockOutLine", "StockBalance", "StockLedger"],
  "control_and_serial": ["StockTransfer", "StockTransferLine", "StockCountSession", "StockCountLine", "StockAdjustment", "StockAdjustmentLine", "ProductSerial"],
  "invoices": ["PurchaseInvoice", "PurchaseInvoiceLine", "SalesInvoice", "SalesInvoiceLine"],
  "warranty": ["WarrantyCoverage", "WarrantyClaim", "ProductSerial", "SalesInvoice", "Customer", "StockOut"],
  "security_audit": ["AppUser", "AuditLog"]
}
```

Add a `relationships` array. Every entry must contain:

```json
{
  "source": "Product",
  "target": "ProductUnit",
  "source_cardinality": "1",
  "target_cardinality": "0..N",
  "foreign_key": "ProductUnit.ProductId",
  "required": true,
  "meaning_vi": "Một sản phẩm có thể có nhiều đơn vị quy đổi.",
  "source_location": "QuanLyHangHoa/Data/AppDbContext.cs:345"
}
```

Include every relationship named in sections 3.3.3–3.3.8 of the approved design. In particular, record:

- `ProductSerial.LastStockInLineId` as required;
- `CurrentWarehouseId`, `LastStockOutLineId`, `StockTransferLineId`, `ReplacementSerialId`, and `ReplacementStockOutId` as optional;
- `PurchaseInvoice.StockInId` and `SalesInvoice.StockOutId` as optional but uniquely indexed when present;
- `WarrantyClaim` → `WarrantyCoverage` as the composite FK `(WarrantyCoverageId, ProductSerialId)`;
- one active `WarrantyCoverage` per serial through the filtered unique index;
- `AuditLog.PerformedBy` as optional with `ON DELETE SET NULL`.

- [ ] **Step 2: Write the manifest validator**

Create `validate_relationships.py` that:

```python
import json
from pathlib import Path

repo = Path(r"F:\Codex Project\ProductManagement_Antigravity")
root = repo / ".tmp" / "chapter3-database-detail"
manifest = json.loads((root / "relationships.json").read_text(encoding="utf-8"))
db_context = (repo / "QuanLyHangHoa" / "Data" / "AppDbContext.cs").read_text(encoding="utf-8")
model_text = "\n".join(
    path.read_text(encoding="utf-8")
    for path in (repo / "QuanLyHangHoa" / "Models").glob("*.cs")
)

required_tokens = {
    "StockTransfer": "modelBuilder.Entity<StockTransfer>",
    "WarrantyCompositeFk": "HasForeignKey(d => new { d.WarrantyCoverageId, d.ProductSerialId })",
    "ActiveCoverageUnique": "UX_WarrantyCoverage_Active_PerSerial",
    "PurchaseStockUnique": "UX_PurchaseInvoice_StockInId",
    "SalesStockUnique": "UX_SalesInvoice_StockOutId",
    "AuditSetNull": "FK_AuditLog_PerformedBy",
}
missing = [name for name, token in required_tokens.items() if token not in db_context]
assert not missing, missing
assert "public int LastStockInLineId" in model_text
assert "public int? CurrentWarehouseId" in model_text
assert "public int? LastStockOutLineId" in model_text
assert "public int? StockTransferLineId" in model_text
assert len(manifest["relationships"]) >= 35
assert all(item["source_location"] for item in manifest["relationships"])
print(f"RELATIONSHIPS={len(manifest['relationships'])}")
print("RELATIONSHIP_MANIFEST=PASS")
```

- [ ] **Step 3: Run the validator**

Run:

```powershell
rtk C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8 .tmp\chapter3-database-detail\validate_relationships.py
```

Expected: at least 35 checked relationships and `RELATIONSHIP_MANIFEST=PASS`.

---

### Task 3: Audit, correct, and export the seven ERDs

**Files:**
- Read/copy: `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\Diagram\Drawio\Hinh_3_2_ERD_TongQuan_Doc_2026-07-27.drawio`
- Read/copy: `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\Diagram\Drawio\Hinh_PL_D_1_ERD_DanhMucSanPham.drawio`
- Read/copy: `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\Diagram\Drawio\Hinh_PL_D_2_ERD_ChungTuKhoSoDu.drawio`
- Read/copy: `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\Diagram\Drawio\Hinh_PL_D_3_ERD_KiemKeSerialTruyVet.drawio`
- Read/copy: `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\Diagram\Drawio\Hinh_PL_D_4_ERD_HoaDon.drawio`
- Read/copy: `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\Diagram\Drawio\Hinh_PL_D_5_ERD_BaoHanh.drawio`
- Read/copy: `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\Diagram\Drawio\Hinh_PL_D_6_ERD_NguoiDungAudit.drawio`
- Create: `.tmp/chapter3-database-detail/drawio/*.drawio`
- Create: `.tmp/chapter3-database-detail/erd-png/*.png`
- Create: `.tmp/chapter3-database-detail/verify_erd_exports.py`

**Interfaces:**
- Consumes: relationship manifest and seven existing editable ERD sources.
- Produces: seven corrected, high-resolution PNGs named `3_2_overview.png` and `3_3_1.png` through `3_3_6.png`.

- [ ] **Step 1: Copy the seven Draw.io sources into staging**

Run explicit `Copy-Item -LiteralPath ... -Destination ...` commands for all seven files. Do not edit the archive repository originals.

- [ ] **Step 2: Compare each diagram with `relationships.json`**

For each subsystem, inspect Draw.io XML and require every entity in its manifest group plus every cross-group bridge used by that diagram. Correct only:

- missing/current FK links;
- cardinality labels;
- nullability markers;
- the obsolete “thiết kế hướng phát triển” wording for `StockTransfer`;
- overlapping connectors or unreadably small labels.

Retain existing visual style. Do not add index lists to entity boxes.

- [ ] **Step 3: Export all diagrams with Draw.io Desktop CLI**

Use this command pattern for each file:

```powershell
rtk powershell -NoProfile -Command "& 'C:\Program Files\draw.io\draw.io.exe' --export --format png --scale 2 --border 20 --output '.tmp\chapter3-database-detail\erd-png\3_3_1.png' '.tmp\chapter3-database-detail\drawio\Hinh_PL_D_1_ERD_DanhMucSanPham.drawio'"
```

Expected: seven non-empty PNG files. If the CLI returns before writing, wait on the spawned Draw.io process once, then verify file existence; do not launch duplicate exports.

- [ ] **Step 4: Write and run the ERD export validator**

Create `verify_erd_exports.py`:

```python
from pathlib import Path
from PIL import Image

root = Path(r"F:\Codex Project\ProductManagement_Antigravity\.tmp\chapter3-database-detail\erd-png")
expected = ["3_2_overview.png"] + [f"3_3_{index}.png" for index in range(1, 7)]
for name in expected:
    path = root / name
    assert path.exists() and path.stat().st_size > 20_000, name
    with Image.open(path) as image:
        assert image.width >= 1600, (name, image.size)
        assert image.height >= 900, (name, image.size)
print("ERD_EXPORTS=7")
print("ERD_EXPORT_QA=PASS")
```

Run:

```powershell
rtk C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8 .tmp\chapter3-database-detail\verify_erd_exports.py
```

Expected: `ERD_EXPORTS=7` and `ERD_EXPORT_QA=PASS`.

---

### Task 4: Build the expanded Chapter 3 in the working DOCX

**Files:**
- Read: `docs/superpowers/specs/2026-07-30-chapter-3-database-detail-design.md`
- Read: `.tmp/chapter3-database-detail/relationships.json`
- Read: `.tmp/chapter3-database-detail/erd-png/*.png`
- Modify: `.tmp/chapter3-database-detail/working.docx`
- Create: `.tmp/chapter3-database-detail/build_chapter3_database.py`

**Interfaces:**
- Consumes: stable heading/caption anchors, checked relationship manifest, and seven ERD PNGs.
- Produces: working DOCX with the approved 3.3.1–3.3.10 structure and no duplicate Appendix B ERDs.

- [ ] **Step 1: Implement stable block replacement**

In `build_chapter3_database.py`, implement these interfaces:

```python
def find_unique_paragraph(document, exact_text: str):
    """Return one paragraph or raise when zero/multiple matches exist."""

def remove_between(start_paragraph, end_paragraph) -> None:
    """Remove sibling OOXML blocks after start and before end."""

def insert_paragraph_before(anchor, text: str, style: str):
    """Insert a paragraph immediately before anchor with an existing Word style."""

def insert_landscape_erd_before(anchor, image_path, caption: str) -> None:
    """Insert landscape section, centered image, Caption paragraph, then restore portrait."""
```

Fail closed unless these anchors are unique:

- `3.3 Thiết kế cơ sở dữ liệu`;
- `3.4 Thiết kế vòng đời chứng từ`;
- captions `Hình PL.B.1` through `Hình PL.B.6`;
- `MỘT SỐ GIAO DIỆN BỔ SUNG`.

- [ ] **Step 2: Replace the old section 3.3**

Keep the `3.3 Thiết kế cơ sở dữ liệu` Heading 2 paragraph. Remove content after it up to, but not including, `3.4 Thiết kế vòng đời chứng từ`.

Insert:

1. `3.3.1 Nguyên tắc thiết kế`;
2. `3.3.2 Mô hình dữ liệu tổng thể`;
3. `3.3.3 Danh mục và đối tác`;
4. `3.3.4 Nhập, xuất và tồn kho`;
5. `3.3.5 Điều chuyển, kiểm kê và số sê-ri`;
6. `3.3.6 Hóa đơn mua và hóa đơn bán`;
7. `3.3.7 Bảo hành`;
8. `3.3.8 Người dùng, phân quyền và nhật ký`;
9. `3.3.9 Ràng buộc toàn vẹn, chỉ mục và giao dịch`;
10. `3.3.10 Kết luận thiết kế cơ sở dữ liệu`.

Use Heading 3 for all ten items. Use the approved design as the prose source. For 3.3.3–3.3.8, insert in this exact order:

- purpose paragraph;
- subsystem ERD;
- relationship table with columns `Thực thể nguồn`, `Quan hệ`, `Thực thể đích`, `Ý nghĩa`;
- relationship/constraint explanation;
- one data-flow example;
- transition paragraph.

- [ ] **Step 3: Apply figure numbering and captions**

Use these captions in Chapter 3:

- `Hình 3.2 – Mô hình thực thể–liên kết tổng thể của cơ sở dữ liệu`;
- `Hình 3.3 – ERD danh mục và đối tác`;
- `Hình 3.4 – ERD nhập, xuất và tồn kho`;
- `Hình 3.5 – ERD điều chuyển, kiểm kê và truy vết số sê-ri`;
- `Hình 3.6 – ERD hóa đơn mua và hóa đơn bán`;
- `Hình 3.7 – ERD bảo hành`;
- `Hình 3.8 – ERD người dùng, phân quyền và nhật ký`.

Renumber later Chapter 3 figure captions sequentially and update every in-text reference that points to an affected figure. Do not alter figures in Chapters 1, 2, 4, or 5.

- [ ] **Step 4: Apply table numbering and captions**

Use these table captions:

- `Bảng 3.3 – Quan hệ thực thể của phân hệ danh mục và đối tác`;
- `Bảng 3.4 – Quan hệ thực thể của phân hệ nhập, xuất và tồn kho`;
- `Bảng 3.5 – Quan hệ thực thể của phân hệ điều chuyển, kiểm kê và số sê-ri`;
- `Bảng 3.6 – Quan hệ thực thể của phân hệ hóa đơn`;
- `Bảng 3.7 – Quan hệ thực thể của phân hệ bảo hành`;
- `Bảng 3.8 – Quan hệ thực thể của phân hệ người dùng và nhật ký`;
- `Bảng 3.9 – Các ràng buộc dữ liệu tiêu biểu`.

Renumber the existing matrix as `Bảng 3.10 – Ma trận phân quyền`, update all affected references, and retain one continuous Chapter 3 table sequence.

- [ ] **Step 5: Remove duplicated Appendix B content**

Remove the six image paragraphs and six `Hình PL.B.*` caption paragraphs. Remove the Appendix B heading/page furniture only when it contains no other content. Preserve the following `MỘT SỐ GIAO DIỆN BỔ SUNG` heading and its content.

- [ ] **Step 6: Apply exact section geometry**

For each ERD section:

```python
from docx.enum.section import WD_ORIENT
from docx.shared import Cm

section.orientation = WD_ORIENT.LANDSCAPE
section.page_width, section.page_height = section.page_height, section.page_width
section.top_margin = Cm(3.5)
section.bottom_margin = Cm(2.5)
section.left_margin = Cm(2.0)
section.right_margin = Cm(2.0)
```

Restore portrait sections to left 3.5 cm, right 2.5 cm, top 2 cm, bottom 2 cm. Link headers/footers consistently and keep page numbering continuous.

- [ ] **Step 7: Save only to the staged working copy**

Run:

```powershell
rtk C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8 .tmp\chapter3-database-detail\build_chapter3_database.py
```

Expected: script prints all ten inserted heading names, seven figure captions, `APPENDIX_B_ERD_CAPTIONS=0`, and saves `working.docx`.

---

### Task 5: Update Word fields and create a QA-only preview

**Files:**
- Modify: `.tmp/chapter3-database-detail/working.docx`
- Create: `.tmp/chapter3-database-detail/word-preview.pdf`
- Use: `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\06_Cong_cu_tao_do_an\update_thesis_fields.ps1`

**Interfaces:**
- Consumes: structurally edited working DOCX.
- Produces: updated TOC, table/figure lists, cross-references, pagination, and temporary Word-rendered PDF.

- [ ] **Step 1: Close any lock files**

Confirm no `~$working.docx` exists. If Word has the staged document open, close that document before automation.

- [ ] **Step 2: Update every field through Microsoft Word**

Run:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File "F:\DoAnTotNghiep_QuanLyKhoBaoHanh\06_Cong_cu_tao_do_an\update_thesis_fields.ps1" -DocxPath "F:\Codex Project\ProductManagement_Antigravity\.tmp\chapter3-database-detail\working.docx" -PdfPath "F:\Codex Project\ProductManagement_Antigravity\.tmp\chapter3-database-detail\word-preview.pdf"
```

Expected: output includes `DOCX=...working.docx`, `PDF=...word-preview.pdf`, and a positive `PAGES=` value.

- [ ] **Step 3: Reopen the DOCX after Word saves it**

Use `python-docx` to open `working.docx`. Expected: no package corruption and all ten new Heading 3 paragraphs remain.

---

### Task 6: Run structural and content regression QA

**Files:**
- Read: `.tmp/chapter3-database-detail/source.docx`
- Read: `.tmp/chapter3-database-detail/working.docx`
- Create: `.tmp/chapter3-database-detail/audit_output.py`
- Create: `.tmp/chapter3-database-detail/audit-result.json`
- Use: bundled document audit scripts under `C:\Users\player\.codex\plugins\cache\openai-primary-runtime\documents\26.727.11326\skills\documents\scripts\`

**Interfaces:**
- Consumes: baseline and Word-updated working DOCX.
- Produces: machine-readable proof that scope, headings, figures, sections, integrity, and non-target content are correct.

- [ ] **Step 1: Run packaged audits**

Run:

```powershell
rtk C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8 C:\Users\player\.codex\plugins\cache\openai-primary-runtime\documents\26.727.11326\skills\documents\scripts\heading_audit.py .tmp\chapter3-database-detail\working.docx
rtk C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8 C:\Users\player\.codex\plugins\cache\openai-primary-runtime\documents\26.727.11326\skills\documents\scripts\section_audit.py .tmp\chapter3-database-detail\working.docx
rtk C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8 C:\Users\player\.codex\plugins\cache\openai-primary-runtime\documents\26.727.11326\skills\documents\scripts\images_audit.py .tmp\chapter3-database-detail\working.docx
rtk C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8 C:\Users\player\.codex\plugins\cache\openai-primary-runtime\documents\26.727.11326\skills\documents\scripts\fields_report.py .tmp\chapter3-database-detail\working.docx
```

Expected: all scripts exit 0; no broken image relationship, invalid section, or missing field target.

- [ ] **Step 2: Implement exact custom assertions**

`audit_output.py` must assert:

```python
required_headings = [
    "3.3.1 Nguyên tắc thiết kế",
    "3.3.2 Mô hình dữ liệu tổng thể",
    "3.3.3 Danh mục và đối tác",
    "3.3.4 Nhập, xuất và tồn kho",
    "3.3.5 Điều chuyển, kiểm kê và số sê-ri",
    "3.3.6 Hóa đơn mua và hóa đơn bán",
    "3.3.7 Bảo hành",
    "3.3.8 Người dùng, phân quyền và nhật ký",
    "3.3.9 Ràng buộc toàn vẹn, chỉ mục và giao dịch",
    "3.3.10 Kết luận thiết kế cơ sở dữ liệu",
]
```

Also assert:

- source SHA-256 still equals `baseline.json`;
- `zipfile.ZipFile(...).testzip()` returns `None`;
- every required heading occurs once with style `Heading 3`;
- captions `Hình 3.2` through `Hình 3.8` occur once;
- captions `Bảng 3.3` through `Bảng 3.10` occur once and remain continuous;
- no `Hình PL.B.` caption remains;
- phrase `thiết kế hướng phát triển` is absent;
- `MỘT SỐ GIAO DIỆN BỔ SUNG` remains;
- all relationship table headers are present six times;
- no comments or tracked-change elements were introduced;
- normalized text before `3.3` and from `3.4` through the Appendix B boundary is unchanged except figure numbers/cross-references;
- content after `MỘT SỐ GIAO DIỆN BỔ SUNG` is unchanged;
- every landscape section has the approved rotated margins.

- [ ] **Step 3: Run custom audit**

Run:

```powershell
rtk C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8 .tmp\chapter3-database-detail\audit_output.py
```

Expected: `DOCX_ZIP=PASS`, `CONTENT_SCOPE=PASS`, `HEADINGS=10`, `ERD_CAPTIONS=7`, `LANDSCAPE_MARGINS=PASS`, and `DOCX_STRUCTURAL_QA=PASS`.

---

### Task 7: Render and inspect every page

**Files:**
- Read: `.tmp/chapter3-database-detail/working.docx`
- Create: `.tmp/chapter3-database-detail/render/page-*.png`
- Create: `.tmp/chapter3-database-detail/render/working.pdf`
- Use: `C:\Users\player\.codex\plugins\cache\openai-primary-runtime\documents\26.727.11326\skills\documents\render_docx.py`

**Interfaces:**
- Consumes: structurally passing working DOCX.
- Produces: page PNGs and visual evidence for every page, especially all ERD and section-transition pages.

- [ ] **Step 1: Render through the canonical document renderer**

Run:

```powershell
rtk C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8 C:\Users\player\.codex\plugins\cache\openai-primary-runtime\documents\26.727.11326\skills\documents\render_docx.py .tmp\chapter3-database-detail\working.docx --output_dir .tmp\chapter3-database-detail\render --emit_pdf
```

Expected: one `page-N.png` per PDF page and a non-empty PDF. If LibreOffice is unavailable, use `word-preview.pdf` from Task 5 with bundled PDF rendering instead and disclose the fallback.

- [ ] **Step 2: Inspect all rendered pages at 100%**

Check every page for clipping, overlap, missing glyphs, blank pages, broken tables, footer/page-number resets, and poor heading breaks.

For the seven ERD pages additionally require:

- readable table/entity text;
- visible cardinality markers;
- no edge crossing through entity boxes;
- caption on the same page;
- no image stretched beyond margins.

- [ ] **Step 3: Iterate until both gates pass**

For any defect, edit only the staged Draw.io/DOCX builder inputs, rebuild `working.docx`, rerun Tasks 5–7, and retain the latest audit outputs. Do not patch the final Desktop copy.

Expected final evidence: latest structural audit passes and latest complete page-image review finds zero visible defects.

---

### Task 8: Deliver the new DOCX without replacing the final source

**Files:**
- Read: `.tmp/chapter3-database-detail/working.docx`
- Create: `C:\Users\player\Desktop\DATN\final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh_CHI_TIET_CSDL_20260730.docx`
- Read-only verify: original Desktop DOCX and PDF

**Interfaces:**
- Consumes: final staged working DOCX with passing structural and visual QA.
- Produces: one new Desktop DOCX and final hashes proving original files were preserved.

- [ ] **Step 1: Obtain filesystem approval for the Desktop write**

Request escalation for the exact destination path. Do not broaden approval to the Desktop or `DATN` tree.

- [ ] **Step 2: Copy the passing staged DOCX**

Run:

```powershell
rtk powershell -NoProfile -Command "Copy-Item -LiteralPath 'F:\Codex Project\ProductManagement_Antigravity\.tmp\chapter3-database-detail\working.docx' -Destination 'C:\Users\player\Desktop\DATN\final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh_CHI_TIET_CSDL_20260730.docx'"
```

Expected: destination exists and its hash equals staged `working.docx`.

- [ ] **Step 3: Verify preservation and final deliverable**

Run exact hash and size checks on:

- original DOCX;
- original PDF;
- new DOCX;
- staged working DOCX.

Expected:

- original DOCX hash equals `baseline.json`;
- original PDF hash equals `baseline.json["source_pdf_sha256"]`;
- new DOCX hash equals staged working hash;
- new DOCX opens through `python-docx`;
- no `~$*.docx` lock file remains;
- no final PDF was created or replaced.

- [ ] **Step 4: Report only the requested artifact**

Return the new DOCX path, summarize the Chapter 3 expansion, state structural/visual QA evidence, and explicitly confirm the original DOCX/PDF were preserved. Do not deliver staging scripts, PNGs, or temporary PDFs.
