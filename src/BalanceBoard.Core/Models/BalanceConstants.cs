namespace BalanceBoard.Core.Models;

/// <summary>
/// Shared thresholds and scaling factors — copy verbatim to Python <c>balance_constants.py</c>.
/// </summary>
public static class BalanceConstants
{
    public const float WeightOnBoardThresholdKg = 5f;
    public const float JumpWeightThresholdKg = 1f;
    public const double JumpHoldSeconds = 2;
    public const float JumpEasyThresholdKg = 20f;
    public const float JumpNormalThresholdKg = 35f;
    public const float JumpHardThresholdKg = 50f;
    public const double JumpEasyHoldSeconds = 0.6;
    public const double JumpNormalHoldSeconds = 0.4;
    public const double JumpHardHoldSeconds = 0.25;
    /// <summary>Short vJoy button pulse for game presets (Minecraft / Controlify).</summary>
    public const double VJoyJumpPulseSeconds = 0.35;
    public const float BalanceCenterPercent = 50f;
    public const float DiagonalDeltaThreshold = 15f;
    public const float MinTotalWeightEpsilon = 0.001f;
    public const float PercentScale = 100f;
    public const float JoyAxisScale = 655.34f;
    public const float JoyAxisOffset = -32767f;
    public const double JoySensitivityMultiplier = 2.0;
    public const int SensorKgToAxisMultiplier = 100;
    public const int SessionPollIntervalMs = 50;
    public const int DisconnectGraceMs = 500;
    public const int HidCallbackDrainMs = 1200;
    public const int PostPairSettleMs = 1000;
    public const int PostWakeSettleMs = 500;
    /// <summary>Hold wake HID session (report mode + LED) before disconnect so the board stays paired and awake.</summary>
    public const int WakeProbeHoldMs = 500;
    public const int BluetoothFinishWaitMs = 2000;
    public const int PairRoundDelayMs = 1500;
    public const int BluetoothInquirySeconds = 6;
    /// <summary>After HID open, wait this long for the first balance reading before treating the session as stale.</summary>
    public const int ConnectHealthGraceMs = 3000;
    /// <summary>No balance reading within this window means the HID session is dead (board may be flashing).</summary>
    public const int ReadingHealthTimeoutMs = 2500;
    public const int ReconnectInitialDelayMs = 750;
    public const int ReconnectMaxDelayMs = 30000;
    /// <summary>HID-only recovery attempts before escalating to light Bluetooth re-pair.</summary>
    public const int RecoveryPairAfterAttempts = 3;
    /// <summary>Light re-pair attempts before escalating to full SYNC pairing (stale bond removal).</summary>
    public const int RecoveryFullPairAfterAttempts = 6;
    /// <summary>Poll interval while waiting for Bluetooth radio to become ready after power-on.</summary>
    public const int BtRadioReadyPollMs = 250;
    /// <summary>Consecutive radio-ready polls required before resuming HID recovery.</summary>
    public const int BtRadioReadyStablePolls = 2;
}

/// <summary>
/// Default movement trigger percentages (also used by <see cref="ActionPresets"/>).
/// </summary>
public static class TriggerDefaults
{
    public const int LeftRight = 8;
    public const int ForwardBackward = 9;
    public const int ModifierLeftRight = 15;
    public const int ModifierForwardBackward = 16;

    public static void ApplyTo(AppSettings settings)
    {
        settings.TriggerLeftRight = LeftRight;
        settings.TriggerForwardBackward = ForwardBackward;
        settings.TriggerModifierLeftRight = ModifierLeftRight;
        settings.TriggerModifierForwardBackward = ModifierForwardBackward;
    }
}
