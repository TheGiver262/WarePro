# Legacy Module ERD In-place Patch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bổ sung và sửa đúng quan hệ của sáu trang ERD phân hệ cũ mà không dựng lại các trang hoặc thay đổi 17 sơ đồ còn lại.

**Architecture:** Một script Python chuẩn đọc file Draw.io tổng, trích sáu khối `PL.D.*`, đối chiếu manifest với quan hệ live từ `AppDbContext`, rồi chỉnh trực tiếp cell/edge trong từng `mxGraphModel`. Script tạo candidate và backup trước; verifier kiểm tra quan hệ, hình học và hash từng trang không thuộc PL.D trước khi file nguồn được ghi đè.

**Tech Stack:** Python 3, `xml.etree.ElementTree`, `unittest`, Draw.io XML không nén.

## Global Constraints

- Làm trực tiếp trên `main`; không tạo branch, worktree hoặc subagent.
- File đích duy nhất: `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\Diagram\Drawio\DO_AN_TAT_CA_SO_DO_2026-07-27.drawio`.
- Giữ nguyên từng byte của 17 khối `<diagram>` không bắt đầu bằng `PL.D.`.
- Không thay toàn bộ `mxGraphModel` của bất kỳ trang PL.D nào.
- Giữ nguyên bảng và tọa độ hiện hữu; chỉ dịch đồng loạt khi cần tạo hành lang ngoài, không thay bố cục tương đối.
- Không xuất PNG/PDF và không sửa DOCX hoặc sáu file Draw.io riêng lẻ.
- Quan hệ lấy từ `QuanLyHangHoa/Data/AppDbContext.cs`; cột vật lý lấy từ `.tmp/current-schema.json`.
- Không thêm dependency.

---

### Task 1: Khóa manifest và baseline bất biến

**Files:**
- Create: `.tmp/legacy-erd-patch/test_patch_legacy_module_erd.py`
- Create: `.tmp/legacy-erd-patch/patch_legacy_module_erd.py`

**Interfaces:**
- Consumes: `detail_correction.load_schema(Path, Path)` và `revision_tools.MODULES`.
- Produces: `required_relationships() -> dict[str, list[tuple[str, str, tuple[str, ...]]]]`, `diagram_blocks(bytes) -> list[bytes]`.

- [ ] **Step 1: Viết test thất bại cho manifest đầy đủ**

```python
def test_required_relationship_counts(self):
    actual = patch.required_relationships()
    self.assertEqual(
        {key: len(value) for key, value in actual.items()},
        {
            "catalog": 34,
            "stock": 33,
            "control": 34,
            "invoice": 15,
            "warranty": 9,
            "user": 23,
        },
    )
```

`required_relationships()` chọn mọi quan hệ có `principal` hoặc `dependent` thuộc `MODULES[module]["entities"]`, sau đó loại hai quan hệ ngữ cảnh `StockIn → StockInLine` và `StockOut → StockOutLine` khỏi trang `control` vì không đầu nào thuộc phân hệ.

- [ ] **Step 2: Viết test thất bại cho 17 trang bất biến**

```python
def test_non_module_pages_remain_byte_identical(self):
    source = SOURCE.read_bytes()
    candidate = patch.patch_bytes(source)
    before = patch.diagram_blocks(source)
    after = patch.diagram_blocks(candidate)
    self.assertEqual(len(before), 23)
    for old, new in zip(before[:17], after[:17], strict=True):
        self.assertEqual(old, new)
```

- [ ] **Step 3: Chạy test để xác nhận RED**

Run:

```powershell
rtk python -X utf8 -m unittest .tmp/legacy-erd-patch/test_patch_legacy_module_erd.py -v
```

Expected: FAIL vì `patch_legacy_module_erd.py` chưa có các hàm trên.

- [ ] **Step 4: Cài parser và manifest tối thiểu**

```python
DIAGRAM_PATTERN = re.compile(rb"<diagram\b[^>]*>.*?</diagram>", re.S)


def diagram_blocks(content):
    blocks = DIAGRAM_PATTERN.findall(content)
    if len(blocks) != 23:
        raise ValueError(f"Expected 23 diagrams, found {len(blocks)}")
    return blocks


def relationship_key(item):
    return (
        item["principal"],
        item["dependent"],
        tuple(item["foreign_keys"]),
    )


def required_relationships():
    _, relationships = load_schema(APP_DB_CONTEXT, MODELS_DIR)
    result = {}
    for key, module in MODULES.items():
        entities = set(module["entities"])
        result[key] = sorted({
            relationship_key(item)
            for item in relationships
            if item["principal"] in entities
            or item["dependent"] in entities
        })
    return result
```

