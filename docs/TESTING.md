# Testing guide

Automated tests for Balance Board Controller run **without user interaction** on Windows x64 with .NET 8.

## Quick start

```powershell
# From repo root — full pipeline (build + all suites)
.\scripts\ci\test.ps1

# PR-fast subset (skips fuzz, slow integration/recovery, slow lifecycle)
.\scripts\ci\test.ps1 -Quick

# Same as test.ps1, alternate entry point
.\scripts\test\run-all.ps1

# Full CI quality gate (format + build + test.ps1)
.\scripts\ci\lint.ps1

# Opt-in physical hardware lane (manual, interactive)
.\src\BalanceBoard.App\bin\Release\net8.0-windows\BalanceBoardApp.exe --physical-test connect-basic
```

Artifacts are written to `artifacts/test/` (gitignored):

| File | Purpose |
|------|---------|
| `test.log` | Human/LLM-readable tagged log (`[SUITE]`, `[PASS]`, `[FAIL]`, `[ARTIFACT]`, `[CONTEXT]`) |
| `summary.json` | Machine-readable pass/fail per suite, durations, failed test names |
| `*.trx` | Per-suite TRX for CI / Azure DevOps parsers |
| `BalanceBoard.Validate.log` | Validate CLI output |

Physical hardware artifacts are written separately under `%AppData%\BalanceBoardApp\artifacts\physical-tests\` so manual runs never mix with CI/default automation output.

## Pipeline layers

| Layer | Project / tool | What it covers |
|-------|----------------|----------------|
| **1 — Unit** | `BalanceBoard.Core.Tests`, `BalanceBoard.Fuzz.Tests` | Pure math, presets, logging, protocol, migrations |
| **2 — Integration** | `BalanceBoard.Integration.Tests` | `BalanceBoardSession`, connect/disconnect, fakes, recovery (slow tests tagged `Category=Slow`) |
| **3 — UI (headless WPF)** | `BalanceBoard.App.Ui.Tests` | Profiles, settings persistence, detail-level visibility, simulated connect, health check |
| **4 — Tools** | `tools/Validate` | vJoy driver/HID diagnostics CLI |
| **5 — Lifecycle** | `BalanceBoard.Automation` | Real process launch, session log creation, and `--simulate-board` smoke |

`tools/UiSmoke` remains as a minimal legacy loader; the unified pipeline uses `BalanceBoard.App.Ui.Tests` instead.

## Physical hardware lane

`--physical-test <scenario>` is an explicit, opt-in app mode for guided manual validation with real hardware. It is not called by `scripts/ci/test.ps1`, `scripts/test/run-all.ps1`, or CI.

Current scenario:

- `connect-basic` — connect, verify live weight, check lean feedback, validate tare/center, and disconnect cleanly.

What the mode does:

- shows an in-app dashboard panel with the current step, instructions, expected signal, and pass/fail/skip controls
- auto-advances a few steps when the app can observe the condition directly, such as connection and live weight
- records structured artifacts per run:
  - `run.json` — scenario metadata, per-step outcomes, timestamps, overall status
  - `events.jsonl` — append-only event stream for starts, completions, and observed state

Recommended launch flow:

```powershell
dotnet build BalanceBoard.sln -c Release
.\src\BalanceBoard.App\bin\Release\net8.0-windows\BalanceBoardApp.exe --physical-test connect-basic
```

## UI test architecture

- **Host:** `WpfTestHost` — shared STA `Application` with the same theme dictionaries as production.
- **Isolation:** `UiTestContext` — temp settings + logs directory via `SettingsStore(baseDir)` and `FileLogService(logsDir)`.
- **Injection:** `MainWindow` accepts optional `SettingsStore` / `BalanceBoardSession` for deterministic tests.
- **No FlaUI / pixels:** tests invoke WPF controls and internal test hooks directly (fast, deterministic).

Respects production guards:

- `_uiReady` — settings saves only after ctor completes
- `_suppressSettingEvents` — bulk UI population does not spam saves

## Adding tests

### Core / integration

1. Add facts under `tests/BalanceBoard.Core.Tests` or `tests/BalanceBoard.Integration.Tests`.
2. Use fakes from `tests/BalanceBoard.Testing` (`FakeBalanceBoardConnection`, `FakeBluetoothPairingService`, `ScriptedBluetoothPairingService` + `ReferenceConnectScenario` for WiiBalanceWalker connect flows).
3. Tag tests that sleep >1s with `[Trait("Category", "Slow")]` so `-Quick` skips them.

### UI

1. Add a fact to `tests/BalanceBoard.App.Ui.Tests`.
2. Inherit `UiTestBase` for temp settings cleanup.
3. Wrap UI work in `WpfTestHost.Invoke(() => { ... })`.
4. Use `Ctx.CreateWindow(...)` and `Ctx.ReadPersistedSettings()` to verify persistence.
5. Prefer `window.Test*` helpers on `MainWindow` over reflection.

### Lifecycle

1. Add facts to `tests/BalanceBoard.Automation`.
2. Spawn `BalanceBoardApp.exe` from Release output (build first).
3. Always clean up processes in `finally` blocks.

## Reading failure logs (LLM-friendly)

On failure, open `artifacts/test/test.log` and search for `[FAIL]`. Each suite also records:

- TRX path (`[ARTIFACT]`)
- Failed test name + message (from TRX parse)
- Last 20 lines of tool output (`[CONTEXT]`)

For UI/session issues, also check the temp log path created by `UiTestContext` (under `%TEMP%\bbc-ui-test-*` during the run).

## Hardware gaps (not automated)

| Area | Why manual |
|------|------------|
| Real Bluetooth pairing / SYNC timing | OS + radio + board state |
| vJoy in specific games | Game-specific input stacks |
| Multiple Nintendo devices in range | Environment-dependent |
| Windows theme contrast spot-checks | Visual QA |

Optional hardware scripts: `tests/hardware/` (run with `scripts/ci/test-all.ps1 -IncludeHardware`).

The new physical lane is the preferred first-party entrypoint for interactive board validation because it keeps prompts and artifacts inside the app instead of the automated CI scripts.

## Meta verification

`scripts/ci/verify-tests.ps1` ensures all test projects are listed in `BalanceBoard.sln` and meet minimum test counts. Called automatically at the end of `test.ps1`.
