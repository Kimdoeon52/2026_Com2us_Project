using System;
using UnityEngine;

/// <summary>
/// ActionState 이벤트를 구독해서 애니메이션을 재생하는 자리 (§1 View 계층, §10 애니메이션 연동).
/// 나중에 진짜 클립이 나오면 Animator Controller의 스테이트 이름만
/// ActionData.AnimationClipName과 맞춰주면 되고, 이 코드는 수정할 필요가 없어야 한다 (§10 원칙).
///
/// 검증 대상: speed = 클립길이 / 데이터길이(TotalFrames/60f) 공식.
/// 상태 전환 자체는 여전히 ActionState(§3)가 프레임으로 정한다 — 여기서는 그림만 맞춰 재생할 뿐이다.
///
/// 2026-10-09 추가: 활성 구간 클립 전환(ActionData.ActiveClipName), 점프 그림(시작 → 공중 루프, 기획서1009
/// 그래픽 §7-1 — 착지는 ActionExecutor.landAction이 행동으로 처리), 피격 점멸(기획서1009 §6-12-10),
/// 사망 연출(폭발 + 피격 모션 유지 후 사라짐).
/// 전부 "그림"이라 CombatTick 밖(Update)에서 돈다 — 판정·프레임은 하나도 안 건드린다 (§3).
/// </summary>
public class RobotView : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string walkStateName = "Walk";         // 바라보는 방향으로 걸을 때
    [SerializeField] private string backWalkStateName = "BackWalk"; // 바라보는 반대쪽으로 걸을 때

    [Header("점프 — 비우면 점프 중에도 Idle/Walk 그대로 (값 전부 임시)")]
    [Tooltip("땅을 박차는 순간 재생할 스테이트. 비우면 바로 공중 루프로")]
    [SerializeField] private string jumpStartStateName;
    [Tooltip("TODO: 임시값. 점프 시작 스테이트를 보여주는 프레임(60fps 기준). 지나면 공중 루프로 넘어간다")]
    [Min(0)] [SerializeField] private int jumpStartFrames = 20;
    [Tooltip("공중에 떠 있는 동안 반복할 스테이트. 비우면 점프 그림 전체를 끈다")]
    [SerializeField] private string jumpLoopStateName;
    // 착지는 여기서 그리지 않는다 — ActionExecutor.landAction(행동)이 맡는다. 착지 동안 이동·공격이
    // 멈춰야 묵직한 느낌이 나는데, 그건 그림이 아니라 상태(ActionState)가 정할 일이라서다

    [Header("사망 연출 — 값 전부 임시")]
    [Tooltip("죽은 뒤 사라질 때까지 계속 재생할 스테이트 (보통 피격 모션)")]
    [SerializeField] private string deathStateName;
    [Tooltip("TODO: 임시값. 폭발이 이어지는 시간(프레임). 끝나면 스프라이트를 끈다")]
    [Min(0)] [SerializeField] private int deathDurationFrames = 90;
    [SerializeField] private RuntimeAnimatorController explosionController;
    [SerializeField] private string explosionStateName;
    [Tooltip("TODO: 임시값. 폭발이 하나씩 터지는 간격(프레임)")]
    [Min(1)] [SerializeField] private int explosionIntervalFrames = 10;
    [Tooltip("폭발이 생기는 범위의 중심(피벗 기준 로컬)과 반경")]
    [SerializeField] private Vector2 explosionCenter = new Vector2(0f, 0.2f);
    [Min(0f)] [SerializeField] private float explosionRadius = 0.4f;

    /// <summary>
    /// 사망 연출이 시작되는 순간. 화면 흔들림·줌인 같은 연출은 나중에 CombatCamera 등이 이 이벤트들을
    /// 구독해서 붙인다 — 지금은 자리만 열어둔다 (구독자 없음)
    /// </summary>
    public event Action OnDeathStarted;
    /// <summary>폭발 하나가 터질 때마다. 받음: 폭발 월드 위치 (흔들림 세기·사운드 위치용)</summary>
    public event Action<Vector3> OnDeathExplosion;
    /// <summary>폭발이 다 끝나고 로봇이 화면에서 사라지는 순간</summary>
    public event Action OnDeathFinished;

    // Idle/Walk/BackWalk 중 뭘 재생할지 고르려면 "지금 이동 입력이 있는지"를 알아야 한다 —
    // ActionState는 그 정보를 안 갖고 있어서(순수하게 행동 진행 상태만 담당), 여기서 직접 IInputSource를 참조한다
    private IInputSource inputSource;
    // 행동 중인지(=걷기 애니메이션을 틀면 안 되는 구간인지) 판단용
    private ActionState actionState;
    // 바라보는 방향(앞/뒤 걷기 구분) + 땅에 있는지(점프 그림)
    private RobotMover mover;
    // 점멸 색을 읽으려고 들고 있는다 (HitReactionData는 executor에 꽂혀 있음)
    private ActionExecutor executor;
    private SpriteRenderer spriteRenderer;

    // 지금 틀어둔 이동 계열 그림의 키. 같은 그림을 매 프레임 Play하면 클립이 0초로 계속 되감기므로 바뀔 때만 Play한다.
    // 스테이트 이름이 아니라 "종류:이름"으로 기억하는 이유: 다른 용도로 같은 클립을 쓸 때(예: 걷기·뒷걸음 둘 다 Move) 구분하려고
    private string currentLocomotion;

    // 점프 그림용 — 렌더 프레임마다 "60fps 기준 몇 프레임 지났는지"로 센다(Time.deltaTime × 60)
    private bool wasGrounded = true;
    private float airFrames;

    // 피격 점멸
    private Color baseColor = Color.white;
    private float flashRemainingFrames;

    // 사망 연출
    private bool isDying;
    private bool deathFinished;
    private float deathElapsedFrames;
    private float nextExplosionFrame;

    /// <summary>
    /// state: 재생 트리거를 받는 이벤트 소스. input: Idle/Walk/BackWalk 중 뭘 틀지 판단용.
    /// mover: 바라보는 방향·땅에 있는지. executor: 피격 이벤트(점멸). 없으면 그 기능만 빠진다
    /// </summary>
    // 호출: PlayerRobotBootstrap.Awake
    public void Init(ActionState state, IInputSource input, RobotMover mover, ActionExecutor executor = null)
    {
        inputSource = input;
        actionState = state;
        this.mover = mover;
        this.executor = executor;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;

        // ActionState 쪽은 "누가 구독하는지" 전혀 모른 채로 이벤트만 쏘고, 반응은 전부 이쪽 책임이다(§13과 같은 패턴)
        state.OnActionBegin += PlayAction;
        state.OnActionActiveStart += OnActionActiveStart;
        state.OnActionEnd += OnActionEnd;
        state.OnDead += BeginDeath;
        if (executor != null) executor.OnDamaged += OnDamaged;
    }

    // 호출: ActionState.OnActionBegin 이벤트. 받음: 시작된 ActionData — AnimationClipName 스테이트를 재생하고 속도를 데이터 길이에 맞춘다
    private void PlayAction(ActionData action)
    {
        currentLocomotion = null; // 행동 클립이 이동 클립을 덮어썼으니, 행동이 끝나면 이동 스테이트를 새로 골라야 한다
        if (animator == null || string.IsNullOrEmpty(action.AnimationClipName))
        {
            // 조용히 넘어가지 않는 이유: 클립 이름을 안 채운 경우 겉으로는 "그냥 안 움직이네"로만 보여서 원인을 찾기 어렵다
            Debug.LogWarning($"[RobotView] {action?.ActionName} — animator 또는 AnimationClipName 미설정");
            return;
        }

        // 길이를 읽기 전에 speed를 1로 되돌리고 Update(0)으로 상태를 확정한다. 안 그러면 Play 직후 이전 상태/속도가 섞여 길이가 틀리게 읽힌다
        animator.speed = 1f;
        animator.Play(action.AnimationClipName, 0, 0f);
        animator.Update(0f);

        float clipLength = animator.GetCurrentAnimatorStateInfo(0).length; // 원본 클립이 실제로 몇 초짜리인지

        // 원본 에셋이 아니라 확정 프레임(Timeline)을 쓴다 — 보정으로 프레임이 줄어든 행동에서 원본 길이에 맞추면
        // 논리는 이미 끝났는데 그림만 남아서 마지막에 툭 끊긴다.
        // 활성 구간에 다른 클립으로 바꾸는 행동(ActiveClipName)이면 첫 클립은 선딜 동안만 보이므로 선딜 길이에만 맞춘다
        ResolvedAction timeline = actionState?.Timeline;
        int frames;
        if (!string.IsNullOrEmpty(action.ActiveClipName))
            frames = timeline != null ? timeline.StartupFrames : action.StartupFrames;
        else
            frames = timeline != null ? timeline.TotalFrames : action.TotalFrames;
        float dataLength = frames * CombatClock.TICK; // 프레임표가 정한 "이 구간은 몇 초여야 하는지"

        // 데이터가 진실이고 그림이 따라온다(§3, §10). dataLength가 0이면 나누기 오류라 원본 속도(1)
        animator.speed = dataLength > 0f ? clipLength / dataLength : 1f;

        Debug.Log($"[RobotView] {action.ActionName} 클립길이={clipLength:F3}s 데이터길이={dataLength:F3}s → speed={animator.speed:F2}");
    }

    // 호출: ActionState.OnActionActiveStart. 활성 구간 전용 클립이 있으면 거기서 갈아끼운다(예: 총 변신 → 사격 루프).
    // 루프 클립은 "몇 번 반복할지"가 데이터(활성 프레임)로 정해지므로 원본 속도(1배)로 그냥 돌린다
    private void OnActionActiveStart(ActionData action)
    {
        if (animator == null || string.IsNullOrEmpty(action.ActiveClipName)) return;
        animator.speed = 1f;
        animator.Play(action.ActiveClipName, 0, 0f);
    }

    /// <summary>
    /// FrameStepper(F3)가 정지 상태에서 애니메이터를 정확히 1틱 미는 용도.
    /// Animator는 Update()의 Time.deltaTime으로만 움직이는데 Time.timeScale=0이면 그게 0이라
    /// 자동으로는 전혀 진행하지 않는다 (§3) — 그래서 여기서 직접 시간을 넣어 밀어줘야 한다.
    /// </summary>
    // 호출: FrameStepper.Update (F3, CombatClock.Tick 직후)
    public void AdvanceOneTick()
    {
        if (animator == null) return;
        // PlayAction에서 맞춰둔 animator.speed가 그대로 곱해져서 배속도 정확히 반영된다
        animator.Update(CombatClock.TICK);
    }

    /// <summary>
    /// Recovery 끝나고 Idle로 복귀하는 순간 — Animator Exit Time에 기대지 않고 여기서 강제로 되돌린다 (§3).
    /// 액션 중 바꿔둔 animator.speed도 1로 원복해야 Idle/Walk이 정상 속도로 재생된다.
    /// </summary>
    // 호출: ActionState.OnActionEnd 이벤트
    private void OnActionEnd(ActionData finishedAction)
    {
        if (animator == null) return;
        animator.speed = 1f;

        // 끝나는 즉시 맞는 그림을 틀어준다 — 다음 Update까지 기다리면 한 프레임 동안 공격 마지막 장이 남는다
        currentLocomotion = null;
        UpdateLocomotion();
    }

    // 호출: Unity 매 렌더 프레임. 그림만 고르는 일이라 CombatTick 밖에서 주사율대로 돌아도 된다 (§3 "렌더·UI는 틱 바깥")
    private void Update()
    {
        float elapsedFrames = Time.deltaTime * 60f;
        UpdateFlash(elapsedFrames);

        if (isDying)
        {
            UpdateDeath(elapsedFrames);
            return;
        }

        TrackAirborne(elapsedFrames);
        UpdateLocomotion();
    }

    // 이륙 순간을 잡아서 점프 시작 그림 타이머를 돌린다. 행동 중(공중 공격 등)에도 계속 세야
    // 행동이 끝났을 때 "지금 공중 몇 프레임째인지"를 바로 알 수 있다
    private void TrackAirborne(float elapsedFrames)
    {
        bool grounded = mover == null || mover.IsGrounded;

        if (!grounded)
        {
            if (wasGrounded) airFrames = 0f; // 방금 떴다
            airFrames += elapsedFrames;
        }

        wasGrounded = grounded;
    }

    // 행동 중이 아니면 공중/착지/대기/걷기 중 맞는 그림을 고르고, 바뀌었을 때만 Play한다.
    // Animator 전이 조건을 쓰지 않고 여기서 직접 Play하는 이유: §2 "전이 조건 금지, Play()로 직접 재생"
    private void UpdateLocomotion()
    {
        if (animator == null || isDying) return;
        // 행동 클립이 재생 중일 땐 건드리지 않는다 — 행동이 끝나면 OnActionEnd에서 다시 불린다
        if (actionState != null && !actionState.CanMove) return;

        bool grounded = mover == null || mover.IsGrounded;

        if (!grounded && !string.IsNullOrEmpty(jumpLoopStateName))
        {
            bool showStart = !string.IsNullOrEmpty(jumpStartStateName) && airFrames < jumpStartFrames;
            if (showStart) PlayLocomotion("jumpStart", jumpStartStateName, 0f);
            else PlayLocomotion("jumpLoop", jumpLoopStateName, 0f);
            return;
        }

        float moveInput = inputSource?.GetMoveInput() ?? 0f;
        if (Mathf.Abs(moveInput) <= 0.01f)
        {
            PlayLocomotion("idle", idleStateName, 0f);
            return;
        }

        // 바라보는 방향으로 누르면 Walk, 반대로 누르면 BackWalk. 좌우 반전(localScale.x)은 RobotMover가 이미 해준다
        bool facingRight = mover == null || mover.FacingRight;
        bool movingForward = facingRight ? moveInput > 0f : moveInput < 0f;
        if (movingForward) PlayLocomotion("walk", walkStateName, 0f);
        else PlayLocomotion("backWalk", backWalkStateName, 0f);
    }

    private void PlayLocomotion(string kind, string stateName, float startTime)
    {
        string key = kind + ":" + stateName;
        if (key == currentLocomotion) return;
        currentLocomotion = key;
        animator.Play(stateName, 0, startTime);
    }

    // 호출: ActionExecutor.OnDamaged. 회피로 피한 경우엔 깜빡이지 않는다
    private void OnDamaged(HitResolutionResult result)
    {
        if (result.IsEvaded || executor == null || executor.HitReaction == null) return;
        flashRemainingFrames = executor.HitReaction.FlashFrames;
    }

    // 2프레임마다 맞은 색 ↔ 원래 색을 번갈아 — "점멸". 끝나면 반드시 원래 색으로 되돌린다
    private void UpdateFlash(float elapsedFrames)
    {
        if (spriteRenderer == null || flashRemainingFrames <= 0f) return;

        flashRemainingFrames -= elapsedFrames;
        if (flashRemainingFrames <= 0f)
        {
            spriteRenderer.color = baseColor;
            return;
        }

        bool on = ((int)(flashRemainingFrames / 2f)) % 2 == 0;
        spriteRenderer.color = on ? executor.HitReaction.FlashColor : baseColor;
    }

    // 호출: ActionState.OnDead. 피격 모션을 루프로 틀어두고 폭발 타이머를 시작한다
    private void BeginDeath()
    {
        isDying = true;
        deathElapsedFrames = 0f;
        nextExplosionFrame = 0f;

        if (animator != null && !string.IsNullOrEmpty(deathStateName))
        {
            animator.speed = 1f;
            animator.Play(deathStateName, 0, 0f);
        }
        OnDeathStarted?.Invoke();
    }

    // 몸 주변 랜덤 위치에서 폭발을 일정 간격으로 터뜨리다가, 시간이 다 되면 로봇을 화면에서 지운다
    private void UpdateDeath(float elapsedFrames)
    {
        if (deathFinished) return;

        deathElapsedFrames += elapsedFrames;

        while (nextExplosionFrame <= deathElapsedFrames && nextExplosionFrame < deathDurationFrames)
        {
            SpawnDeathExplosion();
            nextExplosionFrame += explosionIntervalFrames;
        }

        if (deathElapsedFrames < deathDurationFrames) return;

        deathFinished = true;
        // 오브젝트 자체는 끄지 않는다 — HitDetection·KKH BattleManager가 이 로봇을 계속 참조하고 있어서,
        // SetActive(false)나 Destroy를 하면 그쪽 구독 해제 순서까지 신경 써야 한다. 그림만 끄면 충분하다
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        OnDeathFinished?.Invoke();
    }

    private void SpawnDeathExplosion()
    {
        bool facingRight = mover == null || mover.FacingRight;
        Vector3 center = BoxResolver.ToWorldPoint(transform.position, explosionCenter, facingRight);
        Vector2 offset = UnityEngine.Random.insideUnitCircle * explosionRadius; // 순수 연출용 랜덤 — 판정과 무관
        Vector3 position = center + new Vector3(offset.x, offset.y, 0f);

        SpriteEffect.PlayOnce(explosionController, explosionStateName, position, transform);
        OnDeathExplosion?.Invoke(position);
    }

    private void OnDestroy()
    {
        if (actionState != null)
        {
            actionState.OnActionBegin -= PlayAction;
            actionState.OnActionActiveStart -= OnActionActiveStart;
            actionState.OnActionEnd -= OnActionEnd;
            actionState.OnDead -= BeginDeath;
        }
        if (executor != null) executor.OnDamaged -= OnDamaged;
    }
}
