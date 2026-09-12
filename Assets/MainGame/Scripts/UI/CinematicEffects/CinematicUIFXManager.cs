using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Feedback;
using MainGame.UI.Unified;

namespace MainGame.UI.CinematicEffects
{
    /// <summary>
    /// Central manager and service locator for the Cinematic Living UI Effect System.
    /// Coordinates the 5-level visual hierarchy (Levels 0-4), event bus, and modular controllers:
    /// - Level 0: UIAmbientFX (Living breathing atmosphere)
    /// - Level 1: UIFocusFX (Navigation & selection outline pulse)
    /// - Level 2: UIConfirmFX (Physical punch, shockwaves & particle ejection)
    /// - Level 3: UIImpactFX & CinematicUIShaderController (Panel reveal, traveling current & kinetic impact)
    /// - Level 4: UIScreenTransitionFX (Screen transformation & transition bridge)
    /// Plus UICollectionScanner for character inspection.
    /// </summary>
    [DisallowMultipleComponent]
    public class CinematicUIFXManager : MonoBehaviour
    {
        private static CinematicUIFXManager s_Instance;

        public static CinematicUIFXManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindAnyObjectByType<CinematicUIFXManager>();
                    if (s_Instance == null)
                    {
                        Canvas rootCanvas = FindAnyObjectByType<Canvas>();
                        Transform parent = rootCanvas != null ? rootCanvas.transform : null;

                        GameObject go = new GameObject("CinematicUIFXManager", typeof(RectTransform), typeof(CinematicUIFXManager));
                        if (parent != null)
                        {
                            go.transform.SetParent(parent, false);
                            go.transform.SetAsLastSibling();
                            RectTransform rt = go.GetComponent<RectTransform>();
                            rt.anchorMin = Vector2.zero;
                            rt.anchorMax = Vector2.one;
                            rt.sizeDelta = Vector2.zero;
                            rt.anchoredPosition = Vector2.zero;
                        }
                        s_Instance = go.GetComponent<CinematicUIFXManager>();
                    }
                }
                return s_Instance;
            }
        }

        #region Modular Controllers
        [Header("Modular FX Controllers")]
        [SerializeField] private UIImpactFX m_ImpactFX;
        [SerializeField] private UIFocusFX m_FocusFX;
        [SerializeField] private UIConfirmFX m_ConfirmFX;
        [SerializeField] private UIAmbientFX m_AmbientFX;
        [SerializeField] private UIScreenTransitionFX m_TransitionFX;
        [SerializeField] private UITransitionFX m_ProfileTransitionFX;
        [SerializeField] private UICollectionScanner m_CollectionScanner;

        public UIImpactFX ImpactFX => m_ImpactFX;
        public UIFocusFX FocusFX => m_FocusFX;
        public UIConfirmFX ConfirmFX => m_ConfirmFX;
        public UIAmbientFX AmbientFX => m_AmbientFX;
        public UIScreenTransitionFX TransitionFX => m_TransitionFX;
        public UITransitionFX ProfileTransitionFX => m_ProfileTransitionFX;
        public UICollectionScanner CollectionScanner => m_CollectionScanner;
        #endregion

        #region Unified 14-Event Bus
        // 1. OnFXStart: Dispatched whenever any cinematic FX begins
        public event Action<Component, ScreenFXType> OnFXStart;
        // 2. OnEnergyStart: Dispatched when energy flow begins charging
        public event Action<Component, Color> OnEnergyStart;
        // 3. OnEnergyPeak: Dispatched when energy charges reach maximum luminance
        public event Action<Component, Color> OnEnergyPeak;
        // 4. OnEnergyDischarge: Dispatched on kinetic electric release
        public event Action<Component, Vector2, Color> OnEnergyDischarge;
        // 5. OnScanStart: Dispatched when surface scanning beam begins
        public event Action<Component, float> OnScanStart;
        // 6. OnScanComplete: Dispatched when scanner finishes passing element
        public event Action<Component> OnScanComplete;
        // 7. OnPixelReveal: Dispatched during surface pixel reconstruction/noise reveal
        public event Action<Component, float> OnPixelReveal;
        // 8. OnPixelDissolve: Dispatched during surface pixel dissolution
        public event Action<Component, float> OnPixelDissolve;
        // 9. OnParticleBurst: Dispatched when particle systems eject bursts
        public event Action<Component, Vector2, UIParticleType, int, Color> OnParticleBurst;
        // 10. OnImpact: Dispatched on physical kinetic landing/slam
        public event Action<Component, float, Vector2> OnImpact;
        // 11. OnNodeActivate: Dispatched when level route energy contacts a node
        public event Action<int, Vector2> OnNodeActivate;
        // 12. OnRobotScan: Dispatched when collection inspection scanner passes robot core
        public event Action<Component, int> OnRobotScan;
        // 13. OnTransitionStart: Dispatched on screen mode change start
        public event Action<UIScreen, UIScreen> OnTransitionStart;
        // 14. OnTransitionComplete: Dispatched on screen mode change completion
        public event Action<UIScreen> OnTransitionComplete;

        // Backward compatibility
        public event Action<Component> OnPanelActivate;
        public event Action<Component, bool> OnFocusChanged;
        public event Action<UIScreen, UIScreen> OnScreenTransitionStart;
        public event Action<UIScreen> OnScreenTransitionComplete;
        #endregion

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;

            EnsureComponents();
        }

        private void EnsureComponents()
        {
            if (m_ImpactFX == null) m_ImpactFX = GetComponent<UIImpactFX>() ?? gameObject.AddComponent<UIImpactFX>();
            if (m_FocusFX == null) m_FocusFX = GetComponent<UIFocusFX>() ?? gameObject.AddComponent<UIFocusFX>();
            if (m_ConfirmFX == null) m_ConfirmFX = GetComponent<UIConfirmFX>() ?? gameObject.AddComponent<UIConfirmFX>();
            if (m_AmbientFX == null) m_AmbientFX = GetComponent<UIAmbientFX>() ?? gameObject.AddComponent<UIAmbientFX>();
            if (m_TransitionFX == null) m_TransitionFX = GetComponent<UIScreenTransitionFX>() ?? gameObject.AddComponent<UIScreenTransitionFX>();
            if (m_ProfileTransitionFX == null) m_ProfileTransitionFX = GetComponent<UITransitionFX>() ?? gameObject.AddComponent<UITransitionFX>();
            if (m_CollectionScanner == null) m_CollectionScanner = GetComponent<UICollectionScanner>() ?? gameObject.AddComponent<UICollectionScanner>();
        }

        #region Event Triggers
        public void TriggerFXStart(Component source, ScreenFXType type)
        {
            OnFXStart?.Invoke(source, type);
        }

        public void TriggerEnergyStart(Component source, Color color)
        {
            OnEnergyStart?.Invoke(source, color);
        }

        public void TriggerEnergyPeak(Component source, Color color)
        {
            OnEnergyPeak?.Invoke(source, color);
        }

        public void TriggerEnergyDischarge(Component source, Vector2 pos, Color color)
        {
            OnEnergyDischarge?.Invoke(source, pos, color);
            UIParticleFX.Sparks(pos, source != null ? source.transform : transform, color, 4, 20f);
        }

        public void TriggerScanStart(Component source, float duration)
        {
            OnScanStart?.Invoke(source, duration);
        }

        public void TriggerScanComplete(Component source)
        {
            OnScanComplete?.Invoke(source);
        }

        public void TriggerPixelReveal(Component source, float progress)
        {
            OnPixelReveal?.Invoke(source, progress);
        }

        public void TriggerPixelDissolve(Component source, float progress)
        {
            OnPixelDissolve?.Invoke(source, progress);
        }

        public void TriggerParticleBurst(Component source, Vector2 pos, UIParticleType type, int count, Color color)
        {
            OnParticleBurst?.Invoke(source, pos, type, count, color);
            UIParticleFX.GenericBurst(type, pos, source != null ? source.transform : transform, color, count);
        }

        public void TriggerImpact(Component source, float intensity, Vector2 pos)
        {
            OnImpact?.Invoke(source, intensity, pos);
            RectTransform rt = source as RectTransform ?? source?.GetComponent<RectTransform>();
            if (rt != null)
            {
                m_ImpactFX.PlayImpact(rt, intensity);
            }
        }

        public void TriggerNodeActivate(int nodeIndex, Vector2 position)
        {
            OnNodeActivate?.Invoke(nodeIndex, position);
        }

        public void TriggerRobotScan(Component source, int robotIndex)
        {
            OnRobotScan?.Invoke(source, robotIndex);
        }

        public void TriggerPanelActivate(Component source)
        {
            OnPanelActivate?.Invoke(source);
            RectTransform rt = source as RectTransform ?? source?.GetComponent<RectTransform>();
            if (rt != null)
            {
                PlayPanelRevealFX(rt);
            }
        }

        public void TriggerFocusChanged(Component source, bool focused)
        {
            OnFocusChanged?.Invoke(source, focused);
            RectTransform rt = source as RectTransform ?? source?.GetComponent<RectTransform>();
            if (rt != null)
            {
                if (focused) m_FocusFX.PlayFocus(rt);
                else m_FocusFX.ResetFocus(rt);
            }
        }

        public void TriggerScreenTransitionStart(UIScreen from, UIScreen to)
        {
            OnTransitionStart?.Invoke(from, to);
            OnScreenTransitionStart?.Invoke(from, to);
            if (m_AmbientFX != null)
            {
                m_AmbientFX.SetPaused(true);
            }
        }

        public void TriggerScreenTransitionComplete(UIScreen to)
        {
            OnTransitionComplete?.Invoke(to);
            OnScreenTransitionComplete?.Invoke(to);
            if (m_AmbientFX != null)
            {
                m_AmbientFX.SetPaused(false);
            }
        }
        #endregion

        #region High-Level Facade Methods
        public void PlayFocusFX(RectTransform target, RectTransform pointer = null, bool isDestructive = false)
        {
            EnsureComponents();
            m_FocusFX.PlayFocus(target, pointer, isDestructive);
        }

        public void PlayConfirmFX(RectTransform target, bool isDestructive = false, Action onComplete = null)
        {
            EnsureComponents();
            m_ConfirmFX.PlayConfirm(target, isDestructive, onComplete);
        }

        public void PlayImpactFX(RectTransform target, float intensity = 1.0f, Vector2? dir = null, Color? color = null, Action onComplete = null)
        {
            EnsureComponents();
            m_ImpactFX.PlayImpact(target, intensity, dir, color, onComplete);
        }

        public void PlayPanelRevealFX(RectTransform panel, Action onComplete = null)
        {
            if (panel == null)
            {
                onComplete?.Invoke();
                return;
            }

            CinematicUIEffect effect = panel.GetComponent<CinematicUIEffect>() ?? panel.GetComponentInChildren<CinematicUIEffect>();
            if (effect != null)
            {
                effect.PlayActivationSweep(0.28f, new Color(1f, 1f, 1f, 0.7f), 45f, onComplete);
                effect.TriggerBorderPulse(0.30f, new Color(0.35f, 0.85f, 1f, 1f), 1.8f);
            }
            else
            {
                onComplete?.Invoke();
            }

            UIParticleFX.ScreenEdgeWave(panel, new Color(0.35f, 0.85f, 1f, 0.8f), 2);
        }

        public void PlayTransitionBridge(RectTransform fromScreen, RectTransform toScreen, Action onMidpoint, Action onComplete)
        {
            EnsureComponents();
            m_TransitionFX.PlayModeChangeBridge(fromScreen, toScreen, onMidpoint, onComplete);
        }

        public void PlayProfileTransition(UIScreen fromScreen, UIScreen toScreen, ScreenFXType fromType, ScreenFXType toType, Action onMidpoint, Action onComplete)
        {
            EnsureComponents();
            if (m_ProfileTransitionFX != null)
            {
                m_ProfileTransitionFX.PlayProfileTransition(fromScreen, toScreen, fromType, toType, onMidpoint, onComplete);
            }
            else
            {
                m_TransitionFX.PlayModeChangeBridge(
                    fromScreen != null ? fromScreen.GetComponent<RectTransform>() : null,
                    toScreen != null ? toScreen.GetComponent<RectTransform>() : null,
                    onMidpoint,
                    onComplete
                );
            }
        }

        public void PlayScreenEntrance(RectTransform screenRect, ScreenFXType type, Action onComplete = null)
        {
            EnsureComponents();
            CinematicScreenFXProfile profile = CinematicScreenFXProfile.GetProfile(type);
            TriggerFXStart(screenRect, type);

            if (screenRect != null && profile != null)
            {
                CinematicUIEffect fx = screenRect.GetComponent<CinematicUIEffect>() ?? screenRect.GetComponentInChildren<CinematicUIEffect>();
                if (fx != null)
                {
                    fx.TriggerBorderPulse(profile.EntranceDuration, profile.PrimaryEnergyColor, profile.BorderIntensity, profile.BorderDirection);
                    if (profile.ShockwaveStrength > 0.05f)
                    {
                        fx.TriggerShockwave(profile.EntranceDuration * 0.7f, new Vector2(0.5f, 0.5f), profile.ShockwaveStrength);
                    }
                }

                if (CinematicUIParticleSystem.Instance != null && profile.ParticleBurstCount > 0)
                {
                    CinematicUIParticleSystem.Instance.SpawnSparkBurst(screenRect.position, profile.PrimaryEnergyColor, profile.ParticleBurstCount, profile.ParticleSpreadRadius);
                }
            }

            onComplete?.Invoke();
        }

        public void PlayScreenExit(RectTransform screenRect, ScreenFXType type, Action onComplete = null)
        {
            EnsureComponents();
            CinematicScreenFXProfile profile = CinematicScreenFXProfile.GetProfile(type);

            if (screenRect != null && profile != null)
            {
                CinematicUIEffect fx = screenRect.GetComponent<CinematicUIEffect>() ?? screenRect.GetComponentInChildren<CinematicUIEffect>();
                if (fx != null)
                {
                    fx.TriggerBorderPulse(profile.ExitDuration, profile.PrimaryEnergyColor, profile.BorderIntensity, profile.BorderDirection);
                    if (profile.GlitchIntensity > 0.3f)
                    {
                        fx.TriggerDigitalGlitch(profile.ExitDuration * 0.5f, profile.GlitchIntensity);
                    }
                }
            }

            onComplete?.Invoke();
        }

        public void PlayCharacterScan(RectTransform targetArea, Action onCore = null, Action onComplete = null)
        {
            EnsureComponents();
            m_CollectionScanner.PlayScan(targetArea, onCore, onComplete);
        }

        /// <summary>
        /// Complete reentry safety cleanup: stops active coroutines and resets sub-systems to eliminate transform drift.
        /// </summary>
        public void ResetFX()
        {
            if (m_ImpactFX != null) m_ImpactFX.StopActiveShake();
            if (m_ConfirmFX != null) m_ConfirmFX.StopActivePunch();
            if (m_TransitionFX != null) m_TransitionFX.StopActiveTransition();
            if (m_ProfileTransitionFX != null) m_ProfileTransitionFX.StopActiveTransition();
            if (m_CollectionScanner != null) m_CollectionScanner.StopActiveScan();
            if (m_AmbientFX != null) m_AmbientFX.SetPaused(false);
        }
        #endregion
    }
}
