// RE:AL STEEL - 간단 캐릭터 (이동 + 점프 + 카메라 따라가기)
//
// 아무 오브젝트에 이것 하나만 붙이면 바로 움직인다.
//  · 조작: WASD / 방향키 이동 · Shift 달리기 · Space 점프
//          카메라: 마우스 오른쪽 드래그 돌리기 · 휠 줌 · Q / E 45° · R / F 위아래 기울이기 · Home 처음 각도
//          게임패드: 왼쪽 스틱 이동 · 남쪽 버튼(A) 점프 · 왼쪽 트리거 달리기 · 어깨 버튼 카메라 돌리기
//  · 이동은 카메라 기준 (화면 위 = 앞)
//  · CharacterController 가 자동으로 붙는다. 모델이 없으면 플레이할 때 임시 캡슐을 만든다
//  · 카메라(기본 Main Camera)는 지금 각도를 그대로 유지한 채 부드럽게 따라온다 (옥토패스식 고정 각도)
//  · RS 지형 위라면 콜라이더가 없어도 지면에 붙는다
//  · Animator 가 있으면 Speed(float) · Grounded(bool) · Jump(trigger) 파라미터를 있는 것만 넣어 준다
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
    "· 조작: WASD/방향키 이동 · Shift 달리기 · Space 점프 (게임패드도 됨)\n" +
    "· 카메라: 마우스 오른쪽 드래그 돌리기 · 휠 줌 · Q/E 45° · R/F 기울기 · Home 처음 각도\n" +
    "· 이동은 카메라 기준 (화면 위 = 앞)\n" +
    "· 모델이 없으면 플레이할 때 임시 캡슐이 생긴다. 모델을 자식으로 넣으면 그걸 쓴다\n" +
    "· 시작할 때 씬 카메라 각도를 가져온다. 플레이 중 Pitch · Yaw · Distance 를 바꿔 보고 마음에 들면 값을 적어 두자 (플레이가 끝나면 되돌아감)")]
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[AddComponentMenu("RE_AL STEEL/Character/간단 캐릭터 (이동 + 카메라)")]
public class RSSimpleCharacter : MonoBehaviour
{
    [Header("이동")]
    [RSHelp("걷기 · 달리기 속도와 반응. 가속이 클수록 딱딱 끊기고, 작을수록 미끄러지듯 움직인다.")]
    [Tooltip("걷기 속도 (m/s)")]
    public float walkSpeed = 4f;
    [Tooltip("달리기 속도 (m/s) — Shift / 왼쪽 트리거")]
    public float runSpeed = 7.5f;
    [Tooltip("가속 (m/s²). 클수록 바로 최고 속도")]
    public float acceleration = 40f;
    [Tooltip("몸을 돌리는 속도 (도/초)")]
    public float turnSpeed = 900f;
    [Tooltip("카메라 기준으로 움직인다 (끄면 월드 기준: W = +Z)")]
    public bool cameraRelative = true;

    [Header("점프 · 중력")]
    [Tooltip("점프 높이 (m). 0 = 점프 없음")]
    public float jumpHeight = 1.1f;
    [Tooltip("중력 (m/s², 음수)")]
    public float gravity = -25f;
    [Tooltip("RS 지형 위라면 콜라이더가 없어도 지면에 붙인다")]
    public bool snapToRSTerrain = true;

    [Header("카메라")]
    [RSHelp("비워 두면 Main Camera 를 쓴다. 플레이 중에 Pitch · Yaw · Distance 를 인스펙터에서 바로 바꿔 볼 수 있고, 게임 안에서도 돌릴 수 있다:\n· 마우스 오른쪽 드래그 = 돌리기 · 휠 = 줌 · Q/E = 45° 돌리기 · R/F = 위아래 기울이기 · Home = 처음 각도로\n· 게임패드: 오른쪽 스틱 = 돌리기 · 어깨 버튼 = 45° · 오른쪽 스틱 누르기 = 처음 각도로")]
    [Tooltip("따라올 카메라 (비우면 Main Camera)")]
    public Camera cam;
    [Tooltip("시작할 때 씬에 놓인 카메라의 각도 · 거리를 가져와 아래 Pitch · Yaw · Distance 에 넣는다")]
    public bool keepCurrentAngle = true;
    [Range(5f, 89f), Tooltip("내려다보는 각도 (도). 옥토패스 느낌은 30 ~ 45")]
    public float pitch = 35f;
    [Tooltip("카메라 방향 (Y 회전, 도)")]
    public float yaw = 0f;
    [Tooltip("캐릭터와의 거리 (m)")]
    public float distance = 12f;
    [Tooltip("원근 카메라의 시야각 (도). 0 = 카메라 설정 그대로. 좁을수록 망원 느낌 (옥토패스는 좁은 편 20 ~ 30)")]
    public float fieldOfView = 0f;
    [Tooltip("카메라가 바라보는 점의 높이 (캐릭터 발 기준, m)")]
    public float lookHeight = 1.2f;
    [Tooltip("따라오는 부드러움 (초). 0 = 딱 붙어서")]
    public float followSmooth = 0.18f;
    [Tooltip("움직이는 방향으로 카메라가 조금 앞서 간다 (m)")]
    public float lookAhead = 1.2f;

    [Header("카메라 조작 (게임 중)")]
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
    [Tooltip("마우스 휠 줌 범위 (Distance 배율)")]
    public Vector2 zoomRange = new Vector2(0.5f, 1.8f);
    [Tooltip("카메라 각도가 따라오는 부드러움 (클수록 빠르게)")]
    public float angleSharpness = 10f;

