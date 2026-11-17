// RE:AL STEEL - 간단 캐릭터 (이동 + 카메라 따라가기)
//
// 아무 오브젝트에 이것 하나만 붙이면 바로 움직인다.
//  · 조작: WASD / 방향키 이동 · Shift 달리기 (점프 없음 — 바닥에 붙어 다닌다)
//          카메라 (정면 고정 · 기본): 휠 · +/- 로 거리만 (최소 ~ 최대). 각도 · 방향은 고정
//          카메라 (자유): 마우스 오른쪽 드래그 돌리기 · 휠 · +/- 줌 · Q / E 45° · R / F 위아래 기울이기 · Home 처음 각도
//          연출: SetCameraView(피치, 방향, 거리, 초) 로 범위와 상관없이 잡고 ReleaseCameraView(초) 로 고정 카메라로
//          게임패드: 왼쪽 스틱 이동 · 왼쪽 트리거 달리기 · 어깨 버튼 카메라 돌리기
//  · 이동은 카메라 기준 (화면 위 = 앞)
//  · CharacterController 가 자동으로 붙는다. 모델이 없으면 플레이할 때 임시 캡슐을 만든다
//  · 카메라(기본 Main Camera)는 지금 각도를 그대로 유지한 채 부드럽게 따라온다 (옥토패스식 고정 각도)
//  · RS 지형 위라면 콜라이더가 없어도 지면에 붙는다
//  · 바닥에 붙이기: 내려가는 계단 · 경사에서 붕 뜨지 않고 따라 내려간다. '붙이는 높이'보다 높은 턱에서는 떨어진다
//  · 경사 · 계단 (언리얼 CharacterMovement · KCC 방식 — 바닥 판단은 '캡슐 아래로 쓸기' 하나로만):
//      바닥 = 캡슐 반지름 구를 아래로 쓸어 닿은 면. 걸을 수 있는지는 그 면(닿은 곳 · 발 중심 아래 중 완만한 쪽)의 기울기로
//      가파른 면(Slope Limit 초과)에 서면 미끄러짐 — 시작 · 멈춤 각도에 여유(히스테리시스) + 최소 미끄럼 시간 → 경계에서 떨지 않음
//      부딪힌 가파른 면 · 벽은 다음 프레임에 파고드는 속도를 지운다 → 비비지 않고 따라 걷는다
//      계단 · 턱: 앞에 '걸을 수 있는 디딤판'이 최대 턱 높이 안에 있으면 그만큼 오르고, 윗면이 가파르면(바위 · 고철 더미 · 둑) 턱 오르기를 꺼서 기어오르지 못한다
//      스프라이트 방향은 입력 방향으로 (막혀서 미끄러지는 옆 속도로 뒤집히지 않음)
//  · Animator 가 있으면 Speed(float) · Grounded(bool) 파라미터를 있는 것만 넣어 준다
//  · 4방향 스프라이트: Animator 에 FaceX · FaceY(float) 가 있으면 바라보는 방향을 (0,-1)정면 · (0,1)뒷면 ·
//    (±1,0)옆 으로 딱 잘라 넣고, 옆모습일 때만 왼쪽으로 뒤집는다 (옆 그림은 오른쪽을 보고 그려 둔다).
//    MoveAnimSpeed(float) 가 있으면 걷기 ~ 달리기 속도에 맞춰 재생 속도를 넣어 준다.
//
// Input System · 예전 Input Manager 둘 다 동작한다 (프로젝트 설정에 따라 자동).
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using RealSteel.Common;
using RealSteel.Terrain;

[RSSummary("간단 캐릭터 (이동 + 카메라)",
    "붙이기만 하면 움직이는 테스트용 캐릭터.\n" +
    "· 조작: WASD/방향키 이동 · Shift 달리기 (게임패드도 됨). 점프 없이 바닥에 붙어 다닌다 — 계단 · 경사는 그대로 오르내림\n" +
    "· 카메라 (정면 고정 · 기본): 피치 12° · 정면, 휠 · +/- 키로 거리 4 ~ 12 m 만 바꾼다\n" +
    "· 카메라 (자유): 마우스 오른쪽 드래그 돌리기 · 휠 · +/- 키 줌 · Q/E 45° · R/F 기울기 · Home 처음 각도\n" +
    "· 이동은 카메라 기준 (화면 위 = 앞)\n" +
    "· 모델이 없으면 플레이할 때 임시 캡슐이 생긴다. 모델을 자식으로 넣으면 그걸 쓴다\n" +
    "· 시작할 때 씬 카메라 각도를 가져온다. 플레이 중 Pitch · Yaw · Distance 를 바꿔 보고 마음에 들면 값을 적어 두자 (플레이가 끝나면 되돌아감)")]
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[AddComponentMenu("RE_AL STEEL/Character/간단 캐릭터 (이동 + 카메라)")]
public class RSSimpleCharacter : MonoBehaviour
{
    [RSGroup("이동")]
    [RSHelp("걷기 · 달리기 속도와 반응. 가속이 클수록 딱딱 끊기고, 작을수록 미끄러지듯 움직인다.")]
    [Tooltip("걷기 속도 (m/s)")]
    [RSKey]
    public float walkSpeed = 4f;
    [Tooltip("달리기 속도 (m/s) — Shift / 왼쪽 트리거")]
    [RSKey]
    public float runSpeed = 7.5f;
    [Tooltip("가속 (m/s²). 클수록 바로 최고 속도")]
    public float acceleration = 40f;
    [Tooltip("몸을 돌리는 속도 (도/초)")]
    public float turnSpeed = 900f;
    [Tooltip("카메라 기준으로 움직인다 (끄면 월드 기준: W = +Z)")]
    public bool cameraRelative = true;

    [RSGroup("중력 · 바닥")]
    [RSHelp("점프는 없다. 경사는 CharacterController 의 Slope Limit 까지 걷고, 더 가파른 곳(바위 · 둑 · 고철 더미)에 서면 미끄러진다 — 시작 · 멈춤 각도 여유로 경계에서 떨지 않는다. 계단 · 턱은 디딤판 윗면이 걸을 수 있으면 '최대 턱 높이'까지 오르고, 윗면이 가파르면 못 오른다. 부딪힌 가파른 면은 벽처럼 따라 걷는다. 내려가는 쪽은 제한 없음 — '붙이는 높이' 안의 바닥에 바로 붙인다.")]
    [Tooltip("내려가는 계단 · 경사에서 바닥에 붙여 주는 최대 높이 (m). 이보다 높은 턱에서는 중력으로 떨어진다")]
    [RSKey]
    public float stickDistance = 0.6f;
    [Tooltip("중력 (m/s², 음수)")]
    public float gravity = -25f;
    [Tooltip("RS 지형 위라면 콜라이더가 없어도 지면에 붙인다")]
    public bool snapToRSTerrain = true;
    [Tooltip("CharacterController 의 Slope Limit 보다 가파른 바닥(절벽 · 둑 · 고철 더미)은 못 오르고, 올라서면 미끄러져 내려온다. 내려가는 쪽은 제한 없음")]
    public bool keepOnWalkable = true;
    [Tooltip("걸을 수 없는 경사에서 미끄러져 내려가는 최고 속도 (m/s)")]
    public float maxSlideSpeed = 6f;
    [Tooltip("턱을 이 높이까지 오른다 (m). 디딤판 윗면이 걸을 수 있는 기울기일 때만 — 바위 · 고철 더미 · 둑처럼 윗면이 가파르면 못 오른다.\n" +
             "계단은 조립품이 경사로 콜라이더를 덧대 주므로(조립품 '계단 경사로') 이 값은 낮게 — 키(0.81 m)의 25% 안팎. 이보다 높은 단상 · 받침은 못 올라간다")]
    [RSKey]
    public float maxStepHeight = 0.2f;
    [Range(0f, 10f), Tooltip("미끄러짐 각도 여유 (도). Slope Limit + 절반보다 가파라야 미끄러지기 시작하고, Slope Limit - 절반보다 완만해야 멈춘다 → 경계에서 걷기 ↔ 미끄러짐이 떨지 않는다")]
    public float slopeHysteresis = 4f;
    [Tooltip("한 번 미끄러지기 시작하면 최소 이 시간(초)은 미끄러진다 (경계에서 깜빡임 방지)")]
    public float minSlideTime = 0.15f;

