// RE:AL STEEL - 천막 (차양) (RSAwning)
//
// 막대 위에 천을 펼친 차양 하나. 막대 · 천 · 늘어진 앞자락(물결 · 톱니) · 버팀줄을 메시 하나로 그리고,
// 천은 바람에 펄럭인다 (매 프레임 정점을 움직인다 — 셰이더 · 애니메이션 파일 필요 없음).
//  · 기준점 = 바닥 가운데. 앞(-Z) = 낮은 쪽 (경기장 쪽), 뒤(+Z) = 높은 쪽 (관중석 위)
//  · 막대는 네 모서리 + 가운데 막대(선택). 천은 막대 꼭대기에 묶인 것처럼 막대 사이에서 처진다
//  · 펄럭임은 막대에서 0, 막대 사이 · 앞자락 끝에서 가장 크다
//  · 메시는 저장하지 않고 켤 때마다 다시 만든다 (값을 언제든 고칠 수 있게)
//  · 재질은 조립 도구와 같은 RSSurface (1m = 32px)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;

namespace RealSteel.Build
{
    public enum RSAwningScallop
    {
        [InspectorName("둥근 물결")] 둥근,
        [InspectorName("톱니")] 톱니,
        [InspectorName("일자 (밋밋)")] 일자,
    }

    public enum RSAwningRopes
    {
        [InspectorName("없음")] 없음,
        [InspectorName("뒤 막대만")] 뒤만,
        [InspectorName("앞 · 뒤 막대 모두")] 앞뒤,
    }

    [RSSummary("천막 (차양)", "막대를 세우고 그 위에 천을 펼친 차양. 천은 바람에 펄럭인다.\n" +
        "· 기준점은 바닥 가운데. 앞(-Z, 파란 화살표 반대) = 낮은 쪽, 뒤(+Z) = 높은 쪽\n" +
        "· 관중석 위에 놓으려면 관중석을 고른 채 GameObject ▸ RE_AL STEEL ▸ 천막 (차양), 또는 아래 '맞추기' 버튼\n" +
        "· 관중석이 여러 개면 복제(Ctrl+D)해서 하나씩 맞춘다\n" +
        "· 펄럭임은 플레이 중 + '에디터에서도 움직이기' 를 켜면 씬 뷰에서도")]
    [ExecuteAlways, SelectionBase, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("RE_AL STEEL/Build/천막 (차양)")]
    public class RSAwning : MonoBehaviour
    {
        const float PPM = RSSurface.PixelsPerMeter;

        // ── 모양 ─────────────────────────────
        [RSGroup("모양", true)]
        [RSKey, Min(.5f), Tooltip("가로 (X, m) — 관중석 길이 방향")]
        public float width = 6f;
        [RSKey, Min(.5f), Tooltip("깊이 (Z, m) — 앞 막대 ~ 뒤 막대")]
        public float depth = 3.5f;
        [RSKey, Min(.3f), Tooltip("앞 막대 높이 (m) — 천이 묶이는 높이")]
        public float frontHeight = 2.8f;
        [RSKey, Min(.3f), Tooltip("뒤 막대 높이 (m)")]
        public float backHeight = 4.2f;
        [Range(0f, 1f), Tooltip("천 처짐 (m) — 막대 사이에서 가운데가 내려앉는 깊이")]
        public float sag = .18f;
        [Range(.0625f, 1f), Tooltip("천 칸 크기 (m). 작을수록 부드럽게 휘지만 정점이 많아진다")]
        public float resolution = .25f;