    [Header("모양")]
    [Tooltip("자식에 보이는 모델이 없으면 플레이할 때 임시 캡슐을 만든다")]
    public bool placeholderIfEmpty = true;
    [Tooltip("SpriteRenderer 가 있으면 몸을 돌리는 대신 좌우로 뒤집는다 (2D 스프라이트 캐릭터)")]
    public bool flipSprites = true;
    [Tooltip("플레이할 때 '시야 가림 투명'(RSSeeThrough)이 없으면 붙인다 — 벽 · 건물 · 절벽 뒤로 가도 캐릭터가 보인다")]
    public bool autoSeeThrough = true;

    // ── 상태 ──
    CharacterController cc;
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

    int hSpeed, hGrounded, hJump;
    bool pSpeed, pGrounded, pJump;

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

        if (placeholderIfEmpty && GetComponentInChildren<Renderer>() == null) MakePlaceholder();

        anim = GetComponentInChildren<Animator>();
        sprites = GetComponentsInChildren<SpriteRenderer>();
        if (anim != null)
        {
            foreach (var p in anim.parameters)
            {
                if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) pSpeed = true;
                if (p.name == "Grounded" && p.type == AnimatorControllerParameterType.Bool) pGrounded = true;
                if (p.name == "Jump" && p.type == AnimatorControllerParameterType.Trigger) pJump = true;
            }
            hSpeed = Animator.StringToHash("Speed");
            hGrounded = Animator.StringToHash("Grounded");
            hJump = Animator.StringToHash("Jump");
        }
    }

    void Start()
    {
        if (cam == null) cam = Camera.main;
        SetupCamera();
        SnapToGround(true);
    }

    void Update()
    {
        ReadInput(out Vector2 move, out bool run, out bool jump, out float rot, out float scroll,
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

        // ── 점프 · 중력 ──
        grounded = cc.isGrounded || IsOnRSTerrain(0.05f);
        if (grounded && vy < 0f) vy = -2f;   // 비탈에서 붙어 있게
        if (grounded && jump && jumpHeight > 0f)
        {
            vy = Mathf.Sqrt(2f * jumpHeight * -gravity);
            if (anim != null && pJump) anim.SetTrigger(hJump);
        }
        vy += gravity * Time.deltaTime;

        cc.Move((velocity + Vector3.up * vy) * Time.deltaTime);
        SnapToGround(false);

        // ── 방향 돌리기 / 스프라이트 뒤집기 ──
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        if (flat.sqrMagnitude > 0.04f)
        {
            if (flipSprites && sprites != null && sprites.Length > 0)
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
            if (pGrounded) anim.SetBool(hGrounded, grounded);
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
        if (Mathf.Abs(scroll) > 0.01f) targetZoom = Mathf.Clamp(targetZoom * (1f - scroll * 0.1f), zoomRange.x, zoomRange.y);
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

    void SnapToGround(bool force)
    {
        if (!snapToRSTerrain || RSTerrain.All.Count == 0) return;
        if (!TryTerrainHeight(transform.position, out float h)) return;
        var p = transform.position;
        if (p.y < h || (force && p.y > h))
        {
            cc.enabled = false;
            transform.position = new Vector3(p.x, h, p.z);
            cc.enabled = true;
            if (vy < 0f) vy = 0f;
        }
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
    void ReadInput(out Vector2 move, out bool run, out bool jump, out float rot, out float scroll,
                   out Vector2 orbit, out float tilt, out bool resetCam)
    {
        move = Vector2.zero; run = false; jump = false; rot = 0f; scroll = 0f;
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
            jump |= kb.spaceKey.wasPressedThisFrame;
            if (kb.qKey.wasPressedThisFrame) rot -= 1f;
            if (kb.eKey.wasPressedThisFrame) rot += 1f;
            if (kb.rKey.isPressed) tilt += 1f;
            if (kb.fKey.isPressed) tilt -= 1f;
            resetCam |= kb.homeKey.wasPressedThisFrame;
        }
        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 s = gp.leftStick.ReadValue();
            if (s.sqrMagnitude > 0.04f) move += s;
            run |= gp.leftTrigger.ReadValue() > 0.5f;
            jump |= gp.buttonSouth.wasPressedThisFrame;
            if (gp.leftShoulder.wasPressedThisFrame) rot -= 1f;
            if (gp.rightShoulder.wasPressedThisFrame) rot += 1f;
            Vector2 rs = gp.rightStick.ReadValue();
            if (rs.sqrMagnitude > 0.04f) orbit += rs * (rotateSpeed * Time.deltaTime);
            resetCam |= gp.rightStickButton.wasPressedThisFrame;
        }
        var ms = Mouse.current;
        if (ms != null)
        {
            scroll = ms.scroll.ReadValue().y / 120f;
            if (ms.rightButton.isPressed) orbit += ms.delta.ReadValue() * mouseSensitivity;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        jump = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
        if (Input.GetKeyDown(KeyCode.Q)) rot -= 1f;
        if (Input.GetKeyDown(KeyCode.E)) rot += 1f;
        if (Input.GetKey(KeyCode.R)) tilt += 1f;
        if (Input.GetKey(KeyCode.F)) tilt -= 1f;
        resetCam = Input.GetKeyDown(KeyCode.Home);
        scroll = Input.mouseScrollDelta.y;
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
