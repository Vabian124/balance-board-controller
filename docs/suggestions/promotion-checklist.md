# Promotion checklist

When a suggestion is **Done**, copy stable conventions into permanent LLM/human docs. Mark the suggestion **Promoted** in the review file.

## Promotion targets

| Target | Audience | Max size guidance |
|--------|----------|-------------------|
| `AGENTS.md` | AI agents, quick onboarding | Keep under ~150 lines for "30 second" sections |
| `llms.txt` | Machine index | Links only, no prose |
| `.cursor/rules/*.mdc` | Cursor auto-loaded rules | One concern per file; under ~50 lines |
| `CONTRIBUTING.md` | Human contributors | Process + CI |
| `docs/STANDARDS.md` (new) | Detailed coding rules | Full `coding-standards-draft.md` |

---

## Checklist by topic

### ConnectionWorker / threading (after R-01 resolved)

- [ ] **AGENTS.md** — Add bullet: "All Wiimote/BT on `ConnectionWorker` STA thread."
- [ ] **AGENTS.md** — Document poll model (poll-only OR event+watchdog — pick one).
- [ ] **core-services.mdc** — "Do not add second poll path."
- [ ] **docs/ARCHITECTURE.md** — Replace `System.Timers.Timer` with `ConnectionWorker` diagram.
- [ ] **docs/WORKFLOW.md** — Note `DiscoverDeviceIds` short-circuit when connected.

### Wiimote lifecycle (W-01, W-02)

- [ ] **AGENTS.md** — "Getting unstuck" row: OnReadData crash → check `HidCallbackDrainMs`, single worker.
- [ ] **core-services.mdc** — `WiimoteCollectionHelper.ReleaseAll` required after probes.
- [ ] **CONTRIBUTING.md** — Link to `scripts/ci/check-crash-safety.ps1`.

### UI patterns (U-02, C-06)

- [ ] **wpf-ui.mdc** — Remove `SetupWizardWindow`; say `BeginInvoke` not `Invoke`.
- [ ] **wpf-ui.mdc** — `_uiReady` / `_suppressSettingEvents` examples.
- [ ] **AGENTS.md** — Settings-before-`InitializeComponent` (already present — verify).

### vJoy (P-01)

- [ ] **core-services.mdc** — Axis coalescing rule if implemented.
- [ ] **docs/ARCHITECTURE.md** — Note write optimization in vJoy lifecycle.

### DeviceIdRules (R-02)

- [ ] **CODEMAP.md** — `DeviceIdRules.ExtractFromHidPath`.
- [ ] **coding-standards-draft.md** → **STANDARDS.md** — "Don't duplicate ExtractDeviceId."

### BT reconnect (W-04)

- [ ] **AGENTS.md** — Document auto-reconnect behavior once implemented (backoff, max retries).
- [ ] **docs/ARCHITECTURE.md** — Add reconnect state to session diagram.
- [ ] **ROADMAP.md** — Check off Phase 3 reconnect item.

### Testing (T-07)

- [ ] **ROADMAP.md** — Check off completed tests; add new gaps.
- [ ] **docs/testing/README.md** — Dual-path regression test if added.

### CI (CI-01, D-03)

- [ ] **CONTRIBUTING.md** — Correct workflow filename `ci.yml`.
- [ ] **AGENTS.md** — Fix `build.yml` → `ci.yml`.
- [ ] **docs/CODEMAP.md** — Fix `build.yml` → `ci.yml`.
- [ ] **docs/DEVELOPMENT.md** — Fix `build.yml` → `ci.yml`.

### Commit policy (C-07)

- [ ] **INSTRUCTIONS.md** — Clarify commit default vs user opt-out.
- [ ] **project-core.mdc** — Match INSTRUCTIONS wording.

### Python port (PY-03)

- [ ] **PYTHON_PORTING.md** — Poll vs event contract.
- [ ] **STORAGE.md** — `SchemaVersion` field when added.

### Suggestions backlog meta

- [ ] **llms.txt** — Add line: `docs/suggestions/` — improvement backlog.
- [ ] **INSTRUCTIONS.md** — "Before large refactors, check `docs/suggestions/`."

---

## Per-target promotion templates

Use these as starting text when copying **Done** items into permanent docs.

### AGENTS.md additions