    public enum CameraMode
    {
        [InspectorName("정면 고정 (게임용)")] Fixed,
        [InspectorName("자유 (돌리기 · 기울이기)")] Free,
    }

    [RSGroup("카메라 고정")]
    [RSHelp("정면 고정: 각도 · 방향은 고정하고 휠(+/- 키 · 십자키 위아래)로 거리만 최소 ~ 최대 사이에서 바꾼다. Q/E · R/F · 오른쪽 드래그는 꺼진다.\n자유: 아래 '카메라 조작' 설정대로 돌리고 기울인다 (테스트 · 촬영용).\n연출 중에는 스크립트로 SetCameraView(피치, 방향, 거리, 초) 를 부르면 범위와 상관없이 잡을 수 있고, ReleaseCameraView(초) 로 고정 카메라로 돌아온다.")]
    [Tooltip("정면 고정 / 자유")]
    [RSKey]
    public CameraMode cameraMode = CameraMode.Fixed;
    [Range(5f, 89f), Tooltip("고정 카메라가 내려다보는 각도 (도)")]
    [RSKey]
    public float fixedPitch = 12f;
    [Tooltip("고정 카메라 방향 (Y 회전, 도). 0 = 정면 (+Z 를 바라봄)")]
    public float fixedYaw = 0f;
    [Tooltip("휠로 바꿀 수 있는 거리 (m, 최소 ~ 최대)")]
    [RSKey]
    public Vector2 fixedDistanceRange = new Vector2(4f, 12f);
    [Tooltip("시작 거리 (m). 최소 ~ 최대 안으로 맞춘다")]
    public float fixedStartDistance = 9f;

    [RSGroup("카메라")]
    [RSHelp("비워 두면 Main Camera 를 쓴다. 플레이 중에 Pitch · Yaw · Distance 를 인스펙터에서 바로 바꿔 볼 수 있고, 게임 안에서도 돌릴 수 있다:\n· 마우스 오른쪽 드래그 = 돌리기 · 휠 · +/- 키 = 줌 · Q/E = 45° 돌리기 · R/F = 위아래 기울이기 · Home = 처음 각도로\n· 게임패드: 오른쪽 스틱 = 돌리기 · 어깨 버튼 = 45° · 오른쪽 스틱 누르기 = 처음 각도로")]
    [Tooltip("따라올 카메라 (비우면 Main Camera)")]
    public Camera cam;
    [Tooltip("시작할 때 씬에 놓인 카메라의 각도 · 거리를 가져와 아래 Pitch · Yaw · Distance 에 넣는다")]
    public bool keepCurrentAngle = true;
    [Range(5f, 89f), Tooltip("내려다보는 각도 (도). 옥토패스 느낌은 30 ~ 45")]
    [RSKey]
    public float pitch = 35f;
    [Tooltip("카메라 방향 (Y 회전, 도)")]
    public float yaw = 0f;
    [Tooltip("캐릭터와의 거리 (m)")]
    [RSKey]
    public float distance = 12f;
    [Tooltip("원근 카메라의 시야각 (도). 0 = 카메라 설정 그대로. 좁을수록 망원 느낌 (옥토패스는 좁은 편 20 ~ 30)")]
    public float fieldOfView = 0f;
    [Tooltip("카메라가 바라보는 점의 높이 (캐릭터 발 기준, m)")]
    public float lookHeight = 1.2f;
    [Tooltip("따라오는 부드러움 (초). 0 = 딱 붙어서")]
    public float followSmooth = 0.18f;
    [Tooltip("움직이는 방향으로 카메라가 조금 앞서 간다 (m)")]
    public float lookAhead = 1.2f;

    [RSGroup("카메라 조작 (게임 중)")]
    [Tooltip("마우스 오른쪽 드래그 · 게임패드 오른쪽 스틱으로 카메라를 돌린다")]
    public bool allowOrbit = true;
    [Tooltip("마우스 드래그 감도 (도/픽셀)")]
    public float mouseSensitivity = 0.25f;
    [Tooltip("스틱 · R/F 키 회전 속도 (도/초)")]
    public float rotateSpeed = 90f;
    [Tooltip("위아래 기울기 허용 범위 (도, 최소 ~ 최대)")]
    public Vector2 pitchRange = new Vector2(12f, 75f);
    [Tooltip("Q / E (어깨 버튼) 로 돌리는 각도 (0 = 끔)")]
    public float rotateStep = 45f;
    [Tooltip("줌 범위 (Distance 배율). 0.35 = 거리의 35% 까지 가까이, 2.2 = 2.2배까지 멀리")]
    public Vector2 zoomRange = new Vector2(0.35f, 2.2f);
    [Range(0.02f, 0.4f), Tooltip("휠 한 칸에 가까워지는 비율 (0.12 = 12%)")]
    public float zoomStep = 0.12f;
    [Tooltip("+ / - 키 · 게임패드 십자키 위아래를 누르고 있을 때 줌 속도 (휠 칸/초)")]
    public float zoomKeySpeed = 5f;
    [Tooltip("카메라 각도가 따라오는 부드러움 (클수록 빠르게)")]
    public float angleSharpness = 10f;

    [RSGroup("모양")]
    [Tooltip("자식에 보이는 모델이 없으면 플레이할 때 임시 캡슐을 만든다")]
    public bool placeholderIfEmpty = true;
    [Tooltip("SpriteRenderer 가 있으면 몸을 돌리는 대신 좌우로 뒤집는다 (2D 스프라이트 캐릭터)")]
    public bool flipSprites = true;
    [Range(0.2f, 2f), Tooltip("4방향 그림일 때 대각선으로 가면 옆모습을 얼마나 쉽게 보여 줄지. 1 = 정확히 45° 기준, 작을수록 옆모습이 잘 나온다 (0.6 ≈ 31°)")]
    public float sideBias = 0.6f;
    [Range(0.3f, 1.5f), Tooltip("걷기 속도일 때 이동 애니메이션 재생 배율 (달리기 속도 = 1). 걷기 그림이 따로 없을 때 달리기 그림을 조금 느리게 돌린다")]
    public float walkAnimSpeed = 0.75f;
    [Tooltip("플레이할 때 '시야 가림 투명'(RSSeeThrough)이 없으면 붙인다 — 벽 · 건물 · 절벽 뒤로 가도 캐릭터가 보인다")]
    public bool autoSeeThrough = true;
    [Tooltip("플레이할 때 '풀 눕히기'(RSFoliagePusher)가 없으면 붙인다 — 지나가면 둘레 풀 · 꽃이 눕는다")]
    public bool autoFoliagePush = true;
    [Tooltip("플레이할 때 '원형 그림자'(RSBlobShadow)가 없으면 붙인다 — 발밑 동그란 그림자")]
    public bool autoBlobShadow = true;

    // ── 상태 ──
    CharacterController cc;
    readonly RaycastHit[] rayHits = new RaycastHit[8];
    float slideSpeed;          // 걸을 수 없는 경사에서 미끄러지는 속도
    bool onGround = true;      // 발밑에 바닥이 닿아 있다 (걸을 수 있든 없든)
    bool sliding;              // 걸을 수 없는 경사 위 — 미끄러지는 중
    float slideTimer;
    float lastGap;             // 지난 프레임 발과 바닥 사이 (떠오름 검사용)
    Vector3 groundN = Vector3.up;
    float baseStepOffset = -1f;                      // 인스펙터의 Step Offset (평소 값)
    readonly List<Vector3> walls = new List<Vector3>(8);   // 지난 이동에서 부딪힌 벽 · 가파른 면 (수평 법선)
    bool collecting;
    float animSpeed;
    readonly Collider[] overlap = new Collider[8];
    Animator anim;
    SpriteRenderer[] sprites;
    Vector3 velocity;          // 수평 속도
    float vy;                  // 수직 속도
    bool grounded;
    float zoom = 1f, targetZoom = 1f;
    float curPitch, curYaw;             // 화면에 쓰는 (부드럽게 따라가는) 각도
    float homePitch, homeYaw, homeDist; // Home 으로 돌아갈 처음 값
    Vector3 camFocus, camFocusVel;
    bool camReady;
    float fixedDist = -1f;              // 정면 고정에서 휠로 바꾼 지금 거리
    float curDist = -1f;                // 화면에 쓰는 (부드럽게 따라가는) 거리
    bool viewOverride;                  // 연출용 SetCameraView 중
    float ovPitch, ovYaw, ovDist;
    float transSharp;                   // 연출 전환 속도 (0 = 평소 값)