- [ ] **Step 5: Chạy test manifest**

Run: `rtk python -X utf8 -m unittest .tmp/legacy-erd-patch/test_patch_legacy_module_erd.py -v`

Expected: test manifest PASS; test `patch_bytes` vẫn FAIL.

---

### Task 2: Vá trực tiếp sáu trang PL.D

**Files:**
- Modify: `.tmp/legacy-erd-patch/patch_legacy_module_erd.py`
- Modify: `.tmp/legacy-erd-patch/test_patch_legacy_module_erd.py`

**Interfaces:**
- Consumes: `required_relationships()`, XML cell hiện hữu và `.tmp/current-schema.json`.
- Produces: `patch_diagram(diagram, module_key) -> None`, `patch_bytes(source: bytes) -> bytes`.

- [ ] **Step 1: Viết test thất bại cho việc giữ vertex hiện hữu**

```python
def test_existing_vertices_keep_geometry(self):
    source = SOURCE.read_bytes()
    before = patch.vertex_geometries(source)
    after = patch.vertex_geometries(patch.patch_bytes(source))
    for page, cells in before.items():
        for cell_id, geometry in cells.items():
            self.assertEqual(after[page][cell_id], geometry)
```

- [ ] **Step 2: Viết test thất bại cho coverage và quan hệ giả**

```python
def test_all_required_relationships_are_represented(self):
    candidate = patch.patch_bytes(SOURCE.read_bytes())
    represented = patch.represented_relationships(candidate)
    self.assertEqual(represented, patch.required_relationships())
    for relationships in represented.values():
        self.assertNotIn(
            ("Product", "Supplier", ("ProductId",)),
            relationships,
        )
```

Verifier đọc metadata `data-principal`, `data-dependent`, `data-foreign-keys`; mỗi connector chỉ được biểu diễn đúng một quan hệ.

- [ ] **Step 3: Cài vá cell/edge tại chỗ**

`patch_diagram()` phải thực hiện đúng thứ tự:

1. Lập chỉ mục bảng lõi, khối tham chiếu và các dòng thực thể trong khối.
2. Giữ toàn bộ vertex/geometry cũ.
3. Gán metadata và nhãn PK→FK cho edge đúng đã có.
4. Nếu một edge nhóm cũ đang đại diện nhiều quan hệ thật, giữ edge đó cho quan hệ đầu và nhân bản tối thiểu cho từng quan hệ còn lại; mỗi bản có cổng/lane riêng.
5. Nếu thiếu đầu tham chiếu, thêm đúng một khối theo mẫu style hiện hữu; mỗi bảng tham chiếu là một dòng riêng.
6. Chỉ thêm edge mới khi không thể mở rộng edge hiện hữu; dùng `orthogonalEdgeStyle`, cổng khác nhau và waypoint ngoài hình chữ nhật bảng.
7. Thêm `WareProClientSession` vào vùng trống của PL.D.6 với nhãn `Độc lập – không có FK trong AppDbContext`, không thêm edge.

Nhãn connector dùng:

```python
def relation_label(item):
    principal, dependent, foreign_keys = item
    principal_key = (
        "Id + ProductSerialId"
        if len(foreign_keys) == 2
        else "Id"
    )
    return (
        f"{principal}.{principal_key} → "
        f"{dependent}.{' + '.join(foreign_keys)}"
    )
```

FK kép bảo hành phải hiển thị:

```text
WarrantyCoverage.(Id + ProductSerialId) → WarrantyClaim.(WarrantyCoverageId + ProductSerialId)
```

- [ ] **Step 4: Cài ghép byte bảo toàn trang ngoài phạm vi**

