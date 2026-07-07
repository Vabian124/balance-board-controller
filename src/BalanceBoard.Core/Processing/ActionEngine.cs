using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;
using BalanceBoard.Core.Processing;

namespace BalanceBoard.Core.Processing;

/// <summary>
/// Portable action-slot state machine. Maps movement flags to bindings via <see cref="IInputBackend"/>.
/// Port to Python <c>action_engine.py</c> with a pluggable backend.
/// </summary>
public sealed class ActionEngine : IActionSimulator
{
    private readonly IInputBackend _backend;
    private readonly Dictionary<string, RuntimeAction> _actions = new();

    public ActionEngine(IInputBackend backend) => _backend = backend;

    public void Apply(ProcessedBalance data, AppSettings settings)
    {
        foreach (var slot in ActionSlots.All)
        {
            Set(slot, MovementMapper.IsActive(slot, data), settings.Actions[slot]);
        }
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
            runtime = new RuntimeAction(_backend, binding);
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
        private readonly IInputBackend _backend;
        private ActionBinding _binding;
        private bool _active;
        private readonly System.Timers.Timer _timer;

        public RuntimeAction(IInputBackend backend, ActionBinding binding)
        {
            _backend = backend;
            _binding = binding;
            _timer = new System.Timers.Timer(ActionConstants.MouseMoveIntervalMs) { AutoReset = true };
            _timer.Elapsed += (_, _) =>
            {
                if (_binding.Kind == ActionKind.MouseMoveX)
                {
                    _backend.MoveRelative(_binding.Amount, 0);
                }
                else if (_binding.Kind == ActionKind.MouseMoveY)
                {
                    _backend.MoveRelative(0, _binding.Amount);
                }
            };
        }

        public void UpdateBinding(ActionBinding binding) => _binding = binding;

        public void Start()
        {
            if (_active)
            {
                return;
            }

            _active = true;
            switch (_binding.Kind)
            {
                case ActionKind.Key when VirtualKeyCodes.TryGet(_binding.KeyName, out var key):
                    _backend.KeyDown(key);
                    break;
                case ActionKind.MouseButton:
                    _backend.MouseDown(_binding.MouseButton);
                    break;
                case ActionKind.MouseMoveX:
                case ActionKind.MouseMoveY:
                    _timer.Start();
                    break;
            }
        }

        public void Stop()
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            _timer.Stop();
            switch (_binding.Kind)
            {
                case ActionKind.Key when VirtualKeyCodes.TryGet(_binding.KeyName, out var key):
                    _backend.KeyUp(key);
                    break;
                case ActionKind.MouseButton:
                    _backend.MouseUp(_binding.MouseButton);
                    break;
            }
        }
    }
}
