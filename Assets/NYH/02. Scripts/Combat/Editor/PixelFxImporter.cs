using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// pixelfx 스프라이트 시트(PNG + 같은 이름 JSON)를 가져와 칸마다 자르고 PixelFxData 에셋을 만든다 (2026-11-05).
///
/// 사용: Project 창에서 PNG를 선택 → 메뉴 NYH/이펙트/pixelfx 시트 가져오기 (선택한 PNG).
///   · 텍스처: Sprite(Multiple), Point 필터, 압축 없음, PPU = DefaultPpu ÷ JSON pixelScale(도트 합친 배율 — 화면 크기는 그대로, 도트만 뭉툭)
///   · 칸: JSON frames[].frame (위에서부터 픽셀) → Unity 사각형(아래에서부터), 기준점 = meta.pixelfx.pivot (바닥 효과는 발밑)
///   · 에셋: Assets/NYH/04. SO/Effect/<PNG 이름>.asset — 이미 있으면 frames·fps·loop·source만 바꾸고 조정 값은 그대로
/// JSON은 pixelfx --sheet 출력과 같은 모양이다(잘라낸 시트도 같은 모양으로 낸다).
/// </summary>
public static class PixelFxImporter
{
    // 쿄다이 임시 그림 18ppu × 보스 크기 0.4 = 화면에서 1도트 = 1/45 월드 — 이펙트 도트도 같은 크기로
    private const float DefaultPpu = 45f;
    private const string DataFolder = "Assets/NYH/04. SO/Effect";

#pragma warning disable 0649 // JsonUtility가 채우는 칸
    [System.Serializable] private class Rect4 { public int x, y, w, h; }
    [System.Serializable] private class Frame { public Rect4 frame; }
    [System.Serializable] private class FxMeta { public string id; public float fps = 30f; public bool loop; public int pixelScale = 1; public float[] pivot; }
    [System.Serializable] private class Meta { public FxMeta pixelfx; }
    [System.Serializable] private class Sheet { public Frame[] frames; public Meta meta; }
#pragma warning restore 0649

    [MenuItem("NYH/이펙트/pixelfx 시트 가져오기 (선택한 PNG)")]
    private static void ImportSelected()
    {
        foreach (var path in SelectedSheets())
        {
            var data = Import(path);
            if (data != null) Debug.Log($"[PixelFx] {path} → {AssetDatabase.GetAssetPath(data)} ({data.frames.Length}프레임, {data.fps}fps)", data);
        }
    }

    [MenuItem("NYH/이펙트/pixelfx 시트 가져오기 (선택한 PNG)", true)]
    private static bool ImportSelectedValidate() => SelectedSheets().Any();

    private static IEnumerable<string> SelectedSheets() =>
        Selection.objects.Select(AssetDatabase.GetAssetPath)
            .Where(p => p.EndsWith(".png") && File.Exists(Path.ChangeExtension(p, ".json")));

    public static PixelFxData Import(string pngPath)
    {
        var sheet = JsonUtility.FromJson<Sheet>(File.ReadAllText(Path.ChangeExtension(pngPath, ".json")));
        if (sheet?.frames == null || sheet.frames.Length == 0 || sheet.meta?.pixelfx == null)
        {
            Debug.LogError($"[PixelFx] {pngPath}: JSON에 frames·meta.pixelfx가 없음");
            return null;
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
        string name = Path.GetFileNameWithoutExtension(pngPath);

        // 1) 텍스처 설정 — 도트가 번지지 않게
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 4096;
        // 시트가 정한다 — 크기 조정은 PixelFxData.scale로
        importer.spritePixelsPerUnit = DefaultPpu / Mathf.Max(1, sheet.meta.pixelfx.pixelScale);
        importer.SaveAndReimport();

        // 2) 칸 자르기 — 다시 가져와도 같은 이름이면 같은 ID를 써서 참조가 안 끊기게
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var oldIds = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);

        float[] pivot = sheet.meta.pixelfx.pivot;
        var rects = new List<SpriteRect>();
        for (int i = 0; i < sheet.frames.Length; i++)
        {
            var f = sheet.frames[i].frame;
            string frameName = $"{name}_{i:000}";
            var px = pivot != null && pivot.Length == 2 ? new Vector2(pivot[0] / f.w, 1f - pivot[1] / f.h) : new Vector2(0.5f, 0f);
            rects.Add(new SpriteRect
            {
                name = frameName,
                rect = new Rect(f.x, tex.height - f.y - f.h, f.w, f.h),
                alignment = SpriteAlignment.Custom,
                pivot = px,
                spriteID = oldIds.TryGetValue(frameName, out var id) ? id : GUID.Generate(),
            });
        }
        provider.SetSpriteRects(rects.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            ?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
        provider.Apply();
        importer.SaveAndReimport();

        // 3) 데이터 에셋
        var sprites = AssetDatabase.LoadAllAssetsAtPath(pngPath).OfType<Sprite>()
            .Where(s => s.name.StartsWith(name + "_")).OrderBy(s => s.name).ToArray();
        if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets/NYH/04. SO", "Effect");
        string dataPath = $"{DataFolder}/{name}.asset";
        var data = AssetDatabase.LoadAssetAtPath<PixelFxData>(dataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<PixelFxData>();
            AssetDatabase.CreateAsset(data, dataPath);
        }
        data.frames = sprites;
        data.fps = sheet.meta.pixelfx.fps;
        data.loop = sheet.meta.pixelfx.loop;
        data.source = sheet.meta.pixelfx.id;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        return data;
    }
}
