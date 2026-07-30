# WarePro Seven-Page ERD Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign all seven pages of `WarePro_ERD_Tong_20260730.drawio` to match or exceed the supplied PlantUML references while preserving the real EF Core schema and producing one verified multi-page Draw.io file.

**Architecture:** Reuse the existing schema parser, Draw.io XML builders, and geometry verifier under `.tmp/erd-mvvm-revision`; extend them instead of introducing a second diagram framework. Build a candidate inside `.tmp/erd-redesign`, verify it structurally against `AppDbContext.cs` and visually through temporary renders, and only then back up and replace the Desktop file.

**Tech Stack:** Python 3 standard library (`xml.etree.ElementTree`, `re`, `unittest`, `pathlib`, `hashlib`), uncompressed Draw.io XML, diagrams.net desktop CLI, EF Core model configuration in C#.

## Global Constraints

- Source of truth: `QuanLyHangHoa/Data/AppDbContext.cs` plus `QuanLyHangHoa/Models`.
- Target: `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio`.
- Reference style: `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\Diagram\plantuml-png-erd-module`.
- Keep exactly seven pages and preserve their approved Vietnamese names.
- Redesign all seven pages; do not modify DOCX, PDF, migrations, database schema, application code, or reference PNGs.
- Use a white background, light-gray table cards, thin module-colored borders, Times New Roman, SQL types, `[PK]`, `[FK]`, `[UQ]`, `[AK]`, nullable markers, and Crow's Foot cardinality.
- Never use purple or violet.
- One detailed-page edge equals one real FK or one real composite FK from EF Core.
- Do not create `SourceDocumentId` relations or direct `Product` relations to `Supplier`, `Customer`, or `Warehouse`.
- Show `WarrantyCoverage.(Id, ProductSerialId)` to `WarrantyClaim.(WarrantyCoverageId, ProductSerialId)` as one composite relationship.
- Keep `WareProClientSession` independent because `AppDbContext` declares no FK for it.
- Use orthogonal routing, separate role-FK ports, no edge through a table, and minimize duplicate route segments.
- Overview intermodule links are bundled; detailed-page links terminate directly at nearby external reference cards.
- Work directly on `main`; do not create a branch or worktree.
- Preserve unrelated dirty files and stage only the plan during planning; during implementation, helper/candidate files remain ignored under `.tmp` and the Desktop artifact is outside the repository.

---

### Task 1: Freeze the input artifact and schema baseline

**Files:**
- Read: `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio`
- Read: `QuanLyHangHoa/Data/AppDbContext.cs`
- Read: `QuanLyHangHoa/Models/*.cs`
- Create: `.tmp/erd-redesign/WarePro_ERD_Tong_20260730.source.drawio`
- Create: `.tmp/erd-redesign/source.sha256`

**Interfaces:**
- Consumes: the user-selected Desktop Draw.io file and the current EF Core model.
- Produces: an immutable workspace copy and SHA-256 value used by all later tasks.

- [ ] **Step 1: Verify the exact source, reference folder, and repository state**

Run:

```powershell
Test-Path 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio'
Get-ChildItem 'F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\Diagram\plantuml-png-erd-module' -Filter '*.png' | Select-Object -ExpandProperty Name
git status --short
```

Expected: the target is `True`; five reference PNGs are listed; unrelated dirty files remain visible and untouched.

- [ ] **Step 2: Refresh the workspace source copy and its hash**

Run through `apply_patch` only for any script edits, then use read/copy commands for the artifact:

```powershell
New-Item -ItemType Directory -Force '.tmp\erd-redesign' | Out-Null
Copy-Item -LiteralPath 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio' -Destination '.tmp\erd-redesign\WarePro_ERD_Tong_20260730.source.drawio' -Force
(Get-FileHash '.tmp\erd-redesign\WarePro_ERD_Tong_20260730.source.drawio' -Algorithm SHA256).Hash | Set-Content '.tmp\erd-redesign\source.sha256'
```

Expected: the copied file exists and `source.sha256` contains one 64-character hexadecimal digest.

- [ ] **Step 3: Run the existing schema-parser tests before changing builders**

Run:

```powershell
python -m unittest .tmp/erd-mvvm-revision/test_revision_tools.py .tmp/erd-mvvm-revision/test_relationship_parser_lambdas.py .tmp/erd-mvvm-revision/test_detail_schema.py -v
```

Expected: all parser and schema tests pass against the current `AppDbContext.cs` and models.

- [ ] **Step 4: Commit nothing**

