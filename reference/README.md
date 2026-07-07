# Reference material

This folder holds **read-only reference** code and docs — not part of the active app build.

| Path | Purpose |
|------|---------|
| `WiiBalanceWalker/` | Original v0.5 WinForms source (MS-PL). Behavioral reference for pairing PIN, input bindings, and balance math. |

## Active app

Build and run **`BalanceBoard.sln`** at the repo root. Native DLLs live in **`libs/x64/`** (shared by the modern app and the legacy project if you open it for comparison).

## Do not commit here

Loose `.exe`, `.zip`, or duplicate `.dll` files — use `libs/x64/` instead. The `baseline/` folder at repo root is for local scratch comparisons (gitignored).
