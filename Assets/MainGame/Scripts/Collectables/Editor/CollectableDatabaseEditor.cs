using System.Text;
using UnityEditor;
using UnityEngine;

namespace Collectables.EditorTools
{
    /// <summary>
    /// Adds a read-only summary to the CollectableDatabase inspector: content-derived
    /// grand totals, plus the Memory-Shard story tiers (edited in CollectableConstants)
    /// with how many shards each tier's level range actually contains.
    /// </summary>
    [CustomEditor(typeof(CollectableDatabase))]
    public class CollectableDatabaseEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var db = (CollectableDatabase)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Summary (computed)", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                $"Robot Parts placed (all levels): {db.TotalRobotParts}\n" +
                $"CollectableConstants.RobotPartsGrandTotal (HUD shows this): {CollectableConstants.RobotPartsGrandTotal}\n" +
                $"Memory Shards placed (all levels): {db.TotalMemoryShards}",
                MessageType.Info);

            if (db.TotalRobotParts != CollectableConstants.RobotPartsGrandTotal)
            {
                EditorGUILayout.HelpBox(
                    "Placed Robot Part count doesn't yet equal the grand total (56). The HUD always " +
                    "shows the constant; this is expected until every level's parts are placed. " +
                    "When fully authored, the placed sum should match the constant.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Memory Shard Story Tiers", EditorStyles.boldLabel);

            var sb = new StringBuilder();
            foreach (var tier in CollectableConstants.MemoryShardTiers)
            {
                int placed = db.GetMemoryShardCountInRange(tier.FromLevel, tier.ToLevel);
                sb.AppendLine(
                    $"{tier.StoryId}: levels {tier.FromLevel}-{tier.ToLevel} → need {tier.Required} " +
                    $"(placed in range: {placed})");
            }

            EditorGUILayout.HelpBox(
                sb.Length > 0 ? sb.ToString().TrimEnd() : "No tiers configured.",
                MessageType.None);

            EditorGUILayout.LabelField(
                "Edit tiers in CollectableConstants.cs", EditorStyles.miniLabel);
        }
    }
}