        // ── 막대 ─────────────────────────────
        [RSGroup("막대", true)]
        [Range(0, 6), Tooltip("모서리 막대 사이에 더 세우는 막대 수 (앞 · 뒤 줄마다). 0 = 네 모서리만")]
        public int middlePoles = 0;
        [Range(.03125f, .5f), Tooltip("막대 굵기 (m)")]
        public float poleThickness = .125f;
        [Range(3, 12), Tooltip("막대 면 수 (4 = 각목, 6~8 = 둥근 막대)")]
        public int poleSides = 6;
        [Min(0f), Tooltip("천 위로 튀어나온 막대 끝 길이 (m)")]
        public float poleTop = .15f;
        [Min(0f), Tooltip("땅에 박힌 깊이 (m) — 울퉁불퉁한 바닥에서 막대가 뜨지 않게")]
        public float poleSink = .2f;
        [Tooltip("버팀줄 (막대 꼭대기 → 바깥 땅)")]
        public RSAwningRopes ropes = RSAwningRopes.뒤만;
        [Min(.1f), Tooltip("버팀줄이 막대에서 바깥으로 벌어지는 거리 (m)")]
        public float ropeSpread = 1.2f;
        [Range(.01f, .1f), Tooltip("버팀줄 굵기 (m)")]
        public float ropeThickness = .03125f;

        // ── 앞자락 ───────────────────────────
        [RSGroup("늘어진 자락", true)]
        [Tooltip("앞(낮은 쪽) 가장자리에 늘어진 자락")]
        public bool valanceFront = true;
        [Tooltip("양옆 가장자리에도 자락")]
        public bool valanceSides = false;
        [Tooltip("뒤 가장자리에도 자락")]
        public bool valanceBack = false;
        [Range(0f, 1.5f), Tooltip("자락 길이 (m, 물결 위까지)")]
        public float valanceDrop = .3f;
        [Tooltip("자락 끝 모양")]
        public RSAwningScallop scallop = RSAwningScallop.둥근;
        [Range(.125f, 2f), Tooltip("물결 · 톱니 한 개 너비 (m). 가장자리 길이에 딱 맞게 조금 늘거나 준다")]
        public float scallopWidth = .5f;
        [Range(0f, .5f), Tooltip("물결 · 톱니 깊이 (m)")]
        public float scallopDepth = .125f;

        // ── 바람 ─────────────────────────────
        [RSGroup("바람 (펄럭임)", true)]
        [RSKey, Range(0f, .6f), Tooltip("펄럭임 세기 (m) — 천 가운데가 위아래로 움직이는 폭. 0 = 멈춤")]
        public float windStrength = .1f;
        [Range(0f, 4f), Tooltip("물결이 지나가는 빠르기 (초당 물결 수)")]
        public float windSpeed = .7f;
        [Range(.5f, 12f), Tooltip("물결 길이 (m)")]
        public float waveLength = 3f;
        [Range(0f, 360f), Tooltip("바람이 불어 가는 방향 (월드, 도). 0 = +Z, 90 = +X")]
        public float windAngle = 90f;
        [Range(0f, 1f), Tooltip("돌풍 — 세기가 들쭉날쭉 바뀌는 정도")]
        public float gust = .5f;
        [Range(0f, 1f), Tooltip("잔떨림 — 짧고 빠른 물결을 섞는 정도")]
        public float flutter = .35f;
        [Range(-.3f, .3f), Tooltip("바람에 천이 들리는 정도 (m, + = 위로 부풂)")]
        public float lift = .03f;
        [Range(0f, 60f), Tooltip("자락이 앞뒤로 흔들리는 최대 각도 (도)")]
        public float valanceSwing = 18f;
        [Range(0, 30), Tooltip("초당 갱신 횟수. 0 = 매 프레임 (부드럽게), 8 ~ 12 = 도트 애니메이션처럼 끊어서")]
        public int stepsPerSecond = 0;
        [Tooltip("움직임을 1 픽셀(1/32 m) 단위로 끊는다")]
        public bool pixelSnap = false;
        [Tooltip("이 천막만의 흔들림 순서 (여러 개가 똑같이 움직이지 않게)")]
        public int seed = 1;
        [Tooltip("에디터(플레이 아님)에서도 씬 뷰에서 펄럭이기")]
        public bool animateInEditor = false;

        // ── 칠 ───────────────────────────────
        [RSGroup("칠", true)]
        [Tooltip("천 재질 (1m = 32px 로 타일). 줄무늬는 텍스처 세로 줄 = 앞뒤 방향 줄")]
        public RSSurface clothSurface;
        [Tooltip("막대 · 버팀줄 재질")]
        public RSSurface poleSurface;
        [Tooltip("그림자 드리우기")]
        public bool castShadows = true;
        [Tooltip("재질이 비었을 때 쓸 셰이더 (비우면 Build Pixel Lit)")]
        public Shader shader;

