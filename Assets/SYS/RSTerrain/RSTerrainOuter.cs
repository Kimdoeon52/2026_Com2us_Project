// RE:AL STEEL - RS Terrain : 외곽 지형 (플레이 구역 밖 배경)
//
// RSTerrain 의 일부 (partial). 설정은 RSTerrain 인스펙터의 "외곽" 칸.
//
// 플레이 구역(size) 바깥을 성긴 격자(outerCell)로 둘러싼다.
//  · 가장자리 이음새: 안쪽 줄은 본 지형 테두리 정점을 그대로 쓴다 (높이 · 칠 · 흔든 위치까지 똑같음)
//    → 외곽 칸이 본 지형 칸 여러 개와 닿으면 부채꼴로 이어서 틈(T 자 이음)이 안 생긴다
//  · 높이: 가장자리 높이를 이어받아 평평하게 조금 가다가(outerFlat) 바깥으로 솟는다 (outerRise, 분지 모양)
//          + 둘레 따라 높낮이(스카이라인) + 큰 굴곡 + 계단식 단(outerTerrace)
//  · 칠: 가장자리 칠(길 · 콘크리트 등)이 조금 이어지다 사라지고, 솟는 곳엔 고물(B) 얼룩
//  · 메시: 남 · 북 · 서 · 동 네 조각 (화면 밖 조각은 컬링된다)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealSteel.Terrain
{
    public partial class RSTerrain
    {
        class OuterPiece
        {
            public GameObject go;
            public Mesh mesh;
            public MeshFilter mf;
            public MeshRenderer mr;
            public MeshCollider mc;
        }

        [System.NonSerialized] readonly List<OuterPiece> outerPieces = new List<OuterPiece>();

        static readonly string[] OuterNames = { "Outer_South", "Outer_North", "Outer_West", "Outer_East" };

        void DestroyOuter()
        {
            foreach (var p in outerPieces) { SafeDestroy(p.mesh); SafeDestroy(p.go); }
            outerPieces.Clear();
        }

        // ─────────────────────────────────────────────────────────────
        // 높이 · 칠
        // ─────────────────────────────────────────────────────────────

        /// <summary>외곽 한 점 (지형 로컬 XZ) 의 높이와 칠</summary>
        float OuterSample(float x, float z, out Color col, out float grass)
        {
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            float ex = Mathf.Clamp(x, -hx, hx), ez = Mathf.Clamp(z, -hz, hz);
            float d = new Vector2(x - ex, z - ez).magnitude;   // 플레이 구역까지 거리 (모서리 바깥은 둥글게)

            float edgeH = SampleHeight(ex, ez);
            Vector4 edgeW = SampleWeights(ex, ez);
            float edgeG = SampleGrass(ex, ez);

            // 가장자리 높이 → 바탕 높이로 서서히 (가장자리에 둔덕 · 도랑이 있어도 자연스럽게 풀림)
            float flat = Mathf.Max(0.5f, outerFlat);
            float tBlend = RSMath.SmoothStep(Mathf.Clamp01(d / (flat + 5f)));
            float baseH = baseHeight
                        + RSMath.SNoise(x, z, rollFreq, 0, seed) * rollAmp
                        + RSMath.SNoise(x, z, bumpFreq, 1, seed) * bumpAmp;
            float h = Mathf.Lerp(edgeH, baseH, tBlend);

            // 솟기
            float span = Mathf.Max(1f, outerWidth - outerFlat);
            float t = Mathf.Clamp01((d - outerFlat) / span);
            float sky = Mathf.Max(0.1f, 1f + outerSkylineVar * RSMath.SNoise(x, z, 0.035f, 61, seed));
            float rise = Mathf.Max(0f, outerRise) * sky * Mathf.Pow(t, Mathf.Max(0.5f, outerRiseCurve));
            float grow = RSMath.SmoothStep(Mathf.Clamp01(t * 3f));
            rise += outerNoise * RSMath.SNoise(x, z, 0.08f, 62, seed) * grow;
            rise = Mathf.Max(0f, rise);

            // 계단식 단: 단 높이의 (1-k) 만큼은 평평, k 만큼은 가파른 벽
            if (outerTerrace > 0.05f && outerTerraceSharp > 0.001f && rise > 0f)
            {
                float step = outerTerrace;
                float k = Mathf.Lerp(1f, 0.12f, Mathf.Clamp01(outerTerraceSharp));
                float q = rise / step;
                float fl = Mathf.Floor(q), fr = q - fl;
                float fr2 = Mathf.Clamp01((fr - (1f - k)) / k);
                float terraced = (fl + fr2) * step;
                rise = Mathf.Lerp(rise, terraced, grow);
            }
            h += rise;

            // 잔요철 · 면 흔들기
            h += RSMath.SNoise(x, z, gritFreq, 2, seed) * gritAmp;

            // 칠: 가장자리 칠이 이어지다 사라짐 + 고물 얼룩
            Vector4 w = Vector4.Lerp(edgeW, Vector4.zero, tBlend);
            float g = Mathf.Lerp(edgeG, 0f, tBlend);
            if (outerJunk > 0.001f)
            {
                float n = RSMath.SNoise(x, z, 0.11f, 63, seed) * 0.6f + RSMath.SNoise(x, z, 0.37f, 64, seed) * 0.4f;
                float junk = Mathf.Clamp01((n + outerJunk * 2f - 1f) * 3f) * RSMath.SmoothStep(Mathf.Clamp01(t * 4f));
                w = Vector4.Lerp(w, new Vector4(0f, 0f, 1f, 0f), junk);
                g = Mathf.Lerp(g, 0f, junk);
            }
            w = new Vector4(Mathf.Clamp01(w.x), Mathf.Clamp01(w.y), Mathf.Clamp01(w.z), Mathf.Clamp01(w.w));
            g = Mathf.Clamp01(g);
            float sum = w.x + w.y + w.z + w.w + g;
            if (sum > 1f) { w /= sum; g /= sum; }
            col = new Color(w.x, w.y, w.z, w.w);
            grass = g;
            return h;
        }

        // ─────────────────────────────────────────────────────────────
        // 메시
        // ─────────────────────────────────────────────────────────────

        // 격자 좌표 한 줄: 바깥 칸들 + (본 지형 정점 k 개마다) + 바깥 칸들
        struct Axis
        {
            public float[] pos;    // 좌표
            public int[] main;     // 본 지형 정점 번호 (구역 밖이면 -1)
            public int s0, s1;     // 구역 시작 / 끝 칸 번호 (pos[s0] = -반폭, pos[s1] = +반폭)
        }

        Axis MakeAxis(float half, float cellMain, int cellsMain, float outerC, int nOut)
        {
            int k = Mathf.Max(1, Mathf.RoundToInt(outerC / Mathf.Max(1e-4f, cellMain)));
            var pos = new List<float>(); var main = new List<int>();
            for (int n = nOut; n >= 1; n--) { pos.Add(-half - n * outerC); main.Add(-1); }
            int s0 = pos.Count;
            for (int i = 0; i < cellsMain; i += k) { pos.Add(-half + i * cellMain); main.Add(i); }
            pos.Add(half); main.Add(cellsMain);
            int s1 = pos.Count - 1;
            for (int n = 1; n <= nOut; n++) { pos.Add(half + n * outerC); main.Add(-1); }
            return new Axis { pos = pos.ToArray(), main = main.ToArray(), s0 = s0, s1 = s1 };
        }

        void BuildOuter()
        {
            if (!outerEnabled || hF == null) { DestroyOuter(); return; }

            var root = EnsureGenRoot();
            if (outerPieces.Count != 4 || outerPieces.Exists(p => p.go == null))
            {
                DestroyOuter();
                for (int s = 0; s < 4; s++)
                {
                    var p = new OuterPiece();
                    p.go = new GameObject(OuterNames[s]);
                    p.go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                    p.go.transform.SetParent(root, false);
                    p.mf = p.go.AddComponent<MeshFilter>();
                    p.mr = p.go.AddComponent<MeshRenderer>();
                    p.mesh = new Mesh { name = "RST_Outer", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
                    p.mf.sharedMesh = p.mesh;
                    outerPieces.Add(p);
                }
            }

            float oc = Mathf.Clamp(outerCell, 0.25f, 8f);
            int nOut = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1f, outerWidth) / oc));
            Axis ax = MakeAxis(size.x * 0.5f, csx, cx, oc, nOut);
            Axis az = MakeAxis(size.y * 0.5f, csz, cz, oc, nOut);
            int NX = ax.pos.Length, NZ = az.pos.Length;

            // 격자 정점 (구역 안쪽은 안 씀)
            var V = new Vector3[NX * NZ];
            var C = new Color[NX * NZ];
            var G = new float[NX * NZ];
            float jit = vertexJitter * oc;
            for (int a = 0; a < NX; a++)
                for (int b = 0; b < NZ; b++)
                {
                    int k = a * NZ + b;
                    bool inX = a >= ax.s0 && a <= ax.s1, inZ = b >= az.s0 && b <= az.s1;
                    if (inX && inZ)
                    {
                        bool onEdge = a == ax.s0 || a == ax.s1 || b == az.s0 || b == az.s1;
                        if (!onEdge) continue;
                        int mk = Idx(ax.main[a], az.main[b]);
                        V[k] = P(mk); C[k] = wF[mk]; G[k] = gF[mk];
                        continue;
                    }
                    float x = ax.pos[a], z = az.pos[b];
                    // 흔들기 (가장 바깥 테두리 제외). 칸의 0.3 이하라 구역 안으로 넘어가지 않는다
                    bool border = a == 0 || b == 0 || a == NX - 1 || b == NZ - 1;
                    if (jit > 0f && !border)
                    {
                        x += (RSMath.Hash01(a, b, 71, seed) - 0.5f) * 2f * jit;
                        z += (RSMath.Hash01(a, b, 72, seed) - 0.5f) * 2f * jit;
                    }
                    float h = OuterSample(x, z, out Color col, out float gr);
                    h += (RSMath.Hash01(a, b, 73, seed) - 0.5f) * 2f * facetJitter * 2f;
                    V[k] = new Vector3(x, h, z); C[k] = col; G[k] = gr;
                }

            for (int s = 0; s < 4; s++)
            {
                sV.Clear(); sN.Clear(); sC.Clear(); sU.Clear(); sT.Clear(); sG.Clear();

                for (int a = 0; a < NX - 1; a++)
                    for (int b = 0; b < NZ - 1; b++)
                    {
                        // 조각 나누기: 남(구역 아래) · 북(위) · 서 · 동
                        int side;
                        if (b < az.s0) side = 0;
                        else if (b >= az.s1) side = 1;
                        else if (a < ax.s0) side = 2;
                        else if (a >= ax.s1) side = 3;
                        else continue;   // 구역 안
                        if (side != s) continue;

                        int k00 = a * NZ + b, k10 = (a + 1) * NZ + b, k01 = a * NZ + b + 1, k11 = (a + 1) * NZ + b + 1;
                        bool inSpanX = a >= ax.s0 && a + 1 <= ax.s1;
                        bool inSpanZ = b >= az.s0 && b + 1 <= az.s1;

                        if (inSpanX && b + 1 == az.s0)        // 남쪽 이음새: 위쪽 변이 본 지형 j = 0
                            FanCell(V[k00], C[k00], G[k00], V[k10], C[k10], G[k10], ax.main[a], ax.main[a + 1], true, 0);
                        else if (inSpanX && b == az.s1)       // 북쪽 이음새: 아래쪽 변이 j = cz
                            FanCell(V[k01], C[k01], G[k01], V[k11], C[k11], G[k11], ax.main[a], ax.main[a + 1], true, cz);
                        else if (inSpanZ && a + 1 == ax.s0)   // 서쪽 이음새: 오른쪽 변이 i = 0
                            FanCell(V[k00], C[k00], G[k00], V[k01], C[k01], G[k01], az.main[b], az.main[b + 1], false, 0);
                        else if (inSpanZ && a == ax.s1)       // 동쪽 이음새: 왼쪽 변이 i = cx
                            FanCell(V[k10], C[k10], G[k10], V[k11], C[k11], G[k11], az.main[b], az.main[b + 1], false, cx);
                        else
                        {
                            Vector3 p00 = V[k00], p10 = V[k10], p01 = V[k01], p11 = V[k11];
                            if (Mathf.Abs(p00.y - p11.y) <= Mathf.Abs(p10.y - p01.y))
                            {
                                TriUp(p00, p01, p11, C[k00], C[k01], C[k11], G[k00], G[k01], G[k11]);
                                TriUp(p00, p11, p10, C[k00], C[k11], C[k10], G[k00], G[k11], G[k10]);
                            }
                            else
                            {
                                TriUp(p00, p01, p10, C[k00], C[k01], C[k10], G[k00], G[k01], G[k10]);
                                TriUp(p10, p01, p11, C[k10], C[k01], C[k11], G[k10], G[k01], G[k11]);
                            }
                        }
                    }

                var pc = outerPieces[s];
                var m = pc.mesh;
                m.Clear();
                m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(sV);
                m.SetNormals(sN);
                m.SetColors(sC);
                m.SetUVs(0, sU);
                m.SetUVs(2, sG);
                m.SetTriangles(sT, 0);
                m.RecalculateBounds();
                pc.mr.sharedMaterial = material;
                pc.mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

                if (generateCollider && outerCollider)
                {
                    if (pc.mc == null) pc.mc = pc.go.AddComponent<MeshCollider>();
                    pc.mc.sharedMesh = null;
                    pc.mc.sharedMesh = m;
                }
                else if (pc.mc != null) { SafeDestroy(pc.mc); pc.mc = null; }
            }
        }

        /// <summary>
        /// 이음새 칸: 바깥쪽 두 꼭짓점(A → B) 과 본 지형 테두리 정점들(m0 → m1) 을 부채꼴로 잇는다.
        /// alongX = true 면 테두리가 X 방향 (정점 i = m0..m1, j = fixedIdx), 아니면 Z 방향 (i = fixedIdx, j = m0..m1).
        /// A 는 m0 쪽, B 는 m1 쪽 꼭짓점.
        /// </summary>
        void FanCell(Vector3 A, Color cA, float gA, Vector3 B, Color cB, float gB, int m0, int m1, bool alongX, int fixedIdx)
        {
            int mid = (m0 + m1 + 1) / 2;
            for (int m = m0; m < m1; m++)
            {
                int ka = alongX ? Idx(m, fixedIdx) : Idx(fixedIdx, m);
                int kb = alongX ? Idx(m + 1, fixedIdx) : Idx(fixedIdx, m + 1);
                if (m < mid) TriUp(A, P(ka), P(kb), cA, wF[ka], wF[kb], gA, gF[ka], gF[kb]);
                else         TriUp(B, P(ka), P(kb), cB, wF[ka], wF[kb], gB, gF[ka], gF[kb]);
            }
            int km = alongX ? Idx(mid, fixedIdx) : Idx(fixedIdx, mid);
            TriUp(A, B, P(km), cA, cB, wF[km], gA, gB, gF[km]);
        }

        /// <summary>위를 보도록 감아서 삼각형 추가 (넓이 0 은 버림)</summary>
        static void TriUp(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc, float ga, float gb, float gc)
        {
            float y = (b.z - a.z) * (c.x - a.x) - (b.x - a.x) * (c.z - a.z);   // Cross(b-a, c-a).y
            if (Mathf.Abs(y) < 1e-7f) return;
            if (y > 0f) Tri(a, b, c, ca, cb, cc, ga, gb, gc);
            else        Tri(a, c, b, ca, cc, cb, ga, gc, gb);
        }
    }
}
