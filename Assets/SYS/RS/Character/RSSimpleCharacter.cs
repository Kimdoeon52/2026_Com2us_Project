// RE:AL STEEL - 간단 캐릭터 (이동 + 카메라 따라가기)
//
// 아무 오브젝트에 이것 하나만 붙이면 바로 움직인다.
//  · 조작: WASD / 방향키 이동 · Shift 달리기 (점프 없음 — 바닥에 붙어 다닌다)
//          카메라: 마우스 오른쪽 드래그 돌리기 · 휠 · +/- 줌 · Q / E 45° · R / F 위아래 기울이기 · Home 처음 각도
//          게임패드: 왼쪽 스틱 이동 · 왼쪽 트리거 달리기 · 어깨 버튼 카메라 돌리기
//  · 이동은 카메라 기준 (화면 위 = 앞)
//  · CharacterController 가 자동으로 붙는다. 모델이 없으면 플레이할 때 임시 캡슐을 만든다
//  · 카메라(기본 Main Camera)는 지금 각도를 그대로 유지한 채 부드럽게 따라온다 (옥토패스식 고정 각도)
//  · RS 지형 위라면 콜라이더가 없어도 지면에 붙는다
//  · 바닥에 붙이기: 내려가는 계단 · 경사에서 붕 뜨지 않고 따라 내려간다. '붙이는 높이'보다 높은 턱에서는 떨어진다
//  · Animator 가 있으면 Speed(float) · Grounded(bool) 파라미터를 있는 것만 넣어 준다
//  · 4방향 스프라이트: Animator 에 FaceX · FaceY(float) 가 있으면 바라보는 방향을 (0,-1)정면 · (0,1)뒷면 ·
//    (±1,0)옆 으로 딱 잘라 넣고, 옆모습일 때만 왼쪽으로 뒤집는다 (옆 그림은 오른쪽을 보고 그려 둔다).
//    MoveAnimSpeed(float) 가 있으면 걷기 ~ 달리기 속도에 맞춰 재생 속도를 넣어 준다.
//
// Input System · 예전 Input Manager 둘 다 동작한다 (프로젝트 설정에 따라 자동).
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using RealSteel.Common;
using RealSteel.Terrain;