```python
def patch_bytes(source):
    blocks = diagram_blocks(source)
    replacements = {}
    for index, block in enumerate(blocks):
        if b'name="PL.D.' not in block:
            continue
        diagram = ET.fromstring(block)
        module_key = PAGE_NAME_TO_MODULE[diagram.get("name")]
        patch_diagram(diagram, module_key)
        replacements[index] = ET.tostring(diagram, encoding="utf-8")
    return replace_blocks_by_span(source, blocks, replacements)
```

`replace_blocks_by_span()` ghép theo vị trí regex; không gọi `ET.write()` cho toàn file.

- [ ] **Step 5: Chạy test đến GREEN**

Run: `rtk python -X utf8 -m unittest .tmp/legacy-erd-patch/test_patch_legacy_module_erd.py -v`

Expected: tất cả test PASS; `23` trang, coverage `34,33,34,15,9,23`, 17 trang đầu giữ nguyên byte.

---

### Task 3: Candidate, kiểm chứng hình học và ghi file đích

**Files:**
- Create: `.tmp/legacy-erd-patch/verify_legacy_module_erd.py`
- Create: `.tmp/legacy-erd-patch/backup/DO_AN_TAT_CA_SO_DO_2026-07-27.before.drawio`
- Create: `.tmp/legacy-erd-patch/DO_AN_TAT_CA_SO_DO_2026-07-27.candidate.drawio`
- Modify in place: `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\Diagram\Drawio\DO_AN_TAT_CA_SO_DO_2026-07-27.drawio`

**Interfaces:**
- Consumes: `patch_bytes()`, source Draw.io.
- Produces: báo cáo cấu trúc/hình học và file đích có backup.

- [ ] **Step 1: Tạo backup và candidate**

```python
source_bytes = SOURCE.read_bytes()
BACKUP.parent.mkdir(parents=True, exist_ok=True)
shutil.copy2(SOURCE, BACKUP)
CANDIDATE.write_bytes(patch_bytes(source_bytes))
```

In SHA-256 của source, backup và candidate; source phải bằng backup.

- [ ] **Step 2: Viết verifier hình học**

Verifier phải thất bại nếu:

- XML không parse được hoặc không đủ 23 trang.
- Một trong 17 trang ngoài PL.D đổi byte.
- Coverage không phải `34,33,34,15,9,23`.
- Có quan hệ ngoài manifest hoặc `SourceDocumentId`.
- Edge thiếu endpoint hay `mxGeometry relative="1"`.
- Waypoint hoặc đoạn vuông góc đi xuyên hình chữ nhật của vertex không phải endpoint.
- Hai edge mới dùng trùng toàn bộ waypoint.

Kết quả đạt:

```text
PAGES=23
NON_MODULE_PAGES_BYTE_IDENTICAL=17
RELATIONSHIP_COUNTS=34,33,34,15,9,23
FALSE_RELATIONSHIPS=0
GEOMETRY_ISSUES=0
STRUCTURAL_QA=PASS
```

- [ ] **Step 3: Chạy test và verifier trên candidate**

```powershell
rtk python -X utf8 -m unittest .tmp/legacy-erd-patch/test_patch_legacy_module_erd.py -v
rtk python -X utf8 .tmp/legacy-erd-patch/patch_legacy_module_erd.py --candidate
rtk python -X utf8 .tmp/legacy-erd-patch/verify_legacy_module_erd.py --candidate
```

Expected: tests PASS và `STRUCTURAL_QA=PASS`.

- [ ] **Step 4: Kiểm tra trực quan sáu trang trong Draw.io**

Mở candidate bằng Draw.io Desktop. Với từng trang PL.D.1–PL.D.6, xác nhận bảng cũ không đổi, chữ đọc được, đường không xuyên bảng/đè chữ, và nhãn quan hệ đúng chiều PK→FK. Nếu lỗi, chỉ chỉnh waypoint/cổng hoặc vị trí khối mới rồi chạy lại Step 3.

- [ ] **Step 5: Ghi đè và kiểm chứng lại file đích**

Sau QA trực quan, sao chép candidate vào `SOURCE`, rồi chạy:

```powershell
rtk python -X utf8 .tmp/legacy-erd-patch/verify_legacy_module_erd.py --final
rtk git status --short
```

Expected: `STRUCTURAL_QA=PASS`; trạng thái Git chỉ còn thay đổi có sẵn của người dùng. Không có PNG/DOCX hoặc file Draw.io riêng lẻ nào đổi.
