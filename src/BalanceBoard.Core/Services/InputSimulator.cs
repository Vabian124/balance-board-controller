using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;

namespace BalanceBoard.Core.Services;

/// <summary>
/// Windows keyboard/mouse simulator — thin facade over portable <see cref="ActionEngine"/>.
/// </summary>
public sealed class InputSimulator : IActionSimulator
{
    private readonly ActionEngine _engine;

    public InputSimulator()
        : this(new Win32InputBackend())
    {
    }

    public InputSimulator(IInputBackend backend) => _engine = new ActionEngine(backend);

    public void Apply(ProcessedBalance data, AppSettings settings) => _engine.Apply(data, settings);

    public void ReleaseAll() => _engine.ReleaseAll();
}
