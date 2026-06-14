#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TutorialSystem.EditorTools
{
    /// <summary>
    /// One-click setup for the whole Tutorial System.
    ///
    /// Prefabs/scenes can't be hand-authored safely as text, so this builds the entire runtime
    /// rig procedurally and wires every private [SerializeField] via SerializedObject — exactly what
    /// you'd otherwise do by hand in the Inspector. After running it you have a fully working,
    /// reskinnable tutorial overlay and a sample tutorial asset to play with.
    ///
    /// Run:  Tools ▸ Tutorial System ▸ Setup Tutorial System
    ///
    /// Hierarchy it creates (placeholder art = Unity built-in UI sprites; reskin freely):
    ///   TutorialSystem (TutorialManager, persistent)
    ///     └ TutorialCanvas (Overlay, sort 5000)
    ///         ├ Overlay (TutorialHighlightSystem) → DimTop/Bottom/Left/Right + HighlightRing
    ///         ├ Arrow (TutorialArrowController)
    ///         └ Popup (TutorialPopupUI) → Character, Bubble→Message, NextButton
    /// </summary>
    public static class TutorialSystemSetup
    {
        public const string DataFolder = "Assets/MainGame/TutorialData";
        private const string SamplePath = DataFolder + "/Tutorial_Sample.asset";

        // Reference (design) resolution — portrait, matching the project's mobile UI.
        private static readonly Vector2 RefRes = new Vector2(1080f, 1920f);
        private static readonly Color Accent = new Color(0.20f, 0.62f, 1f, 1f);

        [MenuItem("Tools/Tutorial System/Setup Tutorial System", priority = 0)]
        public static void SetupTutorialSystem()
        {
            // Reuse if it already exists so re-running is safe.
            TutorialManager existing = Object.FindObjectOfType<TutorialManager>();
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                if (!EditorUtility.DisplayDialog("Tutorial System",
                        "A TutorialManager already exists in the scene. Rebuild it from scratch?",
                        "Rebuild", "Cancel"))
                    return;
                Object.DestroyImmediate(existing.transform.root.gameObject);
            }

            EnsureEventSystem();

            // ── Root + canvas ────────────────────────────────────────────────────
            var root = new GameObject("TutorialSystem");
            TutorialManager manager = root.AddComponent<TutorialManager>();

            GameObject canvasGo = NewUI("TutorialCanvas", root.transform);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000; // sit above all gameplay UI
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = RefRes;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            RectTransform canvasRect = (RectTransform)canvasGo.transform;

            // ── Overlay + dim strips + ring (highlight system) ────────────────────
            GameObject overlayGo = NewUI("Overlay", canvasGo.transform);
            Stretch((RectTransform)overlayGo.transform);
            TutorialHighlightSystem highlight = overlayGo.AddComponent<TutorialHighlightSystem>();

            Image top = NewStrip("DimTop", overlayGo.transform);
            Image bottom = NewStrip("DimBottom", overlayGo.transform);
            Image left = NewStrip("DimLeft", overlayGo.transform);
            Image right = NewStrip("DimRight", overlayGo.transform);

            Image ring = NewImage("HighlightRing", overlayGo.transform, UISprite());
            ring.color = new Color(Accent.r, Accent.g, Accent.b, 0.28f);
            ring.raycastTarget = false;
            Center((RectTransform)ring.transform, new Vector2(180f, 180f));

            // ── Arrow ─────────────────────────────────────────────────────────────
            Image arrowImg = NewImage("Arrow", canvasGo.transform, UISprite());
            arrowImg.color = Accent;
            arrowImg.raycastTarget = false;
            Center((RectTransform)arrowImg.transform, new Vector2(90f, 90f));
            TutorialArrowController arrow = arrowImg.gameObject.AddComponent<TutorialArrowController>();
            // A quick "▼" hint child so the placeholder reads as a pointer before reskinning.
            TMP_Text arrowHint = NewText("Hint", arrowImg.transform, "▼", 54);
            arrowHint.color = Color.white;
            Stretch((RectTransform)arrowHint.transform);
            arrowHint.alignment = TextAlignmentOptions.Center;

            // ── Popup ─────────────────────────────────────────────────────────────
            GameObject popupGo = NewUI("Popup", canvasGo.transform);
            Center((RectTransform)popupGo.transform, new Vector2(900f, 320f));
            var popupGroup = popupGo.AddComponent<CanvasGroup>();
            TutorialPopupUI popup = popupGo.AddComponent<TutorialPopupUI>();

            Image character = NewImage("Character", popupGo.transform, UISprite());
            character.color = Color.white;
            var charRt = (RectTransform)character.transform;
            charRt.anchorMin = charRt.anchorMax = new Vector2(0f, 0.5f);
            charRt.pivot = new Vector2(0f, 0.5f);
            charRt.anchoredPosition = new Vector2(20f, 0f);
            charRt.sizeDelta = new Vector2(240f, 240f);

            Image bubble = NewImage("Bubble", popupGo.transform, UISprite());
            bubble.color = new Color(1f, 1f, 1f, 0.97f);
            bubble.type = Image.Type.Sliced;
            var bubbleRt = (RectTransform)bubble.transform;
            bubbleRt.anchorMin = new Vector2(0f, 0f);
            bubbleRt.anchorMax = new Vector2(1f, 1f);
            bubbleRt.offsetMin = new Vector2(270f, 20f);
            bubbleRt.offsetMax = new Vector2(-20f, -20f);

            TMP_Text message = NewText("Message", bubble.transform,
                "Tutorial message goes here.", 36);
            message.color = new Color(0.12f, 0.12f, 0.14f);
            message.alignment = TextAlignmentOptions.Left;
            var msgRt = (RectTransform)message.transform;
            Stretch(msgRt);
            msgRt.offsetMin = new Vector2(30f, 80f);
            msgRt.offsetMax = new Vector2(-30f, -20f);

            // Next button (bottom-right of the bubble)
            GameObject nextGo = NewUI("NextButton", bubble.transform);
            Image nextImg = nextGo.AddComponent<Image>();
            nextImg.sprite = UISprite();
            nextImg.type = Image.Type.Sliced;
            nextImg.color = Accent;
            Button nextBtn = nextGo.AddComponent<Button>();
            nextBtn.targetGraphic = nextImg;
            var nextRt = (RectTransform)nextGo.transform;
            nextRt.anchorMin = nextRt.anchorMax = new Vector2(1f, 0f);
            nextRt.pivot = new Vector2(1f, 0f);
            nextRt.anchoredPosition = new Vector2(-16f, 16f);
            nextRt.sizeDelta = new Vector2(180f, 64f);
            TMP_Text nextLabel = NewText("Label", nextGo.transform, "Next", 32);
            nextLabel.color = Color.white;
            nextLabel.alignment = TextAlignmentOptions.Center;
            Stretch((RectTransform)nextLabel.transform);

            // ── Wire all the private [SerializeField] references ──────────────────
            Wire(highlight, ("m_Top", top), ("m_Bottom", bottom), ("m_Left", left), ("m_Right", right),
                            ("m_Ring", ring));
            Wire(popup, ("m_Content", popupGo.GetComponent<RectTransform>()),
                        ("m_CharacterImage", character), ("m_MessageText", message),
                        ("m_NextButton", nextBtn));
            Wire(manager, ("m_CanvasRect", canvasRect), ("m_Popup", popup), ("m_Arrow", arrow),
                          ("m_Highlight", highlight));

            // ── Sample data so there's something to press Play with ───────────────
            TutorialSequenceData sample = CreateSampleSequence();

            Undo.RegisterCreatedObjectUndo(root, "Setup Tutorial System");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeObject = root;
            EditorUtility.DisplayDialog("Tutorial System",
                "Setup complete!\n\n" +
                "• Rig built in the scene (TutorialSystem → TutorialCanvas).\n" +
                "• Sample tutorial: " + SamplePath + "\n\n" +
                "Next:\n" +
                "1. Tools ▸ Tutorial System ▸ Tutorial Creator to author steps.\n" +
                "2. Select any UI/world object ▸ GameObject ▸ Tutorial ▸ Convert To Tutorial Target.\n" +
                "3. Reskin the placeholder art on the Popup / Arrow / Ring.\n\n" +
                "Don't forget to SAVE the scene (Ctrl+S).",
                "Got it");
        }

        // ─── Sample tutorial asset ──────────────────────────────────────────────────

        private static TutorialSequenceData CreateSampleSequence()
        {
            EnsureFolder(DataFolder);
            TutorialSequenceData seq = AssetDatabase.LoadAssetAtPath<TutorialSequenceData>(SamplePath);
            if (seq != null) return seq;

            seq = ScriptableObject.CreateInstance<TutorialSequenceData>();
            AssetDatabase.CreateAsset(seq, SamplePath);
            SetString(seq, "m_SequenceId", "sample_tutorial");
            SetString(seq, "m_Description", "Auto-generated sample. Edit me in the Tutorial Creator.");

            TutorialStepData s1 = TutorialCreatorUtility.AddStep(seq);
            SetString(s1, "m_StepName", "Welcome");
            SetString(s1, "m_Message", "Welcome! This is a popup-only step. Tap Next to continue.");
            SetEnum(s1, "m_ActionType", (int)TutorialActionType.PopupOnly);

            TutorialStepData s2 = TutorialCreatorUtility.AddStep(seq);
            SetString(s2, "m_StepName", "Point At Something");
            SetString(s2, "m_TargetId", "example_target");
            SetString(s2, "m_Message", "This step highlights the target 'example_target' and points at it.");
            SetEnum(s2, "m_ActionType", (int)TutorialActionType.Highlight);

            AssetDatabase.SaveAssets();
            return seq;
        }

        // ─── Small UI builder helpers ───────────────────────────────────────────────

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite)
        {
            GameObject go = NewUI(name, parent);
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            return img;
        }

        private static Image NewStrip(string name, Transform parent)
        {
            Image img = NewImage(name, parent, null);
            img.color = new Color(0f, 0f, 0f, 0.72f);
            return img;
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size)
        {
            GameObject go = NewUI(name, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.enableWordWrapping = true;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }

        private static Sprite UISprite() =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // ─── Serialized-reference wiring helpers (shared with the creator window) ────

        private static void Wire(Object target, params (string prop, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach ((string prop, Object value) in refs)
            {
                SerializedProperty p = so.FindProperty(prop);
                if (p != null) p.objectReferenceValue = value;
                else Debug.LogWarning($"[TutorialSystemSetup] Missing field '{prop}' on {target.GetType().Name}");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetString(Object target, string prop, string value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(prop);
            if (p != null) p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetEnum(Object target, string prop, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(prop);
            if (p != null) p.enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
