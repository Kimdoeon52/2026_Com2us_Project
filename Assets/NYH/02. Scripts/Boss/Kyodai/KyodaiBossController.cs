using System.Collections.Generic;
using RealSteel.Boss.Core;
using RealSteel.Boss.Visual;
using UnityEngine;

/// <summary>
/// 지역 2 보스 "쿄다이" — 2D 스프라이트판 (2026-11-04 전면 개편, 쿄다이_보스전_제작_가이드 맨 위 절).
/// 3D 리깅판은 _Legacy3D/KyodaiBossController3D로 보관.
///
/// KKH BossControllerBase를 그대로 탄다 — HP·페이즈·FSM(딜타임 → 패턴 → 딜타임)·그로기/스턴은 KKH 것, 여기는 쿄다이 전용만:
///   · 본체: 경기장 뒤의 큰 스프라이트(판정 없음). 천천히 숨 쉬듯 오르내린다(그림만)
///   · 손: 자식 KyodaiHand들을 전투 틱(CombatClock)에서 정해진 순서로 돌린다 — 손의 위치 = 판정 위치
///   · 눈: 자식 KyodaiLaser — 손 다음에 틱. 레이저가 손에 맞으면 그 손이 정지
///   · 패턴: 자식 기믹 컴포넌트 자동 등록. 페이즈별 패턴은 BossMasterData.phases의 availableSkillIDs로 거른다(비우면 전부)
///   · 그로기: 남은 HP 비율 문턱(groggyHpRatios)을 처음 지날 때. 그로기 동안 손은 바닥으로 떨어진다
/// </summary>
public class KyodaiBossController : BossControllerBase
{
    [Tooltip("경기장 왼쪽 끝 x(월드)")]
    [SerializeField] private float arenaMinX = -10f;

    [Tooltip("경기장 오른쪽 끝 x(월드)")]
    [SerializeField] private float arenaMaxX = 10f;

    [Tooltip("바닥 높이 y(월드) — 플레이어가 서 있는 높이")]
    [SerializeField] private float groundY;

    [Tooltip("남은 HP 비율이 이 값 이하로 처음 내려갈 때마다 그로기에 빠진다. 큰 값부터 (2026-11-02 사용자 지정: 80%·40%·20%). " +
             "그로기 시간·받는 피해 배율은 KKH InterruptToGroggy 기본값")]
    [SerializeField] private float[] groggyHpRatios = { 0.8f, 0.4f, 0.2f };

    [Header("본체 (그림만)")]
    [Tooltip("경기장 뒤 본체 스프라이트")]
    [SerializeField] private Transform body;

    [Tooltip("본체가 오르내리는 크기(월드)")]
    [SerializeField] private float bodyFloatAmplitude = 0.25f;

    [Tooltip("본체가 오르내리는 주기(초)")]
    [Min(0.1f)] [SerializeField] private float bodyFloatPeriod = 5f;

    [Header("2페이즈 (어려움·헬만 — KKH allowPhase2)")]
    [Tooltip("전환 연출 마지막에 양손이 같이 재생하는 동작(데미지 없음). 비우면 떨림만")]
    [SerializeField] private KyodaiHandMotion phaseTransitionMotion;

    [Tooltip("전환 연출 — 본체가 떨며 붉게 번쩍이는 프레임")]
    [Min(0)] [SerializeField] private int transitionShakeFrames = 120;

    [Tooltip("전환 떨림 크기(보스 단위)")]
    [Min(0f)] [SerializeField] private float transitionShakeAmount = 0.15f;

    [SerializeField] private Color transitionFlashColor = new Color(1f, 0.35f, 0.3f);

    [Tooltip("TODO: 임시값. 2페이즈 손 떠다니는 빠르기 배율 (그림만)")]
    [Min(0.1f)] [SerializeField] private float phase2FloatSpeed = 1.4f;

    [Header("팔 무력화 (기획서 부위 구성 HP 구조)")]
    [Tooltip("TODO: 기획 수치 없음. 팔(손) 하나에 이만큼 데미지가 쌓이면 무력화. 0이면 무력화 없음")]
    [Min(0)] [SerializeField] private int armDisableDamage = 300;