This task creates ignored QA inputs only. Confirm `git status --short` contains no new tracked task file.

### Task 2: Lock the seven-page semantic contract with failing tests

**Files:**
- Modify: `.tmp/erd-mvvm-revision/test_drawio_generation.py`
- Modify: `.tmp/erd-mvvm-revision/test_detail_pages.py`
- Modify: `.tmp/erd-mvvm-revision/test_detail_verifier.py`

**Interfaces:**
- Consumes: `build_drawio_document(Path, Path) -> Element`, `build_detail_diagrams(Path, Path) -> list[Element]`, and `verify_file(Path, Path) -> dict`.
- Produces: executable acceptance checks for page names, table-card metadata, real FK coverage, composite keys, bundling, forbidden relations/colors, and geometry.

- [ ] **Step 1: Add overview acceptance assertions**

Extend `test_builds_seven_pages_with_overview_modules_and_gateway_edges` with exact checks:

```python
self.assertEqual(len(overview.findall(".//mxCell[@data-module]")), 6)
self.assertEqual(len(overview.findall(".//mxCell[@data-entity]")), 30)
self.assertGreater(len(overview.findall(".//mxCell[@data-bundle='1']")), 0)
self.assertFalse(overview.findall(".//mxCell[@data-fake='1']"))
for cell in overview.findall(".//mxCell[@data-entity]"):
    self.assertIn("fontFamily=Times New Roman", cell.get("style", ""))
```

- [ ] **Step 2: Add detailed-card and relationship assertions**

Add checks that every core card has all three zones and every edge carries schema metadata:

```python
for diagram in diagrams:
    core = diagram.findall(".//mxCell[@data-card='core']")
    self.assertGreater(len(core), 0)
    for card in core:
        self.assertEqual(card.get("data-zones"), "header,columns,constraints")
        self.assertIn("fontFamily=Times New Roman", card.get("style", ""))
    for edge in diagram.findall(".//mxCell[@edge='1']"):
        self.assertIn("data-principal", edge.attrib)
        self.assertIn("data-dependent", edge.attrib)
        self.assertIn("data-foreign-keys", edge.attrib)
        self.assertIn("orthogonalEdgeStyle", edge.get("style", ""))
```

Retain the exact negative and special-case checks already present:

```python
self.assertNotIn(("Product", "Supplier"), catalog_pairs)
self.assertNotIn(("Product", "Customer"), catalog_pairs)
self.assertNotIn(("Product", "Warehouse"), catalog_pairs)
self.assertEqual(len(composite), 1)
self.assertEqual(composite[0].get("data-foreign-keys"), "WarrantyCoverageId,ProductSerialId")
```

- [ ] **Step 3: Add verifier assertions for all seven redesigned pages**

Replace the obsolete `page1_bytes_unchanged` expectation with:

```python
self.assertEqual(report["pages"], 7)
self.assertEqual(report["page_names"], EXPECTED_PAGE_NAMES)
self.assertEqual(report["false_edges"], [])
self.assertEqual(report["missing_edges"], [])
self.assertEqual(report["waypoint_collisions"], [])
self.assertEqual(report["duplicate_routes"], [])
self.assertEqual(report["forbidden_colors"], [])
self.assertEqual(report["encoding_errors"], [])
self.assertTrue(report["overview_uses_bundles"])
```

- [ ] **Step 4: Run the focused tests and confirm the new contract fails**

Run:

```powershell
python -m unittest .tmp/erd-mvvm-revision/test_drawio_generation.py .tmp/erd-mvvm-revision/test_detail_pages.py .tmp/erd-mvvm-revision/test_detail_verifier.py -v
```

Expected: FAIL because the current builders do not yet emit `data-card`, `data-zones`, complete overview bundles, or the expanded verifier report.

- [ ] **Step 5: Commit nothing**

These tests live under ignored `.tmp`; keep them as the runnable QA harness and do not stage them.

### Task 3: Upgrade the shared schema and card renderer

**Files:**
- Modify: `.tmp/erd-mvvm-revision/detail_correction.py`
- Modify: `.tmp/erd-mvvm-revision/revision_tools/__init__.py`
- Modify: `.tmp/erd-mvvm-revision/detail_page_builder.py`

**Interfaces:**
- Consumes: parsed model fields, indexes, alternate keys, and normalized EF Core relationships.
- Produces: table cards with `data-entity`, `data-card`, `data-zones`, SQL field labels, and stable edge-port metadata used by overview and detail builders.

