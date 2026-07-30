# Detailed ERD Correction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sửa chính xác sáu trang ERD chi tiết trong tệp DrawIO hiện tại, giữ nguyên tuyệt đối trang ERD tổng quan và không tạo PNG hoặc sửa DOCX.

**Architecture:** Tái sử dụng bộ sinh XML trong `.tmp/erd-mvvm-revision/revision_tools`, nhưng thay cơ chế chọn quan hệ theo allowlist bằng manifest quan hệ tường minh cho từng trang. Mỗi trang chi tiết dùng bảng nội bộ đầy đủ, bảng ngoài phân hệ thu gọn có PK/FK liên quan và connector ER trực tiếp. Một hàm ghép theo chuỗi thay đúng sáu khối `<diagram>` để khối XML trang 1 được giữ nguyên từng byte.

**Tech Stack:** Python 3 chuẩn, `unittest`, `xml.etree.ElementTree`, Draw.io Desktop 31.0.2, Windows UI Automation.

## Global Constraints

- Thực hiện trực tiếp trên `main`; không tạo branch hoặc worktree.
- Chỉ sửa `C:\Users\player\Desktop\DATN\final\WarePro_ERD_Tong_20260730.drawio`.
- Chỉ thay sáu trang từ trang 2 đến trang 7.
- XML nguyên bản của trang 1 phải giữ nguyên từng byte; baseline canonical SHA-256 hiện tại là `7891bfcd462e3bda2531c2361a6eb2841a65f9ccf9f1df7e6b5189ba1423a3c8`.
- Không xuất PNG và không sửa bất kỳ DOCX nào.
- `QuanLyHangHoa/Data/AppDbContext.cs` là nguồn chuẩn của quan hệ; `QuanLyHangHoa/Models/*.cs` là nguồn thuộc tính và kiểu dữ liệu.
- Không vẽ `StockLedger.SourceDocumentId` như FK.
- Không tạo quan hệ trực tiếp giả giữa `Product` với `Supplier`, `Customer` hoặc `Warehouse`.
- Connector phải vuông góc, không xuyên bảng, không dùng chung đoạn đường và hạn chế tối đa giao cắt.
- Không sửa hoặc stage `QuanLyHangHoa/Views/LoginView.xaml`, `06_Bao_cao/branch-archive-20260730/` hoặc `crossrefs-test.docx`.
- Không thêm dependency mới.

## File Map

- Modify: `.tmp/erd-mvvm-revision/revision_tools/__init__.py` — manifest trang, nhãn bảng, connector, bố cục và ghép sáu trang.
- Modify: `.tmp/erd-mvvm-revision/test_revision_tools.py` — kiểm tra trích quan hệ và tính tùy chọn của FK.
- Modify: `.tmp/erd-mvvm-revision/test_drawio_generation.py` — kiểm tra sáu trang, số quan hệ, bảng ngoài và nhãn PK/FK.
- Create: `.tmp/erd-mvvm-revision/apply_detail_correction.py` — tạo backup, sinh candidate và thay sáu trang.
- Create: `.tmp/erd-mvvm-revision/verify_detail_correction.py` — kiểm tra cấu trúc, quan hệ, hình học và hash trang 1.
- Create: `.tmp/erd-detail-correction/backup/WarePro_ERD_Tong_20260730.before.drawio` — bản sao khôi phục.
- Create: `.tmp/erd-detail-correction/WarePro_ERD_Tong_20260730.candidate.drawio` — bản ứng viên trước khi ghi đè.
- Modify in place: `C:\Users\player\Desktop\DATN\final\WarePro_ERD_Tong_20260730.drawio` — đầu ra duy nhất.

---

### Task 1: Khóa manifest quan hệ và kiểm thử hồi quy

**Files:**
- Modify: `.tmp/erd-mvvm-revision/revision_tools/__init__.py`
- Modify: `.tmp/erd-mvvm-revision/test_revision_tools.py`
- Modify: `.tmp/erd-mvvm-revision/test_drawio_generation.py`

