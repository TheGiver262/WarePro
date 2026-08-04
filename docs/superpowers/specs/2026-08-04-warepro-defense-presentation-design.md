# WarePro Graduation Defense Presentation

## Communication job

By the end of a 15-minute defense, the thesis committee should understand that WarePro solves a traceability and data-integrity problem in serial-managed inventory and warranty operations because its workflow, layered architecture, transaction boundary, and test evidence are connected end to end.

## Audience and outcome

- Audience: graduation-defense committee and supervising lecturer.
- Outcome: committee can follow the business problem, judge the design choices, see the implemented product surface, and assess the evidence and remaining limits.
- Central takeaway: the project contribution is not the number of screens; it is the consistent chain from product and serial number to stock documents, invoices, warranty, authorization, and audit.
- Duration: 15 minutes.
- Deck size: 19 slides, averaging 40-55 seconds per slide. Timing stays in speaker notes, not visible slide copy.

## Narrative and slide contract

Each slide has one claim and answers the question raised by the previous slide.

1. Cover: identify the project, student, and supervisor.
2. Problem: disconnected spreadsheets make stock, serial, invoice, and warranty history hard to verify.
3. Objective: unify those records while preserving traceability and integrity.
4. Scope: show included modules and explicit exclusions.
5. Core workflow: connect receiving, issuing, selling, and warranty as one chain.
6. Requirements: show actors and the highest-value functional constraints.
7. Solution overview: show WarePro's end-to-end information flow.
8. Architecture: explain WPF, MVVM, service layer, EF Core, and SQL Server boundaries.
9. Data model: show module-level relationships without an unreadable full ERD.
10. Core design: explain StockBalance, StockLedger, and ProductSerial as current state, history, and item identity.
11. Document lifecycle: show Draft -> PendingApproval -> Approved -> Posted and reversal instead of destructive edits.
12. Posting transaction: show atomic updates for balance, serial, ledger, audit, and document state.
13. Concurrency: show RowVersion protection and bounded deadlock retry.
14. Serial and warranty: show serial traceability and warranty state handling.
15. Invoice and warranty: show how sales history supports after-sales processing.
16. Authorization and audit: show service-layer authorization, query safety, and audit records.
17. Product surface: show dashboard and representative inventory/warranty screens from the current artifact set.
18. Evaluation: show 904 passing tests, what they cover, and honest limitations.
19. Close: summarize contribution, state next steps, and invite questions.

## Visual contract

- Use the approved local reference deck `Long_DATN_ppt.pptx` as the source visual template.
- Duplicate source slide patterns; preserve its master/layout hierarchy, typography, spacing, footer, and slide-number behavior.
- Reuse source patterns for title, agenda/section, text-plus-visual, flowchart, result, conclusion, and question slides. Do not retain unrelated neural-network content.
- Keep the visual system white background, black/dark text, large titles, sparse composition, and high-contrast diagrams. No purple/violet accents.
- Prefer real current thesis figures, current application screenshots, and current ERD/module exports. Do not invent UI, metrics, quotes, or architecture evidence.
- Use at least the source deck's visual readability; shorten copy before reducing inherited font sizes.
- Keep diagrams limited to the few that materially clarify data flow, architecture, lifecycle, and transaction behavior.

## Source and provenance contract

Primary local sources:

- `E:\Minh\DATN\Final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh.docx`
- `E:\Minh\DATN\Final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh.pdf`
- `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\03_Tai_lieu_tham_khao\Do_an_tot_nghiep\Long_DATN_ppt.pptx`
- Current diagram and image assets under `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\`.

The deck must include `[Sources]` blocks in speaker notes for every externally sourced non-trivial claim and every external asset. Local thesis figures and screenshots should identify their source file/path in notes. Web references may include the official SoICT graduation page and HUST IT thesis-course outcome PDF used only to calibrate defense emphasis.

## QA and acceptance criteria

- Final deliverable is one editable `.pptx` copy; source files remain unchanged.
- Every output slide maps to an inspected source slide in `template-frame-map.json`.
- All inherited placeholders are intentionally filled or deleted; no default `Slide Number`, `Date`, `Footer`, or empty prompt remains.
- Render every final slide to PNG and inspect every slide individually at full size.
- Run slide overflow testing and template-fidelity validation.
- Fix all unintended clipping, wrapping, overlap, unreadable diagrams, missing glyphs, and broken source notes before delivery.
- Confirm the deck contains no unsupported claim beyond the latest DOCX/PDF and current implementation evidence.

## Out of scope

- No full thesis rewrite.
- No new product functionality or code changes.
- No invented demo data, fabricated benchmark, or claim that limitations are solved.
- No public upload or external sharing of the thesis or deck.
