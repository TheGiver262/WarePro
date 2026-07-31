# WarePro ERD Layout Normalization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Normalize the overview and five non-invoice module ERDs to match the compact, clearly routed invoice layout while preserving the invoice page byte-for-byte.

**Architecture:** Reuse the existing Draw.io XML generators, relationship manifests, Manhattan router, and verifier under `.tmp/erd-mvvm-revision`. Add content-derived card sizing, occupancy-aware orthogonal routing, compact module packing, and raw diagram-block merging that replaces pages 1–4 and 6–7 but retains page 5 from the latest source snapshot.

**Tech Stack:** Python 3 standard library, `xml.etree.ElementTree`, `html`, `re`, `unittest`, Draw.io desktop CLI, PowerShell.

## Global Constraints

- Work directly on `main`; do not create a worktree.
- Preserve unrelated dirty files and stage only plan/spec documents if a commit is needed.
- Modify exactly six pages: overview plus catalog, stock, control, warranty, and user modules.
- Preserve the raw `<diagram>` block at zero-based index `4` (`Hóa đơn`) byte-for-byte.
- Preserve page order, page names, table content, table pairs, relationship metadata, and crow's-foot cardinalities.
- Keep one unlabeled edge per table pair.
- Every route segment must be horizontal or vertical.
- No table overlap, edge-through-table collision, duplicate route, gateway, bundle, purple, or violet.
- Size cards from visible text; permit at most one row of unused vertical space beyond content and padding.
- Do not modify DOCX, PDF, or reference Draw.io files.
- Back up the latest Desktop file immediately before replacement.

---

### Task 1: Snapshot the latest file and lock the layout contract

**Files:**
- Read: `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio`
- Create: `.tmp/erd-layout-normalization/WarePro_ERD_Tong_20260730.source.drawio`
- Create: `.tmp/erd-mvvm-revision/test_layout_normalization.py`
- Modify: `.tmp/erd-mvvm-revision/test_detail_merge.py`

**Interfaces:**
- Consumes: the latest Desktop artifact and `detail_merge.diagram_blocks(content) -> list[bytes | str]`.
- Produces: a source snapshot, baseline SHA-256 values, and failing tests for card sizing, orthogonal routes, and invoice preservation.

- [ ] **Step 1: Copy and hash the latest Desktop file**

Run:

```powershell
New-Item -ItemType Directory -Force -Path '.tmp\erd-layout-normalization'
Copy-Item -LiteralPath 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio' -Destination '.tmp\erd-layout-normalization\WarePro_ERD_Tong_20260730.source.drawio' -Force
Get-FileHash -Algorithm SHA256 -LiteralPath 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio'
Get-FileHash -Algorithm SHA256 -LiteralPath '.tmp\erd-layout-normalization\WarePro_ERD_Tong_20260730.source.drawio'
```

Expected: both hashes equal `92EB9E31229C4FDCD3396722B2CE1D2C128E415C661374E8DD21DDD65ABD1964`, unless the user changed the file again; if changed, accept the new matching pair as the baseline.

- [ ] **Step 2: Write failing card-fit tests**

Create `test_layout_normalization.py` with concrete expectations:

