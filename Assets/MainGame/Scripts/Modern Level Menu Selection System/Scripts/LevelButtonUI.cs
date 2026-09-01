using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ModernLevelSelection
{
    /// <summary>
    /// UI controller for a single level button. Responsible for visual states and click animation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelButtonUI : MonoBehaviour
    {
        #region Inspector

        [Header("References")]
        [Tooltip("Main clickable button for the level.")]
        [SerializeField]
        private Button _button;

        [Tooltip("Thumbnail image shown for unlocked levels.")]
        [SerializeField]
        private Image _thumbnail;

        [Tooltip("Text component that displays the level number.")]
        [SerializeField]
        private TextMeshProUGUI _levelNumberText;
        [SerializeField]
        private GameObject _levelNumberRoot;

        [Tooltip("Container that visually shows stars. Expect 3 child images, enabled depending on stars.")]
        [SerializeField]
        private GameObject _starsRoot;

        [Tooltip("Root GameObject active when level is locked.")]
        [SerializeField]
        private GameObject _lockedRoot;

        [Tooltip("Root GameObject active when level is unlocked.")]
        [SerializeField]
        private GameObject _unlockedRoot;

        [Tooltip("Root GameObject active when level is coming soon.")]
        [SerializeField]
        private GameObject _comingSoonRoot;

        [Tooltip("Background image for additional theming.")]
        [SerializeField]
        private Image _background;

        [Tooltip("Highlight image to indicate the current unlocked level.")]
        [SerializeField]
        private Image _highlight;

        [Header("Animation")]
        [Tooltip("Scale multiplier for press animation.")]
        [SerializeField]
        private float _pressScale = 0.9f;

        [Tooltip("Duration for press/release animation.")]
        [SerializeField]
        private float _pressDuration = 0.12f;

        Vector3 original;
        Transform transformCached;

        #endregion

        #region Events

        /// <summary>
        /// Event invoked when this level is clicked. Parameter = level number.
        /// </summary>
        public UnityEvent<int> OnLevelClicked = new UnityEvent<int>();

        #endregion

        #region State

        private int _levelNumber;
        private Coroutine _pressRoutine;

        #endregion

        #region Unity

        private void Reset()
        {
            // Try to auto-wire common components if available
            if (_button == null) _button = GetComponent<Button>() ?? GetComponentInChildren<Button>(true);
            if (_thumbnail == null) _thumbnail = GetComponentInChildren<Image>();
            if (_levelNumberText == null) _levelNumberText = GetComponentInChildren<TextMeshProUGUI>();
        }

        private void Awake()
        {

            transformCached = _button.transform;
            original = transformCached.localScale;
            if (_button != null)
            {
                _button.onClick.AddListener(HandleClick);
            }

        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Configure this button for a level.
        /// </summary>
        public void Setup(int levelNumber, LevelState state, int stars, bool interactable)
        {
            _levelNumber = levelNumber;
            SetState(state);
            SetStars(stars);
            if (_levelNumberText != null)
            {
                _levelNumberText.text = levelNumber.ToString();
            }
            if (_button != null)
            {
                _button.interactable = interactable;
            }
        }

        /// <summary>
        /// Refresh visuals (use when updated externally).
        /// </summary>
        public void Refresh(int stars, LevelState state)
        {
            SetState(state);
            SetStars(stars);
        }

        /// <summary>
        /// Sets the logical and visual state of this button.
        /// </summary>
        public void SetState(LevelState state)
        {
            if (_lockedRoot != null) _lockedRoot.SetActive(state == LevelState.Locked);
            if (_unlockedRoot != null) _unlockedRoot.SetActive(state == LevelState.Unlocked);
            if (_comingSoonRoot != null) _comingSoonRoot.SetActive(state == LevelState.ComingSoon);
            if (_levelNumberRoot != null) _levelNumberRoot.SetActive(state == LevelState.Unlocked);
          //  if (_starsRoot != null) _starsRoot.SetActive(state == LevelState.Unlocked);
            if (_thumbnail != null) _thumbnail.enabled = (state == LevelState.Unlocked);
            if (_highlight != null) _highlight.enabled = false;
        }

        /// <summary>
        /// Enable or disable the highlight that indicates the current unlocked level.
        /// </summary>
        public void SetHighlight(bool value)
        {
            if (_highlight != null) _highlight.enabled = value;
        }

        /// <summary>
        /// Set displayed stars (0-3).
        /// </summary>
        public void SetStars(int stars)
        {
            stars = Mathf.Clamp(stars, 0, 3);
            if (_starsRoot == null) return;
            int childCount = _starsRoot.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                var child = _starsRoot.transform.GetChild(i).gameObject;
                child.SetActive(i < stars);
            }
        }

        /// <summary>
        /// Plays a simple punch scale animation for click feedback.
        /// </summary>
        public void PlayClickAnimation()
        {
            if (_pressRoutine != null) StopCoroutine(_pressRoutine);
            _pressRoutine = StartCoroutine(PressRoutine());
        }

        #endregion

        #region Private

        private void HandleClick()
        {
            PlayClickAnimation();
            OnLevelClicked?.Invoke(_levelNumber);
        }

        private IEnumerator PressRoutine()
        {
            if (_button == null)
                yield break;

            Vector3 target = original * _pressScale;
            float t = 0f;
            while (t < _pressDuration)
            {
                t += Time.unscaledDeltaTime;
                transformCached.localScale = Vector3.Lerp(original, target, t / _pressDuration);
                yield return null;
            }
            t = 0f;
            while (t < _pressDuration)
            {
                t += Time.unscaledDeltaTime;
                transformCached.localScale = Vector3.Lerp(target, original, t / _pressDuration);
                yield return null;
            }
            transformCached.localScale = original;
            _pressRoutine = null;
        }

        #endregion
    }
}
