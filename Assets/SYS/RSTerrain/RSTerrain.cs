// RE:AL STEEL - RS Terrain : 범용 옥토패스풍 지형
//
// 구성
//   RSTerrain (이 컴포넌트)          크기 · 격자 · 바탕 노이즈 · 머티리얼 · 브러시 데이터
//     └ 자식에 요소 컴포넌트들       RSPlateau(둔덕·절벽) RSHeap(더미) RSPit(웅덩이)
//                                    RSPad(작업장·평탄) RSSpline(길·배수로·경사로)
//
// 최종 지형 = 바탕 노이즈 → 요소들을 order 순서대로 적용 → 브러시 데이터(손질) 얹기
//
// 메시는 씬에 저장하지 않는다 (HideFlags.DontSave). 씬을 열거나 플레이할 때 요소 + 브러시 데이터로
// 다시 만든다. 그래서 씬 파일이 가볍고, 요소를 옮기면 언제든 다시 계산된다.
// 정점 하나하나를 손으로 편집하고 싶으면 에디터의 "ProBuilder 로 변환" 을 쓴다.
//
// 셰이더: RE_AL STEEL/Terrain Splat Pixel Lit (정점 컬러 R 길 · G 콘크리트 · B 고물 · A 진흙)
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;

namespace RealSteel.Terrain
{
    [RSSummary("RS 지형 — 요소를 쌓고 브러시로 손질하는 옥토패스풍 지형", "자식에 붙인 요소(둔덕 · 더미 · 웅덩이 · 작업장 · 길/배수로 · 폐자재)를 순서(Order)대로 쌓아 지형을 만들고, 브러시로 손질한 높이 · 칠을 그 위에 덮는다.\n· 요소 추가: 위 버튼 → 씬에서 옮기고, 핸들로 크기 조절\n· 칠 레이어: 바탕(흙/절벽 자동) · R 길 · G 콘크리트 · B 고물 · A 진흙\n· 만들어진 메시(__RST_Generated)는 저장되지 않고 씬을 열 때마다 다시 만들어진다. 설정은 항상 여기서 바꾼다.")]
    [ExecuteAlways, SelectionBase, DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]   // 다른 스크립트의 Start 보다 먼저 지형이 만들어지도록
    [AddComponentMenu("RE_AL STEEL/RS Terrain (지형)")]
    public partial class RSTerrain : MonoBehaviour
    {
        /// <summary>활성화된 지형 목록 (에디터 자동 갱신용)</summary>
        public static readonly List<RSTerrain> All = new List<RSTerrain>();

        // ─────────────────────────────────────────────────────────────
        // 설정
        // ─────────────────────────────────────────────────────────────

        [Header("크기")]
        [RSHelp("격자가 촘촘할수록 디테일이 살고 무거워진다. 40×40 · 칸 0.25 ≈ 삼각형 5만 개, 드로우콜 10 안팎.")]
        [Tooltip("지형 전체 크기 X, Z (m). 이 오브젝트 위치가 가운데")]
        public Vector2 size = new Vector2(40f, 40f);
        [Tooltip("격자 한 칸 (m). 0.25 = 디테일 (권장), 0.5 = 가벼움 (삼각형 1/4)")]
        public float cellSize = 0.25f;
        [Tooltip("메시 조각 크기 (m). 브러시는 닿은 조각만 다시 만든다. 보통 20")]
        public float chunkSize = 20f;
        [Tooltip("지형 가장자리 단면(디오라마 옆면)이 내려가는 깊이 (m, 음수)")]
        public float skirtBottom = -3f;

        [Header("바탕 노이즈")]
        [RSHelp("요소가 없는 곳의 기본 울퉁불퉁함. 굴곡 · 요철 · 자갈 세 겹을 더한다. Amp = 높이(m), Freq = 촘촘함(클수록 잘다).")]
        [Tooltip("무작위 시드. 바꾸면 바탕 요철 · 요소의 불규칙함 · 폐자재 배치가 전부 새로 나온다")]
        public int seed = 1004;
        [Tooltip("기본 지면 높이 (m)")]
        public float baseHeight = 0f;
        [Tooltip("완만한 굴곡 높이 (m). 0.2 ~ 0.3")]
        public float rollAmp = 0.24f;
        [Tooltip("완만한 굴곡 촘촘함. 작을수록 넓은 언덕 (0.1 ~ 0.2)")]
        public float rollFreq = 0.13f;
        [Tooltip("잔 울퉁불퉁 높이 (m). 0.05 ~ 0.1")]
        public float bumpAmp = 0.08f;
        [Tooltip("잔 울퉁불퉁 촘촘함 (0.4 ~ 0.8)")]
        public float bumpFreq = 0.55f;
        [Tooltip("자갈 수준 요철 높이 (m). 0.02 ~ 0.05")]
        public float gritAmp = 0.035f;
        [Tooltip("자갈 요철 촘촘함 (1.5 ~ 2.5)")]
        public float gritFreq = 1.9f;
        [Tooltip("면마다 높이를 살짝 달리해 각진 로우폴리 느낌을 낸다 (m). 0.01 ~ 0.03")]
        public float facetJitter = 0.015f;
        [Range(0f, 0.4f), Tooltip("격자 정점을 옆으로 흔드는 정도 (칸 대비). 0 = 반듯한 격자, 0.3 = 불규칙한 삼각형 (권장)")]
        public float vertexJitter = 0.3f;

        [Header("레이어 경계 (정점 컬러)")]
        [RSHelp("칠 레이어(길 · 콘크리트 등) 경계의 모양. 여기서 크게 휘고, 셰이더가 텍셀 단위 톱니를 한 번 더 넣는다 (머티리얼의 경계 흐트러짐).")]
        [Range(0f, 1f), Tooltip("경계선을 노이즈로 크게 휜다 (0 = 요소 모양 그대로)")]
        public float boundaryWarp = 0.6f;
        [Range(0, 3), Tooltip("경계를 주변 칸과 섞는 폭 (칸). 클수록 톱니가 깊게 파고든다. 1 권장")]
        public int boundaryBlur = 1;

