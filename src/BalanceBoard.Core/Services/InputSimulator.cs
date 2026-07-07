using System.Runtime.InteropServices;
using System.Windows.Forms;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Services;

public sealed class InputSimulator
{
    [DllImport("user32.dll")]
    private static extern int SendInput(int cInputs, ref INPUT pInputs, int cbSize);

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

    private readonly Dictionary<string, RuntimeAction> _actions = new();

    public void Apply(ProcessedBalance data, AppSettings settings)
    {
        Set("Left", data.MoveLeft, settings.Actions["Left"]);
        Set("Right", data.MoveRight, settings.Actions["Right"]);
        Set("Forward", data.MoveForward, settings.Actions["Forward"]);
        Set("Backward", data.MoveBackward, settings.Actions["Backward"]);
        Set("Modifier", data.Modifier, settings.Actions["Modifier"]);
        Set("Jump", data.Jump, settings.Actions["Jump"]);
        Set("DiagonalLeft", data.DiagonalLeft, settings.Actions["DiagonalLeft"]);
        Set("DiagonalRight", data.DiagonalRight, settings.Actions["DiagonalRight"]);
    }

    public void ReleaseAll()
    {
        foreach (var pair in _actions.ToList())
        {
            pair.Value.Stop();
        }
    }

    private void Set(string name, bool active, ActionBinding binding)
    {
        if (!_actions.TryGetValue(name, out var runtime))
        {
            runtime = new RuntimeAction(binding);
            _actions[name] = runtime;
        }
        else
        {
            runtime.UpdateBinding(binding);
        }

        if (active)
        {
            runtime.Start();
        }
        else
        {
            runtime.Stop();
        }
    }

    private sealed class RuntimeAction
    {
        private ActionBinding _binding;
        private bool _active;
        private readonly System.Timers.Timer _timer = new(2) { AutoReset = true };

        public RuntimeAction(ActionBinding binding)
        {
            _binding = binding;
            _timer.Elapsed += (_, _) =>
            {
                if (_binding.Kind == ActionKind.MouseMoveX) MoveRelative(_binding.Amount, 0);
                else if (_binding.Kind == ActionKind.MouseMoveY) MoveRelative(0, _binding.Amount);
            };
        }

        public void UpdateBinding(ActionBinding binding) => _binding = binding;

        public void Start()
        {
            if (_active) return;
            _active = true;
            switch (_binding.Kind)
            {
                case ActionKind.Key when Enum.TryParse<Keys>(_binding.KeyName, out var key):
                    KeyDown(key);
                    break;
                case ActionKind.MouseButton:
                    MouseDown(_binding.MouseButton);
                    break;
                case ActionKind.MouseMoveX:
                case ActionKind.MouseMoveY:
                    _timer.Start();
                    break;
            }
        }

        public void Stop()
        {
            if (!_active) return;
            _active = false;
            _timer.Stop();
            switch (_binding.Kind)
            {
                case ActionKind.Key when Enum.TryParse<Keys>(_binding.KeyName, out var key):
                    KeyUp(key);
                    break;
                case ActionKind.MouseButton:
                    MouseUp(_binding.MouseButton);
                    break;
            }
        }
    }

    private static void KeyDown(Keys key)
    {
        var scan = (ushort)MapVirtualKey((uint)key, 0);
        var input = new INPUT
        {
            dwType = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wScan = scan, dwFlags = KEYEVENTF_SCANCODE } }
        };
        SendInput(1, ref input, Marshal.SizeOf<INPUT>());
    }

    private static void KeyUp(Keys key)
    {
        var scan = (ushort)MapVirtualKey((uint)key, 0);
        var input = new INPUT
        {
            dwType = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wScan = scan, dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP } }
        };
        SendInput(1, ref input, Marshal.SizeOf<INPUT>());
    }

    private static void MouseDown(string button)
    {
        if (!TryGetMouseButton(button, down: true, out var flag, out var data))
        {
            return;
        }

        var input = new INPUT
        {
            dwType = INPUT_MOUSE,
            U = new InputUnion { mi = new MOUSEINPUT { dwFlags = flag, mouseData = data } }
        };
        SendInput(1, ref input, Marshal.SizeOf<INPUT>());
    }

    private static void MouseUp(string button)
    {
        if (!TryGetMouseButton(button, down: false, out var flag, out var data))
        {
            return;
        }

        var input = new INPUT
        {
            dwType = INPUT_MOUSE,
            U = new InputUnion { mi = new MOUSEINPUT { dwFlags = flag, mouseData = data } }
        };
        SendInput(1, ref input, Marshal.SizeOf<INPUT>());
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

    private static void MoveRelative(int x, int y)
    {
        var input = new INPUT
        {
            dwType = INPUT_MOUSE,
            U = new InputUnion { mi = new MOUSEINPUT { dx = x, dy = y, dwFlags = MOUSEEVENTF_MOVE } }
        };
        SendInput(1, ref input, Marshal.SizeOf<INPUT>());
    }

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);
}