    int hSpeed, hGrounded;
    bool pSpeed, pGrounded;
    // 4방향 스프라이트
    static readonly int hFaceX = Animator.StringToHash("FaceX");
    static readonly int hFaceY = Animator.StringToHash("FaceY");
    static readonly int hMoveAnimSpeed = Animator.StringToHash("MoveAnimSpeed");
    static readonly int FlipXId = Shader.PropertyToID("_FlipX");
    bool pFace, pMoveAnimSpeed;
    bool faceSide;                 // 지금 옆모습인가
    bool flipped;                  // 지금 왼쪽으로 뒤집혔나
    MaterialPropertyBlock flipBlock;

    // ─────────────────────────────────────────────────────────────

    void Reset()
    {
        // 발 기준 피벗에 맞춘다 (모델 발이 오브젝트 위치에 있다고 가정)
        var c = GetComponent<CharacterController>();
        if (c != null && c.center == Vector3.zero)
        {
            float h = 1.8f;
            var r = GetComponentInChildren<Renderer>();
            if (r != null) h = Mathf.Clamp(r.bounds.size.y, 0.8f, 4f);
            c.height = h;
            c.radius = Mathf.Min(0.4f, h * 0.25f);
            c.center = new Vector3(0f, h * 0.5f + c.skinWidth, 0f);
        }
    }

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (cc.center == Vector3.zero) Reset();
        baseStepOffset = cc.stepOffset;
        if (autoSeeThrough && Application.isPlaying && GetComponent<RSSeeThrough>() == null) gameObject.AddComponent<RSSeeThrough>();
        if (autoFoliagePush && Application.isPlaying && GetComponent<RSFoliagePusher>() == null) gameObject.AddComponent<RSFoliagePusher>();
        if (autoBlobShadow && Application.isPlaying && GetComponent<RSBlobShadow>() == null) gameObject.AddComponent<RSBlobShadow>();

        if (placeholderIfEmpty && GetComponentInChildren<Renderer>() == null) MakePlaceholder();