```python
import sys
import unittest
from pathlib import Path

WORK_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(WORK_DIR))

from layout_metrics import card_size, visible_lines


class LayoutNormalizationTests(unittest.TestCase):
    def test_card_size_tracks_visible_text(self):
        short = "<b>Product</b><br><hr><br>[PK] Id : int"
        long = short + "<br>RowVersion : varbinary(max)"
        self.assertLess(card_size(short, compact=True)[1], card_size(long, compact=True)[1])
        self.assertGreaterEqual(card_size(long, compact=True)[0], 250)
        self.assertLessEqual(card_size(short, compact=True)[1] - len(visible_lines(short)) * 18, 44)

    def test_visible_lines_ignore_markup(self):
        self.assertEqual(
            visible_lines("<b>A</b><br><hr><br>B &amp; C"),
            ["A", "B & C"],
        )


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 3: Change the merge test to protect the invoice block**

In `test_detail_merge.py`, assert raw page 5 preservation and replacement of the other pages:

```python
source_blocks = diagram_blocks(SOURCE.read_bytes())
candidate_blocks = diagram_blocks(output.read_bytes())
self.assertEqual(candidate_blocks[4], source_blocks[4])
self.assertEqual(
    hashlib.sha256(candidate_blocks[4]).hexdigest(),
    hashlib.sha256(source_blocks[4]).hexdigest(),
)
self.assertTrue(all(
    candidate_blocks[index] != source_blocks[index]
    for index in (0, 1, 2, 3, 5, 6)
))
```

- [ ] **Step 4: Run focused tests and confirm failure**

Run:

```powershell
python -m unittest .tmp/erd-mvvm-revision/test_layout_normalization.py .tmp/erd-mvvm-revision/test_detail_merge.py -v
```

Expected: FAIL because `layout_metrics.py` and selective page merging do not exist yet.

### Task 2: Size cards tightly and pack non-invoice module pages

**Files:**
- Create: `.tmp/erd-mvvm-revision/layout_metrics.py`
- Modify: `.tmp/erd-mvvm-revision/detail_layout_v2.py`
- Modify: `.tmp/erd-mvvm-revision/detail_page_builder.py`
- Modify: `.tmp/erd-mvvm-revision/test_layout_normalization.py`
- Modify: `.tmp/erd-mvvm-revision/test_detail_pages.py`

**Interfaces:**
- Produces: `visible_lines(label: str) -> list[str]`, `card_size(label: str, compact: bool) -> tuple[int, int]`, and `pack_column(entities: list[str], sizes: dict[str, tuple[int, int]], x: int, start_y: int, gap_y: int = 90) -> tuple[dict[str, tuple[int, int, int, int]], int]`.
- Consumes: labels returned by `full_table_label` and `compact_table_label`.

- [ ] **Step 1: Implement markup-aware card measurement**

Create `layout_metrics.py`:

```python
import html
import math
import re

ROW_HEIGHT = 18
HORIZONTAL_PADDING = 24
VERTICAL_PADDING = 34


def visible_lines(label):
    text = re.sub(r"<hr\s*/?>", "", label, flags=re.I)
    text = re.sub(r"<br\s*/?>", "\n", text, flags=re.I)
    text = re.sub(r"<[^>]+>", "", text)
    return [line.strip() for line in html.unescape(text).splitlines() if line.strip()]


def card_size(label, compact=False):
    lines = visible_lines(label)
    longest = max((len(line) for line in lines), default=1)
    minimum = 250 if compact else 330
    maximum = 520 if compact else 680
    width = min(maximum, max(minimum, math.ceil(longest * 7.0 + HORIZONTAL_PADDING * 2)))
    height = len(lines) * ROW_HEIGHT + VERTICAL_PADDING
    return width, height
```

These constants intentionally follow the observed invoice density: 12 pt Times New Roman, 18-unit rows, and 34 units total vertical padding.

- [ ] **Step 2: Replace field-count sizing with label sizing**

In `detail_layout_v2.py`, calculate each label once and derive geometry from it:

```python
label = full_table_label(entity, fields, foreign_keys)
width, height = card_size(label, compact=False)
```

For external cards:

```python
label = compact_table_label(entity, fields, selected)
width, height = card_size(label, compact=True)
```

Return both positions and labels so `detail_page_builder.py` does not rebuild content with a different measurement input.

- [ ] **Step 3: Use compact invoice-style page arrangements**

Keep deterministic per-module column order, but calculate `x`/`y` from measured sizes and `CARD_GAP_X = 150`, `CARD_GAP_Y = 90`. Place core cards in the center. Place external cards on top, bottom, left, or right according to the shortest connection distance and distribute each side sequentially.

Use this exact packing rule:

```python
def pack_column(entities, sizes, x, start_y, gap_y=90):
    result = {}
    y = start_y
    for entity in entities:
        width, height = sizes[entity]
        result[entity] = (x, y, width, height)
        y += height + gap_y
    return result, y
