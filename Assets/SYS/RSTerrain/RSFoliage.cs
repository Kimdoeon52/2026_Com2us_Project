// RE:AL STEEL - RS Terrain 풀 · 꽃 심기 (RSFoliage)
//
// 스프라이트 시트 한 장에 그린 풀 · 꽃을 지면에 세워 심는다 (카메라를 향해 서는 빌보드).
// 지형 모양은 안 바꾼다. 지형이 바뀌면 최종 지면(요소 + 브러시)에 맞춰 다시 심는다.
//
// 심는 곳
//   · 칠한 레이어 따라: 풀 레이어(또는 고른 레이어)를 칠한 곳에 저절로 자란다 — 브러시로 풀을 칠하면 그 위에 난다
//   · 원 / 지형 전체: 칠과 상관없이 그 안에 흩뿌린다 (길 · 콘크리트 · 진흙은 피함)
//
// 배치는 지형 위 고정 격자(칸마다 무작위 한 자리)라서, 칠이나 값을 바꿔도 바뀐 곳만 달라진다.
// 메시는 10m 조각마다 하나로 합친다 (포기 수천 개 = 드로우콜 몇 개). 씬에는 저장하지 않는다.
// 셰이더: RE_AL STEEL/Foliage Sprite (바람 흔들림 · 캐릭터가 지나가면 눕기)
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Terrain
{
    [RSSummary("풀꽃 자동 채우기", "풀을 칠한 곳(또는 원 · 전체)에 시트의 풀 · 꽃을 저절로 촘촘히 세운다. 하나씩 골라 놓으려면 이 요소 대신 '풀꽃 배치' 창 (Tools/RE_AL STEEL/Stage).\n" +
        "· 시트를 넣고 [시트 자르기] → 투명한 틈 기준으로 하나씩 잘려 목록에 들어간다 (항목마다 비율 조절)\n" +
        "· 심는 곳 '칠한 레이어 따라' = 브러시로 풀을 칠한 곳에 저절로 난다\n" +
        "· 바람 · 눕기 세기는 머티리얼(RE_AL STEEL/Foliage Sprite)에서 조절")]
    [AddComponentMenu("RE_AL STEEL/Terrain/풀꽃 자동 채우기")]
    public class RSFoliage : RSTerrainFeature
    {
        public enum Area
        {
            [InspectorName("칠한 레이어 따라 (지형 전체)")] Layer,
            [InspectorName("원 (반경)")] Circle,
            [InspectorName("지형 전체")] Whole,
        }

        [System.Serializable]
        public class Item
        {
            [Tooltip("시트 안 위치 (픽셀, 왼쪽 아래 기준)")]
            public RectInt rect;
            [Range(0f, 5f), Tooltip("나오는 비율 (0 = 안 씀)")]
            public float weight = 1f;
        }

        [Header("스프라이트")]
        [RSHelp("스프라이트 시트 한 장. [시트 자르기] 가 투명한 틈을 기준으로 하나씩 잘라 목록에 넣는다. 머티리얼은 비워 두면 버튼으로 만든다.")]
        [Tooltip("풀 · 꽃을 그린 시트 (배경 투명). 가져오기 설정: 필터 Point · 압축 없음 · 밉맵 끔 권장")]
        public Texture2D sheet;
        [Tooltip("시트에서 잘라낸 풀 · 꽃 (시트 자르기 버튼이 채운다)")]
        public List<Item> items = new List<Item>();
        [Tooltip("픽셀 / m. 지형 머티리얼의 PPU(기본 32)와 같게 두면 도트 크기가 바닥과 맞는다")]
        public float ppu = 32f;
        [Tooltip("RE_AL STEEL/Foliage Sprite 머티리얼")]
        public Material material;

        [Header("심는 곳")]
        [RSHelp("칠한 레이어 따라 = 풀을 칠한 곳에 저절로 난다. 원 / 지형 전체 = 칠과 상관없이 흩뿌린다 (길 · 콘크리트 · 진흙은 피함).")]
        [Tooltip("칠한 레이어 따라 / 원 / 지형 전체")]
        public Area area = Area.Layer;
        [Tooltip("'칠한 레이어 따라' 에서 따라갈 레이어")]
        public RSLayer layer = RSLayer.Grass;
        [Range(0.05f, 0.95f), Tooltip("레이어가 이만큼 이상 칠해진 곳에만 난다. 낮추면 경계 밖으로 조금 번진다")]
        public float threshold = 0.4f;
        [Tooltip("원 모드의 반경 (m). 원 핸들로도 조절")]
        public float radius = 5f;
        [Range(0f, 1f), Tooltip("원 가장자리로 갈수록 드물게 (0 = 딱 끊김)")]
        public float edgeFade = 0.5f;
        [Range(0.3f, 1f), Tooltip("이보다 가파른 곳엔 안 난다 (1 = 평지만)")]
        public float minFlatness = 0.7f;
        [Tooltip("원 · 전체 모드: 길(R) · 콘크리트(G) · 진흙(A) 위는 피한다")]
        public bool avoidPaths = true;

        [Header("밀도 · 모양")]
        [RSHelp("몇 포기를 어떻게 심을지. 밀도를 바꿔도 이미 있던 자리는 그대로 남는다.")]
        [Range(0.2f, 30f), Tooltip("1㎡ 당 포기 수. 풀밭은 4 ~ 10")]
        public float density = 6f;
        [Range(0f, 1f), Tooltip("뭉치기 — 클수록 무더기 · 빈터가 뚜렷하다")]
        public float clumping = 0.45f;
        [Tooltip("크기 무작위 (최소 ~ 최대 배율)")]
        public Vector2 scaleRange = new Vector2(0.85f, 1.15f);
        [Tooltip("무작위로 좌우 뒤집기")]
        public bool randomFlip = true;
        [Range(0f, 0.35f), Tooltip("포기마다 밝기 · 색을 조금씩 다르게")]
        public float colorVariation = 0.12f;
        [Tooltip("밑동을 땅에 묻는 깊이 (m). 경사에서 뜨지 않게")]
        public float sink = 0.04f;

        [Header("렌더")]
        [Tooltip("메시 조각 크기 (m). 작을수록 화면 밖 조각이 잘 빠지지만 드로우콜이 는다")]
        public float tileSize = 10f;

        public override Color GizmoColor { get { return new Color(0.4f, 0.95f, 0.35f); } }
        public override int DefaultOrder { get { return 110; } }

        // 지형 모양은 안 건드린다
        protected override Rect ComputeBounds() { return new Rect(0f, 0f, 0f, 0f); }
        public override void Apply(ref RSSample s, float x, float z) { }

        // 값만 바꿨을 땐 지형 전체가 아니라 풀만 다시 심는다 (가볍게)
        protected override void OnValidate()
        {
            ppu = Mathf.Max(1f, ppu);
            tileSize = Mathf.Max(2f, tileSize);
            if (scaleRange.y < scaleRange.x) scaleRange.y = scaleRange.x;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                var t = Owner;
                if (t != null && t.HasHeights) t.RebuildFoliage();
            };
#endif
        }

        // ─────────────────────────────────────────────────────────────
        // 심기
        // ─────────────────────────────────────────────────────────────

        public class Sink
        {
            public readonly Dictionary<long, Buffer> tiles = new Dictionary<long, Buffer>();
            public class Buffer
            {
                public readonly List<Vector3> v = new List<Vector3>();
                public readonly List<Vector2> uv = new List<Vector2>();
                public readonly List<Vector4> quad = new List<Vector4>();
                public readonly List<Color> col = new List<Color>();
                public readonly List<int> t = new List<int>();
                public float maxH, maxW;
            }
            public Buffer Get(long key)
            {
                if (!tiles.TryGetValue(key, out var b)) tiles[key] = b = new Buffer();
                return b;
            }
        }

        /// <summary>이 요소의 풀 · 꽃을 조각별 버퍼에 담는다 (지형 로컬 좌표)</summary>
        public int Plant(RSTerrain t, Sink output)
        {
            if (sheet == null || items == null || items.Count == 0 || density <= 0f) return 0;
            UpdateFrame(t);

            // 비율 누적
            float total = 0f;
            foreach (var it in items) if (it != null && it.weight > 0f && it.rect.width > 0 && it.rect.height > 0) total += it.weight;
            if (total <= 0f) return 0;

            float W = sheet.width, H = sheet.height;
            // 고정된 잔 격자(후보 자리)에서 밀도만큼 뽑는다 — 밀도를 올려도 원래 포기는 그대로, 새 포기만 늘어난다
            const float cell = 0.18f;                      // 1㎡ 당 후보 약 31 자리 (밀도 최대 30)
            float keep = Mathf.Clamp01(density * cell * cell);
            float tile = Mathf.Max(2f, tileSize);

            // 심을 범위 (지형 로컬)
            float hx = t.size.x * 0.5f, hz = t.size.y * 0.5f;
            float x0 = -hx, x1 = hx, z0 = -hz, z1 = hz;
            if (area == Area.Circle)
            {
                x0 = Mathf.Max(x0, c.x - radius); x1 = Mathf.Min(x1, c.x + radius);
                z0 = Mathf.Max(z0, c.z - radius); z1 = Mathf.Min(z1, c.z + radius);
            }
            int i0 = Mathf.FloorToInt(x0 / cell), i1 = Mathf.CeilToInt(x1 / cell);
            int j0 = Mathf.FloorToInt(z0 / cell), j1 = Mathf.CeilToInt(z1 / cell);

            int planted = 0;
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    // 칸마다 고정 난수 → 값을 바꿔도 이 칸의 포기는 같은 자리 · 같은 모양
                    float roll = RSMath.Hash01(i, j, 1, seed);
                    if (roll >= keep) continue;                    // 밀도 (먼저 걸러서 가볍게)
                    float x = (i + RSMath.Hash01(i, j, 2, seed)) * cell;
                    float z = (j + RSMath.Hash01(i, j, 3, seed)) * cell;
                    if (!t.InsideXZ(x, z)) continue;

                    float p = 1f;
                    if (area == Area.Circle)
                    {
                        float d = new Vector2(x - c.x, z - c.z).magnitude / Mathf.Max(0.01f, radius);
                        if (d > 1f) continue;
                        if (edgeFade > 0.001f) p *= 1f - SS(Mathf.Clamp01((d - (1f - edgeFade)) / edgeFade));
                    }

                    if (area == Area.Layer)
                    {
                        float lw = LayerWeight(t, x, z);
                        p *= SS(Mathf.Clamp01((lw - (threshold - 0.12f)) / 0.24f));
                    }
                    else if (avoidPaths)
                    {
                        Vector4 w = t.SampleWeights(x, z);
                        if (w.x > 0.3f || w.y > 0.35f || w.w > 0.35f) continue;
                    }
                    if (p <= 0.001f) continue;

                    if (clumping > 0.001f)
                    {
                        float n = N(x, z, 0.32f, 5) * 0.7f + N(x, z, 0.9f, 6) * 0.3f;
                        p *= Mathf.Lerp(1f, SS(Mathf.Clamp01((n - 0.35f) / 0.3f)), clumping);
                    }
                    if (roll >= keep * p) continue;

                    if (t.SampleNormal(x, z).y < minFlatness) continue;

                    // 어떤 풀 · 꽃
                    float pick = RSMath.Hash01(i, j, 4, seed) * total;
                    Item item = null;
                    foreach (var it in items)
                    {
                        if (it == null || it.weight <= 0f || it.rect.width <= 0 || it.rect.height <= 0) continue;
                        item = it;
                        pick -= it.weight;
                        if (pick <= 0f) break;
                    }
                    if (item == null) continue;

                    float sc = Mathf.Lerp(scaleRange.x, scaleRange.y, RSMath.Hash01(i, j, 5, seed));
                    float w2 = item.rect.width / ppu * sc * 0.5f;
                    float h = item.rect.height / ppu * sc;
                    bool flip = randomFlip && RSMath.Hash01(i, j, 6, seed) < 0.5f;
                    float rnd = RSMath.Hash01(i, j, 7, seed);

                    float bright = 1f + (RSMath.Hash01(i, j, 8, seed) - 0.5f) * 2f * colorVariation;
                    float warm = (RSMath.Hash01(i, j, 9, seed) - 0.5f) * colorVariation;
                    var col = new Color(Mathf.Clamp01(bright + warm), Mathf.Clamp01(bright), Mathf.Clamp01(bright - warm), 1f);

                    var pivot = new Vector3(x, t.SampleHeight(x, z) - sink, z);

                    // UV (반 텍셀 안쪽으로 — 옆 그림이 번져 들어오지 않게)
                    float u0 = (item.rect.xMin + 0.01f) / W, u1 = (item.rect.xMax - 0.01f) / W;
                    float v0 = (item.rect.yMin + 0.01f) / H, v1 = (item.rect.yMax - 0.01f) / H;
                    if (flip) { float tmp = u0; u0 = u1; u1 = tmp; }

                    long key = ((long)Mathf.FloorToInt((x + hx) / tile) << 32) | (uint)Mathf.FloorToInt((z + hz) / tile);
                    var b = output.Get(key);
                    int vi = b.v.Count;
                    for (int k = 0; k < 4; k++) { b.v.Add(pivot); b.col.Add(col); }
                    b.uv.Add(new Vector2(u0, v0)); b.quad.Add(new Vector4(-w2, 0f, h, rnd));
                    b.uv.Add(new Vector2(u1, v0)); b.quad.Add(new Vector4( w2, 0f, h, rnd));
                    b.uv.Add(new Vector2(u1, v1)); b.quad.Add(new Vector4( w2, h,  h, rnd));
                    b.uv.Add(new Vector2(u0, v1)); b.quad.Add(new Vector4(-w2, h,  h, rnd));
                    b.t.Add(vi); b.t.Add(vi + 2); b.t.Add(vi + 1);
                    b.t.Add(vi); b.t.Add(vi + 3); b.t.Add(vi + 2);
                    b.maxH = Mathf.Max(b.maxH, h);
                    b.maxW = Mathf.Max(b.maxW, w2);
                    planted++;
                }
            return planted;
        }

        float LayerWeight(RSTerrain t, float x, float z)
        {
            if (layer == RSLayer.Grass) return t.SampleGrass(x, z);
            Vector4 w = t.SampleWeights(x, z);
            switch (layer)
            {
                case RSLayer.R: return w.x;
                case RSLayer.G: return w.y;
                case RSLayer.B: return w.z;
                case RSLayer.A: return w.w;
                case RSLayer.Base: return Mathf.Clamp01(1f - (w.x + w.y + w.z + w.w + t.SampleGrass(x, z)));
                default: return 0f;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (area == Area.Circle) DrawOutline(CirclePts(radius), true, true);
        }
    }
}
