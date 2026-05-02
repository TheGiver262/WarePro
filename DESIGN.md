# DESIGN SYSTEM: ANTIGRAVITY LOGISTICS

> Version: 1.1.0
> Last Updated: 2026-05-01
> Status: Production-Ready (Safe, shipping-ready, infinitely remixable)

---

# 1. Metadata

```yaml
version: 1.1.0
name: Antigravity Logistics Design System
description: "Safe, shipping-ready, infinitely remixable. Neutral slate# Software Design Document - Antigravity Pro Max Kit

# 1. Core Philosophy: Inventory & Warranty Focused

The system is optimized for **Stock Control** and **Warranty Lifecycle Tracking**. Financial liability tracking (Debt, Partial Payments) is intentionally removed to keep the system lightweight and specialized.

### 1.1 Pay-in-Full Model
- All transactions (Sales/Purchase) are assumed "Paid in Full" at the moment of document creation.
- No partial payment tracking or debt management logic.
- Total amounts include taxes and sub-totals, but payments are handled externally or implicitly as full payments.

### 1.2 Auditability over Accounting
- The `AuditLog` and `StockLedger` are the primary sources of truth for document history and stock movements.
- `Transactions` table tracks document changes for accountability, not financial balancing.
- Minimum commercial details (prices, tax) are kept for record-keeping and inventory valuation only.
"
```

---

# 2. Color System (XAML Tokens)

Hệ thống màu sắc tập trung vào độ tương phản cao của các màu trung tính và một màu nhấn duy nhất để thúc đẩy tương tác.

### 2.1 Core Brushes
| Token | Hex | Usage |
| :--- | :--- | :--- |
| `AppPrimaryBrush` | `#0F172A` | Slate 900. Headlines, core text, sidebar. |
| `AppSecondaryBrush` | `#64748B` | Slate 500. Borders, captions, metadata. |
| `AppTertiaryBrush` | `#4F46E5` | Indigo 600. **The sole driver for interaction.** |
| `AppSurfaceBrush` | `#FFFFFF` | White. Card backgrounds, dialog surfaces. |
| `AppBackgroundBrush` | `#F1F5F9` | Slate 100. The page foundation. |
| `AppBorderBrush` | `#E2E8F0` | Default border color for inputs. |
| `AppDividerBrush` | `#CBD5E1` | Subtle separation lines. |

### 2.2 Semantic Brushes
| Token | Hex | Usage |
| :--- | :--- | :--- |
| `AppSuccessBrush` | `#10B981` | Success states, positive stock. |
| `AppWarningBrush` | `#F59E0B` | Warning states, low stock. |
| `AppErrorBrush` | `#EF4444` | Errors, negative stock, deletions. |
| `AppInfoBrush` | `#3B82F6` | Info banners, help text. |

### 2.3 Text Brushes
| Token | Hex | Usage |
| :--- | :--- | :--- |
| `TextPrimaryBrush` | `#0F172A` | Main headings, body text. |
| `TextSecondaryBrush` | `#64748B` | Labels, captions, secondary info. |
| `TextDisabledBrush` | `#94A3B8` | Disabled text, placeholders. |
| `TextOnPrimaryBrush` | `#FFFFFF` | Text on top of primary/tertiary colors. |

---

# 3. Typography System

Sử dụng font **Inter** cho toàn bộ hệ thống để đảm bảo tính hiện đại và khả năng đọc tuyệt vời.

| Role | Font Family | Size (px) | Weight | LetterSpacing |
| :--- | :--- | :--- | :--- | :--- |
| `Display` | Inter | 60px | 700 | -0.03em |
| `H1` | Inter | 36px | 700 | -0.02em |
| `H2` | Inter | 24px | 600 | -0.01em |
| `Body` | Inter | 15.2px | 400 | 0 |
| `Label` | Inter | 12px | 600 | 0.02em |
| `Caption` | Inter | 11px | 400 | 0 |
| `DataMono` | Fira Code | 12px | 500 | 0 |

---

# 4. Spacing System (Thickness)

| Token | Value (px) | Usage |
| :--- | :--- | :--- |
| `Spacing.sm` | 8 | Small gaps, internal padding. |
| `Spacing.md` | 16 | Standard gaps, component margins. |
| `Spacing.lg` | 32 | Large gaps, page margins. |

---

# 5. Radius System (CornerRadius)

| Token | Value (px) | Usage |
| :--- | :--- | :--- |
| `Radius.sm` | 4 | Inputs, checkboxes, badges. |
| `Radius.md` | 8 | Standard buttons, small cards. |
| `Radius.lg` | 12 | Large cards, dialogs, main containers. |

---

# 6. Layout System (WPF Grid Patterns)

### 6.1 Breakpoints
- **Standard:** 1024px - 1440px (Default).
- **Wide:** > 1440px.