**Interfaces:**
- Consumes: `extract_relationships(Path)`, `_parse_models(Path)`.
- Produces: `DETAIL_PAGE_RELATIONSHIPS`, `DETAIL_PAGE_EDGE_COUNTS`, `is_optional_fk(fields, dependent, foreign_keys)`.

- [ ] **Step 1: Viết kiểm thử thất bại cho các quan hệ bắt buộc**

Mở rộng `test_revision_tools.py` bằng tập phụ thuộc trực tiếp của `Product`:

```python
product_dependents = {
    "ProductUnit",
    "ProductSerial",
    "PurchaseInvoiceLine",
    "SalesInvoiceLine",
    "StockAdjustmentLine",
    "StockBalance",
    "StockCountLine",
    "StockInLine",
    "StockLedger",
    "StockOutLine",
    "StockTransferLine",
}
actual_product_dependents = {
    item["dependent"]
    for item in relationships
    if item["principal"] == "Product"
    and item["foreign_keys"] == ["ProductId"]
}
self.assertEqual(actual_product_dependents, product_dependents)
self.assertNotIn(
    ("StockLedger", "SourceDocument", ("SourceDocumentId",)),
    actual,
)
```

Thêm kiểm tra FK tổng hợp:

```python
self.assertIn(
    (
        "WarrantyClaim",
        "WarrantyCoverage",
        ("WarrantyCoverageId", "ProductSerialId"),
    ),
    actual,
)
```

- [ ] **Step 2: Chạy kiểm thử để ghi nhận baseline**

Run:

```powershell
rtk proxy "C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" -X utf8 -m unittest .tmp/erd-mvvm-revision/test_revision_tools.py -v
```

Expected: kiểm thử trích quan hệ hiện có PASS; kiểm thử mới về manifest trang chưa tồn tại sẽ FAIL sau khi được thêm ở bước kế tiếp.

- [ ] **Step 3: Khai báo manifest quan hệ tường minh**

Trong `revision_tools/__init__.py`, thay `DETAIL_EXTERNAL_ALLOWLIST` và `USER_REFERENCE_GROUPS` bằng:

```python
DETAIL_PAGE_EDGE_COUNTS = {
    "catalog": 29,
    "stock": 31,
    "control": 32,
    "invoice": 15,
    "warranty": 9,
    "user": 23,
}

DETAIL_PAGE_RELATIONSHIPS = {
    "catalog": [
        ("Category", "Product", ("CategoryId",)),
        ("Brand", "Product", ("BrandId",)),
        ("Unit", "Product", ("DefaultUnitId",)),
        ("Product", "ProductUnit", ("ProductId",)),
        ("Unit", "ProductUnit", ("UnitId",)),
        ("Product", "ProductSerial", ("ProductId",)),
        ("Product", "PurchaseInvoiceLine", ("ProductId",)),
        ("Product", "SalesInvoiceLine", ("ProductId",)),
        ("Product", "StockAdjustmentLine", ("ProductId",)),
        ("Product", "StockBalance", ("ProductId",)),
        ("Product", "StockCountLine", ("ProductId",)),
        ("Product", "StockInLine", ("ProductId",)),
        ("Product", "StockLedger", ("ProductId",)),
        ("Product", "StockOutLine", ("ProductId",)),
        ("Product", "StockTransferLine", ("ProductId",)),
        ("Supplier", "StockIn", ("SupplierId",)),
        ("Supplier", "PurchaseInvoice", ("SupplierId",)),
        ("Customer", "StockOut", ("CustomerId",)),
        ("Customer", "SalesInvoice", ("CustomerId",)),
        ("Customer", "WarrantyCoverage", ("CustomerId",)),
        ("Warehouse", "ProductSerial", ("CurrentWarehouseId",)),
        ("Warehouse", "StockIn", ("WarehouseId",)),
        ("Warehouse", "StockOut", ("WarehouseId",)),
        ("Warehouse", "StockAdjustment", ("WarehouseId",)),
        ("Warehouse", "StockCountSession", ("WarehouseId",)),
        ("Warehouse", "StockBalance", ("WarehouseId",)),
        ("Warehouse", "StockLedger", ("WarehouseId",)),
        ("Warehouse", "StockTransfer", ("FromWarehouseId",)),
        ("Warehouse", "StockTransfer", ("ToWarehouseId",)),
    ],
    "stock": [
        ("StockIn", "StockInLine", ("StockInId",)),
        ("StockOut", "StockOutLine", ("StockOutId",)),
        ("Supplier", "StockIn", ("SupplierId",)),
        ("Customer", "StockOut", ("CustomerId",)),
        ("Warehouse", "StockIn", ("WarehouseId",)),
        ("Warehouse", "StockOut", ("WarehouseId",)),
        ("Warehouse", "StockBalance", ("WarehouseId",)),
        ("Warehouse", "StockLedger", ("WarehouseId",)),
        ("Product", "StockInLine", ("ProductId",)),
        ("Product", "StockOutLine", ("ProductId",)),
        ("Product", "StockBalance", ("ProductId",)),
        ("Product", "StockLedger", ("ProductId",)),
        ("Unit", "StockInLine", ("UnitId",)),
        ("Unit", "StockOutLine", ("UnitId",)),
        ("AppUser", "StockIn", ("CreatedBy",)),
        ("AppUser", "StockIn", ("ApprovedBy",)),
        ("AppUser", "StockIn", ("PostedBy",)),
        ("AppUser", "StockOut", ("CreatedBy",)),
        ("AppUser", "StockOut", ("ApprovedBy",)),
        ("AppUser", "StockOut", ("PostedBy",)),
        ("AppUser", "StockLedger", ("PostedBy",)),
        ("StockCountSession", "StockIn", ("StockCountSessionId",)),
        ("StockCountSession", "StockOut", ("StockCountSessionId",)),
        ("StockCountLine", "StockIn", ("StockCountLineId",)),
        ("StockCountLine", "StockOut", ("StockCountLineId",)),
        ("ProductSerial", "StockLedger", ("ProductSerialId",)),
        ("StockIn", "PurchaseInvoice", ("StockInId",)),
        ("StockInLine", "PurchaseInvoiceLine", ("StockInLineId",)),
        ("StockOut", "SalesInvoice", ("StockOutId",)),
        ("StockOutLine", "SalesInvoiceLine", ("StockOutLineId",)),
        ("StockOut", "WarrantyClaim", ("ReplacementStockOutId",)),
    ],
    "control": [
        ("StockTransfer", "StockTransferLine", ("StockTransferId",)),
        ("StockCountSession", "StockCountLine", ("SessionId",)),
        ("StockAdjustment", "StockAdjustmentLine", ("AdjustmentId",)),
        ("StockTransferLine", "ProductSerial", ("StockTransferLineId",)),
        ("ProductSerial", "StockAdjustmentLine", ("ProductSerialId",)),
        ("Warehouse", "StockTransfer", ("FromWarehouseId",)),
        ("Warehouse", "StockTransfer", ("ToWarehouseId",)),
        ("Warehouse", "StockCountSession", ("WarehouseId",)),
        ("Warehouse", "StockAdjustment", ("WarehouseId",)),
        ("Warehouse", "ProductSerial", ("CurrentWarehouseId",)),
        ("Product", "StockTransferLine", ("ProductId",)),
        ("Product", "StockCountLine", ("ProductId",)),
        ("Product", "StockAdjustmentLine", ("ProductId",)),
        ("Product", "ProductSerial", ("ProductId",)),
        ("Unit", "StockTransferLine", ("UnitId",)),
        ("AppUser", "StockTransfer", ("CreatedBy",)),
        ("AppUser", "StockTransfer", ("ApprovedBy",)),
        ("AppUser", "StockTransfer", ("PostedBy",)),
        ("AppUser", "StockCountSession", ("CreatedBy",)),
        ("AppUser", "StockCountSession", ("ApprovedBy",)),
        ("AppUser", "StockCountSession", ("PostedBy",)),
        ("AppUser", "StockAdjustment", ("CreatedBy",)),
        ("AppUser", "StockAdjustment", ("ApprovedBy",)),
        ("AppUser", "StockAdjustment", ("PostedBy",)),
        ("StockIn", "StockInLine", ("StockInId",)),
        ("StockInLine", "ProductSerial", ("LastStockInLineId",)),
        ("StockOut", "StockOutLine", ("StockOutId",)),
        ("StockOutLine", "ProductSerial", ("LastStockOutLineId",)),
        ("ProductSerial", "StockLedger", ("ProductSerialId",)),
        ("ProductSerial", "WarrantyCoverage", ("ProductSerialId",)),
        ("ProductSerial", "WarrantyClaim", ("ProductSerialId",)),
        ("ProductSerial", "WarrantyClaim", ("ReplacementSerialId",)),
    ],
    "invoice": [
        ("PurchaseInvoice", "PurchaseInvoiceLine", ("PurchaseInvoiceId",)),
        ("SalesInvoice", "SalesInvoiceLine", ("SalesInvoiceId",)),
        ("Supplier", "PurchaseInvoice", ("SupplierId",)),
        ("Customer", "SalesInvoice", ("CustomerId",)),
        ("Product", "PurchaseInvoiceLine", ("ProductId",)),
        ("Product", "SalesInvoiceLine", ("ProductId",)),
        ("Unit", "PurchaseInvoiceLine", ("UnitId",)),
        ("Unit", "SalesInvoiceLine", ("UnitId",)),
        ("AppUser", "PurchaseInvoice", ("CreatedBy",)),
        ("AppUser", "SalesInvoice", ("CreatedBy",)),
        ("StockIn", "PurchaseInvoice", ("StockInId",)),
        ("StockInLine", "PurchaseInvoiceLine", ("StockInLineId",)),
        ("StockOut", "SalesInvoice", ("StockOutId",)),
        ("StockOutLine", "SalesInvoiceLine", ("StockOutLineId",)),
        ("SalesInvoice", "WarrantyCoverage", ("SalesInvoiceId",)),
    ],
    "warranty": [
        (
            "WarrantyCoverage",
            "WarrantyClaim",
            ("WarrantyCoverageId", "ProductSerialId"),
        ),
        ("ProductSerial", "WarrantyCoverage", ("ProductSerialId",)),
        ("ProductSerial", "WarrantyClaim", ("ProductSerialId",)),
        ("ProductSerial", "WarrantyClaim", ("ReplacementSerialId",)),
        ("Customer", "WarrantyCoverage", ("CustomerId",)),
        ("SalesInvoice", "WarrantyCoverage", ("SalesInvoiceId",)),
        ("StockOut", "WarrantyClaim", ("ReplacementStockOutId",)),
        ("AppUser", "WarrantyClaim", ("ProcessedBy",)),
        ("AppUser", "WarrantyClaim", ("ApprovedBy",)),
    ],
    "user": [
        ("AppUser", "AppUser", ("CreatedBy",)),
        ("AppUser", "AuditLog", ("PerformedBy",)),
        ("AppUser", "AuditArchiveManifest", ("ActorId",)),
        ("AppUser", "StockIn", ("CreatedBy",)),
        ("AppUser", "StockIn", ("ApprovedBy",)),
        ("AppUser", "StockIn", ("PostedBy",)),
        ("AppUser", "StockOut", ("CreatedBy",)),
        ("AppUser", "StockOut", ("ApprovedBy",)),
        ("AppUser", "StockOut", ("PostedBy",)),
        ("AppUser", "StockTransfer", ("CreatedBy",)),
        ("AppUser", "StockTransfer", ("ApprovedBy",)),
        ("AppUser", "StockTransfer", ("PostedBy",)),
        ("AppUser", "StockCountSession", ("CreatedBy",)),
        ("AppUser", "StockCountSession", ("ApprovedBy",)),
        ("AppUser", "StockCountSession", ("PostedBy",)),
        ("AppUser", "StockAdjustment", ("CreatedBy",)),
        ("AppUser", "StockAdjustment", ("ApprovedBy",)),
        ("AppUser", "StockAdjustment", ("PostedBy",)),
        ("AppUser", "PurchaseInvoice", ("CreatedBy",)),
        ("AppUser", "SalesInvoice", ("CreatedBy",)),
        ("AppUser", "StockLedger", ("PostedBy",)),
        ("AppUser", "WarrantyClaim", ("ProcessedBy",)),
        ("AppUser", "WarrantyClaim", ("ApprovedBy",)),
    ],
}
```