    [Tooltip("무력화 시간(프레임). 기획: 8초 = 480 — 손이 바닥으로 떨어져 누워 있고 그 팔 패턴은 안 나온다")]
    [Min(0)] [SerializeField] private int armDisableFrames = 480;

    [Tooltip("임시 체력 표시(KyodaiHpDebugView)를 시작할 때 붙인다 — 정식 UI가 생기면 끈다")]
    [SerializeField] private bool showDebugHp = true;

    private readonly List<BossPatternGimmickBase> allPatterns = new List<BossPatternGimmickBase>();
    private KyodaiHand[] hands = new KyodaiHand[0];
    private KyodaiLaser[] lasers = new KyodaiLaser[0];
    private KyodaiCrusher[] crushers = new KyodaiCrusher[0];
    private KyodaiTentacleSpawner[] tentacleSpawners = new KyodaiTentacleSpawner[0];
    private KyodaiShockwave shockwave;
    private SpriteRenderer bodyRenderer;
    private Color bodyColor = Color.white;

    // 2페이즈 전환 연출: -1 = 안 하는 중, 0~ = 진행 틱. 그로기 중에 50%를 지나면 그로기가 끝난 뒤로 미룬다
    private int transitionFrame = -1;
    private bool transitionSlamStarted;
    private bool transitionPending;
    private Vector3 bodyBase;
    private bool hasBodyBase;
    private bool subscribed;
    private Vector3 anchor;
    private bool hasAnchor;
    private const string AnimationHolderName = "KKH 애니메이션 (방향 전환용)";
    private int groggyStep;
    private bool defeated;