```

Do not regenerate page `invoice`; it remains available only as the layout reference.

- [ ] **Step 4: Add geometry assertions**

For every generated non-invoice page, derive the label size and assert:

```python
required_width, required_height = card_size(
    card.get("value", ""),
    compact=card.get("data-card") == "external",
)
geometry = card.find("mxGeometry")
self.assertGreaterEqual(float(geometry.get("width")), required_width)
self.assertLessEqual(float(geometry.get("width")) - required_width, 20)
self.assertGreaterEqual(float(geometry.get("height")), required_height)
self.assertLessEqual(float(geometry.get("height")) - required_height, 18)
```

- [ ] **Step 5: Run layout and detail-page tests**

Run:

```powershell
python -m unittest .tmp/erd-mvvm-revision/test_layout_normalization.py .tmp/erd-mvvm-revision/test_detail_pages.py -v
```

Expected: PASS for card measurement and compact geometry; relationship counts remain `[33, 29, 26, 15, 7, 12]` in the generator even though page 5 will later be preserved from source.

### Task 3: Separate orthogonal routes and compact the overview

**Files:**
- Modify: `.tmp/erd-mvvm-revision/detail_layout_v2.py`
- Modify: `.tmp/erd-mvvm-revision/detail_page_builder.py`
- Modify: `.tmp/erd-mvvm-revision/overview_v2.py`
- Modify: `.tmp/erd-mvvm-revision/test_layout_normalization.py`
- Modify: `.tmp/erd-mvvm-revision/test_drawio_generation.py`

**Interfaces:**
- Produces: `route_edge(index: int, source_entity: str, target_entity: str, geometries: dict, external_sides: dict, incident: dict, content_bottom: float, used_segments: set[tuple]) -> tuple[list[tuple], float, float, float, float]` and `reserve_segments(path: list[tuple], used_segments: set[tuple]) -> None`.
- Consumes: measured table rectangles from Task 2 and existing relationship incident order.

- [ ] **Step 1: Add segment normalization and occupancy penalties**

Add to `detail_layout_v2.py`:

```python
def normalized_segment(start, end):
    return tuple(sorted((tuple(start), tuple(end))))


def reserve_segments(points, used_segments):
    for start, end in zip(points, points[1:]):
        used_segments.add(normalized_segment(start, end))
```

In `_grid_path`, add `+ 10000` when a candidate segment already exists in `used_segments`. Keep the existing bend penalty and obstacle rejection.

- [ ] **Step 2: Reserve each accepted route before routing the next edge**

In both `detail_page_builder.py` and `overview_v2.py`:

```python
used_segments = set()
for index, item in enumerate(selected):
    route = route_edge(
        index,
        item["principal"],
        item["dependent"],
        geometries,
        external_sides,
        incident,
        content_bottom,
        used_segments,
    )
    points = route[0]
    reserve_segments(points, used_segments)
    _add_edge(
        root,
        module_key,
        index,
        item,
        fields,
        cell_ids[item["principal"]],
        cell_ids[item["dependent"]],
        route,
    )
```

In `overview_v2.py`, call the same router with `principal`, `dependent`, `absolute_rectangles`, an empty external-side map, `incident`, `page_height - 80`, and the overview `used_segments`; reserve `route[0]` before `_add_edge`.

The router may share a short endpoint stub only when two edges leave the same port; otherwise it must choose a parallel lane.

- [ ] **Step 3: Compute overview card and module bounds from content**

Replace `_card_positions(entities)` fixed `250/355 × 88` geometry with measured labels. Pack cards into three columns for modules with more than four entities, otherwise two. Derive each module rectangle from the packed cards plus `35` units side padding, `55` units bottom padding, and a `48` unit swimlane header.

Keep the existing 2×3 module order:

```python
(
    ("catalog", "stock", "invoice"),
    ("user", "control", "warranty"),
)
```

Pack module rectangles using `MODULE_GAP_X = 170` and `MODULE_GAP_Y = 190`; compute page width and height from the resulting bounding box rather than fixed `3000 × 1780`.

- [ ] **Step 4: Add orthogonality and route-uniqueness tests**

Add helpers to `test_layout_normalization.py`:

```python
def route_points(edge, rectangles):
    style = {
        key: value
        for item in edge.get("style", "").split(";")
        if "=" in item
        for key, value in [item.split("=", 1)]
    }
    def port(cell_id, prefix):
        x, y, width, height = rectangles[cell_id]
        ratio_x = float(style.get(f"{prefix}X", "0.5"))
        ratio_y = float(style.get(f"{prefix}Y", "0.5"))
        return x + width * ratio_x, y + height * ratio_y
    waypoints = [
        (float(point.get("x")), float(point.get("y")))
        for point in edge.findall("./mxGeometry/Array[@as='points']/mxPoint")
    ]
    return [port(edge.get("source"), "exit"), *waypoints, port(edge.get("target"), "entry")]

