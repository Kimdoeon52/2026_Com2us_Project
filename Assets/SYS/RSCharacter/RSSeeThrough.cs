// RE:AL STEEL - 시야 가림 투명
//
// 캐릭터에 붙인다. 카메라와 캐릭터 사이의 벽 · 건물 · 절벽을 캐릭터 둘레만 디더(점무늬) 구멍으로 뚫어서
// 가려져도 캐릭터가 보이게 한다. 실제 구멍은 RS 셰이더(지형 · 트라이플래너 · 빌보드)가 뚫고,
// 이 컴포넌트는 매 카메라마다 캐릭터 위치를 셰이더 전역값으로 넣어 줄 뿐이다 (오브젝트 수정 없음).
//
// · 캐릭터 뒤 배경, 캐릭터가 서 있는 바닥은 안 뚫린다 (앞쪽 여유 · 바닥 여유)
// · 그림자는 그대로 남는다
// · URP Lit 등 다른 셰이더를 쓰는 물체는 안 뚫린다 → RS 셰이더로 바꾸거나 인스펙터의 "못 받는 머티리얼 찾기"
// · 최대 4명까지 동시에
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;
using RealSteel.Terrain;

[RSSummary("시야 가림 투명",
    "카메라와 이 캐릭터 사이를 가리는 벽 · 건물 · 절벽을 캐릭터 둘레만 점무늬로 뚫어서 보이게 한다.\n" +
    "· RS 셰이더(지형 · 트라이플래너 · 빌보드) 머티리얼만 뚫린다. 머티리얼마다 '시야 가리면 뚫기' 체크로 끌 수 있다\n" +
    "· 캐릭터 뒤 배경과 발밑 바닥은 안 뚫리고, 그림자는 그대로 남는다\n" +
    "· 기본은 '항상' — 앞에 뭐가 있으면 뚫는다. '가려질 때만'은 콜라이더 · RS 지형으로 가려짐을 감지해서 서서히 켠다\n" +
    "· 편집 중엔 꺼져 있다 (미리보기 켜면 씬에서도 확인 가능)")]
[ExecuteAlways, DisallowMultipleComponent]
[AddComponentMenu("RE_AL STEEL/Character/시야 가림 투명")]
public class RSSeeThrough : MonoBehaviour
{
    public enum Mode
    {
        [InspectorName("항상 (앞에 있으면 뚫기)")] Always,
        [InspectorName("가려질 때만 (감지해서 서서히)")] WhenBlocked,
    }

    [RSHelp("구멍 모양. 반지름은 월드 미터 — 카메라가 멀어져도 캐릭터 대비 크기가 같다.")]
    [Tooltip("항상: 캐릭터 앞에 있는 건 늘 뚫는다 (가볍고 확실)\n가려질 때만: 카메라→캐릭터 사이에 콜라이더 · RS 지형이 있을 때만 서서히 뚫는다")]
    public Mode mode = Mode.Always;
    [Range(0.3f, 5f), Tooltip("구멍 반지름 (m). 캐릭터 키의 0.7~1배 정도가 자연스럽다")]
    public float radius = 1.4f;
    [Range(0f, 1f), Tooltip("가운데 뚫리는 정도. 1 = 완전히 비움 / 0.7 = 벽이 조금 남아 어디 있는지 보인다")]
    public float strength = 0.85f;
    [Range(0.05f, 1f), Tooltip("가장자리 부드러움. 클수록 점무늬가 넓게 번지며 사라진다")]
    public float edgeSoftness = 0.45f;
    [Range(1, 8), Tooltip("점무늬 한 칸 크기 (화면 픽셀). 픽셀아트 느낌은 2~3")]
    public int ditherPixel = 2;

