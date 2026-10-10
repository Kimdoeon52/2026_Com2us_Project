using System;
using System.Threading;
using UnityEngine;
using RealSteel.Boss.FSM;

/// <summary>
/// [보스 총괄 컨트롤러 (BossController)]
/// 보스 오브젝트의 최상위 MonoBehaviour 컴포넌트임.
/// BossSnapshot 및 BossStateMachine의 수명주기를 관리하며, 기믹/전투 엔진의 인터럽트를 수신함.
/// </summary>
public class BossController : MonoBehaviour
{
    [Header("1. 보스 데이터 원장 에셋")]
    [SerializeField] private BossMasterData masterData;

    [Header("2. 디버그 모니터링")]
    [SerializeField] private string currentStateName;
    [SerializeField] private int currentHpView;
    [SerializeField] private bool isGroggyView;

    public BossMasterData MasterData => masterData;
    public BossSnapshot Snapshot { get; private set; }
    public BossStateMachine StateMachine { get; private set; }

    private CancellationTokenSource _stateCts;
    private int _patternIndex = 0;

    // 이벤트 델리게이트
    public event Action<string> OnStateChanged;
    public event Action<int, int> OnHpChanged;
    public event Action<bool, float> OnGroggyChanged;
    public event Action<bool, float> OnStunChanged;
    public event Action<int, int> OnDpsCheckProgressChanged;
    public event Action<BossPatternData> OnPatternStarted;

    private void Awake()
    {
        InitializeSnapshot();
        InitializeStateMachine();
    }

    private void Update()
    {
        StateMachine?.Update();

        // 인스펙터 디버깅 갱신
        if (Snapshot != null)
        {
            currentHpView = Snapshot.currentHp;
            isGroggyView = Snapshot.isGroggy;
        }
        if (StateMachine?.CurrentState != null)
        {
            currentStateName = StateMachine.CurrentState.StateName;
        }
    }

    private void OnDestroy()
    {
        CancelCurrentStateToken();
    }

    /// <summary>
    /// 마스터 데이터를 기반으로 런타임 스냅샷을 생성 및 등록함
    /// </summary>
    public void InitializeSnapshot()
    {
        if (masterData == null)
        {
            Debug.LogError("[BossController] BossMasterData가 지정되지 않았습니다!");
            return;
        }

        Snapshot = new BossSnapshot
        {
            bossID = masterData.bossID,
            bossName = masterData.bossName,
            maxHp = masterData.maxHp,
            currentHp = masterData.maxHp,
            baseDefense = masterData.baseDefense,
            currentPhase = 1,
            isGroggy = false,
            currentDamageMultiplier = 1.0f
        };

        // 기믹/부위 상태 초기화
        foreach (var gimmick in masterData.gimmickParts)
        {
            Snapshot.gimmickStates[gimmick.gimmickID] = new BossGimmickRuntimeState(
                gimmick.gimmickID,
                gimmick.gimmickName,
                gimmick.isPhysicalBossPart,
                gimmick.maxDurability
            );
        }

        Debug.Log($"[BossController] {Snapshot.bossName} 스냅샷 초기화 완료 (HP: {Snapshot.maxHp}, 방어력: {Snapshot.baseDefense})");
    }

    /// <summary>
    /// 상태 머신 초기화 및 딜타임(IdleState) 진입
    /// </summary>
    public void InitializeStateMachine()
    {
        StateMachine = new BossStateMachine();
        StateMachine.OnStateChanged += (prev, next) =>
        {
            OnStateChanged?.Invoke(next);
            Debug.Log($"[BossController] 상태 전이: {prev ?? "None"} ➔ {next}");
        };

        // 초기 시작 상태: 1.0초 확정 딜타임
        var token = RenewStateCancellationToken();
        StateMachine.Initialize(new BossIdleState(this, token, masterData != null ? masterData.patternPostDelay : 1.0f));
    }

    /// <summary>
    /// 비동기 상태용 CancellationToken을 갱신하고 기존 토큰을 안전하게 취소함
    /// </summary>
    public CancellationToken RenewStateCancellationToken()
    {
        CancelCurrentStateToken();
        _stateCts = new CancellationTokenSource();
        return _stateCts.Token;
    }

    public void CancelCurrentStateToken()
    {
        if (_stateCts != null)
        {
            if (!_stateCts.IsCancellationRequested)
            {
                _stateCts.Cancel();
            }
            _stateCts.Dispose();
            _stateCts = null;
        }
    }

    /// <summary>
    /// 다음 순서의 공격 패턴 데이터를 순환 또는 가중치 선택하여 반환함
    /// </summary>
    public BossPatternData GetNextPattern()
    {
        if (masterData == null || masterData.patterns == null || masterData.patterns.Count == 0)
        {
            return null;
        }

        // 사용 가능한(봉인되지 않은) 패턴 필터링
        var validPatterns = masterData.patterns.FindAll(p => p != null && !Snapshot.IsSkillSealed(p.patternID));
        if (validPatterns.Count == 0)
        {
            validPatterns = masterData.patterns;
        }

        var pattern = validPatterns[_patternIndex % validPatterns.Count];
        _patternIndex++;
        return pattern;
    }

    /// <summary>
    /// 기믹 파훼(못판 착지 등) 시 외부에서 호출되어 진행 중인 공격을 즉시 취소하고 4초 그로기로 전이
    /// </summary>
    public void InterruptToGroggy(float duration = 4.0f, float damageMultiplier = 1.5f)
    {
        var token = RenewStateCancellationToken();
        StateMachine.ChangeState(new BossGroggyState(this, token, duration, damageMultiplier));
    }

    /// <summary>
    /// DPS 체크 저지 성공(300 피해 누적) 시 호출되어 3초 스턴으로 전이
    /// </summary>
    public void InterruptToStun(float duration = 3.0f)
    {
        var token = RenewStateCancellationToken();
        StateMachine.ChangeState(new BossStunState(this, token, duration));
    }

    /// <summary>
    /// 보스 피격 처리 및 상태 머신 대미지 누적 이벤트 전달
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (Snapshot == null || !Snapshot.IsAlive) return;

        // 그로기 중 피해 배율 적용
        float multiplier = Snapshot.isGroggy ? Snapshot.currentDamageMultiplier : 1.0f;
        int finalDamage = Mathf.RoundToInt(damage * multiplier);

        Snapshot.currentHp = Mathf.Max(0, Snapshot.currentHp - finalDamage);
        OnHpChanged?.Invoke(Snapshot.currentHp, Snapshot.maxHp);

        // 현재 상태(전조 등)에 피격 대미지 전달
        StateMachine.OnDamageTaken(finalDamage);

        if (Snapshot.currentHp <= 0)
        {
            Debug.Log($"<color=red>[BossController] {Snapshot.bossName} 토벌 완료!</color>");
        }
    }

    public void NotifyDpsCheckProgress(int currentDamage, int requiredDamage)
    {
        OnDpsCheckProgressChanged?.Invoke(currentDamage, requiredDamage);
    }

    public void NotifyPatternStarted(BossPatternData pattern)
    {
        OnPatternStarted?.Invoke(pattern);
    }

    public void SetGroggyView(bool isGroggy, float duration)
    {
        Snapshot.isGroggy = isGroggy;
        OnGroggyChanged?.Invoke(isGroggy, duration);
    }

    public void SetStunView(bool isStun, float duration)
    {
        OnStunChanged?.Invoke(isStun, duration);
    }
}