- [ ] **Step 1: Extend schema metadata without adding a new parser**

Make the existing `load_schema` path return this stable shape for each entity:

```python
{
    "name": "Product",
    "fields": [{"name": "Id", "sql_type": "int", "nullable": False}],
    "primary_key": ["Id"],
    "alternate_keys": [],
    "unique_indexes": [["ProductCode"]],
}
```

Derive SQL labels from the existing model/property parsing and EF configuration. Do not infer relationships from navigation properties when `AppDbContext` has no `HasForeignKey`.

- [ ] **Step 2: Centralize the approved visual constants**

Define and reuse exactly these non-purple constants in `revision_tools/__init__.py`:

```python
FONT = "Times New Roman"
PAGE_BACKGROUND = "#FFFFFF"
CARD_FILL = "#F7F8FA"
CARD_HEADER_FILL = "#ECEFF3"
NOTE_FILL = "#FFF8D6"
TEXT = "#1F2937"
MODULE_COLORS = {
    "catalog": "#2563EB",
    "stock": "#0F766E",
    "control": "#B45309",
    "invoice": "#C2410C",
    "warranty": "#B91C1C",
    "user": "#374151",
}
```

- [ ] **Step 3: Emit a reusable three-zone table card**

Implement one shared helper and call it from both builders:

```python
def add_table_card(root, cell_id, entity, x, y, width, module_key, *, compact=False, external=False):
    """Return the parent mxCell id for a table card with header, columns, and constraints zones."""
```

The parent gets `data-card="external"` or `data-card="core"`, `data-entity`, and `data-zones="header,columns,constraints"`; child cells contain the name, field list, and only important PK/UQ/AK/index constraints. External cards use dashed borders and only fields participating in visible relationships.

- [ ] **Step 4: Give each role FK a deterministic port**

Replace edge-index-only ports with a stable mapping based on `(principal, dependent, foreign_keys)` so `CreatedBy`, `ApprovedBy`, and `PostedBy` leave and enter on separate slots. Preserve Crow's Foot markers and `data-principal`, `data-dependent`, `data-foreign-keys` attributes.

- [ ] **Step 5: Run parser and card tests**

Run:

```powershell
python -m unittest .tmp/erd-mvvm-revision/test_revision_tools.py .tmp/erd-mvvm-revision/test_relationship_parser_lambdas.py .tmp/erd-mvvm-revision/test_detail_schema.py .tmp/erd-mvvm-revision/test_drawio_generation.py -v
```

Expected: parser/schema tests pass; overview tests may still fail only on layout/bundling not yet implemented.

### Task 4: Redesign the overview page with six enlarged modules and bundled intermodule links

**Files:**
- Modify: `.tmp/erd-mvvm-revision/revision_tools/__init__.py`
- Test: `.tmp/erd-mvvm-revision/test_drawio_generation.py`

**Interfaces:**
- Consumes: shared table-card helper and the normalized relationship list.
- Produces: `diagram[id='overview'][name='ERD tổng quan']` with six 3-by-2 module containers, internal direct edges, and intermodule bundle trunks.

- [ ] **Step 1: Implement the fixed 3-by-2 module grid**

Use a landscape page and fixed module order:

```python
OVERVIEW_GRID = [
    ("catalog", 80, 100), ("stock", 1120, 100), ("control", 2160, 100),
    ("invoice", 80, 1080), ("warranty", 1120, 1080), ("user", 2160, 1080),
]
```

Size each container to keep all compact cards and internal routes inside its bounds; expand the page instead of shrinking text below the approved readable size.

- [ ] **Step 2: Preserve direct internal relationships**

For each module, draw only relationships whose principal and dependent both belong to that module. Route them inside the container and retain schema metadata on every edge.

- [ ] **Step 3: Bundle every module-to-module relationship family**

Group cross-module relations by `(source_module, target_module)`. Add one invisible gateway on each module boundary, one trunk edge marked `data-bundle="1"`, then short branch edges to every participating table. Branches retain their true FK metadata; trunks contain module-pair metadata only and are excluded from FK counts.

- [ ] **Step 4: Run the overview test**

Run:

```powershell
python -m unittest .tmp/erd-mvvm-revision/test_drawio_generation.py -v
```

Expected: PASS with six module containers, 30 table cards, real internal FK edges, and at least one bundled intermodule trunk.

### Task 5: Redesign the six detailed module pages

