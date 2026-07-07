using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Abstractions;

/// <summary>
/// Applies movement flags to keyboard/mouse bindings. Core logic lives in <see cref="Processing.ActionEngine"/>.
/// </summary>
public interface IActionSimulator
{
    void Apply(ProcessedBalance data, AppSettings settings);

    void ReleaseAll();
}
