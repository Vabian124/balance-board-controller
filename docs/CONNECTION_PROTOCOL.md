# Wii Balance Board — Connection Protocol Reference

Technical reference for technicians debugging **RVL-WBC-01** connectivity in Balance Board Controller. Covers Bluetooth pairing, HID/L2CAP layers, report formats, and how this repo drives the board via **WiimoteLib** + **32feet.NET**.

**Primary sources:** [WiiBrew — Wii Balance Board](https://www.wiibrew.org/wiki/Wii_Balance_Board), [WiiBrew — Wiimote](https://www.wiibrew.org/wiki/Wiimote), [BrianPeek/lshachar WiimoteLib](https://github.com/lshachar/WiimoteLib), [WiiBalanceWalker reference](../reference/WiiBalanceWalker/FormBluetooth.cs), Nintendo WBC accessory manual (calibration concepts).

---

## 1. Device identity

| Field | Value |
|-------|-------|
| Bluetooth friendly name | `Nintendo RVL-WBC-01` |
| USB HID VID/PID (Wiimote family) | `0x057E` / `0x0306` (WiimoteLib filter) |
| Extension ID (register `0xA400FA`, decrypted) | `0x0402` → `0x2A2C` (Balance Board) |
| Protocol family | Same as Wiimote — extension controller permanently attached |

The board exposes standard Bluetooth HID SDP records like a Wiimote. On **Windows x64**, applications do **not** open L2CAP sockets directly; the stack exposes a **HID device path** (`\\?\HID#...`) consumed via `CreateFile` + `HidD_SetOutputReport` / async read (WiimoteLib approach).

---

## 2. Bluetooth pairing

### 2.1 PIN algorithm (permanent sync)

Nintendo uses **legacy PIN pairing**, not modern SSP passkeys. The PIN depends on **which button initiated discovery**:

| Discovery method | PIN bytes |
|------------------|-----------|
| **Red SYNC** (under battery cover) | **Host** Bluetooth MAC, **reversed** (6 bytes) |
| Hold **1+2** on Wiimote | **Device** MAC reversed (not typical for balance board) |

Balance Board Controller and WiiBalanceWalker use **SYNC flow** → **host MAC reversed**.

**Algorithm** (see `WiiBluetoothPin.cs`, ported from `FormBluetooth.AddressToWiiPin`):

1. Take adapter MAC as 12 hex digits, no separators (e.g. `A1B2C3D4E5F6`).
2. Walk **from last byte to first** in 2-hex-digit pairs.
3. Each pair → `Convert.ToInt32(hex, 16)` → cast to `char` → append to PIN string (6 characters).
4. If any pair is `00`, permanent PIN may fail on some stacks (documented WiiBalanceWalker limitation).

**Example:** host MAC `00:1A:7D:DA:71:13` → normalized `001A7DDA7113` → reversed pairs `13, 71, DA, 7D, 1A, 00` → PIN chars `0x13, 0x71, 0xDA, 0x7D, 0x1A, 0x00`.

**32feet pairing sequence** (`BluetoothPairingService.PairDiscoverableBoard`):

```
1. BluetoothClient.DiscoverDevices(255, authenticated=false, remembered=false, unknown=true)
2. Filter DeviceName contains "Nintendo"
3. new BluetoothWin32Authentication(address, pin)   // register PIN handler BEFORE pair
4. BluetoothSecurity.PairRequest(address, null)     // null → legacy PIN, not SSP
5. device.SetServiceState(BluetoothService.HumanInterfaceDevice, true)
6. Wait BluetoothFinishWaitMs (2000 ms) for HID driver install
```

**Stale cleanup** (first full pairing round only): remove remembered Nintendo devices, disable HID service, `BluetoothSecurity.RemoveDevice`.

### 2.2 Reconnection without SYNC

After **one successful permanent pair**, reconnection **usually** works by:

- Pressing the **front power button** (board wakes and reconnects to saved host).
- Host opening the HID path (WiimoteLib `FindAllWiimotes` + `Connect`).

**SYNC is required again when:**

- Bond cleared (long SYNC hold ~10s, re-pairing to another host, USB dongle replugged with new MAC).
- Windows pairing entry broken or stale.
- Board in **discovery mode** (flashing) and not accepting saved host reconnect.

LabChart / Home Assistant / wiiweigh document the same pattern: pair once with SYNC; later wake with power button.

### 2.4 Host adapter MAC stability

Windows **classic Bluetooth** adapters normally expose a **stable** `BluetoothRadio.LocalAddress` for pairing. Some USB dongles or driver resets can change the reported MAC; **BLE privacy / random addresses apply to peripherals**, not typically the host BR/EDR address used for Wii PIN pairing.

This app stores `LastBluetoothAdapterMac` in `settings.json` after a successful pair. On each connect:

1. Log `[CONNECT] Bluetooth adapter XX:XX:…`
2. If the MAC differs from saved → log *Adapter address changed* and escalate QuickReconnect to **full pairing** (SYNC may be required).
3. After 3 failed HID-only recovery attempts, run a **light re-pair** round automatically.


| Layer | What Windows shows | What app needs |
|-------|-------------------|----------------|
| **ACL / pairing** | Settings → Bluetooth: “Connected” or “Paired” | Bond + HID service enabled |
| **HID enumeration** | Device Manager → Human Interface Devices | `SetupDiEnumDeviceInterfaces` finds `VID_057E&PID_0306` |
| **HID session** | (not shown) | `CreateFile` on HID path, async input loop active |
| **Wiimote handshake** | (not shown) | Status report → extension init → report mode `0x34` continuous |
| **Live data** | (not shown) | Input reports `0x34` with extension bytes updating |

**Windows “Connected” does not guarantee weight data.** It often means the Bluetooth link or pairing record exists while the board is **flashing** (discovery / failed reconnect) or HID is open but **extension init / data reporting** never completed.

At the L2CAP level (WiiBrew): control PSM `0x11`, data PSM `0x13`. Windows abstracts this behind the HID driver.

---

## 3. LED / flashing states

| LED pattern | Meaning |
|-------------|---------|
| **Flashing blue** | Discovery / sync in progress — board searching for host or pairing not completed |
| **Solid blue** | Active session with host (Wii: sync OK; PC: usually after successful HID + `SetLEDs`) |
| **Off** | Asleep, dead batteries, or powered down (~15 min idle power save) |

**Flashing while Windows says “connected”** typically means:

- ACL or pairing record exists, but **no active Wii-style data session**.
- Board timed out waiting for host HID traffic after wake.
- Stale Windows HID node — enumerate finds a path but board firmware is not streaming.

This repo logs: `[DISCONNECT] HID session stale — no balance readings (board may be flashing).`

---

## 4. HID report formats

### 4.1 Output reports (host → board)

WiimoteLib sends **22-byte HID output reports** via `WriteReport` / `HidD_SetOutputReport`. Report ID is `mBuff[0]`. Bit 0 of byte 1 = rumble; bit 2 (`0x04`) = ON flag for several commands.

| Output ID | Name | Purpose in connect flow |
|-----------|------|-------------------------|
| `0x12` | Data reporting mode | Set input report type + continuous flag |
| `0x15` | Status request | Triggers status input report; starts extension detection |
| `0x11` | Player LEDs | Visual confirm; `SetLEDs(true,false,false,false)` |
| `0x16` | Write memory | Extension init writes (`0x55` to `0xA400F0`, etc.) |
| `0x17` | Read memory | Read extension type, calibration |

**Set report mode 0x34 continuous** (WiimoteLib `SetReportType(ButtonsExtension, true)`):

```
Byte:  0     1     2
      0x12  0x04  0x34
            ^^^^ continuous (0x04) + rumble off
```

WiimoteLib **forces** `ButtonsExtension` (`0x34`) for balance boards even if the app passes `InputReport.IRAccel` (`BalanceBoardConnection` line 128).

**Status request:**

```
0x15  0x00
```

**Extension init writes** (during `InitializeExtension`):

```
WriteMemory → 0x04A400F0 ← 0x55
WriteMemory → 0x04A400FB ← 0x00
ReadMemory  → 0x04A400FA (6 bytes, type ID)
ReadMemory  → 0x04A40020 (32 bytes, calibration)
```

WiiBrew notes: balance board init on real Wii writes additional `0xF1` bytes; PC stacks often work without the full Wii sequence, but incomplete init can yield **disabled load sensors** until power cycle.

### 4.2 Input report `0x34` — Core Buttons + 19 extension bytes

```
(a1) 34  BB  BB  EE  EE  EE  EE  EE  EE  EE  EE  EE  EE  EE  EE  EE  EE  EE  EE  EE  EE
          |buttons| |------------ 19 extension bytes ------------------------------|
```

**Extension bytes 0–10** (balance payload, big-endian 16-bit values):

| Ext offset | Content |
|------------|---------|
| 0–1 | Top Right raw |
| 2–3 | Bottom Right raw |
| 4–5 | Top Left raw |
| 6–7 | Bottom Left raw |
| 8 | Temperature |
| 9 | `0x00` |
| 10 | Battery raw |

**Example** (from ESPHome/WiiBrew trace, trimmed):

```
34 00 00  0E F7  0D 16  50 23  2C C2  1C  00  87  ...
          TR    BR    TL    BL   temp    batt
```

Report mode `0x32` (8 extension bytes) is sufficient for weight only; `0x34` adds battery in the stream. WiimoteLib uses `0x34`.

### 4.3 Calibration and kg conversion

Calibration block: **`0xA40020`–`0xA4003F`** (32 bytes, unencrypted on balance board).

Per sensor (TR, BR, TL, BL): three **big-endian uint16** reference raw values at **0 kg, 17 kg, 34 kg**.

**Kg interpolation** (WiimoteLib `GetBalanceBoardSensorValue`):

- Between 0 and 17 kg refs → linear 0–17.
- Between 17 and 34 → linear 17–34.
- Above highest ref → extrapolate from upper two points.

**Total weight in WiimoteLib:** sum of four sensor kg values **divided by 4** (historical averaging bug vs sum — see [WiimoteLib issue #6](https://github.com/BrianPeek/WiimoteLib/issues/6)). `BalanceBoardConnection` reads `bb.WeightKg` as provided by the DLL.

**Temperature compensation** (official Wii Fit): optional factor using byte 8 vs reference temp at `0xA40060` — not implemented in this app's core path.

### 4.4 Tare in this app

`BalanceBoardConnection.Tare()` sets:

```csharp
_device.WiimoteState.BalanceBoardState.ZeroPoint.Reset = true;
```

This is **WiimoteLib software zero-point** (offsets processed values in the library), **not** the Nintendo `WBCSetZEROPoint` firmware call. `BalanceProcessor.Tare()` adds app-level processing offsets.

---

## 5. WiimoteLib connect sequence (what actually runs)

When `BalanceBoardConnection.TryConnectDevice` calls `_device.Connect()`:

```
1. SetupDiEnum HID interfaces → VID 057E PID 0306
2. CreateFile(devicePath, ReadWrite, Overlapped)
3. BeginAsyncRead (22-byte reports, callback OnReadData)
4. ReadWiimoteCalibration (accelerometer — noop for BB weight)
5. GetStatus() → output 0x15, wait status input report (timeout 3s)
6. If status reports extension attached:
     InitializeExtension():
       Write 0x55 / 0x00 to extension key registers
       Read type → BalanceBoard
       SetReportType(ButtonsExtension, continuous) → 0x12 0x04 0x34
       Read 32-byte calibration from 0xA40020
7. Return to app code
```

App then **again** calls:

```
SetReportType(IRAccel, true)  → internally forced to 0x34 continuous
SetLEDs(true, false, false, false)
```

**Important race:** `Connect()` returns after step 5–6 **synchronously** for status, but extension init may still be completing via nested `BeginAsyncRead` calls. `IsConnected = true` is set **before any `0x34` weight report** is verified.

**Disconnect:** `Wiimote.Disconnect()` closes stream/handle; `WiimoteCollectionHelper.ReleaseAll` adds `DisconnectGraceMs` (500) + `HidCallbackDrainMs` (1200) to drain thread-pool `OnReadData` callbacks.

---

## 6. This repo's connection flows

### 6.1 Components

| File | Role |
|------|------|
| `BluetoothPairingService.cs` | 32feet inquiry, PIN pair, HID service enable |
| `WiimoteCollectionHelper.cs` | HID discovery, wake probe, safe release |
| `BalanceBoardConnection.cs` | WiimoteLib connect, readings, tare |
| `BalanceBoardSession.cs` | Intents, health watchdog, BT recovery loop |
| `ConnectionWorker.cs` | STA thread — all Wiimote/BT calls |

### 6.2 Connection intents

| Intent | Steps |
|--------|-------|
| **QuickReconnect** | `WakePairedDevices` → settle 500ms → HID `Connect` |
| **PairAndConnect** | Light pair (no stale remove) OR up to 4 full pair rounds → HID `Connect` |

**WakePairedDevices** (`WiimoteCollectionHelper.WakeDevices`):

```
FindAllWiimotes → foreach: Connect() → SetLEDs(1,0,0,0) → Disconnect()
```

WiiBalanceWalker does the same after pairing so boards **do not sleep and vanish from HID**. This repo **intentionally** does not leave wake handles open (comment: race crashes WiimoteLib).

### 6.3 Session health (v1.2+)

`BalanceBoardSession.IsConnected` = **`IsSessionHealthy()`**, not raw `BalanceBoardConnection.IsConnected`:

| Check | Threshold |
|-------|-----------|
| Grace after connect (no reading yet) | `ConnectHealthGraceMs` = 3000 ms |
| Stale after first reading | `ReadingHealthTimeoutMs` = 2500 ms |

On stale: disconnect HID, `StartBluetoothRecovery()` → background loop with exponential backoff (`ReconnectInitialDelayMs` 1s → max 30s), calling `TryQuickReconnect` (wake + HID only, **no re-pair**).

### 6.4 Log markers to grep

```
[CONNECT] Intent=
[CONNECT] HID discovery:
[CONNECT] HID attempt / success / failed
[CONNECT] First balance reading
[CONNECT] Flow complete:
[CONNECT] Bluetooth recovery started
[DISCONNECT] HID session stale
[DISCONNECT] Releasing HID collection
```

---

## 7. Bug analysis: Connected UI, flashing board, no data

### 7.1 Symptom map

| Observation | Likely protocol state |
|-------------|----------------------|
| Windows BT “Connected”, board **flashing** | Paired / partial ACL; board **not** in active data session with this host |
| App “connected”, no `[CONNECT] First balance reading` | HID handle may be open; **no `0x34` stream** or extension not initialized |
| HID success log but zero weight | Calibration not loaded, wrong device (Wiimote), or sensors not initialized |
| Worked once, fails after idle | Board asleep; need **power button** or SYNC; recovery HID-only may be insufficient |

### 7.2 Missing handshake steps

Common failure points **after** `HID success` log:

1. **Status / extension init** did not finish (no `0x34` reports).
2. **Data reporting mode** not set to `0x34` continuous after extension event (WiiBrew: *“Following extension connect/disconnect, reporting is disabled until mode reset”*).
3. **Wake probe** disconnected too fast — board returned to sleep before app connect.
4. **Stale HID path** — Windows enumerates ghost interface; real board is flashing in discovery.

### 7.3 `BalanceBoardConnection` vs session health gap

- `BalanceBoardConnection.IsConnected` flips `true` immediately after `Connect()` + LED command.
- Session health mitigates UI lying after 3s, but **recovery only runs QuickReconnect** (no pairing). Flashing board often needs **SYNC + light pair** or user power button.

---

## 8. Recommended reconnect sequence (protocol level)

For **BT reconnect worker** (`BalanceBoardSession.BluetoothRecoveryLoop` and related agents):

### Phase A — Board already paired, solid or slow-blinking LED

1. **Do not** call `BluetoothSecurity.RemoveDevice` (breaks permanent PIN).
2. `WakePairedDevices`: `Connect` → `SetReportType(ButtonsExtension, true)` → `SetLEDs(1,0,0,0)` → wait **≥500ms** → `Disconnect`.
3. Wait `PostWakeSettleMs` (500ms).
4. `FindAllWiimotes` — require non-empty HID list.
5. `Connect(preferredDeviceId)` on `ConnectionWorker` STA thread.
6. Wait for **first `0x34` parsing** with `ExtensionType.BalanceBoard` (not just `Connect()` return).
7. Verify `[CONNECT] First balance reading` within `ConnectHealthGraceMs`.

### Phase B — Board flashing (discovery), no readings

1. Log: *“Board in discovery — press power once or SYNC.”*
2. `PairDiscoverableBoard(removeStalePairings: false)` — single light round.
3. Wait `PostPairSettleMs` + `BluetoothFinishWaitMs`.
4. Repeat Phase A steps 4–7.

### Phase C — Repeated failure

1. One round with `removeStalePairings: true` (full cleanup).
2. User must press **SYNC** within inquiry window (`BluetoothInquirySeconds` = 6s per scan).
3. Re-pair with host-reversed PIN.
4. Phase A wake + connect.

### Phase D — Escalation / UX

- Surface `ConnectionPhase.PairedReconnecting` when BT radio off vs `Reconnecting` for HID attempts.
- If `IsConnected` HID but unhealthy > grace: **force** `Disconnect` + drain (`HidCallbackDrainMs`) before retry.
- Never run concurrent `WiimoteCollection` probes during active session connect.

### Code-level fixes (recommended)

1. **Gate `TryConnectDevice` success** on first valid balance reading OR explicit extension-init-complete flag (WiimoteLib does not expose one — timeout wait for `WiimoteChanged` with BB type).
2. **Upgrade recovery loop**: after N failed QuickReconnects, call `PairDiscoverableBoard(false)` before next HID attempt.
3. **WakePairedDevices**: add `SetReportType(..., true)` before disconnect (mirror full session, not LED-only).
4. **Distinguish** `BalanceBoardConnection.IsConnected` (HID handle) from session healthy in UI labels.
5. **Detect flashing** indirectly: HID open + no `0x34` within grace → status *“Board paired but not streaming — press power or SYNC.”*

---

## 9. WiimoteLib on Windows — practical notes

- Uses **Windows HID API only** — works across Microsoft, Broadcom, Intel stacks that expose standard HID.
- Enumerates **all** `057E:0306` devices — may include Wiimotes; app checks `ExtensionType.BalanceBoard`.
- `OnReadData` runs on **thread pool**; disconnect must unsubscribe + drain (this repo's `ConnectionWorker` + `ReleaseAll` pattern).
- `mAltWriteMethod` fallback uses `HidD_SetOutputReport` if stream write fails during calibration read.
- Bundled DLL: `libs/x64/WiimoteLib.dll` ([lshachar fork](https://github.com/lshachar/WiimoteLib)).

---

## 10. Quick reference hex

| Item | Hex |
|------|-----|
| Extension type (encrypted read) | `04 02` at `FA` |
| Extension init | `F0←55`, `FB←00` |
| Report mode | Output `12 04 34` → Input `34 ...` |
| Load cells in ext bytes | 8 bytes: TR, BR, TL, BL (16-bit BE each) |
| Calib 0kg TR offset in block | bytes `0x24–0x25` of calib read |
| Button A (power) | Core button byte 2, bit 3 |

---

## 11. Related repo docs

- [WORKFLOW.md](WORKFLOW.md) — user-visible connect policy
- [ARCHITECTURE.md](ARCHITECTURE.md) — `ConnectionWorker`, recovery roadmap
- [STORAGE.md](STORAGE.md) — `LastConnectedDeviceId`, session logs
- [TEST_PLAN.md](TEST_PLAN.md) — reconnect test matrix

---

*Document generated for technician debugging. Recommend committing to the repo after review.*
