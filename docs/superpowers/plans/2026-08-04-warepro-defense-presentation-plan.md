# WarePro Graduation Defense Presentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and verify a 19-slide, 15-minute editable PowerPoint defense deck whose visible claims and visuals are traceable to the newest DOCX/PDF in `E:\Minh\DATN\Final` and the current thesis asset set.

**Architecture:** Use the inspected `Long_DATN_ppt.pptx` as the inherited visual source. Create a frame map, duplicate source slides into a starter deck, then edit inherited text/image slots with `@oai/artifact-tool`; export one final copy to `E:\Minh\DATN\Final` and keep all intermediate renders, notes, layouts, and audit ledgers under `.tmp\defense-ppt`.

**Tech Stack:** JavaScript ES modules, `@oai/artifact-tool`, bundled Node.js/Python runtimes, PowerPoint template-following scripts, `render_slides.py`, `slides_test.py`, PowerShell ZIP/XML checks, and the newest local thesis DOCX/PDF plus current diagram/screenshot assets.

## Global Constraints

- Duration: 15 minutes; output: exactly 19 slides.
- Visible claims must be supported by the newest DOCX/PDF or current implementation evidence; unsupported claims are deleted.
- Preserve the source deck's master/layout hierarchy, typography, spacing, footer, and slide-number behavior.
- Use white background, black/dark text, sparse composition, and no purple/violet accents.
- Add `[Sources]` blocks to speaker notes for every external asset and non-trivial claim.
- Keep source DOCX/PDF/PPTX unchanged; final PPTX is a new copy.
- Render and inspect every final slide; fix overflow, clipping, wrapping, overlap, placeholder, and provenance defects before handoff.

---

### Task 1: Lock source evidence and slide copy

**Files:**
- Create: `.tmp/defense-ppt/source-notes.txt`
- Create: `.tmp/defense-ppt/content-lock.json`
- Read: `E:\Minh\DATN\Final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh.docx`
- Read: `E:\Minh\DATN\Final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh.pdf`
- Read: `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\`

**Interfaces:**
- Produces `content-lock.json` with 19 records: `slide`, `title`, `claim`, `evidence`, `sourcePath`, `sourceLocator`, `allowedVisibleText`.
- Produces `source-notes.txt` with local paths, source timestamps, web URLs, and asset provenance.

- [ ] **Step 1: Reconfirm source timestamps and hashes.**

Run:
```powershell
Get-ChildItem -LiteralPath 'E:\Minh\DATN\Final' -File |
  Where-Object Extension -in '.docx','.pdf' |
  Sort-Object LastWriteTime -Descending |
  Select-Object FullName,Length,LastWriteTime
