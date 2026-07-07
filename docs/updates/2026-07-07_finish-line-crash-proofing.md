# Finish-line: crash-proofing, UX detail levels, jump presets

## Done

- Safe Minecraft preset brush resolution (`TryFindResource` + `#22C55E` fallback)
- Disconnect hardening (`_disconnecting` flag, benign `ObjectDisposedException` / `IOException`)
- `JumpPresets` (Easy/Normal/Hard) + top-center jump banner + prominent "Jump!" direction text
- `UiDetailLevel` (Simple / Standard / Advanced) with progressive disclosure
- Structured log prefixes: `[CONNECT]`, `[DISCONNECT]`, `[JUMP]`, `[VJOY]`, `[SETTINGS]`, `[ERROR]`
- UiSmoke applies Minecraft preset; integration tests for disconnect + late callbacks
- Full CI quality gate passes (84 tests)

## Not done

- Real-board manual QA (user checklist in release notes)
- Python port of `JumpPresets` / `UiDetailLevel`
