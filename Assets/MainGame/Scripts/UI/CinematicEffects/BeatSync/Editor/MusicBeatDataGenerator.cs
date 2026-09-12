#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MainGame.UI.CinematicEffects.BeatSync.Editor
{
    public static class MusicBeatDataGenerator
    {
        [MenuItem("Tools/RETRY/Generate Music Beat Data")]
        public static void GenerateBeatData()
        {
            const string resourcesDir = "Assets/MainGame/Resources";
            const string assetPath = resourcesDir + "/MusicBeatData.asset";

            if (!Directory.Exists(resourcesDir))
            {
                Directory.CreateDirectory(resourcesDir);
            }

            MusicBeatData data = AssetDatabase.LoadAssetAtPath<MusicBeatData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<MusicBeatData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.Bpm = 113.304f;
            data.FirstBeatOffset = 0.136f;
            data.BeatsPerBar = 4;
            data.TrackLength = 59.736f;
            data.GenerateMarkers();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MusicBeatDataGenerator] Successfully generated MusicBeatData at {assetPath} with {data.Markers.Length} beats across {Mathf.CeilToInt(data.TrackLength / data.BarDuration)} bars.");
        }
    }
}
#endif