    [RSGroup("범위")]
    [RSHelp("무엇을 뚫지 않을지. 발밑 바닥과 캐릭터 바로 옆 물체는 남긴다.")]
    [Tooltip("캐릭터보다 이만큼 이상 카메라 쪽에 있어야 뚫린다 (m). 캐릭터 바로 옆 난간까지 뚫리면 키운다")]
    public float frontMargin = 0.6f;
    [Tooltip("발보다 이만큼 이상 높은 곳만 뚫린다 (m). 캐릭터 앞 바닥이 뚫리면 키운다")]
    public float floorClearance = 0.4f;
    [Tooltip("구멍 가운데를 옮긴다 (몸 가운데 기준, 월드)")]
    public Vector3 centerOffset = Vector3.zero;

    [RSGroup("가려질 때만 모드")]
    [Tooltip("비워 두면 캐릭터의 카메라 → Main Camera")]
    public Camera viewCamera;
    [Tooltip("가림으로 칠 콜라이더 레이어")]
    public LayerMask blockLayers = ~0;
    [Tooltip("RS 지형(콜라이더 없어도)도 가림으로 본다")]
    public bool detectRSTerrain = true;
    [Tooltip("켜지고 꺼지는 속도 (초당)")]
    public float fadeSpeed = 5f;

    [RSGroup("어디서 보일지")]
    [Tooltip("편집 중(플레이 아닐 때)에도 뚫어서 미리 보기")]
    public bool previewInEditMode = false;
    [Tooltip("씬 뷰 카메라에도 적용 (끄면 게임 카메라만)")]
    public bool inSceneView = false;

    // ── 상태 (저장 안 함) ──
    float fade;
    CharacterController cc;
    Renderer[] renderers;

    static readonly List<RSSeeThrough> active = new List<RSSeeThrough>();
    static bool hooked;
    static readonly Vector4[] bufA = new Vector4[4], bufB = new Vector4[4];
    static readonly RaycastHit[] hits = new RaycastHit[16];
    static readonly int idOn = Shader.PropertyToID("_RSSeeThroughOn");
    static readonly int idCount = Shader.PropertyToID("_RSSeeThroughCount");
    static readonly int idA = Shader.PropertyToID("_RSSeeThroughA");
    static readonly int idB = Shader.PropertyToID("_RSSeeThroughB");
    static readonly int idParams = Shader.PropertyToID("_RSSeeThroughParams");

    /// <summary>지금 이 캐릭터에 적용 중인 세기 (0~1)</summary>
    public float CurrentStrength => strength * (mode == Mode.Always ? 1f : fade);

