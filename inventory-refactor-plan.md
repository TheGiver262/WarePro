# Inventory Refactor Plan

## Overview
Refactor the Inventory Management System to align with the design baseline (`Thiết kế phần mềm.md`).

## Project Type: WEB/MOBILE/BACKEND?
**Project Type**: DESKTOP (WPF/C#)

## Success Criteria
1. Database schema matches `database_schema.sql`.
2. StockBalance is the single source of truth for quantity.
3. Serial tracking is implemented and unique system-wide.
4. Role-based access control (Admin, Manager, Staff) is active.
5. Unit conversion logic correctly updates stock in base units.

## Tech Stack
- **Language**: C# 12
- **Framework**: WPF (.NET 8/9)
- **Database**: SQLite (Local)
- **ORM**: Entity Framework Core
- **UI**: Material Design in XAML
- **Auth**: BCrypt.Net-Next

## File Structure (Planned)
- `QuanLyHangHoa/Models/`: Entity models synchronized with schema.
- `QuanLyHangHoa/Services/`: Business logic (StockService, AuthService, SerialService).
- `QuanLyHangHoa/ViewModels/`: MVVM ViewModels.
- `QuanLyHangHoa/Views/`: WPF XAML Views.
- `Database/`: SQLite file location.

## Task Breakdown

| Task ID | Name | Agent | Skills | Priority | Dependencies | INPUT -> OUTPUT -> VERIFY |
|---------|------|-------|--------|----------|--------------|---------------------------|
| T1 | Sync Database Schema | database-architect | database-design | P0 | None | `database_schema.sql` -> Updated `AppDbContext.cs` & Migrations -> Verify tables in SQLite Viewer |
| T2 | Implement Auth Module | security-auditor | clean-code | P1 | T1 | User design -> `AuthService.cs` & Login UI -> Successful login with role check |
| T3 | Implement Core Stock Logic | backend-specialist | clean-code | P1 | T1 | Design rules -> `StockService.cs` -> Unit tests for Increase/Decrease stock |
| T4 | Product & Unit Conversion | backend-specialist | clean-code | P1 | T1 | Design rules -> `ProductUnit` conversion logic -> Verify Piece-to-Box conversion in UI |
| T5 | Serial Tracking Workflow | backend-specialist | clean-code | P2 | T3 | Design rules -> `SerialService.cs` -> Verify serial status lifecycle |
| T6 | Warranty Module | backend-specialist | clean-code | P2 | T5 | Design rules -> `WarrantyService.cs` -> Create coverage on sale |
| T7 | UI Refinement | frontend-specialist | frontend-design | P3 | All | UI requirements -> Polished XAML views -> Visual audit and performance check |

## Phase X: Verification Checklist
- [ ] Database Schema Match: 
- [ ] Auth/Roles Work: 
- [ ] Stock Transaction Integrity: 
- [ ] Serial Uniqueness: 
- [ ] UI Aesthetics (No Purple): 
- [ ] Performance (Fast Load): 
