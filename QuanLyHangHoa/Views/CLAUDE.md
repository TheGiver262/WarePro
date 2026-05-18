# Views Context - ProductManagement

Guidelines for WPF UI development, styling, and standardizing views under the Pro Max Design System.

## Standard Layout (3-Row Architecture)

Main list screens must follow a consistent 3-row layout inside a Grid:
1. **Row 0 (Header Area)**: Page title, add new action button, export button.
2. **Row 1 (Filter Card)**: Search input, filter toggle button, advanced filter expandable card (holds fields like Category, Status, dates).
3. **Row 2 (DataGrid)**: Dynamic grid holding items list, standardized with thin columns and optimized data alignment.

## UI Styling Standards

- **Wide-Monitor Scaling**: Keep `MaxWidth="1600"` and `HorizontalAlignment="Stretch"` on the main container Grid or Border to ensure beautiful rendering on ultra-wide screens.
- **Icon Buttons**: Standardize control buttons (Search, Refresh, Clear, Add) to use the `ProMaxIconButtonStyle` style.
- **Purple Ban**: Absolutely NO purple/violet accents. Use slate, anthracite, navy, and soft emerald for status cues.
- **DataGrid Styling**:
  - Centralize headers using `SurfaceMutedBrush` in `Tables.xaml`.
  - Remove redundant inline properties (`Background="Transparent"`, `BorderThickness="0"`) to let the main application theme shine.

## Related Files

- WPF Styles: [QuanLyHangHoa/Themes/Buttons.xaml](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Themes/Buttons.xaml)
- Table Styles: [QuanLyHangHoa/Themes/Tables.xaml](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Themes/Tables.xaml)
