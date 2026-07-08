## Summary

<!-- What changed and why? -->

## Test plan

- [ ] `.\scripts\ci\lint.ps1` (or CI **Quality gate** green)
- [ ] Manual smoke (if UI/connect touched): `.\start.bat`

CI runs the unified test pipeline (`scripts/ci/test.ps1`): Core, Fuzz, Integration, **BalanceBoard.App.Ui.Tests**, Validate, **BalanceBoard.Automation**.

## Checklist

- [ ] Focused diff; no unrelated refactors
- [ ] `docs/updates/` entry if behavior or architecture changed (see `docs/updates/README.md`)
