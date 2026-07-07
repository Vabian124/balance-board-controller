# Glossary

Terms used throughout Balance Board Controller code and docs.

| Term | Meaning |
|------|---------|
| **Balance board** | Nintendo Wii Fit Balance Board (4 load sensors, Bluetooth) |
| **COG / center of gravity** | Lean position derived from corner weights; maps to balance X/Y % |
| **Tare** | Zero calibration — subtracts minimum seen weight per corner |
| **Set center** | User standing position becomes neutral; stores per-corner offsets |
| **Trigger** | Balance % threshold before a movement flag activates (e.g. 8% left/right) |
| **Modifier** | Secondary movement zone (higher threshold) — bound to Shift in desktop preset |
| **Jump** | Detected when weight drops below `JumpWeightThresholdKg` for `JumpHoldSeconds` |
| **Jump preset** | Easy / Normal / Hard (`JumpLevel`) — accessibility tuning via `JumpPresets` |
| **UI detail level** | Simple / Standard / Advanced (`UiDetailLevel`) — progressive disclosure in tabbed UI |
| **ProcessedBalance** | Output of `BalanceProcessor` — lean, flags, vJoy axis values |
| **Action slot** | One of 8 named outputs: Left, Right, Forward, Backward, Modifier, Jump, DiagonalLeft, DiagonalRight |
| **Preset / profile** | Named `AppSettings` bundle from `ActionPresets` (Game Controller, Minecraft, Pedal, Desktop, Balance Mouse) |
| **ConnectionWorker** | Dedicated STA thread for WiimoteLib, Bluetooth, and the 50 ms poll loop |
| **vJoy** | Virtual joystick driver; games see a standard game controller |
| **Feeder** | Process that writes to vJoy (this app, WiiBalanceWalker, vJoy Monitor) |
| **VJD_STAT_BUSY** | vJoy device held by another process |
| **WiimoteLib** | Third-party library for Wii HID devices |
| **SendInput** | Windows API used to synthesize keyboard/mouse |
| **X1 / X2** | Mouse side buttons (back/forward); `MOUSEEVENTF_XDOWN` with mouseData 1 or 2 |
| **Debug Suite** | Advanced tab: health check, log viewer, copy report |
| **Validate tool** | CLI in `tools/Validate/` — same diagnostics without GUI |
| **Single instance** | Only one `BalanceBoardApp` via mutex; new launch activates existing window |
| **FeederProcessCleanup** | Kills stale `BalanceBoardApp`, `WiiBalanceWalker`, `WBBGUI` before vJoy acquire |
| **Structured log tag** | Prefix like `[CONNECT]`, `[DISCONNECT]`, `[JUMP]` in session log for support grep |
