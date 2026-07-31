# WarePro ERD External Module Collapse Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rút gọn sáu ERD phân hệ bằng cách thay các thẻ bảng ngoài phân hệ bằng một hộp đại diện cho mỗi phân hệ sở hữu bảng.

**Architecture:** Biến đổi trực tiếp XML của file Draw.io mới nhất trên Desktop để giữ nguyên nội dung và layout thủ công của các bảng nội bộ. Bộ biến đổi chỉ giữ quan hệ ngoài khi bảng nội bộ sở hữu FK, gom các quan hệ đó theo `(phân hệ ngoài, bảng nội bộ)`, rồi bố trí hộp phân hệ và đường dependency orthogonal quanh vùng bảng nội bộ.

**Tech Stack:** Python 3 standard library (`xml.etree.ElementTree`, `hashlib`, `pathlib`), Draw.io XML, Draw.io CLI, `unittest`.

## Global Constraints

- Chỉ sửa sáu ERD phân hệ; trang `ERD tổng quan` phải byte-identical.
- Không sửa DOCX, PDF hoặc schema database.
- Dùng file `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio` mới nhất làm nguồn.
- Giữ đầy đủ bảng và quan hệ nội bộ của từng phân hệ.
- Chỉ giữ quan hệ ngoài khi FK nằm trên bảng thuộc phân hệ hiện tại.
- Không còn thẻ bảng riêng lẻ cho bảng ngoài phân hệ.
- Không dùng màu tím hoặc violet.
- Tạo backup ngay trước khi thay file Desktop.

---

### Task 1: Snapshot và kiểm thử hồi quy đỏ

**Files:**
- Create: `.tmp/erd-module-collapse/WarePro_ERD_Tong_20260730.source.drawio`
- Create: `.tmp/erd-mvvm-revision/test_external_module_collapse.py`
- Modify: `.tmp/erd-mvvm-revision/detail_merge.py`

**Interfaces:**
- Consumes: file Draw.io Desktop mới nhất.
- Produces: `diagram_blocks(data: bytes) -> list[bytes]` để so sánh raw page và bộ test mô tả cấu trúc mới.

- [ ] **Step 1: Chụp snapshot và SHA-256 nguồn**

Run:

```powershell
Copy-Item -LiteralPath 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio' `
  -Destination '.tmp\erd-module-collapse\WarePro_ERD_Tong_20260730.source.drawio'
Get-FileHash -Algorithm SHA256 '.tmp\erd-module-collapse\WarePro_ERD_Tong_20260730.source.drawio'
```

Expected: snapshot tồn tại và hash được ghi lại trước mọi biến đổi.

- [ ] **Step 2: Viết test cấu trúc mong muốn**

```python
EXPECTED_INTERNAL = {
    "catalog": {"Category", "Brand", "Unit", "Product", "ProductUnit",
                "Supplier", "Customer", "Warehouse"},
    "stock": {"StockIn", "StockInLine", "StockOut", "StockOutLine",
              "StockBalance", "StockLedger"},
    "control": {"StockTransfer", "StockTransferLine", "StockCountSession",
                "StockCountLine", "StockAdjustment", "StockAdjustmentLine",
                "ProductSerial"},
    "invoice": {"PurchaseInvoice", "PurchaseInvoiceLine",
                "SalesInvoice", "SalesInvoiceLine"},
    "warranty": {"WarrantyCoverage", "WarrantyClaim"},
    "user": {"AppUser", "AuditLog", "AuditArchiveManifest",
             "WareProClientSession"},
}

def test_collapsed_pages_keep_only_internal_table_cards(self):
    root = collapse_file(SOURCE)
    for page_key, expected in EXPECTED_INTERNAL.items():
        diagram = find_page(root, page_key)
        actual = {
            cell.get("data-entity")
            for cell in diagram.findall(".//mxCell[@data-card='core']")
        }
        assert actual == expected
        assert diagram.findall(".//mxCell[@data-external='1']") == []
```

- [ ] **Step 3: Viết test quyền sở hữu FK và hộp module**

```python
def test_external_module_boxes_only_represent_outgoing_dependencies(self):
    root = collapse_file(SOURCE)
    invoice = find_page(root, "invoice")
    assert module_keys(invoice) == {"catalog", "stock", "user"}
    assert "WarrantyCoverage" not in module_table_names(invoice)
    assert module_keys(find_page(root, "catalog")) == set()
    assert module_keys(find_page(root, "user")) == set()