        // ── 맞추기 ───────────────────────────
        [RSGroup("맞추기")]
        [Tooltip("이 물체(관중석 등)를 덮도록 위치 · 방향 · 크기를 맞춘다 (인스펙터 아래 버튼)")]
        public Transform fitTarget;
        [Min(0f), Tooltip("맞출 때 물체 밖으로 더 넓히는 여유 (m, 한쪽)")]
        public float fitMargin = .3f;
        [Min(.5f), Tooltip("맞출 때 물체 꼭대기 위로 띄우는 높이 (m) — 사람이 앉아도 머리가 닿지 않게")]
        public float fitClearance = 1.9f;

        // ── 내부 ─────────────────────────────
        Mesh mesh;
        Material fallbackMat;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> trisCloth = new List<int>();
        readonly List<int> trisPole = new List<int>();
        readonly List<float> supportsX = new List<float>();
        float lastBuiltT = float.NaN;
        bool dirty = true;

        // 매 프레임 바뀌지 않는 값 (Build 시작에 계산)
        Vector2 windLocal;
        float gustNow, timeNow, originAlong;

        void OnEnable()
        {
            dirty = true;
            Build(CurrentTime());
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
            UnityEditor.EditorApplication.update += EditorTick;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
#endif
        }

        void OnDestroy()
        {
            if (mesh != null) DestroyImmediate(mesh);
            if (fallbackMat != null) DestroyImmediate(fallbackMat);
        }

        void OnValidate()
        {
            dirty = true;
#if UNITY_EDITOR
            // OnValidate 안에서 메시를 바로 바꾸면 경고가 나므로 다음 틱에 만든다
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Build(CurrentTime()); };
#endif
        }

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            var r = GetComponent<MeshRenderer>();
            if (!dirty && r != null && !r.isVisible) return;
            Build(CurrentTime());
        }

#if UNITY_EDITOR
        double lastEditorTick;
        void EditorTick()
        {
            if (Application.isPlaying || !animateInEditor || this == null || !isActiveAndEnabled) return;
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now - lastEditorTick < 1.0 / 30.0) return;
            lastEditorTick = now;
            Build(CurrentTime());
            UnityEditor.SceneView.RepaintAll();
        }
#endif

        float CurrentTime()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return animateInEditor ? (float)UnityEditor.EditorApplication.timeSinceStartup : 0f;