        [Header("렌더")]
        [Tooltip("지형 머티리얼 (RE_AL STEEL/Terrain Splat Pixel Lit). 레이어별 텍스처 · 경계 흐트러짐 · 픽셀 밀도는 머티리얼에서 바꾼다")]
        public Material material;
        [Tooltip("배수로 · 웅덩이 수면 머티리얼. 비우면 수면이 안 보인다")]
        public Material waterMaterial;
        [Tooltip("지면 MeshCollider 를 만든다 (캐릭터 · 물리용). 지면 붙이기 메뉴는 콜라이더 없이도 동작")]
        public bool generateCollider = true;
        [Tooltip("지형이 그림자를 드리운다 (둔덕 · 절벽 그림자)")]
        public bool castShadows = true;

        [Header("외곽 (플레이 구역 밖 배경 지형)")]
        [RSHelp("플레이 구역을 둘러싸는 배경 지형. 가장자리 높이 · 칠을 그대로 이어받고, 바깥으로 갈수록 솟아올라 지형의 끝을 숨긴다 (분지 모양). 격자가 성겨서 넓어도 가볍다. 켜면 디오라마 옆면(단면)은 안 만든다.")]
        [Tooltip("외곽 지형을 만든다")]
        public bool outerEnabled = false;
        [Tooltip("가장자리에서 바깥으로 뻗는 폭 (m). 카메라에 보이는 만큼 — 보통 25 ~ 40")]
        public float outerWidth = 30f;
        [Tooltip("외곽 격자 한 칸 (m). 1 권장 — 멀어서 성겨도 티가 안 난다")]
        public float outerCell = 1f;
        [Tooltip("가장자리에서 평평하게 이어지는 폭 (m). 이 뒤부터 솟는다")]
        public float outerFlat = 3f;
        [Tooltip("가장 바깥 높이 (m). 8 ~ 12 면 둘러싼 언덕 · 고철 산")]
        public float outerRise = 9f;
        [Range(0.5f, 4f), Tooltip("솟는 모양. 1 = 일정한 경사, 2 안팎 = 처음엔 완만하다 바깥에서 가파르게 (분지)")]
        public float outerRiseCurve = 1.8f;
        [Range(0f, 1f), Tooltip("둘레를 따라 높낮이가 들쭉날쭉한 정도. 0 = 스카이라인이 평평")]
        public float outerSkylineVar = 0.45f;
        [Tooltip("큰 굴곡 높이 (m). 바깥으로 갈수록 커진다")]
        public float outerNoise = 1.5f;
        [Tooltip("계단식 단 높이 (m). 0 = 끔. 1.5 ~ 2.5 면 층층이 깎인 절벽 (옥토패스풍). 가파른 면은 셰이더가 절벽 텍스처로 칠한다")]
        public float outerTerrace = 2f;
        [Range(0f, 1f), Tooltip("단 모서리 날카로움. 0 = 계단 없이 매끈 / 1 = 거의 수직 절벽")]
        public float outerTerraceSharp = 0.6f;
        [Range(0f, 1f), Tooltip("고물(B) 레이어로 덮는 정도 — 폐철 산더미 느낌")]
        public float outerJunk = 0.35f;
        [Tooltip("외곽에도 콜라이더를 만든다 (가장자리 밖으로 나가도 받쳐 준다)")]
        public bool outerCollider = true;

        [Header("브러시 데이터 (손으로 깎고 칠한 것)")]
        [RSHelp("브러시로 손질한 높이 · 칠은 이 에셋에 따로 저장된다. 요소를 바꿔도 유지되고, 크기 · 격자를 바꾸면 위치 기준으로 옮겨진다.")]
        [Tooltip("브러시 데이터 에셋 (RSTerrainData). 없으면 브러시를 켤 때 만들 수 있다")]
        public RSTerrainData data;

        [Header("편집")]
        [Tooltip("요소를 옮기거나 값을 바꾸면 자동으로 다시 만든다")]
        public bool autoRebuild = true;
        [Tooltip("드래그하는 동안에도 계속 다시 만든다 (무거움). 끄면 마우스를 놓을 때 한 번")]
        public bool rebuildWhileDragging = false;

        // ─────────────────────────────────────────────────────────────
        // 상태
        // ─────────────────────────────────────────────────────────────

        public enum BrushOp { Raise, Lower, Flatten, Smooth, ResetHeight, Paint, ErasePaint }

        [System.NonSerialized] public bool dirty = true;
        [System.NonSerialized] public float dirtyTime;

        [System.NonSerialized] int cx, cz;         // 칸 수
        [System.NonSerialized] int vx, vz;         // 정점 수
        [System.NonSerialized] float csx, csz;     // 실제 칸 크기
        [System.NonSerialized] float[] h0, hF;     // 절차 높이 / 최종 높이
        [System.NonSerialized] Vector4[] w0;       // 절차 레이어
        [System.NonSerialized] Color[] wF;         // 최종 레이어 (정점 컬러)
        [System.NonSerialized] float[] g0, gF;     // 풀 레이어 (절차 / 최종) — 메시 UV2.x
        [System.NonSerialized] float[] px, pz;     // 흔든 정점 XZ
        [System.NonSerialized] float minH, maxH;

        [System.NonSerialized] List<RSTerrainFeature> feats = new List<RSTerrainFeature>();
        [System.NonSerialized] Transform genRoot;
        [System.NonSerialized] List<Chunk> chunks = new List<Chunk>();
        [System.NonSerialized] GameObject waterGo;
        [System.NonSerialized] Mesh waterMesh;
        [System.NonSerialized] List<GameObject> debrisGos = new List<GameObject>();

