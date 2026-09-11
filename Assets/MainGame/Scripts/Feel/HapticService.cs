using System.Collections;
using System.Runtime.InteropServices;
using Setting.Menu;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Plays haptics — phone vibration and gamepad rumble — for the whole game.
///
/// One <see cref="HapticPattern"/> in, the right buzz out on whatever the player is
/// holding. Every platform is answered by its own backend and the callers never know
/// which one ran:
///
///   • iOS      — the system Taptic engine, through the small native file that ships
///                beside this one (Assets/Plugins/iOS/FeelHaptics.mm). Real impact and
///                notification haptics, not the 400ms buzz Handheld.Vibrate gives.
///   • Android  — the vibration motor, driven with VibrationEffect waveforms so a light
///                tick really is lighter than a heavy one. Falls back to the old
///                amplitude-less vibrate() below API 26, and to Handheld.Vibrate if the
///                vibrator cannot be reached at all.
///   • Gamepad  — both rumble motors, wherever a pad is connected. This is also what
///                makes the whole system testable in the editor.
///
/// Everything routes through <see cref="Enabled"/>, which is stored in the player's own
/// settings file (<see cref="SettingsData.HapticsEnabled"/>) and so survives a restart.
/// A player who has turned vibration off gets silence from every call site without any of
/// them having to check.
///
/// Self-provisioning and persistent: the first call creates it, and it survives scene
/// loads so the Android vibrator handle and the settings read are done once per run.
/// </summary>
[DisallowMultipleComponent]
public class HapticService : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────

    private static HapticService s_Instance;
    private static bool s_Quitting;

    /// <summary>
    /// The live service, created on first use. Null only while the application is
    /// shutting down, so callers keep the null-conditional.
    /// </summary>
    public static HapticService Instance
    {
        get
        {
            if (s_Instance != null) return s_Instance;
            if (s_Quitting || !Application.isPlaying) return null;

            var holder = new GameObject("[HapticService]");
            s_Instance = holder.AddComponent<HapticService>();
            return s_Instance;
        }
    }

    // ─── Tuning ───────────────────────────────────────────────────────────────

    [Tooltip("Scales every haptic the game asks for. Below 1 softens the whole game's " +
             "vibration without any call site changing; 0 is the same as switching it off.")]
    [SerializeField, Range(0f, 1f)] private float m_GlobalStrength = 1f;

    [Tooltip("Shortest gap between two haptics, in unscaled seconds. A run of impacts in " +
             "the same frame — a brick shattering INTO a landing — would otherwise stack " +
             "into one long smear that reads as a malfunction rather than as two hits.")]
    [SerializeField] private float m_MinInterval = 0.04f;

    [Tooltip("Rumble strength for the pad's low-frequency (heavy) motor, as a fraction of " +
             "the pattern's amplitude. The two motors feel very different; the heavy one " +
             "carries the body of an impact.")]
    [SerializeField, Range(0f, 2f)] private float m_GamepadLowFrequency = 1f;

    [Tooltip("Rumble strength for the pad's high-frequency (light) motor. Kept under the " +
             "low motor so an impact reads as a thump rather than a buzz.")]
    [SerializeField, Range(0f, 2f)] private float m_GamepadHighFrequency = 0.65f;

    // ─── State ────────────────────────────────────────────────────────────────

    // One pulse of a pattern: wait Delay, then buzz for Duration at Amplitude.
    private readonly struct Pulse
    {
        public readonly int DelayMs;
        public readonly int DurationMs;
        public readonly float Amplitude;

        public Pulse(int delayMs, int durationMs, float amplitude)
        {
            DelayMs = delayMs;
            DurationMs = durationMs;
            Amplitude = amplitude;
        }
    }

    private bool m_Enabled = true;
    private float m_LastPlayTime = -999f;
    private Coroutine m_GamepadRoutine;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject m_Vibrator;
    private int m_AndroidSdk;
    private bool m_AndroidProbed;
#endif

#if UNITY_IOS && !UNITY_EDITOR
    // Implemented in Assets/Plugins/iOS/FeelHaptics.mm. That file and this branch belong
    // together: deleting one without the other breaks the iOS link step.
    [DllImport("__Internal")]
    private static extern void FeelHapticsPlay(int pattern);
