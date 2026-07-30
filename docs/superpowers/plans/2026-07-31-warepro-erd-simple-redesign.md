# WarePro Simple ERD Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the bundled and duplicate relationships in the seven-page WarePro Draw.io file with clean, unlabeled, direct crow's-foot relationships, at most one edge per table pair.

**Architecture:** Reuse the existing schema parser, table-card builders, Manhattan router, and verifier under `.tmp/erd-mvvm-revision`. Add one deterministic relationship-collapsing helper, feed its output to all six detail pages, and replace overview gateways with one routed edge for each pair already represented by `OVERVIEW_EDGES`.

**Tech Stack:** Python 3 standard library, `xml.etree.ElementTree`, `unittest`, Draw.io desktop CLI.

## Global Constraints

- Work directly on `main`; do not create a worktree.
- Preserve all unrelated dirty files and never stage them.
- Keep exactly seven pages with their current Vietnamese names.
- Keep existing tables and table attributes.
- Draw at most one unlabeled edge for each `(principal, dependent)` pair on a page.
- Preserve aggregated foreign-key names only in `data-foreign-keys`; never show them as an edge label.
- Use direct orthogonal crow's-foot edges; no gateway, bundle, or module bus.
- Keep external-reference cards dashed and visually subordinate.
- Do not use purple or violet.
- Do not modify DOCX, PDF, or either reference file in Downloads.
- Back up the latest Desktop file immediately before replacement.

---

### Task 1: Snapshot the latest source and lock the simplified relationship contract

**Files:**
- Read: `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio`
- Create: `.tmp/erd-simple-redesign/WarePro_ERD_Tong_20260730.source.drawio`
- Modify: `.tmp/erd-mvvm-revision/test_drawio_generation.py`
- Modify: `.tmp/erd-mvvm-revision/test_detail_pages.py`
- Modify: `.tmp/erd-mvvm-revision/test_detail_verifier.py`

**Interfaces:**
- Consumes: the latest Desktop Draw.io file and `revision_tools.build_drawio_document(Path, Path) -> Element`.
- Produces: failing tests for one edge per pair, empty edge values, no bundles, and exact post-collapse counts.

- [ ] **Step 1: Record and copy the latest source**

Run:

```powershell
Get-FileHash -LiteralPath 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio' -Algorithm SHA256
Copy-Item -LiteralPath 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio' -Destination '.tmp\erd-simple-redesign\WarePro_ERD_Tong_20260730.source.drawio'
```

Expected: the source copy exists and its SHA256 equals the Desktop file.

- [ ] **Step 2: Replace bundle-oriented overview assertions**

Change `test_drawio_generation.py` to assert:

```python
edges = overview.findall(".//mxCell[@edge='1']")
pairs = [
    (edge.get("data-principal"), edge.get("data-dependent"))
    for edge in edges
]
self.assertEqual(len(edges), 36)
self.assertEqual(len(pairs), len(set(pairs)))
self.assertFalse(overview.findall(".//mxCell[@data-bundle='1']"))
self.assertFalse(overview.findall(".//mxCell[@data-gateway='1']"))
self.assertTrue(all(edge.get("value", "") == "" for edge in edges))
```

- [ ] **Step 3: Replace detail edge-count assertions**

Use these exact counts in `test_detail_pages.py` and `test_detail_verifier.py`:

```python
expected_counts = {
    "page_catalog": 33,
    "page_stock": 29,
    "page_control": 26,
    "page_invoice": 15,
    "page_warranty": 7,
    "page_user": 12,
}
```

For each page, also assert:

```python
pairs = [
    (edge.get("data-principal"), edge.get("data-dependent"))
    for edge in edges
]
self.assertEqual(len(pairs), len(set(pairs)))
self.assertTrue(all(edge.get("value", "") == "" for edge in edges))
```

- [ ] **Step 4: Run the focused tests and confirm failure**

Run:

```powershell
python -m unittest .tmp/erd-mvvm-revision/test_drawio_generation.py .tmp/erd-mvvm-revision/test_detail_pages.py .tmp/erd-mvvm-revision/test_detail_verifier.py -v
```

Expected: FAIL because the current generator still creates bundles, duplicate pairs, labels, and old edge counts.

### Task 2: Collapse multiple foreign keys into one table-pair relationship

**Files:**
- Create: `.tmp/erd-mvvm-revision/relationship_simplifier.py`
- Create: `.tmp/erd-mvvm-revision/test_relationship_simplifier.py`
- Modify: `.tmp/erd-mvvm-revision/detail_page_builder.py`

**Interfaces:**
- Consumes: normalized relationship dictionaries containing `principal`, `dependent`, `foreign_keys`, `relationship`, and `source_line`.
- Produces: `collapse_relationships(items: list[dict]) -> list[dict]` with stable pair order and deduplicated foreign keys.

- [ ] **Step 1: Write the failing unit test**

```python
def test_collapses_roles_between_the_same_tables():
    items = [
        {"principal": "AppUser", "dependent": "StockIn", "foreign_keys": ["CreatedBy"], "relationship": "WithMany", "source_line": 1},
        {"principal": "AppUser", "dependent": "StockIn", "foreign_keys": ["ApprovedBy"], "relationship": "WithMany", "source_line": 2},
        {"principal": "AppUser", "dependent": "StockIn", "foreign_keys": ["CreatedBy"], "relationship": "WithMany", "source_line": 3},
    ]
    self.assertEqual(
        collapse_relationships(items),
        [{
            "principal": "AppUser",
            "dependent": "StockIn",
            "foreign_keys": ["CreatedBy", "ApprovedBy"],
            "relationship": "WithMany",
            "source_line": 1,
            "source_lines": [1, 2, 3],
        }],
    )
```

- [ ] **Step 2: Run the unit test and confirm failure**

Run:

```powershell
python -m unittest .tmp/erd-mvvm-revision/test_relationship_simplifier.py -v
```

Expected: FAIL because `relationship_simplifier.py` does not exist.

- [ ] **Step 3: Implement the minimal stable collapse helper**

```python
def collapse_relationships(items):
    grouped = {}
    for item in items:
        key = (item["principal"], item["dependent"])
        if key not in grouped:
            grouped[key] = {
                **item,
                "foreign_keys": [],
                "source_lines": [],
            }
        target = grouped[key]
        for foreign_key in item["foreign_keys"]:
            if foreign_key not in target["foreign_keys"]:
                target["foreign_keys"].append(foreign_key)
        target["source_lines"].append(item["source_line"])
    return list(grouped.values())
```

- [ ] **Step 4: Apply collapse after each detail manifest is selected**

In `_build_detail_page`, transform the selected relationships before calculating cards, incident edges, routes, or metadata:

```python
selected = collapse_relationships(selected)
```

- [ ] **Step 5: Run simplifier and detail tests**

Run:

```powershell
python -m unittest .tmp/erd-mvvm-revision/test_relationship_simplifier.py .tmp/erd-mvvm-revision/test_detail_pages.py -v
```

Expected: simplifier PASS; detail pages may still fail only on labels or route assertions handled in Task 3.

### Task 3: Remove labels and route direct detail-page edges cleanly

**Files:**
- Modify: `.tmp/erd-mvvm-revision/detail_page_builder.py`
- Modify: `.tmp/erd-mvvm-revision/detail_layout_v2.py`
- Modify: `.tmp/erd-mvvm-revision/test_detail_pages.py`

**Interfaces:**
- Consumes: collapsed relationships from `collapse_relationships` and current table geometries.
- Produces: one unlabeled `mxCell` edge per table pair with aggregated `data-foreign-keys` and orthogonal waypoints.

- [ ] **Step 1: Force visible edge values to be empty**

In `_add_edge`, retain metadata but use:

```python
"value": "",
"data-foreign-keys": ",".join(item["foreign_keys"]),
"data-relationship-count": str(len(item.get("source_lines", [item["source_line"]]))),
```

