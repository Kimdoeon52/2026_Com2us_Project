// RE:AL STEEL - 원형 그림자 (발밑 블롭 그림자)
//
// 캐릭터에 붙인다. 발밑에 동그란 그림자를 깐다 (옥토패스식 — 스프라이트는 진짜 그림자를 안 드리우니까).
//
// · 바닥에 '붙는' 작은 격자 메시다. 매 프레임 격자 점마다 땅 높이를 재서(RS 지형 높이 + 콜라이더 레이)
//   비탈 · 계단 · 상자 위에서도 땅 모양을 따라 휜다. 그래서 물 반사 속에도 그대로 비친다
// · 턱 아래로 떨어지는 점(높이 차 > '턱 높이')은 투명하게 → 절벽 옆면에 그림자가 커튼처럼 걸리지 않는다
// · 떠 있으면(턱에서 떨어질 때 등) 그림자는 땅에 남은 채 작아지고 옅어진다
// · 색은 '곱하기'로 얹는다 → 바닥 색이 살아 있는 채 어두워진다. 안개(Unity Fog)도 받는다
// · 픽셀 맞추기: 그림자 가장자리를 월드 격자(기본 1m = 32픽셀, 텍스처 밀도와 같음)에 맞춰 도트처럼 끊는다
// · 편집 중에도 보인다. 숨은 오브젝트 하나를 만들고, 컴포넌트를 끄면 지운다 (씬에 저장 안 됨)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;
using RealSteel.Terrain;

[RSSummary("원형 그림자",
    "발밑에 동그란 그림자를 깐다. 땅 모양(비탈 · 계단 · 상자)을 따라 휘고, 떠 있으면(떨어질 때) 작아지며 옅어진다.\n" +
    "· 땅 높이는 RS 지형 + 콜라이더로 잰다. 콜라이더 없는 장식 메시 위에는 안 올라간다\n" +
    "· 색은 곱하기라 바닥 색이 살아 있다. '픽셀 맞추기'를 켜면 가장자리가 도트로 끊긴다 (1m = 32픽셀)\n" +
    "· 편집 중에도 보인다 (숨은 오브젝트, 씬에 저장 안 됨)")]
[ExecuteAlways, DisallowMultipleComponent]
[AddComponentMenu("RE_AL STEEL/Character/원형 그림자")]
public class RSBlobShadow : MonoBehaviour
{
    [RSGroup("모양")]
    [Tooltip("그림자 반지름 (m, 월드 기준 — 위에서 내려다보면 자연히 타원으로 보인다)")]
    [RSKey]
    public float radius = 0.32f;
    [Range(0.5f, 2f), Tooltip("좌우(카메라 기준) 늘이기. 1 = 정원")]
    public float widthScale = 1.15f;
    [Range(0f, 1f), Tooltip("진하기")]
    [RSKey]
    public float strength = 0.55f;
    [Tooltip("그림자 색 (곱하기). 완전 검정보다 살짝 푸른 보라가 옥토패스 느낌")]
    public Color color = new Color(0.12f, 0.10f, 0.20f, 1f);
    [Range(0.01f, 1f), Tooltip("가장자리 부드러움 (반지름에 대한 비율). 작을수록 딱 떨어진다")]
    public float softness = 0.45f;
    [Range(0, 8), Tooltip("진하기 단계 수 (0 = 부드럽게 이어짐). 2 ~ 3 이면 픽셀아트식 계단")]
    public int bands = 3;
    [Tooltip("가장자리를 월드 픽셀 격자에 맞춰 도트처럼 끊는다")]
    public bool pixelSnap = true;
    [Tooltip("픽셀 격자 밀도 (1m 당 픽셀). 텍스처 밀도와 맞춘다")]
    public float pixelsPerMeter = 32f;
    [Tooltip("그림자 중심 오프셋 (캐릭터 기준 로컬 x · z, m). 발이 그림 가운데가 아니면 맞춘다")]
    public Vector2 offset = Vector2.zero;

    [RSGroup("떠 있을 때")]
    [Tooltip("이 높이(m)만큼 뜨면 가장 작고 옅어진다")]
    public float fadeHeight = 2.5f;
    [Range(0f, 1f), Tooltip("가장 높이 떴을 때 크기 비율")]
    public float minScale = 0.55f;
    [Range(0f, 1f), Tooltip("가장 높이 떴을 때 진하기 비율")]
    public float minStrength = 0.35f;