[RSSummary("간단 캐릭터 (이동 + 카메라)",
    "붙이기만 하면 움직이는 테스트용 캐릭터.\n" +
    "· 조작: WASD/방향키 이동 · Shift 달리기 (게임패드도 됨). 점프 없이 바닥에 붙어 다닌다 — 계단 · 경사는 그대로 오르내림\n" +
    "· 카메라: 마우스 오른쪽 드래그 돌리기 · 휠 · +/- 키 줌 · Q/E 45° · R/F 기울기 · Home 처음 각도\n" +
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
    [RSHelp("점프는 없다. 계단 · 경사는 CharacterController 의 Step Offset · Slope Limit 대로 오르고, Slope Limit 보다 가파른 곳(절벽 · 둑)은 못 오른다. 내려가는 쪽은 제한 없음. 내려갈 때는 '붙이는 높이' 안의 바닥에 바로 붙인다 → 발이 뜨지 않는다.")]
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

        // ── 수평 속도 ──
        float maxSpeed = run ? runSpeed : walkSpeed;
        velocity = Vector3.MoveTowards(velocity, wish * maxSpeed, acceleration * Time.deltaTime);

        // ── 바닥 판정 (현업 방식: 발밑 바닥 면의 기울기로 '걸을 수 있는 바닥'인지 본다) ──
        Vector3 pos = transform.position;
        bool steep = false;
        Vector3 floorN = Vector3.up;
        if (keepOnWalkable && GroundAt(pos, pos.y, out float floorY, out floorN) && floorY >= pos.y - FloorReach())
            steep = floorN.y < CosLimit();

        // ── 중력 ──
        grounded = (cc.isGrounded || IsOnRSTerrain(0.05f)) && !steep;
        if (grounded && vy < 0f) vy = -2f;   // 비탈에서 붙어 있게
        vy += gravity * Time.deltaTime;

        Vector3 slide = Vector3.zero;
        if (steep)
        {
            // 걸을 수 없는 경사 위: 오르는 쪽 입력은 지우고, 경사를 따라 미끄러져 내려간다 (높이를 얻을 방법이 없다 → 비벼도 못 오른다)
            Vector3 downhill = new Vector3(floorN.x, 0f, floorN.z);
            if (downhill.sqrMagnitude > 1e-6f)
            {
                downhill.Normalize();
                float uphill = -Vector3.Dot(velocity, downhill);
                if (uphill > 0f) velocity += downhill * uphill;
                float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - floorN.y * floorN.y));
                slideSpeed = Mathf.Min(slideSpeed + Mathf.Abs(gravity) * sinA * Time.deltaTime, Mathf.Max(0.5f, maxSlideSpeed));
                slide = downhill * slideSpeed;
            }
        }
        else
        {
            slideSpeed = 0f;
            if (grounded && keepOnWalkable) BlockSteepAhead(pos);   // 앞의 가파른 경사는 벽처럼 — 올라타지 않고 따라 걷는다
        }

        cc.Move((velocity + slide + Vector3.up * vy) * Time.deltaTime);
        SnapToGround(false);

        // ── 바닥에 붙이기: 땅에 있다가 이번 움직임으로 떴으면(내려가는 계단 · 경사) 아래 바닥으로 바로 내린다 ──
        if (grounded && !cc.isGrounded && !IsOnRSTerrain(0.05f)) StickDown();

        // ── 방향 돌리기 / 스프라이트 뒤집기 ──
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        if (flat.sqrMagnitude > 0.04f)
        {
            if (pFace && anim != null)
            {
                UpdateFacing(flat);
            }
            else if (flipSprites && sprites != null && sprites.Length > 0)
            {
                float side = cam != null ? Vector3.Dot(flat, cam.transform.right) : flat.x;
                if (Mathf.Abs(side) > 0.05f) foreach (var s in sprites) if (s != null) s.flipX = side < 0f;
            }
            else
            {
                var target = Quaternion.LookRotation(flat.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            }
        }

        // ── 애니메이터 ──
        if (anim != null)
        {
            if (pSpeed) anim.SetFloat(hSpeed, flat.magnitude);
            if (pGrounded) anim.SetBool(hGrounded, grounded || steep);
            if (pMoveAnimSpeed)
            {
                float t = runSpeed > walkSpeed + 0.01f ? Mathf.InverseLerp(walkSpeed, runSpeed, flat.magnitude) : 1f;
                anim.SetFloat(hMoveAnimSpeed, Mathf.Lerp(walkAnimSpeed, 1f, t));
            }
        }

        // ── 카메라 입력 ──
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

    void LateUpdate()
    {
        if (cam == null) return;
        if (!camReady) SetupCamera();

        float dt = Time.deltaTime;
        zoom = Mathf.Lerp(zoom, targetZoom, 1f - Mathf.Exp(-10f * dt));
        float k = 1f - Mathf.Exp(-Mathf.Max(0.1f, angleSharpness) * dt);
        curPitch = Mathf.LerpAngle(curPitch, pitch, k);
        curYaw = Mathf.LerpAngle(curYaw, yaw, k);
        if (fieldOfView > 0.5f && !cam.orthographic) cam.fieldOfView = fieldOfView;

        Vector3 want = transform.position + Vector3.up * lookHeight;
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        if (runSpeed > 0.01f) want += flat / runSpeed * lookAhead;

        camFocus = followSmooth > 0.001f ? Vector3.SmoothDamp(camFocus, want, ref camFocusVel, followSmooth) : want;

        Vector3 dir = Quaternion.Euler(curPitch, curYaw, 0f) * Vector3.forward;
        cam.transform.position = camFocus - dir * (Mathf.Max(0.5f, distance) * zoom);
        cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
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
        if (keepCurrentAngle)
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
        camFocus = focus;
        camFocusVel = Vector3.zero;
        camReady = true;
    }

    /// <summary>에디터에서 카메라 각도를 바꾼 뒤 다시 맞추고 싶을 때</summary>
    [ContextMenu("카메라 각도 다시 잡기")]
    public void RecaptureCamera() { keepCurrentAngle = true; camReady = false; }

    /// <summary>처음 각도로</summary>
    public void ResetCameraAngle() { pitch = homePitch; yaw = homeYaw; distance = homeDist; targetZoom = 1f; }

    // ── RS 지형: 콜라이더 없이도 지면에 붙인다 ──
    bool IsOnRSTerrain(float tolerance)
    {
        if (!snapToRSTerrain || RSTerrain.All.Count == 0) return false;
        return TryTerrainHeight(transform.position, out float h) && transform.position.y <= h + tolerance;
    }

    /// <summary>
    /// 발밑 stickDistance 안에 바닥(콜라이더 · RS 지형)이 있으면 거기로 내린다. 없으면(높은 턱) 그대로 두고 떨어지게.
    /// 콜라이더는 CharacterController 로 아래로 밀어 보고 닿았는지로 판단한다 (닿은 곳에서 멈추므로 정확히 붙는다).
    /// </summary>
    void StickDown()
    {
        float d = Mathf.Max(0f, stickDistance);
        if (d <= 0f) return;
        Vector3 before = transform.position;

        cc.Move(Vector3.down * d);
        bool ok = cc.isGrounded;

        // RS 지형 (콜라이더가 없어도)
        if (snapToRSTerrain && RSTerrain.All.Count > 0 && TryTerrainHeight(before, out float h)
            && h <= before.y + 0.001f && before.y - h <= d)
        {
            if (transform.position.y < h) SetPosition(new Vector3(transform.position.x, h, transform.position.z));
            ok = true;
        }

        if (!ok) SetPosition(before);       // 붙일 바닥이 없다 → 원래 자리에서 떨어진다
        else if (vy < 0f) vy = -2f;
    }

    // ── 걸을 수 있는 바닥 (언리얼 CharacterMovement · KCC 와 같은 방식) ──
    //  1. 발밑 바닥 면의 기울기가 Slope Limit(CharacterController) 이하 = 걸을 수 있음. RS 지형은 잔요철에 안 걸리게 높이맵 기울기(0.2 m)로 본다
    //  2. 걸을 수 없는 경사 위에 있으면 '미끄러짐' 상태: 오르는 쪽 입력은 지우고 중력 방향으로 미끄러져 내려간다
    //     → 비스듬히 비비며 타고 오르기가 불가능하고, 경사 한가운데 멈춰 갇히지도 않는다
    //  3. 걷는 중 바로 앞이 걸을 수 없는 오르막이면 그 면을 벽으로 보고 파고드는 속도만 지운다 (따라 걷기). 작은 턱(Step Offset 이하)은 그냥 넘는다
    //  4. 내려가는 쪽은 제한 없음
    //  5. 지형 높이로 위로 끌어올리는 일은 하지 않는다 (콜라이더 없는 지형 · 바닥을 뚫고 빠졌을 때만)

    float CosLimit() => Mathf.Cos(Mathf.Clamp(cc.slopeLimit, 1f, 89f) * Mathf.Deg2Rad);

    float StepUp() => Mathf.Max(0.3f, cc.stepOffset) + 0.2f;

    float CapsuleRadius() => cc.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));

    /// <summary>발밑 바닥으로 인정하는 깊이. 가파른 경사에서는 캡슐 바닥이 면보다 떠 있으므로 그만큼 여유</summary>
    float FloorReach() => 0.1f + CapsuleRadius();

    void BlockSteepAhead(Vector3 pos)
    {
        Vector3 h = new Vector3(velocity.x, 0f, velocity.z);
        float sp = h.magnitude;
        if (sp < 0.01f) return;
        Vector3 dir = h / sp;
        float near = CapsuleRadius() + 0.1f;
        // 바로 앞 면이 걸을 수 없는 기울기이고
        if (!GroundAt(pos + dir * near, pos.y, out float y1, out Vector3 n1) || n1.y >= CosLimit()) return;
        // 조금 더 앞까지 Step Offset 넘게 오르면(작은 턱이 아니면) 벽
        if (!GroundAt(pos + dir * (near + 0.3f), pos.y, out float y2, out _)) return;
        if (Mathf.Max(y1, y2) - pos.y <= cc.stepOffset) return;

        Vector3 nh = new Vector3(n1.x, 0f, n1.z);
        if (nh.sqrMagnitude < 1e-6f) return;
        nh.Normalize();
        float d = Vector3.Dot(velocity, nh);
        if (d < 0f) velocity -= nh * d;
    }

    /// <summary>pos 의 발밑 바닥 높이 · 노멀. refY + 계단 높이에서 아래로 쏜다. 콜라이더가 없으면 RS 지형 높이맵</summary>
    bool GroundAt(Vector3 pos, float refY, out float y, out Vector3 n)
    {
        float top = refY + StepUp();
        float dist = top - refY + Mathf.Max(0.05f, stickDistance) + 0.5f;
        // 작은 구로 쏜다 — 다리 판자 틈 · 계단 모서리에서 바닥을 놓치지 않게
        const float probeR = 0.1f;
        int count = Physics.SphereCastNonAlloc(new Vector3(pos.x, top + probeR, pos.z), probeR, Vector3.down, rayHits, dist,
                                               Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; int bi = -1;
        for (int i = 0; i < count; i++)
        {
            var c = rayHits[i].collider;
            if (c == null || c == cc || c.transform.IsChildOf(transform)) continue;
            if (rayHits[i].distance <= 0f && rayHits[i].point == Vector3.zero) continue;   // 처음부터 겹친 것
            if (rayHits[i].distance < best) { best = rayHits[i].distance; bi = i; }
        }
        if (bi >= 0)
        {
            var h = rayHits[bi];
            y = h.point.y;
            // 노멀은 발 중심 바로 아래 면으로 (구가 모서리에 닿으면 노멀이 기울어 계단을 못 오르므로). 틈이면 위로 본다
            n = h.collider.Raycast(new Ray(new Vector3(pos.x, top, pos.z), Vector3.down), out RaycastHit fh, dist + probeR)
                ? fh.normal : Vector3.up;
            if (h.collider.GetComponentInParent<RSTerrain>() != null && TerrainNormal(h.point, out Vector3 tn)) n = tn;
            return true;
        }
        // 콜라이더 없는 RS 지형
        if (snapToRSTerrain && RSTerrain.All.Count > 0 && TryTerrainHeight(pos, out float th)
            && th <= top && th >= top - dist && TerrainNormal(new Vector3(pos.x, th, pos.z), out n))
        { y = th; return true; }
        y = 0f; n = Vector3.up;
        return false;
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
        float under = TerrainHasCollider(p) ? 0.3f : 0f;
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
