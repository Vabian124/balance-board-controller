# Native libraries (x64)

These DLLs are **checked into the repo** so a fresh clone builds without extra downloads. They are copied next to `BalanceBoardApp.exe` at build time.

| File | Source | License |
|------|--------|---------|
| `WiimoteLib.dll` | [lshachar/WiimoteLib](https://github.com/lshachar/WiimoteLib) | MIT |
| `InTheHand.Net.Personal.dll` | [32feet](https://github.com/inthehand/32feet) | See upstream |
| `vJoyInterface.dll`, `vJoyInterfaceWrap.dll` | [shauleiz/vJoy](https://github.com/shauleiz/vJoy) | See upstream |

## vJoy driver vs bundled DLLs

The **vJoy kernel driver** must be installed separately (not shipped in this repo). If health check reports a DLL/driver version mismatch, copy matching `vJoyInterface*.dll` from your vJoy installation folder into this directory and rebuild.

See [THIRD_PARTY_NOTICES.md](../../THIRD_PARTY_NOTICES.md).