#endif
            return Time.time;
        }

        /// <summary>값을 바꾼 뒤 즉시 다시 만든다</summary>
        public void Rebuild() { dirty = true; Build(CurrentTime()); }

        // ─────────────────────────────────────────────────────────────
        //  천 모양 (해석식 — 어디서든 같은 점을 얻을 수 있게)
        // ─────────────────────────────────────────────────────────────

        float W => Mathf.Max(.5f, width);
        float D => Mathf.Max(.5f, depth);

        /// <summary>막대 사이 처짐 모양 0~1 (막대 위치 0, 가운데 1)</summary>
        float SpanShape(float x)
        {
            int segs = middlePoles + 1;
            float p = Mathf.Clamp01((x + W * .5f) / W) * segs;
            float s = p - Mathf.Floor(p);
            if (p >= segs) s = 0f;
            return 4f * s * (1f - s);
        }

        /// <summary>천 위 한 점의 처짐 · 펄럭임 가중치 (막대 0 ~ 가운데 1)</summary>
        float Weight(float x, float v)
        {
            float sx = SpanShape(x);
            float sz = Mathf.Sin(Mathf.PI * Mathf.Clamp01(v));
            return 1f - (1f - sx) * (1f - sz);
        }

        /// <summary>바람 물결 (-1 ~ 1 근처)</summary>
        float Wave(float x, float z, float phase)
        {
            // 월드 기준으로 이어지게 — 여러 천막에 같은 바람 물결이 차례로 지나간다
            float along = x * windLocal.x + z * windLocal.y + originAlong;
            float wl = Mathf.Max(.25f, waveLength);
            float a = Mathf.Sin(2f * Mathf.PI * (along / wl - timeNow * windSpeed) + phase);
            float b = Mathf.Sin(2f * Mathf.PI * (along / (wl * .37f) - timeNow * windSpeed * 2.3f) + phase * 1.7f + 1.3f);
            float c = Mathf.Sin(2f * Mathf.PI * (x * .61f - z * .43f) / wl - timeNow * windSpeed * 4.1f + seed * .77f);
            return a * (1f - flutter * .5f) + (b * .6f + c * .4f) * flutter;
        }

        /// <summary>천 위 점 (x, v) 의 위치. v = 0 앞 가장자리, 1 뒤 가장자리</summary>
        Vector3 ClothPoint(float x, float v)
        {
            float z = (v - .5f) * D;
            float y = Mathf.Lerp(frontHeight, backHeight, v);
            float w = Weight(x, v);
            y -= sag * w;
            if (windStrength > 0f)
                y += w * (windStrength * (1f + gustNow) * Wave(x, z, 0f) + lift * (1f + gustNow));
            return new Vector3(x, y, z);
        }

        float Snap(float f) { return pixelSnap ? Mathf.Round(f * PPM) / PPM : f; }

        Vector3 SnapDelta(Vector3 rest, Vector3 moved)
        {
            if (!pixelSnap) return moved;
            var d = moved - rest;
            return rest + new Vector3(Snap(d.x), Snap(d.y), Snap(d.z));
        }

        // ─────────────────────────────────────────────────────────────
        //  메시 만들기
        // ─────────────────────────────────────────────────────────────

        void Build(float t)
        {
            if (stepsPerSecond > 0) t = Mathf.Floor(t * stepsPerSecond) / stepsPerSecond;
            if (!dirty && t == lastBuiltT) return;
            lastBuiltT = t;
            dirty = false;

            timeNow = t + seed * 3.17f;
            float ang = (windAngle - transform.eulerAngles.y) * Mathf.Deg2Rad;
            windLocal = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang));
            float wa = windAngle * Mathf.Deg2Rad;
            var wp = transform.position;
            originAlong = wp.x * Mathf.Sin(wa) + wp.z * Mathf.Cos(wa);
            // 돌풍도 바람 방향으로 흘러가게 (바람 위쪽 천막이 먼저 흔들림)
            gustNow = gust * (Mathf.PerlinNoise(timeNow * .35f - originAlong * .04f, 1.31f + seed * .07f) - .5f) * 2f;

            verts.Clear(); uvs.Clear(); trisCloth.Clear(); trisPole.Clear();

            var clothTex = clothSurface != null ? clothSurface.TexturePixels : new Vector2Int(32, 32);
            var poleTex = poleSurface != null ? poleSurface.TexturePixels : new Vector2Int(32, 32);

            BuildCloth(clothTex);
            if (valanceFront) BuildValance(0, clothTex);
            if (valanceSides) { BuildValance(1, clothTex); BuildValance(3, clothTex); }
            if (valanceBack) BuildValance(2, clothTex);
            BuildPoles(poleTex);

            if (mesh == null)
            {
                mesh = new Mesh { name = "천막 (자동 · 저장 안 됨)", hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic();
            }
            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trisCloth, 0, false);
            mesh.SetTriangles(trisPole, 1, false);
            mesh.RecalculateNormals();
            float pad = windStrength * 2f + valanceDrop + scallopDepth + .5f;
            var b = new Bounds(new Vector3(0f, Mathf.Max(frontHeight, backHeight) * .5f, 0f),
                new Vector3(W + ropeSpread * 2f + pad * 2f, Mathf.Max(frontHeight, backHeight) + poleTop + poleSink + pad * 2f, D + ropeSpread * 2f + pad * 2f));
            mesh.bounds = b;

            var mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh != mesh) mf.sharedMesh = mesh;
            var mr = GetComponent<MeshRenderer>();
            ApplyMaterials(mr);
        }

        void ApplyMaterials(MeshRenderer mr)
        {
            if (mr == null) return;
            var sh = shader != null ? shader : Shader.Find(RSSurface.ShaderName);
            Material cm = clothSurface != null ? clothSurface.GetMaterial(sh) : Fallback(sh);
            Material pm = poleSurface != null ? poleSurface.GetMaterial(sh) : Fallback(sh);
            var cur = mr.sharedMaterials;
            if (cur.Length != 2 || cur[0] != cm || cur[1] != pm) mr.sharedMaterials = new[] { cm, pm };
            var mode = castShadows ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;
            if (mr.shadowCastingMode != mode) mr.shadowCastingMode = mode;
        }

        Material Fallback(Shader sh)
        {
            if (fallbackMat == null && sh != null)
            {
                fallbackMat = new Material(sh) { name = "천막 기본 회색", hideFlags = HideFlags.DontSave };
                fallbackMat.SetColor("_BaseColor", new Color(.8f, .78f, .74f));
            }
            return fallbackMat;
        }

        /// <summary>앞면 + 뒷면 사각형 (뒷면은 정점을 따로 둬서 노멀이 반대로)</summary>
        void QuadBoth(List<int> tris, int a, int b, int c, int d, int backOffset)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
            tris.Add(a + backOffset); tris.Add(c + backOffset); tris.Add(b + backOffset);
            tris.Add(a + backOffset); tris.Add(d + backOffset); tris.Add(c + backOffset);
        }

        /// <summary>앞면 정점들을 그대로 한 벌 더 (뒷면용)</summary>
        void DuplicateFrom(int start)
        {
            int n = verts.Count - start;
            for (int i = 0; i < n; i++) { verts.Add(verts[start + i]); uvs.Add(uvs[start + i]); }
        }

        void BuildCloth(Vector2Int tex)
        {
            int segs = middlePoles + 1;
            float res = Mathf.Max(.0625f, resolution);
            int perSeg = Mathf.Max(2, Mathf.CeilToInt(W / segs / res));
            int nx = segs * perSeg + 1;
            int nz = Mathf.Max(3, Mathf.CeilToInt(D / res) + 1);
            int start = verts.Count;

            for (int j = 0; j < nz; j++)
            {
                float v = j / (float)(nz - 1);
                for (int i = 0; i < nx; i++)
                {
                    float x = -W * .5f + W * i / (nx - 1);
                    var p = ClothPoint(x, v);
                    if (pixelSnap)
                    {
                        float z = (v - .5f) * D;
                        var rest = new Vector3(x, Mathf.Lerp(frontHeight, backHeight, v) - sag * Weight(x, v), z);
                        p = SnapDelta(rest, p);
                    }
                    verts.Add(p);
                    // 줄무늬(텍스처 세로 줄)가 앞뒤 방향으로 가게. 천을 따라 잰 길이 대신 평면 길이 (처짐이 작아 차이 미미)
                    float slope = Mathf.Sqrt(D * D + (backHeight - frontHeight) * (backHeight - frontHeight));
                    uvs.Add(new Vector2((x + W * .5f) * PPM / tex.x, v * slope * PPM / tex.y));
                }
            }
            DuplicateFrom(start);
            int back = nx * nz;
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = start + j * nx + i;
                    // 위에서 봤을 때 시계 방향 = 앞면이 위
                    QuadBoth(trisCloth, a, a + nx, a + nx + 1, a + 1, back);
                }
        }

        /// <summary>가장자리 자락. edge 0 = 앞, 1 = 오른, 2 = 뒤, 3 = 왼</summary>
        void BuildValance(int edge, Vector2Int tex)
        {
            if (valanceDrop <= 0f && (scallop == RSAwningScallop.일자 || scallopDepth <= 0f)) return;
            float L = (edge == 0 || edge == 2) ? W : D;
            int teeth = Mathf.Max(1, Mathf.RoundToInt(L / Mathf.Max(.125f, scallopWidth)));
            int perTooth = scallop == RSAwningScallop.일자 ? 2 : (scallop == RSAwningScallop.톱니 ? 2 : 8);
            int cols = teeth * perTooth + 1;
            const int rows = 4;
            int start = verts.Count;

            Vector3 outDir;
            switch (edge)
            {
                case 0: outDir = Vector3.back; break;
                case 1: outDir = Vector3.right; break;
                case 2: outDir = Vector3.forward; break;
                default: outDir = Vector3.left; break;
            }
            float windOut = Vector2.Dot(windLocal, new Vector2(outDir.x, outDir.z));
            float uOffset = edge == 0 ? 0f : (edge == 1 ? W : (edge == 2 ? W + D : W * 2f + D));

            for (int i = 0; i < cols; i++)
            {
                float s = L * i / (cols - 1);
                float x, v;
                switch (edge)
                {
                    case 0: x = -W * .5f + s; v = 0f; break;
                    case 1: x = W * .5f; v = s / D; break;
                    case 2: x = W * .5f - s; v = 1f; break;
                    default: x = -W * .5f; v = 1f - s / D; break;
                }
                var top = ClothPoint(x, v);
                var topRest = new Vector3(x, Mathf.Lerp(frontHeight, backHeight, v) - sag * Weight(x, v), (v - .5f) * D);

                // 물결 · 톱니
                float tt = (s / L * teeth) % 1f;
                if (i == cols - 1) tt = 0f;
                float tooth;
                switch (scallop)
                {
                    case RSAwningScallop.둥근: tooth = Mathf.Sqrt(Mathf.Max(0f, 1f - (2f * tt - 1f) * (2f * tt - 1f))); break;
                    case RSAwningScallop.톱니: tooth = 1f - Mathf.Abs(2f * tt - 1f); break;
                    default: tooth = 0f; break;
                }
                float len = valanceDrop + scallopDepth * tooth;

                // 흔들림 각 (바깥으로 + / 안으로 -)
                float swing = 0f;
                if (windStrength > 0f && valanceSwing > 0f)
                {
                    float wv = Wave(x, (v - .5f) * D, .8f);
                    swing = valanceSwing * Mathf.Deg2Rad * (1f + gustNow) * (wv * .65f + windOut * .35f) * Mathf.Clamp01(windStrength / .1f);
                }

                for (int r = 0; r < rows; r++)
                {
                    float h = len * r / (rows - 1);
                    // 아래로 갈수록 더 휘게 (천이 늘어진 느낌)
                    float a = swing * (.6f + .4f * r / (rows - 1));
                    var p = top + outDir * (h * Mathf.Sin(a)) + Vector3.down * (h * Mathf.Cos(a));
                    var rest = topRest + Vector3.down * h;
                    verts.Add(pixelSnap ? SnapDelta(rest, p) : p);
                    uvs.Add(new Vector2((uOffset + s) * PPM / tex.x, -h * PPM / tex.y));
                }
            }
            DuplicateFrom(start);
            int back = cols * rows;
            for (int i = 0; i < cols - 1; i++)
                for (int r = 0; r < rows - 1; r++)
                {
                    int a = start + i * rows + r;
                    QuadBoth(trisCloth, a, a + rows, a + rows + 1, a + 1, back);
                }
        }

        void BuildPoles(Vector2Int tex)
        {
            supportsX.Clear();
            int segs = middlePoles + 1;
            for (int i = 0; i <= segs; i++) supportsX.Add(-W * .5f + W * i / segs);

            float r = poleThickness * .5f;
            foreach (var x in supportsX)
            {
                AddPole(new Vector3(x, 0f, -D * .5f), frontHeight, r, tex);
                AddPole(new Vector3(x, 0f, D * .5f), backHeight, r, tex);
            }

            if (ropes == RSAwningRopes.없음) return;
            for (int k = 0; k < supportsX.Count; k++)
            {
                float x = supportsX[k];
                float sx = k == 0 ? -1f : (k == supportsX.Count - 1 ? 1f : 0f);
                // 뒤
                {
                    var dir = new Vector3(sx * .7f, 0f, 1f).normalized;
                    var top = new Vector3(x, backHeight, D * .5f);
                    AddRope(top, new Vector3(x, 0f, D * .5f) + dir * (ropeSpread * Mathf.Max(1f, backHeight / 2.5f)), tex);
                }
                if (ropes == RSAwningRopes.앞뒤)
                {
                    var dir = new Vector3(sx * .7f, 0f, -1f).normalized;
                    var top = new Vector3(x, frontHeight, -D * .5f);
                    AddRope(top, new Vector3(x, 0f, -D * .5f) + dir * (ropeSpread * Mathf.Max(1f, frontHeight / 2.5f)), tex);
                }
            }
        }

        void AddPole(Vector3 basePos, float height, float r, Vector2Int tex)
        {
            int n = Mathf.Clamp(poleSides, 3, 12);
            float y0 = -poleSink, y1 = height + poleTop;
            float circ = 2f * Mathf.PI * r;
            int start = verts.Count;
            for (int i = 0; i <= n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f + Mathf.PI / n;
                var o = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float u = circ * i / n * PPM / tex.x;
                verts.Add(basePos + o + Vector3.up * y0); uvs.Add(new Vector2(u, y0 * PPM / tex.y));
                verts.Add(basePos + o + Vector3.up * y1); uvs.Add(new Vector2(u, y1 * PPM / tex.y));
            }
            for (int i = 0; i < n; i++)
            {
                int a = start + i * 2;
                trisPole.Add(a); trisPole.Add(a + 1); trisPole.Add(a + 3);
                trisPole.Add(a); trisPole.Add(a + 3); trisPole.Add(a + 2);
            }
            // 꼭대기 뚜껑
            int c = verts.Count;
            verts.Add(basePos + Vector3.up * y1); uvs.Add(new Vector2(.5f, .5f));
            for (int i = 0; i <= n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f + Mathf.PI / n;
                verts.Add(basePos + new Vector3(Mathf.Cos(a) * r, y1, Mathf.Sin(a) * r));
                uvs.Add(new Vector2(.5f + Mathf.Cos(a) * r * PPM / tex.x, .5f + Mathf.Sin(a) * r * PPM / tex.y));
            }
            for (int i = 0; i < n; i++) { trisPole.Add(c); trisPole.Add(c + i + 2); trisPole.Add(c + i + 1); }
        }

        void AddRope(Vector3 a, Vector3 b, Vector2Int tex)
        {
            float h = Mathf.Max(.005f, ropeThickness * .5f);
            var dir = (b - a);
            float len = dir.magnitude;
            if (len < 1e-3f) return;
            dir /= len;
            var side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            var up = Vector3.Cross(side, dir).normalized;
            b += Vector3.down * poleSink * .5f;   // 땅에 박히게
            var off = new[] { side * h + up * h, -side * h + up * h, -side * h - up * h, side * h - up * h };
            for (int f = 0; f < 4; f++)
            {
                int s = verts.Count;
                var o0 = off[f]; var o1 = off[(f + 1) % 4];
                verts.Add(a + o0); verts.Add(a + o1); verts.Add(b + o1); verts.Add(b + o0);
                float v1 = len * PPM / tex.y;
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(.1f, 0f)); uvs.Add(new Vector2(.1f, v1)); uvs.Add(new Vector2(0f, v1));
                trisPole.Add(s); trisPole.Add(s + 2); trisPole.Add(s + 1);
                trisPole.Add(s); trisPole.Add(s + 3); trisPole.Add(s + 2);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  맞추기 (관중석 등 덮기)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// target 을 덮도록 위치 · 방향 · 크기를 맞춘다. 낮은 쪽을 앞(-Z)으로 본다.
        /// 성공하면 true. 메시를 읽을 수 없으면 렌더러 경계 상자로 대신한다.
        /// </summary>
        public bool FitTo(Transform target)
        {
            if (target == null) return false;
            var mf = target.GetComponentInChildren<MeshFilter>();
            var tr = mf != null ? mf.transform : target;

            // 물체의 세 축 중 가장 위를 향한 축 = 높이, 나머지 둘 중 긴 쪽 = 가로
            Vector3[] axes = { tr.right, tr.up, tr.forward };
            Vector3[] scaled = { tr.TransformVector(Vector3.right), tr.TransformVector(Vector3.up), tr.TransformVector(Vector3.forward) };
            int upAxis = 0; float best = -1f;
            for (int i = 0; i < 3; i++) { float d = Mathf.Abs(Vector3.Dot(axes[i], Vector3.up)); if (d > best) { best = d; upAxis = i; } }

            Bounds lb;
            if (mf != null && mf.sharedMesh != null) lb = mf.sharedMesh.bounds;
            else
            {
                var rr = target.GetComponentInChildren<Renderer>();
                if (rr == null) return false;
                lb = new Bounds(tr.InverseTransformPoint(rr.bounds.center), tr.InverseTransformVector(rr.bounds.size));
            }

            int h0 = (upAxis + 1) % 3, h1 = (upAxis + 2) % 3;
            float len0 = Mathf.Abs(lb.size[h0] * scaled[h0].magnitude);
            float len1 = Mathf.Abs(lb.size[h1] * scaled[h1].magnitude);
            int wAxis = len0 >= len1 ? h0 : h1;
            int dAxis = wAxis == h0 ? h1 : h0;

            // 월드 꼭짓점 8개 → 바닥 · 꼭대기
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x, (i & 2) == 0 ? lb.min.y : lb.max.y, (i & 4) == 0 ? lb.min.z : lb.max.z);
                float y = tr.TransformPoint(c).y;
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }

            // 깊이 축 방향 (수평)
            var dWorld = scaled[dAxis]; dWorld.y = 0f;
            if (dWorld.sqrMagnitude < 1e-6f) return false;
            dWorld.Normalize();

            // 어느 쪽이 낮은지 — 정점 높이를 깊이 앞 · 뒤 반으로 나눠 평균
            float lowSideSign = -1f;   // 기본: 물체 축 - 쪽이 낮다고 가정
            float frontTop = maxY, backTop = maxY;
            if (mf != null && mf.sharedMesh != null)
            {
                try
                {
                    var vs = mf.sharedMesh.vertices;
                    if (vs != null && vs.Length > 0)
                    {
                        float mid = lb.center[dAxis];
                        float maxNeg = float.MinValue, maxPos = float.MinValue;
                        double sumNeg = 0, sumPos = 0; int nNeg = 0, nPos = 0;
                        for (int i = 0; i < vs.Length; i++)
                        {
                            float y = tr.TransformPoint(vs[i]).y;
                            if (vs[i][dAxis] < mid) { sumNeg += y; nNeg++; maxNeg = Mathf.Max(maxNeg, y); }
                            else { sumPos += y; nPos++; maxPos = Mathf.Max(maxPos, y); }
                        }
                        if (nNeg > 0 && nPos > 0)
                        {
                            bool negLower = sumNeg / nNeg < sumPos / nPos;
                            lowSideSign = negLower ? -1f : 1f;
                            // 앞 절반의 꼭대기 = 앞 가장자리에서 조금 들어간 곳의 높이에 가깝다
                            frontTop = negLower ? maxNeg : maxPos;
                            backTop = negLower ? maxPos : maxNeg;
                        }
                    }
                }
                catch { /* 읽을 수 없는 메시 — 경계 상자로 */ }
            }

            // 축의 + 방향이 월드에서 dWorld. 낮은 쪽 = 앞 = 천막 -Z → 천막 +Z = 높은 쪽
            var fwd = dWorld * (-lowSideSign);
            var centerW = tr.TransformPoint(lb.center);

            width = len0 >= len1 ? len0 : len1;
            depth = len0 >= len1 ? len1 : len0;
            width += fitMargin * 2f;
            depth += fitMargin * 2f;

            float baseY = minY;
            backHeight = Mathf.Max(1f, backTop - baseY + fitClearance);
            frontHeight = Mathf.Clamp(frontTop - baseY + fitClearance, 1f, backHeight);
            // 앞이 너무 높으면 경사가 없어 밋밋 — 뒤보다 최소 0.4m 낮게
            if (backHeight - frontHeight < .4f) frontHeight = Mathf.Max(1f, backHeight - .4f);

            transform.position = new Vector3(centerW.x, baseY, centerW.z);
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            Rebuild();
            return true;
        }
    }
}
