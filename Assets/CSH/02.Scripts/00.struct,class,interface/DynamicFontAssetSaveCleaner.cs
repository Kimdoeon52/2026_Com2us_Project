using TMPro;
using UnityEditor;

public class DynamicFontAssetSaveCleaner : AssetModificationProcessor
{
    static string[] OnWillSaveAssets(string[] paths)
    {
        foreach (string path in paths)
        {
            if (!path.EndsWith(".asset"))
                continue;

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fontAsset == null)
                continue;

            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
                continue;

            // true: 아틀라스 텍스처도 1x1로 줄임 (현재 커밋된 상태와 동일)
            fontAsset.ClearFontAssetData(true);
        }

        return paths;
    }
}