        class Chunk
        {
            public int i0, i1, j0, j1;
            public GameObject go;
            public Mesh mesh;
            public MeshFilter mf;
            public MeshRenderer mr;
            public MeshCollider mc;
        }

        const string GenName = "__RST_Generated (자동 생성 · 저장 안 됨)";

        public bool HasMesh { get { return chunks != null && chunks.Count > 0 && chunks[0].go != null; } }
        public bool HasHeights { get { return hF != null; } }
        public int VertexCountX { get { return vx; } }
        public int VertexCountZ { get { return vz; } }
        public float CellX { get { return csx; } }
        public float CellZ { get { return csz; } }

        // ─────────────────────────────────────────────────────────────
        // 수명
        // ─────────────────────────────────────────────────────────────

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            MarkDirty();
            // 플레이 모드에서는 여기서 바로 만들지 않는다 — 씬을 불러오는 중엔 자식 요소가 아직 안 켜졌을 수 있다.
            // 모든 OnEnable 이 끝난 뒤 Start 에서 한 번 만든다.
        }

        void Start()
        {
            if (Application.isPlaying && dirty) Rebuild();
        }

        // 플레이 모드: 요소가 켜지거나 바뀌면(dirty) 프레임 끝에 다시 만든다.
        // (에디터에서는 RSTerrainAutoRebuild 가 맡는다)
        void LateUpdate()
        {
            if (Application.isPlaying && dirty) Rebuild();
        }

        /// <summary>
        /// 요소가 켜져 있는지. isActiveAndEnabled 는 씬을 불러오는 중(플레이 시작 등)
        /// 아직 OnEnable 이 안 불린 요소를 false 로 돌려줘서, 뒤쪽 요소가 빠진 채 지형이 만들어졌다.
        /// </summary>
        static bool IsLive(Behaviour f)
        {
            return f != null && f.enabled && f.gameObject.activeInHierarchy;
        }

        void OnDisable()
        {
            All.Remove(this);
            // 오브젝트가 꺼지는 중에는 자식을 즉시 지울 수 없다 (유니티 제약).
            // 그땐 참조만 놓고, 다음에 만들 때 남은 것을 정리한다 (EnsureGenRoot).
            if (Application.isPlaying || gameObject.activeInHierarchy) DestroyGenerated();
            else ForgetGenerated();
        }

        void ForgetGenerated()
        {
            if (chunks != null) chunks.Clear();
            outerPieces.Clear();
            genRoot = null; waterGo = null; waterMesh = null;
            debrisGos.Clear(); foliageGos.Clear();
        }

        void OnValidate()
        {
            size = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            cellSize = Mathf.Clamp(cellSize, 0.1f, 4f);
            chunkSize = Mathf.Max(4f, chunkSize);
            MarkDirty();
        }

        public void MarkDirty()
        {
            dirty = true;
            dirtyTime = Time.realtimeSinceStartup;
        }

        /// <summary>요소 트랜스폼이 바뀌었는지 (에디터 자동 갱신용). 확인하면서 플래그를 지운다.</summary>
        public bool ConsumeTransformChanges()
        {
            bool changed = false;
            foreach (var f in GetComponentsInChildren<RSTerrainFeature>(false))
            {
                if (f.transform.hasChanged) { changed = true; f.transform.hasChanged = false; }
            }
            return changed;
        }

        // ─────────────────────────────────────────────────────────────
        // 평가
        // ─────────────────────────────────────────────────────────────

        /// <summary>지형 로컬 XZ 에서 절차 지형 (브러시 제외). Rebuild 중에만 유효 — 요소가 준비돼 있어야 한다.</summary>
        public RSSample Evaluate(float x, float z)
        {
            var s = new RSSample();
            float roll = RSMath.SNoise(x, z, rollFreq, 0, seed) * rollAmp;
            float bump = RSMath.SNoise(x, z, bumpFreq, 1, seed) * bumpAmp;
            float grit = RSMath.SNoise(x, z, gritFreq, 2, seed) * gritAmp;
            s.h = baseHeight + roll + bump + grit;
            s.detail = bump + grit;

            var p = new Vector2(x, z);
            for (int i = 0; i < feats.Count; i++)
            {
                var f = feats[i];
                if (f.bounds.Contains(p)) f.Apply(ref s, x, z);
            }
            return s;
        }

        void PrepareFeatures()
        {
            feats.Clear();
            var list = GetComponentsInChildren<RSTerrainFeature>(false)
                       .Where(f => IsLive(f))
                       .OrderBy(f => f.order)   // 안정 정렬 → 같은 order 는 하이어라키 순서
                       .ToList();
            foreach (var f in list) { f.Prepare(this); feats.Add(f); }
        }

        int Idx(int i, int j) { return i * vz + j; }

        // ─────────────────────────────────────────────────────────────
        // 생성
        // ─────────────────────────────────────────────────────────────

        public void Rebuild()
        {
            ComputeProcedural();
            SyncData();
            Compose(0, vx - 1, 0, vz - 1);
            BuildChunks();
            BuildOuter();
            BuildWater();
            BuildScatter();
            BuildFoliage();
            dirty = false;
        }

