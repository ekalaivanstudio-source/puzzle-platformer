using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomeUI
{
    /// <summary>
    /// Coordinates the settings system: loads <see cref="SettingsData"/> on startup, hands it to
    /// every <see cref="ISettingsModule"/> to apply, and saves changes. It knows nothing about
    /// individual settings — it just orchestrates load → apply → save, so categories stay
    /// independent and new ones plug in without editing this class.
    ///
    /// Place it on a GameObject whose children include the category managers (Graphics, Audio,
    /// Input). Optionally mark it persistent so one instance survives scene loads and keeps the
    /// player's choices applied everywhere.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        private const string SaveFileName = "settings.json";

        public static SettingsManager Instance { get; private set; }

        [Tooltip("Designer defaults used on first run and on reset.")]
        [SerializeField] private SettingsDefaults m_Defaults;

        [Tooltip("Category managers. Found automatically among children if left empty.")]
        [SerializeField] private List<MonoBehaviour> m_ModuleBehaviours = new List<MonoBehaviour>();

        [Tooltip("Direct references the UI uses (resolution list / rebinding). Optional.")]
        [SerializeField] private GraphicsManager m_Graphics;
        [SerializeField] private InputManager m_Input;

        [Tooltip("Survive scene loads so settings stay applied across the whole game.")]
        [SerializeField] private bool m_Persist = true;

        private readonly List<ISettingsModule> m_Modules = new List<ISettingsModule>();

        /// <summary>Raised after settings are (re)applied, so any open UI can refresh.</summary>
        public event Action OnSettingsApplied;

        /// <summary>The live settings object. Mutate via the Set* helpers (which save), not directly.</summary>
        public SettingsData Data { get; private set; }

        public GraphicsManager Graphics => m_Graphics;
        public InputManager Input => m_Input;

        private void Awake()
        {
            if (m_Persist)
            {
                if (Instance != null && Instance != this) { Destroy(gameObject); return; }
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                // Re-apply on every scene load so each scene's (per-scene) AudioManager etc.
                // pick up the saved settings rather than their own Inspector defaults.
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            else
            {
                Instance = this;
            }

            CollectModules();
            Load();
            ApplyAll();

            if (m_Input != null) m_Input.OnBindingsChanged += OnBindingsChanged;
        }

        private void OnDestroy()
        {
            if (m_Input != null) m_Input.OnBindingsChanged -= OnBindingsChanged;
            if (m_Persist) SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyAll();

        private void CollectModules()
        {
            m_Modules.Clear();

            if (m_ModuleBehaviours.Count > 0)
            {
                foreach (MonoBehaviour b in m_ModuleBehaviours)
                    if (b is ISettingsModule m) m_Modules.Add(m);
            }
            else
            {
                // Open/closed: every ISettingsModule under this object is applied, no edits needed.
                foreach (ISettingsModule m in GetComponentsInChildren<ISettingsModule>(includeInactive: true))
                    m_Modules.Add(m);
            }

            if (m_Graphics == null) m_Graphics = GetComponentInChildren<GraphicsManager>(true);
            if (m_Input == null) m_Input = GetComponentInChildren<InputManager>(true);
        }

        // ─── Load / Save / Apply ──────────────────────────────────────────────────

        /// <summary>Loads the save file, or the designer defaults on first run.</summary>
        public void Load()
        {
            SettingsData fallback = m_Defaults != null ? m_Defaults.CreateDefaultData() : new SettingsData();
            Data = JsonSaveUtility.Load(SaveFileName, fallback);
        }

        /// <summary>Persists the current data (snapshotting input overrides first).</summary>
        public void Save()
        {
            if (m_Input != null) Data.InputBindingOverridesJson = m_Input.GetBindingOverridesJson();
            JsonSaveUtility.Save(SaveFileName, Data);
        }

        /// <summary>Applies every category to the engine and notifies listeners.</summary>
        public void ApplyAll()
        {
            for (int i = 0; i < m_Modules.Count; i++)
                m_Modules[i].Apply(Data);
            OnSettingsApplied?.Invoke();
        }

        /// <summary>Apply + save in one call. UI calls this after changing any value.</summary>
        public void ApplyAndSave()
        {
            ApplyAll();
            Save();
        }

        /// <summary>Restores designer defaults (including input bindings) and saves.</summary>
        public void ResetToDefaults()
        {
            Data = m_Defaults != null ? m_Defaults.CreateDefaultData() : new SettingsData();
            if (m_Input != null) m_Input.ResetBindings();
            ApplyAndSave();
        }

        private void OnBindingsChanged()
        {
            // A rebind/reset happened — capture and persist immediately.
            Data.InputBindingOverridesJson = m_Input.GetBindingOverridesJson();
            JsonSaveUtility.Save(SaveFileName, Data);
        }
    }
}
