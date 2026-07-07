# Update 012 — Fix CornerRadius startup crash

| Field | Value |
|-------|-------|
| Commit | *(set after commit)* |
| Date | 2026-07-07 |
| Branch | `main` |

## What was done

- **Fixed startup crash:** `Radius.Card` / `Radius.Button` were `sys:Double` but `Border.CornerRadius` requires `CornerRadius` struct — caused the dialog *"Set property CornerRadius threw an exception"*
- Verified: app launches and stays running (no immediate exit)
- Re-ran: `dotnet build -warnaserror`, `dotnet format`, `Validate`, `test-flow.ps1`

## Root cause

`Themes/Colors.xaml` used `<sys:Double x:Key="Radius.Card">` bound to `Border.CornerRadius` in Card style, StatusChip, BalanceBoardVisual, and button template.

## Not done / left for later

- Automated UI/XAML load test in CI (headless WPF is awkward on GitHub Actions)
