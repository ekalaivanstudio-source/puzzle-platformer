using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HomeUI
{
    /// <summary>
    /// The controls category of the settings system. Owns mouse sensitivity, controller vibration,
    /// and keyboard/controller rebinding for an <see cref="InputActionAsset"/> (the project's input
    /// actions). Rebinds are persisted as the asset's own override JSON, so they survive restarts.
    ///
    /// Gameplay reads <see cref="MouseSensitivity"/> when applying look/aim, and calls
    /// <see cref="Rumble"/> for haptics; both honour the player's saved preferences.
    ///
    /// Requires the Input System package (this project ships 1.19.0).
    /// </summary>
    public class InputManager : MonoBehaviour, ISettingsModule
    {
        [Tooltip("The project's Input Actions asset that rebinding operates on.")]
        [SerializeField] private InputActionAsset m_Actions;

        /// <summary>Raised whenever bindings change (rebind committed or reset), so settings can save.</summary>
        public event Action OnBindingsChanged;

        /// <summary>Current mouse sensitivity multiplier; gameplay multiplies look delta by this.</summary>
        public float MouseSensitivity { get; private set; } = 1f;

        /// <summary>Whether controller rumble is allowed.</summary>
        public bool VibrationEnabled { get; private set; } = true;

        private InputActionRebindingExtensions.RebindingOperation m_Rebind;
        private Coroutine m_RumbleRoutine;

        public void Apply(SettingsData data)
        {
            MouseSensitivity = data.MouseSensitivity;
            VibrationEnabled = data.ControllerVibration;

            if (m_Actions == null) return;
            if (string.IsNullOrEmpty(data.InputBindingOverridesJson))
                m_Actions.RemoveAllBindingOverrides();
            else
                m_Actions.LoadBindingOverridesFromJson(data.InputBindingOverridesJson);
        }

        /// <summary>Serialized binding overrides for saving into <see cref="SettingsData"/>.</summary>
        public string GetBindingOverridesJson() =>
            m_Actions != null ? m_Actions.SaveBindingOverridesAsJson() : "";

        /// <summary>Human-readable label for a binding, e.g. "W" or "Button South" (drives rebind buttons).</summary>
        public string GetBindingDisplayString(string actionName, int bindingIndex)
        {
            InputAction action = m_Actions != null ? m_Actions.FindAction(actionName) : null;
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return "";
            return action.GetBindingDisplayString(bindingIndex);
        }

        /// <summary>
        /// Starts listening for the next control the player presses and rebinds the given binding to
        /// it. Works for keyboard and controller alike. Escape cancels. <paramref name="onComplete"/>
        /// fires for both success and cancel so the UI can refresh its label.
        /// </summary>
        public void StartInteractiveRebind(string actionName, int bindingIndex, Action onComplete)
        {
            InputAction action = m_Actions != null ? m_Actions.FindAction(actionName) : null;
            if (action == null)
            {
                Debug.LogError($"[InputManager] Action '{actionName}' not found.");
                onComplete?.Invoke();
                return;
            }

            m_Rebind?.Dispose();
            action.Disable(); // required while rebinding

            m_Rebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op => FinishRebind(action, onComplete, committed: true))
                .OnCancel(op => FinishRebind(action, onComplete, committed: false))
                .Start();
        }

        private void FinishRebind(InputAction action, Action onComplete, bool committed)
        {
            m_Rebind?.Dispose();
            m_Rebind = null;
            action.Enable();
            if (committed) OnBindingsChanged?.Invoke();
            onComplete?.Invoke();
        }

        /// <summary>Clears all rebinds back to the asset defaults and notifies listeners to save.</summary>
        public void ResetBindings()
        {
            if (m_Actions == null) return;
            m_Actions.RemoveAllBindingOverrides();
            OnBindingsChanged?.Invoke();
        }

        // ─── Haptics ────────────────────────────────────────────────────────────

        /// <summary>
        /// Rumbles the current gamepad if vibration is enabled. Safe to call when no pad is connected.
        /// </summary>
        public void Rumble(float lowFrequency = 0.4f, float highFrequency = 0.6f, float duration = 0.2f)
        {
            if (!VibrationEnabled || Gamepad.current == null) return;
            if (m_RumbleRoutine != null) StopCoroutine(m_RumbleRoutine);
            m_RumbleRoutine = StartCoroutine(RumbleRoutine(lowFrequency, highFrequency, duration));
        }

        private IEnumerator RumbleRoutine(float low, float high, float duration)
        {
            Gamepad pad = Gamepad.current;
            pad.SetMotorSpeeds(low, high);
            yield return new WaitForSecondsRealtime(duration);
            pad.SetMotorSpeeds(0f, 0f);
            m_RumbleRoutine = null;
        }

        private void OnDisable()
        {
            m_Rebind?.Dispose();
            m_Rebind = null;
            if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
        }
    }
}
