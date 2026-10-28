using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 3D 보스 모델을 저해상도로 찍어서 경기장에 판(Quad) 2장으로 띄우는 "촬영 스튜디오" (2026-10-28, 쿄다이).
///
/// 왜 이렇게 하나:
///   · 3D 모델을 낮은 해상도 + Point 필터로 찍으면 그대로 픽셀 아트처럼 보인다. 플레이어 스프라이트·UI는 안 건드린다
///   · 판이 2장(뒤/앞)이라 레이어 규칙을 그대로 그릴 수 있다 — 배경 부위는 뒤 판(플레이어 뒤, 어둡게),
///     전경으로 나온 팔·다리는 앞 판(플레이어 앞). 부위가 어느 판에 찍힐지는 렌더 레이어로 고른다(KyodaiLimb)
///   · 스튜디오 카메라가 원근이라, 팔을 카메라 쪽으로 당기기만 하면 "크기 확대"(전이 예고)가 저절로 된다
///
/// 좌표: 판은 경기장 평면(planeZ)의 arenaRect를 덮는다. 스튜디오 화면의 (u,v) = 경기장의 (u,v).
///   StudioToWorld / WorldToStudio로 "모델의 손이 경기장 어디에 보이나 / 경기장 이 자리에 손을 두려면 스튜디오 어디인가"를 바꾼다.
///
/// 스튜디오 자체(카메라·모델)는 메인 카메라에 안 보이는 먼 곳에 둔다. 메인 카메라에서 두 렌더 레이어는 빼준다.
/// </summary>
public class PixelStudio : MonoBehaviour
{
    [Tooltip("모델을 찍는 원근 카메라. 이 카메라 설정을 복사해 앞 판용 카메라를 하나 더 만든다")]
    [SerializeField] private Camera studioCamera;

    [Tooltip("판이 덮는 경기장 영역(월드). 스튜디오 화면 전체가 여기에 대응한다")]
    [SerializeField] private Rect arenaRect = new Rect(-16f, -3f, 32f, 18f);

    [Tooltip("판이 놓이는 경기장 평면의 z (플레이어가 서 있는 평면)")]
    [SerializeField] private float planeZ;

    [Tooltip("월드 1칸당 픽셀 수. 낮을수록 더 굵은 픽셀")]
    [Min(1)] [SerializeField] private int pixelsPerUnit = 16;

    [Tooltip("뒤 판에 찍힐 렌더 레이어 번호(이름 없는 빈 레이어 사용 — ProjectSettings 수정 없이)")]
    [Range(8, 31)] [SerializeField] private int backLayer = 30;

    [Tooltip("앞 판에 찍힐 렌더 레이어 번호")]
    [Range(8, 31)] [SerializeField] private int frontLayer = 31;

    [Tooltip("뒤 판 정렬 순서 (플레이어 스프라이트보다 낮게)")]
    [SerializeField] private int backSortingOrder = -50;

    [Tooltip("앞 판 정렬 순서 (플레이어 스프라이트보다 높게)")]
    [SerializeField] private int frontSortingOrder = 50;

    [Tooltip("뒤 판 색. 기획서10_28 레이어 규칙: 배경은 \"어둡고 채도 낮음\"")]
    [SerializeField] private Color backTint = new Color(0.45f, 0.45f, 0.5f, 1f);

    [SerializeField] private Color frontTint = Color.white;

    private Camera frontCamera;
    private RenderTexture backTexture;
    private RenderTexture frontTexture;
    private MeshRenderer backQuad;
    private MeshRenderer frontQuad;

    public int BackLayer => backLayer;
    public int FrontLayer => frontLayer;
    public Rect ArenaRect => arenaRect;
    public Camera StudioCamera => studioCamera;

