# Releasing Balance Board Controller

Target: **under 5–10 minutes** end-to-end when CI on `main` is already green.

## How it works

| Stage | Workflow | What runs | Typical time |
|-------|----------|-----------|--------------|
| **Quality** | `ci.yml` on push/PR to `main` | format → build → full test pipeline | ~4–8 min |
| **Release** | `release.yml` on tag push | CI status check → build → zip → GitHub Release | ~2–4 min |

Release **does not** re-run the 5-layer test suite. It verifies a successful CI run on the tagged commit, then packages only.

## Fast release (maintainer)

### New version

1. Bump version and tag:
   ```powershell
   .\scripts\release\bump-and-tag.ps1 -Version 1.5.2 -Commit
   ```
2. Update [CHANGELOG.md](../CHANGELOG.md) (and commit if not done in step 1).
3. Push `main`, wait for green CI (~5 min).
4. Push the tag:
   ```powershell
   git push origin v1.5.2
   ```
   Or one command after CI is green:
   ```powershell
   .\scripts\release\quick-release.ps1
   ```

### Re-publish an existing tag (e.g. fix CI then ship v1.4.0)

1. Fix on `main`, push, wait for green CI.
2. Move the tag to the fixed commit and push:
   ```powershell
   .\scripts\release\quick-release.ps1 -Tag v1.4.0 -Retag
   ```
3. Or re-run packaging only (no tag push):
   ```powershell
   .\scripts\release\quick-release.ps1 -Tag v1.4.0 -DispatchOnly
   ```

### Manual workflow dispatch

GitHub → **Actions** → **Release** → **Run workflow** → enter tag (e.g. `v1.4.0`).  
Use **skip_ci_check** only for emergency republish when CI metadata is missing.

## What was slow before

- **Duplicate quality gate**: `release.yml` ran `scripts/ci/lint.ps1` (full format + build + all tests) before packaging — doubling CI time (~8+ min).
- **Simulated connect on CI**: `--simulate-board` still waited for Bluetooth radio, causing UI and lifecycle timeouts.
- **`dotnet format` PATH**: CI runners set `DOTNET_ROOT` but not always PATH; format subprocess failed before tests ran.
- **Artifact upload noise**: empty `artifacts/test/` after early failure caused extra warnings.

## Local checks

```powershell
# Full gate (same as CI)
.\scripts\ci\lint.ps1

# Faster local iteration
.\scripts\ci\lint.ps1 -Quick
.\scripts\ci\test.ps1 -Quick

# Package only (what release runs)
.\scripts\ci\publish-release.ps1
```

## Safety model

- **CI on `main`** is the single full test gate (unit, integration, UI, Validate, lifecycle).
- **Release** requires a green CI conclusion on the tagged SHA (unless `skip_ci_check` on manual dispatch).
- **Release build** still runs `dotnet build -warnaserror` before `dotnet publish`.
