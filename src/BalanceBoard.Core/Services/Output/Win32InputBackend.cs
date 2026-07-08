using System.Runtime.InteropServices;
using BalanceBoard.Core.Abstractions;

namespace BalanceBoard.Core.Services.Output;

/// <summary>
/// Windows SendInput backend for <see cref="Processing.ActionEngine"/>.
/// </summary>
public sealed class Win32InputBackend : IInputBackend
{
    [DllImport("user32.dll")]
    private static extern int SendInput(int cInputs, ref INPUT pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint dwType;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_XDOWN = 0x0080;
    private const uint MOUSEEVENTF_XUP = 0x0100;
    private const uint MOUSEEVENTF_MOVE = 0x0001;

    // Match WiiBalanceWalker InputManager.GetScanKey — extended flag distinguishes arrows/nav from numpad.
    private static readonly HashSet<ushort> ExtendedVirtualKeys =
    [
        0x21, // PageUp
        0x22, // PageDown
        0x23, // End
        0x24, // Home
        0x25, // Left
        0x26, // Up
        0x27, // Right
        0x28, // Down
        0x2D, // Insert
        0x2E, // Delete
        0x5B, // LWin
        0x5C, // RWin
        0x6F, // Divide (numpad /) — treated as extended by Windows
        0x90, // NumLock
        0xA3, // RControlKey
        0xA5, // RMenu (RAlt)
        0x2C, // PrintScreen
    ];

    /// <summary>
    /// True when <paramref name="virtualKey"/> must be sent with <c>KEYEVENTF_EXTENDEDKEY</c>
    /// so scancode injection is not interpreted as a numpad key.
    /// </summary>
    public static bool RequiresExtendedKeyFlag(ushort virtualKey) => ExtendedVirtualKeys.Contains(virtualKey);

    /// <summary>Build SendInput KEYBDINPUT.dwFlags for a virtual-key scancode injection.</summary>
    public static uint BuildKeyEventFlags(ushort virtualKey, bool keyUp)
    {
        var flags = KEYEVENTF_SCANCODE;
        if (RequiresExtendedKeyFlag(virtualKey))
        {
            flags |= KEYEVENTF_EXTENDEDKEY;
        }

        if (keyUp)
        {
            flags |= KEYEVENTF_KEYUP;
        }

        return flags;
    }

    public void KeyDown(ushort virtualKey)
    {
        var scan = (ushort)MapVirtualKey(virtualKey, 0);
        var input = new INPUT
        {
            dwType = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT { wScan = scan, dwFlags = BuildKeyEventFlags(virtualKey, keyUp: false) },
            },
        };
        _ = SendInput(1, ref input, Marshal.SizeOf<INPUT>());
    }

    public void KeyUp(ushort virtualKey)
    {
        var scan = (ushort)MapVirtualKey(virtualKey, 0);
        var input = new INPUT
        {
            dwType = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT { wScan = scan, dwFlags = BuildKeyEventFlags(virtualKey, keyUp: true) },
            },
        };
        _ = SendInput(1, ref input, Marshal.SizeOf<INPUT>());
    }

    public void MouseDown(string button)
    {
        if (!TryGetMouseButton(button, down: true, out var flag, out var data))
        {
            return;
        }

        SendMouse(flag, data);
    }

    public void MouseUp(string button)
    {
        if (!TryGetMouseButton(button, down: false, out var flag, out var data))
        {
            return;
        }

        SendMouse(flag, data);
    }

    public void MoveRelative(int deltaX, int deltaY)
    {
        var input = new INPUT
        {
            dwType = INPUT_MOUSE,
            U = new InputUnion { mi = new MOUSEINPUT { dx = deltaX, dy = deltaY, dwFlags = MOUSEEVENTF_MOVE } }
        };
        _ = SendInput(1, ref input, Marshal.SizeOf<INPUT>());
    }

    private static void SendMouse(uint flag, int data)
    {
        var input = new INPUT
        {
            dwType = INPUT_MOUSE,
            U = new InputUnion { mi = new MOUSEINPUT { dwFlags = flag, mouseData = data } }
        };
        _ = SendInput(1, ref input, Marshal.SizeOf<INPUT>());
    }

    private static bool TryGetMouseButton(string button, bool down, out uint flag, out int data)
    {
        data = 0;
        flag = button switch
        {
            "Left" => down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP,
            "Right" => down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP,
            "Middle" => down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
            "X1" or "Back" => down ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP,
            "X2" => down ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP,
            _ => 0,
        };

        if (button is "X1" or "Back")
        {
            data = 1;
        }
        else if (button is "X2")
        {
            data = 2;
        }

        return flag != 0;
    }
}
