# Eliminate N+1 Queries Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove every confirmed N+1 database-query loop from stock posting, reversal, and dynamic import while preserving current business behavior.

**Architecture:** Keep the existing EF Core services and transaction boundaries. Before each affected loop, collect distinct keys, issue one `Contains` query per entity type, materialize dictionaries/lookups, then perform loop work in memory. Add a test-only SELECT counter to prove query counts stay nearly constant as row counts grow.

**Tech Stack:** .NET 8, C#, EF Core 8, SQLite in-memory tests, xUnit.

## Global Constraints

- Do not change public service APIs, database schema, authorization, idempotency, validation, audit, or transaction behavior.
- Do not add repositories, data-loader abstractions, raw SQL, packages, or migrations.
- Preserve row-level error messages and partial-success semantics in dynamic import.
- Keep `TEST_EVIDENCE.md` and all unrelated working-tree changes untouched.
- Run each new query-count test red before changing its production service.

---

## Task 1: Add a reusable SELECT counter for tests

**Files:**

- Create: `QuanLyHangHoa.Tests/Helpers/SelectCommandCounter.cs`
- Modify: `QuanLyHangHoa.Tests/Helpers/DatabaseHelper.cs`
- Test: `QuanLyHangHoa.Tests/Helpers/SelectCommandCounter.cs`

- [ ] **Step 1: Add interceptor support to the existing context helper**

Add `using Microsoft.EntityFrameworkCore.Diagnostics;`, keep the current call sites valid, and extend the helper with an optional interceptor list:

```csharp
public static AppDbContext CreateContext(
    SqliteConnection connection,
    params IInterceptor[] interceptors)
{
    // existing PRAGMA block remains unchanged
    var builder = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection);

    if (interceptors.Length > 0)
        builder.AddInterceptors(interceptors);

    return new AppDbContext(builder.Options);
}
```

- [ ] **Step 2: Add the smallest shared counter**

Create a test-only `SelectCommandCounter : DbCommandInterceptor` that:

- increments with `Interlocked.Increment` in both synchronous and asynchronous reader hooks;
- counts only commands whose trimmed text starts with `SELECT` (case-insensitive);
- exposes `Count` through `Volatile.Read` and a `Reset()` method.

- [ ] **Step 3: Compile the helper change**

Run:

```powershell
dotnet test QuanLyHangHoa.Tests/QuanLyHangHoa.Tests.csproj --no-restore --filter "FullyQualifiedName~StockInServiceTests" -m:1 -nr:false -p:UseSharedCompilation=false
```

Expected: existing stock-in tests pass; no production behavior changed.

- [ ] **Step 4: Commit the test infrastructure**

```powershell
git add -- QuanLyHangHoa.Tests/Helpers/DatabaseHelper.cs QuanLyHangHoa.Tests/Helpers/SelectCommandCounter.cs
git commit -m "test: add SELECT query counter"
```

---

## Task 2: Batch stock-in and stock-out posting lookups

**Files:**

- Modify: `QuanLyHangHoa.Tests/Services/StockInServiceTests.cs`
- Modify: `QuanLyHangHoa.Tests/Services/StockOutServiceTests.cs`
- Modify: `QuanLyHangHoa/Services/StockInService.cs`
- Modify: `QuanLyHangHoa/Services/StockOutService.cs`

- [ ] **Step 1: Write red stock-in query-count test**

Add `Post_query_count_does_not_grow_per_line` to `StockInServiceTests`. Use two fresh SQLite in-memory databases and a local measurement function that seeds 1 versus 6 distinct serial-tracked products, saves/approves drafts before attaching/resetting the counter, posts the document, and returns the SELECT count. Assert the 6-line count is at most the 1-line count plus 2.

- [ ] **Step 2: Run the stock-in test and confirm the N+1 failure**

```powershell
dotnet test QuanLyHangHoa.Tests/QuanLyHangHoa.Tests.csproj --no-restore --filter "FullyQualifiedName~StockInServiceTests.Post_query_count_does_not_grow_per_line" -m:1 -nr:false -p:UseSharedCompilation=false
```

Expected: FAIL because product and existing-serial lookups currently execute inside the line loop.

- [ ] **Step 3: Batch stock-in products and serials**

In the posting stage:

1. collect distinct `ProductId` values and all non-empty serial numbers from the loaded lines;
2. load products once and index by `Id`;
3. load existing serials once and index/group by serial number;
4. replace `db.Products.Find(...)`, per-line serial queries, and navigation fallback reads with dictionary/lookup access;
5. preserve the same missing-product, duplicate-serial, serial-count, and tracking validations.

- [ ] **Step 4: Run stock-in tests green**

Run the focused command from Step 2, then:

```powershell
dotnet test QuanLyHangHoa.Tests/QuanLyHangHoa.Tests.csproj --no-restore --filter "FullyQualifiedName~StockInServiceTests" -m:1 -nr:false -p:UseSharedCompilation=false
```

Expected: query-count and existing stock-in tests pass.

- [ ] **Step 5: Write red stock-out query-count test**

Add the same 1-versus-6-line test shape to `StockOutServiceTests`. Seed stock balances and in-stock serials, finish draft/approval setup before resetting the counter, then measure only posting. Assert growth is at most 2 SELECTs.

- [ ] **Step 6: Run the stock-out test and confirm the N+1 failure**

```powershell
dotnet test QuanLyHangHoa.Tests/QuanLyHangHoa.Tests.csproj --no-restore --filter "FullyQualifiedName~StockOutServiceTests.Post_query_count_does_not_grow_per_line" -m:1 -nr:false -p:UseSharedCompilation=false
```

Expected: FAIL because product and serial resolution currently query per line.

- [ ] **Step 7: Batch stock-out products and serials**

Load all required products and serials once before validation. Use an ordinal serial-number dictionary and line-local filtered lists from memory. Preserve warehouse/status/product checks and all current errors.

- [ ] **Step 8: Run stock-out tests green**

Run the focused command from Step 6, then the whole `StockOutServiceTests` class.

- [ ] **Step 9: Commit stock posting changes**

```powershell
git add -- QuanLyHangHoa/Services/StockInService.cs QuanLyHangHoa/Services/StockOutService.cs QuanLyHangHoa.Tests/Services/StockInServiceTests.cs QuanLyHangHoa.Tests/Services/StockOutServiceTests.cs
git commit -m "perf: batch stock posting lookups"
```

---

## Task 3: Batch transfer posting and reversal balances

**Files:**

- Modify: `QuanLyHangHoa.Tests/Inventory/StockTransferAndConcurrencyTests.cs`
- Modify: `QuanLyHangHoa.Tests/Services/StockReversalServiceTests.cs`
- Modify: `QuanLyHangHoa/Services/StockTransferService.cs`
- Modify: `QuanLyHangHoa/Services/StockReversalService.cs`

- [ ] **Step 1: Write and run the red transfer query-count test**

Add `PostTransfer_query_count_does_not_grow_per_line`. Measure posting for 1 versus 6 distinct products after submit/approve setup and assert growth is at most 2 SELECTs.

```powershell
dotnet test QuanLyHangHoa.Tests/QuanLyHangHoa.Tests.csproj --no-restore --filter "FullyQualifiedName~StockTransferAndConcurrencyTests.PostTransfer_query_count_does_not_grow_per_line" -m:1 -nr:false -p:UseSharedCompilation=false
```

Expected: FAIL because `db.Products.Find(line.ProductId)` runs once per line.

- [ ] **Step 2: Batch transfer products**

Collect distinct product IDs before the validation loop, load them with one `Contains` query, index by ID, and preserve the missing-product and serial quantity checks.

- [ ] **Step 3: Run transfer tests green**

Run the focused test, then:

```powershell
dotnet test QuanLyHangHoa.Tests/QuanLyHangHoa.Tests.csproj --no-restore --filter "FullyQualifiedName~StockTransferAndConcurrencyTests|FullyQualifiedName~StockTransferSerialBaseQuantityTests" -m:1 -nr:false -p:UseSharedCompilation=false
```

- [ ] **Step 4: Write and run the red reversal query-count test**

Add `ReverseDocument_query_count_does_not_grow_per_movement` to `StockReversalServiceTests`. Seed a posted stock-in with ledgers and balances for 1 versus 6 products, reset the counter immediately before `ReverseDocumentAsync`, and assert growth is at most 2 SELECTs.

```powershell
dotnet test QuanLyHangHoa.Tests/QuanLyHangHoa.Tests.csproj --no-restore --filter "FullyQualifiedName~StockReversalServiceTests.ReverseDocument_query_count_does_not_grow_per_movement" -m:1 -nr:false -p:UseSharedCompilation=false
```