    private void Awake()
    {
        if (studioCamera == null)
        {
            Debug.LogError("[PixelStudio] 스튜디오 카메라가 비어 있음");
            enabled = false;
            return;
        }

        int width = Mathf.Max(8, Mathf.RoundToInt(arenaRect.width * pixelsPerUnit));
        int height = Mathf.Max(8, Mathf.RoundToInt(arenaRect.height * pixelsPerUnit));
        backTexture = CreateTexture(width, height, "PixelStudio_Back");
        frontTexture = CreateTexture(width, height, "PixelStudio_Front");

        // 스튜디오 안에 넣은 모델은 전부 뒤 판 레이어로 — 30·31번은 이름 없는 레이어라 인스펙터 Layer 목록에서 못 고른다.
        // 전경으로 나오는 부위는 KyodaiLimb가 그때그때 앞 판 레이어로 옮긴다
        SetLayerRecursive(transform, backLayer);

        SetupCamera(studioCamera, backTexture, 1 << backLayer);

        frontCamera = new GameObject("앞 판 카메라").AddComponent<Camera>();
        frontCamera.transform.SetParent(studioCamera.transform, false);
        frontCamera.CopyFrom(studioCamera);
        SetupCamera(frontCamera, frontTexture, 1 << frontLayer);

        backQuad = CreateQuad("뒤 판", backTexture, backTint, backSortingOrder, planeZ);
        frontQuad = CreateQuad("앞 판", frontTexture, frontTint, frontSortingOrder, planeZ);

        // 스튜디오 모델이 메인 카메라에 직접 찍히지 않게
        var main = Camera.main;
        if (main != null) main.cullingMask &= ~((1 << backLayer) | (1 << frontLayer));
    }

    private void OnDestroy()
    {
        if (backTexture != null) backTexture.Release();
        if (frontTexture != null) frontTexture.Release();
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t) SetLayerRecursive(child, layer);
    }

    private static RenderTexture CreateTexture(int width, int height, string name)
    {
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = name,
            filterMode = FilterMode.Point, // 픽셀이 뭉개지지 않게
            wrapMode = TextureWrapMode.Clamp,
        };
        rt.Create();
        return rt;
    }

    private void SetupCamera(Camera cam, RenderTexture target, int cullingMask)
    {
        cam.targetTexture = target;
        cam.cullingMask = cullingMask;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // 투명 — 모델 없는 곳은 판이 비친다
        cam.aspect = arenaRect.width / arenaRect.height;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;
    }

    // 판은 직접 만든 사각 메쉬 — 정점 색(흰색)이 있어야 Sprites/Default가 색을 제대로 곱한다
    private MeshRenderer CreateQuad(string name, Texture texture, Color tint, int sortingOrder, float z)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(arenaRect.center.x, arenaRect.center.y, z);

        var mesh = new Mesh { name = name };
        float hw = arenaRect.width * 0.5f;
        float hh = arenaRect.height * 0.5f;
        mesh.vertices = new[] { new Vector3(-hw, -hh), new Vector3(hw, -hh), new Vector3(-hw, hh), new Vector3(hw, hh) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var renderer = go.AddComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture, color = tint };
        renderer.sharedMaterial = material;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    /// <summary>스튜디오 안의 점이 경기장 어디에 보이는지 (경기장 평면 2D 좌표)</summary>
    public Vector2 StudioToWorld(Vector3 studioPoint)
    {
        Vector3 vp = studioCamera.WorldToViewportPoint(studioPoint);
        return new Vector2(arenaRect.xMin + vp.x * arenaRect.width, arenaRect.yMin + vp.y * arenaRect.height);
    }

    /// <summary>경기장의 이 자리에 보이려면 스튜디오 카메라에서 depth만큼 떨어진 어디에 둬야 하는지</summary>
    public Vector3 WorldToStudio(Vector2 worldPoint, float depth)
    {
        float u = (worldPoint.x - arenaRect.xMin) / arenaRect.width;
        float v = (worldPoint.y - arenaRect.yMin) / arenaRect.height;
        return studioCamera.ViewportToWorldPoint(new Vector3(u, v, depth));
    }

    /// <summary>스튜디오 카메라에서 depth만큼 떨어진 곳의 스튜디오 1칸이 경기장에서 몇 칸으로 보이는지</summary>
    public float WorldUnitsPerStudioUnit(float depth)
    {
        float frustumHeight = 2f * depth * Mathf.Tan(studioCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return arenaRect.height / frustumHeight;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(arenaRect.center.x, arenaRect.center.y, planeZ), new Vector3(arenaRect.width, arenaRect.height, 0f));
    }
#endif
}
