// RE:AL STEEL - 나노봇 군집: 3페 글리치 오라 (11-11)
//
// 3페 그림 기사 몸 전체에 붙는 네모 글리치 (붕괴: 스타레일 은랑풍). 평면 — 고리 · 앞뒤 없음.
// 봇(기사 픽셀)은 그대로 두고, 같은 칸 눈금 위에 VFX 칸을 따로 그린다 (메시 하나, 셰이더 NanoBot Glitch Aura).
//
// 만드는 법 (지금 프레임의 몸 칸 = 구운 픽셀 × 배율²):
//   1) 블록 패치   — 몸을 6칸 · 3칸 블록으로 나눠 몇 개를 색 칸으로 덮는다. 가장자리에 걸친 블록이 더 잘 뽑힌다. 일부는 속이 빈 네모
//   2) 가로 막대   — 몇 줄이 몸을 가로질러 한쪽 바깥으로 삐져나온다 (몸 위는 반투명)
//   3) 어긋난 윤곽 — 실루엣 테두리를 좌우로 1~2칸 밀어 두 색으로 (몸 밖에만)
//   4) 가장자리 블록 · 흩날리는 칸
// 모양은 '글리치 틱'(초당 glitchFps 번)마다 새로 뽑는다. 해시라서 같은 틱 · 같은 프레임이면 같은 모양 (깜빡임이 일정)
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealSteel.NanoBoss
{
    public enum GlitchAuraColor
    {
        [InspectorName("① 은랑 — 시안 + 마젠타")] Wolf = 0,
        [InspectorName("② 기사 — 크림슨 + 금색")] Crimson = 1,
        [InspectorName("③ 보라 + 시안")] Violet = 2,
    }

    [Serializable]
    public class NanoGlitchAura
    {
        [Tooltip("3페 그림 기사 몸에 글리치 오라를 그린다")]
        public bool on = true;
        [Tooltip("색 조합")]
        public GlitchAuraColor color = GlitchAuraColor.Wolf;
        [Tooltip("양 (블록 · 막대 수). 1 = 견본 이미지와 같게")]
        [Range(0f, 2.5f)] public float amount = 1f;
        [Tooltip("초당 모양이 바뀌는 횟수 — 낮을수록 뚝뚝 끊기는 글리치")]
        [Range(2f, 30f)] public float glitchFps = 12f;
        [Tooltip("밝기 (블룸이 있으면 1 넘게 하면 빛난다)")]
        [Range(0.2f, 6f)] public float intensity = 1.6f;
        [Tooltip("칸 둘레 번짐 세기 (0 = 칸만)")]
        [Range(0f, 2f)] public float glow = 0.5f;
        [Tooltip("번짐 크기 (칸 몇 개 폭)")]
        [Range(1f, 6f)] public float glowSize = 3.5f;
        [Tooltip("3페가 시작될 때 · 끝날 때 서서히 켜지고 꺼지는 시간 (초)")]
        [Range(0.05f, 3f)] public float fade = 0.6f;
        [Tooltip("칼날에도 붙인다")]
        public bool onBlade;
        [Tooltip("기사 앞으로 띄우는 거리 (m) — 봇 위에 그려지게")]
        [Range(0f, 0.5f)] public float zOffset = 0.05f;
        [Tooltip("셰이더 (RE_AL STEEL/NanoBot Glitch Aura). 비면 이름으로 찾는다 — 빌드에선 꼭 넣어 둘 것")]
        public Shader shader;

        internal static readonly Color[][] Palettes =
        {
            new[] { C(80, 235, 255), C(255, 70, 210), C(240, 250, 255) },
            new[] { C(255, 60, 80), C(255, 200, 120), C(255, 240, 235) },
            new[] { C(170, 110, 255), C(90, 230, 255), C(245, 240, 255) },
        };
        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
    }

    public partial class RSNanoSwarm
    {
        public const string AuraShaderName = "RE_AL STEEL/NanoBot Glitch Aura";

        [Header("3페 글리치 오라")]
        public NanoGlitchAura glitchAura = new NanoGlitchAura();

        // FillKnight 가 채우는 지금 프레임
        NanoKnightClip auraClip;
        int auraFrame = -1, auraS;
        float auraOx, auraOy, auraFc = 1f, auraZ, auraCs;

        // 몸 칸 (프레임 · 배율이 바뀔 때만 다시)
        NanoKnightClip aMaskClip;
        int aMaskFrame = -1, aMaskS = -1;
        bool aMaskBlade;
        const int AuraPad = 24;
        int aW, aH, aX0, aY0, aTop, aBot;
        bool[] aBody = Array.Empty<bool>(), aEdge = Array.Empty<bool>(), aNear = Array.Empty<bool>();
        int[] aRowLo = Array.Empty<int>(), aRowHi = Array.Empty<int>();
        readonly List<int> aEdgeList = new List<int>();

        // 오라 칸 (틱마다 다시)
        byte[] aCol = Array.Empty<byte>();
        float[] aAl = Array.Empty<float>();
        readonly List<int> aTouched = new List<int>();
        int aTick = -1;
        float aAmount;
        bool aDirty;

        Mesh aMesh;
        Material aMat;
        readonly List<Vector3> aV = new List<Vector3>();
        readonly List<Color> aC = new List<Color>();
        readonly List<Vector4> aUV = new List<Vector4>();
        readonly List<int> aI = new List<int>();

        /// <summary>FillKnight 끝에서 — 지금 그림 기사 프레임과 칸 → 월드 변환</summary>
        void NoteAuraFrame(NanoKnightClip clip, int f, int S, float ox, float oy, float fc, float z, float cs)
        {
            auraClip = clip; auraFrame = f; auraS = S;
            auraOx = ox; auraOy = oy; auraFc = fc; auraZ = z; auraCs = cs;
        }

        /// <summary>Simulate 에서 FillKnight 다음 — 켜짐 정도 · 모양 · 메시</summary>
        void UpdateAura(float dt, float time)
        {
            var g = glitchAura;
            bool live = g != null && g.on && knightSprite && auraClip != null && auraClip.Ready && auraFrame >= 0;
            float target = live ? 1f : 0f;
            aAmount = Mathf.MoveTowards(aAmount, target, dt / Mathf.Max(0.01f, g != null ? g.fade : 0.5f));
            if (aAmount <= 0f || auraClip == null || !auraClip.Ready) { aTouched.Clear(); aV.Clear(); return; }

            int f = Mathf.Clamp(auraFrame, 0, auraClip.frames - 1);
            if (aMaskClip != auraClip || aMaskFrame != f || aMaskS != auraS || aMaskBlade != g.onBlade)
            {
                BuildAuraMask(auraClip, f, auraS, g.onBlade);
                aMaskClip = auraClip; aMaskFrame = f; aMaskS = auraS; aMaskBlade = g.onBlade;
                aDirty = true;
            }
            int tick = Mathf.FloorToInt(time * Mathf.Max(1f, g.glitchFps));
            if (tick != aTick || aDirty)
            {
                GenerateAura(tick, g.amount);
                aTick = tick; aDirty = false;
            }
            BuildAuraMesh(g);
        }

        void BuildAuraMask(NanoKnightClip clip, int f, int S, bool blade)
        {
            int s0 = clip.start[f];
            int n = clip.Body(f) + (blade ? clip.Blade(f) : 0);
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int p = 0; p < n; p++)
            {
                int v = clip.px[s0 + p];
                int X = ((v & 0xFFF) - 2048) * S, Y = (((v >> 12) & 0xFFF) - 2048) * S;
                if (X < minX) minX = X; if (Y < minY) minY = Y;
                if (X + S - 1 > maxX) maxX = X + S - 1; if (Y + S - 1 > maxY) maxY = Y + S - 1;
            }
            if (n == 0) { aW = aH = 0; aEdgeList.Clear(); return; }
            aX0 = minX - AuraPad; aY0 = minY - AuraPad;
            aW = maxX - minX + 1 + AuraPad * 2; aH = maxY - minY + 1 + AuraPad * 2;
            int size = aW * aH;
            if (aBody.Length < size)
            {
                aBody = new bool[size]; aEdge = new bool[size]; aNear = new bool[size];
                aCol = new byte[size]; aAl = new float[size];
            }
            Array.Clear(aBody, 0, size); Array.Clear(aEdge, 0, size); Array.Clear(aNear, 0, size);
            Array.Clear(aAl, 0, size);
            aTouched.Clear();
            for (int p = 0; p < n; p++)
            {
                int v = clip.px[s0 + p];
                int X = ((v & 0xFFF) - 2048) * S - aX0, Y = (((v >> 12) & 0xFFF) - 2048) * S - aY0;
                for (int j = 0; j < S; j++)
                for (int i = 0; i < S; i++) aBody[(Y + j) * aW + X + i] = true;
            }
            if (aRowLo.Length < aH) { aRowLo = new int[aH]; aRowHi = new int[aH]; }
            aTop = -1; aBot = -1;
            aEdgeList.Clear();
            for (int y = 0; y < aH; y++)
            {
                aRowLo[y] = -1; aRowHi[y] = -1;
                for (int x = 0; x < aW; x++)
                {
                    int k = y * aW + x;
                    if (!aBody[k]) continue;
                    if (aRowLo[y] < 0) aRowLo[y] = x;
                    aRowHi[y] = x;
                    if (aTop < 0) aTop = y;
                    aBot = y;
                    bool e = x == 0 || y == 0 || x == aW - 1 || y == aH - 1
                             || !aBody[k - 1] || !aBody[k + 1] || !aBody[k - aW] || !aBody[k + aW];
                    if (e) { aEdge[k] = true; aEdgeList.Add(k); }
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx >= 0 && yy >= 0 && xx < aW && yy < aH) aNear[yy * aW + xx] = true;
                    }
                }
            }
        }

        static float AH(int a, int b, int c, int d = 0)
        {
            unchecked
            {
                uint x = 0x9E3779B9u;
                x = (x ^ (uint)a) * 0x85EBCA6Bu; x ^= x >> 13;
                x = (x ^ (uint)b) * 0x85EBCA6Bu; x ^= x >> 13;
                x = (x ^ (uint)c) * 0x85EBCA6Bu; x ^= x >> 13;
                x = (x ^ (uint)d) * 0x85EBCA6Bu; x ^= x >> 13;
                return (x & 0xFFFF) / 65536f;
            }
        }

        void APut(int x, int y, int ci, float al)
        {
            if (x < 0 || y < 0 || x >= aW || y >= aH) return;
            int k = y * aW + x;
            if (al <= aAl[k]) return;
            if (aAl[k] <= 0f) aTouched.Add(k);
            aAl[k] = al; aCol[k] = (byte)ci;
        }

        void GenerateAura(int t, float dens)
        {
            foreach (int k in aTouched) aAl[k] = 0f;
            aTouched.Clear();
            if (aW <= 0 || aTop < 0) return;

            // 1) 블록 패치 — 몸 전체에 평면으로 (가장자리에 걸친 블록이 3배 잘 뽑힌다)
            for (int pass = 0; pass < 2; pass++)
            {
                int bs = pass == 0 ? 6 : 3, salt = pass + 1;
                float p = (pass == 0 ? 0.13f : 0.10f) * dens;
                for (int by = 0; by < aH; by += bs)
                for (int bx = 0; bx < aW; bx += bs)
                {
                    bool any = false, edge = false;
                    for (int j = 0; j < bs && by + j < aH; j++)
                    for (int i = 0; i < bs && bx + i < aW; i++)
                    {
                        int k = (by + j) * aW + bx + i;
                        if (aNear[k]) any = true;
                        if (aEdge[k]) edge = true;
                    }
                    if (!any) continue;
                    // 해시는 기사 기준 칸(aX0 · aY0 더함)으로 — 프레임이 바뀌어도 같은 자리면 비슷하게
                    int gx = (bx + aX0) / bs, gy = (by + aY0) / bs;
                    float pe = edge ? p : p * 0.3f;
                    if (AH(gx, gy, t, salt) > pe) continue;
                    float kc = AH(gx, gy, t, salt + 7);
                    int ci = kc < 0.12f ? 2 : (kc < 0.42f ? 1 : 0);
                    bool hollow = bs >= 6 && AH(gx, gy, t, salt + 9) < 0.3f;
                    float al = hollow ? 0.95f : 0.4f + 0.45f * AH(gx, gy, t, salt + 3);
                    for (int j = 0; j < bs && by + j < aH; j++)
                    for (int i = 0; i < bs && bx + i < aW; i++)
                    {
                        int k = (by + j) * aW + bx + i;
                        if (!aNear[k]) continue;
                        if (hollow && i > 0 && i < bs - 1 && j > 0 && j < bs - 1) continue;
                        APut(bx + i, by + j, ci, al);
                    }
                }
            }

            // 2) 가로 글리치 막대 — 몸을 가로질러 한쪽 바깥으로
            int bars = Mathf.RoundToInt(5f * dens);
            for (int n = 0; n < bars; n++)
            {
                int y = aTop + (int)(AH(n, t, 31) * (aBot - aTop));
                if (y < 0 || y >= aH || aRowLo[y] < 0) continue;
                int lo = aRowLo[y], hi = aRowHi[y];
                int hgt = 1 + (int)(AH(n, t, 32) * 2f);
                int side = AH(n, t, 33) < 0.5f ? 1 : -1;
                int ext = 3 + (int)(AH(n, t, 34) * 14f);
                int ci = AH(n, t, 35) < 0.6f ? 0 : 1;
                int xa = side > 0 ? lo : lo - ext, xb = side > 0 ? hi + ext : hi;
                for (int x = xa; x <= xb; x++)
                for (int j = 0; j < hgt; j++)
                {
                    int yy = y - j;
                    bool inside = x >= 0 && x < aW && yy >= 0 && yy < aH && aBody[yy * aW + x];
                    APut(x, yy, ci, inside ? 0.45f : 0.9f);
                }
            }

            // 3) 어긋난 윤곽 — 테두리를 좌우로 밀어 두 색 (몸 밖에만)
            int off = 1 + (int)(AH(t, 41, 0) * 2f);
            foreach (int k in aEdgeList)
            {
                int x = k % aW, y = k / aW;
                if (AH((x + aX0) / 3, (y + aY0) / 3, t, 42) >= 0.55f) continue;
                int xr = x + off, xl = x - off;
                if (xr < aW && !aBody[y * aW + xr]) APut(xr, y, 0, 0.85f);
                if (xl >= 0 && !aBody[y * aW + xl]) APut(xl, y, 1, 0.85f);
            }

            // 4) 가장자리 블록 · 흩날리는 칸
            float pb = 0.035f * Mathf.Max(0.3f, dens), pf = pb + 0.015f * Mathf.Max(0.3f, dens);
            for (int q = 0; q < aEdgeList.Count; q++)
            {
                int k = aEdgeList[q];
                int x = k % aW, y = k / aW;
                float r = AH(x + aX0, y + aY0, t, 51);
                if (r < pb)
                {
                    int n = 1 + (int)(AH(q, t, 52) * 3f);
                    int ci = r < pb * 0.23f ? 2 : 0;
                    for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++) APut(x + i + 1, y - j + 1, ci, 0.95f);
                }
                else if (r < pf)
                {
                    int d = 3 + (int)(AH(q, t, 53) * 8f);
                    APut(x + (int)((AH(q, t, 54) - 0.5f) * 2f * d), y + (int)(AH(q, t, 55) * d), 0, 0.8f);
                }
            }
        }

        void BuildAuraMesh(NanoGlitchAura g)
        {
            aV.Clear(); aC.Clear(); aUV.Clear(); aI.Clear();
            if (aTouched.Count == 0) return;
            var pal = NanoGlitchAura.Palettes[Mathf.Clamp((int)g.color, 0, NanoGlitchAura.Palettes.Length - 1)];
            float cs = auraCs, fc = auraFc;
            float z = auraZ - g.zOffset;
            float h = cs * 0.5f, gh = cs * g.glowSize * 0.5f;
            // 번짐 먼저 (칸 아래에 깔리게)
            if (g.glow > 0f)
                foreach (int k in aTouched)
                {
                    int x = k % aW, y = k / aW;
                    float cx = auraOx + fc * (x + aX0 + 0.5f) * cs, cy = auraOy + (y + aY0 + 0.5f) * cs;
                    var c = pal[aCol[k]]; c.a = aAl[k] * aAmount;
                    AQuad(cx, cy, z, gh, c, 1f);
                }
            foreach (int k in aTouched)
            {
                int x = k % aW, y = k / aW;
                float cx = auraOx + fc * (x + aX0 + 0.5f) * cs, cy = auraOy + (y + aY0 + 0.5f) * cs;
                var c = pal[aCol[k]]; c.a = aAl[k] * aAmount;
                AQuad(cx, cy, z, h, c, 0f);
            }
            if (!aMesh)
            {
                aMesh = new Mesh { name = "NanoGlitchAura", hideFlags = HideFlags.DontSave };
                aMesh.MarkDynamic();
                aMesh.indexFormat = IndexFormat.UInt32;
            }
            aMesh.Clear();
            aMesh.SetVertices(aV);
            aMesh.SetColors(aC);
            aMesh.SetUVs(0, aUV);
            aMesh.SetTriangles(aI, 0, true);
        }

        void AQuad(float cx, float cy, float z, float h, Color c, float mode)
        {
            int b = aV.Count;
            aV.Add(new Vector3(cx - h, cy - h, z)); aV.Add(new Vector3(cx - h, cy + h, z));
            aV.Add(new Vector3(cx + h, cy + h, z)); aV.Add(new Vector3(cx + h, cy - h, z));
            // 뒤집혀도 눈금(오른쪽 · 아래)이 화면 기준으로 같게
            aUV.Add(new Vector4(0f, 0f, mode, 0f)); aUV.Add(new Vector4(0f, 1f, mode, 0f));
            aUV.Add(new Vector4(1f, 1f, mode, 0f)); aUV.Add(new Vector4(1f, 0f, mode, 0f));
            aC.Add(c); aC.Add(c); aC.Add(c); aC.Add(c);
            aI.Add(b); aI.Add(b + 1); aI.Add(b + 2);
            aI.Add(b); aI.Add(b + 2); aI.Add(b + 3);
        }

        Material GetAuraMaterial()
        {
            if (aMat) return aMat;
            var sh = glitchAura.shader ? glitchAura.shader : Shader.Find(AuraShaderName);
            if (!sh) return null;
            aMat = new Material(sh) { name = "NanoGlitchAura (자동)", hideFlags = HideFlags.DontSave };
            return aMat;
        }

        /// <summary>OnBeginCamera 끝에서 — 봇 다음에 오라</summary>
        void DrawAura(Camera cam)
        {
            var g = glitchAura;
            if (g == null || aAmount <= 0f || aV.Count == 0 || !aMesh) return;
            var mat = GetAuraMaterial();
            if (!mat) return;
            mat.SetFloat(AuraIds.Intensity, g.intensity);
            mat.SetFloat(AuraIds.Glow, g.glow);
            var rp = new RenderParams(mat)
            {
                camera = cam,
                layer = gameObject.layer,
                worldBounds = aMesh.bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderMesh(rp, aMesh, 0, Matrix4x4.identity);
        }

        void DestroyAura()
        {
            if (aMat) { if (Application.isPlaying) Destroy(aMat); else DestroyImmediate(aMat); }
            if (aMesh) { if (Application.isPlaying) Destroy(aMesh); else DestroyImmediate(aMesh); }
        }

        static class AuraIds
        {
            public static readonly int Intensity = Shader.PropertyToID("_Intensity");
            public static readonly int Glow = Shader.PropertyToID("_GlowStrength");
        }
    }
}