Danh sách này là manifest đóng; bộ sinh phải thất bại nếu một tuple không tồn tại trong quan hệ trích từ `AppDbContext.cs`.

- [ ] **Step 4: Thêm kiểm thử số cạnh và chống quan hệ giả**

Trong `test_drawio_generation.py`, thêm:

```python
expected_counts = {
    "page_catalog": 29,
    "page_stock": 31,
    "page_control": 32,
    "page_invoice": 15,
    "page_warranty": 9,
    "page_user": 23,
}
for diagram_id, expected in expected_counts.items():
    diagram = next(
        item for item in diagrams if item.get("id") == diagram_id
    )
    self.assertEqual(
        len(diagram.findall(".//mxCell[@edge='1']")),
        expected,
        diagram_id,
    )

catalog = next(item for item in diagrams if item.get("id") == "page_catalog")
catalog_pairs = {
    (edge.get("data-principal"), edge.get("data-dependent"))
    for edge in catalog.findall(".//mxCell[@edge='1']")
}
self.assertNotIn(("Product", "Supplier"), catalog_pairs)
self.assertNotIn(("Product", "Customer"), catalog_pairs)
self.assertNotIn(("Product", "Warehouse"), catalog_pairs)
```

- [ ] **Step 5: Chạy kiểm thử và xác nhận trạng thái RED**