**Files:**
- Modify: `.tmp/erd-mvvm-revision/detail_page_builder.py`
- Test: `.tmp/erd-mvvm-revision/test_detail_pages.py`

**Interfaces:**
- Consumes: shared cards, exact normalized relationships, and module membership.
- Produces: six detailed diagrams in the existing page-name order.

- [ ] **Step 1: Implement the approved catalog layout**

Place `Product` centrally; place `Category`, `Brand`, `Unit`, and `ProductUnit` around it; place `Supplier`, `Customer`, and `Warehouse` as independent core cards. Keep real external dependents close to the specific referenced core table. Do not add direct edges from `Product` to the three independent tables.

- [ ] **Step 2: Implement the left-to-right inventory flow**

Place `StockIn -> StockInLine -> StockBalance/StockLedger -> StockOutLine -> StockOut`; put `Product`, `Warehouse`, and `AppUser` in the top reference row. Put count, invoice, serial, and warranty reference cards beside the exact table that owns each FK.

- [ ] **Step 3: Implement transfer/count/adjustment clusters**

Create three horizontal clusters for transfer, count, and adjustment; place `ProductSerial` at bottom center and `Warehouse`, `Product`, `Unit`, `AppUser` at top. Route `FromWarehouseId` and `ToWarehouseId` separately, as well as creator/approver/poster roles.

- [ ] **Step 4: Implement mirrored invoice branches**

Build the purchase branch from `Supplier`, `StockIn`, `PurchaseInvoice`, `PurchaseInvoiceLine`; build the sales branch from `Customer`, `StockOut`, `SalesInvoice`, `SalesInvoiceLine`; keep `Product` and `Unit` between the branches. Label optional one-to-one stock-document relations concisely.

- [ ] **Step 5: Implement the warranty hub and composite FK**

Center `WarrantyCoverage` and `WarrantyClaim`; surround them with `ProductSerial`, `Customer`, `SalesInvoice`, `StockOut`, and `AppUser`. Draw the coverage/claim composite FK as one emphasized edge and route faulty serial, replacement serial, replacement stock-out, processor, and approver through separate ports.

- [ ] **Step 6: Implement the user/audit hub**

Center `AppUser`; place `AuditLog` and `AuditArchiveManifest` left; place compact operational reference cards right; draw the `CreatedBy` self-loop locally. Place `WareProClientSession` separately with the note `Độc lập – không có FK trong AppDbContext` and no edge.

- [ ] **Step 7: Run all detailed-page tests**

Run:

```powershell
python -m unittest .tmp/erd-mvvm-revision/test_detail_pages.py -v
```

Expected: PASS with all six diagram ids, exact real-FK counts from the current parser, no fake catalog links, the composite warranty edge, and the independent client-session note.

### Task 6: Expand structural and geometry verification

**Files:**
- Modify: `.tmp/erd-mvvm-revision/detail_verifier.py`
- Modify: `.tmp/erd-mvvm-revision/test_detail_verifier.py`

**Interfaces:**
- Consumes: candidate Draw.io path, source path, and current schema relationship set.
- Produces: a dictionary containing page, FK, style, encoding, collision, and bundle audit results.

- [ ] **Step 1: Compare candidate edge metadata to the EF relationship set**

Return both directions of the comparison:

```python
report["false_edges"] = sorted(candidate_fk_edges - schema_fk_edges)
report["missing_edges"] = sorted(required_page_edges - candidate_fk_edges)
```

Exclude overview bundle trunks from FK comparison but include every branch carrying `data-foreign-keys`.

- [ ] **Step 2: Verify visual rules structurally**

Scan all cell styles and values. Report `forbidden_colors` for purple/violet hex values or named colors, `encoding_errors` for replacement characters/mojibake markers, missing Times New Roman styles, absent dashed borders on external cards, and absent three-zone metadata on core cards.

- [ ] **Step 3: Verify routing geometry on all seven pages**

Use only top-level owned table-card rectangles, a `0.2` pixel tolerance, explicit source/target ports, and waypoint segments. Report an issue only when an edge segment enters a non-endpoint table rectangle. Detect identical segment sequences as duplicate routes, allowing only explicitly shared overview trunks.

- [ ] **Step 4: Run the verifier test**

Run:

```powershell
python -m unittest .tmp/erd-mvvm-revision/test_detail_verifier.py -v
```

Expected: PASS and every problem list is empty.

### Task 7: Build and render the seven-page candidate

