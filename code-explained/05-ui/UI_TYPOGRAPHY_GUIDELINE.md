# WarePro UI Typography Guideline

## 1. General Principles
The primary goal of the WarePro typography system is to ensure **clarity**, **consistency**, and **visual hierarchy**. This application uses a "Pro Max" design aesthetic, which prioritizes clean, modern typography. Hardcoded font weights are discouraged in favor of centralized styles.

### 1.1 Font Family
- **Primary**: `Segoe UI` (Application Standard)
- **Data/Monospace**: `Consolas` (For numeric codes, IDs, and serial numbers where alignment matters)

---

## 2. Font Weight Hierarchy
To avoid visual clutter, font weights must be used sparingly.

| Element | Font Weight | Style Reference |
| :--- | :--- | :--- |
| **Page Titles** | **Bold (700)** | `TypographyH1` |
| **Main Section Headers** | **SemiBold (600)** | `TypographyH2`, `TypographyH3` |
| **Dashboard Metrics** | **Bold (700)** | `TypographyStatNumber` |
| **Buttons (Primary/Action)**| **SemiBold (600)** | `TypographyLabel` |
| **Search Panel Labels** | **Normal (400)** | `TypographyCaption` or `LabelText` |
| **DataGrid Headers** | **Normal (400)** | `DataGridColumnHeader` |
| **DataGrid Row Data** | **Normal (400)** | `BodyText` |
| **Important IDs in Tables**| **SemiBold (600)** | `AppDataGridTextBold` (Use sparingly) |
| **Totals in Tables** | **SemiBold (600)** | `AppDataGridTextRightBold` |

---

## 3. Case Guidelines
- **UPPERCASE**:
  - Page titles (H1).
  - Main button labels (e.g., "LƯU THAY ĐỔI").
  - Table headers.
  - Sidebar menu items.
- **Title Case**:
  - Field labels (e.g., "Mã hàng hóa", "Số lượng").
  - Tab names.
- **Sentence Case**:
  - Help text, tooltips, and descriptions.

---

## 4. Visual Emphasis Rules
1. **No Hardcoded FontWeight**: Never use `FontWeight="Bold"` or `FontWeight="SemiBold"` directly in `.xaml` view files. If an element needs emphasis, use a predefined Style or update the theme.
2. **Search Panels**: Labels in search panels **must** be `Normal` weight. Bold labels in search panels are strictly forbidden as they distract from the primary content.
3. **DataGrid Consistency**: All data cells should be `Normal` weight by default. Only "Key Columns" (like a primary ID) or "Calculated Totals" may use `SemiBold`. Never use `Bold (700)` inside a DataGrid cell unless it's a critical error status.

---

## 5. Summary Table for Developers

| Type | Style | Weight | Color | Case |
| :--- | :--- | :--- | :--- | :--- |
| Page Title | `TypographyH1` | Bold | Primary | UPPERCASE |
| Section Header | `TypographyH3` | SemiBold | Primary | UPPERCASE |
| Search Label | `TypographyCaption` | Normal | Secondary | UPPERCASE |
| Field Label | `LabelText` | Normal | Secondary | Title Case |
| Table Header | N/A | Normal | Secondary | UPPERCASE |
| Table Data | `BodyText` | Normal | Primary | Normal |
| Summary Metric | `TypographyStatNumber` | Bold | Primary | N/A |

> [!IMPORTANT]
> If you see a bold label in a search area, it is a bug. Change it to `Normal` weight using `TypographyCaption`.