Expected: FAIL because each grouped movement calls `StockBalances.SingleOrDefault`.

- [ ] **Step 5: Batch reversal balances**

Collect distinct product and warehouse IDs from `movements`, load the candidate balances in one query, index them by `(ProductId, WarehouseId)`, and use the dictionary inside the movement loop. Keep quantity checks and balance mutations unchanged.

- [ ] **Step 6: Run reversal tests green**

Run the focused test, then:

```powershell
dotnet test QuanLyHangHoa.Tests/QuanLyHangHoa.Tests.csproj --no-restore --filter "FullyQualifiedName~StockReversalServiceTests|FullyQualifiedName~StockReversalIntegrityTests" -m:1 -nr:false -p:UseSharedCompilation=false
```

- [ ] **Step 7: Commit transfer and reversal changes**

```powershell
git add -- QuanLyHangHoa/Services/StockTransferService.cs QuanLyHangHoa/Services/StockReversalService.cs QuanLyHangHoa.Tests/Inventory/StockTransferAndConcurrencyTests.cs QuanLyHangHoa.Tests/Services/StockReversalServiceTests.cs
git commit -m "perf: batch transfer and reversal lookups"
```

---

## Task 4: Batch dynamic-import master-data lookups

**Files:**

- Modify: `QuanLyHangHoa.Tests/Services/DynamicImportWriteSafetyTests.cs`
- Modify: `QuanLyHangHoa/Services/DataImport/DynamicImportService.cs`

- [ ] **Step 1: Add a shared measurement helper inside the existing test class**

Reuse `OpenDatabase`, existing mappings/row builders, and `SelectCommandCounter`. The helper must create a fresh database per measurement, attach the counter only to the service context factory, and return the SELECT count for one import call.

- [ ] **Step 2: Write red category and product import tests**

Add:

- `Category_import_query_count_does_not_grow_per_row`
- `Product_import_query_count_does_not_grow_per_row`

Each compares 1 versus 12 rows and allows at most 2 additional SELECTs. The product case must include repeated and newly auto-created category/brand/unit names to prove in-memory maps are updated after creation.

- [ ] **Step 3: Run the tests and confirm failure**

```powershell
dotnet test QuanLyHangHoa.Tests/QuanLyHangHoa.Tests.csproj --no-restore --filter "FullyQualifiedName~DynamicImportWriteSafetyTests.Category_import_query_count_does_not_grow_per_row|FullyQualifiedName~DynamicImportWriteSafetyTests.Product_import_query_count_does_not_grow_per_row" -m:1 -nr:false -p:UseSharedCompilation=false
```

Expected: FAIL because category upsert and product/reference resolution query inside row loops.

- [ ] **Step 4: Batch category upserts**

Load all matching category codes once, create an ordinal-code dictionary, update existing tracked entities, and insert missing categories while updating the dictionary immediately.

- [ ] **Step 5: Batch product and reference resolution**

Before the product loop, load requested products, categories, brands, and units once into case-consistent dictionaries. Resolve each row from memory. When `autoCreateReferences` adds an entity, save only where its generated ID is required by the current behavior, then insert it into the matching dictionary so later rows never query it again.

- [ ] **Step 6: Batch product-serial note updates**

For the operation document prefix, load all requested serials once, index by serial number, and update notes from the prepared rows in memory.

- [ ] **Step 7: Batch replay verification for master data and serials**

In `HasCommittedImportAsync`, replace per-row `AnyAsync`/serial queries for category, product, and product serial cases with one projection query per case and set/dictionary comparisons in memory. Preserve exact-payload replay decisions.

- [ ] **Step 8: Run focused and existing dynamic-import tests**

Run the focused command from Step 3, then:

```powershell
dotnet test QuanLyHangHoa.Tests/QuanLyHangHoa.Tests.csproj --no-restore --filter "FullyQualifiedName~DynamicImportWriteSafetyTests" -m:1 -nr:false -p:UseSharedCompilation=false
```

- [ ] **Step 9: Commit master-data import changes**

```powershell
git add -- QuanLyHangHoa/Services/DataImport/DynamicImportService.cs QuanLyHangHoa.Tests/Services/DynamicImportWriteSafetyTests.cs
git commit -m "perf: batch import reference lookups"
```

---

## Task 5: Batch dynamic-import document and invoice lookups