**Files:**
- Modify: `.tmp/erd-mvvm-revision/generate_drawio.py`
- Create: `.tmp/erd-redesign/WarePro_ERD_Tong_20260730.candidate.drawio`
- Create: `.tmp/erd-redesign/rendered/page-1.png` through `page-7.png`

**Interfaces:**
- Consumes: completed builders and current EF schema.
- Produces: one candidate Draw.io and seven temporary QA renders.

- [ ] **Step 1: Point the generator at the candidate path**

Use the existing `write_drawio` entry point:

```python
output = Path(".tmp/erd-redesign/WarePro_ERD_Tong_20260730.candidate.drawio")
root = write_drawio(output, Path("QuanLyHangHoa/Data/AppDbContext.cs"), Path("QuanLyHangHoa/Models"))
assert len(root.findall("diagram")) == 7
```

- [ ] **Step 2: Run the full focused test suite**

Run:

```powershell
python -m unittest discover -s .tmp/erd-mvvm-revision -p 'test_*.py' -v
```

Expected: all tests pass.

- [ ] **Step 3: Generate the candidate**

Run:

```powershell
python .tmp/erd-mvvm-revision/generate_drawio.py
```

Expected: `PAGES=7` and the candidate path is printed.

- [ ] **Step 4: Render all pages with the installed diagrams.net CLI**

Run:

```powershell
New-Item -ItemType Directory -Force '.tmp\erd-redesign\rendered' | Out-Null
& 'C:\Program Files\draw.io\draw.io.exe' --export --format png --all-pages --output '.tmp\erd-redesign\rendered' '.tmp\erd-redesign\WarePro_ERD_Tong_20260730.candidate.drawio'
```

Expected: seven PNG page renders are created; no delivered PNG/SVG is copied outside `.tmp`.

- [ ] **Step 5: Inspect every rendered page**

Open all seven PNGs with the local image viewer. Check readable text, balanced spacing, no clipped cards, no edge through a table, no overlapping labels, no purple/violet, correct external-card dashed styling, and visual similarity to or improvement over the PlantUML references. If any check fails, adjust only the responsible page layout/route and repeat Steps 2–5.

### Task 8: Final QA, backup, and Desktop replacement

**Files:**
- Read: `.tmp/erd-redesign/WarePro_ERD_Tong_20260730.candidate.drawio`
- Create: `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.before-redesign-20260731.drawio`
- Modify: `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio`

**Interfaces:**
- Consumes: a candidate that passed structural tests and seven-page visual QA.
- Produces: a recoverable Desktop backup and the approved redesigned Draw.io artifact.

- [ ] **Step 1: Run fresh structural verification immediately before copy**

Run:

```powershell
python -c "import sys; from pathlib import Path; sys.path.insert(0, '.tmp/erd-mvvm-revision'); from detail_verifier import verify_file; print(verify_file(Path('.tmp/erd-redesign/WarePro_ERD_Tong_20260730.candidate.drawio'), Path('.tmp/erd-redesign/WarePro_ERD_Tong_20260730.source.drawio')))"
```

Expected: seven pages, approved names, zero false/missing edges, zero collisions/duplicate routes, zero forbidden colors, zero encoding errors, and `overview_uses_bundles=True`.

- [ ] **Step 2: Confirm the Desktop source has not changed during implementation**

Run:

```powershell
Get-FileHash 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio' -Algorithm SHA256
Get-Content '.tmp\erd-redesign\source.sha256'
```

Expected: both SHA-256 values match. If they differ, stop before overwriting and reconcile the newer Desktop file.

- [ ] **Step 3: Back up and replace the exact Desktop file**

After sandbox approval for the external write, run:

```powershell
Copy-Item -LiteralPath 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio' -Destination 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.before-redesign-20260731.drawio'
Copy-Item -LiteralPath '.tmp\erd-redesign\WarePro_ERD_Tong_20260730.candidate.drawio' -Destination 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio' -Force
```

Expected: backup and target both exist; the target hash equals the candidate hash.

- [ ] **Step 4: Reopen/export the replaced Desktop artifact once**

Run the diagrams.net CLI against the final Desktop path into a new temporary QA folder. Expected: seven pages export successfully, proving the copied artifact opens after replacement.

- [ ] **Step 5: Verify repository scope and report completion**

Run:

```powershell
git status --short
git log -1 --oneline
```

Expected: unrelated dirty files are unchanged; no implementation helper or generated render is staged; the only repository commit for this phase is the approved planning document. Report the final Desktop path, backup path, seven-page QA result, and explicitly state that no DOCX/PDF/delivered PNG was created.