    /// <summary>
    /// 보스 크기 = 루트 스케일 (2026-11-04, 사용자: 플레이어에 맞춰 0.4). 손 동작 오프셋·판정 크기·대기 자리·레이저 굵기·
    /// 느낌 값(떠다니기 등)은 전부 "보스 단위"로 적고, 쓸 때 여기를 곱한다 — 스케일만 바꾸면 그림·움직임·판정이 같이 맞는다.
    /// 경기장·바닥·플레이어 위치는 월드 그대로
    /// </summary>
    public float Size => Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));

    /// <summary>
    /// 루트의 고정 자리 (2026-11-04). 쿄다이는 배경에 고정된 보스라 루트가 움직이면 안 되는데, KKH 상태가 딜타임마다
    /// SnapToGroundY로 루트를 옮긴다 — 프레임 끝(LateUpdate)에 여기로 되돌리고, 손·눈은 프레임 중간에도 이 자리로 계산한다.
    /// 플레이 전(에디터 미리보기)엔 지금 자리
    /// </summary>
    public Vector3 Anchor => hasAnchor ? anchor : transform.position;

    public int ArmDisableDamage => armDisableDamage;
    public int ArmDisableFrames => armDisableFrames;

    public float ArenaMinX => arenaMinX;
    public float ArenaMaxX => arenaMaxX;
    public float ArenaCenterX => (arenaMinX + arenaMaxX) * 0.5f;
    public float ArenaWidth => arenaMaxX - arenaMinX;
    public override float GroundY => groundY;
    public IReadOnlyList<float> GroggyHpRatios => groggyHpRatios;
    public IReadOnlyList<KyodaiHand> Hands => hands;

    /// <summary>플레이어 x. 플레이어가 없으면 경기장 가운데. 미리보기 중엔 가상 플레이어 x</summary>
    public float PlayerX => PlayerXOverride ?? (PlayerTransform != null ? PlayerTransform.position.x : ArenaCenterX);

    /// <summary>패턴 미리보기 창이 쓰는 가상 플레이어 x (저장 안 됨). null이면 진짜 플레이어</summary>
    public float? PlayerXOverride { get; set; }

    /// <summary>경기장 안으로 x를 자른다. halfWidth만큼 안쪽 여유를 둔다</summary>
    public float ClampToArena(float x, float halfWidth = 0f)
    {
        float min = arenaMinX + halfWidth;
        float max = arenaMaxX - halfWidth;
        return min > max ? ArenaCenterX : Mathf.Clamp(x, min, max);
    }

    // ───────────────────────── 초기화 ─────────────────────────

    // KKH InitializeBoss(base.Awake)보다 먼저 고정 자리를 잡는다
    protected override void Awake()
    {
        anchor = transform.position;
        hasAnchor = true;
        base.Awake();
    }

    // KKH FacePlayer는 애니메이션 컨트롤러가 붙은 오브젝트의 스케일 x를 뒤집는다. 루트에 붙으면(KKH 기본) 쿄다이 전체 —
    // 본체·손·눈 레이저 — 가 플레이어가 가운데를 넘을 때마다 거울로 뒤집혀, 본체가 옆으로 튀고 레이저가 하늘로 나갔다.
    // 그림 없는 빈 자식에 붙여서 방향 전환이 아무것도 안 뒤집게 한다(쿄다이는 좌우 대칭이라 바라볼 방향이 없다)
    protected override void SetupAnimationController()
    {
        if (animationController != null && animationController.transform != transform) return;
        Transform holder = transform.Find(AnimationHolderName);
        if (holder == null)
        {
            holder = new GameObject(AnimationHolderName).transform;
            holder.SetParent(transform, false);
        }
        var controller = holder.GetComponent<BossAnimationController>();
        if (controller == null) controller = holder.gameObject.AddComponent<BossAnimationController>();
        animationController = controller;
    }

    // KKH가 루트를 옮기거나(SnapToGroundY·카메라 벽 클램프) 뒤집어도(음수 스케일) 프레임 끝에 원래대로
    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (hasAnchor && transform.position != anchor) transform.position = anchor;
        Vector3 s = transform.localScale;
        if (s.x < 0f) transform.localScale = new Vector3(-s.x, s.y, s.z);
    }

    protected override void RegisterGimmicks()
    {
        // 꺼둔 패턴(오브젝트 비활성·컴포넌트 체크 해제)은 등록하지 않는다
        foreach (var gimmick in GetComponentsInChildren<BossGimmickBase>(false))
            if (gimmick.enabled) RegisterGimmick(gimmick);

        // 상시 위협(내려찍는 기물·촉수 생성기)은 데미지 방식만 패턴과 같고, 패턴 순서에는 안 넣는다 — 따로 돈다
        activePatternPool.RemoveAll(p => p is IKyodaiHazard);

        allPatterns.Clear();
        allPatterns.AddRange(activePatternPool);
        if (allPatterns.Count == 0)
            Debug.LogWarning("[Kyodai] 자식에 패턴 기믹이 하나도 없음 — 딜타임만 반복한다");
    }

    protected override void BuildPhasePatternPool(int phase)
    {
        BossPhaseData phaseData = masterData != null && masterData.phases != null
            ? masterData.phases.Find(p => p.phaseIndex == phase)
            : null;

        activePatternPool.Clear();
        foreach (var pattern in allPatterns)
        {
            bool allowed = phaseData == null || phaseData.availableSkillIDs == null || phaseData.availableSkillIDs.Count == 0
                           || phaseData.availableSkillIDs.Contains(pattern.GimmickID);
            if (allowed) activePatternPool.Add(pattern);
        }

        // 난이도별 패턴 수 제한(기획서 §6.10.4, KKH maxAllowedPatterns)보다 많으면 페이즈가 시작될 때마다 무작위로 그 수만큼 고른다
        // (2026-11-04) — KKH는 넘친 패턴을 목록 뒤에서 잘라버려서, 쿄다이처럼 패턴이 많으면 뒤쪽 패턴이 영영 안 나온다
        int cap = Snapshot != null && Snapshot.maxAllowedPatterns > 0 ? Snapshot.maxAllowedPatterns : int.MaxValue;
        if (activePatternPool.Count > cap)
        {
            for (int i = activePatternPool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (activePatternPool[i], activePatternPool[j]) = (activePatternPool[j], activePatternPool[i]);
            }
            activePatternPool.RemoveRange(cap, activePatternPool.Count - cap);
            var names = new List<string>();
            foreach (var p in activePatternPool) names.Add(p.GimmickName);
            Debug.Log($"[Kyodai] 페이즈 {phase} 패턴 (제한 {cap}개, 무작위): {string.Join(", ", names)}");
        }
        _patternIndex = 0;
    }

    // InitializeBoss가 다시 불려도 두 번 걸리지 않게 빼고 건다
    protected override void OnBossInitialized()
    {
        defeated = false;
        groggyStep = 0;

        hands = GetComponentsInChildren<KyodaiHand>(true);
        foreach (var hand in hands) hand.Init(this);
        if (hands.Length == 0) Debug.LogWarning("[Kyodai] 자식에 KyodaiHand가 없음");
        lasers = GetComponentsInChildren<KyodaiLaser>(true);
        foreach (var laser in lasers) laser.Init(this);
        crushers = GetComponentsInChildren<KyodaiCrusher>(true);
        foreach (var crusher in crushers) crusher.SetRunning(true); // 내려찍는 기물은 1페이즈부터 (2026-11-04 사용자 정정)
        tentacleSpawners = GetComponentsInChildren<KyodaiTentacleSpawner>(true);
        shockwave = GetComponentInChildren<KyodaiShockwave>(true);
        if (shockwave != null) shockwave.Init(this);
        foreach (var spawner in tentacleSpawners) spawner.SetRunning(true); // 촉수는 전투 내내 (2페이즈엔 더 빨리)
        transitionFrame = -1;
        transitionPending = false;
        if (body != null && bodyRenderer == null)
        {
            bodyRenderer = body.GetComponent<SpriteRenderer>();
            if (bodyRenderer != null) bodyColor = bodyRenderer.color;
        }

        if (body != null && !hasBodyBase)
        {
            bodyBase = body.localPosition;
            hasBodyBase = true;
        }

        OnGroggyChanged -= HandleGroggyChanged;
        OnGroggyChanged += HandleGroggyChanged;

        if (showDebugHp)
        {
            var view = GetComponent<KyodaiHpDebugView>();
            if (view == null) view = gameObject.AddComponent<KyodaiHpDebugView>();
            view.SetGroggyMarks(groggyHpRatios);
        }
    }

    private void OnEnable() => Subscribe();

    private void Start() => Subscribe(); // Awake 순서상 OnEnable 때 CombatClock이 늦게 생겨도 한 번 더

    private void OnDisable()
    {
        var clock = CombatClock.Existing;
        if (clock != null && subscribed) clock.OnCombatTick -= CombatTick;
        subscribed = false;
    }

    protected override void OnDestroy()
    {
        OnGroggyChanged -= HandleGroggyChanged;
        base.OnDestroy();
    }

    private void Subscribe()
    {
        if (subscribed || !Application.isPlaying) return;
        CombatClock.Instance.OnCombatTick += CombatTick;
        subscribed = true;
    }

    // ───────────────────────── 틱·그림 ─────────────────────────

    // 손 순서 고정(자식 순서) — 같은 입력이면 같은 결과. 레이저는 손 다음(손 위치가 최신일 때 막힘·정지를 본다)
    private void CombatTick()
    {
        foreach (var hand in hands)
            if (hand != null) hand.Tick();
        foreach (var laser in lasers)
            if (laser != null) laser.Tick();
        foreach (var crusher in crushers)
            if (crusher != null) crusher.Tick();
        foreach (var spawner in tentacleSpawners)
            if (spawner != null) spawner.Tick();
        if (shockwave != null) shockwave.Tick();
        TickTransition();
    }

    /// <summary>충격파 내려치기: x 자리에서 바닥을 따라 좌우로. 호출: KyodaiHand(spawnShockwave 키의 충격)</summary>
    public void SpawnShockwave(float x, System.Func<GameObject, bool> touch)
    {
        if (shockwave != null) shockwave.Spawn(x, touch);
        else Debug.LogWarning("[Kyodai] 충격파 키인데 KyodaiShockwave가 없음 — 메뉴 '2D 쿄다이 만들기·갱신'");
    }

    // ───────────────────────── 2페이즈 ─────────────────────────

    // KKH TransitionPhase(HP 문턱 + allowPhase2)가 부른다. 패턴 목록은 BuildPhasePatternPool이 이미 바꿔뒀다
    private void BeginPhase2()
    {
        if (defeated || Snapshot == null || Snapshot.currentHp <= 0) return;
        if (Snapshot.isGroggy)
        {
            transitionPending = true; // 그로기 연출과 겹치지 않게 그로기가 끝나고
            return;
        }
        transitionPending = false;

        // 하던 패턴을 끊고(KKH 패턴 정리 → 손 Stop) 연출 동안은 딜타임으로 붙잡아둔다 — 끝나면 KKH가 2페이즈 패턴을 고른다
        int motionFrames = phaseTransitionMotion != null ? phaseTransitionMotion.TotalFrames + 10 : 0;
        float holdSeconds = (transitionShakeFrames + motionFrames + 30) / 60f;
        var token = RenewStateCancellationToken();
        StateMachine.ChangeState(new RealSteel.Boss.FSM.BossIdleState(this, token, holdSeconds));

        foreach (var hand in hands) if (hand != null) hand.Stop();
        foreach (var laser in lasers) if (laser != null) laser.Stop();
        foreach (var crusher in crushers) if (crusher != null) crusher.SetRunning(false); // 연출 동안 쉼 (찍던 중이면 올라감)
        transitionFrame = 0;
        transitionSlamStarted = false;
        Debug.Log("[Kyodai] 2페이즈 전환 연출 시작");
    }

    // 떨림(transitionShakeFrames) → 양손 동시에 내려침(데미지 없음) → 손이 다 끝나면 2페이즈 상시 요소 켜기
    private void TickTransition()
    {
        if (transitionFrame < 0) return;
        transitionFrame++;
        if (!transitionSlamStarted && transitionFrame >= transitionShakeFrames)
        {
            transitionSlamStarted = true;
            if (phaseTransitionMotion != null)
                foreach (var hand in hands) if (hand != null) hand.Play(phaseTransitionMotion, null, null);
        }
        if (!transitionSlamStarted) return;
        foreach (var hand in hands) if (hand != null && hand.IsPlaying) return;

        transitionFrame = -1;
        foreach (var hand in hands) if (hand != null) hand.FloatSpeed = phase2FloatSpeed;
        foreach (var crusher in crushers) if (crusher != null) crusher.SetRunning(true); // 연출 동안 쉬던 것 다시
        foreach (var spawner in tentacleSpawners) if (spawner != null) spawner.SetPhase2(true);
        Debug.Log("[Kyodai] 2페이즈 시작 — 양손 패턴·촉수 빨리");
    }


    protected override void Update()
    {
        base.Update();
        if (body == null || !hasBodyBase) return;
        float y = bodyFloatAmplitude * Mathf.Sin(Time.time * Mathf.PI * 2f / bodyFloatPeriod);
        Vector3 offset = new Vector3(0f, defeated ? -bodyFloatAmplitude * 2f : y, 0f);

        // 2페이즈 전환 떨림 구간: 본체가 덜덜 떨고 붉게 번쩍인다 (본체는 보스 루트 밑이라 로컬 1 = 보스 단위)
        bool shaking = transitionFrame >= 0 && !transitionSlamStarted;
        if (shaking) offset += (Vector3)(Random.insideUnitCircle * transitionShakeAmount);
        body.localPosition = bodyBase + offset;
        if (bodyRenderer != null)
        {
            float pulse = shaking ? 0.5f + 0.5f * Mathf.Sin(Time.time * 18f) : 0f;
            Color flash = transitionFlashColor;
            flash.a = bodyColor.a;
            bodyRenderer.color = Color.Lerp(bodyColor, flash, pulse);
        }
    }

    // ───────────────────────── 손 고르기 ─────────────────────────

    /// <summary>
    /// 패턴이 쓸 손. 재생 중·정지 중·쓰러진 손은 되도록 피한다(다 그러면 원래 고른 손).
    /// alternate: 번갈아 쓰기용 카운터 (패턴마다 따로)
    /// </summary>
    public KyodaiHand PickHand(KyodaiHandChoice choice, ref int alternate)
    {
        if (hands.Length == 0) return null;
        KyodaiHand left = FindHand(KyodaiHandSide.Left), right = FindHand(KyodaiHandSide.Right);
        KyodaiHand first, second;
        switch (choice)
        {
            case KyodaiHandChoice.Left: first = left; second = null; break;
            case KyodaiHandChoice.Right: first = right; second = null; break;
            case KyodaiHandChoice.NearestToPlayer:
            case KyodaiHandChoice.FarthestFromPlayer: // 경기장 반대쪽 끝에서 출발하는 동작(손바닥 밀기)
                bool leftCloser = left != null && (right == null || Mathf.Abs(left.Home.x - PlayerX) <= Mathf.Abs(right.Home.x - PlayerX));
                bool pickLeft = choice == KyodaiHandChoice.NearestToPlayer ? leftCloser : !leftCloser && left != null;
                first = pickLeft ? left : right;
                second = pickLeft ? right : left;
                break;
            default: // Alternate
                first = alternate % 2 == 0 ? left : right;
                second = alternate % 2 == 0 ? right : left;
                alternate++;
                break;
        }
        if (first == null) first = second;
        if (first != null && !IsFree(first) && second != null && IsFree(second)) return second;
        return first != null ? first : hands[0];
    }

    private static bool IsFree(KyodaiHand hand) => !hand.IsPlaying && !hand.IsFrozen && !hand.IsDown;

    private KyodaiHand FindHand(KyodaiHandSide side)
    {
        foreach (var hand in hands) if (hand != null && hand.Side == side) return hand;
        return null;
    }

    // ───────────────────────── 피해·그로기·격파 ─────────────────────────

    protected override void OnPhaseChanged(int prevPhase, int newPhase)
    {
        Debug.Log($"[Kyodai] 페이즈 {prevPhase} → {newPhase}");
        if (newPhase >= 2 && prevPhase < 2) BeginPhase2();
    }

    // 허브 경유(HitDetection)든 TakeDamage든 피해 뒤에는 여기를 지난다
    protected override void ProcessPostDamageReactions(int finalDamage)
    {
        base.ProcessPostDamageReactions(finalDamage);
        TryHpGroggy();
    }

    // 한 방에 문턱을 여러 개 넘으면 그로기는 한 번만. 이미 그로기 중에 넘은 문턱은 그냥 지나간다
    private void TryHpGroggy()
    {
        if (defeated || Snapshot == null || Snapshot.maxHp <= 0 || Snapshot.currentHp <= 0) return;
        float ratio = Snapshot.currentHp / (float)Snapshot.maxHp;
        bool crossed = false;
        while (groggyStep < groggyHpRatios.Length && ratio <= groggyHpRatios[groggyStep])
        {
            groggyStep++;
            crossed = true;
        }
        if (!crossed || Snapshot.isGroggy) return;

        Debug.Log($"[Kyodai] HP {ratio:P0} — 그로기");
        InterruptToGroggy();
    }

    private void HandleGroggyChanged(bool groggy, float duration)
    {
        if (defeated) return;
        foreach (var hand in hands) if (hand != null) hand.SetDown(groggy);
        if (groggy) foreach (var laser in lasers) if (laser != null) laser.Stop();

        foreach (var spawner in tentacleSpawners) if (spawner != null) spawner.SetPaused(groggy);
        // 그로기 동안 기물은 쉬고, 전환 연출 도중이면 끊었다가 그로기가 끝난 뒤 다시
        foreach (var crusher in crushers) if (crusher != null) crusher.SetRunning(!groggy && transitionFrame < 0 && !transitionPending);
        if (groggy && transitionFrame >= 0)
        {
            transitionFrame = -1;
            transitionPending = true;
        }
        if (!groggy && transitionPending) BeginPhase2();
    }

    protected override void OnBossDefeated()
    {
        Debug.Log("[Kyodai] 쿄다이 격파");
        defeated = true;
        foreach (var hand in hands) if (hand != null) hand.Kill();
        foreach (var laser in lasers) if (laser != null) laser.Stop();
        foreach (var crusher in crushers) if (crusher != null) crusher.SetRunning(false);
        foreach (var spawner in tentacleSpawners) if (spawner != null) spawner.KillAll();
        if (shockwave != null) shockwave.Clear();
        transitionFrame = -1;
        transitionPending = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(arenaMinX, groundY), new Vector3(arenaMaxX, groundY));
        Gizmos.DrawLine(new Vector3(arenaMinX, groundY), new Vector3(arenaMinX, groundY + 3f));
        Gizmos.DrawLine(new Vector3(arenaMaxX, groundY), new Vector3(arenaMaxX, groundY + 3f));
    }
#endif
}
