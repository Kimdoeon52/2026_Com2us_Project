// RE:AL STEEL - RS Terrain 공통 (레이어 · 샘플 · 노이즈 · 도형 거리)
using UnityEngine;

namespace RealSteel.Terrain
{
    /// <summary>
    /// 지형 레이어. 정점 컬러 채널과 1:1 (TerrainSplatPixelLit 셰이더).
    /// 바탕 = 정점 컬러 0 → 윗면은 흙, 가파른 면은 절벽 (셰이더가 법선으로 가름).
    /// </summary>
    public enum RSLayer
    {
        [InspectorName("없음 (칠하지 않음)")] None = -1,
        [InspectorName("바탕 (흙 / 절벽)")]  Base = 0,
        [InspectorName("R  길")]             R = 1,
        [InspectorName("G  콘크리트")]        G = 2,
        [InspectorName("B  고물")]            B = 3,
        [InspectorName("A  진흙 / 기름")]      A = 4,
        [InspectorName("풀")]                  Grass = 5,
    }

    /// <summary>지형 한 점의 상태. 요소들이 순서대로 이걸 고쳐 나간다.</summary>
    public struct RSSample
    {
        /// <summary>높이 (지형 로컬 Y)</summary>
        public float h;

        /// <summary>레이어 가중치 R G B A (0 = 바탕)</summary>
        public Vector4 w;

        /// <summary>풀 레이어 가중치 (다섯 번째 레이어 — 메시 UV2.x 로 셰이더에 간다)</summary>
        public float grass;

        /// <summary>바탕 노이즈 중 잔요철 성분. 길이 "지형 따라" 모드에서 이만큼 눌러 편다.</summary>
        public float detail;

        /// <summary>1 이면 이 자리의 정점은 XZ 흔들기·면 흔들기·경계 휘기를 하지 않는다 (다리 받침 등 반듯해야 할 곳).</summary>
        public float lockXZ;

        /// <summary>레이어를 m 만큼 덮어 칠한다. 바탕을 칠하면 다른 레이어를 지운다.</summary>
        public void Paint(RSLayer layer, float m)
        {
            if (layer == RSLayer.None || m <= 0f) return;
            float t = Mathf.Clamp01(m);
            w = Vector4.Lerp(w, RSMath.LayerVector(layer), t);
            grass = Mathf.Lerp(grass, RSMath.LayerGrass(layer), t);
        }
    }

    public static class RSMath
    {
        public static float SmoothStep(float t) { return t * t * (3f - 2f * t); }

        public static Vector4 LayerVector(RSLayer l)
        {
            switch (l)
            {
                case RSLayer.R: return new Vector4(1f, 0f, 0f, 0f);
                case RSLayer.G: return new Vector4(0f, 1f, 0f, 0f);
                case RSLayer.B: return new Vector4(0f, 0f, 1f, 0f);
                case RSLayer.A: return new Vector4(0f, 0f, 0f, 1f);
                default:        return Vector4.zero;
            }
        }

        /// <summary>풀 레이어면 1</summary>
        public static float LayerGrass(RSLayer l) { return l == RSLayer.Grass ? 1f : 0f; }

        public static Color LayerColor(RSLayer l)
        {
            Vector4 v = LayerVector(l);
            return new Color(v.x, v.y, v.z, v.w);
        }

        /// <summary>정수 해시 → [0,1)</summary>
        public static float Hash01(int a, int b, int c, int seed)
        {
            unchecked
            {
                uint h = (uint)a * 0x8da6b343u ^ (uint)b * 0xd8163841u ^ (uint)c * 0xcb1ab31fu ^ (uint)seed * 0x9e3779b9u;
                h ^= h >> 16; h *= 0x7feb352du;
                h ^= h >> 15; h *= 0x846ca68bu;
                h ^= h >> 16;
                return (h & 0xffffffu) / 16777216f;
            }
        }

        /// <summary>Perlin [0,1]. channel·seed 마다 다른 오프셋 → 서로 독립된 노이즈.</summary>
        public static float Noise(float x, float z, float freq, int channel, int seed)
        {
            float ox = 37f + Hash01(channel, seed, 11, 0) * 400f;
            float oz = 53f + Hash01(channel, seed, 12, 0) * 400f;
            return Mathf.Clamp01(Mathf.PerlinNoise(x * freq + ox, z * freq + oz));
        }

        /// <summary>Perlin [-1,1]</summary>
        public static float SNoise(float x, float z, float freq, int channel, int seed)
        {
            return (Noise(x, z, freq, channel, seed) - 0.5f) * 2f;
        }

        /// <summary>원점 중심 둥근 사각형 부호 거리 (안쪽 음수). half = 둥글기를 뺀 반폭.</summary>
        public static float RoundedRectDist(Vector2 p, Vector2 half, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x) - half.x, Mathf.Abs(p.y) - half.y);
            Vector2 qp = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return qp.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        /// <summary>보로노이 — 가장 가까운 점, 두 번째 거리, 칸 좌표</summary>
        public static void Voronoi(Vector2 p, float cs, int seed, out int cx, out int cz, out Vector2 fp, out float d1, out float d2)
        {
            cs = Mathf.Max(0.05f, cs);
            int gx = Mathf.FloorToInt(p.x / cs), gz = Mathf.FloorToInt(p.y / cs);
            d1 = d2 = 1e9f; cx = gx; cz = gz; fp = p;
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    int ax = gx + i, az = gz + j;
                    var f = new Vector2((ax + 0.15f + 0.7f * Hash01(ax, az, 11, seed)) * cs,
                                        (az + 0.15f + 0.7f * Hash01(ax, az, 12, seed)) * cs);
                    float d = (p - f).magnitude;
                    if (d < d1) { d2 = d1; d1 = d; cx = ax; cz = az; fp = f; }
                    else if (d < d2) d2 = d;
                }
        }

        /// <summary>브러시 감쇠. hardness = 가운데 꽉 찬 반경 비율</summary>
        public static float Falloff(float t, float hardness)
        {
            if (t >= 1f) return 0f;
            if (t <= hardness) return 1f;
            return 1f - SmoothStep(Mathf.Clamp01((t - hardness) / Mathf.Max(1e-4f, 1f - hardness)));
        }
    }
}
