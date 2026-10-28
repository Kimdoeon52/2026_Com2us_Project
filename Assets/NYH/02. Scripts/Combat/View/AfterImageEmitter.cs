using UnityEngine;

/// <summary>
/// 지금 그려진 자세를 반투명 잔상으로 잠깐 남기는 연출 (View). 판정과 무관하다. 잔상 종류는 두 가지다.
///
/// ── 1) 끝 자세 잔상 — 연속 공격 패턴용 (2026-10-11, 레퍼런스: 스컬 "초대 용사") ─────────────
///   연속 공격(하단 → 어퍼컷 → 중단)에서 각 기술의 활성이 끝나는 순간(ActionState.OnActionRecoveryStart) 1장.
///   앞 동작의 잔상이 다음 동작을 하는 동안 서서히 사라져서 패턴이 한 박자씩 화면에 읽힌다:
///
///     시간 →   [하단 ━━━━]                 하단 잔상 ░░░░▒ (사라지는 중)
///                         [어퍼컷 ━━━━━]              어퍼컷 잔상 ░░░░▒
///                                        [중단 ━━━━]               중단 잔상 ░░░▒
///
///   · 찍는 시점이 "활성 끝"인 이유: 후딜 끝에서 찍으면 이미 대기 자세로 돌아오는 중이라 밋밋하다. 활성 끝 = 팔을 다 뻗은 자세.
///   · 수명이 고정값(lifetimeSeconds)인 이유: "다음 동작이 끝날 때 사라지게"로 묶으면 패턴의 마지막 기술은 다음 동작이 없어서 꼬인다.
///     적 Skill1/2가 30프레임(0.5초)이라 0.5초면 "다음 동작이 끝날 때쯤 사라지는" 흐름이 나온다.
///   · 언제 켤지는 바깥이 SetArmed로 정한다 — 지금은 AIInputSource가 패턴 시작 때 켜고 끝/중단 때 끈다.
///     같은 기술을 단독으로 쓸 땐 안 남는다(패턴일 때만 남기기로 결정, 2026-10-11).
///
/// ── 2) 꼬리 잔상 — 돌진용 (2026-10-11) ─────────────────────────────────────────────
///   ActionData.TrailAfterImageInterval이 0보다 큰 행동의 활성 구간 동안 그 프레임마다 1장씩. 빠르게 지나간 궤적을 보여준다:
///
///     ▒ ▒ ▒ ▒ ▓ ▓ █ →   (뒤로 갈수록 오래된 잔상 = 더 투명)
///
///   · 기술 데이터에 붙어 있으므로 그 기술은 패턴이든 단독이든 언제 써도 남는다.
///   · 끝 자세 잔상보다 짧게(0.25초), 어두운 보라색으로 — 움직임의 궤적이지 "자세"를 보여주는 게 아니라서.
///
/// ── 붙이는 곳 / 준비 ────────────────────────────────────────────────────────────────
///   로봇 루트 오브젝트(ActionExecutor·RobotView·SpriteRenderer가 있는 그 오브젝트).
///   · 꼬리 잔상 기술이 있으면 RobotView.EnsureEffectComponents가 자동으로 붙인다.
///   · 패턴 끝 자세 잔상만 쓸 거면 자동으로 안 붙는다(패턴 설정은 AI 프로필 쪽이라 RobotView가 모름) — 직접 붙일 것.
///   · poolSize: 동시에 보일 수 있는 잔상 수. 꼬리 잔상은 "남는 시간 ÷ 찍는 간격"만큼 필요하다.
///     예) 0.25초 ÷ (3프레임 = 0.05초) = 5장 + 패턴 잔상 여유 → 기본 10. 모자라면 가장 오래된 것부터 재사용돼서 꼬리가 짧게 끊겨 보인다.
///
/// ── 규칙 ─────────────────────────────────────────────────────────────────────────
///   · 기술 이름으로 분기하지 않는다(§13) — "켜져 있나" / "행동 데이터에 간격이 있나"만 본다.
///   · 찍는 시점은 CombatClock 틱 기준이라 주사율과 무관하고 F3 한 칸 전진에도 정확히 맞는다(§3).
///   · 사라지는 속도는 실제 시간(Time.deltaTime) — 순수 연출이라 틱 바깥이다. F2(timeScale 0)면 잔상도 멈춘다.
///   · 잔상 오브젝트는 시작할 때 poolSize개를 미리 만들어 돌려 쓴다 — 몇 프레임마다 만들고 지우면 가비지가 쌓인다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AfterImageEmitter : MonoBehaviour
{
    [Header("끝 자세 잔상 (패턴) — 연출값")]
    [Tooltip("잔상이 완전히 사라지기까지의 시간(초). 다음 동작 길이와 비슷하면 '다음 동작 끝날 때쯤 사라지는' 느낌이 난다")]
    [Min(0.01f)] [SerializeField] private float lifetimeSeconds = 0.5f;

    [Tooltip("찍힌 순간의 불투명도. 여기서 0까지 줄어든다")]
    [Range(0f, 1f)] [SerializeField] private float startAlpha = 0.5f;

    // 스프라이트 색에 "곱해지는" 색이다. 기본 스프라이트 셰이더가 곱셈이라 어둡게 만들기는 되지만,
    // 원래보다 밝게(흰 실루엣 등)는 안 된다 — 그게 필요하면 단색 출력 셰이더가 따로 필요하다
    [Tooltip("원래 색에 곱하는 색. 흰색이면 원래 색 그대로, 어두운 색이면 실루엣처럼 보인다")]
    [SerializeField] private Color tint = Color.white;

    [Header("꼬리 잔상 (돌진) — 연출값")]
    [Tooltip("꼬리 잔상 하나가 사라지기까지의 시간(초). 짧을수록 궤적이 짧게 따라온다")]
    [Min(0.01f)] [SerializeField] private float trailLifetimeSeconds = 0.25f;

    [Range(0f, 1f)] [SerializeField] private float trailStartAlpha = 0.6f;

    [Tooltip("꼬리 잔상 색. 어두운 보라 계열이면 레퍼런스처럼 실루엣 궤적이 된다")]
    [SerializeField] private Color trailTint = new Color(0.35f, 0.3f, 0.55f, 1f);

    [Header("공통")]
    // Awake에서 한 번만 읽는다 — 플레이 중에 바꿔도 반영 안 됨(풀은 시작할 때 만들어 두므로)
    [Tooltip("동시에 화면에 있을 수 있는 잔상 개수. 다 차면 가장 오래된 것부터 다시 쓴다. 꼬리 잔상은 '남는 시간 ÷ 간격'만큼 필요하다")]
    [Min(1)] [SerializeField] private int poolSize = 10;

    // 잔상 한 장. 찍을 때 종류(끝 자세/꼬리)에 따라 수명·불투명도·색이 달라서 장마다 따로 들고 있는다
    private class Ghost
    {
        public SpriteRenderer renderer;
        public float remaining;  // 남은 시간(초). 0 이하면 안 보이는 중(= 다시 써도 되는 칸)
        public float lifetime;   // 찍힐 때 정해진 전체 수명 — 남은 비율(remaining/lifetime)로 알파를 줄인다
        public float startAlpha;
        public Color baseColor;  // 원래 스프라이트 색 × 틴트. 여기에 알파만 매 프레임 줄여서 씌운다
    }

    private SpriteRenderer source;  // 로봇 본체 그림 — 이걸 그대로 복사해서 잔상을 만든다
    private ActionState state;
    private Ghost[] ghosts;
    private int nextGhost;          // 다음에 쓸 칸(돌아가며 씀). 다 차면 가장 오래 전에 쓴 칸으로 돌아온다
    private bool armed;             // 끝 자세 잔상을 찍을지 — 패턴 중에만 참

    /// <summary>
    /// 켜져 있는 동안 활성 구간이 끝날 때마다 끝 자세 잔상을 찍는다. 이미 찍힌 잔상은 꺼도 자연스럽게 사라진다.
    /// 꼬리 잔상은 이것과 무관하게 행동 데이터만 보고 찍는다
    /// </summary>
    // 호출: AIInputSource(패턴 시작 → 패턴의 leaveAfterImage 값 / 패턴 끝·중단 → false)
    public void SetArmed(bool value) => armed = value;

    private void Awake()
    {
        source = RobotView.FindRenderer(this); // 2026-10-28: 그림이 Visual 자식으로 분리돼서 루트 렌더러가 아니라 그림 렌더러를 본다
        ghosts = new Ghost[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            // 로봇의 자식으로 두지 않는다 — 자식이면 로봇이 움직일 때 잔상도 같이 따라와서 "남겨진" 느낌이 안 난다
            var go = new GameObject($"AfterImage_{name}_{i}");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
            ghosts[i] = new Ghost { renderer = renderer };
        }
    }

    // OnEnable/OnDisable 짝으로 구독하는 이유는 PlayerRobotBootstrap과 같다 — 꺼졌다 켜져도 중복 구독·누락이 없게
    private void OnEnable() => CombatClock.Instance.OnCombatTick += OnTick;

    private void OnDisable()
    {
        var clock = CombatClock.Existing;
        if (clock != null) clock.OnCombatTick -= OnTick;
    }

    // Start에서 구독하는 이유: ActionState는 PlayerRobotBootstrap.Awake → ActionExecutor.Init에서 만들어져서
    // Awake 시점엔 아직 없을 수 있다 (AIInputSource.Start와 같은 이유).
    // RobotView.EnsureEffectComponents가 Awake 도중에 이 컴포넌트를 AddComponent로 붙여도 Start는 정상적으로 불린다
    private void Start()
    {
        var executor = GetComponent<ActionExecutor>();
        state = executor != null ? executor.State : null;
        if (state == null)
        {
            Debug.LogWarning($"[AfterImageEmitter] {name}: ActionExecutor.State가 없음 — 잔상을 찍지 않음");
            return;
        }
        state.OnActionRecoveryStart += OnRecoveryStart;
    }

    // 잔상 오브젝트는 로봇 자식이 아니라서 로봇이 사라져도 같이 안 지워진다 — 여기서 직접 치운다
    private void OnDestroy()
    {
        if (state != null) state.OnActionRecoveryStart -= OnRecoveryStart;
        if (ghosts == null) return;
        foreach (var ghost in ghosts)
            if (ghost.renderer != null) Destroy(ghost.renderer.gameObject);
    }

    // 호출: ActionState.OnActionRecoveryStart (CombatTick 안). 끝 자세 잔상 — 패턴 중(armed)일 때만
    private void OnRecoveryStart(ActionData action)
    {
        if (armed) Snapshot(lifetimeSeconds, startAlpha, tint);
    }

    // 호출: CombatClock.OnCombatTick. 꼬리 잔상 — 활성 구간 진입 틱(FrameInPhase 0)부터 간격마다.
    // 활성 구간만 보는 이유: 돌진은 선딜 동안 제자리 예비동작이라(ActionData.advanceDuringActiveOnly) 그때 찍으면 한자리에 겹쳐 쌓인다
    private void OnTick()
    {
        if (state == null) return;
        ActionData action = state.CurrentAction;
        if (action == null || action.TrailAfterImageInterval <= 0 || state.Phase != ActionPhase.Active) return;
        if (state.FrameInPhase % action.TrailAfterImageInterval != 0) return;

        Snapshot(trailLifetimeSeconds, trailStartAlpha, trailTint);
    }

    // 지금 화면에 그려진 그대로(스프라이트·위치·회전·좌우 반전·크기)를 잔상 하나에 복사한다.
    // Animator는 붙이지 않는다 — 찍힌 순간의 그림 한 장으로 멈춰 있어야 잔상이다
    private void Snapshot(float lifetime, float alpha, Color color)
    {
        if (source.sprite == null) return;

        Ghost ghost = ghosts[nextGhost];
        nextGhost = (nextGhost + 1) % ghosts.Length;

        Transform t = ghost.renderer.transform;
        t.SetPositionAndRotation(transform.position, transform.rotation);
        // lossyScale = 부모까지 다 곱한 실제 크기. 부호도 그대로 복사되므로 로봇이 왼쪽을 보고 있었으면 잔상도 왼쪽을 본다
        // (좌우 반전 규칙을 여기서 다시 계산하지 않는다 — RobotMover가 정한 결과를 그대로 베낀다)
        t.localScale = transform.lossyScale;

        SpriteRenderer r = ghost.renderer;
        r.sprite = source.sprite;
        r.flipX = source.flipX;
        r.flipY = source.flipY;
        r.sharedMaterial = source.sharedMaterial;
        r.sortingLayerID = source.sortingLayerID;
        // 본체보다 한 칸 뒤 — 잔상이 지금 동작을 가리지 않게 (§10 정렬은 sortingOrder로)
        r.sortingOrder = source.sortingOrder - 1;

        // 피격 점멸 중에 찍히면 점멸 색까지 잔상에 남는다 — 지금은 그대로 둠(잔상은 "그 순간 보이던 것"이라)
        ghost.baseColor = source.color * color;
        ghost.lifetime = lifetime;
        ghost.startAlpha = alpha;
        ghost.remaining = lifetime;
        ApplyAlpha(ghost);
        r.enabled = true;
    }

    // 호출: Unity 매 렌더 프레임. timeScale = 0(F2 일시정지)이면 Time.deltaTime이 0이라 잔상도 같이 멈춘다
    private void Update()
    {
        foreach (var ghost in ghosts)
        {
            if (ghost.remaining <= 0f) continue;

            ghost.remaining -= Time.deltaTime;
            if (ghost.remaining <= 0f)
            {
                ghost.renderer.enabled = false; // 오브젝트는 지우지 않고 숨기기만 — 다음에 다시 쓴다
                continue;
            }
            ApplyAlpha(ghost);
        }
    }

    // 남은 시간 비율만큼 알파를 줄인다. 직선으로 줄어든다(찍힌 순간 startAlpha → 수명 끝에서 0)
    private static void ApplyAlpha(Ghost ghost)
    {
        Color c = ghost.baseColor;
        c.a *= ghost.startAlpha * (ghost.remaining / ghost.lifetime);
        ghost.renderer.color = c;
    }
}
