using System.IO;
using UnityEditor;

namespace Collectables.EditorTools
{
    /// <summary>Shared editor path helpers for the collectable tools.</summary>
    internal static class CollectableToolsPaths
    {
        /// <summary>Creates an asset folder (and any missing parents) if it doesn't exist.</summary>
        public static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;

            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolder);

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
