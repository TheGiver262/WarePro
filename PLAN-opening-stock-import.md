# PLAN: Opening Stock / General File Import with Dynamic Column Mapping

## 1. Overview
This plan outlines the design and implementation of an intelligent, multi-entity Excel/CSV import system. When a user uploads a file inside the **Nhập Tồn Đầu Kỳ** tab, the system will:
1. Parse the file headers.
2. Automatically analyze the file content to detect the target database entity (e.g., Products, Serials, Stock In/Out, Purchase/Sales Invoices).
3. Display a localized column mapping interface allowing the user to map document headers to database columns.
4. Provide fuzzy pre-matching for headers.
5. Parse, validate, and batch-upsert data into the database.

---

## 2. Project Type
- **Type:** WEB/WPF (Desktop App)
- **Primary Agent:** `frontend-specialist` (UI/UX layout and mapping views) & `backend-specialist` (Excel parsing, type prediction, mapping, and database transactional upserts).

---

## 3. Success Criteria
- [ ] Users can browse and upload `.xlsx`, `.xls`, or `.csv` files.
- [ ] The app accurately predicts the file type (Product, Category, Serial, StockIn, StockOut, PurchaseInvoice, SalesInvoice) based on header similarity.
- [ ] Users can override the predicted type using a dropdown.
- [ ] The column mapping UI lists database fields in Vietnamese with a dropdown of Excel/CSV headers, automatically pre-selecting matches based on fuzzy string matching.
- [ ] A preview panel shows the first 5 parsed records according to the mapping.
- [ ] Validation errors (e.g., missing required fields, parsing failures) are shown inline or in a separate tab.
- [ ] Clicking "Xác nhận" successfully performs a transactional batch import/upsert into the database.
- [ ] Fully verified by compiling successfully and passing all tests.

---

## 4. Tech Stack & Libraries
- **Language/Framework:** C# / .NET 8 / WPF
- **Excel/CSV Parsing:** ClosedXML (Excel) and CsvHelper (CSV) (already integrated in the project).
- **MVVM Pattern:** CommunityToolkit.Mvvm (already integrated).

---

## 5. File Structure & Changes

### [NEW] Services
- [NEW] `Services/DataImport/FileClassificationService.cs` - Classifies file types based on header sets.
- [NEW] `Services/DataImport/DynamicImportService.cs` - Generic mapping and validation engine that reads rows, maps properties, and performs DB updates.

### [MODIFY] Views & ViewModels
- [MODIFY] [OpeningBalanceImportView.xaml](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Views/OpeningBalanceImportView.xaml) - Rework UI to support the mapping dropdowns, preview grid, and validation messages.
- [MODIFY] [OpeningBalanceImportViewModel.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/ViewModels/OpeningBalanceImportViewModel.cs) - Incorporate classification, mapping logic, preview state, and import commands.

---

## 6. Task Breakdown

### Phase 1: Core Services & Logic (P1 Backend)
- **Task 1.1: File Classification Logic**
  - Implement header scoring algorithm using typical alias dictionaries.
  - *Verify:* Unit tests with mock headers for all 7 target types.
- **Task 1.2: Dynamic Mapping & Converter**
  - Implement a mapping converter that maps string headers to target object properties (handling conversions for Nullables, decimals, integers, and DateTimes).
  - *Verify:* Test mapping dictionary conversion works for sample rows.
- **Task 1.3: Relational Resolving & Transactional Upsert**
  - Handle entity-specific relationships (e.g. finding Category by Name and mapping to CategoryId, or creating it on-the-fly).
  - Use EF Core transactions to insert/update the matched records.
  - *Verify:* Mock database integration test for upserting products/serials.

### Phase 2: UI/UX Redesign & ViewModels (P2 UI/UX)
- **Task 2.1: ViewModel Mapping & Preview State**
  - Update `OpeningBalanceImportViewModel` with properties: `ImportTypes` list, `SelectedImportType`, `ColumnMappings` list, `ExcelHeaders` list, `ParsedPreview` collection.
  - Bind `BrowseFileCommand` to classification logic, setting ExcelHeaders and triggering initial fuzzy matching.
- **Task 2.2: Redesign View with Wizard Layout**
  - Step 1: File Selection & Classification Confirmation.
  - Step 2: Mapping Grid (Target Db Field [Vietnamese] <-> Dropdown of file headers).
  - Step 3: Preview DataGrid (Display mapped data, highlighting validation errors).
  - Step 4: Import execution and success/error reporting.
- **Task 2.3: Spacing & Styling Polish**
  - Ensure margins, colors (AppBackgroundBrush, SurfaceMutedBrush), and typography styles strictly follow typography guidelines.

### Phase 3: Verification & Polish (P3 Polish)
- **Task 3.1: Integration Verification**
  - Compile the project successfully.
  - Execute automated tests to guarantee no regressions.

---

## 7. Phase X: Verification Plan

### Automated Checks
- [ ] Run `dotnet build` to confirm zero compilation errors.
- [ ] Run `dotnet test` to confirm all 83 existing tests and new tests pass.

### Manual Verification
- [ ] Upload an Excel file with random headers containing product information, map them manually, and verify the database is successfully updated with the new products.
