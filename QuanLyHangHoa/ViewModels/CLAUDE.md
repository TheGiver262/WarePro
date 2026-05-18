# ViewModels Context - ProductManagement

Guidelines for designing and maintaining C# MVVM ViewModels in the `QuanLyHangHoa` namespace.

## Core Rules

- **Designer Safety**: Always ensure ViewModels have parameterless constructors or safety checks (such as checking `DesignerProperties.GetIsInDesignMode`) to prevent NullReferenceException during XAML design time.
- **Filter Reset Pattern**:
  - Reset filters should ONLY clear keywords and search terms.
  - NEVER automatically collapse or close the advanced filter panel (keep `IsAdvancedFilterOpen` state unchanged).
- **Excel Export Protocol**:
  - Always export the exact data currently displayed on screen (loop through the bound ObservableCollection, e.g., `InventoryItems` or `Products` in memory).
  - Use `XLWorkbook` and `XLWorksheet` from ClosedXML.
  - Do NOT query the database again during export.
  - Apply the standardized anthracite header color (`#4A5568`) with bold white text.

## Related Files

- Models & Services: [QuanLyHangHoa/Models/](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Models/)
- Decisions log: [.memory/decisions.md](file:///f:/Codex%20Project/ProductManagement_Antigravity/.memory/decisions.md)
