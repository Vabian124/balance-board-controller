# INSTRUCTIONS.md — LLM maintenance playbook

> **Every AI assistant working in this repo must read this file.**  
> It defines what to do on **every pass**, **every other pass**, and **before ending a session**.

Fork-friendly: new code is **[MIT licensed](../LICENSE)** — anyone may use, copy, modify, merge, publish, distribute, sublicense, and sell. Legacy `WiiBalanceWalker/` remains under MS-PL (see [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md)).

---

## Every pass (do this always)

Run through this list **at the start and end** of each turn where you touch the repo:

1. **Orient**
   - Read [AGENTS.md](AGENTS.md) if this is your first turn in the repo.
   - Check `git status` and `git log -3 --oneline` — know what is committed vs dirty.
   - Read the latest entry in [docs/updates/README.md](docs/updates/README.md) so you do not redo or contradict past work.

2. **Stay in scope**
   - UI → `src/BalanceBoard.App/` only.
   - Logic → `src/BalanceBoard.Core/` only.
   - Minimal diffs; match existing style ([.editorconfig](.editorconfig)).

3. **Never leave dirty work**
   - **Commit every change** before you finish the session (user requirement).
   - Do not accumulate uncommitted files across turns.
   - Only skip commit if the user explicitly says “do not commit” for that turn.

4. **Never commit**
   - Logs (`*.log`, `%AppData%` paths), `baseline/`, loose files in `reference/` (not `reference/WiiBalanceWalker/`)
   - Passwords, tokens, personal paths, or machine-specific secrets
   - Accidental `bin/`, `obj/`, `.vs/` (already gitignored)

5. **Verify when code changed**
   ```powershell
   dotnet build BalanceBoard.sln -c Release
   ```

---

## Every other pass (alternate sessions / larger edits)

On sessions where you implement features, fix bugs, or change docs structure, also do:

1. **Update the audit log** ([docs/updates/](docs/updates/))
   - After each meaningful commit, add `YYYY-MM-DD_HHMMSS_<short-hash>_<slug>.md`
   - Append a row to `docs/updates/README.md` index
   - List **what was done** and **what was NOT done**

2. **Keep docs truthful**
   - [docs/CODEMAP.md](docs/CODEMAP.md) — if files added/removed/renamed
   - [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — if data flow or presets changed
   - [docs/ROADMAP.md](docs/ROADMAP.md) — check off completed items; add new plans
   - [README.md](README.md) — if user-facing behavior changed

3. **Format & hygiene**
   ```powershell
   dotnet format BalanceBoard.sln
   ```

4. **License & attribution**
   - New source files: MIT header not required (LICENSE covers repo), but do not remove [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)
   - Do not relicense legacy `reference/WiiBalanceWalker/` — keep MS-PL notice intact

---

## Before ending a session (checklist)

```
[ ] git status — working tree clean (nothing unstaged/uncommitted)
[ ] dotnet build BalanceBoard.sln -c Release — passes (if code touched)
[ ] docs/updates/ entry added (if meaningful work this session)
[ ] ROADMAP/CODEMAP/README updated (if behavior or structure changed)
[ ] Commit message explains WHY, not just WHAT
[ ] No personal data in committed files
```

**Push to GitHub:** only when the user asks. **Commit locally:** always (unless user opts out).

---

## How to commit (required style)

1. Inspect changes:
   ```powershell
   git status
   git diff
   git log -3 --oneline
   ```

2. Stage relevant files only (not logs, not local junk).

3. Commit with a clear message:
   ```powershell
   git commit -m "Short summary in imperative mood." -m "Optional body: why this change matters."
   ```

4. Record in `docs/updates/` (see [docs/updates/README.md](docs/updates/README.md)).

### Commit message examples

| Good | Bad |
|------|-----|
| `Add tray icon with connect toggle` | `fixes` |
| `Fix vJoy busy retry after feeder cleanup` | `WIP` |
| `Document LLM pass instructions in INSTRUCTIONS.md` | `update files` |

---

## Fork & copy guidance (for humans and LLMs)

| Component | License | You may |
|-----------|---------|---------|
| **Balance Board Controller** (new code: `src/`, `tools/`, docs) | **MIT** | Use commercially, fork, modify, redistribute — include LICENSE copy |
| **WiiBalanceWalker/** (legacy reference) | **MS-PL** | Per MS-PL terms; keep notices; path `reference/WiiBalanceWalker/` |
| **WiimoteLib.dll** | MIT | Per upstream |
| **vJoy** | Upstream | Install separately; driver not shipped |

This repo is intended to be **fork-friendly**. Do not add restrictive licenses or DRM. Do not remove credit to upstream projects in `THIRD_PARTY_NOTICES.md`.

---

## When confused

| Question | Where to look |
|----------|----------------|
| What did past agents do? | [docs/updates/](docs/updates/) |
| What is planned but not built? | [docs/ROADMAP.md](docs/ROADMAP.md) |
| Which file do I edit? | [docs/CODEMAP.md](docs/CODEMAP.md) |
| How does runtime work? | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| Build / CI / debug? | [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) |
| Terms (tare, vJoy, triggers)? | [docs/GLOSSARY.md](docs/GLOSSARY.md) |

If user request conflicts with this file, **follow the user** — then update this file if the new rule should persist.

---

## Quick reference: doc map

```
INSTRUCTIONS.md     ← you are here (every-pass playbook)
AGENTS.md           ← project onboarding & conventions
llms.txt            ← machine index of all docs
docs/updates/       ← timestamped history per commit
docs/ROADMAP.md     ← future work
.cursor/rules/      ← Cursor-specific rules (auto-loaded)
```