#endif

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (s_Instance != null && s_Instance != this) { Destroy(gameObject); return; }
        s_Instance = this;
        DontDestroyOnLoad(gameObject);

        // Read once, here, rather than per call: the settings live in a JSON file on disk
        // and a haptic can fire several times a second.
        m_Enabled = SettingsSaveSystem.LoadSettings().HapticsEnabled;
    }

    private void OnDestroy()
    {
        if (s_Instance == this) s_Instance = null;
        StopGamepadRumble();
    }

    private void OnApplicationQuit() { s_Quitting = true; }

    private void OnApplicationPause(bool paused)
    {
        // A pad left rumbling as the app goes to the background keeps rumbling.
        if (paused) StopGamepadRumble();
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Whether haptics play at all. Persisted to the player's settings file on every
    /// change, so a settings screen only has to set this — there is nothing else to save.
    /// </summary>
    public bool Enabled
    {
        get => m_Enabled;
        set
        {
            if (m_Enabled == value) return;
            m_Enabled = value;

            if (!value) StopGamepadRumble();

            SettingsData settings = SettingsSaveSystem.LoadSettings();
            settings.HapticsEnabled = value;
            SettingsSaveSystem.SaveSettings(settings);
        }
    }

    /// <summary>
    /// Scales every haptic. Set from a settings slider if the game ever grows one; not
    /// persisted on its own, since <see cref="Enabled"/> is the switch players expect.
    /// </summary>
    public float GlobalStrength
    {
        get => m_GlobalStrength;
        set => m_GlobalStrength = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Plays one haptic pattern. Safe to call from anywhere, as often as you like:
    /// <see cref="HapticPattern.None"/>, a player who has switched vibration off, a device
    /// with no vibrator and a second call in the same frame all end here quietly.
    /// </summary>
    public void Play(HapticPattern pattern)
    {
        if (pattern == HapticPattern.None) return;
        if (!m_Enabled || m_GlobalStrength <= 0f) return;

        // Unscaled, because most haptics fire during a hit-stop where the game clock is
        // deliberately stopped.
        if (Time.unscaledTime - m_LastPlayTime < m_MinInterval) return;
        m_LastPlayTime = Time.unscaledTime;

        Pulse[] pulses = GetPulses(pattern);

        // The pad is driven on every platform: a phone with a controller attached should
        // rumble it as well as buzz.
        PlayOnGamepad(pulses);

#if UNITY_IOS && !UNITY_EDITOR
        // The Taptic engine owns the shaping of its own patterns, so iOS gets the intent
        // rather than the waveform.
        try { FeelHapticsPlay((int)pattern); } catch (System.EntryPointNotFoundException) { }
#elif UNITY_ANDROID && !UNITY_EDITOR
        PlayOnAndroid(pulses);
#endif
    }

    /// <summary>
    /// Plays a custom one-off buzz rather than a named pattern — for a caller that wants
    /// to scale a haptic with something continuous, like fall height.
    /// </summary>
    public void Play(float durationSeconds, float amplitude)
    {
        if (!m_Enabled || m_GlobalStrength <= 0f) return;
        if (durationSeconds <= 0f || amplitude <= 0f) return;
        if (Time.unscaledTime - m_LastPlayTime < m_MinInterval) return;
        m_LastPlayTime = Time.unscaledTime;

        var pulses = new[]
        {
            new Pulse(0, Mathf.RoundToInt(durationSeconds * 1000f), Mathf.Clamp01(amplitude)),
        };

        PlayOnGamepad(pulses);

#if UNITY_ANDROID && !UNITY_EDITOR
        PlayOnAndroid(pulses);
#endif
    }

    /// <summary>Cuts any haptic still running. Called on pause and on teardown.</summary>
    public void Stop()
    {
        StopGamepadRumble();

#if UNITY_ANDROID && !UNITY_EDITOR
        try { AndroidVibrator()?.Call("cancel"); } catch (System.Exception) { }
#endif
    }

    // ─── Patterns ─────────────────────────────────────────────────────────────

    // The waveform behind each pattern, already scaled by the global strength. Durations
    // are deliberately short: a phone motor takes a few milliseconds to spin up and down,
    // so anything under ~10ms is felt as nothing and anything over ~80ms stops reading as
    // an impact and starts reading as an alert.
    private Pulse[] GetPulses(HapticPattern pattern)
    {
        float s = m_GlobalStrength;

        switch (pattern)
        {
            case HapticPattern.Selection:    return new[] { new Pulse(0, 10, 0.35f * s) };
            case HapticPattern.LightImpact:  return new[] { new Pulse(0, 20, 0.45f * s) };
            case HapticPattern.MediumImpact: return new[] { new Pulse(0, 35, 0.70f * s) };
            case HapticPattern.HeavyImpact:  return new[] { new Pulse(0, 55, 1.00f * s) };

            // Short at full strength — the whole thing is over before the motor has
            // finished spinning up, which is what makes it read as a snap.
            case HapticPattern.RigidImpact:  return new[] { new Pulse(0, 14, 1.00f * s) };

            // The opposite trade: long enough to feel, weak enough to stay soft.
            case HapticPattern.SoftImpact:   return new[] { new Pulse(0, 65, 0.32f * s) };

            case HapticPattern.Success:
                return new[] { new Pulse(0, 30, 0.45f * s), new Pulse(80, 60, 1.00f * s) };

            case HapticPattern.Warning:
                return new[] { new Pulse(0, 45, 0.80f * s), new Pulse(70, 45, 0.80f * s) };

            case HapticPattern.Failure:
                return new[]
                {
                    new Pulse(0, 55, 1.00f * s),
                    new Pulse(55, 30, 0.50f * s),
                    new Pulse(45, 65, 1.00f * s),
                };

            default: return System.Array.Empty<Pulse>();
        }
    }

    // ─── Gamepad ──────────────────────────────────────────────────────────────

    private void PlayOnGamepad(Pulse[] pulses)
    {
        if (pulses == null || pulses.Length == 0) return;
        if (Gamepad.current == null) return;

        StopGamepadRumble();
        m_GamepadRoutine = StartCoroutine(GamepadRoutine(pulses));
    }

    // Unscaled throughout: a rumble that is meant to punctuate a hit-stop must not be
    // frozen by the very hit-stop it is punctuating.
    private IEnumerator GamepadRoutine(Pulse[] pulses)
    {
        foreach (Pulse pulse in pulses)
        {
            if (pulse.DelayMs > 0)
            {
                Gamepad.current?.SetMotorSpeeds(0f, 0f);
                yield return new WaitForSecondsRealtime(pulse.DelayMs / 1000f);
            }

            // Re-read every pulse: the pad can be unplugged mid-pattern.
            Gamepad pad = Gamepad.current;
            if (pad == null) break;

            pad.SetMotorSpeeds(
                Mathf.Clamp01(pulse.Amplitude * m_GamepadLowFrequency),
                Mathf.Clamp01(pulse.Amplitude * m_GamepadHighFrequency));

            yield return new WaitForSecondsRealtime(pulse.DurationMs / 1000f);
        }

        m_GamepadRoutine = null;
        StopGamepadRumble();
    }

    private void StopGamepadRumble()
    {
        if (m_GamepadRoutine != null)
        {
            StopCoroutine(m_GamepadRoutine);
            m_GamepadRoutine = null;
        }

        // ResetHaptics rather than SetMotorSpeeds(0,0): it also clears any pause state the
        // Input System is holding, which a plain zero does not.
        Gamepad.current?.ResetHaptics();
    }

    // ─── Android ──────────────────────────────────────────────────────────────

#if UNITY_ANDROID && !UNITY_EDITOR
    private void PlayOnAndroid(Pulse[] pulses)
    {
        if (pulses == null || pulses.Length == 0) return;

        AndroidJavaObject vibrator = AndroidVibrator();
        if (vibrator == null)
        {
            // Nothing tuned about it, but better than a silent device.
            Handheld.Vibrate();
            return;
        }

        try
        {
            // A waveform is timings and amplitudes in lockstep, alternating silence and
            // buzz: [delay, duration, delay, duration, ...] against [0, amp, 0, amp, ...].
            // Building the whole pattern as one effect hands the timing to the OS instead
            // of trying to hold it from a coroutine, which is the difference between a
            // crisp double tap and two vaguely related buzzes.
            long[] timings = new long[pulses.Length * 2];
            int[] amplitudes = new int[pulses.Length * 2];

            for (int i = 0; i < pulses.Length; i++)
            {
                timings[i * 2] = pulses[i].DelayMs;
                amplitudes[i * 2] = 0;
                timings[i * 2 + 1] = pulses[i].DurationMs;
                // Android's scale is 1..255, and 0 means "off" — so a quiet pulse has to
                // floor at 1 rather than round down into silence.
                amplitudes[i * 2 + 1] =
                    Mathf.Clamp(Mathf.RoundToInt(pulses[i].Amplitude * 255f), 1, 255);
            }

            if (m_AndroidSdk >= 26)
            {
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
                           "createWaveform", timings, amplitudes, -1))
                {
                    vibrator.Call("vibrate", effect);
                }
                return;
            }

            // Pre-Oreo there is no amplitude control at all — only on/off timings, so a
            // light tick and a heavy thump differ solely in how long they run.
            vibrator.Call("vibrate", timings, -1);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[HapticService] Android vibration failed ({e.GetType().Name}). " +
                             "Check that android.permission.VIBRATE is in the manifest.");
            m_Vibrator = null;
        }
    }

    // Resolved once and cached. A device with no vibrator, or a manifest missing the
    // VIBRATE permission, leaves this null and every later call falls back quietly.
    private AndroidJavaObject AndroidVibrator()
    {
        if (m_AndroidProbed) return m_Vibrator;
        m_AndroidProbed = true;

        try
        {
            using (var versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
                m_AndroidSdk = versionClass.GetStatic<int>("SDK_INT");

            using (var playerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity =
                       playerClass.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                m_Vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }

            if (m_Vibrator != null && !m_Vibrator.Call<bool>("hasVibrator"))
                m_Vibrator = null;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[HapticService] No Android vibrator ({e.GetType().Name}).");
            m_Vibrator = null;
        }

        return m_Vibrator;
    }
#endif
}
