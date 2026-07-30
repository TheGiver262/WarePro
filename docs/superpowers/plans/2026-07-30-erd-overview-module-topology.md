# ERD Overview Module Topology Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce the current overview ERD from 13 inter-module trunks to the six module connections approved from the old DOCX overview.

**Architecture:** Treat the current Draw.io file as the immutable baseline and edit only the first `<diagram>`. Remove disallowed module trunks, their module branches, and their two junction vertices by `data-module-pair`; preserve all other XML cells and all six later diagram blocks.

**Tech Stack:** Python 3 standard library (`xml.etree.ElementTree`, `pathlib`), Draw.io XML, `unittest`.

## Global Constraints

- Work directly on `main`; do not create a worktree.
- Preserve the current Draw.io state last modified at 2026-07-30 16:08:54.
- Allowed pairs are exactly `catalog__stock`, `catalog__invoice`, `stock__control`, `control__warranty`, `invoice__user`, and `user__warranty`.
- Do not edit DOCX or create PNG.
- Preserve diagram pages 2–7 byte-for-byte.

---

### Task 1: Prune overview topology

**Files:**
- Create: `.tmp/erd-overview-redraw/prune_overview_topology.py`
- Test: `.tmp/erd-overview-redraw/test_prune_overview_topology.py`

**Interfaces:**
- Consumes: current Draw.io bytes and `set[str]` of allowed `data-module-pair` values.
- Produces: `prune_overview(source: bytes, allowed_pairs: set[str]) -> bytes`.

- [ ] **Step 1: Write the failing test**

Create a small seven-page XML fixture containing allowed and rejected trunks,
branches, and junctions. Assert that rejected pairs disappear, six allowed pairs
remain, internal edges remain, and diagram blocks 2–7 are identical.

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
rtk proxy C:\Users\player\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8 .tmp\erd-overview-redraw\test_prune_overview_topology.py -v
```

Expected: `ImportError` or missing `prune_overview` before implementation.

- [ ] **Step 3: Implement the minimum XML transformation**

In `prune_overview`, parse only the first diagram model, remove every `mxCell`
whose `data-scope` is `module-trunk` or `module-branch`, or whose
`data-junction` is present, when its `data-module-pair` is not allowed. Serialize
the first diagram and reassemble it with the untouched source byte ranges for
pages 2–7.

- [ ] **Step 4: Run the test to verify it passes**

Run the command from Step 2.

Expected: all tests pass with exit code 0.

### Task 2: Apply and verify the approved topology

**Files:**
- Modify: `C:\Users\player\Desktop\DATN\final\WarePro_ERD_Tong_20260730.drawio`
- Create backup: `.tmp/erd-overview-redraw/backup/WarePro_ERD_Tong_20260730.before-six-trunk-topology.drawio`

**Interfaces:**
- Consumes: `prune_overview` and the six allowed pairs.
- Produces: final Draw.io file with six inter-module trunks.

- [ ] **Step 1: Back up the current file and generate a candidate**

Copy the live file to the exact backup path. Generate the candidate from that
backup, never from an older generated artifact.

- [ ] **Step 2: Run structural assertions**

Assert: 7 pages, 6 modules, 31 table vertices, 17 internal edges, 6 module
trunks, 30 module branches, 12 junctions, no disallowed pair, and byte-identical
pages 2–7. Assert every remaining trunk has non-empty relationship metadata.

- [ ] **Step 3: Inspect the candidate in Draw.io**

Open the candidate, fit the overview page to the window, and verify that no
remaining trunk crosses a table and that removing the seven pairs leaves no
orphan branch or junction.

- [ ] **Step 4: Replace the final file and repeat structural assertions**

Copy the verified candidate over the final Draw.io file, then run the same
assertions against the final path and record its SHA-256.
