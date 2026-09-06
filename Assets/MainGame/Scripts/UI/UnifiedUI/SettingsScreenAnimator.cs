using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MainGame.UI.Animation;
using Setting.Menu;

namespace MainGame.UI.Unified
{
    /// <summary>
    /// Animates the Settings screen entrance and exit with high-velocity physical choreography:
    /// - Heavy panel slide from right with landing micro-shake
    /// - SETTINGS signboard landing with decaying pendulum oscillation and ambient rope sway
    /// - Alternating row entrance trajectories:
    ///     * Master Volume: from left (-650px)
    ///     * Music: from top (+450px)
    ///     * Sound FX: from left (-650px)
    ///     * Brightness: from top-right (+600px, +350px)
    /// - Row signboard arrives first, slider/values arrive 0.05s later (not as a single unit)
    /// - Physical landing compression: Target + 20px -> Target - 5px -> Target (scale 1.00 -> 0.94 -> 1.03 -> 1.00)
    /// - Value change pulse on individual rows
    /// - Complete out-of-screen exits (> 850px)
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsScreenAnimator : MonoBehaviour
    {
        [Header("Panel Background")]
        [SerializeField] private RectTransform m_PanelBackground;
        [SerializeField] private float m_PanelSlideDistance = 850f;
        [SerializeField] private float m_PanelDuration = 0.38f;

        [Header("Header Elements")]
        [Tooltip("SETTINGS signboard entering from upper-right with physical pendulum decay.")]
        [SerializeField] private RectTransform m_SettingsSign;

        [Header("Settings Rows (Order: Master, Music, SoundFX, Brightness)")]
        [SerializeField] private RectTransform[] m_SettingsRows;

        [Header("Back Button")]
        [SerializeField] private RectTransform m_BackButton;

        private Vector2 m_PanelRestPos;
        private Vector2 m_SignRestPos;
        private Vector3 m_SignRestAngles;
        private Vector2 m_BackRestPos;

        private Vector2[] m_RowRestPositions;
        private RectTransform[] m_RowSignboards;
        private RectTransform[] m_RowValues;
        private Vector2[] m_SignboardRestPositions;
        private Vector2[] m_ValuesRestPositions;

        private Coroutine m_ActiveRoutine;
        private Coroutine m_SignBreezeRoutine;
        private Coroutine[] m_RowPulseRoutines;
        private bool m_HasCapturedRest = false;

        private void Awake()
        {
            CaptureRestState();
        }

        private void Start()
        {
            CaptureRestState();
            SubscribeToRowEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromRowEvents();
        }

        public void CaptureRestState()
        {
            if (m_HasCapturedRest) return;

            if (m_PanelBackground == null)
            {
                Transform bg = transform.Find("Holder/BG") ?? transform.Find("BG");
                if (bg != null) m_PanelBackground = bg as RectTransform;
            }

            if (m_SettingsSign == null)
            {
                Transform sign = transform.Find("Holder/Setting IMG") ?? transform.Find("Setting IMG");
                if (sign != null) m_SettingsSign = sign as RectTransform;
            }

            if (m_BackButton == null)
            {
                Transform back = transform.Find("Holder/Back B") ?? transform.Find("Holder/B Back") ?? transform.Find("Back B") ?? transform.Find("B Back");
                if (back != null) m_BackButton = back as RectTransform;
            }

            if (m_SettingsRows == null || m_SettingsRows.Length == 0)
            {
                Transform ctrlHolder = transform.Find("Holder/Controlles Holder") ?? transform.Find("Controlles Holder");
                if (ctrlHolder != null)
                {
                    var rows = new List<RectTransform>();
                    string[] rowNames = new string[] { "Master Volume", "Music", "Sound FX", "Brithtness" };
                    foreach (var rName in rowNames)
                    {
                        Transform rowTrans = ctrlHolder.Find(rName);
                        if (rowTrans != null) rows.Add(rowTrans as RectTransform);
                    }
                    m_SettingsRows = rows.ToArray();
                }
            }

            if (m_PanelBackground != null)
            {
                m_PanelRestPos = m_PanelBackground.anchoredPosition;
            }

            if (m_SettingsSign != null)
            {
                m_SignRestPos = m_SettingsSign.anchoredPosition;
                m_SignRestAngles = m_SettingsSign.localEulerAngles;
            }

            if (m_BackButton != null)
            {
                m_BackRestPos = m_BackButton.anchoredPosition;
            }

            if (m_SettingsRows != null && m_SettingsRows.Length > 0)
            {
                int count = m_SettingsRows.Length;
                m_RowRestPositions = new Vector2[count];
                m_RowSignboards = new RectTransform[count];
                m_RowValues = new RectTransform[count];
                m_SignboardRestPositions = new Vector2[count];
                m_ValuesRestPositions = new Vector2[count];
                m_RowPulseRoutines = new Coroutine[count];

                for (int i = 0; i < count; i++)
                {
                    if (m_SettingsRows[i] != null)
                    {
                        m_RowRestPositions[i] = m_SettingsRows[i].anchoredPosition;

                        // Identify signboard ("Button name") and values/slider ("Values")
                        Transform signChild = m_SettingsRows[i].Find("Button name");
                        if (signChild == null && m_SettingsRows[i].childCount > 0)
                        {
                            signChild = m_SettingsRows[i].GetChild(0);
                        }
                        if (signChild != null)
                        {
                            m_RowSignboards[i] = signChild as RectTransform;
                            m_SignboardRestPositions[i] = m_RowSignboards[i].anchoredPosition;
                        }

                        Transform valChild = m_SettingsRows[i].Find("Values");
                        if (valChild == null && m_SettingsRows[i].childCount > 1)
                        {
                            valChild = m_SettingsRows[i].GetChild(1);
                        }
                        if (valChild != null)
                        {
                            m_RowValues[i] = valChild as RectTransform;
                            m_ValuesRestPositions[i] = m_RowValues[i].anchoredPosition;
                        }
                    }
                }
            }

            m_HasCapturedRest = true;
        }

        private void SubscribeToRowEvents()
        {
            if (m_SettingsRows == null) return;
            for (int i = 0; i < m_SettingsRows.Length; i++)
            {
                if (m_SettingsRows[i] == null) continue;
                int rowIndex = i;
                SettingStepControl stepCtrl = m_SettingsRows[i].GetComponent<SettingStepControl>();
                if (stepCtrl != null)
                {
                    stepCtrl.OnValueChanged += (val) => TriggerRowPulse(rowIndex);
                }

                Slider slider = m_SettingsRows[i].GetComponentInChildren<Slider>();
                if (slider != null)
                {
                    slider.onValueChanged.AddListener((val) => TriggerRowPulse(rowIndex));
                }
            }
        }

        private void UnsubscribeFromRowEvents()
        {
            // Clean up if needed
        }

        public void ResetToRestState()
        {
            StopActiveAnimation();
            if (!m_HasCapturedRest) return;

            if (m_PanelBackground != null)
            {
                m_PanelBackground.anchoredPosition = m_PanelRestPos;
                m_PanelBackground.localScale = Vector3.one;
            }

            if (m_SettingsSign != null)
            {
                m_SettingsSign.anchoredPosition = m_SignRestPos;
                m_SettingsSign.localEulerAngles = m_SignRestAngles;
                m_SettingsSign.localScale = Vector3.one;
            }

            if (m_BackButton != null)
            {
                m_BackButton.anchoredPosition = m_BackRestPos;
                m_BackButton.localScale = Vector3.one;
            }

            if (m_SettingsRows != null && m_RowRestPositions != null)
            {
                for (int i = 0; i < m_SettingsRows.Length; i++)
                {
                    if (m_SettingsRows[i] != null && i < m_RowRestPositions.Length)
                    {
                        m_SettingsRows[i].anchoredPosition = m_RowRestPositions[i];
                        m_SettingsRows[i].localScale = Vector3.one;
                    }

                    if (m_RowSignboards != null && i < m_RowSignboards.Length && m_RowSignboards[i] != null)
                    {
                        m_RowSignboards[i].anchoredPosition = m_SignboardRestPositions[i];
                        m_RowSignboards[i].localScale = Vector3.one;
                    }

                    if (m_RowValues != null && i < m_RowValues.Length && m_RowValues[i] != null)
                    {
                        m_RowValues[i].anchoredPosition = m_ValuesRestPositions[i];
                        m_RowValues[i].localScale = Vector3.one;
                    }
                }
            }
        }

        public void PlayEntrance(Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(EntranceRoutine(onComplete));
        }

        public void PlayExit(Action onComplete)
        {
            StopActiveAnimation();
            CaptureRestState();
            m_ActiveRoutine = StartCoroutine(ExitRoutine(onComplete));
        }

        public void StopActiveAnimation()
        {
            if (m_ActiveRoutine != null)
            {
                StopCoroutine(m_ActiveRoutine);
                m_ActiveRoutine = null;
            }

            if (m_SignBreezeRoutine != null)
            {
                StopCoroutine(m_SignBreezeRoutine);
                m_SignBreezeRoutine = null;
            }
        }

        public void TriggerRowPulse(int rowIndex)
        {
            if (m_SettingsRows == null || rowIndex < 0 || rowIndex >= m_SettingsRows.Length) return;
            RectTransform row = m_SettingsRows[rowIndex];
            if (row == null || !row.gameObject.activeInHierarchy) return;

            if (m_RowPulseRoutines != null && rowIndex < m_RowPulseRoutines.Length)
            {
                if (m_RowPulseRoutines[rowIndex] != null)
                {
                    StopCoroutine(m_RowPulseRoutines[rowIndex]);
                }
                m_RowPulseRoutines[rowIndex] = StartCoroutine(RowPulseRoutine(row, rowIndex));
            }
        }

        private IEnumerator RowPulseRoutine(RectTransform row, int index)
        {
            Transform valChild = (m_RowValues != null && index < m_RowValues.Length) ? m_RowValues[index] : null;
            Transform target = valChild != null ? valChild : row;

            float duration = 0.14f;
            float elapsed = 0f;
            Vector3 punchScale = new Vector3(1.08f, 1.08f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 2f);
                if (target != null)
                {
                    target.localScale = Vector3.Lerp(punchScale, Vector3.one, ease);
                }
                yield return null;
            }

            if (target != null) target.localScale = Vector3.one;
            if (m_RowPulseRoutines != null && index < m_RowPulseRoutines.Length)
            {
                m_RowPulseRoutines[index] = null;
            }
        }

