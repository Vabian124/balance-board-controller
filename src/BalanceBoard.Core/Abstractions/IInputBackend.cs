namespace BalanceBoard.Core.Abstractions;

/// <summary>
/// Platform adapter for keyboard/mouse injection. Implement with Win32 SendInput (.NET) or pynput (Python).
/// </summary>
public interface IInputBackend
{
    void KeyDown(ushort virtualKey);

    void KeyUp(ushort virtualKey);

    void MouseDown(string button);

    void MouseUp(string button);

    void MoveRelative(int deltaX, int deltaY);
}
