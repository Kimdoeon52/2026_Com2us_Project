using System.Collections.Generic;
using UnityEngine;

// 좌우 반전 스프라이트를 만들어 캐시한다. 텍스처가 읽기 불가여도 GPU 복사로 만든다
public static class SpriteMirror
{
    private static readonly Dictionary<Sprite, Sprite> Cache = new Dictionary<Sprite, Sprite>();

    public static Sprite FlipX(Sprite source)
    {
        if (source == null)
            return null;

        if (Cache.TryGetValue(source, out Sprite flipped) && flipped != null)
            return flipped;

        flipped = Create(source);
        Cache[source] = flipped;
        return flipped;
    }

    private static Sprite Create(Sprite source)
    {
        // 타이트 패킹은 사각 영역이 없어 원본을 쓴다
        if (source.packed && source.packingMode == SpritePackingMode.Tight)
            return source;

        Texture2D texture = source.texture;
        Rect rect = source.packed ? source.textureRect : source.rect;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);

        // 영역 오른쪽 끝부터 왼쪽으로 읽어 그린다
        Vector2 scale = new Vector2(-rect.width / texture.width, rect.height / texture.height);
        Vector2 offset = new Vector2(rect.xMax / texture.width, rect.y / texture.height);

        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(texture, target, scale, offset);

        var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
        copy.name = $"{texture.name}_FlipX";
        copy.filterMode = texture.filterMode;
        copy.wrapMode = TextureWrapMode.Clamp;

        RenderTexture.active = target;
        copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        copy.Apply(false, true);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        Vector2 pivot = new Vector2(1f - source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
        Vector4 border = new Vector4(source.border.z, source.border.y, source.border.x, source.border.w);

        Sprite sprite = Sprite.Create(copy, new Rect(0, 0, width, height), pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        sprite.name = $"{source.name}_FlipX";
        return sprite;
    }

    // 도메인 리로드를 끈 상태에서도 플레이 진입 시 비어 있어야 한다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Cache.Clear();
}
