// RE:AL STEEL - 구름 그림자 (흘러가는 구름 무늬로 햇빛을 가린다)
//
// 태양(Directional Light)에 붙인다. 구름 무늬 텍스처를 만들어 라이트 쿠키로 씌우고, 바람 방향으로 흘린다.
// 쿠키는 URP 가 조명 계산에서 곱해 주므로 지형 · 로봇 · 소품(URP Lit, RS 셰이더) 모두 같이 가려진다.
// 텍스처는 실행 중에 만들어 쓰고 저장하지 않는다.
//
// 필요 조건: URP Asset 의 Light Cookies 지원 (기본 켜짐). RS 셰이더 3종은 _LIGHT_COOKIES 키워드를 받도록 수정됨.
using UnityEngine;
using UnityEngine.Rendering.Universal;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("구름 그림자", "태양(Directional Light)에 구름 무늬를 씌워 바람 방향으로 흘린다. 큰 구름 + 잔 얼룩(나뭇잎 그늘) 두 겹을 곱한다. 지형 · 로봇 · 소품이 모두 같이 가려진다.\n· 무늬 한 장(Cloud Size)이 반복된다\n· 밤엔 시간대가 자동으로 옅게 만든다\n· 안 보이면 URP Asset → Lighting → Light Cookies 확인")]
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Light))]
    [AddComponentMenu("RE_AL STEEL/Lighting/구름 그림자")]
    public partial class RSCloudShadow : MonoBehaviour, IRSEditorAnimated
    {
        // ── 룩 (스테이지 룩 프로필로 옮길 수 있는 값) ──
        [System.Serializable]
        public class Look
        {
            [RSGroup("구름")]
            [RSHelp("큰 구름: 하늘을 지나가는 구름 덩어리의 그늘.")]
            [Range(0f, 1f), Tooltip("구름 그늘 진하기 (0 = 안 보임, 1 = 햇빛 완전히 가림). 0.4 ~ 0.6")]
            [RSKey]
            public float strength = 0.55f;
            [Range(0f, 1f), Tooltip("하늘을 덮는 구름 양 (실제 그늘 면적 비율)")]
            [RSKey]
            public float coverage = 0.45f;
            [Range(0f, 1f), Tooltip("구름 가장자리 부드러움 (0 = 칼같이, 1 = 흐릿)")]
            public float softness = 0.35f;
            [Range(0, 6), Tooltip("그늘 단계 (0 = 부드럽게, 2 ~ 4 = 픽셀아트식 계단). 두 겹 모두에 적용")]
            public int bands = 0;
            [Range(1, 5), Tooltip("구름 모양의 잘게 부서짐 (노이즈 겹 수)")]
            public int detail = 4;
            [Range(1, 8), Tooltip("무늬 한 장 안의 구름 덩어리 수 (클수록 작은 구름이 많이)")]
            public int blobs = 3;
            [Tooltip("무작위 시드. 바꾸면 구름 · 얼룩 모양이 새로")]
            public int seed = 7;
            [RSGroup("나뭇잎 그늘 (얼룩진 햇빛)")]
            [RSHelp("나뭇잎 그늘: 나무 사이로 새어 드는 햇빛처럼 잘게 얼룩진 그늘 (옥토패스 낮 바닥의 얼룩).")]
            [Range(0f, 1f), Tooltip("얼룩 그늘 진하기 (0 = 끔)")]
            public float dappleStrength = 0.5f;
            [Range(0f, 1f), Tooltip("얼룩 그늘이 덮는 양")]
            public float dappleCoverage = 0.45f;
            [Range(0f, 1f), Tooltip("얼룩 가장자리 부드러움")]
            public float dappleSoftness = 0.25f;
            [Range(2, 24), Tooltip("무늬 한 장 안의 얼룩 수 (클수록 잘다)")]
            public int dappleSize = 9;
            [Range(1, 4), Tooltip("얼룩 모양의 잘게 부서짐")]
            public int dappleDetail = 3;
            [RSGroup("크기 · 바람")]
            [RSHelp("무늬 크기와 흐름. 무늬 한 장이 이 크기로 반복된다.")]
            [Tooltip("구름 무늬 한 장이 덮는 크기 (m). 전투 구역보다 크게 (30 ~ 60)")]
            [RSKey]
            public float cloudSize = 40f;
            [Tooltip("바람 (m/s, X · Z). 구름 그림자가 이 방향 · 속도로 흐른다. 0.3 ~ 1")]
            public Vector2 wind = new Vector2(0.6f, 0.25f);
            [RSGroup("픽셀")]
            [RSHelp("그늘 경계의 픽셀 크기. 지형 칸(0.25m)보다 작게 두면 자연스럽고, 크게 두면 굵은 픽셀아트 계단이 된다.")]
            [Range(0.03f, 1f), Tooltip("구름 그늘 한 픽셀의 크기 (m). 0.1 권장. 작을수록 곱고 텍스처 생성이 조금 무거워진다")]
            public float pixelSize = 0.1f;
            [Tooltip("픽셀 경계를 또렷하게 (끄면 부드럽게 번짐)")]
            public bool pixelated = true;
        }

        [SerializeField, RSLook] Look look = new Look();

        /// <summary>지금 쓰는 룩 값 — 스테이지 룩 프로필이 있으면 그 값, 없으면 이 컴포넌트 값</summary>
        public Look L { get { var p = RSStageLook.Current; return p != null ? p.clouds : look; } }
        /// <summary>이 컴포넌트에 저장된 값 (프로필이 없을 때 쓰는 값)</summary>
        public Look LocalLook { get { return look; } }
        /// <summary>스테이지 룩 프로필 값을 쓰고 있는지</summary>
        public bool UsesStageLook { get { return RSStageLook.Current != null; } }





        [RSGroup("에디터")]
        [Tooltip("플레이하지 않아도 에디터에서 구름을 흘려 본다")]
        public bool animateInEditMode = true;

        /// <summary>시간대(RSTimeOfDay)가 넣는 배율. 밤에는 옅어진다</summary>
        [System.NonSerialized] public float strengthScale = 1f;

        Light lt;
        Texture2D tex;
        int builtKey;
        float clock;

        public bool WantsEditorAnimation { get { return isActiveAndEnabled && animateInEditMode; } }

        void OnEnable()
        {

            MigrateLegacy();

            RSStageLook.Changed += OnStageLookChanged;
            lt = GetComponent<Light>();
            builtKey = 0;
            RSLightingClock.Register(this);
            Apply();
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

            OnValidate(); Apply();

        }


        void OnDisable()
        {

            RSStageLook.Changed -= OnStageLookChanged;
            RSLightingClock.Unregister(this);
            if (lt != null && lt.cookie == tex) lt.cookie = null;
            if (tex != null)
            {
                if (Application.isPlaying) Destroy(tex); else DestroyImmediate(tex);
                tex = null;
            }
        }

        void OnValidate()
        {

            MigrateLegacy();
            L.cloudSize = Mathf.Max(1f, L.cloudSize);
            builtKey = 0;
        }

        void Update()
        {
            if (Application.isPlaying) clock += Time.deltaTime;
            Apply();
        }

        public void EditorTick(float dt)
        {
            clock += dt;
            Apply();
        }

        /// <summary>옥토패스 참고 권장값으로</summary>
        public void ResetLook()
        {
            L.strength = 0.55f; L.coverage = 0.45f; L.softness = 0.35f; L.bands = 0; L.detail = 4; L.blobs = 3;
            L.dappleStrength = 0.5f; L.dappleCoverage = 0.45f; L.dappleSoftness = 0.25f; L.dappleSize = 9; L.dappleDetail = 3;
            L.cloudSize = 40f; L.wind = new Vector2(0.6f, 0.25f); L.pixelSize = 0.1f; L.pixelated = true;
            builtKey = 0;
            Apply();
        }

        public void Apply()
        {
            if (lt == null) lt = GetComponent<Light>();
            if (lt == null) return;

            EnsureTexture();
            if (lt.cookie != tex) lt.cookie = tex;

            var ad = lt.GetUniversalAdditionalLightData();
            if (ad == null) return;
            var size = new Vector2(L.cloudSize, L.cloudSize);
            if (ad.lightCookieSize != size) ad.lightCookieSize = size;
            ad.lightCookieOffset = new Vector2(Mathf.Repeat(L.wind.x * clock, L.cloudSize),
                                               Mathf.Repeat(L.wind.y * clock, L.cloudSize));
        }

        // ─────────────────────────────────────────────────────────────
        // 구름 무늬 텍스처 (이음매 없이 반복)
        // ─────────────────────────────────────────────────────────────

        // 모양(노이즈)은 무거우니 따로 기억해 두고, 진하기만 바뀌면 합성만 다시 한다
        int shapeKey;
        float[] cloudCache, dappleCache;

        void EnsureTexture()
        {
            float s = Mathf.Round(L.strength * Mathf.Clamp01(strengthScale) * 50f) / 50f;   // 0.02 단위로만 다시 만든다
            float ds = Mathf.Round(L.dappleStrength * Mathf.Clamp01(strengthScale) * 50f) / 50f;
            int cells = Mathf.Clamp(Mathf.RoundToInt(L.cloudSize / Mathf.Max(0.03f, L.pixelSize)), 16, 512);

            int sk = 17, key = 17;
            unchecked
            {
                sk = sk * 31 + cells;
                sk = sk * 31 + Mathf.RoundToInt(L.coverage * 1000f);
                sk = sk * 31 + Mathf.RoundToInt(L.softness * 1000f);
                sk = sk * 31 + L.detail;
                sk = sk * 31 + L.blobs;
                sk = sk * 31 + L.seed;
                sk = sk * 31 + Mathf.RoundToInt(L.dappleCoverage * 1000f);
                sk = sk * 31 + Mathf.RoundToInt(L.dappleSoftness * 1000f);
                sk = sk * 31 + L.dappleSize * 7 + L.dappleDetail;
                sk = sk * 31 + (ds > 0f ? 1 : 0);
                if (sk == 0) sk = 1;
                key = sk * 31 + Mathf.RoundToInt(s * 1000f);
                key = key * 31 + Mathf.RoundToInt(ds * 1000f);
                key = key * 31 + L.bands;
                key = key * 31 + (L.pixelated ? 1 : 0);
                if (key == 0) key = 1;
            }
            if (tex != null && key == builtKey) return;
            builtKey = key;

            // 픽셀 모드: 칸 하나를 2~4 배로 키워서 넣는다 → 쿠키 아틀라스의 쌍선형 필터에도 칸 경계가 또렷하게 남는다
            int up = L.pixelated ? (cells <= 256 ? 4 : 2) : 1;
            int size = cells * up;

            if (tex == null || tex.width != size)
            {
                if (tex != null) { if (Application.isPlaying) Destroy(tex); else DestroyImmediate(tex); }
                tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
                {
                    name = "RS_CloudCookie",
                    hideFlags = HideFlags.DontSave,
                    wrapMode = TextureWrapMode.Repeat,
                };
            }
            tex.filterMode = L.pixelated ? FilterMode.Point : FilterMode.Bilinear;

            // 큰 구름 + 잔 얼룩(나뭇잎 그늘) 두 겹을 곱한다
            if (sk != shapeKey || cloudCache == null)
            {
                shapeKey = sk;
                cloudCache = Layer(cells, L.blobs, L.detail, L.seed, L.coverage, L.softness);
                dappleCache = ds > 0f ? Layer(cells, L.dappleSize, L.dappleDetail, L.seed * 7 + 999, L.dappleCoverage, L.dappleSoftness) : null;
            }
            var cloud = cloudCache;
            var dapple = ds > 0f ? dappleCache : null;

            var cellVal = new float[cells * cells];
            for (int i = 0; i < cellVal.Length; i++)
            {
                float c = cloud[i], d = dapple != null ? dapple[i] : 0f;
                if (L.bands > 0) { c = Mathf.Round(c * L.bands) / L.bands; d = Mathf.Round(d * L.bands) / L.bands; }
                cellVal[i] = (1f - c * s) * (1f - d * ds);
            }

            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(cellVal[(y / up) * cells + (x / up)]) * 255f);
                    px[y * size + x] = new Color32(b, b, b, 255);
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
        }

        /// <summary>한 겹: 0 = 햇빛, 1 = 그늘. 양(coverage)은 실제 면적 비율로 맞춘다</summary>
        static float[] Layer(int cells, int blobCount, int octaves, int sd, float cov, float soft)
        {
            var val = new float[cells * cells];
            for (int y = 0; y < cells; y++)
                for (int x = 0; x < cells; x++)
                    val[y * cells + x] = Fbm((x + 0.5f) / cells, (y + 0.5f) / cells, blobCount, octaves, sd);

            var sorted = (float[])val.Clone();
            System.Array.Sort(sorted);
            int n = sorted.Length - 1;
            float th = sorted[Mathf.Clamp(Mathf.RoundToInt((1f - cov) * n), 0, n)];
            float spread = Mathf.Max(0.02f, sorted[Mathf.RoundToInt(0.9f * n)] - sorted[Mathf.RoundToInt(0.1f * n)]);
            float sw = Mathf.Max(0.002f, soft * 0.35f * spread);
            if (cov <= 0.001f) th = 2f;
            if (cov >= 0.999f) th = -1f;
            for (int i = 0; i < val.Length; i++) val[i] = SmoothStep(th - sw, th + sw, val[i]);
            return val;
        }

        static float Fbm(float u, float v, int blobCount, int octaves, int sd)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int period = Mathf.Max(1, blobCount);
            for (int o = 0; o < Mathf.Clamp(octaves, 1, 5); o++)
            {
                sum += amp * PeriodicNoise(u * period, v * period, period, sd + o * 131);
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            float n = sum / norm;
            return Mathf.Clamp01((n - 0.5f) * 1.9f + 0.5f);   // 값 노이즈는 가운데로 몰리므로 대비를 늘린다
        }

        static float PeriodicNoise(float x, float y, int period, int s)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int ax = Mod(x0, period), bx = Mod(x0 + 1, period);
            int ay = Mod(y0, period), by = Mod(y0 + 1, period);
            float a = Hash(ax, ay, s), b = Hash(bx, ay, s), c = Hash(ax, by, s), d = Hash(bx, by, s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