    // ─────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Unhook();
        active.Clear();
    }

    void OnEnable()
    {
        cc = GetComponent<CharacterController>();
        renderers = null;
        fade = 0f;
        if (!active.Contains(this)) active.Add(this);
        Hook();
    }

    void OnDisable()
    {
        active.Remove(this);
        if (active.Count == 0)
        {
            Unhook();
            Shader.SetGlobalFloat(idOn, 0f);
            Shader.SetGlobalFloat(idCount, 0f);
        }
    }

    void LateUpdate()
    {
        if (mode != Mode.WhenBlocked) { fade = 1f; return; }
        float target = IsBlocked() ? 1f : 0f;
        float dt = Application.isPlaying ? Time.deltaTime : 1f;
        fade = Mathf.MoveTowards(fade, target, Mathf.Max(0.01f, fadeSpeed) * dt);
    }

    // ── 몸 위치 ──

    /// <summary>몸 가운데 (월드), 발 높이, 키</summary>
    public void GetBody(out Vector3 center, out float feetY, out float height)
    {
        if (cc == null) cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            center = transform.TransformPoint(cc.center);
            height = Mathf.Max(0.1f, cc.height * Mathf.Abs(transform.lossyScale.y));
        }
        else
        {
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>();
            bool any = false;
            var b = new Bounds();
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (any) { center = b.center; height = Mathf.Max(0.1f, b.size.y); }
            else { center = transform.position + Vector3.up * 0.9f; height = 1.8f; }
        }
        feetY = center.y - height * 0.5f;
        center += centerOffset;
    }

    Camera ResolveCamera()
    {
        if (viewCamera != null) return viewCamera;
        var ch = GetComponent<RSSimpleCharacter>();
        if (ch != null && ch.cam != null) return ch.cam;
        return Camera.main;
    }

    bool IsBlocked()
    {
        var cam = ResolveCamera();
        if (cam == null) return false;
        GetBody(out Vector3 c, out float feet, out float h);
        Vector3 o = cam.transform.position;

        // 가운데 · 머리 · 발 조금 위, 셋 중 하나라도 막히면 가려진 것
        float low = feet + floorClearance + 0.1f;
        Vector3 p0 = c, p1 = new Vector3(c.x, feet + h * 0.9f, c.z), p2 = new Vector3(c.x, Mathf.Min(low, c.y), c.z);
        return RayBlocked(o, p0) || RayBlocked(o, p1) || RayBlocked(o, p2);
    }

    bool RayBlocked(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float dist = d.magnitude - frontMargin;
        if (dist <= 0.05f) return false;
        d.Normalize();

        int n = Physics.RaycastNonAlloc(from, d, hits, dist, blockLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var t = hits[i].collider != null ? hits[i].collider.transform : null;
            if (t != null && !t.IsChildOf(transform)) return true;
        }

        if (detectRSTerrain)
        {
            var ray = new Ray(from, d);
            foreach (var t in RSTerrain.All)
                if (t != null && t.Raycast(ray, out Vector3 p) && (p - from).sqrMagnitude < dist * dist)
                    return true;
        }
        return false;
    }

    // ── 카메라마다 셰이더 전역값 ──

    static void Hook()
    {
        if (hooked) return;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        hooked = true;
    }

    static void Unhook()
    {
        if (!hooked) return;
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        hooked = false;
    }

    bool WantsCamera(bool isScene)
    {
        if (!Application.isPlaying && !previewInEditMode) return false;
        if (isScene && !inSceneView) return false;
        return true;
    }

    static void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        bool isGame = cam != null && cam.cameraType == CameraType.Game;
        bool isScene = cam != null && cam.cameraType == CameraType.SceneView;

        int n = 0;
        RSSeeThrough first = null;
        if (isGame || isScene)
        {
            for (int i = 0; i < active.Count && n < 4; i++)
            {
                var s = active[i];
                if (s == null || !s.isActiveAndEnabled || !s.WantsCamera(isScene)) continue;
                float st = Mathf.Clamp01(s.CurrentStrength);
                if (st <= 0.001f) continue;
                s.GetBody(out Vector3 c, out float feet, out float h);
                bufA[n] = new Vector4(c.x, c.y, c.z, Mathf.Max(0.05f, s.radius));
                bufB[n] = new Vector4(feet, Mathf.Max(0f, s.floorClearance), Mathf.Max(0f, s.frontMargin), st);
                if (first == null) first = s;
                n++;
            }
        }
        for (int i = n; i < 4; i++) { bufA[i] = Vector4.zero; bufB[i] = Vector4.zero; }

        Shader.SetGlobalVectorArray(idA, bufA);
        Shader.SetGlobalVectorArray(idB, bufB);
        Shader.SetGlobalFloat(idCount, n);
        Shader.SetGlobalFloat(idOn, n > 0 ? 1f : 0f);
        if (first != null)
            Shader.SetGlobalVector(idParams, new Vector4(first.edgeSoftness, first.ditherPixel, 0f, 0f));
    }

    void OnDrawGizmosSelected()
    {
        GetBody(out Vector3 c, out float feet, out float h);
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.6f);
        Gizmos.DrawWireSphere(c, radius);
        Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.6f);
        float y = feet + floorClearance;
        Gizmos.DrawLine(new Vector3(c.x - radius, y, c.z), new Vector3(c.x + radius, y, c.z));
        Gizmos.DrawLine(new Vector3(c.x, y, c.z - radius), new Vector3(c.x, y, c.z + radius));
    }
}