```

- [ ] **Step 4: Viết test giữ nguyên tổng quan và nội dung hóa đơn**

```python
def test_overview_and_invoice_manual_text_are_preserved(self):
    result = collapse_file(SOURCE)
    result_bytes = serialize(result)
    assert diagram_blocks(result_bytes)[0] == diagram_blocks(SOURCE.read_bytes())[0]
    invoice_text = invoice_values(result)
    assert "RowVersion : rowversion" in invoice_text
    assert "TaxRate : decimal(9,4)" in invoice_text
    assert "PK/FK: AppDbContext" not in invoice_text
```

- [ ] **Step 5: Chạy test để xác nhận RED**

Run:

```powershell
python .tmp\erd-mvvm-revision\test_external_module_collapse.py -v
```

Expected: FAIL vì `collapse_file` chưa tồn tại.

---

### Task 2: Bộ biến đổi thẻ ngoài thành hộp phân hệ

**Files:**
- Create: `.tmp/erd-mvvm-revision/collapse_external_modules.py`
- Test: `.tmp/erd-mvvm-revision/test_external_module_collapse.py`

**Interfaces:**
- Consumes: `source_path: Path`.
- Produces: `collapse_file(source_path: Path) -> ET.ElementTree`.
- Produces: `write_candidate(source_path: Path, output_path: Path) -> None`.

- [ ] **Step 1: Khai báo mapping phân hệ và trang**

```python
ENTITY_MODULE = {
    entity: module_key
    for module_key, module in MODULES.items()
    for entity in module["entities"]
}
PAGE_KEYS = ("catalog", "stock", "control", "invoice", "warranty", "user")
```

- [ ] **Step 2: Chọn cạnh ngoài do bảng nội bộ sở hữu FK**

```python
def outgoing_external_edges(diagram, internal_entities):
    return [
        edge
        for edge in diagram.findall(".//mxCell[@edge='1']")
        if edge.get("data-dependent") in internal_entities
        and edge.get("data-principal") not in internal_entities
    ]
```

Mọi cạnh có `data-dependent` nằm ngoài phân hệ bị loại vì FK do phân hệ khác sở hữu.

- [ ] **Step 3: Gom dependency theo phân hệ ngoài và bảng nội bộ**

```python
def dependency_groups(edges):
    groups = {}
    for edge in edges:
        module_key = ENTITY_MODULE[edge.get("data-principal")]
        dependent = edge.get("data-dependent")
        item = groups.setdefault((module_key, dependent), set())
        item.add(edge.get("data-principal"))
    return groups
```

- [ ] **Step 4: Thay thẻ ngoài bằng hộp module**

```python
def module_label(module_key, table_names):
    tables = " · ".join(sorted(table_names))
    return (
        f"<b>PHÂN HỆ {MODULES[module_key]['name'].upper()}</b>"
        f"<br><font color='#64748B'>{tables}</font>"
    )
```

Hộp có `data-module-reference`, không có `data-entity`, `data-external`,
PK/FK hoặc constraint.

- [ ] **Step 5: Tạo cạnh dependency không dùng crow's-foot**

```python
DEPENDENCY_STYLE = (
    "edgeStyle=orthogonalEdgeStyle;orthogonalLoop=1;rounded=0;html=1;"
    "strokeColor=#64748B;strokeWidth=1.4;endArrow=open;endFill=0;"
)
```

Mỗi cặp `(phân hệ ngoài, bảng nội bộ)` có đúng một cạnh. Cardinality vật lý
giữa các bảng ngoài vẫn được lưu trong schema và ERD tổng quan; hộp module
không phải thực thể nên không dùng crow's-foot.

- [ ] **Step 6: Chạy test Task 1**

Run:

```powershell
python .tmp\erd-mvvm-revision\test_external_module_collapse.py -v
```

Expected: các test cấu trúc xanh; test layout có thể còn đỏ.

---

### Task 3: Bố cục cụm nội bộ và routing dependency

**Files:**
- Modify: `.tmp/erd-mvvm-revision/collapse_external_modules.py`
- Modify: `.tmp/erd-mvvm-revision/test_external_module_collapse.py`

**Interfaces:**
- Consumes: `diagram`, danh sách hộp module, hình học bảng nội bộ.
- Produces: `layout_page(diagram, page_key) -> None`.

- [ ] **Step 1: Viết test không chồng bảng và không xuyên bảng**

```python
def test_module_boxes_and_internal_cards_do_not_overlap():
    for diagram in collapsed_detail_pages():
        rectangles = visible_rectangles(diagram)
        assert pairwise_overlaps(rectangles) == []
        assert route_card_collisions(diagram) == []