**Files:**

- Modify: `QuanLyHangHoa.Tests/Services/DynamicImportWriteSafetyTests.cs`
- Modify: `QuanLyHangHoa/Services/DataImport/DynamicImportService.cs`

- [ ] **Step 1: Write red transactional import query-count tests**

Add theory-backed coverage for:

- stock-in documents;
- stock-out documents;
- purchase invoices;
- sales invoices.

For each import type, compare 1 versus 8 valid lines/rows in fresh databases and allow at most 3 additional SELECTs. Use existing row/mapping builders where available; add only the minimal missing builders.

- [ ] **Step 2: Run the tests and confirm linear growth**

```powershell
dotnet test QuanLyHangHoa.Tests/QuanLyHangHoa.Tests.csproj --no-restore --filter "FullyQualifiedName~DynamicImportWriteSafetyTests.Transactional_import_query_count_does_not_grow_per_row" -m:1 -nr:false -p:UseSharedCompilation=false
```

Expected: FAIL for the current per-row product/unit/serial/balance/party lookups.

- [ ] **Step 3: Batch stock-in document data**

Per prepared batch/document group, preload requested warehouses, suppliers, products, units, and existing serial numbers into dictionaries/sets. Resolve every line from those structures and update them when auto-create is used. Do not change grouping, posting, or error aggregation.

- [ ] **Step 4: Batch stock-out document data**

Preload warehouses, customers, products, units, requested serials, and candidate balances. Index balances by `(ProductId, WarehouseId)` and serials by serial number. Keep the aggregate available-quantity check per product, but make it dictionary-only.

- [ ] **Step 5: Batch purchase and sales invoice data**

For each prepared invoice batch, preload parties, products, and units once. Resolve header and line references from dictionaries. Preserve totals, payment validation, replay protection, and atomic rollback behavior.

- [ ] **Step 6: Batch replay verification for documents and invoices**

Replace any remaining per-row database calls in `HasCommittedImportAsync` with one projected query per import type plus in-memory comparisons. Verify the same document code, row count, quantities, totals, serials, and operation prefix as before.

- [ ] **Step 7: Prove no query remains inside an import row/serial loop**

Inspect `DynamicImportService.cs` and confirm every `foreach`/group loop uses preloaded tracked collections only. A database query may occur before a loop, never once per input row or serial.

```powershell
rg -n -C 5 "foreach|AnyAsync|FirstOrDefault|SingleOrDefault|Find\(" QuanLyHangHoa/Services/DataImport/DynamicImportService.cs
```

Expected: no EF query call is executed inside a row, line, product-group, or serial loop.

- [ ] **Step 8: Run the complete dynamic-import test class**

```powershell
dotnet test QuanLyHangHoa.Tests/QuanLyHangHoa.Tests.csproj --no-restore --filter "FullyQualifiedName~DynamicImportWriteSafetyTests" -m:1 -nr:false -p:UseSharedCompilation=false
```

Expected: all query-count, replay, cancellation, rollback, payment, and atomicity tests pass.

- [ ] **Step 9: Commit transactional import changes**

```powershell
git add -- QuanLyHangHoa/Services/DataImport/DynamicImportService.cs QuanLyHangHoa.Tests/Services/DynamicImportWriteSafetyTests.cs
git commit -m "perf: batch transactional import lookups"
```

---

## Task 6: Full regression verification

**Files:**

- Verify only; modify production/tests only if a regression directly caused by Tasks 1-5 is found.

- [ ] **Step 1: Run all tests without restoring**

```powershell
dotnet test QuanLyHangHoa/QuanLyHangHoa.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RestoreBuildInParallel=false
```

Expected: all non-skipped tests pass; no new skipped/representative test is added.

- [ ] **Step 2: Build the solution**

```powershell
dotnet build QuanLyHangHoa/QuanLyHangHoa.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RestoreBuildInParallel=false
```

Expected: build succeeds with zero errors.

- [ ] **Step 3: Review exact scope**

```powershell
git status --short
git diff --check
git diff --stat HEAD~5..HEAD
```

Expected: only the five services, their existing tests, the test helper, and approved docs are changed; `TEST_EVIDENCE.md` remains untracked and untouched.

- [ ] **Step 4: Record final evidence**

Report focused query-count results, total test result, build result, and any pre-existing warnings. Do not claim visual/UI changes because this task has none.