- [ ] **Step 2: Preserve standard crow's-foot endpoints**

Keep `startArrow=ERone` on the principal side and choose the dependent end from the existing optional/unique rules. For aggregated role relationships that are not unique, use `endArrow=ERzeroToMany`.

- [ ] **Step 3: Route after collapse**

Recalculate `incident`, port ratios, and Manhattan paths from the collapsed list so each pair receives one lane. Continue using `_grid_path` and reject any route segment intersecting a non-endpoint table rectangle.

- [ ] **Step 4: Run the detail tests**

Run:

```powershell
python -m unittest .tmp/erd-mvvm-revision/test_relationship_simplifier.py .tmp/erd-mvvm-revision/test_detail_pages.py -v
```

Expected: PASS with counts `[33, 29, 26, 15, 7, 12]`, no duplicate pairs, and no edge labels.

### Task 4: Replace overview gateways with direct pair edges

**Files:**
- Modify: `.tmp/erd-mvvm-revision/overview_v2.py`
- Modify: `.tmp/erd-mvvm-revision/test_drawio_generation.py`

**Interfaces:**
- Consumes: the 36 unique table pairs already listed by `OVERVIEW_EDGES`, overview card geometries, and six module rectangles.
- Produces: `build_overview(fields, relationships) -> Element` with exactly 36 direct, unlabeled edges and no gateway/bundle cells.

- [ ] **Step 1: Remove gateway and trunk construction**

Delete calls to `_gateway_point`, `_pair_sides`, `_trunk_points`, and all `data-bundle="1"` edge creation. Do not replace them with module-level edges.

- [ ] **Step 2: Create one edge for each unique overview pair**

Normalize `OVERVIEW_EDGES` in stable order:

```python
pairs = []
seen = set()
for principal, dependent, _label, _cross in OVERVIEW_EDGES:
    key = (principal, dependent)
    if key not in seen:
        seen.add(key)
        pairs.append(key)
```

Create each edge directly from `ov_<principal>` to `ov_<dependent>`, set `value=""`, and retain only `data-principal` and `data-dependent` relationship metadata.

- [ ] **Step 3: Route overview edges through free corridors**

Use the card rectangles as obstacles and assign orthogonal lanes around module/card gaps. Reject any path that intersects a non-endpoint card; prefer the shortest valid path and spread incident ports along card sides.

- [ ] **Step 4: Run the overview test**

Run:

```powershell
python -m unittest .tmp/erd-mvvm-revision/test_drawio_generation.py -v
```

Expected: PASS with 36 unique direct edges, zero bundles, zero gateways, and zero labels.

### Task 5: Strengthen the verifier and generate the seven-page candidate

**Files:**
- Modify: `.tmp/erd-mvvm-revision/detail_verifier.py`
- Modify: `.tmp/erd-mvvm-revision/test_detail_verifier.py`
- Modify: `.tmp/erd-mvvm-revision/generate_drawio.py`
- Create: `.tmp/erd-simple-redesign/WarePro_ERD_Tong_20260730.candidate.drawio`

**Interfaces:**
- Consumes: the simplified generator and the source snapshot from Task 1.
- Produces: a verification report with page counts, pair coverage, label/gateway checks, route collisions, colors, and encoding errors.

- [ ] **Step 1: Add explicit verifier fields**

Compute and return these fields from `verify_file`:

```python
page_edges = [
    diagram.findall(".//mxCell[@edge='1']")
    for diagram in root.findall("diagram")
]
report["overview_edge_count"] = len(page_edges[0])
report["edge_counts"] = [len(edges) for edges in page_edges[1:]]
report["duplicate_pairs"] = [
    diagram.get("name")
    for diagram, edges in zip(root.findall("diagram"), page_edges)
    if len([
        (edge.get("data-principal"), edge.get("data-dependent"))
        for edge in edges
    ])
    != len({
        (edge.get("data-principal"), edge.get("data-dependent"))
        for edge in edges
    })
]
report["labeled_edges"] = [
    edge.get("id") for edges in page_edges for edge in edges
    if edge.get("value", "")
]
report["bundle_edges"] = [
    edge.get("id") for edges in page_edges for edge in edges
    if edge.get("data-bundle") == "1"
]
report["gateway_cells"] = [
    cell.get("id")
    for cell in root.findall(".//mxCell[@data-gateway='1']")
]
```

