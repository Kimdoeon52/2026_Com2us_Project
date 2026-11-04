using System.Collections.Generic;
using RealSteel.Boss.Core;
using UnityEngine;

/// <summary>
/// 지역 2 보스 "쿄다이" — 2D 스프라이트판 (2026-11-04 전면 개편, 쿄다이_보스전_제작_가이드 맨 위 절).
/// 3D 리깅판은 _Legacy3D/KyodaiBossController3D로 보관.
///
/// KKH BossControllerBase를 그대로 탄다 — HP·페이즈·FSM(딜타임 → 패턴 → 딜타임)·그로기/스턴은 KKH 것, 여기는 쿄다이 전용만:
///   · 본체: 경기장 뒤의 큰 스프라이트(판정 없음). 천천히 숨 쉬듯 오르내린다(그림만)
///   · 손: 자식 KyodaiHand들을 전투 틱(CombatClock)에서 정해진 순서로 돌린다 — 손의 위치 = 판정 위치
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

    [Tooltip("임시 체력 표시(KyodaiHpDebugView)를 시작할 때 붙인다 — 정식 UI가 생기면 끈다")]
    [SerializeField] private bool showDebugHp = true;

    private readonly List<BossPatternGimmickBase> allPatterns = new List<BossPatternGimmickBase>();
    private KyodaiHand[] hands = new KyodaiHand[0];
    private Vector3 bodyBase;
    private bool hasBodyBase;
    private bool subscribed;
    private int groggyStep;
    private bool defeated;

    public float ArenaMinX => arenaMinX;
    public float ArenaMaxX => arenaMaxX;
    public float ArenaCenterX => (arenaMinX + arenaMaxX) * 0.5f;
    public float ArenaWidth => arenaMaxX - arenaMinX;
    public override float GroundY => groundY;
    public IReadOnlyList<float> GroggyHpRatios => groggyHpRatios;
    public IReadOnlyList<KyodaiHand> Hands => hands;

    /// <summary>플레이어 x. 플레이어가 없으면 경기장 가운데</summary>
    public float PlayerX => PlayerTransform != null ? PlayerTransform.position.x : ArenaCenterX;

    /// <summary>경기장 안으로 x를 자른다. halfWidth만큼 안쪽 여유를 둔다</summary>
    public float ClampToArena(float x, float halfWidth = 0f)
    {
        float min = arenaMinX + halfWidth;
        float max = arenaMaxX - halfWidth;
        return min > max ? ArenaCenterX : Mathf.Clamp(x, min, max);
    }

    // ───────────────────────── 초기화 ─────────────────────────

    protected override void RegisterGimmicks()
    {
        // 꺼둔 패턴(오브젝트 비활성·컴포넌트 체크 해제)은 등록하지 않는다
        foreach (var gimmick in GetComponentsInChildren<BossGimmickBase>(false))
            if (gimmick.enabled) RegisterGimmick(gimmick);

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

    // 손 순서 고정(자식 순서) — 같은 입력이면 같은 결과
    private void CombatTick()
    {
        foreach (var hand in hands)
            if (hand != null) hand.Tick();
    }

    protected override void Update()
    {
        base.Update();
        if (body == null || !hasBodyBase) return;
        float y = bodyFloatAmplitude * Mathf.Sin(Time.time * Mathf.PI * 2f / bodyFloatPeriod);
        body.localPosition = bodyBase + new Vector3(0f, defeated ? -bodyFloatAmplitude * 2f : y, 0f);
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
                bool leftCloser = left != null && (right == null || Mathf.Abs(left.Home.x - PlayerX) <= Mathf.Abs(right.Home.x - PlayerX));
                first = leftCloser ? left : right;
                second = leftCloser ? right : left;
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
    }

    protected override void OnBossDefeated()
    {
        Debug.Log("[Kyodai] 쿄다이 격파");
        defeated = true;
        foreach (var hand in hands) if (hand != null) hand.Kill();
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