Get-FileHash -LiteralPath 'E:\Minh\DATN\Final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh.docx'
Get-FileHash -LiteralPath 'E:\Minh\DATN\Final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh.pdf'
```

Expected: the 02/08/2026 DOCX/PDF pair remains newest and hashes are recorded in `source-notes.txt`.

- [ ] **Step 2: Extract headings, evaluation evidence, and figure captions.**

Use the bundled Python runtime with `PYTHONUTF8=1`, `python-docx`, and `pdfplumber`; record only facts already present in the Final pair. Confirm the values used by slides: WPF/MVVM/EF Core/SQL Server, `StockBalance`, `StockLedger`, `ProductSerial`, four document states, RowVersion/deadlock handling, warranty traceability, and 904 passing tests at commit `41cc3a7`.

Expected: no slide copy contains a number, module, role, or result absent from the extracted source text.

- [ ] **Step 3: Write the 19-slide content lock.**

Use the approved slide sequence from `docs/superpowers/specs/2026-08-04-warepro-defense-presentation-design.md`. For each slide, write one sentence claim and one or more exact source locators. Keep exclusions visible only on the scope and limitations slides.

- [ ] **Step 4: Review the lock for unsupported claims.**

Run:
```powershell
rg -n -i 'TBD|TODO|invent|fabricat|placeholder|should|probably' '.tmp\defense-ppt\content-lock.json'
```

Expected: zero matches.

### Task 2: Inspect template and map every output slide

**Files:**
- Read: `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\03_Tai_lieu_tham_khao\Do_an_tot_nghiep\Long_DATN_ppt.pptx`
- Create: `.tmp/defense-ppt/template-audit.txt`
- Create: `.tmp/defense-ppt/template-frame-map.json`
- Create: `.tmp/defense-ppt/deviation-log.txt`
- Create: `.tmp/defense-ppt/template-inspect/` (generated)

**Interfaces:**
- `template-frame-map.json` contains 19 `outputSlides` records. Every record has `outputSlide`, `sourceSlide`, `narrativeRole`, `reuseMode`, and explicit `editTargets`.

- [ ] **Step 1: Render and inspect all 39 source slides.**

Use artifact-tool import/export; if the Windows runtime lacks `unzip`, use the already-tested local ZIP compatibility wrapper without editing the installed skill. Preserve source renders and layouts under `.tmp\defense-ppt\template-inspect`.

- [ ] **Step 2: Record reusable source patterns.**

Document source slide numbers for title, agenda, text-plus-visual, flowchart, result, conclusion, and closing patterns. Record typography, footer, slide number, inherited placeholders, and image frames in `template-audit.txt`.

- [ ] **Step 3: Map output to source slides.**

Choose source slide patterns that support the 19 narrative roles. Reuse a source slide multiple times only when its inherited slots remain appropriate. Record omitted source slides and reasons.

- [ ] **Step 4: Self-validate the map.**

Run the template map validator and confirm all output slides map to source slides and every edit target resolves to an inherited element. Record any intentional deviation in `deviation-log.txt`.

### Task 3: Build and verify the inherited starter deck

**Files:**
- Create: `.tmp/defense-ppt/template-starter.pptx`
- Create: `.tmp/defense-ppt/template-starter-preview/`
- Create: `.tmp/defense-ppt/template-starter-layout/`
- Create: `.tmp/defense-ppt/template-starter-contact-sheet.png`

- [ ] **Step 1: Duplicate mapped source slides.**

Run `prepare_template_starter_deck.mjs` with the source PPTX and `template-frame-map.json`.

- [ ] **Step 2: Inspect the starter deck.**

Confirm slide count is 19, source masters/layouts remain inherited, and every inherited placeholder is either filled or classified for deletion.

- [ ] **Step 3: Render the starter deck.**

Use artifact-tool render output and inspect each slide before editing. Reject any starter slide with unintended empty prompts, lost footer, or broken inherited geometry.

### Task 4: Author the deck from locked copy and assets

**Files:**
- Create: `.tmp/defense-ppt/build-defense-deck.mjs`
- Create: `.tmp/defense-ppt/final-layout/`
- Create: `.tmp/defense-ppt/final-slides/`
- Create: `.tmp/defense-ppt/final-source-notes.txt`
- Create: `E:\Minh\DATN\Final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh_thuyet_trinh.pptx`

**Interfaces:**
- `build-defense-deck.mjs` imports `template-starter.pptx`, resolves inherited element IDs from the map, edits only those targets, adds speaker notes, renders previews/layouts, and exports the final PPTX.
- Speaker notes use the exact form `[Sources]\n- <source path or URL>\n[/Sources]` per slide.

- [ ] **Step 1: Resolve inherited text and image targets.**

Use `presentation.inspect({ kind: "slide,textbox,shape,image,notes,layout" })` and `presentation.resolve(anchorId)`; never clear text by broad heuristics. Keep source chrome and authentic logos unless the map explicitly marks a source element for rewrite/delete.

- [ ] **Step 2: Insert only evidence-backed copy.**

Populate the 19 slides from `content-lock.json`. Preserve inherited font family/size/weight/spacing. Shorten copy or change mapped source layout when text does not fit; do not silently shrink text.

- [ ] **Step 3: Insert current thesis visuals.**

Use current diagrams/screenshots from `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\` and the Final thesis figures. Add alt text and provenance notes. Do not generate fake UI, metrics, architecture, or user outcomes.

- [ ] **Step 4: Add speaker notes.**

Add a short talk track and `[Sources]` block to all 19 slides. Notes may include timing guidance but visible slides may not.

- [ ] **Step 5: Export and write an audit ledger.**

Export the new PPTX to the Final directory and record the output hash, slide count, source pair hashes, and build timestamp in `.tmp\defense-ppt\final-source-notes.txt`.

### Task 5: Render, inspect, and enforce source synchronization

**Files:**
- Read: `.tmp/defense-ppt/content-lock.json`
- Read: `E:\Minh\DATN\Final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh_thuyet_trinh.pptx`
- Create: `.tmp/defense-ppt/qa-report.txt`

- [ ] **Step 1: Render every final slide.**

Run `render_slides.py` and inspect each PNG at full size plus one deck montage for pacing.

- [ ] **Step 2: Run overflow and fidelity checks.**

Run `slides_test.py` and `check_template_fidelity.mjs`. Treat any overflow, clipping, lost source master, unfilled placeholder, or unresolved edit target as a failure.

- [ ] **Step 3: Check final PPTX XML placeholders.**

Inspect every `ppt/slides/slide*.xml`; reject empty structural placeholders and visible default prompt text. Confirm notes XML contains `[Sources]` for all 19 slides.

- [ ] **Step 4: Check visible-copy synchronization.**

Extract final slide text and compare against `content-lock.json`. Review every claim manually against the Final DOCX/PDF and remove any text not backed by a source locator.

- [ ] **Step 5: Record QA evidence.**

Write slide count, overflow result, fidelity result, placeholder result, notes coverage, source hash comparison, and any fixed issue in `qa-report.txt`. Only after all checks pass may the deck be reported as complete.

### Task 6: Final handoff

**Files:**
- Final: `E:\Minh\DATN\Final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh_thuyet_trinh.pptx`
- QA: `.tmp/defense-ppt/qa-report.txt`

- [ ] **Step 1: Reopen the exported PPTX and verify final hash.**

Confirm the file is readable, contains exactly 19 slides, and its source-note ledger matches the delivered bytes.

- [ ] **Step 2: Preserve source files and report limitations honestly.**

Do not modify or overwrite the Final DOCX/PDF or the reference PPTX. Mention any visual QA limitation explicitly if a renderer cannot inspect a slide.

- [ ] **Step 3: Deliver one clickable PPTX link.**

Include only verified claims, source paths, QA evidence, and the final artifact link in the handoff.