Run:

```powershell
rtk proxy "C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" -X utf8 -m unittest discover -s .tmp/erd-mvvm-revision -p "test_*.py" -v
```

Expected: FAIL vì bộ sinh cũ chưa tạo đủ số cạnh và còn gộp quan hệ người dùng.

---

### Task 2: Sinh bảng chi tiết, bảng tham chiếu và connector chính xác

**Files:**
- Modify: `.tmp/erd-mvvm-revision/revision_tools/__init__.py`
- Modify: `.tmp/erd-mvvm-revision/test_drawio_generation.py`

**Interfaces:**
- Consumes: `DETAIL_PAGE_RELATIONSHIPS`, trường model và quan hệ từ `AppDbContext`.
- Produces: `_select_detail_relationships`, `_compact_field_label`, `_relationship_label`, `_build_detail`.

- [ ] **Step 1: Chọn đúng quan hệ theo manifest**

Thêm hàm:

```python
def _relationship_key(item):
    return (
        item["principal"],
        item["dependent"],
        tuple(item["foreign_keys"]),
    )


def _select_detail_relationships(module_key, relationships):
    actual = {_relationship_key(item): item for item in relationships}
    required = DETAIL_PAGE_RELATIONSHIPS[module_key]
    missing = [key for key in required if key not in actual]
    if missing:
        raise ValueError(f"{module_key}: missing relationships: {missing}")
    return [actual[key] for key in required]
```

Không thêm bất kỳ cạnh nào ngoài danh sách đã duyệt.

- [ ] **Step 2: Hiển thị bảng ngoài phân hệ với PK/FK liên quan**

Thêm:

```python
def _relevant_external_fields(entity, selected):
    names = {"Id"}
    for item in selected:
        if item["dependent"] == entity:
            names.update(item["foreign_keys"])
    return names


def _compact_field_label(entity, fields, selected):
    relevant = _relevant_external_fields(entity, selected)
    rows = [f"<b>{entity}</b>", "<hr>"]
    for type_name, name in fields.get(entity, []):
        if name not in relevant:
            continue
        tag = "PK" if name == "Id" else "FK"
        rows.append(f"[{tag}] {name} : {type_name}")
    return "<br>".join(rows)
```

`WareProClientSession` vẫn là bảng nội bộ đầy đủ và thêm một note riêng:

```text
Độc lập – không có FK trong AppDbContext
```

- [ ] **Step 3: Thêm optionality và nhãn PK→FK**

Suy ra optionality từ kiểu trường model:

```python
def is_optional_fk(fields, dependent, foreign_keys):
    types = dict((name, type_name) for type_name, name in fields[dependent])
    return any(types[name].endswith("?") for name in foreign_keys)


def _relationship_label(item, fields):
    principal_key = (
        "(Id, ProductSerialId)"
        if len(item["foreign_keys"]) == 2
        else "Id"
    )
    dependent_key = (
        "(" + ", ".join(item["foreign_keys"]) + ")"
        if len(item["foreign_keys"]) > 1
        else item["foreign_keys"][0]
    )
    left = "0..1" if is_optional_fk(
        fields, item["dependent"], item["foreign_keys"]
    ) else "1"
    return (
        f"{left} ↔ 0..N<br>"
        f"{item['principal']}.{principal_key} → "
        f"{item['dependent']}.{dependent_key}"
    )
```

Connector nguồn là bảng cha, đích là bảng con. Dùng `startArrow=ERone` hoặc `ERzeroToOne`, `endArrow=ERzeroToMany`, `edgeStyle=entityRelationEdgeStyle`, `html=1`.

- [ ] **Step 4: Bố trí bảng và đường nối theo hành lang**

Thay `_detail_positions` bằng cấu hình từng trang:

```python
DETAIL_LAYOUTS = {
    "catalog": {
        "primary_columns": [["Category", "Brand", "Unit"], ["Product", "ProductUnit"], ["Supplier", "Customer", "Warehouse"]],
        "external_sides": ("left", "right", "bottom"),
    },
    "stock": {
        "primary_columns": [["StockIn", "StockInLine"], ["StockBalance", "StockLedger"], ["StockOut", "StockOutLine"]],
        "external_sides": ("left", "right", "top", "bottom"),
    },
    "control": {
        "primary_columns": [["StockTransfer", "StockTransferLine"], ["StockCountSession", "StockCountLine", "ProductSerial"], ["StockAdjustment", "StockAdjustmentLine"]],
        "external_sides": ("left", "right", "top", "bottom"),
    },
    "invoice": {
        "primary_columns": [["PurchaseInvoice", "PurchaseInvoiceLine"], ["SalesInvoice", "SalesInvoiceLine"]],
        "external_sides": ("left", "right", "top", "bottom"),
    },
    "warranty": {
        "primary_columns": [["WarrantyCoverage"], ["WarrantyClaim"]],
        "external_sides": ("left", "right"),
    },
    "user": {
        "primary_columns": [["AuditLog", "AuditArchiveManifest"], ["AppUser", "WareProClientSession"]],
        "external_sides": ("left", "right", "top", "bottom"),
    },
}
```

Quy tắc hình học:

- Canvas mặc định `3600 × 2400`, được tăng chiều cao nếu bảng nội bộ dài.
- Vùng bảng nội bộ bắt đầu tại `x=720`; cột cách nhau tối thiểu `560 px`.
- Bảng ngoài rộng `380 px`, cao theo số dòng PK/FK; đặt ngoài vùng nội bộ.
- Mỗi cạnh liên phân hệ nhận một lane riêng cách cạnh trước tối thiểu `18 px`.
- Mỗi cạnh có ít nhất hai waypoint vuông góc khi nguồn và đích không cùng hàng/cột.
- Hai cạnh không được dùng cùng toàn bộ dãy waypoint.
- Tất cả waypoint phải nằm ngoài hình chữ nhật của mọi bảng.

- [ ] **Step 5: Kiểm thử nhãn và hộp ngoài**

Thêm:

```python
for diagram in diagrams[1:]:
    for cell in diagram.findall(".//mxCell[@data-external='1']"):
        self.assertIn("[PK] Id", cell.get("value", ""))

catalog = next(item for item in diagrams if item.get("id") == "page_catalog")
product_edges = [
    edge
    for edge in catalog.findall(".//mxCell[@edge='1']")
    if edge.get("data-principal") == "Product"
    and "ProductId" in edge.get("data-foreign-keys", "")
]
self.assertEqual(len(product_edges), 11)

warranty = next(item for item in diagrams if item.get("id") == "page_warranty")
composite = [
    edge
    for edge in warranty.findall(".//mxCell[@edge='1']")
    if edge.get("data-foreign-keys") == "WarrantyCoverageId,ProductSerialId"
]
self.assertEqual(len(composite), 1)
self.assertIn("(Id, ProductSerialId)", composite[0].get("value"))
```