    [RSGroup("땅 따라가기")]
    [Range(3, 11), Tooltip("격자 한 변의 점 수. 클수록 울퉁불퉁한 땅을 잘 따른다 (7 = 49점)")]
    public int resolution = 7;
    [Tooltip("이보다 높이 차가 큰 점(턱 아래 · 위)은 그림자를 지운다 (m)")]
    public float maxStep = 0.45f;
    [Tooltip("발보다 이만큼 위에서 아래로 땅을 찾는다 (m). 계단 한 칸보다 크고, 탁자 높이보다 작게")]
    public float probeUp = 0.5f;
    [Tooltip("땅을 찾을 거리 (m, 아래로)")]
    public float probeDown = 6f;
    [Tooltip("땅에서 띄우는 높이 (m). 땅에 묻혀 깜빡이면 조금 올린다")]
    public float lift = 0.02f;
    [Tooltip("땅으로 칠 레이어")]
    public LayerMask groundMask = ~0;

    [SerializeField, HideInInspector] Shader shader;   // 빌드에 셰이더가 빠지지 않게 참조해 둔다

    // ── 내부 ──
    const string ShaderName = "RE_AL STEEL/Blob Shadow";
    static readonly int idCenter = Shader.PropertyToID("_BlobCenter");
    static readonly int idParams = Shader.PropertyToID("_BlobParams");
    static readonly int idParams2 = Shader.PropertyToID("_BlobParams2");
    static readonly int idColor = Shader.PropertyToID("_BlobColor");
    static readonly int idAxis = Shader.PropertyToID("_BlobAxis");

    GameObject holder;
    MeshFilter mf;
    MeshRenderer mr;
    Mesh mesh;
    Material mat;
    MaterialPropertyBlock mpb;
    int builtRes = -1;
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Color> cols = new List<Color>();
    readonly RaycastHit[] hits = new RaycastHit[8];

    /// <summary>지금 캐릭터가 땅에서 떠 있는 높이 (m)</summary>
    public float AirHeight { get; private set; }

    void OnEnable()
    {
        Build();
    }

    void OnDisable()
    {
        Cleanup();
    }

    void OnDestroy()
    {
        Cleanup();
    }

    void Reset()
    {
        shader = Shader.Find(ShaderName);
    }

    void OnValidate()
    {
        if (shader == null) shader = Shader.Find(ShaderName);
        radius = Mathf.Max(0.02f, radius);
        fadeHeight = Mathf.Max(0.01f, fadeHeight);
        pixelsPerMeter = Mathf.Max(1f, pixelsPerMeter);
        maxStep = Mathf.Max(0.01f, maxStep);
        probeUp = Mathf.Max(0.05f, probeUp);
        probeDown = Mathf.Max(0.1f, probeDown);
    }

    void Build()
    {
        if (holder != null) return;
        var sh = shader != null ? shader : Shader.Find(ShaderName);
        if (sh == null)
        {
            Debug.LogWarning("[원형 그림자] 셰이더 '" + ShaderName + "' 를 못 찾았습니다 (RS/Shaders/BlobShadow.shader).", this);
            return;
        }
        holder = new GameObject("BlobShadow (" + name + ")");
        holder.hideFlags = HideFlags.HideAndDontSave;
        holder.layer = gameObject.layer;
        mf = holder.AddComponent<MeshFilter>();
        mr = holder.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        mr.sharedMaterial = mat;
        mesh = new Mesh { name = "BlobShadow", hideFlags = HideFlags.HideAndDontSave };
        mesh.MarkDynamic();
        mf.sharedMesh = mesh;
        mpb = new MaterialPropertyBlock();
        builtRes = -1;
        UpdateShadow();
    }

    void Cleanup()
    {
        if (holder != null) DestroySafe(holder);
        if (mesh != null) DestroySafe(mesh);
        if (mat != null) DestroySafe(mat);
        holder = null; mesh = null; mat = null; mf = null; mr = null;
        builtRes = -1;
    }

    static void DestroySafe(Object o)
    {
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }

    void LateUpdate()
    {
        if (holder == null) Build();
        UpdateShadow();
    }

    // ─────────────────────────────────────────────────────────────