        void ComputeProcedural()
        {
            PrepareFeatures();

            float cell = Mathf.Max(0.1f, cellSize);
            cx = Mathf.Max(1, Mathf.RoundToInt(size.x / cell));
            cz = Mathf.Max(1, Mathf.RoundToInt(size.y / cell));
            vx = cx + 1; vz = cz + 1;
            csx = size.x / cx; csz = size.y / cz;

            int n = vx * vz;
            h0 = new float[n]; hF = new float[n];
            w0 = new Vector4[n]; wF = new Color[n];
            g0 = new float[n]; gF = new float[n];
            px = new float[n]; pz = new float[n];

            float amp = vertexJitter * Mathf.Min(csx, csz);

            for (int i = 0; i < vx; i++)
                for (int j = 0; j < vz; j++)
                {
                    float x0 = -size.x * 0.5f + i * csx, z0 = -size.y * 0.5f + j * csz;
                    var s0 = Evaluate(x0, z0);

                    // 정점 XZ 흔들기. 테두리 정점은 테두리를 따라서만, 고정 영역은 안 흔든다.
                    float x = x0, z = z0;
                    float free = 1f - Mathf.Clamp01(s0.lockXZ);
                    if (amp > 0f && free > 0.001f)
                    {
                        if (i > 0 && i < cx) x += (RSMath.Hash01(i, j, 51, seed) - 0.5f) * 2f * amp * free;
                        if (j > 0 && j < cz) z += (RSMath.Hash01(i, j, 52, seed) - 0.5f) * 2f * amp * free;
                    }
                    var s = (x != x0 || z != z0) ? Evaluate(x, z) : s0;
                    float lockv = Mathf.Clamp01(Mathf.Max(s0.lockXZ, s.lockXZ));

                    float h = s.h + (RSMath.Hash01(i, j, 21, seed) - 0.5f) * 2f * facetJitter * (1f - lockv);

                    // 레이어 경계 휘기 — 마스크를 옆으로 밀린 위치에서 읽는다
                    Vector4 w = s.w;
                    float g = s.grass;
                    if (boundaryWarp > 0.0001f && lockv < 0.999f)
                    {
                        float a = boundaryWarp;
                        float wx = RSMath.SNoise(x, z, 0.7f, 19, seed) * 0.6f * a + RSMath.SNoise(x, z, 2.3f, 21, seed) * 0.2f * a;
                        float wz = RSMath.SNoise(x, z, 0.7f, 20, seed) * 0.6f * a + RSMath.SNoise(x, z, 2.3f, 22, seed) * 0.2f * a;
                        var sw = Evaluate(x + wx, z + wz);
                        w = Vector4.Lerp(sw.w, s.w, lockv);
                        g = Mathf.Lerp(sw.grass, s.grass, lockv);
                    }

                    int k = Idx(i, j);
                    px[k] = x; pz[k] = z;
                    h0[k] = h;
                    w0[k] = w;
                    g0[k] = g;
                }

            if (boundaryBlur > 0) BlurWeights(boundaryBlur);
        }

        void BlurWeights(int r)
        {
            var tmp = new Vector4[w0.Length];
            var tg = new float[g0.Length];
            for (int i = 0; i < vx; i++)
                for (int j = 0; j < vz; j++)
                {
                    Vector4 acc = Vector4.zero; float ag = 0f; int n = 0;
                    for (int k = -r; k <= r; k++) { int ii = i + k; if (ii < 0 || ii >= vx) continue; acc += w0[Idx(ii, j)]; ag += g0[Idx(ii, j)]; n++; }
                    tmp[Idx(i, j)] = acc / n; tg[Idx(i, j)] = ag / n;
                }
            for (int i = 0; i < vx; i++)
                for (int j = 0; j < vz; j++)
                {
                    Vector4 acc = Vector4.zero; float ag = 0f; int n = 0;
                    for (int k = -r; k <= r; k++) { int jj = j + k; if (jj < 0 || jj >= vz) continue; acc += tmp[Idx(i, jj)]; ag += tg[Idx(i, jj)]; n++; }
                    w0[Idx(i, j)] = acc / n; g0[Idx(i, j)] = ag / n;
                }
        }

        /// <summary>브러시 데이터를 현재 격자에 맞춘다 (크기·격자가 바뀌었으면 위치 기준으로 옮겨 담음).</summary>
        public void SyncData()
        {
            if (data == null || vx == 0) return;
            if (!data.Matches(vx, vz, size)) data.Resize(vx, vz, size);
        }

        /// <summary>절차 결과 + 브러시 데이터 → 최종 높이·색 (범위만)</summary>
        void Compose(int i0, int i1, int j0, int j1)
        {
            bool hasData = data != null && data.Matches(vx, vz, size);
            i0 = Mathf.Max(0, i0); j0 = Mathf.Max(0, j0);
            i1 = Mathf.Min(vx - 1, i1); j1 = Mathf.Min(vz - 1, j1);

            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    int k = Idx(i, j);
                    float h = h0[k];
                    Vector4 w = w0[k];
                    float g = g0[k];
                    if (hasData)
                    {
                        h += data.height[k];
                        float a = Mathf.Clamp01(data.amount[k]);
                        if (a > 0f)
                        {
                            Color c = data.paint[k];
                            w = Vector4.Lerp(w, new Vector4(c.r, c.g, c.b, c.a), a);
                            g = Mathf.Lerp(g, data.grass[k], a);
                        }
                    }
                    w = new Vector4(Mathf.Clamp01(w.x), Mathf.Clamp01(w.y), Mathf.Clamp01(w.z), Mathf.Clamp01(w.w));
                    g = Mathf.Clamp01(g);
                    float sum = w.x + w.y + w.z + w.w + g;
                    if (sum > 1f) { w /= sum; g /= sum; }
                    hF[k] = h;
                    wF[k] = new Color(w.x, w.y, w.z, w.w);
                    gF[k] = g;
                }

