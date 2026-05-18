# CLAUDE.md - Root Context for ProductManagement

Global guidelines and context routing map for AI coding agents.

## Context Routing

When working on specific sub-components, immediately load the relevant subdirectory context file:
- Views (WPF UI/XAML): [QuanLyHangHoa/Views/CLAUDE.md](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Views/CLAUDE.md)
- ViewModels (C# MVVM): [QuanLyHangHoa/ViewModels/CLAUDE.md](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/ViewModels/CLAUDE.md)

## Core Development Standards

- **Aesthetics & UI**: Strictly follow the Pro Max design standard (3-row layout, glassmorphism, HSL tailormade colors).
- **Purple Ban**: NEVER use standard purple or violet colors/accents in any UI styling.
- **WPF Compilation**: Run `dotnet build` from the workspace root to compile the solution.
- **Interactive Memory (Option B)**: Newly inferred decisions/preferences must be written to [.memory/inbox.md](file:///f:/Codex%20Project/ProductManagement_Antigravity/.memory/inbox.md) first for user review.

## Commands

- Build Solution: `dotnet build`
- Run Tests: `dotnet test`