- [ ] **Step 6: Chạy kiểm thử đến trạng thái GREEN**

Run:

```powershell
rtk proxy "C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" -X utf8 -m unittest discover -s .tmp/erd-mvvm-revision -p "test_*.py" -v
```

Expected: tất cả kiểm thử PASS; số cạnh lần lượt là `29, 31, 32, 15, 9, 23`.

---

### Task 3: Ghép sáu trang mà không chạm XML trang tổng quan

**Files:**
- Modify: `.tmp/erd-mvvm-revision/revision_tools/__init__.py`
- Create: `.tmp/erd-mvvm-revision/apply_detail_correction.py`
- Create: `.tmp/erd-mvvm-revision/verify_detail_correction.py`
- Modify: `.tmp/erd-mvvm-revision/test_drawio_generation.py`

**Interfaces:**
- Consumes: DrawIO nguồn và sáu `<diagram>` mới.
- Produces: `diagram_blocks`, `replace_detail_pages`, candidate và báo cáo kiểm chứng.

- [ ] **Step 1: Viết kiểm thử bảo toàn byte trang 1**

Thêm:

```python
source_text = source_path.read_text(encoding="utf-8")
source_page_1 = diagram_blocks(source_text)[0]
replace_detail_pages(
    source_path,
    candidate_path,
    Path("QuanLyHangHoa/Data/AppDbContext.cs"),
    Path("QuanLyHangHoa/Models"),
)
candidate_text = candidate_path.read_text(encoding="utf-8")
self.assertEqual(diagram_blocks(candidate_text)[0], source_page_1)
```

Expected: FAIL vì `diagram_blocks` và `replace_detail_pages` chưa tồn tại.

- [ ] **Step 2: Cài ghép theo chuỗi**

```python
DIAGRAM_PATTERN = re.compile(
    r"<diagram\b[^>]*>.*?</diagram>",
    re.S,
)


def diagram_blocks(text):
    blocks = DIAGRAM_PATTERN.findall(text)
    if len(blocks) != 7:
        raise ValueError(f"Expected 7 diagrams, found {len(blocks)}")
    return blocks


def replace_detail_pages(source_path, output_path, app_db_context, models_dir):
    source_text = source_path.read_text(encoding="utf-8")
    old_blocks = diagram_blocks(source_text)
    generated = build_drawio_document(app_db_context, models_dir)
    new_blocks = [
        ET.tostring(item, encoding="unicode")
        for item in generated.findall("diagram")[1:]
    ]
    candidate = source_text
    for old, new in zip(old_blocks[1:], new_blocks, strict=True):
        candidate = candidate.replace(old, new, 1)
    if diagram_blocks(candidate)[0] != old_blocks[0]:
        raise AssertionError("Overview XML changed")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(candidate, encoding="utf-8", newline="")
```

- [ ] **Step 3: Viết script áp dụng có backup**

`apply_detail_correction.py` phải:

1. Đọc `C:\Users\player\Desktop\DATN\final\WarePro_ERD_Tong_20260730.drawio`.
2. Tạo `.tmp/erd-detail-correction/backup/WarePro_ERD_Tong_20260730.before.drawio` bằng `shutil.copy2`.
3. Gọi `replace_detail_pages` để tạo candidate.
4. Chưa ghi đè file Desktop ở bước này.
5. In `SOURCE_SHA256`, `BACKUP_SHA256`, `PAGE1_RAW_SHA256`, `CANDIDATE`.

- [ ] **Step 4: Viết trình kiểm chứng cấu trúc và hình học**

`verify_detail_correction.py` phải thất bại nếu:

- Không có đúng 7 trang hoặc tên trang thay đổi.
- Khối `<diagram>` đầu tiên khác byte so với backup.
- Số cạnh sáu trang không phải `29, 31, 32, 15, 9, 23`.
- Thiếu bất kỳ tuple trong `DETAIL_PAGE_RELATIONSHIPS`.
- Có `Product → Supplier`, `Product → Customer`, `Product → Warehouse`.
- Có cạnh mang `SourceDocumentId`.
- Có hộp ngoài thiếu `[PK] Id`.
- Có edge thiếu `mxGeometry relative="1"`.
- Có waypoint nằm trong hình chữ nhật của bảng không phải nguồn/đích.
- Hai cạnh có cùng dãy waypoint.

Kết quả thành công:

```text
PAGES=7
PAGE1_BYTES_UNCHANGED=1
EDGE_COUNTS=29,31,32,15,9,23
FALSE_EDGES=0
WAYPOINT_COLLISIONS=0
STRUCTURAL_QA=PASS
```

- [ ] **Step 5: Chạy test và tạo candidate**

Run:

```powershell
rtk proxy "C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" -X utf8 -m unittest discover -s .tmp/erd-mvvm-revision -p "test_*.py" -v
rtk proxy "C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" -X utf8 .tmp/erd-mvvm-revision/apply_detail_correction.py
rtk proxy "C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" -X utf8 .tmp/erd-mvvm-revision/verify_detail_correction.py --candidate
```

Expected: tests PASS và `STRUCTURAL_QA=PASS`.

---

### Task 4: QA trực quan và ghi đè đầu ra duy nhất

**Files:**
- Read: `.tmp/erd-detail-correction/WarePro_ERD_Tong_20260730.candidate.drawio`
- Modify in place: `C:\Users\player\Desktop\DATN\final\WarePro_ERD_Tong_20260730.drawio`

**Interfaces:**
- Consumes: candidate đã đạt kiểm tra cấu trúc.
- Produces: file DrawIO cuối đã kiểm tra trực quan và có backup.

- [ ] **Step 1: Mở candidate bằng Draw.io Desktop**

Mở đúng một cửa sổ Draw.io với candidate. Không xuất PNG/PDF. Dùng ảnh chụp cửa sổ trực tiếp để rà từng trang 2–7.

- [ ] **Step 2: Rà sáu trang**

Với từng trang, xác nhận:

- Bảng nội bộ hiển thị đủ thuộc tính, kiểu, PK/FK.
- Bảng ngoài hiển thị tên, `[PK] Id` và FK liên quan.
- Connector gắn đúng bảng cha–con và nhãn đúng `PK → FK`.
- Không connector xuyên bảng, chồng khít hoặc đè chữ.
- Các nhánh từ `Product`, `Warehouse` và `AppUser` tách thành lane riêng.
- Trang bảo hành hiển thị FK tổng hợp và hai vai trò của `ProductSerial`.
- `WareProClientSession` có note độc lập và không có cạnh giả.

Nếu một trang lỗi, chỉ chỉnh `DETAIL_LAYOUTS` hoặc lane allocator, chạy lại toàn bộ tests và mở lại đúng trang đó.

- [ ] **Step 3: Ghi đè file Desktop sau khi QA đạt**

Sao chép candidate vào đúng:

```text
C:\Users\player\Desktop\DATN\final\WarePro_ERD_Tong_20260730.drawio
```

Không tạo DrawIO kết quả thứ hai trong `final`.

- [ ] **Step 4: Chạy kiểm chứng sau ghi đè**

Run:

```powershell
rtk proxy "C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" -X utf8 .tmp/erd-mvvm-revision/verify_detail_correction.py --final
```

Expected:

```text
PAGES=7
PAGE1_BYTES_UNCHANGED=1
EDGE_COUNTS=29,31,32,15,9,23
FALSE_EDGES=0
WAYPOINT_COLLISIONS=0
STRUCTURAL_QA=PASS
```

- [ ] **Step 5: Xác nhận phạm vi cuối**

Run:

```powershell
rtk git status --short
```

Expected: chỉ còn các thay đổi có sẵn của người dùng; `.tmp` không được stage. Xác nhận không có PNG mới và không có DOCX nào đổi thời gian/hash.
