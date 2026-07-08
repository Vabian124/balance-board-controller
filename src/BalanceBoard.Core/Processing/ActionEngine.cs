using BalanceBoard.Core.Abstractions;
using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Processing;

/// <summary>
/// Portable action-slot state machine. Maps movement flags to bindings via <see cref="IInputBackend"/>.
/// Port to Python <c>action_engine.py</c> with a pluggable backend.
/// </summary>
public sealed class ActionEngine(IInputBackend backend) : IActionSimulator
{
    private readonly IInputBackend _backend = backend;
    private readonly Dictionary<string, RuntimeAction> _actions = new();

    public void Apply(ProcessedBalance data, AppSettings settings)
    {
        var movementEnabled = !settings.DisableKeyboardActions;
        if (movementEnabled)
        {
            foreach (var slot in ActionSlots.Movement)
            {
                Set(slot, MovementMapper.IsActive(slot, data), settings.Actions[slot], MovementMapper.SlotIntensity(slot, data));
            }
        }
        else
        {
            foreach (var slot in ActionSlots.Movement)
            {
                Set(slot, false, settings.Actions[slot], 0f);
            }
        }

        if (settings.Actions.TryGetValue(ActionSlots.BoardButton, out var boardBinding)
            && boardBinding.Kind == ActionKind.Key)
        {
            Set(ActionSlots.BoardButton, data.ButtonA, boardBinding, 1f);
        }
        else
        {
            Set(ActionSlots.BoardButton, false, new ActionBinding(), 0f);
        }
    }

    public void ReleaseAll()
    {
        foreach (var pair in _actions.ToList())
        {
            pair.Value.Stop();
        }
    }

    private void Set(string name, bool active, ActionBinding binding, float intensity)
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

        runtime.SetIntensity(intensity);

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
        private float _intensity = 1f;
        private readonly System.Timers.Timer _timer;

        public RuntimeAction(IInputBackend backend, ActionBinding binding)
        {
            _backend = backend;
            _binding = binding;
            _timer = new System.Timers.Timer(ActionConstants.MouseMoveIntervalMs) { AutoReset = true };
            _timer.Elapsed += (_, _) =>
            {
                var amount = MovementMapper.ScaleMouseAmount(_binding.Amount, _intensity);
                if (_binding.Kind == ActionKind.MouseMoveX)
                {
                    _backend.MoveRelative(amount, 0);
                }
                else if (_binding.Kind == ActionKind.MouseMoveY)
                {
                    _backend.MoveRelative(0, amount);
                }
            };
        }

        public void SetIntensity(float intensity) => _intensity = intensity;

        /// <summary>
        /// Swaps the binding. If the slot is currently held down and the new binding drives a
        /// different physical output (key/mouse button), release the old output first — otherwise
        /// the previously pressed key/button would stay stuck down forever (never released, since
        /// <see cref="Stop"/> would only know about the new binding).
        /// </summary>
        public void UpdateBinding(ActionBinding binding)
        {
            if (_active && RequiresRebind(_binding, binding))
            {
                ReleasePhysical(_binding);
                _binding = binding;
                EngagePhysical(binding);
                return;
            }

            _binding = binding;
        }

        private static bool RequiresRebind(ActionBinding oldBinding, ActionBinding newBinding)
        {
            if (oldBinding.Kind != newBinding.Kind)
            {
                return IsHeldOutput(oldBinding.Kind) || IsHeldOutput(newBinding.Kind);
            }

            return oldBinding.Kind switch
            {
                ActionKind.Key => oldBinding.KeyName != newBinding.KeyName,
                ActionKind.MouseButton => oldBinding.MouseButton != newBinding.MouseButton,
                _ => false,
            };
        }

        private static bool IsHeldOutput(ActionKind kind) => kind is ActionKind.Key or ActionKind.MouseButton;

        public void Start()
        {
            if (_active)
            {
                return;
            }

            _active = true;
            EngagePhysical(_binding);
        }

        public void Stop()
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            ReleasePhysical(_binding);
        }

        private void EngagePhysical(ActionBinding binding)
        {
            switch (binding.Kind)
            {
                case ActionKind.Key when VirtualKeyCodes.TryGet(binding.KeyName, out var key):
                    _backend.KeyDown(key);
                    break;
                case ActionKind.MouseButton:
                    _backend.MouseDown(binding.MouseButton);
                    break;
                case ActionKind.MouseMoveX:
                case ActionKind.MouseMoveY:
                    _timer.Start();
                    break;
            }
        }

        private void ReleasePhysical(ActionBinding binding)
        {
            _timer.Stop();
            switch (binding.Kind)
            {
                case ActionKind.Key when VirtualKeyCodes.TryGet(binding.KeyName, out var key):
                    _backend.KeyUp(key);
                    break;
                case ActionKind.MouseButton:
                    _backend.MouseUp(binding.MouseButton);
                    break;
            }
        }
    }
}