for start, end in zip(points, points[1:]):
    self.assertTrue(start[0] == end[0] or start[1] == end[1])
```

Also assert that no two full waypoint tuples are identical and no segment intersects any non-endpoint table rectangle.

- [ ] **Step 5: Run layout, detail, and overview tests**

Run:

```powershell
python -m unittest .tmp/erd-mvvm-revision/test_layout_normalization.py .tmp/erd-mvvm-revision/test_detail_pages.py .tmp/erd-mvvm-revision/test_drawio_generation.py -v
```

Expected: PASS; overview retains 36 unique relationship pairs, all non-invoice generated cards fit content, and routes are orthogonal without table collisions or duplicate full paths.

### Task 4: Merge six generated pages while preserving invoice bytes

**Files:**
- Modify: `.tmp/erd-mvvm-revision/detail_merge.py`
- Modify: `.tmp/erd-mvvm-revision/generate_drawio.py`
- Modify: `.tmp/erd-mvvm-revision/detail_verifier.py`
- Modify: `.tmp/erd-mvvm-revision/test_detail_merge.py`
- Modify: `.tmp/erd-mvvm-revision/test_detail_verifier.py`
- Create: `.tmp/erd-layout-normalization/WarePro_ERD_Tong_20260730.generated.drawio`
- Create: `.tmp/erd-layout-normalization/WarePro_ERD_Tong_20260730.candidate.drawio`

**Interfaces:**
- Produces: `merge_diagram_blocks(source_bytes: bytes, generated_bytes: bytes, preserve_indexes: set[int]) -> bytes`.
- Consumes: a seven-page generated document and the source snapshot.

- [ ] **Step 1: Implement raw selective merging**

Add to `detail_merge.py`:

```python
def merge_diagram_blocks(source_bytes, generated_bytes, preserve_indexes):
    source_matches = list(BYTES_DIAGRAM_PATTERN.finditer(source_bytes))
    generated_blocks = diagram_blocks(generated_bytes)
    if len(source_matches) != 7 or len(generated_blocks) != 7:
        raise ValueError("Expected exactly 7 diagrams")
    result = []
    cursor = 0
    for index, match in enumerate(source_matches):
        result.append(source_bytes[cursor:match.start()])
        result.append(
            match.group(0)
            if index in preserve_indexes
            else generated_blocks[index]
        )
        cursor = match.end()
    result.append(source_bytes[cursor:])
    merged = b"".join(result)
    ET.fromstring(merged)
    return merged