    void UpdateShadow()
    {
        if (mesh == null || mr == null) return;

        int n = Mathf.Clamp(resolution, 3, 11);
        if (n != builtRes) BuildTopology(n);

        Vector3 feet = transform.position;
        if (offset.sqrMagnitude > 0f)
        {
            Vector3 ox = transform.right; ox.y = 0f;
            Vector3 oz = transform.forward; oz.y = 0f;
            feet += ox.normalized * offset.x + oz.normalized * offset.y;
        }

        // 카메라 기준 좌우 축 (늘이기 방향)
        Vector3 right = Vector3.right;
        var cam = Camera.main;
        if (cam != null)
        {
            right = cam.transform.right; right.y = 0f;
            if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
            right.Normalize();
        }
        Vector3 fwd = new Vector3(-right.z, 0f, right.x);

        // 가운데 땅
        bool centerHit = SampleGround(feet, out float groundC);
        if (!centerHit)
        {
            mr.enabled = false;
            AirHeight = 0f;
            return;
        }
        mr.enabled = true;
        AirHeight = Mathf.Max(0f, feet.y - groundC);
        float air01 = Mathf.Clamp01(AirHeight / fadeHeight);
        float r = radius * Mathf.Lerp(1f, minScale, air01);
        float a = strength * Mathf.Lerp(1f, minStrength, air01);

        float rx = r * widthScale, rz = r;
        // 가장자리 픽셀이 잘리지 않게 한 칸 여유
        float pad = pixelSnap ? 1.5f / pixelsPerMeter : 0f;
        float ex = rx + pad, ez = rz + pad;

        verts.Clear(); cols.Clear();
        for (int j = 0; j < n; j++)
        {
            float v = j / (float)(n - 1) * 2f - 1f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1) * 2f - 1f;
                Vector3 p = feet + right * (u * ex) + fwd * (v * ez);
                float h = groundC, alpha = 1f;
                if (SampleGround(p, out float g))
                {
                    if (Mathf.Abs(g - groundC) > maxStep) alpha = 0f;
                    else h = g;
                }
                else alpha = 0f;
                verts.Add(new Vector3(p.x, h + lift, p.z));
                cols.Add(new Color(1f, 1f, 1f, alpha));
            }
        }
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.RecalculateBounds();

        mr.GetPropertyBlock(mpb);
        mpb.SetVector(idCenter, new Vector4(feet.x, groundC, feet.z, 0f));
        mpb.SetVector(idAxis, new Vector4(right.x, right.z, 1f / Mathf.Max(1e-3f, rx), 1f / Mathf.Max(1e-3f, rz)));
        mpb.SetVector(idParams, new Vector4(a, Mathf.Clamp(softness, 0.01f, 1f), bands, pixelSnap ? pixelsPerMeter : 0f));
        mpb.SetVector(idParams2, new Vector4(maxStep, 0f, 0f, 0f));
        mpb.SetColor(idColor, color);
        mr.SetPropertyBlock(mpb);
    }

    void BuildTopology(int n)
    {
        builtRes = n;
        mesh.Clear();
        var v = new Vector3[n * n];
        mesh.vertices = v;
        var tri = new int[(n - 1) * (n - 1) * 6];
        int k = 0;
        for (int j = 0; j < n - 1; j++)
            for (int i = 0; i < n - 1; i++)
            {
                int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                tri[k++] = a; tri[k++] = c; tri[k++] = b;
                tri[k++] = b; tri[k++] = c; tri[k++] = d;
            }
        mesh.triangles = tri;
    }

    /// <summary>p 의 x · z 에서 발 높이 + probeUp 아래로 가장 높은 땅. RS 지형 높이와 콜라이더 중 높은 쪽.</summary>
    bool SampleGround(Vector3 p, out float h)
    {
        float top = transform.position.y + probeUp;
        h = float.NegativeInfinity;
        bool found = false;

        foreach (var t in RSTerrain.All)
        {
            if (t == null || !t.HasHeights) continue;
            var l = t.transform.InverseTransformPoint(p);
            if (!t.InsideXZ(l.x, l.z)) continue;
            float y = t.SampleHeightWorld(new Vector3(p.x, top, p.z));
            if (y <= top + 0.001f && y > h) { h = y; found = true; }
        }

        int count = Physics.RaycastNonAlloc(new Vector3(p.x, top, p.z), Vector3.down, hits,
                                            probeUp + probeDown, groundMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var c = hits[i].collider;
            if (c == null || c.transform.IsChildOf(transform)) continue;   // 자기 콜라이더 · CharacterController 제외
            float y = hits[i].point.y;
            if (y > h) { h = y; found = true; }
        }
        return found;
    }
}
