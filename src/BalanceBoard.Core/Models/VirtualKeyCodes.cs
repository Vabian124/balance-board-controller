namespace BalanceBoard.Core.Models;

/// <summary>
/// String key names to Windows virtual-key codes. Portable lookup — no WinForms dependency.
/// Port to Python as <c>virtual_key_codes.py</c> (same names, same VK integers).
/// </summary>
public static class VirtualKeyCodes
{
    private static readonly Dictionary<string, ushort> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Back"] = 0x08,
        ["Tab"] = 0x09,
        ["Return"] = 0x0D,
        ["Enter"] = 0x0D,
        ["ShiftKey"] = 0x10,
        ["ControlKey"] = 0x11,
        ["Menu"] = 0x12,
        ["Alt"] = 0x12,
        ["Pause"] = 0x13,
        ["Capital"] = 0x14,
        ["Escape"] = 0x1B,
        ["Space"] = 0x20,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["End"] = 0x23,
        ["Home"] = 0x24,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
        ["LShiftKey"] = 0xA0,
        ["RShiftKey"] = 0xA1,
        ["LControlKey"] = 0xA2,
        ["RControlKey"] = 0xA3,
        ["LMenu"] = 0xA4,
        ["RMenu"] = 0xA5,
        ["LWin"] = 0x5B,
        ["RWin"] = 0x5C,
        // WPF System.Windows.Input.Key.ToString() aliases (what the in-app key capture records).
        ["LeftShift"] = 0xA0,
        ["RightShift"] = 0xA1,
        ["LeftCtrl"] = 0xA2,
        ["RightCtrl"] = 0xA3,
        ["LeftAlt"] = 0xA4,
        ["RightAlt"] = 0xA5,
        ["CapsLock"] = 0x14,
        ["System"] = 0x12,
        ["D0"] = 0x30,
        ["D1"] = 0x31,
        ["D2"] = 0x32,
        ["D3"] = 0x33,
        ["D4"] = 0x34,
        ["D5"] = 0x35,
        ["D6"] = 0x36,
        ["D7"] = 0x37,
        ["D8"] = 0x38,
        ["D9"] = 0x39,
        ["NumPad0"] = 0x60,
        ["NumPad1"] = 0x61,
        ["NumPad2"] = 0x62,
        ["NumPad3"] = 0x63,
        ["NumPad4"] = 0x64,
        ["NumPad5"] = 0x65,
        ["NumPad6"] = 0x66,
        ["NumPad7"] = 0x67,
        ["NumPad8"] = 0x68,
        ["NumPad9"] = 0x69,
        ["F1"] = 0x70,
        ["F2"] = 0x71,
        ["F3"] = 0x72,
        ["F4"] = 0x73,
        ["F5"] = 0x74,
        ["F6"] = 0x75,
        ["F7"] = 0x76,
        ["F8"] = 0x77,
        ["F9"] = 0x78,
        ["F10"] = 0x79,
        ["F11"] = 0x7A,
        ["F12"] = 0x7B,
    };

    public static bool TryGet(string keyName, out ushort virtualKey)
    {
        if (string.IsNullOrWhiteSpace(keyName))
        {
            virtualKey = 0;
            return false;
        }

        if (ByName.TryGetValue(keyName, out virtualKey))
        {
            return true;
        }

        if (keyName.Length == 1)
        {
            var ch = char.ToUpperInvariant(keyName[0]);
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                virtualKey = ch;
                return true;
            }
        }

        virtualKey = 0;
        return false;
    }
}