```markdown
## Threading (ConnectionWorker)

- All WiimoteLib and Bluetooth calls go through `ConnectionWorker` (STA thread) via `_worker.Invoke*`.
- Poll model: [poll-only | event+watchdog] — see ARCHITECTURE.md. Do not add a second poll path.
- Wiimote teardown: always `ReleaseAll` + honor `HidCallbackDrainMs` after disconnect.

## Getting unstuck

| Symptom | Check |
|---------|-------|
| OnReadData crash after disconnect | `HidCallbackDrainMs`, single worker, unsubscribe before Disconnect |
| vJoy busy on startup | `FeederProcessCleanup` before acquire |
| Dual vJoy writes | R-01 — only one poll driver should be active |
```

### llms.txt rows (links only)

```
docs/suggestions/          Improvement backlog (not yet implemented)
docs/STANDARDS.md          Coding standards (after promotion)
CONTRIBUTING.md            Human contributor guide
scripts/ci/lint.ps1        Canonical CI gate (local + GitHub Actions)
.github/workflows/ci.yml   CI workflow (not build.yml)
```

### `.cursor/rules/core-services.mdc` additions

```markdown
- Route Wiimote/BT through ConnectionWorker STA thread only.
- Do not add a second poll loop outside BalanceBoardSession.
- WiimoteCollectionHelper.ReleaseAll required after any WiimoteCollection probe.
- Use DeviceIdRules.ExtractFromHidPath — do not duplicate ExtractDeviceId.
- vJoy: skip SetAxis when value unchanged (after P-01).
```

### `.cursor/rules/wpf-ui.mdc` fixes

```markdown
- MainWindow only (SetupWizardWindow was removed 2026-07-07).
- Use Dispatcher.BeginInvoke for session events — not Invoke.
- Settings: load before InitializeComponent; guard with _uiReady and _suppressSettingEvents.
```

### CONTRIBUTING.md sections to add

- [ ] **CI** — "Run `.\scripts\ci\lint.ps1` before opening a PR. Workflow: `.github/workflows/ci.yml`."
- [ ] **Crash safety** — Link `scripts/ci/check-crash-safety.ps1`; forbidden: `Environment.Exit`, `Shutdown(-1)`.
- [ ] **Suggestions backlog** — "Check `docs/suggestions/` for known architecture debt before large refactors."
- [ ] **Coding standards** — Link `docs/STANDARDS.md` (after creation).

### docs/STANDARDS.md creation

- [ ] Copy full `coding-standards-draft.md` content.
- [ ] Remove "draft" header; set status to **Enforced** with CI references.
- [ ] Add link from `CONTRIBUTING.md` and `llms.txt`.
- [ ] Mark promoted items in `2026-07-07-code-review.md` as **Promoted**.

---

## Promotion order (recommended)

1. Fix **stale/wrong** docs (ARCHITECTURE, wpf-ui, ROADMAP, ci.yml path) — prevents agent harm. **No code dependency.**
2. Promote **ConnectionWorker + poll model** after R-01 is decided and implemented.
3. Promote **Wiimote teardown** constants (already implemented — W-01, W-02).
4. Promote **vJoy coalescing** after P-01 lands.
5. Promote **DeviceIdRules.ExtractFromHidPath** after R-02 lands.
6. Promote **BT auto-reconnect** after W-04 lands.
7. Merge **coding-standards-draft.md** → `docs/STANDARDS.md` + CONTRIBUTING link.
8. Add **llms.txt** index row for suggestions backlog.

---

## Verification before marking Promoted

For each promoted item, confirm:

- [ ] Implementation merged to `main` (not just proposed).
- [ ] Permanent doc text matches actual code behavior.
- [ ] No contradictory guidance left in ARCHITECTURE, AGENTS, or cursor rules.
- [ ] Review item status updated: `Done` → `Promoted` with target file noted.

---

## Anti-patterns for promotion

- Do not copy work-in-progress notes into AGENTS.md (e.g. dual poll before fix).
- Do not duplicate full CODEMAP into AGENTS — link instead.
- Do not put suggestion **Proposed** items into always-applied cursor rules.
- Do not promote `QuickReconnect` auto-recovery until W-04 is implemented.
- Do not reference `SetupWizardWindow` anywhere after U-01 promotion.