```

- [ ] **Step 2: Viết test đường dependency vuông góc**

```python
def test_dependency_routes_are_orthogonal():
    for edge in dependency_edges():
        points = edge_route(edge)
        assert all(x1 == x2 or y1 == y2
                   for (x1, y1), (x2, y2) in zip(points, points[1:]))
```

- [ ] **Step 3: Bố trí bảng nội bộ**

- Danh mục: ba cụm `Sản phẩm`, `Đối tác`, `Kho hàng`.
- Kho và kiểm kê: giữ bố cục hàng cha → hàng dòng → bảng trạng thái/sê-ri.
- Hóa đơn: giữ nguyên hình học thủ công của bốn bảng nội bộ.
- Bảo hành: giữ `WarrantyCoverage` và `WarrantyClaim` ở trung tâm.
- Người dùng: giữ `AppUser` làm lõi; `WareProClientSession` độc lập.

- [ ] **Step 4: Bố trí hộp module ở vành trên/dưới**

```python
def pack_module_boxes(boxes, canvas_width, y):
    gap = 80
    total = sum(box.width for box in boxes) + gap * (len(boxes) - 1)
    x = (canvas_width - total) / 2
    return place_left_to_right(boxes, x=x, y=y, gap=gap)
```

- [ ] **Step 5: Route trunk và nhánh trong vùng trắng**

Mỗi hộp module dùng một hành lang riêng. Các cạnh cùng hộp dùng chung đoạn đầu
và tách nhánh trước khi đến bảng nội bộ; các waypoint không nằm trong rectangle
của bảng hoặc tiêu đề.

- [ ] **Step 6: Chạy test layout**

Run:

```powershell
python .tmp\erd-mvvm-revision\test_external_module_collapse.py -v
```

Expected: PASS.

---

### Task 4: Candidate, QA và thay file Desktop

**Files:**
- Create: `.tmp/erd-module-collapse/WarePro_ERD_Tong_20260730.candidate.drawio`
- Create: `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.before-module-collapse-20260731.drawio`
- Modify: `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio`

**Interfaces:**
- Consumes: snapshot Task 1 và transformer Task 2–3.
- Produces: candidate đã kiểm tra và file Desktop có backup.

- [ ] **Step 1: Sinh candidate**

Run:

```powershell
python .tmp\erd-mvvm-revision\collapse_external_modules.py `
  .tmp\erd-module-collapse\WarePro_ERD_Tong_20260730.source.drawio `
  .tmp\erd-module-collapse\WarePro_ERD_Tong_20260730.candidate.drawio
```

Expected: file Draw.io bảy trang được sinh thành công.

- [ ] **Step 2: Chạy toàn bộ test và verifier**

Run:

```powershell
python -m unittest discover -s .tmp\erd-mvvm-revision -p "test_*.py" -v
python .tmp\erd-mvvm-revision\verify_layout_normalization.py
```

Expected: toàn bộ test PASS; verifier không báo thiếu bảng, chồng bảng,
đường xuyên bảng, màu cấm hoặc encoding lỗi.

- [ ] **Step 3: Render sáu trang phân hệ**

Run Draw.io CLI với `--page-index 2,3,4,5,6,7`, xuất PNG vào `C:\tmp`.

Expected: sáu lệnh exit code `0`; kiểm tra trực quan xác nhận chữ đọc được,
hộp module gọn, không có bảng ngoài riêng lẻ và routing không che bảng.

- [ ] **Step 4: Kiểm tra file Desktop chưa thay đổi**

Run:

```powershell
Get-FileHash -Algorithm SHA256 `
  'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio'
```

Expected: hash vẫn bằng snapshot Task 1. Nếu lệch, dừng và chụp snapshot mới.

- [ ] **Step 5: Backup và thay file**

```powershell
Copy-Item -LiteralPath $target -Destination $backup
Copy-Item -LiteralPath $candidate -Destination $target -Force
```

- [ ] **Step 6: Xác minh cuối**

Expected:

- Backup hash bằng snapshot.
- File Desktop hash bằng candidate.
- Trang tổng quan byte-identical.
- Candidate mở và render được bằng Draw.io.