Assert expected values in the test: overview `36`, detail counts `[33, 29, 26, 15, 7, 12]`, and all four issue lists empty. Keep the existing checks for false/missing relationships, waypoint collisions, duplicate routes, forbidden colors, and encoding errors.

- [ ] **Step 2: Run the full test suite**

Run:

```powershell
python -m unittest discover -s '.tmp\erd-mvvm-revision' -p 'test_*.py' -v
```

Expected: all tests PASS.

- [ ] **Step 3: Generate the candidate**

Set `generate_drawio.py` output to:

```python
Path(".tmp/erd-simple-redesign/WarePro_ERD_Tong_20260730.candidate.drawio")
```

Run:

```powershell
python .tmp/erd-mvvm-revision/generate_drawio.py
```

Expected: `PAGES=7` and the candidate path is printed.

- [ ] **Step 4: Verify the candidate against the snapshot**

Run:

```powershell
python -c "import json,sys; from pathlib import Path; sys.path.insert(0,'.tmp/erd-mvvm-revision'); from detail_verifier import verify_file; print(json.dumps(verify_file(Path('.tmp/erd-simple-redesign/WarePro_ERD_Tong_20260730.candidate.drawio'), Path('.tmp/erd-simple-redesign/WarePro_ERD_Tong_20260730.source.drawio')), ensure_ascii=True))"
```

Expected: seven pages; overview 36 edges; detail counts `[33,29,26,15,7,12]`; all issue lists empty.

### Task 6: Render, inspect, back up, and replace the Desktop artifact

**Files:**
- Read: `.tmp/erd-simple-redesign/WarePro_ERD_Tong_20260730.candidate.drawio`
- Create: `.tmp/erd-simple-redesign/rendered/page-1.png` through `page-7.png`
- Create: `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.before-simple-redesign-20260731.drawio`
- Modify: `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio`

**Interfaces:**
- Consumes: a candidate with a clean structural report.
- Produces: the final Desktop Draw.io file plus a recoverable backup of the exact pre-replacement file.

- [ ] **Step 1: Render all seven pages for visual QA**

Run Draw.io CLI once per page with `--page-index 1` through `7`, writing only to `.tmp/erd-simple-redesign/rendered/`.

Expected: seven non-empty PNGs.

- [ ] **Step 2: Inspect all pages**

Check that cards are aligned, text is readable, external references are dashed, edges do not cross cards, no labels appear, and the visual style matches the clean PlantUML/Mermaid references.

- [ ] **Step 3: Guard against concurrent edits**

Hash the current Desktop file and compare it with the Task 1 snapshot. If hashes differ, stop and ask the user rather than overwriting newer work.

- [ ] **Step 4: Back up and replace**

Run:

```powershell
Copy-Item -LiteralPath 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio' -Destination 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.before-simple-redesign-20260731.drawio'
Copy-Item -LiteralPath '.tmp\erd-simple-redesign\WarePro_ERD_Tong_20260730.candidate.drawio' -Destination 'C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio' -Force
```

Expected: backup hash equals the source snapshot; final Desktop hash equals the candidate.

- [ ] **Step 5: Verify the final Desktop copy**

Run the verifier directly against the Desktop file and render page 1 once more. Expected: the same clean report and the same page-1 image hash as the accepted candidate render.

## Final Verification

Run:

```powershell
python -m unittest discover -s '.tmp\erd-mvvm-revision' -p 'test_*.py' -v
git status --short
```

Expected: all tests PASS; repository status contains only the user's unrelated pre-existing files plus committed design/plan documents; no generated artifact is staged.
