using UnityEditor;
using UnityEngine;

namespace Collectables.EditorTools
{
    /// <summary>
    /// Designer tools for the collectable systems:
    ///   • Reset all saved collectable progress (the JSON save file).
    ///   • Create ready-to-use Robot Part / Memory Shard prefabs.
    ///   • Create the shared CollectableDatabase asset.
    ///   • Assign missing unique ids to Collectables in the open scene.
    ///
    /// Open via Tools ▸ Collectables ▸ Collectable Tools.
    /// </summary>
    public class CollectableToolsWindow : EditorWindow
    {
        private const string PrefabFolder = "Assets/MainGame/Prefabs/Collectables";
        private const string DatabaseFolder = "Assets/MainGame/ScriptableObjects";

        [MenuItem("Tools/Collectables/Collectable Tools")]
        public static void Open()
        {
            var window = GetWindow<CollectableToolsWindow>("Collectable Tools");
            window.minSize = new Vector2(340, 320);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Save Data", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                $"Robot Parts collected: {CollectableSaveSystem.GetTotalCollected(CollectableType.RobotPart)}\n" +
                $"Memory Shards collected: {CollectableSaveSystem.GetTotalCollected(CollectableType.MemoryShard)}\n\n" +
                $"File: {CollectableSaveSystem.SavePath}",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Reset Collectable Data", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Reset Collectable Data",
                        "Permanently delete ALL collected Robot Parts and Memory Shards progress?",
                        "Reset", "Cancel"))
                    {
                        CollectableSaveSystem.ResetAll();
                        Debug.Log("[Collectables] Save data reset.");
                    }
                }
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Create Prefabs", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates a prefab with a trigger collider, SpriteRenderer and Collectable " +
                "component preconfigured. Assign a sprite afterwards, then drop copies into levels.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Robot Part Prefab", GUILayout.Height(28)))
                    CreateCollectablePrefab(CollectableType.RobotPart, "RobotPart");

                if (GUILayout.Button("Create Memory Shard Prefab", GUILayout.Height(28)))
                    CreateCollectablePrefab(CollectableType.MemoryShard, "MemoryShard");
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Database", EditorStyles.boldLabel);
            if (GUILayout.Button("Create Collectable Database Asset", GUILayout.Height(28)))
                CreateDatabaseAsset();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Scene Maintenance", EditorStyles.boldLabel);
            if (GUILayout.Button("Assign Missing Ids In Open Scene(s)", GUILayout.Height(28)))
                AssignMissingIds();
        }

        // ─── Prefab creation ────────────────────────────────────────────────────────

        private static void CreateCollectablePrefab(CollectableType type, string baseName)
        {
            CollectableToolsPaths.EnsureFolder(PrefabFolder);

            var go = new GameObject(baseName);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            var collectable = go.AddComponent<Collectable>();
            var so = new SerializedObject(collectable);
            so.FindProperty("m_Type").enumValueIndex = (int)type;
            // Leave the prefab-asset id empty on purpose; scene instances self-assign one.
            so.FindProperty("m_UniqueId").stringValue = string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = AssetDatabase.GenerateUniqueAssetPath($"{PrefabFolder}/{baseName}.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out bool success);
            Object.DestroyImmediate(go);

            if (success)
            {
                Debug.Log($"[Collectables] Created prefab at {path}", prefab);
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            else
            {
                Debug.LogError($"[Collectables] Failed to create prefab at {path}");
            }
        }

        // ─── Database creation ────────────────────────────────────────────────────────

        private static void CreateDatabaseAsset()
        {
            CollectableToolsPaths.EnsureFolder(DatabaseFolder);
            var db = ScriptableObject.CreateInstance<CollectableDatabase>();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DatabaseFolder}/CollectableDatabase.asset");
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Collectables] Created database at {path}", db);
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
        }

        // ─── Scene maintenance ────────────────────────────────────────────────────────

        private static void AssignMissingIds()
        {
            var collectables = Object.FindObjectsByType<Collectable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int fixedCount = 0;
            foreach (var c in collectables)
            {
                if (!string.IsNullOrEmpty(c.UniqueId)) continue;

                var so = new SerializedObject(c);
                so.FindProperty("m_UniqueId").stringValue = System.Guid.NewGuid().ToString("N");
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(c);
                fixedCount++;
            }

            if (fixedCount > 0)
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

            Debug.Log($"[Collectables] Assigned ids to {fixedCount} collectable(s) with a missing id.");
        }
    }
}