```

- [ ] **Step 2: Generate then merge**

Update `generate_drawio.py` to:

```python
source = Path(".tmp/erd-layout-normalization/WarePro_ERD_Tong_20260730.source.drawio")
generated = Path(".tmp/erd-layout-normalization/WarePro_ERD_Tong_20260730.generated.drawio")
candidate = Path(".tmp/erd-layout-normalization/WarePro_ERD_Tong_20260730.candidate.drawio")
write_drawio(generated, Path("QuanLyHangHoa/Data/AppDbContext.cs"), Path("QuanLyHangHoa/Models"))
candidate.write_bytes(merge_diagram_blocks(source.read_bytes(), generated.read_bytes(), {4}))
```

- [ ] **Step 3: Extend verifier fields**

Return and assert:

```python
report["invoice_bytes_unchanged"] = candidate_blocks[4] == baseline_blocks[4]
report["invoice_raw_sha256"] = hashlib.sha256(candidate_blocks[4]).hexdigest()
report["table_overlaps"] = table_overlaps
report["non_orthogonal_segments"] = non_orthogonal_segments
report["card_fit_issues"] = card_fit_issues
```

Skip card-fit enforcement for diagram index `4`; its byte preservation is authoritative. Continue checking all seven pages for relationship pairs, colors, encoding, bundles, gateways, and edge labels.

- [ ] **Step 4: Run the full unit suite**

Run:

```powershell
python -m unittest discover -s '.tmp\erd-mvvm-revision' -p 'test_*.py' -v
```

Expected: all tests PASS; `invoice_bytes_unchanged=True`; issue lists empty.

- [ ] **Step 5: Generate and structurally verify the candidate**

Run:

```powershell
python .tmp/erd-mvvm-revision/generate_drawio.py
python -c "import json,sys; from pathlib import Path; sys.path.insert(0,'.tmp/erd-mvvm-revision'); from detail_verifier import verify_file; print(json.dumps(verify_file(Path('.tmp/erd-layout-normalization/WarePro_ERD_Tong_20260730.candidate.drawio'), Path('.tmp/erd-layout-normalization/WarePro_ERD_Tong_20260730.source.drawio')), ensure_ascii=True))"
```

Expected: seven pages; invoice unchanged; overview 36 edges; detail counts remain `[33,29,26,15,7,12]`; all collision, overlap, orthogonality, fit, relationship, color, encoding, label, bundle, and gateway issue lists empty.

### Task 5: Render, inspect, back up, and replace the Desktop file

**Files:**
- Read: `.tmp/erd-layout-normalization/WarePro_ERD_Tong_20260730.candidate.drawio`
- Create: `.tmp/erd-layout-normalization/rendered/page-1.png`, `page-2.png`, `page-3.png`, `page-4.png`, `page-6.png`, `page-7.png`
- Create: `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.before-layout-normalization-20260731.drawio`
- Modify: `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio`

**Interfaces:**
- Consumes: structurally verified candidate from Task 4.
- Produces: visually approved six-page layout update and a recoverable backup of the exact pre-replacement file.

- [ ] **Step 1: Render only the six modified pages**

Use Draw.io CLI with one-based `--page-index` values `1,2,3,4,6,7`.

Expected: six non-empty PNG files. Do not export page 5 as evidence of a changed page; its raw SHA-256 check is the preservation proof.

- [ ] **Step 2: Inspect each rendered page**

For every image confirm:

- all text is visible and cards have no unnecessary empty body;
- core cards form a readable central flow;
- external cards stay around the perimeter;
- every connection is distinguishable from neighboring connections;
- every segment is horizontal or vertical;
- no route crosses a card or module title;
- no large unused canvas region remains.

If any page fails, adjust only its coordinates or routing hints, regenerate, rerun the full verifier, and re-render that page.

- [ ] **Step 3: Guard against a newer Desktop edit**

Run:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio'
Get-FileHash -Algorithm SHA256 -LiteralPath '.tmp\erd-layout-normalization\WarePro_ERD_Tong_20260730.source.drawio'
```

Expected: hashes equal. If different, stop; take a new snapshot and rebuild against it rather than overwriting the user's newer work.

- [ ] **Step 4: Back up and replace**

Run:

```powershell
Copy-Item -LiteralPath 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio' -Destination 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.before-layout-normalization-20260731.drawio'
Copy-Item -LiteralPath '.tmp\erd-layout-normalization\WarePro_ERD_Tong_20260730.candidate.drawio' -Destination 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio' -Force
```

Expected: backup hash equals source snapshot; final Desktop hash equals candidate.

- [ ] **Step 5: Verify the final Desktop artifact**

Run the full unit suite and `verify_file` directly against the Desktop file. Re-render one modified page from the final Desktop copy and compare its hash with the accepted candidate render.

Expected: all tests PASS; structural report unchanged; final render hash matches; invoice raw block hash remains equal to source.

## Final Verification

Run:

```powershell
python -m unittest discover -s '.tmp\erd-mvvm-revision' -p 'test_*.py' -v
git status --short
```

Expected: all tests PASS. Repository status contains only the user's unrelated pre-existing files and committed design/plan documents; no generated Draw.io or PNG artifact is staged.