### 6.2 View Densities
| View Type | Density | Row Height | Grid Padding |
| :--- | :--- | :--- | :--- |
| `Dashboard` | Medium | Auto | 32px (Spacing.lg) |
| `Forms` | Medium | 36px | 16px (Spacing.md) |
| `DataTables` | **High** | 28px | 8px (Spacing.sm) |

---

# 7. Elevation & Surface Rules

- **Flat Design:** Hệ thống này ưu tiên thiết kế phẳng (Flat). **Cấm sử dụng Gradients.**
- **Shadow Policy:** Chỉ dùng bóng đổ rất nhẹ cho các thành phần nổi bật (Dp1 hoặc Dp2).
- **Surface Hierarchy:** Sử dụng màu `AppBackgroundBrush` làm nền trang và `AppSurfaceBrush` cho các Card.

---

# 8. Motion System

- **Fast:** 150ms (Hover, Toggle).
- **Standard:** 250ms (Dialogs).
- **Easing:** `CubicEase Out`.

---

# 9. Iconography System

- **Provider:** MaterialDesign PackIcon.
- **Style:** Outline/Minimal.
- **Rules:** Sử dụng `AppSecondaryBrush` cho icon mặc định, `AppTertiaryBrush` cho icon hành động.

---

# 10. Illustration & Visual Assets

- **Style:** Minimalist vector.
- **Palette:** Sử dụng bảng màu Slate/Indigo.

---

# 11. Accessibility Rules

- **Contrast:** Đảm bảo WCAG AA (4.5:1).
- **Focus:** 2px border sử dụng `AppTertiaryBrush`.

---

# 12. Responsive Rules

- Sidebar tự động thu gọn.
- Forms chuyển sang 1 cột khi không gian hẹp.

---

# 13. Canonical Components (WPF/XAML)

### 13.1 Buttons
- `PrimaryButton`:
  - Background: `{StaticResource AppTertiaryBrush}`
  - Text: `{StaticResource TextOnPrimaryBrush}`
  - CornerRadius: `{StaticResource Radius.md}`
  - Padding: `12,20`
- `SecondaryButton`:
  - Background: `Transparent`
  - Border: `{StaticResource AppSecondaryBrush}`
  - Text: `{StaticResource AppSecondaryBrush}`

### 13.2 Card
- `AppCard`:
  - Background: `{StaticResource AppSurfaceBrush}`
  - CornerRadius: `{StaticResource Radius.lg}`
  - Padding: `{StaticResource Spacing.lg}` (24px - user request: 24px)
  - Border: 1px `{StaticResource AppBorderBrush}`

---

# 14. Page Archetypes

- **Dashboard:** Neutral slate foundation, high whitespace, indigo accents for primary KPIs.
- **Inventory Grid:** High density, zebra striping, focused actions.

---

# 15. Design Constraints (DO'S & DON'TS)

- **DO** use Tertiary (`AppTertiaryBrush`) for exactly **one action** per screen.
- **DO** let Neutral carry the composition — negative space is a feature.
- **DON'T** introduce gradients. This system is flat on purpose.
- **DON'T** mix Tertiary with alternate accents; the single-accent rule is load-bearing.
- **CẤM** sử dụng các giá trị spacing/radius không nằm trong hệ thống token.

---

# 16. AI Agent Rules (ANTIGRAVITY PROTOCOL)

1.  **Read Before Code:** Đọc `DESIGN.md` trước khi viết XAML.
2.  **Single Accent Rule:** Luôn kiểm tra xem chỉ có duy nhất 1 hành động chính sử dụng màu Indigo không.
3.  **Flat Check:** Loại bỏ mọi gradient hoặc hiệu ứng bóng đổ phức tạp.
4.  **Token-First:** 100% sử dụng StaticResource.

---

# 17. Visual Consistency Policies

- **Whitespace Policy:** Sử dụng `Spacing.lg` (32px) cho lề trang chính.
- **Density Policy:** Inventory View buộc phải dùng mật độ cao (`RowHeight: 28px`).

---

# 18. Anti-Patterns (CẤM)

- **Color Hallucination:** Dùng các màu sắc ngoài bảng màu Slate/Indigo đã định nghĩa.
- **UI Drift:** Tự ý thay đổi bán kính bo góc (CornerRadius).
- **Over-crowding:** Thiếu không gian trống (Negative space).

---

# 19. Token Referencing Rules

```yaml
Style: "StandardButton"
Background: "{Colors.AppTertiaryBrush}"
CornerRadius: "{Radius.md}"
Padding: "{Spacing.md}"
```

---

# 20. Output Requirements

Thiết kế phải mang lại cảm giác: **"Safe, shipping-ready, infinitely remixable."**
- Tin cậy (Safe)
- Sẵn sàng xuất xưởng (Shipping-ready)
- Dễ dàng tùy biến lại (Infinitely remixable)