        private IEnumerator EntranceRoutine(Action onComplete)
        {
            // 1. Panel slides from offscreen right (+850px) with heavy micro-shake on landing
            if (m_PanelBackground != null)
            {
                Vector2 startPanel = new Vector2(m_PanelRestPos.x + m_PanelSlideDistance, m_PanelRestPos.y);
                m_PanelBackground.anchoredPosition = startPanel;
                StartCoroutine(AnimateMotion(m_PanelBackground, startPanel, m_PanelRestPos, 0f, 0f, m_PanelDuration, 0f, EasingType.EaseOutCubic, 1f, () =>
                {
                    UIMicroShake.Shake(0.70f, 0.07f);
                }));
            }

            // 2. SETTINGS sign enters from upper-right with decaying pendulum oscillation
            if (m_SettingsSign != null)
            {
                StartCoroutine(SignEntranceRoutine());
            }

            // 3. Back button from bottom offscreen (-180px)
            if (m_BackButton != null)
            {
                Vector2 startBack = new Vector2(m_BackRestPos.x, m_BackRestPos.y - 180f);
                m_BackButton.anchoredPosition = startBack;
                StartCoroutine(AnimatePhysicalLanding(
                    m_BackButton,
                    startBack, m_BackRestPos,
                    0.38f, 0.16f,
                    new Vector2(0f, -12f),
                    null
                ));
            }

            // 4. Alternating directional row entrance:
            // Row 0 (Master Volume): from left (-650px)
            // Row 1 (Music): from top (+450px)
            // Row 2 (Sound FX): from left (-650px)
            // Row 3 (Brightness): from top-right (+600px, +350px)
            // Stagger: signboard lands first, slider/values arrive 0.05s later!
            int finishedCount = 0;
            int totalComponents = 0;

            if (m_SettingsRows != null)
            {
                // Offsets for each row (genuine screen-edge entry distances)
                Vector2[] entryOffsets = new Vector2[]
                {
                    new Vector2(-800f, 0f),       // Master Volume: Fast Left
                    new Vector2(0f, 650f),        // Music: Top Drop
                    new Vector2(-800f, 0f),       // Sound FX: Fast Left
                    new Vector2(750f, 450f)       // Brightness: Top-Right Drop
                };

                for (int i = 0; i < m_SettingsRows.Length; i++)
                {
                    if (m_SettingsRows[i] != null && m_SettingsRows[i].gameObject.activeInHierarchy)
                    {
                        totalComponents += 2; // signboard + slider
                    }
                }

                for (int i = 0; i < m_SettingsRows.Length; i++)
                {
                    RectTransform row = m_SettingsRows[i];
                    if (row == null || !row.gameObject.activeInHierarchy) continue;

                    Vector2 offset = (i < entryOffsets.Length) ? entryOffsets[i] : new Vector2(-650f, 0f);
                    float baseDelay = 0.12f + (i * 0.07f);

                    // Row root sits at rest position
                    row.anchoredPosition = m_RowRestPositions[i];
                    row.localScale = Vector3.one;

                    RectTransform signboard = (m_RowSignboards != null && i < m_RowSignboards.Length) ? m_RowSignboards[i] : null;
                    RectTransform values = (m_RowValues != null && i < m_RowValues.Length) ? m_RowValues[i] : null;

                    Vector2 signRest = (signboard != null) ? m_SignboardRestPositions[i] : Vector2.zero;
                    Vector2 valRest = (values != null) ? m_ValuesRestPositions[i] : Vector2.zero;

                    // 1) Signboard enters first
                    if (signboard != null)
                    {
                        Vector2 signStart = signRest + offset;
                        signboard.anchoredPosition = signStart;
                        Vector2 overshoot = -offset.normalized * 18f;

                        StartCoroutine(AnimatePhysicalLanding(
                            signboard,
                            signStart, signRest,
                            0.36f, baseDelay,
                            overshoot,
                            () => {
                                finishedCount++;
                                UIMicroShake.Shake(0.20f, 0.04f);
                            }
                        ));
                    }
                    else
                    {
                        finishedCount++;
                    }

                    // 2) Slider / Values arrives 0.05s later!
                    if (values != null)
                    {
                        Vector2 valStart = valRest + offset;
                        values.anchoredPosition = valStart;
                        Vector2 overshoot = -offset.normalized * 14f;

                        StartCoroutine(AnimatePhysicalLanding(
                            values,
                            valStart, valRest,
                            0.34f, baseDelay + 0.05f,
                            overshoot,
                            () => finishedCount++
                        ));
                    }
                    else
                    {
                        finishedCount++;
                    }
                }
            }

            while (finishedCount < totalComponents)
            {
                yield return null;
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator SignEntranceRoutine()
        {
            Vector2 startSign = new Vector2(m_SignRestPos.x + 350f, m_SignRestPos.y + 250f);
            m_SettingsSign.anchoredPosition = startSign;
            m_SettingsSign.localEulerAngles = new Vector3(0f, 0f, 10f);

            float dropDuration = 0.38f;
            float elapsed = 0f;
            while (elapsed < dropDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutCubic, t);

                if (m_SettingsSign != null)
                {
                    m_SettingsSign.anchoredPosition = Vector2.Lerp(startSign, m_SignRestPos, ease);
                }
                yield return null;
            }

            if (m_SettingsSign != null)
            {
                m_SettingsSign.anchoredPosition = m_SignRestPos;
                UIMicroShake.Shake(0.50f, 0.06f);
            }

            // Physical damped pendulum decay (swings -12° -> +7° -> -3° -> +1° -> 0°)
            elapsed = 0f;
            float swingDuration = 0.75f;
            while (elapsed < swingDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float decay = Mathf.Exp(-elapsed * 4f);
                float angle = -decay * Mathf.Sin(elapsed * 12f) * 12f;

                if (m_SettingsSign != null)
                {
                    m_SettingsSign.localEulerAngles = new Vector3(0f, 0f, m_SignRestAngles.z + angle);
                }
                yield return null;
            }

            if (m_SettingsSign != null) m_SettingsSign.localEulerAngles = m_SignRestAngles;

            // Ambient rope breeze sway
            m_SignBreezeRoutine = StartCoroutine(SignAmbientSwayRoutine());
        }

        private IEnumerator SignAmbientSwayRoutine()
        {
            while (m_SettingsSign != null)
            {
                float angle = Mathf.Sin(Time.unscaledTime * 1.5f) * 0.75f;
                m_SettingsSign.localEulerAngles = new Vector3(0f, 0f, m_SignRestAngles.z + angle);
                yield return null;
            }
        }

        private IEnumerator ExitRoutine(Action onComplete)
        {
            float duration = 0.26f;

            if (m_SignBreezeRoutine != null)
            {
                StopCoroutine(m_SignBreezeRoutine);
                m_SignBreezeRoutine = null;
            }

            // Rows fling completely offscreen (> 850px)
            // Row 0 & 2: left (-900px)
            // Row 1 & 3: right (+900px)
            if (m_SettingsRows != null)
            {
                for (int i = 0; i < m_SettingsRows.Length; i++)
                {
                    RectTransform row = m_SettingsRows[i];
                    if (row == null || !row.gameObject.activeInHierarchy) continue;

                    bool toLeft = (i % 2 == 0);
                    float xOffset = toLeft ? -950f : 950f;
                    Vector2 targetPos = new Vector2(m_RowRestPositions[i].x + xOffset, m_RowRestPositions[i].y);

                    StartCoroutine(AnimateMotion(row, row.anchoredPosition, targetPos, 0f, 0f, duration, i * 0.02f, EasingType.EaseInBack, 1.15f));
                }
            }

            // Sign exits upper-right (+500px, +300px)
            if (m_SettingsSign != null)
            {
                Vector2 targetSign = new Vector2(m_SignRestPos.x + 500f, m_SignRestPos.y + 300f);
                StartCoroutine(AnimateMotion(m_SettingsSign, m_SettingsSign.anchoredPosition, targetSign, 0f, 8f, duration, 0f, EasingType.EaseInBack, 1.15f));
            }

            // Back button exits bottom (-500px)
            if (m_BackButton != null)
            {
                Vector2 targetBack = new Vector2(m_BackRestPos.x, m_BackRestPos.y - 500f);
                StartCoroutine(AnimateMotion(m_BackButton, m_BackButton.anchoredPosition, targetBack, 0f, 0f, duration, 0f, EasingType.EaseInBack, 1.1f));
            }

            // Panel exits right (+950px)
            if (m_PanelBackground != null)
            {
                Vector2 targetPanel = new Vector2(m_PanelRestPos.x + m_PanelSlideDistance, m_PanelRestPos.y);
                StartCoroutine(AnimateMotion(m_PanelBackground, m_PanelBackground.anchoredPosition, targetPanel, 0f, 0f, duration, 0.04f, EasingType.EaseInBack, 1.1f));
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            m_ActiveRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator AnimatePhysicalLanding(
            RectTransform target,
            Vector2 startPos, Vector2 restPos,
            float travelDuration, float delay,
            Vector2 overshootOffset,
            Action onDone = null)
        {
            if (delay > 0f)
            {
                float delayTimer = 0f;
                while (delayTimer < delay)
                {
                    delayTimer += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (target == null)
            {
                onDone?.Invoke();
                yield break;
            }

            // Phase 1: High velocity travel to rest + overshoot
            Vector2 overshootPos = restPos + overshootOffset;
            float elapsed = 0f;
            while (elapsed < travelDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float ease = UIEasing.Evaluate(EasingType.EaseOutQuad, t);

                if (target != null)
                {
                    target.anchoredPosition = Vector2.LerpUnclamped(startPos, overshootPos, ease);
                }
                yield return null;
            }

            // Phase 2: Landing compression sequence:
            // Target + overshoot -> Target - minor bounce -> rest
            // Scale: 1.00 -> 0.94 -> 1.03 -> 1.00
            Vector2 reboundPos = restPos - (overshootOffset * 0.25f);
            Vector3 squishScale = new Vector3(1.05f, 0.94f, 1f);
            Vector3 stretchScale = new Vector3(0.97f, 1.03f, 1f);

            // Settle 1: rebound
            float settle1Dur = 0.08f;
            elapsed = 0f;
            while (elapsed < settle1Dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settle1Dur);
                if (target != null)
                {
                    target.anchoredPosition = Vector2.Lerp(overshootPos, reboundPos, t);
                    target.localScale = Vector3.Lerp(Vector3.one, squishScale, t);
                }
                yield return null;
            }

            // Settle 2: return to rest
            float settle2Dur = 0.08f;
            elapsed = 0f;
            while (elapsed < settle2Dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settle2Dur);
                if (target != null)
                {
                    target.anchoredPosition = Vector2.Lerp(reboundPos, restPos, t);
                    target.localScale = Vector3.Lerp(squishScale, stretchScale, t);
                }
                yield return null;
            }

            // Settle 3: normalize scale
            float settle3Dur = 0.06f;
            elapsed = 0f;
            while (elapsed < settle3Dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settle3Dur);
                if (target != null)
                {
                    target.localScale = Vector3.Lerp(stretchScale, Vector3.one, t);
                }
                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = restPos;
                target.localScale = Vector3.one;
            }

            onDone?.Invoke();
        }

        private IEnumerator AnimateMotion(
            RectTransform target,
            Vector2 startPos, Vector2 targetPos,
            float startRotZ, float targetRotZ,
            float duration, float delay,
            EasingType easing, float overshoot,
            Action onDone = null)
        {
            if (delay > 0f)
            {
                float delayTimer = 0f;
                while (delayTimer < delay)
                {
                    delayTimer += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = UIEasing.Evaluate(easing, t, overshoot);

                if (target != null)
                {
                    target.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, ease);
                    if (Mathf.Abs(startRotZ - targetRotZ) > 0.01f)
                    {
                        target.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpUnclamped(startRotZ, targetRotZ, ease));
                    }
                }
                yield return null;
            }

            if (target != null)
            {
                target.anchoredPosition = targetPos;
                target.localEulerAngles = new Vector3(0f, 0f, targetRotZ);
            }

            onDone?.Invoke();
        }
    }
}
