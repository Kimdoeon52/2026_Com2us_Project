using UnityEngine;

/// <summary>
/// 바닥 그림자 그림 (2026-11-16 사용자: 그림자가 너무 반듯해서 부자연스러움). 그림 파일 없이 코드로 한 번 그린다.
///
/// 픽셀 아트라 부드러운 그라데이션 대신 투명도 몇 단계 + 4×4 디더링(점 섞기)으로 가장자리를 흐린다 —
/// 안쪽은 진하고, 바깥으로 갈수록 점이 듬성해지며 사라진다. 색은 흰색(그리는 쪽이 검게 칠한다).
///   · Blob: 타원 그림자 (로봇·쿄다이 손)
///   · Band: 띠 그림자 (바닥 예고) — 9분할(Sliced)용. 가운데 칸은 진하고 양 끝 칸만 흐려져서, 너비를 아무리 늘려도 끝만 부드럽다
/// 저장하지 않는다(HideAndDontSave) — 씬에 빈 참조가 남지 않게.
/// </summary>
public static class ShadowSprites
{
    private const int BlobW = 32, BlobH = 12;
    private const int BandW = 24, BandH = 8, BandBorder = 10; // 띠 양 끝 흐려지는 칸(픽셀)

    private static Sprite blob, band;

    // 4×4 Bayer 행렬 (0~15) — 같은 투명도라도 점이 고르게 흩어지게
    private static readonly int[] Bayer =
    {
        0, 8, 2, 10,
        12, 4, 14, 6,
        3, 11, 1, 9,
        15, 7, 13, 5,
    };

    /// <summary>타원 그림자. 피벗 가운데</summary>
    public static Sprite Blob
    {
        get
        {
            if (blob == null)
                blob = Make("그림자_타원", BlobW, BlobH, Vector4.zero, (x, y) =>
                {
                    float nx = (x + 0.5f) / BlobW * 2f - 1f, ny = (y + 0.5f) / BlobH * 2f - 1f;
                    return 1f - Mathf.Sqrt(nx * nx + ny * ny); // 가운데 1 → 테두리 0
                });
            return blob;
        }
    }

    /// <summary>띠 그림자 (Sliced용, 테두리 = 좌우 BandBorder 픽셀). 피벗 가운데</summary>
    public static Sprite Band
    {
        get
        {
            if (band == null)
                band = Make("그림자_띠", BandW, BandH, new Vector4(BandBorder, 0f, BandBorder, 0f), (x, y) =>
                {
                    // 가로: 양 끝 BandBorder 칸에서만 흐려짐. 세로: 위아래가 조금 흐려짐
                    float fromEdge = Mathf.Min(x + 0.5f, BandW - x - 0.5f);
                    float fx = Mathf.Clamp01(fromEdge / BandBorder);
                    float ny = (y + 0.5f) / BandH * 2f - 1f;
                    float fy = 1f - ny * ny;
                    return Mathf.Min(fx, fy);
                },
                // 가운데 칸은 가로로 늘어나므로 디더링하면 점이 긴 줄무늬가 된다 — 양 끝 칸만 디더링
                (x, y) => x < BandBorder || x >= BandW - BandBorder);
            return band;
        }
    }

    /// <summary>띠 그림자 한쪽 끝의 흐려지는 길이(스프라이트 로컬 단위, PPU 1 기준 픽셀 수)</summary>
    public static float BandBorderPixels => BandBorder;

    // falloff(x, y): 0(바깥) ~ 1(안쪽). 단계로 끊고(안쪽 진함 → 바깥 옅음) 디더링으로 단계 사이를 섞는다
    // dither(x, y): 이 칸을 디더링할지 (null = 전부). 안 하면 가장 가까운 단계로 반올림
    private static Sprite Make(string name, int w, int h, Vector4 border, System.Func<int, int, float> falloff,
                               System.Func<int, int, bool> dither = null)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            name = name,
        };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float f = Mathf.Clamp01(falloff(x, y));
            // 부드러운 감쇠(가운데는 거의 꽉, 바깥으로 빨리 옅어짐) → 0~4단계
            float a = Mathf.SmoothStep(0f, 1f, f * 1.6f);
            float level = a * 4f;
            int lo = Mathf.FloorToInt(level);
            float frac = level - lo;
            // 단계 사이는 디더링: 이 칸의 Bayer 문턱보다 크면 한 단계 위 (디더링 안 하는 칸은 반올림)
            bool useDither = dither == null || dither(x, y);
            if (useDither ? frac > (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f : frac >= 0.5f) lo++;
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp(lo, 0, 4) / 4f * 255f);
            pixels[y * w + x] = new Color32(255, 255, 255, alpha);
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect, border);
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
