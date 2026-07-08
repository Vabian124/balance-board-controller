# Contributing

Thanks for improving Balance Board Controller. This repo is structured for portfolio-quality engineering: clear layers, automated gates, and documented architecture.

## Quick start

1. Fork / branch from `main`
2. Read [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) and [docs/testing/README.md](docs/testing/README.md)
3. Make focused changes
4. Run the quality gate:

   ```powershell
   .\scripts\ci\lint.ps1
   ```

5. Open a PR (template provided)

## `main` branch protection

`main` is protected on GitHub:

| Rule | Effect |
|------|--------|
| **CI must pass** | Merges require the **Quality gate** check (same as `scripts/lint.ps1`) |
| **Up to date** | Branch must include latest `main` before merge |
| **No force-push** | History on `main` cannot be rewritten |
| **No deletion** | `main` cannot be deleted accidentally |

Work on a feature branch, open a PR, wait for green CI, then merge. Repo admins can still bypass in an emergency (`enforce_admins` is off).

## Quality requirements

All PRs must pass **CI** (same as `scripts/ci/lint.ps1`):

| Step | What it enforces |
|------|------------------|
| `dotnet format --verify-no-changes` | Consistent style ([`.editorconfig`](.editorconfig)) |
| `dotnet build -warnaserror` | Roslyn analyzers + nullable (Release) |
| `BalanceBoard.Core.Tests` / `BalanceBoard.Fuzz.Tests` | Unit + property tests |
| `BalanceBoard.Integration.Tests` | Session/connect fakes |
| `BalanceBoard.App.Ui.Tests` | Headless WPF UI tests |
| `tools/Validate` | vJoy/HID diagnostics CLI |
| `BalanceBoard.Automation` | Start/stop/single-instance lifecycle |

SDK version is pinned in [`global.json`](global.json).

## For AI coding agents

1. [INSTRUCTIONS.md](INSTRUCTIONS.md) — every-pass checklist
2. [AGENTS.md](AGENTS.md) — architecture and edit map
3. [llms.txt](llms.txt) — machine-readable index

## Scope guidelines

- UI in `BalanceBoard.App`, logic in `BalanceBoard.Core`
- One concern per PR when possible
- Update [docs/ROADMAP.md](docs/ROADMAP.md) when completing planned items
- Add `docs/updates/` entry for meaningful commits ([index](docs/updates/README.md))
- Never commit logs, secrets, or personal data

## Security

See [SECURITY.md](SECURITY.md) for vulnerability reporting.

## Questions

[GitHub Issues](https://github.com/Vabian124/balance-board-controller/issues)