        anim = GetComponentInChildren<Animator>();
        sprites = GetComponentsInChildren<SpriteRenderer>();
        if (anim != null)
        {
            foreach (var p in anim.parameters)
            {
                if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) pSpeed = true;
                if (p.name == "Grounded" && p.type == AnimatorControllerParameterType.Bool) pGrounded = true;
                if (p.name == "FaceX" && p.type == AnimatorControllerParameterType.Float) pFace = true;
                if (p.name == "MoveAnimSpeed" && p.type == AnimatorControllerParameterType.Float) pMoveAnimSpeed = true;
            }
            bool hasFaceY = false;
            foreach (var p in anim.parameters)
                if (p.name == "FaceY" && p.type == AnimatorControllerParameterType.Float) hasFaceY = true;
            pFace &= hasFaceY;
            hSpeed = Animator.StringToHash("Speed");
            hGrounded = Animator.StringToHash("Grounded");
        }
    }

    void Start()
    {
        if (cam == null) cam = Camera.main;
        SetupCamera();
        SnapToGround(true);
        onGround = true; sliding = false; lastGap = GroundTolerance();
        if (pFace) SetFlip(false, true);   // 4방향 그림은 원본 그대로(정면)에서 시작
    }

    void Update()
    {
        ReadInput(out Vector2 move, out bool run, out float rot, out float scroll,
                  out Vector2 orbit, out float tilt, out bool resetCam);

        // ── 방향 (카메라 기준) ──
        Vector3 fwd = Vector3.forward, right = Vector3.right;
        if (cameraRelative && cam != null)
        {
            fwd = cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = cam.transform.up;
            fwd.y = 0f; fwd.Normalize();
            right = new Vector3(fwd.z, 0f, -fwd.x);
        }
        Vector3 wish = fwd * move.y + right * move.x;
        if (wish.sqrMagnitude > 1f) wish.Normalize();

        float dt = Time.deltaTime;
        float lim = Mathf.Clamp(cc.slopeLimit, 1f, 89f);

        // ── 수평 속도 (입력) ──
        float maxSpeed = run ? runSpeed : walkSpeed;
        velocity = Vector3.MoveTowards(velocity, wish * maxSpeed, acceleration * dt);

        // 지난 이동에서 부딪힌 벽 · 가파른 면으로 파고드는 성분은 지운다 → 비비지 않고 면을 따라 걷는다
        if (keepOnWalkable) ClipIntoWalls(ref velocity);

        // ── 미끄러짐: 걸을 수 없는 경사 위 — 오르막 쪽 입력은 지우고 면을 따라 내려간다 ──
        Vector3 slideMove = Vector3.zero;
        if (sliding && keepOnWalkable)
        {
            Vector3 down = Vector3.ProjectOnPlane(Vector3.down, groundN);
            if (down.sqrMagnitude > 1e-6f)
            {
                down.Normalize();
                Vector3 dh = new Vector3(down.x, 0f, down.z);
                if (dh.sqrMagnitude > 1e-6f)
                {
                    dh.Normalize();
                    float uphill = -Vector3.Dot(velocity, dh);
                    if (uphill > 0f) velocity += dh * uphill;   // 비벼도 높이를 얻을 방법이 없다
                }
                float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - groundN.y * groundN.y));
                slideSpeed = Mathf.Min(slideSpeed + Mathf.Abs(gravity) * sinA * dt, Mathf.Max(0.5f, maxSlideSpeed));
                slideMove = down * slideSpeed;
            }
        }
        else slideSpeed = 0f;

        // ── 세로 ──
        if (sliding && keepOnWalkable) vy = -1f;          // 면을 따라 내려가는 건 slideMove 가 한다. 살짝 눌러 붙어 있게
        else if (onGround) vy = -2f;                       // 비탈에서 뜨지 않게 살짝 누른다
        else vy += gravity * dt;

        // ── 턱 · 계단: 앞이 걸을 수 있는 디딤판이면 그 높이까지, 윗면이 가파르면 턱 오르기를 끈다 ──
        if (keepOnWalkable) cc.stepOffset = StepOffsetFor(transform.position, velocity, lim);

        // ── 이동 ──
        Vector3 before = transform.position;
        walls.Clear();
        collecting = true;
        cc.Move((velocity + slideMove + Vector3.up * vy) * dt);
        collecting = false;

        // 미끄러지는 중에는 높이를 얻을 수 없다 — CharacterController 가 면을 따라 미끄러지며 위로 밀려 올라간 만큼 되돌린다
        // (오목한 구석 · 꼭짓점을 대각선으로 꾹 누르면 '면 따라 미끄러짐'이 위쪽을 향해 조금씩 타고 오르던 것)
        // 걷는 중에도 한 프레임에 오를 수 있는 높이는 '이번 턱 높이 + 수평 이동 × tan(Slope Limit)' 까지 — 그 이상 밀려 오른 건 되돌린다
        if (keepOnWalkable)
        {
            Vector3 mv = transform.position - before;
            float gain = mv.y;
            float allowed = sliding ? 0f : cc.stepOffset + new Vector2(mv.x, mv.z).magnitude * Mathf.Tan(lim * Mathf.Deg2Rad) + 0.005f;
            if (gain > allowed + 0.0005f)
            {
                collecting = true;
                cc.Move(Vector3.down * (gain - allowed + 0.002f));
                collecting = false;
                gain = transform.position.y - before.y;
            }
            // 올라갔으면 그 높이에 '걸을 수 있는 바닥'이 실제로 있어야 한다 (계단 · 경사로).
            // 벽 구석에 끼어 CharacterController 의 턱 오르기 · 겹침 밀어내기로 떠오른 건 바닥이 없으니 전부 되돌린다
            if (gain > 0.002f)
            {
                // 올라간 뒤에도 발과 바닥 사이가 지난 프레임보다 벌어지면 안 된다 (계단 · 경사로는 그대로, 구석에서 떠오른 건 벌어짐)
                // 정상: ① 넘을 수 있는 턱에 올라섬(걸침 포함) ② 발밑 바닥이 같이 올라옴(경사로 · 계단 경사로)
                //       ③ 울퉁불퉁한 바닥의 작은 혹 — 발밑에서 이번 턱 높이까지 뜨는 건 허용 (바로 다음 프레임에 붙인다)
                // 비정상: 그보다 더 떠오름 — 둥근 캡슐 바닥이 모서리 · 구석을 타고 밀려 오른 것
                //         (구석 · 모서리 · 높은 단 앞에서는 StepOffsetFor 가 턱 높이를 4 cm 로 내려 두므로 거기서 바로 막힌다)
                bool floor = ProbeGround(transform.position, Mathf.Max(0.05f, stickDistance), lim, out GroundInfo ng)
                             && ng.angle <= lim + Mathf.Max(0f, slopeHysteresis) * 0.5f
                             && (ng.perched || ng.cgap <= Mathf.Max(Mathf.Max(lastGap, 0f) + 0.015f, cc.stepOffset + 0.01f));
                if (!floor)
                {
                    collecting = true;
                    cc.Move(Vector3.down * (gain + 0.002f));
                    collecting = false;
                }
            }
        }

        // ── 바닥 다시 보기 · 내려가는 계단 / 경사에 붙이기 ──
        bool wasWalking = onGround && !sliding;
        float reach = Mathf.Max(0.05f, stickDistance);
        bool has = ProbeGround(transform.position, reach, lim, out GroundInfo g);
        float tol = GroundTolerance();
        if (wasWalking && has && g.gap > tol && g.gap <= reach && g.angle <= lim + slopeHysteresis * 0.5f)
        {
            cc.Move(Vector3.down * (g.gap + 0.01f));
            has = ProbeGround(transform.position, reach, lim, out g);
        }
        SnapToGround(false);

        // ── 상태 (다음 프레임용) ──
        UpdateGroundState(has, g, tol, lim, dt);
        if (!onGround && cc.isGrounded && vy <= 0f) onGround = true;   // 탐지가 놓친 울퉁불퉁한 바닥 — CharacterController 가 닿았다고 하면 믿는다
        grounded = onGround && !sliding;

        // ── 방향 돌리기 / 스프라이트 뒤집기 — 입력 방향으로 (막혀서 생긴 옆 속도로 뒤집히지 않게) ──
        Vector3 moved = transform.position - before;
        Vector3 flat = dt > 1e-5f ? new Vector3(moved.x, 0f, moved.z) / dt : Vector3.zero;
        if (wish.sqrMagnitude > 0.01f)
        {
            if (pFace && anim != null)
            {
                UpdateFacing(wish);
            }
            else if (flipSprites && sprites != null && sprites.Length > 0)
            {
                float side = cam != null ? Vector3.Dot(wish, cam.transform.right) : wish.x;
                if (Mathf.Abs(side) > 0.05f) foreach (var s in sprites) if (s != null) s.flipX = side < 0f;
            }
            else
            {
                var target = Quaternion.LookRotation(wish.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
            }
        }

        // ── 애니메이터 — 실제로 움직인 속도 (벽에 막혀 있으면 서 있는 그림) ──
        animSpeed = Mathf.Lerp(animSpeed, flat.magnitude, 1f - Mathf.Exp(-15f * dt));
        if (anim != null)
        {
            if (pSpeed) anim.SetFloat(hSpeed, animSpeed);
            if (pGrounded) anim.SetBool(hGrounded, onGround);
            if (pMoveAnimSpeed)
            {
                float t = runSpeed > walkSpeed + 0.01f ? Mathf.InverseLerp(walkSpeed, runSpeed, animSpeed) : 1f;
                anim.SetFloat(hMoveAnimSpeed, Mathf.Lerp(walkAnimSpeed, 1f, t));
            }
        }

        // ── 카메라 입력 ──
        if (viewOverride)
        {
            // 연출: 범위와 상관없이 스크립트가 정한 값
            pitch = ovPitch; yaw = ovYaw; distance = ovDist; targetZoom = 1f;
        }
        else if (cameraMode == CameraMode.Fixed)
        {
            // 정면 고정: 각도 · 방향 고정, 휠로 거리만
            FixedRange(out float lo, out float hi);
            if (fixedDist <= 0f) fixedDist = fixedStartDistance;
            if (Mathf.Abs(scroll) > 0.001f) fixedDist *= Mathf.Pow(1f - Mathf.Clamp(zoomStep, 0.01f, 0.9f), scroll);
            fixedDist = Mathf.Clamp(fixedDist, lo, hi);
            pitch = fixedPitch; yaw = fixedYaw; distance = fixedDist; targetZoom = 1f;
        }
        else
        {
            if (rotateStep > 0f && Mathf.Abs(rot) > 0.5f) yaw += Mathf.Sign(rot) * rotateStep;
            if (allowOrbit)
            {
                yaw += orbit.x;
                pitch -= orbit.y;
            }
            pitch += tilt * rotateSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));
            if (Mathf.Abs(scroll) > 0.001f)
                targetZoom = Mathf.Clamp(targetZoom * Mathf.Pow(1f - Mathf.Clamp(zoomStep, 0.01f, 0.9f), scroll),
                                         Mathf.Min(zoomRange.x, zoomRange.y), Mathf.Max(zoomRange.x, zoomRange.y));
            if (resetCam) { pitch = homePitch; yaw = homeYaw; distance = homeDist; targetZoom = 1f; }
        }
    }

    void LateUpdate()
    {
        if (cam == null) return;
        if (!camReady) SetupCamera();

        float dt = Time.deltaTime;
        zoom = Mathf.Lerp(zoom, targetZoom, 1f - Mathf.Exp(-10f * dt));
        float k = 1f - Mathf.Exp(-(transSharp > 0f ? transSharp : Mathf.Max(0.1f, angleSharpness)) * dt);
        curPitch = Mathf.LerpAngle(curPitch, pitch, k);
        curYaw = Mathf.LerpAngle(curYaw, yaw, k);
        if (fieldOfView > 0.5f && !cam.orthographic) cam.fieldOfView = fieldOfView;

        Vector3 want = transform.position + Vector3.up * lookHeight;
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        if (runSpeed > 0.01f) want += flat / runSpeed * lookAhead;

        camFocus = followSmooth > 0.001f ? Vector3.SmoothDamp(camFocus, want, ref camFocusVel, followSmooth) : want;

        float wantDist = Mathf.Max(0.5f, distance) * zoom;
        if (curDist < 0f) curDist = wantDist;
        curDist = Mathf.Lerp(curDist, wantDist, 1f - Mathf.Exp(-(transSharp > 0f ? transSharp : 10f) * dt));

        Vector3 dir = Quaternion.Euler(curPitch, curYaw, 0f) * Vector3.forward;
        cam.transform.position = camFocus - dir * curDist;
        cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        // 연출 전환이 끝나면 평소 속도로
        if (transSharp > 0f && Mathf.Abs(Mathf.DeltaAngle(curPitch, pitch)) < 0.05f &&
            Mathf.Abs(Mathf.DeltaAngle(curYaw, yaw)) < 0.05f && Mathf.Abs(curDist - wantDist) < 0.01f) transSharp = 0f;
    }

    // ── 4방향 스프라이트: 정면 · 뒷면 · 옆 고르기 ──
    void UpdateFacing(Vector3 flat)
    {
        // 화면 기준 좌우(sx) · 앞뒤(sy) 성분
        Vector3 camRight = Vector3.right, camFwd = Vector3.forward;
        if (cam != null)
        {
            camRight = cam.transform.right; camRight.y = 0f;
            camFwd = cam.transform.forward; camFwd.y = 0f;
            if (camFwd.sqrMagnitude < 1e-4f) camFwd = cam.transform.up;
            camFwd.y = 0f;
            camRight.Normalize(); camFwd.Normalize();
        }
        float sx = Vector3.Dot(flat, camRight);
        float sy = Vector3.Dot(flat, camFwd);

        // 경계에서 깜빡이지 않게 지금 상태 쪽으로 조금 더 버틴다
        float bias = Mathf.Max(0.05f, sideBias) * (faceSide ? 0.8f : 1.25f);
        faceSide = Mathf.Abs(sx) >= Mathf.Abs(sy) * bias;

        if (faceSide)
        {
            anim.SetFloat(hFaceX, 1f);          // 옆 그림 하나를 뒤집어 쓴다
            anim.SetFloat(hFaceY, 0f);
            SetFlip(sx < 0f);
        }
        else
        {
            anim.SetFloat(hFaceX, 0f);
            anim.SetFloat(hFaceY, sy >= 0f ? 1f : -1f);   // 화면 위쪽으로 가면 뒷면
            SetFlip(false);                                 // 정면 · 뒷면은 뒤집지 않는다 (팔 색이 바뀌므로)
        }
    }

    /// <summary>빌보드 셰이더의 _FlipX 를 렌더러마다 넣는다 (SpriteRenderer.flipX 는 빌보드 셰이더에서 안 먹는다).</summary>
    void SetFlip(bool on, bool force = false)
    {
        if (!force && on == flipped) return;
        flipped = on;
        if (sprites == null) return;
        if (flipBlock == null) flipBlock = new MaterialPropertyBlock();
        foreach (var s in sprites)
        {
            if (s == null) continue;
            s.GetPropertyBlock(flipBlock);      // SpriteFlash 등 다른 값은 그대로 둔다
            flipBlock.SetFloat(FlipXId, on ? 1f : 0f);
            s.SetPropertyBlock(flipBlock);
        }
    }

    // ─────────────────────────────────────────────────────────────

    void SetupCamera()
    {
        if (cam == null) return;
        Vector3 focus = transform.position + Vector3.up * lookHeight;
        if (cameraMode == CameraMode.Fixed && !viewOverride)
        {
            // 정면 고정: 씬 카메라 각도는 가져오지 않는다
            FixedRange(out float lo, out float hi);
            fixedDist = Mathf.Clamp(fixedStartDistance, lo, hi);
            pitch = fixedPitch; yaw = fixedYaw; distance = fixedDist; targetZoom = zoom = 1f;
        }
        else if (keepCurrentAngle)
        {
            // 씬에 놓인 카메라의 각도 · 거리를 가져온다
            Vector3 f = cam.transform.forward;
            float d = Vector3.Dot(focus - cam.transform.position, f);
            if (d < 2f) d = Vector3.Distance(focus, cam.transform.position);
            if (d >= 2f) distance = d;
            var e = Quaternion.LookRotation(f, Vector3.up).eulerAngles;
            pitch = e.x > 180f ? e.x - 360f : e.x;
            yaw = e.y;
            keepCurrentAngle = false;   // 한 번 가져온 뒤로는 인스펙터 값이 기준
        }
        pitch = Mathf.Clamp(pitch, Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));
        homePitch = pitch; homeYaw = yaw; homeDist = distance;
        curPitch = pitch; curYaw = yaw;
        curDist = -1f;
        camFocus = focus;
        camFocusVel = Vector3.zero;
        camReady = true;
    }

    /// <summary>에디터에서 카메라 각도를 바꾼 뒤 다시 맞추고 싶을 때</summary>
    [ContextMenu("카메라 각도 다시 잡기")]
    public void RecaptureCamera() { keepCurrentAngle = true; camReady = false; }

    /// <summary>처음 각도로</summary>
    public void ResetCameraAngle() { pitch = homePitch; yaw = homeYaw; distance = homeDist; targetZoom = 1f; }

    void FixedRange(out float lo, out float hi)
    {
        lo = Mathf.Max(0.5f, Mathf.Min(fixedDistanceRange.x, fixedDistanceRange.y));
        hi = Mathf.Max(lo, Mathf.Max(fixedDistanceRange.x, fixedDistanceRange.y));
    }

    /// <summary>정면 고정 / 자유 바꾸기</summary>
    public void SetCameraMode(CameraMode mode) { cameraMode = mode; }

    /// <summary>연출용: 범위 · 고정과 상관없이 이 각도 · 거리로 seconds 동안 옮긴다. ReleaseCameraView 전까지 휠 · 조작은 무시</summary>
    public void SetCameraView(float pitchDeg, float yawDeg, float dist, float seconds = 0.6f)
    {
        viewOverride = true;
        ovPitch = pitchDeg; ovYaw = yawDeg; ovDist = Mathf.Max(0.5f, dist);
        transSharp = seconds > 0.001f ? 4f / seconds : 1000f;
    }

    /// <summary>연출 끝: 고정(또는 자유) 카메라로 seconds 동안 돌아온다</summary>
    public void ReleaseCameraView(float seconds = 0.6f)
    {
        viewOverride = false;
        transSharp = seconds > 0.001f ? 4f / seconds : 1000f;
    }

    /// <summary>정면 고정에서 지금 거리 (m)</summary>
    public float FixedDistance { get { return fixedDist > 0f ? fixedDist : fixedStartDistance; } }

    /// <summary>에디터에서 씬 카메라를 고정 각도 · 시작 거리로 놓는다 (배치를 게임 화면 기준으로 보려고)</summary>
    [ContextMenu("씬 카메라를 고정 각도로 놓기")]
    public void PlaceCameraFixed()
    {
        var c = cam != null ? cam : Camera.main;
        if (c == null) return;
        FixedRange(out float lo, out float hi);
        float d = Mathf.Clamp(fixedStartDistance, lo, hi);
        Vector3 dir = Quaternion.Euler(fixedPitch, fixedYaw, 0f) * Vector3.forward;
        Vector3 focus = transform.position + Vector3.up * lookHeight;
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(c.transform, "카메라 고정 각도");
#endif
        c.transform.SetPositionAndRotation(focus - dir * d, Quaternion.LookRotation(dir, Vector3.up));
    }

    // ─────────────────────────────────────────────────────────────
    // 바닥 · 경사 · 계단 (언리얼 CharacterMovement · Kinematic Character Controller 와 같은 방식)
    //  1. 바닥 판단은 ProbeGround 하나로만: 캡슐 반지름만 한 구를 발 위에서 아래로 쓸어 처음 닿은 면.
    //     걸을 수 있는지 = 닿은 곳 법선 · 그 자리 실제 면 · 발 중심 바로 아래 면 중 가장 완만한 것의 기울기
    //     (계단 모서리에 걸쳐도 디딤판으로, 비탈 아래 평지에 서 있어도 평지로 본다)
    //  2. 걷기 ↔ 미끄러짐은 각도 여유 + 최소 시간으로만 바뀐다 (프레임마다 떨지 않음)
    //  3. 부딪힌 가파른 면 · 벽(OnControllerColliderHit)은 다음 프레임에 파고드는 속도를 지운다
    //  4. 계단 · 턱: 앞 디딤판이 걸을 수 있는 면이고 최대 턱 높이 안이면 Step Offset 을 그만큼 올리고,
    //     윗면이 가파르면 Step Offset 을 거의 0 으로 내린다 — CharacterController 가 턱 오르기로 바위를 조금씩 기어오르는 것을 막는다
    //  5. 지형 높이로 위로 끌어올리기(SnapToGround)는 콜라이더 없는 지형 · 바닥을 뚫고 빠졌을 때만
    // ─────────────────────────────────────────────────────────────

    struct GroundInfo
    {
        public Vector3 point;     // 닿은 곳
        public Vector3 normal;    // 미끄러질 방향에 쓰는 면 법선
        public float gap;         // 캡슐이 이만큼 내려가면 닿는다 (m, 음수 = 살짝 파묻힘)
        public float angle;       // 걸을 수 있는지 판단할 기울기 (도)
        public float cgap;        // 발 중심 바로 아래 바닥까지 (m) — 떠오름 검사용 (가장자리에 걸쳐도 '내 발밑'은 이것)
        public bool perched;      // 넘을 수 있는 턱(이번 Step Offset 이하, 윗면 걸을 수 있음)의 코에 걸쳐 있다
    }

    float CosLimit() => Mathf.Cos(Mathf.Clamp(cc.slopeLimit, 1f, 89f) * Mathf.Deg2Rad);

    float CapsuleRadius() => cc.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));

    float CapsuleHeight() => Mathf.Max(cc.height * Mathf.Abs(transform.lossyScale.y), CapsuleRadius() * 2f);

    /// <summary>바닥에 닿아 있다고 보는 틈 (CharacterController 는 Skin Width 만큼 떠 있거나 파묻힌다)</summary>
    float GroundTolerance() => cc.skinWidth + 0.05f;

    bool IsSelf(Collider c) => c == null || c == cc || c.transform.IsChildOf(transform);

    /// <summary>발밑 바닥. foot = 발(피벗) 위치, reach = 발 아래로 찾을 깊이</summary>
    bool ProbeGround(Vector3 foot, float reach, float lim, out GroundInfo g)
    {
        g = new GroundInfo { normal = Vector3.up, angle = 0f, gap = float.MaxValue };
        float r = CapsuleRadius() * 0.9f;
        float up = Mathf.Max(baseStepOffset > 0f ? baseStepOffset : cc.stepOffset, 0.05f) + 0.05f;   // 살짝 파묻혀도 찾게 위에서 시작
        Vector3 origin = foot + Vector3.up * (r + up);
        float dist = up + reach;

        int n = Physics.SphereCastNonAlloc(origin, r, Vector3.down, rayHits, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        int bi = -1; float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (IsSelf(rayHits[i].collider)) continue;
            if (rayHits[i].distance <= 0f && rayHits[i].point == Vector3.zero) continue;   // 처음부터 겹친 것 (옆 벽 등)
            if (rayHits[i].distance < best) { best = rayHits[i].distance; bi = i; }
        }

        if (bi >= 0)
        {
            var h = rayHits[bi];
            g.point = h.point;
            g.gap = h.distance - up;
            g.cgap = g.gap;
            g.normal = h.normal;
            float rc = CapsuleRadius();
            Vector3 off = new Vector3(h.point.x - foot.x, 0f, h.point.z - foot.z);
            float dh = off.magnitude;

            // 캡슐 바닥 가운데 쪽에 닿음 → 닿은 곳 둘레의 '실제 면' 기울기 (구의 접촉 법선은 오목한 골 · 꼭짓점에서
            // 두 면의 평균이 되어, 둘 다 가파른 골짜기도 완만하게 읽힌다 → 골을 타고 오르던 원인)
            if (dh <= rc * 0.5f)
            {
                g.angle = FaceAngle(h.collider, h.point, h.normal, out Vector3 fn);
                g.normal = fn;
                return true;
            }

            // ── 캡슐 가장자리에 닿음: 모서리 · 꼭짓점 · 턱 · 비탈 (언리얼 Perch 규칙) ──
            // 가장자리에 걸쳐 '서 있다'고 인정하는 건 이번에 넘을 수 있는 턱 높이까지만.
            // (예전엔 최대 턱 + 0.1 m 까지 인정해서, 둥근 캡슐 바닥이 모서리를 타고 밀려 오른 것도 '디딤판에 섰다'로 받아 줬다)
            float perchDepth = Mathf.Max(baseStepOffset > 0f ? baseStepOffset : 0f, cc.stepOffset) + 0.02f;
            bool center = CenterFloor(foot, up, Mathf.Max(reach, perchDepth), out RaycastHit ch);
            float cGap = center ? ch.distance - up : float.MaxValue;
            float cAng = center ? Vector3.Angle(Vector3.up, ch.normal) : 90f;
            g.cgap = center ? cGap : float.MaxValue;

            // 걸친 물체가 '넘을 수 있는 턱'인가: 윗면이 걸을 수 있고, 발밑 바닥에서 그 윗면까지가 이번 턱 높이 이하
            Vector3 outward = dh > 1e-5f ? off / dh * 0.015f : Vector3.zero;
            bool topOk = h.collider.Raycast(new Ray(h.point + outward + Vector3.up * 0.05f, Vector3.down), out RaycastHit fh, 0.12f)
                         && Vector3.Angle(Vector3.up, fh.normal) <= lim;
            float stepRise = center ? h.point.y - (foot.y - cGap) : float.MaxValue;
            g.perched = topOk && center && cAng <= lim && stepRise > 0.02f && stepRise <= perchDepth;

            // 1) 발 중심 바로 아래가 바닥 → 그 바닥 기준 (비탈 아래 평지 · 완만한 비탈 · 옆 벽 모서리에 구가 먼저 닿은 경우)
            if (center && cGap <= GroundTolerance() + 0.05f)
            {
                if (cAng <= lim) { g.normal = ch.normal; g.angle = cAng; return true; }
                g.angle = Mathf.Max(cAng, Vector3.Angle(Vector3.up, h.normal));   // 가파른 비탈 한가운데
                return true;
            }

            // 2) 넘을 수 있는 턱 코에 걸침 → 디딤판에 선 것으로
            if (g.perched)
            {
                g.normal = fh.normal;
                g.angle = Vector3.Angle(Vector3.up, fh.normal);
                return true;
            }

            // 3) 상자 · 바위 모서리나 꼭짓점에 걸쳤는데 발 아래는 깊다 → 설 수 없는 곳.
            //    닿은 점에서 밀려나는 방향(닿은 법선)으로 미끄러져 내려간다 — 모서리를 비벼 타고 오르지 못한다
            FaceAngle(h.collider, h.point, h.normal, out Vector3 sn);
            g.normal = sn.y > 0.01f ? sn : (h.normal.y > 0.01f ? h.normal : Vector3.up);
            g.angle = 90f;
            return true;
        }

        // 콜라이더 없는 RS 지형 (높이맵)
        if (snapToRSTerrain && RSTerrain.All.Count > 0 && !TerrainHasCollider(foot) && TryTerrainHeight(foot, out float th))
        {
            float gap = foot.y - th;
            if (gap <= reach && gap >= -up && TerrainNormal(new Vector3(foot.x, th, foot.z), out Vector3 tn))
            {
                g.point = new Vector3(foot.x, th, foot.z);
                g.gap = gap;
                g.normal = tn;
                g.angle = Vector3.Angle(Vector3.up, tn);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 닿은 곳 둘레(가운데 + 접선 방향 4 cm 네 점)의 실제 삼각형 면 기울기 중 가장 완만한 것 (도).
    /// 잔요철(울퉁불퉁한 지형)은 완만한 면이 하나라도 있으면 걸을 수 있고, 오목한 골 · 꼭짓점은 둘레 면이 다 가팔라서 못 걷는다.
    /// gentlest = 그 면의 법선 (미끄러질 방향에도 쓴다)
    /// </summary>
    static float FaceAngle(Collider col, Vector3 p, Vector3 contactN, out Vector3 gentlest)
    {
        gentlest = contactN;
        Vector3 t1 = Vector3.Cross(contactN, Vector3.up);
        if (t1.sqrMagnitude < 1e-4f) t1 = Vector3.Cross(contactN, Vector3.right);
        t1.Normalize();
        Vector3 t2 = Vector3.Cross(contactN, t1).normalized;
        float best = 180f; bool any = false;
        for (int i = 0; i < 5; i++)
        {
            Vector3 off = i == 0 ? Vector3.zero : i == 1 ? t1 * 0.04f : i == 2 ? -t1 * 0.04f : i == 3 ? t2 * 0.04f : -t2 * 0.04f;
            if (col.Raycast(new Ray(p + off + contactN * 0.03f, -contactN), out RaycastHit rh, 0.09f))
            {
                float a = Vector3.Angle(Vector3.up, rh.normal);
                if (a < best) { best = a; gentlest = rh.normal; }
                any = true;
            }
        }
        return any ? best : Vector3.Angle(Vector3.up, contactN);
    }

    bool CenterFloor(Vector3 foot, float up, float reach, out RaycastHit best)
    {
        best = default;
        int n = Physics.RaycastNonAlloc(foot + Vector3.up * up, Vector3.down, rayHits, up + reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float bd = float.MaxValue; bool ok = false;
        for (int i = 0; i < n; i++)
        {
            if (IsSelf(rayHits[i].collider)) continue;
            if (rayHits[i].distance < bd) { bd = rayHits[i].distance; best = rayHits[i]; ok = true; }
        }
        return ok;
    }

    void UpdateGroundState(bool has, GroundInfo g, float tol, float lim, float dt)
    {
        float half = Mathf.Max(0f, slopeHysteresis) * 0.5f;
        lastGap = has ? Mathf.Min(Mathf.Min(g.cgap, g.gap + 0.3f), tol) : tol;
        if (!has || g.gap > tol)
        {
            // 미끄러지다 면에서 살짝 떠도(울퉁불퉁한 비탈) 바로 공중으로 보지 않는다
            if (sliding && has && g.gap <= tol + 0.15f) { slideTimer += dt; onGround = true; groundN = g.normal; return; }
            onGround = false;
            sliding = false;
            groundN = Vector3.up;
            return;
        }

        onGround = true;
        groundN = g.normal;
        if (!keepOnWalkable) { sliding = false; return; }

        if (!sliding)
        {
            if (g.angle > lim + half) { sliding = true; slideTimer = 0f; }
        }
        else
        {
            slideTimer += dt;
            if (g.angle < lim - half && slideTimer >= minSlideTime) sliding = false;
        }
    }

    /// <summary>지난 이동에서 부딪힌 벽 · 가파른 면으로 파고드는 수평 속도를 지운다</summary>
    void ClipIntoWalls(ref Vector3 v)
    {
        if (walls.Count == 0) return;
        // 여러 면(오목한 구석)은 한 면을 빼면 다른 면으로 다시 파고들 수 있다 → 몇 번 되풀이, 그래도 파고들면 구석에 막힌 것 → 멈춤
        for (int pass = 0; pass < 3; pass++)
        {
            bool into = false;
            for (int i = 0; i < walls.Count; i++)
            {
                float d = Vector3.Dot(v, walls[i]);
                if (d < -1e-4f) { v -= walls[i] * d; into = true; }
            }
            if (!into) return;
        }
        for (int i = 0; i < walls.Count; i++)
            if (Vector3.Dot(v, walls[i]) < -1e-3f) { v = Vector3.zero; return; }
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!collecting || walls.Count >= 8) return;
        // 접촉 법선 대신 실제 면 법선 (꼭짓점 · 골에서 접촉 법선은 위로 기울어 '걸을 수 있는 바닥'으로 오해된다)
        Vector3 n = hit.normal;
        if (hit.collider != null && hit.collider.Raycast(new Ray(hit.point + hit.normal * 0.03f, -hit.normal), out RaycastHit fh, 0.08f))
            n = fh.normal.y < n.y ? fh.normal : n;   // 둘 중 더 가파른 쪽
        if (n.y >= CosLimit() - 0.001f || n.y < -0.3f) return;               // 걸을 수 있는 바닥 · 천장은 벽 아님
        // 발 높이(이번 턱 높이 이하)에 닿은 가파른 면: 벽 · 구석의 밑동일 수도, 돌다리 · 울퉁불퉁한 바닥의 작은 혹일 수도 있다.
        // 면 너머가 평지 · 넘을 수 있는 턱이면 벽으로 치지 않는다 (CharacterController 가 넘는다) — 아니면 벽
        float above = hit.point.y - transform.position.y;
        if (above <= cc.stepOffset + 0.01f)
        {
            Vector3 into = new Vector3(-n.x, 0f, -n.z);
            if (into.sqrMagnitude > 1e-6f)
            {
                float cosLim = CosLimit();
                int cls = Classify(hit.point + into.normalized * 0.05f, transform.position.y,
                                   baseStepOffset > 0f ? baseStepOffset : cc.stepOffset, cosLim, out _);
                if (cls == 0 || cls == 1 || cls == 2) return;
            }
        }
        Vector3 nh = new Vector3(n.x, 0f, n.z);
        if (nh.sqrMagnitude < 1e-6f) return;
        walls.Add(nh.normalized);
    }

    /// <summary>
    /// 이번 이동의 Step Offset.
    ///  정면 (가까운 곳부터 세 점, 처음 만나는 턱으로):
    ///   · 평지 · 내리막            → 옆도 본다 (아래)
    ///   · 작은 턱 (평소 값 이하)   → 평소 값
    ///   · 계단 (걸을 수 있는 윗면 · 최대 턱 높이 이하 · 위에 설 자리) → 그 높이까지
    ///   · 막힘 (가파른 윗면 · 너무 높음 · 설 자리 없음) → 거의 0
    ///  정면이 평지인데 옆(±45° · ±90°)에 막힌 것(상자 · 바위 모서리 · 꼭짓점)이 닿아 있으면 → 거의 0
    ///  (CharacterController 는 비스듬히 스친 모서리도 턱 오르기로 넘으려 해서, 꼭짓점을 비비면 조금씩 타고 오른다)
    ///  미끄러지는 중 → 거의 0
    /// </summary>
    float StepOffsetFor(Vector3 foot, Vector3 v, float lim)
    {
        float baseStep = baseStepOffset > 0f ? baseStepOffset : cc.stepOffset;
        const float tiny = 0.04f;
        if (sliding) return Mathf.Min(baseStep, tiny);
        Vector3 hv = new Vector3(v.x, 0f, v.z);
        float sp = hv.magnitude;
        if (sp < 0.05f || !onGround) return baseStep;
        Vector3 dir = hv / sp;
        float r = CapsuleRadius();
        float cosLim = Mathf.Cos(lim * Mathf.Deg2Rad);

        for (int k = 0; k < 3; k++)
        {
            int cls = Classify(foot + dir * (r + 0.04f + k * 0.06f), foot.y, baseStep, cosLim, out float rise);
            if (cls == 0) continue;
            if (cls == 1) return baseStep;
            if (cls == 2) return Mathf.Min(rise + 0.03f, maxStepHeight + 0.03f);
            return Mathf.Min(baseStep, tiny);
        }
        for (int a = 0; a < 4; a++)
        {
            float deg = a == 0 ? 45f : a == 1 ? -45f : a == 2 ? 90f : -90f;
            Vector3 d2 = Quaternion.Euler(0f, deg, 0f) * dir;
            if (Classify(foot + d2 * (r + 0.03f), foot.y, baseStep, cosLim, out _) == 3) return Mathf.Min(baseStep, tiny);
        }
        return baseStep;
    }

    /// <summary>p 자리: 0 평지 · 내리막 · 없음, 1 작은 턱, 2 계단, 3 막힘</summary>
    int Classify(Vector3 p, float footY, float baseStep, float cosLim, out float rise)
    {
        rise = 0f;
        int t = TopAt(p, footY, out float topY, out Vector3 topN);
        if (t == 2) { rise = float.MaxValue; return 3; }        // 쏘는 자리가 이미 물체 속 = 최대 턱보다 높은 벽 · 바위
        if (t == 0) return 0;
        rise = topY - footY;
        // 윗면 기울기는 삼각형 하나가 아니라 6 cm 둘레 높이로 (울퉁불퉁한 모델 — 돌다리 · Meshy 소품 — 의 작은 혹을 가파른 벽으로 오해하지 않게)
        const float e = 0.06f;
        if (TopAt(p + Vector3.right * e, footY, out float yx, out _) == 1 && TopAt(p + Vector3.forward * e, footY, out float yz, out _) == 1)
        {
            Vector3 sn = Vector3.Cross(new Vector3(0f, yz - topY, e), new Vector3(e, yx - topY, 0f)).normalized;
            if (sn.y < 0f) sn = -sn;
            if (sn.y > topN.y) topN = sn;      // 둘 중 완만한 쪽
        }
        if (rise <= 0.02f) return 0;
        if (rise > maxStepHeight + 0.01f || topN.y < cosLim) return 3;
        if (!RoomAbove(p, topY)) return 3;
        if (rise <= baseStep + 0.005f) return 1;
        return 2;
    }

    /// <summary>p 의 윗면 (발 높이 + 최대 턱 높이 위에서 아래로). 0 = 없음, 1 = 찾음, 2 = 시작 자리가 이미 물체 속</summary>
    int TopAt(Vector3 p, float footY, out float y, out Vector3 n)
    {
        y = 0f; n = Vector3.up;
        float top = footY + maxStepHeight + 0.15f;
        const float pr = 0.03f;
        int c = Physics.SphereCastNonAlloc(new Vector3(p.x, top + pr, p.z), pr, Vector3.down, rayHits, maxStepHeight + 0.35f,
                                           Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float bd = float.MaxValue; int bi = -1;
        for (int i = 0; i < c; i++)
        {
            if (IsSelf(rayHits[i].collider)) continue;
            if (rayHits[i].distance <= 0f && rayHits[i].point == Vector3.zero) return 2;
            if (rayHits[i].distance < bd) { bd = rayHits[i].distance; bi = i; }
        }
        if (bi < 0) return 0;
        var h = rayHits[bi];
        y = h.point.y;
        n = h.collider.Raycast(new Ray(new Vector3(p.x, top, p.z), Vector3.down), out RaycastHit fh, maxStepHeight + 0.5f) ? fh.normal : h.normal;
        return 1;
    }

    /// <summary>디딤판 위(topY)에 캡슐이 설 자리가 있는지</summary>
    bool RoomAbove(Vector3 p, float topY)
    {
        float r = CapsuleRadius() * 0.8f;
        Vector3 a = new Vector3(p.x, topY + r + 0.03f, p.z);
        Vector3 b = new Vector3(p.x, topY + Mathf.Max(r + 0.04f, CapsuleHeight() - r), p.z);
        int c = Physics.OverlapCapsuleNonAlloc(a, b, r, overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < c; i++) if (!IsSelf(overlap[i])) return false;
        return true;
    }

    void OnDisable()
    {
        if (cc != null && baseStepOffset > 0f) cc.stepOffset = baseStepOffset;   // 인스펙터 값으로 되돌린다
    }

    static bool TerrainNormal(Vector3 p, out Vector3 n)
    {
        const float e = 0.2f;
        n = Vector3.up;
        if (!TryTerrainHeight(p + new Vector3(e, 0f, 0f), out float hx1) || !TryTerrainHeight(p - new Vector3(e, 0f, 0f), out float hx0) ||
            !TryTerrainHeight(p + new Vector3(0f, 0f, e), out float hz1) || !TryTerrainHeight(p - new Vector3(0f, 0f, e), out float hz0))
            return false;
        n = new Vector3(hx0 - hx1, 2f * e, hz0 - hz1).normalized;
        return true;
    }

    void SetPosition(Vector3 p)
    {
        cc.enabled = false;
        transform.position = p;
        cc.enabled = true;
    }

    void SnapToGround(bool force)
    {
        if (!snapToRSTerrain || RSTerrain.All.Count == 0) return;
        if (!TryTerrainHeight(transform.position, out float h)) return;
        var p = transform.position;
        // 위로 끌어올리기는 콜라이더 없는 지형이거나 바닥을 뚫고 빠졌을 때만 (안 그러면 경사 제한을 무시하고 둑 · 절벽을 타고 오른다)
        // 콜라이더가 있으면 '바닥을 뚫고 1 m 넘게 빠졌을 때'만 구한다 (틈에 낀 정도로 툭 튀어 오르지 않게)
        float under = TerrainHasCollider(p) ? 1f : 0f;
        if (p.y < h - under || (force && p.y > h))
        {
            cc.enabled = false;
            transform.position = new Vector3(p.x, h, p.z);
            cc.enabled = true;
            if (vy < 0f) vy = 0f;
        }
    }

    static bool TerrainHasCollider(Vector3 world)
    {
        foreach (var t in RSTerrain.All)
        {
            if (t == null || !t.HasHeights || !t.generateCollider) continue;
            var l = t.transform.InverseTransformPoint(world);
            if (t.InsideXZ(l.x, l.z)) return true;
        }
        return false;
    }

    static bool TryTerrainHeight(Vector3 world, out float h)
    {
        h = float.NegativeInfinity;
        bool found = false;
        foreach (var t in RSTerrain.All)
        {
            if (t == null || !t.HasHeights) continue;
            var l = t.transform.InverseTransformPoint(world);
            if (!t.InsideXZ(l.x, l.z)) continue;
            float y = t.SampleHeightWorld(world);
            if (y > h) { h = y; found = true; }
        }
        return found;
    }

    // ── 입력 (Input System / 예전 Input Manager) ──
    void ReadInput(out Vector2 move, out bool run, out float rot, out float scroll,
                   out Vector2 orbit, out float tilt, out bool resetCam)
    {
        move = Vector2.zero; run = false; rot = 0f; scroll = 0f;
        orbit = Vector2.zero; tilt = 0f; resetCam = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
            run |= kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (kb.qKey.wasPressedThisFrame) rot -= 1f;
            if (kb.eKey.wasPressedThisFrame) rot += 1f;
            if (kb.rKey.isPressed) tilt += 1f;
            if (kb.fKey.isPressed) tilt -= 1f;
            resetCam |= kb.homeKey.wasPressedThisFrame && kb.shiftKey.isPressed;
            if (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed) scroll += zoomKeySpeed * Time.deltaTime;
            if (kb.minusKey.isPressed || kb.numpadMinusKey.isPressed) scroll -= zoomKeySpeed * Time.deltaTime;
        }
        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 s = gp.leftStick.ReadValue();
            if (s.sqrMagnitude > 0.04f) move += s;
            run |= gp.leftTrigger.ReadValue() > 0.5f;
            if (gp.leftShoulder.wasPressedThisFrame) rot -= 1f;
            if (gp.rightShoulder.wasPressedThisFrame) rot += 1f;
            Vector2 rs = gp.rightStick.ReadValue();
            if (rs.sqrMagnitude > 0.04f) orbit += rs * (rotateSpeed * Time.deltaTime);
            resetCam |= gp.rightStickButton.wasPressedThisFrame;
            if (gp.dpad.up.isPressed) scroll += zoomKeySpeed * Time.deltaTime;
            if (gp.dpad.down.isPressed) scroll -= zoomKeySpeed * Time.deltaTime;
        }
        var ms = Mouse.current;
        if (ms != null)
        {
            // 휠 값 단위가 Input System 버전 · 설정마다 다르다 (한 칸 = 120 또는 1 안팎) → 한 칸 ≈ 1 로 맞춘다.
            // 예전 코드는 무조건 120 으로 나눠서, 한 칸 = 1 로 오는 환경에선 줌이 전혀 안 먹었다.
            float wheel = ms.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.0001f)
            {
                float notches = Mathf.Abs(wheel) >= 10f ? wheel / 120f : wheel;
                if (Mathf.Abs(notches) < 0.34f) notches = Mathf.Sign(notches) * 0.34f;
                scroll += Mathf.Clamp(notches, -3f, 3f);
            }
            if (ms.rightButton.isPressed) orbit += ms.delta.ReadValue() * mouseSensitivity;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (Input.GetKeyDown(KeyCode.Q)) rot -= 1f;
        if (Input.GetKeyDown(KeyCode.E)) rot += 1f;
        if (Input.GetKey(KeyCode.R)) tilt += 1f;
        if (Input.GetKey(KeyCode.F)) tilt -= 1f;
        resetCam = Input.GetKeyDown(KeyCode.Home) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        scroll = Mathf.Clamp(Input.mouseScrollDelta.y, -3f, 3f);
        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus)) scroll += zoomKeySpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus)) scroll -= zoomKeySpeed * Time.deltaTime;
        if (Input.GetMouseButton(1))
            orbit += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * (mouseSensitivity * 20f);
#endif
        if (move.sqrMagnitude > 1f) move.Normalize();
    }

    // ── 임시 캡슐 ──
    void MakePlaceholder()
    {
        float h = cc.height, r = cc.radius;
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "임시 캡슐 (모델을 넣으면 안 생김)";
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(transform, false);
        body.transform.localPosition = cc.center;
        body.transform.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);

        // 앞쪽 표시
        var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "앞";
        Destroy(nose.GetComponent<Collider>());
        nose.transform.SetParent(transform, false);
        nose.transform.localPosition = cc.center + new Vector3(0f, h * 0.2f, r * 0.9f);
        nose.transform.localScale = new Vector3(r * 0.8f, r * 0.4f, r * 0.6f);
    }
}
