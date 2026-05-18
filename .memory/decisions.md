# Architecture and Design Decisions (ADRs)

Approved technical decisions and architectural patterns for `ProductManagement_Antigravity`.

## ADR 1: WPF Pro Max UI Standard Layout
* **Status**: Approved
* **Date**: 2026-05-18
* **Context**: Main views suffered from layout inconsistency on wide monitors.
* **Decision**: All list-based Views must implement:
  - Container Grid with `MaxWidth="1600"` and `HorizontalAlignment="Stretch"`.
  - Consistent 3-Row Layout: Row 0 (Header card), Row 1 (Filter controls card), Row 2 (DataGrid).
  - Standardized control buttons using `ProMaxIconButtonStyle`.

## ADR 2: Excel Export dynamically bounded
* **Status**: Approved
* **Date**: 2026-05-18
* **Context**: Exporting full database records caused high database overhead and ignored UI filters.
* **Decision**: All Excel exports must use ClosedXML (`XLWorkbook` / `XLWorksheet`) and loop directly through the in-memory ObservableCollection currently bound to the DataGrid. It must only export rows currently shown on screen and avoid re-querying the database.

## ADR 3: Non-Collapsible Filter Resets
* **Status**: Approved
* **Date**: 2026-05-18
* **Context**: Pressing the reset filter button collapsed the advanced filter panel, disrupting search flow.
* **Decision**: The `ResetFilter()` command in ViewModels must ONLY clear values and search keywords. It must NOT set `IsAdvancedFilterOpen = false`.