            minH = float.MaxValue; maxH = float.MinValue;
            for (int k = 0; k < hF.Length; k++) { if (hF[k] < minH) minH = hF[k]; if (hF[k] > maxH) maxH = hF[k]; }
        }

        // ─────────────────────────────────────────────────────────────
        // 메시
        // ─────────────────────────────────────────────────────────────

        Transform EnsureGenRoot()
        {
            if (genRoot != null) return genRoot;

            // 이전에 남은 것 (스크립트 리로드 · 비활성화 뒤) 은 메시까지 정리하고 새로 만든다
            var stale = transform.Find(GenName);
            if (stale != null)
            {
                foreach (var mf in stale.GetComponentsInChildren<MeshFilter>(true)) SafeDestroy(mf.sharedMesh);
                SafeDestroy(stale.gameObject);
            }
            chunks.Clear(); outerPieces.Clear(); waterGo = null; waterMesh = null; debrisGos.Clear(); foliageGos.Clear();

            var go = new GameObject(GenName);
            go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            go.transform.SetParent(transform, false);
            genRoot = go.transform;
            return genRoot;
        }

        void DestroyGenerated()
        {
            if (chunks != null)
            {
                foreach (var c in chunks) { SafeDestroy(c.mesh); SafeDestroy(c.go); }
                chunks.Clear();
            }
            DestroyOuter();
            SafeDestroy(waterMesh); waterMesh = null;
            SafeDestroy(waterGo); waterGo = null;
            ClearDebris();
            ClearFoliage();
            if (genRoot != null) SafeDestroy(genRoot.gameObject);
            else
            {
                var t = transform.Find(GenName);
                if (t != null) SafeDestroy(t.gameObject);
            }
            genRoot = null;
        }

        static void SafeDestroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void BuildChunks()
        {
            var root = EnsureGenRoot();
            int ncx = Mathf.Max(1, Mathf.CeilToInt(size.x / chunkSize));
            int ncz = Mathf.Max(1, Mathf.CeilToInt(size.y / chunkSize));
            int perX = Mathf.CeilToInt(cx / (float)ncx), perZ = Mathf.CeilToInt(cz / (float)ncz);

            // 조각 수가 바뀌면 새로 만든다
            if (chunks.Count != ncx * ncz || chunks.Any(c => c.go == null))
            {
                foreach (var c in chunks) { SafeDestroy(c.mesh); SafeDestroy(c.go); }
                chunks.Clear();
                for (int a = 0; a < ncx; a++)
                    for (int b = 0; b < ncz; b++)
                    {
                        var c = new Chunk();
                        c.go = new GameObject("Chunk_" + a + "_" + b);
                        c.go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                        c.go.transform.SetParent(root, false);
                        c.mf = c.go.AddComponent<MeshFilter>();
                        c.mr = c.go.AddComponent<MeshRenderer>();
                        c.mesh = new Mesh { name = "RST_Chunk", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
                        c.mf.sharedMesh = c.mesh;
                        chunks.Add(c);
                    }
            }

            int idx = 0;
            for (int a = 0; a < ncx; a++)
                for (int b = 0; b < ncz; b++)
                {
                    var c = chunks[idx++];
                    c.i0 = Mathf.Min(cx, a * perX); c.i1 = Mathf.Min(cx, (a + 1) * perX);
                    c.j0 = Mathf.Min(cz, b * perZ); c.j1 = Mathf.Min(cz, (b + 1) * perZ);
                    BuildChunkMesh(c);
                }
        }

        static readonly List<Vector3> sV = new List<Vector3>();
        static readonly List<Vector3> sN = new List<Vector3>();
        static readonly List<Color> sC = new List<Color>();
        static readonly List<Vector2> sU = new List<Vector2>();
        static readonly List<int> sT = new List<int>();
        static readonly List<Vector2> sG = new List<Vector2>();   // UV2: x = 풀 레이어

        Vector3 P(int k) { return new Vector3(px[k], hF[k], pz[k]); }

        void BuildChunkMesh(Chunk c)
        {
            sV.Clear(); sN.Clear(); sC.Clear(); sU.Clear(); sT.Clear(); sG.Clear();

            for (int i = c.i0; i < c.i1; i++)
                for (int j = c.j0; j < c.j1; j++)
                {
                    int k00 = Idx(i, j), k10 = Idx(i + 1, j), k11 = Idx(i + 1, j + 1), k01 = Idx(i, j + 1);
                    Vector3 a = P(k00), b = P(k10), cc = P(k11), d = P(k01);

                    // 대각선은 높이차가 작은 쪽으로 — 능선·골이 지형을 따라 접힌다
                    if (Mathf.Abs(a.y - cc.y) <= Mathf.Abs(b.y - d.y))
                    {
                        Tri(a, d, cc, wF[k00], wF[k01], wF[k11], gF[k00], gF[k01], gF[k11]);
                        Tri(a, cc, b, wF[k00], wF[k11], wF[k10], gF[k00], gF[k11], gF[k10]);
                    }
                    else
                    {
                        Tri(a, d, b, wF[k00], wF[k01], wF[k10], gF[k00], gF[k01], gF[k10]);
                        Tri(b, d, cc, wF[k10], wF[k01], wF[k11], gF[k10], gF[k01], gF[k11]);
                    }
                }

            // 스커트 (디오라마 단면) — 바깥 테두리에 닿는 조각만. 외곽 지형이 있으면 필요 없다.
            if (!outerEnabled)
            {
                if (c.j0 == 0)  for (int i = c.i0; i < c.i1; i++) Skirt(P(Idx(i, 0)),  P(Idx(i + 1, 0)),  Vector3.back);
                if (c.j1 == cz) for (int i = c.i0; i < c.i1; i++) Skirt(P(Idx(i, cz)), P(Idx(i + 1, cz)), Vector3.forward);
                if (c.i0 == 0)  for (int j = c.j0; j < c.j1; j++) Skirt(P(Idx(0, j)),  P(Idx(0, j + 1)),  Vector3.left);
                if (c.i1 == cx) for (int j = c.j0; j < c.j1; j++) Skirt(P(Idx(cx, j)), P(Idx(cx, j + 1)), Vector3.right);
            }

            var m = c.mesh;
            m.Clear();
            m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(sV);
            m.SetNormals(sN);
            m.SetColors(sC);
            m.SetUVs(0, sU);
            m.SetUVs(2, sG);
            m.SetTriangles(sT, 0);
            m.RecalculateBounds();

            c.mr.sharedMaterial = material;
            c.mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            if (generateCollider)
            {
                if (c.mc == null) c.mc = c.go.AddComponent<MeshCollider>();
                c.mc.sharedMesh = null;
                c.mc.sharedMesh = m;
            }
            else if (c.mc != null) { SafeDestroy(c.mc); c.mc = null; }
        }

        static void Tri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
        {
            Tri(a, b, c, ca, cb, cc, 0f, 0f, 0f);
        }

        static void Tri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc, float ga, float gb, float gc)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int i0 = sV.Count;
            sV.Add(a); sV.Add(b); sV.Add(c);
            sN.Add(n); sN.Add(n); sN.Add(n);
            sC.Add(ca); sC.Add(cb); sC.Add(cc);
            sU.Add(new Vector2(a.x, a.z) * 0.5f); sU.Add(new Vector2(b.x, b.z) * 0.5f); sU.Add(new Vector2(c.x, c.z) * 0.5f);
            sT.Add(i0); sT.Add(i0 + 1); sT.Add(i0 + 2);
            sG.Add(new Vector2(ga, 0f)); sG.Add(new Vector2(gb, 0f)); sG.Add(new Vector2(gc, 0f));
        }

        void Skirt(Vector3 p0, Vector3 p1, Vector3 outward)
        {
            var q0 = new Vector3(p0.x, skirtBottom, p0.z);
            var q1 = new Vector3(p1.x, skirtBottom, p1.z);
            // 정점 컬러 0 → 셰이더에서 절벽 텍스처
            Vector3 n = Vector3.Cross(p1 - p0, q1 - p0);
            if (Vector3.Dot(n, outward) >= 0f)
            {
                Tri(p0, p1, q1, Color.clear, Color.clear, Color.clear);
                Tri(p0, q1, q0, Color.clear, Color.clear, Color.clear);
            }
            else
            {
                Tri(p0, q1, p1, Color.clear, Color.clear, Color.clear);
                Tri(p0, q0, q1, Color.clear, Color.clear, Color.clear);
            }
        }

        void BuildWater()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            foreach (var f in feats) f.AddWater(this, v, t);

            if (v.Count == 0)
            {
                SafeDestroy(waterMesh); waterMesh = null;
                SafeDestroy(waterGo); waterGo = null;
                return;
            }

            if (waterGo == null)
            {
                waterGo = new GameObject("Water");
                waterGo.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                waterGo.transform.SetParent(EnsureGenRoot(), false);
                waterGo.AddComponent<MeshFilter>();
                var r = waterGo.AddComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            if (waterMesh == null) waterMesh = new Mesh { name = "RST_Water", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };

            // 수면 정리: 굽이가 급하면 사각형이 꼬여(나비 모양) 한쪽 삼각형이 아래를 본다.
            // 그러면 RecalculateNormals 가 위(+) 아래(-) 를 더해 법선이 0 이 되고,
            // 셰이더에서 0 을 정규화 → NaN 픽셀 → Bloom 이 하얗게 크게 번진다 (라이트를 꺼도 빛남).
            // → 삼각형마다 위를 보게 감고, 넓이 0 인 것은 버리고, 법선은 전부 위로 고정.
            var tris = new List<int>(t.Count);
            for (int i = 0; i + 2 < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                float cy = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x); // (b-a)x(c-a) 의 y 부호 반대
                if (Mathf.Abs(cy) < 1e-5f || float.IsNaN(cy)) continue;
                if (cy < 0f) { tris.Add(t[i]); tris.Add(t[i + 1]); tris.Add(t[i + 2]); }
                else         { tris.Add(t[i]); tris.Add(t[i + 2]); tris.Add(t[i + 1]); }
            }

            var uv = new List<Vector2>(v.Count);
            var nrm = new List<Vector3>(v.Count);
            foreach (var p in v) { uv.Add(new Vector2(p.x, p.z) * 0.5f); nrm.Add(Vector3.up); }

            waterMesh.Clear();
            waterMesh.SetVertices(v);
            waterMesh.SetNormals(nrm);
            waterMesh.SetUVs(0, uv);
            waterMesh.SetTriangles(tris, 0);
            waterMesh.RecalculateBounds();
            waterGo.GetComponent<MeshFilter>().sharedMesh = waterMesh;
            waterGo.GetComponent<MeshRenderer>().sharedMaterial = waterMaterial;
        }

        // ─────────────────────────────────────────────────────────────
        // 폐자재 (RSScatter) — 머티리얼별로 한 메시
        // ─────────────────────────────────────────────────────────────

        void ClearDebris()
        {
            foreach (var go in debrisGos)
            {
                if (go == null) continue;
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null) SafeDestroy(mf.sharedMesh);
                SafeDestroy(go);
            }
            debrisGos.Clear();
        }

        /// <summary>폐자재만 다시 놓는다 (브러시로 지면을 고친 뒤 등)</summary>
        public void RebuildScatter()
        {
            if (hF == null) return;
            BuildScatter();
            BuildFoliage();
        }

        void BuildScatter()
        {
            ClearDebris();
            var sink = new RSDebrisSink();
            bool any = false;
            foreach (var f in GetComponentsInChildren<RSScatter>(false))
            {
                if (!IsLive(f)) continue;
                f.Scatter(this, sink);
                any = true;
            }
            if (!any) return;

            var root = EnsureGenRoot();
            foreach (var kv in sink.buffers) MakeDebris(root, kv.Key, kv.Value);
            MakeDebris(root, null, sink.NullBuffer);
        }

        void MakeDebris(Transform root, Material mat, RSDebrisSink.Buffer b)
        {
            if (b.v.Count == 0) return;
            var go = new GameObject("Debris_" + (mat != null ? mat.name : "NoMaterial"));
            go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            go.transform.SetParent(root, false);
            var mesh = new Mesh { name = "RST_Debris", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(b.v);
            mesh.SetNormals(b.n);
            mesh.SetUVs(0, b.uv);
            mesh.SetTriangles(b.t, 0);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (b.collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            debrisGos.Add(go);
        }

        /// <summary>수면용: 위를 보는 사각형 하나 추가 (감기 방향 자동)</summary>
        public static void AddQuadUp(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i0 = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            bool up = Vector3.Cross(b - a, c - a).y >= 0f;
            if (up) { t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2); t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 3); }
            else    { t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 1); t.Add(i0); t.Add(i0 + 3); t.Add(i0 + 2); }
        }

        // ─────────────────────────────────────────────────────────────
        // 조회
        // ─────────────────────────────────────────────────────────────

        public bool InsideXZ(float x, float z)
        {
            return x >= -size.x * 0.5f && x <= size.x * 0.5f && z >= -size.y * 0.5f && z <= size.y * 0.5f;
        }

        /// <summary>최종 높이 (지형 로컬). 격자 쌍선형 보간.</summary>
        public float SampleHeight(float x, float z)
        {
            if (hF == null) return baseHeight;
            float fi = Mathf.Clamp((x + size.x * 0.5f) / csx, 0f, cx);
            float fj = Mathf.Clamp((z + size.y * 0.5f) / csz, 0f, cz);
            int i0 = Mathf.Min(cx - 1, Mathf.FloorToInt(fi)), j0 = Mathf.Min(cz - 1, Mathf.FloorToInt(fj));
            float u = fi - i0, w = fj - j0;
            float a = hF[Idx(i0, j0)], b = hF[Idx(i0 + 1, j0)], c = hF[Idx(i0, j0 + 1)], d = hF[Idx(i0 + 1, j0 + 1)];
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), w);
        }

        /// <summary>최종 레이어 가중치 R G B A (지형 로컬). 격자 쌍선형 보간.</summary>
        public Vector4 SampleWeights(float x, float z)
        {
            if (wF == null) return Vector4.zero;
            float fi = Mathf.Clamp((x + size.x * 0.5f) / csx, 0f, cx);
            float fj = Mathf.Clamp((z + size.y * 0.5f) / csz, 0f, cz);
            int i0 = Mathf.Min(cx - 1, Mathf.FloorToInt(fi)), j0 = Mathf.Min(cz - 1, Mathf.FloorToInt(fj));
            float u = fi - i0, w = fj - j0;
            Color c = Color.Lerp(Color.Lerp(wF[Idx(i0, j0)], wF[Idx(i0 + 1, j0)], u),
                                 Color.Lerp(wF[Idx(i0, j0 + 1)], wF[Idx(i0 + 1, j0 + 1)], u), w);
            return new Vector4(c.r, c.g, c.b, c.a);
        }

        /// <summary>최종 풀 레이어 가중치 (지형 로컬). 격자 쌍선형 보간.</summary>
        public float SampleGrass(float x, float z)
        {
            if (gF == null) return 0f;
            float fi = Mathf.Clamp((x + size.x * 0.5f) / csx, 0f, cx);
            float fj = Mathf.Clamp((z + size.y * 0.5f) / csz, 0f, cz);
            int i0 = Mathf.Min(cx - 1, Mathf.FloorToInt(fi)), j0 = Mathf.Min(cz - 1, Mathf.FloorToInt(fj));
            float u = fi - i0, w = fj - j0;
            return Mathf.Lerp(Mathf.Lerp(gF[Idx(i0, j0)], gF[Idx(i0 + 1, j0)], u),
                              Mathf.Lerp(gF[Idx(i0, j0 + 1)], gF[Idx(i0 + 1, j0 + 1)], u), w);
        }

        /// <summary>최종 지면 법선 (지형 로컬)</summary>
        public Vector3 SampleNormal(float x, float z)
        {
            const float e = 0.2f;
            float l = SampleHeight(x - e, z), r = SampleHeight(x + e, z);
            float d = SampleHeight(x, z - e), u = SampleHeight(x, z + e);
            return new Vector3(l - r, 2f * e, d - u).normalized;
        }

        /// <summary>월드 좌표 → 지면 높이 (월드 Y)</summary>
        public float SampleHeightWorld(Vector3 world)
        {
            var l = transform.InverseTransformPoint(world);
            return transform.TransformPoint(new Vector3(l.x, SampleHeight(l.x, l.z), l.z)).y;
        }

        /// <summary>월드 광선과 지면의 교점 (높이 격자를 따라 걸어간다 — 콜라이더 없어도 됨)</summary>
        public bool Raycast(Ray worldRay, out Vector3 hitWorld)
        {
            hitWorld = Vector3.zero;
            if (hF == null) return false;

            Vector3 o = transform.InverseTransformPoint(worldRay.origin);
            Vector3 dir = transform.InverseTransformDirection(worldRay.direction).normalized;
            var lr = new Ray(o, dir);

            var box = new Bounds(new Vector3(0f, (minH + maxH) * 0.5f, 0f),
                                 new Vector3(size.x, Mathf.Max(0.1f, maxH - minH) + 2f, size.y));
            float t0;
            if (box.Contains(o)) t0 = 0f;
            else if (!box.IntersectRay(lr, out t0)) return false;
            t0 = Mathf.Max(0f, t0);

            float step = Mathf.Min(csx, csz) * 0.5f;
            float tMax = t0 + (size.magnitude + (maxH - minH) + 4f) * 1.5f;
            float prev = t0;
            bool entered = false;
            for (float t = t0; t < tMax; t += step)
            {
                Vector3 p = lr.GetPoint(t);
                if (!InsideXZ(p.x, p.z)) { if (entered) break; prev = t; continue; }
                entered = true;
                if (p.y <= SampleHeight(p.x, p.z))
                {
                    float lo = prev, hi = t;
                    for (int it = 0; it < 12; it++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        Vector3 q = lr.GetPoint(mid);
                        if (q.y <= SampleHeight(q.x, q.z)) hi = mid; else lo = mid;
                    }
                    Vector3 hit = lr.GetPoint(hi);
                    hitWorld = transform.TransformPoint(hit);
                    return true;
                }
                prev = t;
            }
            return false;
        }

        /// <summary>정점 (i, j) 최종 위치·색 (ProBuilder 변환용)</summary>
        public Vector3 GetVertex(int i, int j) { return P(Idx(i, j)); }
        public Color GetVertexColor(int i, int j) { return wF[Idx(i, j)]; }
        public float GetVertexGrass(int i, int j) { return gF[Idx(i, j)]; }

        // ─────────────────────────────────────────────────────────────
        // 브러시
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 브러시 한 번 찍기. center = 지형 로컬. 데이터 에셋이 있어야 한다.
        /// 닿은 범위만 다시 합치고, 닿은 조각 메시만 다시 만든다.
        /// </summary>
        public void ApplyBrush(BrushOp op, Vector3 center, float radius, float strength, float hardness, RSLayer layer, float flattenY)
        {
            if (data == null || hF == null) return;
            SyncData();
            radius = Mathf.Max(0.05f, radius);

            int i0 = Mathf.Max(0, Mathf.FloorToInt((center.x - radius + size.x * 0.5f) / csx));
            int i1 = Mathf.Min(vx - 1, Mathf.CeilToInt((center.x + radius + size.x * 0.5f) / csx));
            int j0 = Mathf.Max(0, Mathf.FloorToInt((center.z - radius + size.y * 0.5f) / csz));
            int j1 = Mathf.Min(vz - 1, Mathf.CeilToInt((center.z + radius + size.y * 0.5f) / csz));
            if (i0 > i1 || j0 > j1) return;

            float[] snap = op == BrushOp.Smooth ? (float[])hF.Clone() : null;
            Color target = RSMath.LayerColor(layer == RSLayer.None ? RSLayer.Base : layer);
            float targetGrass = RSMath.LayerGrass(layer);

            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    int k = Idx(i, j);
                    float d = new Vector2(px[k] - center.x, pz[k] - center.z).magnitude;
                    float f = RSMath.Falloff(d / radius, hardness) * strength;
                    if (f <= 0f) continue;

                    switch (op)
                    {
                        case BrushOp.Raise:       data.height[k] += f * 0.12f; break;
                        case BrushOp.Lower:       data.height[k] -= f * 0.12f; break;
                        case BrushOp.Flatten:     data.height[k] = Mathf.Lerp(data.height[k], flattenY - h0[k], f); break;
                        case BrushOp.ResetHeight: data.height[k] *= 1f - f; break;
                        case BrushOp.Smooth:
                        {
                            float sum = 0f; int n = 0;
                            if (i > 0)      { sum += snap[Idx(i - 1, j)]; n++; }
                            if (i < vx - 1) { sum += snap[Idx(i + 1, j)]; n++; }
                            if (j > 0)      { sum += snap[Idx(i, j - 1)]; n++; }
                            if (j < vz - 1) { sum += snap[Idx(i, j + 1)]; n++; }
                            if (n > 0) data.height[k] += (sum / n - snap[k]) * f;
                            break;
                        }
                        case BrushOp.Paint:
                        {
                            float a = data.amount[k];
                            data.paint[k] = a <= 0.001f ? target : Color.Lerp(data.paint[k], target, Mathf.Clamp01(f * 1.5f));
                            data.grass[k] = a <= 0.001f ? targetGrass : Mathf.Lerp(data.grass[k], targetGrass, Mathf.Clamp01(f * 1.5f));
                            data.amount[k] = Mathf.Min(1f, a + f);
                            break;
                        }
                        case BrushOp.ErasePaint:  data.amount[k] *= 1f - f; break;
                    }
                }

            Compose(i0, i1, j0, j1);
            RebuildChunksTouching(i0, i1, j0, j1);
        }

        /// <summary>브러시 데이터를 바꾼 뒤(되돌리기 등) 전체를 다시 합친다. 절차 계산은 다시 안 함.</summary>
        public void RecomposeAll()
        {
            if (hF == null) { Rebuild(); return; }
            SyncData();
            Compose(0, vx - 1, 0, vz - 1);
            foreach (var c in chunks) if (c.go != null) BuildChunkMesh(c);
            BuildOuter();
            BuildWater();
            BuildScatter();
            BuildFoliage();
        }

        void RebuildChunksTouching(int i0, int i1, int j0, int j1)
        {
            // 가장자리를 손질했으면 외곽도 이어 붙인다
            if (outerEnabled && (i0 <= 0 || j0 <= 0 || i1 >= vx - 1 || j1 >= vz - 1)) BuildOuter();
            foreach (var c in chunks)
            {
                if (c.go == null) continue;
                if (i1 < c.i0 || i0 > c.i1 || j1 < c.j0 || j0 > c.j1) continue;
                BuildChunkMesh(c);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawWireCube(new Vector3(0f, baseHeight, 0f), new Vector3(size.x, 0f, size.y));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
