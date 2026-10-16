// RE:AL STEEL - 간접광 (라이트맵 굽지 않음)
//
// 하늘빛(앰비언트)은 어디서나 같은 밝기라 구석 · 구덩이 · 다리 밑도 똑같이 밝고, 햇빛이 바닥에서 튀어
// 주변을 물들이는 것(녹슨 고물 옆이 주황, 풀밭 옆이 초록)이 없다. 이 컴포넌트가 그 두 가지를 흉내 낸다.
//
//   1. 하늘 가림 (구석 어둡게)  바닥 높이 지도를 만들어, 칸마다 주변 지형이 하늘을 얼마나 가리는지 계산
//   2. 튄 빛 (반사광)          햇빛을 받는 바닥 칸의 색(칠 레이어 · 머티리얼 색)을 주변으로 번지게
//   3. 색 번짐 요소           '간접광 색 번짐' 을 붙인 오브젝트 · (선택) 포인트 라이트 색을 더함
//
// 결과는 바닥을 덮는 작은 텍스처 두 장(색 + 하늘 보임 / 바닥 높이)이고, RS 셰이더(지형 · 건물 · 캐릭터 · 풀꽃)가
// 앰비언트를 계산할 때 이 값을 곱하고 더한다 (RSIndirect.hlsl). 다른 셰이더는 영향 없음.
//
// 바닥은 콜라이더로 잰다 (위에서 아래로 레이). 콜라이더가 없는 물체는 계산에 안 들어간다.
// 해가 움직이거나(시간대) 지형이 바뀌면 알아서 다시 계산한다. 시간이 흐르는 동안엔 여러 프레임에 나눠 계산하고 서서히 바꾼다.
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("간접광 (굽지 않음)", "라이트맵 없이 '구석은 어둡게 · 햇빛 받은 바닥 색은 주변으로 번지게' 한다.\n" +
        "· 지형 · 건물 · 캐릭터 · 풀꽃(RS 셰이더)이 받는다. 세기는 아래 '받는 쪽 세기' 에서 따로\n" +
        "· 해가 움직이거나 지형을 고치면 자동으로 다시 계산 (수동: '지금 다시 계산')\n" +
        "· 바닥은 콜라이더로 잰다 — 콜라이더 없는 소품은 안 들어간다\n" +
        "· 색 번짐을 더하려면 오브젝트에 '간접광 색 번짐' 을 붙인다")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Lighting/간접광 (굽지 않음)")]
    public partial class RSIndirectLight : MonoBehaviour, IRSEditorAnimated
    {
        [RSGroup("범위")]
        [RSHelp("계산할 바닥 범위. '지형에 맞추기' 면 RS 지형 크기에 자동으로 맞는다. 칸이 작을수록 곱지만 계산이 늘어난다 (40m 에 0.5m 칸 = 6400칸, 수 ms).")]
        [Tooltip("RS 지형(플레이 구역) 크기에 자동으로 맞춘다. 끄면 아래 가운데 · 크기")]
        public bool fitToTerrain = true;
        [Tooltip("범위 가운데 (월드). 지형에 맞추기를 끄면 씀")]
        public Vector3 center = Vector3.zero;
        [Tooltip("범위 크기 X · Z (m)")]
        public Vector2 size = new Vector2(40f, 40f);
        [Range(0.25f, 2f), Tooltip("칸 크기 (m). 0.5 권장")]
        public float cellSize = 0.5f;
        [Tooltip("바닥으로 칠 레이어 (캐릭터 · 트리거는 자동 제외)")]
        public LayerMask groundLayers = ~0;

        // ── 룩 (스테이지 룩 프로필로 옮길 수 있는 값) ──
        [System.Serializable]
        public class Look
        {
            [RSGroup("구석 어둡게 (하늘 가림)")]
            [RSHelp("주변 벽 · 둔덕 · 구덩이 벽이 하늘을 가리는 만큼 앰비언트(하늘빛)를 줄인다. 절벽 밑 · 골목 · 수로가 자연스럽게 어두워진다.")]
            [Range(0f, 1f), Tooltip("세기. 0 = 끔")]
            [RSKey]
            public float occlusion = 0.75f;
            [Range(1f, 12f), Tooltip("얼마나 먼 벽까지 보나 (m)")]
            public float occlusionReach = 5f;
            [Range(0f, 1f), Tooltip("지붕 · 다리 밑(위가 막힌 곳)의 하늘빛 밝기. 0 = 깜깜")]
            public float underCover = 0.45f;
            [RSGroup("튄 빛 (반사광)")]
            [RSHelp("햇빛 · 달빛을 받은 바닥의 색이 주변 벽 아랫부분 · 캐릭터 옆면 · 그늘진 바닥으로 번진다.")]
            [Range(0f, 3f), Tooltip("세기. 0 = 끔")]
            [RSKey]
            public float bounce = 1.2f;
            [Range(0.5f, 8f), Tooltip("옆으로 번지는 거리 (m)")]
            public float bounceReach = 2.5f;
            [Range(0.5f, 8f), Tooltip("바닥에서 위로 번지는 높이 (m). 벽 · 캐릭터가 이 높이까지 물든다")]
            public float bounceHeight = 3f;
            [Range(0f, 2f), Tooltip("번지는 색의 진하기 (1 = 바닥 색 그대로, 크면 더 알록달록)")]
            public float saturation = 1.2f;
            [RSGroup("받는 쪽 세기")]
            [RSHelp("셰이더 종류별로 얼마나 받을지. 캐릭터는 옥토패스처럼 조금 덜 받아야 어두운 곳에서도 잘 보인다.")]
            [Range(0f, 1f), Tooltip("RS 지형")]
            public float terrain = 1f;
            [Range(0f, 1f), Tooltip("건물 · 소품 (트라이플래너)")]
            public float buildings = 1f;
            [Range(0f, 1f), Tooltip("캐릭터 · 빌보드 스프라이트")]
            [RSKey]
            public float characters = 0.6f;
            [Range(0f, 1f), Tooltip("풀꽃")]
            public float foliage = 1f;
            [RSGroup("색 번짐 · 불빛")]
            [Tooltip("'간접광 색 번짐' 요소를 더한다")]
            public bool includeSources = true;
            [Tooltip("포인트 · 스폿 라이트 색도 바닥에 은은하게 번지게 (밤에 가로등 · 캐릭터 빛 둘레)")]
            public bool includeLights = true;
            [Range(0f, 2f), Tooltip("라이트 번짐 세기")]
            public float lightSpill = 0.5f;
        }

        [SerializeField, RSLook] Look look = new Look();

        /// <summary>지금 쓰는 룩 값 — 스테이지 룩 프로필이 있으면 그 값, 없으면 이 컴포넌트 값</summary>
        public Look L { get { var p = RSStageLook.Current; return p != null ? p.indirect : look; } }
        /// <summary>이 컴포넌트에 저장된 값 (프로필이 없을 때 쓰는 값)</summary>
        public Look LocalLook { get { return look; } }
        /// <summary>스테이지 룩 프로필 값을 쓰고 있는지</summary>
        public bool UsesStageLook { get { return RSStageLook.Current != null; } }





        [RSGroup("갱신")]
        [Range(0.5f, 15f), Tooltip("해가 이 각도 이상 움직이면 다시 계산 (도)")]
        public float sunChangeDegrees = 2f;
        [Range(0f, 3f), Tooltip("새 계산 결과로 서서히 바뀌는 시간 (초, 플레이 중)")]
        public float blendSeconds = 1f;
        [Tooltip("해 (Directional Light). 비우면 RenderSettings.sun")]
        public Light sun;

        // ─────────────────────────────────────────────────────────────
        // 상태
        // ─────────────────────────────────────────────────────────────

        public static RSIndirectLight Active { get; private set; }

        int nx, nz;
        float cs;
        Vector2 origin;          // 범위 XZ 최솟값
        float yTop, yBottom;
        float[] height, vis;
        Vector3[] normal;
        Color[] albedo, sunBounce, extra, target, current;
        bool[] valid;
        int validCount;
        Color outside;

        Texture2D tex, hTex;
        Color[] upload;

        bool geometryDirty = true, sunDirty = true, combineDirty = true, blending;
        float nextGeometryTry;
        Vector3 lastSunDir; Color lastSunColor; float lastSunIntensity;
        float sourcesSig = float.NaN;
        int sunJob = -1;         // 나눠서 계산 중인 칸 번호 (-1 = 안 함)
        Color[] sunRad;
        Vector3 jobL; Color jobColor;

        // 인스펙터 표시용
        public int CellsX => nx;
        public int CellsZ => nz;
        public int ValidCells => validCount;
        public float LastGeometryMs { get; private set; }
        public float LastSunMs { get; private set; }
        public Texture2D PreviewVis { get; private set; }
        public Texture2D PreviewBounce { get; private set; }
        public Bounds Area => new Bounds(new Vector3(origin.x + nx * cs * 0.5f, (yTop + yBottom) * 0.5f, origin.y + nz * cs * 0.5f),
                                         new Vector3(nx * cs, yTop - yBottom, nz * cs));

        static readonly int IdTex = Shader.PropertyToID("_RSIndirectTex");
        static readonly int IdHeight = Shader.PropertyToID("_RSIndirectHeightTex");
        static readonly int IdRect = Shader.PropertyToID("_RSIndirectRect");
        static readonly int IdParams = Shader.PropertyToID("_RSIndirectParams");
        static readonly int IdReceive = Shader.PropertyToID("_RSIndirectReceive");
        static readonly int IdOutside = Shader.PropertyToID("_RSIndirectOutside");
        static readonly int IdExtra = Shader.PropertyToID("_RSIndirectExtra");

        const string GenName = "__RST_Generated";

        // ─────────────────────────────────────────────────────────────
        // 수명
        // ─────────────────────────────────────────────────────────────

        void OnEnable()
        {

            MigrateLegacy();

            RSStageLook.Changed += OnStageLookChanged;
            Active = this;
            geometryDirty = true;
            nextGeometryTry = 0f;
            RSLightingClock.Register(this);
        }

        /// <summary>스테이지 룩 프로필이 바뀌거나 값이 바뀌면 (인스펙터에서 프로필을 고친 경우 이 컴포넌트의 OnValidate 는 안 불린다)</summary>

        [System.NonSerialized] int lookSeen;
        [System.NonSerialized] RSStageLook lookFrom;
        void OnStageLookChanged()

        {

            if (this == null || !isActiveAndEnabled) return;
            // 다른 묶음(예: 젖은 바닥 슬라이더)만 바뀐 거면 건너뛴다 — 무거운 재계산을 피하려고
            int h = JsonUtility.ToJson(L).GetHashCode();
            if (h == lookSeen && RSStageLook.Current == lookFrom) return;
            lookSeen = h; lookFrom = RSStageLook.Current;

            OnValidate();

        }


        void OnDisable()
        {

            RSStageLook.Changed -= OnStageLookChanged;
            RSLightingClock.Unregister(this);
            if (Active == this) Active = null;
            Shader.SetGlobalVector(IdParams, Vector4.zero);   // 셰이더 쪽 끔
        }

        void OnDestroy()
        {
            Kill(tex); Kill(hTex); Kill(PreviewVis); Kill(PreviewBounce);
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void OnValidate()
        {

            MigrateLegacy();
            geometryDirty = true;
            nextGeometryTry = 0f;
        }

        /// <summary>지형 · 건물이 바뀌었을 때 (에디터 도구가 부른다)</summary>
        public void MarkGeometryDirty(float delay = 0f)
        {
            failCount = 0;
            geometryDirty = true;
            nextGeometryTry = Now + delay;
        }

        /// <summary>전부 지금 다시 계산</summary>
        public void RecalculateNow()
        {
            geometryDirty = true;
            nextGeometryTry = 0f;
            sunJob = -1;
            Tick(0f, true);
        }

        static float Now => Time.realtimeSinceStartup;

        /// <summary>텍스처 평균색 기억을 지운다 (텍스처를 바꿨을 때)</summary>
        public static void ClearAlbedoCache() { RSAlbedo.ClearCache(); }

        /// <summary>
        /// 에디터 시계가 이번 틱에 일을 시킬지. true 면 씬 뷰가 다시 그려지므로 할 일이 있을 때만 true.
        /// (바닥을 못 찾아 다시 시도하는 동안에는 시도할 시각이 됐을 때만 — 계속 true 면 화면이 쉬지 않고 다시 그려진다)
        /// </summary>
        public bool WantsEditorAnimation
        {
            get
            {
                if (!isActiveAndEnabled) return false;
                if (geometryDirty) return Now >= nextGeometryTry;
                if (valid == null || validCount == 0) return false;
                return sunJob >= 0 || blending || SunChanged() || SourcesChanged(false);
            }
        }

        int failCount;

        public void EditorTick(float dt) { Tick(dt, true); }

        void Update()
        {
            if (Application.isPlaying) Tick(Time.deltaTime, false);
        }

        void Tick(float dt, bool instant)
        {
            if (geometryDirty && Now >= nextGeometryTry)
            {
                // 플레이 첫 프레임엔 지형이 아직 안 만들어졌을 수 있다
                if (Application.isPlaying && Time.frameCount < 3) return;
                ScanGeometry();
                if (validCount == 0)
                {
                    // 바닥을 못 찾으면 잠시 뒤 다시 (1초 → 2초 → … 최대 10초 간격)
                    nextGeometryTry = Now + Mathf.Min(10f, 1f + failCount++);
                    ApplyGlobals();   // 셰이더 쪽 끔
                    return;
                }
                failCount = 0;
                geometryDirty = false;
                sunDirty = true;
                sunJob = -1;
            }
            if (valid == null || validCount == 0) return;

            if (SunChanged()) sunDirty = true;
            if (sunDirty && sunJob < 0) StartSunJob();
            if (sunJob >= 0) RunSunJob(instant || !Application.isPlaying ? int.MaxValue : 6000);

            if (SourcesChanged(true)) combineDirty = true;
            if (combineDirty && sunJob < 0) Combine(instant || !Application.isPlaying || blendSeconds <= 0f);

            if (blending)
            {
                float k = blendSeconds > 0f ? Mathf.Clamp01(dt / blendSeconds) * 1.5f : 1f;
                bool done = true;
                for (int i = 0; i < current.Length; i++)
                {
                    current[i] = Color.Lerp(current[i], target[i], k);
                    if (done && MaxDiff(current[i], target[i]) > 0.002f) done = false;
                }
                if (done) { System.Array.Copy(target, current, current.Length); blending = false; }
                Upload();
            }
            ApplyGlobals();
        }

        static float MaxDiff(Color a, Color b)
        {
            return Mathf.Max(Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g)), Mathf.Max(Mathf.Abs(a.b - b.b), Mathf.Abs(a.a - b.a)));
        }

        // ─────────────────────────────────────────────────────────────
        // 1) 바닥 스캔 (높이 · 법선 · 색 · 하늘 가림)
        // ─────────────────────────────────────────────────────────────

        void ResolveArea()
        {
            Bounds b = default; bool any = false;
            if (fitToTerrain)
            {
                foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!r.name.StartsWith("Chunk")) continue;
                    var p = r.transform.parent;
                    if (p == null || !p.name.StartsWith(GenName)) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
            }
            if (!any) b = new Bounds(center, new Vector3(Mathf.Max(1f, size.x), 20f, Mathf.Max(1f, size.y)));

            cs = Mathf.Max(0.1f, cellSize);
            // 너무 많은 칸이면 칸을 키운다 (최대 약 6만 칸)
            while ((b.size.x / cs) * (b.size.z / cs) > 60000f) cs *= 1.25f;
            nx = Mathf.Max(2, Mathf.CeilToInt(b.size.x / cs));
            nz = Mathf.Max(2, Mathf.CeilToInt(b.size.z / cs));
            origin = new Vector2(b.center.x - nx * cs * 0.5f, b.center.z - nz * cs * 0.5f);
            yTop = b.max.y + 40f;
            yBottom = b.min.y - 5f;
        }

        static readonly RaycastHit[] hits = new RaycastHit[16];

        static bool Ignored(Collider c)
        {
            if (c == null) return true;
            if (c is CharacterController) return true;
            var rb = c.attachedRigidbody;
            return rb != null && !rb.isKinematic;   // 움직이는 물체는 제외
        }

        /// <summary>위에서 아래로 쏴서 가장 위의 바닥</summary>
        bool CastDown(float x, float z, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(new Vector3(x, yTop, z), Vector3.down, hits, yTop - yBottom, groundLayers, QueryTriggerInteraction.Ignore);
            float bd = float.MaxValue; bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (Ignored(hits[i].collider)) continue;
                if (hits[i].distance < bd) { bd = hits[i].distance; best = hits[i]; found = true; }
            }
            return found;
        }

        /// <summary>p 에서 dir 쪽으로 막히는지 (캐릭터 · 움직이는 물체는 무시)</summary>
        bool Blocked(Vector3 p, Vector3 dir, float dist)
        {
            int n = Physics.RaycastNonAlloc(p, dir, hits, dist, groundLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!Ignored(hits[i].collider)) return true;
            return false;
        }

        void ScanGeometry()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ResolveArea();
            int n = nx * nz;
            height = new float[n]; vis = new float[n]; normal = new Vector3[n];
            albedo = new Color[n]; valid = new bool[n];
            sunBounce = new Color[n]; extra = new Color[n]; target = new Color[n];
            if (current == null || current.Length != n) current = new Color[n];
            validCount = 0;

            Physics.SyncTransforms();
            RSAlbedo.BeginScan();
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i;
                    float x = origin.x + (i + 0.5f) * cs, z = origin.y + (j + 0.5f) * cs;
                    if (CastDown(x, z, out var hit))
                    {
                        height[k] = hit.point.y;
                        normal[k] = hit.normal;
                        albedo[k] = RSAlbedo.At(hit);
                        valid[k] = true;
                        validCount++;
                    }
                    else
                    {
                        height[k] = yBottom;
                        normal[k] = Vector3.up;
                        albedo[k] = Color.black;
                    }
                }
            RSAlbedo.EndScan();

            ComputeSkyVisibility();
            EnsureTextures();

            // 높이 텍스처 (바닥 높이 — 셰이더가 '바닥에서 얼마나 위인지' · '위가 막혔는지' 를 잰다)
            var hp = new Color[n];
            for (int k = 0; k < n; k++) hp[k] = new Color(height[k], 0f, 0f, 0f);
            hTex.SetPixels(hp);
            hTex.Apply(false);

            LastGeometryMs = (float)sw.Elapsed.TotalMilliseconds;
            combineDirty = true;
        }

        void ComputeSkyVisibility()
        {
            int steps = Mathf.Clamp(Mathf.CeilToInt(L.occlusionReach / cs), 1, 40);
            var dirs = new Vector2[8];
            for (int d = 0; d < 8; d++) { float a = d * Mathf.PI / 4f; dirs[d] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }

            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i;
                    if (!valid[k]) { vis[k] = 1f; continue; }
                    float h0 = height[k], occ = 0f;
                    for (int d = 0; d < 8; d++)
                    {
                        float maxSin = 0f;
                        for (int s = 1; s <= steps; s++)
                        {
                            int ii = i + Mathf.RoundToInt(dirs[d].x * s), jj = j + Mathf.RoundToInt(dirs[d].y * s);
                            if (ii < 0 || jj < 0 || ii >= nx || jj >= nz) break;
                            int q = jj * nx + ii;
                            if (!valid[q]) continue;
                            float dh = height[q] - h0;
                            if (dh <= 0.05f) continue;
                            float dist = s * cs * dirs[d].magnitude;
                            float tanA = dh / dist;
                            float sinA = tanA / Mathf.Sqrt(1f + tanA * tanA);
                            if (sinA > maxSin) maxSin = sinA;
                        }
                        occ += maxSin;
                    }
                    vis[k] = Mathf.Clamp01(1f - occ / 8f);
                }
        }

        // ─────────────────────────────────────────────────────────────
        // 2) 햇빛이 튀는 빛 (여러 프레임에 나눠 계산)
        // ─────────────────────────────────────────────────────────────

        Light SunLight => sun != null ? sun : RenderSettings.sun;

        bool SunChanged()
        {
            var l = SunLight;
            if (l == null) return lastSunIntensity != 0f && (lastSunIntensity = 0f) == 0f;
            float inten = l.isActiveAndEnabled ? l.intensity : 0f;
            if (Vector3.Angle(l.transform.forward, lastSunDir) > sunChangeDegrees) return true;
            if (Mathf.Abs(inten - lastSunIntensity) > 0.05f * Mathf.Max(0.2f, lastSunIntensity)) return true;
            Color c = l.color;
            return Mathf.Abs(c.r - lastSunColor.r) + Mathf.Abs(c.g - lastSunColor.g) + Mathf.Abs(c.b - lastSunColor.b) > 0.06f;
        }

        void StartSunJob()
        {
            var l = SunLight;
            lastSunDir = l != null ? l.transform.forward : Vector3.down;
            lastSunColor = l != null ? l.color : Color.black;
            lastSunIntensity = l != null && l.isActiveAndEnabled ? l.intensity : 0f;
            jobL = -lastSunDir;
            jobColor = lastSunColor.linear * lastSunIntensity;
            if (jobL.y < 0.02f) jobColor = Color.black;   // 해가 지평선 아래
            if (sunRad == null || sunRad.Length != nx * nz) sunRad = new Color[nx * nz];
            sunJob = 0;
            sunDirty = false;
            sunTimer = System.Diagnostics.Stopwatch.StartNew();
        }

        System.Diagnostics.Stopwatch sunTimer;

        void RunSunJob(int budget)
        {
            int n = nx * nz;
            bool anyLight = jobColor.maxColorComponent > 0.0001f;
            int end = (int)Mathf.Min(n, (long)sunJob + budget);
            for (int k = sunJob; k < end; k++)
            {
                if (!valid[k] || !anyLight) { sunRad[k] = Color.clear; continue; }
                Vector3 nrm = normal[k];
                float cos = Vector3.Dot(nrm, jobL);
                if (cos <= 0f) { sunRad[k] = Color.clear; continue; }
                int i = k % nx, j = k / nx;
                var p = new Vector3(origin.x + (i + 0.5f) * cs, height[k], origin.y + (j + 0.5f) * cs) + nrm * 0.05f;
                if (Blocked(p, jobL, 80f)) { sunRad[k] = Color.clear; continue; }
                Color c = albedo[k] * jobColor * cos;
                sunRad[k] = c;
            }
            sunJob = end;
            if (sunJob < n) return;

            // 옆으로 번지기 (가우시안 두 번)
            BlurInto(sunRad, sunBounce, Mathf.Max(1, Mathf.RoundToInt(L.bounceReach / cs)));
            // 색 진하기
            float lumW = 0f; Color sum = Color.clear;
            for (int k = 0; k < n; k++)
            {
                Color c = sunBounce[k];
                float lum = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                c = new Color(lum + (c.r - lum) * L.saturation, lum + (c.g - lum) * L.saturation, lum + (c.b - lum) * L.saturation, 0f);
                c.r = Mathf.Max(0f, c.r); c.g = Mathf.Max(0f, c.g); c.b = Mathf.Max(0f, c.b);
                sunBounce[k] = c;
                if (valid[k]) { sum += c; lumW += 1f; }
            }
            outside = lumW > 0f ? sum / lumW : Color.clear;
            sunJob = -1;
            combineDirty = true;
            LastSunMs = sunTimer != null ? (float)sunTimer.Elapsed.TotalMilliseconds : 0f;
        }

        void BlurInto(Color[] src, Color[] dst, int r)
        {
            float sigma = Mathf.Max(0.5f, r * 0.5f);
            var w = new float[r * 2 + 1];
            float ws = 0f;
            for (int t = -r; t <= r; t++) { w[t + r] = Mathf.Exp(-(t * t) / (2f * sigma * sigma)); ws += w[t + r]; }
            for (int t = 0; t < w.Length; t++) w[t] /= ws;

            var tmp = new Color[src.Length];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    Color acc = Color.clear;
                    for (int t = -r; t <= r; t++)
                    {
                        int ii = i + t;
                        if (ii < 0 || ii >= nx) continue;
                        acc += src[j * nx + ii] * w[t + r];
                    }
                    tmp[j * nx + i] = acc;
                }
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    Color acc = Color.clear;
                    for (int t = -r; t <= r; t++)
                    {
                        int jj = j + t;
                        if (jj < 0 || jj >= nz) continue;
                        acc += tmp[jj * nx + i] * w[t + r];
                    }
                    dst[j * nx + i] = acc;
                }
        }

        // ─────────────────────────────────────────────────────────────
        // 3) 색 번짐 요소 · 라이트
        // ─────────────────────────────────────────────────────────────

        static readonly List<(Vector3 pos, Color col, float radius)> spill = new List<(Vector3, Color, float)>();
        Light[] lightCache;
        float lightCacheTime = -10f;

        void CollectSpill()
        {
            spill.Clear();
            if (L.includeSources)
                foreach (var s in RSBounceSource.All)
                {
                    if (s == null || !s.isActiveAndEnabled) continue;
                    Color c = s.EffectiveColor;
                    if (c.maxColorComponent <= 0.001f) continue;
                    spill.Add((s.transform.position, c, s.radius));
                }
            if (L.includeLights && L.lightSpill > 0f)
            {
                if (lightCache == null || Now - lightCacheTime > 1f)
                {
                    lightCache = FindObjectsByType<Light>(FindObjectsSortMode.None);
                    lightCacheTime = Now;
                }
                foreach (var l in lightCache)
                {
                    if (l == null || !l.isActiveAndEnabled || l.intensity <= 0f) continue;
                    if (l.type != LightType.Point && l.type != LightType.Spot) continue;
                    if (l.GetComponent<RSBounceSource>() != null) continue;   // 요소가 따로 있으면 그걸로
                    Color c = l.color.linear * l.intensity * L.lightSpill * 0.25f;
                    spill.Add((l.transform.position, c, Mathf.Max(0.5f, l.range * 0.8f)));
                }
            }
        }

        bool SourcesChanged(bool commit)
        {
            if (valid == null) return false;
            CollectSpill();
            float sig = 17f;
            foreach (var s in spill)
                sig = sig * 1.0001f + s.pos.x * 3.1f + s.pos.y * 5.3f + s.pos.z * 7.7f + s.col.r * 11f + s.col.g * 13f + s.col.b * 17f + s.radius * 19f;
            sig += spill.Count * 101f;
            bool changed = !Mathf.Approximately(sig, sourcesSig) || float.IsNaN(sourcesSig);
            if (commit) sourcesSig = sig;
            return changed;
        }

        void Combine(bool instant)
        {
            int n = nx * nz;
            System.Array.Clear(extra, 0, n);
            foreach (var s in spill)
            {
                int i0 = Mathf.Max(0, Mathf.FloorToInt((s.pos.x - s.radius - origin.x) / cs));
                int i1 = Mathf.Min(nx - 1, Mathf.CeilToInt((s.pos.x + s.radius - origin.x) / cs));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((s.pos.z - s.radius - origin.y) / cs));
                int j1 = Mathf.Min(nz - 1, Mathf.CeilToInt((s.pos.z + s.radius - origin.y) / cs));
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        int k = j * nx + i;
                        float dx = origin.x + (i + 0.5f) * cs - s.pos.x, dz = origin.y + (j + 0.5f) * cs - s.pos.z;
                        float dy = Mathf.Max(0f, s.pos.y - height[k]);
                        float d = Mathf.Sqrt(dx * dx + dz * dz + dy * dy * 0.5f) / s.radius;
                        if (d >= 1f) continue;
                        float f = (1f - d) * (1f - d);
                        extra[k] += s.col * f;
                    }
            }
            for (int k = 0; k < n; k++)
            {
                Color c = sunBounce[k] + extra[k];
                c.a = vis[k];
                target[k] = c;
            }
            combineDirty = false;
            if (instant) { System.Array.Copy(target, current, n); blending = false; Upload(); }
            else blending = true;
        }

        // ─────────────────────────────────────────────────────────────
        // 텍스처 · 셰이더 값
        // ─────────────────────────────────────────────────────────────

        void EnsureTextures()
        {
            if (tex == null || tex.width != nx || tex.height != nz)
            {
                Kill(tex);
                tex = new Texture2D(nx, nz, TextureFormat.RGBAHalf, false, true)
                { name = "RS_Indirect", hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            }
            if (hTex == null || hTex.width != nx || hTex.height != nz)
            {
                Kill(hTex);
                var fmt = SystemInfo.SupportsTextureFormat(TextureFormat.RFloat) ? TextureFormat.RFloat : TextureFormat.RHalf;
                hTex = new Texture2D(nx, nz, fmt, false, true)
                { name = "RS_IndirectHeight", hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            }
        }

        void Upload()
        {
            if (tex == null || current == null) return;
            tex.SetPixels(current);
            tex.Apply(false);
#if UNITY_EDITOR
            UpdatePreviews();
#endif
        }

        void ApplyGlobals()
        {
            if (tex == null || hTex == null || validCount == 0) { Shader.SetGlobalVector(IdParams, Vector4.zero); return; }
            Shader.SetGlobalTexture(IdTex, tex);
            Shader.SetGlobalTexture(IdHeight, hTex);
            Shader.SetGlobalVector(IdRect, new Vector4(origin.x, origin.y, 1f / (nx * cs), 1f / (nz * cs)));
            Shader.SetGlobalVector(IdParams, new Vector4(1f, L.occlusion, L.bounce, L.bounceHeight));
            Shader.SetGlobalVector(IdReceive, new Vector4(L.terrain, L.buildings, L.characters, L.foliage));
            // w = 가장자리 페이드 (uv 단위 역수) — 가장자리 3칸에 걸쳐 바깥 값으로
            Shader.SetGlobalVector(IdOutside, new Vector4(outside.r, outside.g, outside.b, Mathf.Min(nx, nz) / 3f));
            Shader.SetGlobalVector(IdExtra, new Vector4(L.underCover, 1f / nx, 1f / nz, 0f));
        }

#if UNITY_EDITOR
        void UpdatePreviews()
        {
            if (PreviewVis == null || PreviewVis.width != nx || PreviewVis.height != nz)
            {
                Kill(PreviewVis); Kill(PreviewBounce);
                PreviewVis = new Texture2D(nx, nz, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Point };
                PreviewBounce = new Texture2D(nx, nz, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Point };
            }
            float maxB = 0.0001f;
            for (int k = 0; k < current.Length; k++) maxB = Mathf.Max(maxB, current[k].maxColorComponent);
            var pv = new Color32[current.Length];
            var pb = new Color32[current.Length];
            for (int k = 0; k < current.Length; k++)
            {
                byte v = (byte)(Mathf.Clamp01(current[k].a) * 255f);
                pv[k] = valid[k] ? new Color32(v, v, v, 255) : new Color32(60, 20, 20, 255);
                Color b = current[k] / maxB;
                pb[k] = new Color(Mathf.Sqrt(Mathf.Clamp01(b.r)), Mathf.Sqrt(Mathf.Clamp01(b.g)), Mathf.Sqrt(Mathf.Clamp01(b.b)), 1f);
            }
            PreviewVis.SetPixels32(pv); PreviewVis.Apply(false);
            PreviewBounce.SetPixels32(pb); PreviewBounce.Apply(false);
        }
#endif

        void OnDrawGizmosSelected()
        {
            if (nx == 0) return;
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.6f);
            var a = Area;
            Gizmos.DrawWireCube(new Vector3(a.center.x, yBottom + 5f, a.center.z), new Vector3(a.size.x, 0.1f, a.size.z));
        }
    }

    /// <summary>레이가 맞은 곳의 바닥 색 (머티리얼 · 칠 레이어 · 텍스처 평균색). 선형 색공간</summary>
    static class RSAlbedo
    {
        static readonly Dictionary<Texture, Color> avg = new Dictionary<Texture, Color>();
        static readonly Dictionary<Mesh, (Color[] col, List<Vector2> uv2, int[] tri)> meshes = new Dictionary<Mesh, (Color[], List<Vector2>, int[])>();
        static readonly Color Gray = new Color(0.35f, 0.35f, 0.35f, 1f);

        public static void BeginScan() { meshes.Clear(); }
        public static void EndScan() { meshes.Clear(); }

        public static Color At(RaycastHit hit)
        {
            var col = hit.collider;
            var r = col.GetComponent<Renderer>();
            if (r == null) r = col.GetComponentInParent<Renderer>();
            if (r == null) return Gray;
            var mc = col as MeshCollider;
            Material m = MaterialAt(r, mc, hit.triangleIndex);
            if (m == null) return Gray;

            if (m.HasProperty("_LayerR") && mc != null && mc.sharedMesh != null && mc.sharedMesh.isReadable && hit.triangleIndex >= 0)
                return Terrain(m, mc.sharedMesh, hit);
            if (m.HasProperty("_TopMap"))
            {
                bool top = hit.normal.y > 0.5f;
                return Avg(m.GetTexture(top ? "_TopMap" : "_SideMap")) * Tint(m, top ? "_TopColor" : "_SideColor");
            }
            if (m.HasProperty("_BaseMap")) return Avg(m.GetTexture("_BaseMap")) * Tint(m, "_BaseColor");
            if (m.HasProperty("_MainTex")) return Avg(m.GetTexture("_MainTex")) * Tint(m, "_Color");
            return Tint(m, "_BaseColor");
        }

        static Color Tint(Material m, string prop)
        {
            if (!m.HasProperty(prop)) return Color.white;
            Color c = m.GetColor(prop).linear;
            c.a = 1f;
            return c;
        }

        static Material MaterialAt(Renderer r, MeshCollider mc, int tri)
        {
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) return null;
            if (mats.Length == 1 || mc == null || mc.sharedMesh == null || tri < 0) return mats[0];
            var mesh = mc.sharedMesh;
            int idx = tri * 3;
            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                var d = mesh.GetSubMesh(s);
                if (idx >= d.indexStart && idx < d.indexStart + d.indexCount) return mats[s];
            }
            return mats[0];
        }

        static Color Terrain(Material m, Mesh mesh, RaycastHit hit)
        {
            if (!meshes.TryGetValue(mesh, out var data))
            {
                var uv2 = new List<Vector2>();
                mesh.GetUVs(2, uv2);
                data = (mesh.colors, uv2, mesh.triangles);
                meshes[mesh] = data;
            }
            int t = hit.triangleIndex * 3;
            if (data.tri == null || t + 2 >= data.tri.Length || data.col == null || data.col.Length == 0) return Gray;
            int a = data.tri[t], b = data.tri[t + 1], c = data.tri[t + 2];
            Vector3 bc = hit.barycentricCoordinate;
            Color w = data.col[a] * bc.x + data.col[b] * bc.y + data.col[c] * bc.z;
            float g = 0f;
            if (data.uv2 != null && data.uv2.Count > Mathf.Max(a, Mathf.Max(b, c)))
                g = data.uv2[a].x * bc.x + data.uv2[b].x * bc.y + data.uv2[c].x * bc.z;

            float cliffY = m.HasProperty("_CliffY") ? m.GetFloat("_CliffY") : 0.6f;
            bool cliff = hit.normal.y < cliffY;
            Color baseC = cliff ? Avg(m.GetTexture("_BaseSide")) * Tint(m, "_BaseSideColor")
                                : Avg(m.GetTexture("_BaseTop")) * Tint(m, "_BaseTopColor");
            float sum = w.r + w.g + w.b + w.a + g;
            float wb = Mathf.Max(0f, 1f - sum);
            Color acc = baseC * wb
                + Layer(m, "_LayerR", "_ColorR") * w.r + Layer(m, "_LayerG", "_ColorG") * w.g
                + Layer(m, "_LayerB", "_ColorB") * w.b + Layer(m, "_LayerA", "_ColorA") * w.a
                + (cliff ? baseC : Layer(m, "_LayerGrass", "_ColorGrass")) * g;
            float tot = wb + sum;
            acc = tot > 1e-4f ? acc / tot : baseC;
            acc.a = 1f;
            return acc;
        }

        static Color Layer(Material m, string tex, string tint)
        {
            return m.HasProperty(tex) ? Avg(m.GetTexture(tex)) * Tint(m, tint) : Gray;
        }

        /// <summary>텍스처 평균색 (GPU 로 줄여서 읽는다 — 읽기 권한 없는 텍스처도 된다). 투명 픽셀은 뺀다</summary>
        public static Color Avg(Texture t)
        {
            if (t == null) return Color.white;
            if (avg.TryGetValue(t, out var c)) return c;
            const int S = 64;
            var rt = RenderTexture.GetTemporary(S, S, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            Graphics.Blit(t, rt);
            RenderTexture.active = rt;
            var tmp = new Texture2D(S, S, TextureFormat.RGBA32, false, true);
            tmp.ReadPixels(new Rect(0, 0, S, S), 0, 0);
            tmp.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = tmp.GetPixels32();
            if (Application.isPlaying) Object.Destroy(tmp); else Object.DestroyImmediate(tmp);
            double r = 0, g = 0, b = 0, wsum = 0;
            foreach (var p in px)
            {
                double w = p.a / 255.0;
                r += p.r / 255.0 * w; g += p.g / 255.0 * w; b += p.b / 255.0 * w; wsum += w;
            }
            c = wsum > 0.001 ? new Color((float)(r / wsum), (float)(g / wsum), (float)(b / wsum), 1f) : Color.gray;
            avg[t] = c;
            return c;
        }

        public static void ClearCache() { avg.Clear(); meshes.Clear(); }
    }
}
