using System;
using UnityEngine;

/// <summary>
/// 로봇 하나에 붙는 실행기 (3층 · 시스템). 개수 고정 — 기술이 몇 개든 이 클래스는 하나다.
/// 이 클래스는 지금 진행 중인 게 무슨 기술인지 몰라도 선딜 → 판정 → 후딜을 돌릴 수 있어야 한다 (§1).
///
/// CombatClock.CombatTick()에서 씬에 있는 로봇 수만큼 ExecuteTick()이 호출된다.
/// Update()에서 직접 프레임을 세지 않는다 — 반드시 CombatTick 경유 (§3).
/// </summary>
public class ActionExecutor : MonoBehaviour
{
    // 걷기·대기 중엔 ActionState.CurrentAction이 계속 null이라 판정용 박스를 못 구하는데,
    // 그렇다고 그 동안 맞을 수도 없고 몸통도 없으면 격투 게임으로서 말이 안 되므로, 이 기간에
    // 대신 쓸 허트/푸시박스를 담은 전용 ActionData를 인스펙터에서 미리 꽂아둔다 (ActionState 생성자로 그대로 전달됨)
    [Tooltip("걷기·대기 중(행동 없음) 허트/푸시박스로 쓸 ActionData. Hit박스는 없이 Hurt/Push만 담아야 한다")]
    [SerializeField] private ActionData idleAction;

    // 사람이 조작하는지 AI가 조작하는지 몰라도 되도록 인터페이스로만 들고 있는다 (§8)
    private IInputSource inputSource;
    // 이 로봇의 "지금 뭘 하고 있는가" 전체를 담는 상태 객체. Init()에서 idleAction과 함께 생성된다
    // (필드 선언 시점에 바로 new하지 않는 이유: idleAction이 인스펙터 값이라 Init이 불릴 때까진 확정되지 않기 때문)
    private ActionState state;

    /// <summary>HitDetection/RobotMover/PlayerRobotBootstrap이 이 로봇의 행동·프레임 상태를 읽는 유일한 통로.</summary>
    public ActionState State => state;

    /// <summary>
    /// CombatDataHub/BattleManager(KKH) API가 조회 키로 쓰는 식별자 ("Player" / "Enemy").
    /// PlayerRobotBootstrap.Awake에서 주입된다. HitDetection 등 다른 3층 시스템도
    /// GetComponent&lt;ActionExecutor&gt;().FighterId로 이 값을 가져다 쓴다.
    /// </summary>
    // 왜 GameObject 참조가 아니라 문자열인가: KKH의 CombatDataHub API 전체가 "Player"/"Enemy" 문자열로
    // 로봇을 구분하도록 이미 만들어져 있어서(§14), NYH 쪽도 같은 키를 그대로 들고 있어야 서로 연결된다
    public string FighterId { get; private set; }

    // 호출: PlayerRobotBootstrap.Awake. 받음: 입력원(사람인지 AI인지 몰라도 되게 인터페이스로 받는다), fighterId(CombatDataHub 조회 키)
    // (2026-09-29: 가드가 기동 행동에서 삭제되면서 방향 기반 가드 판정용으로 받던 mover 파라미터는 더 이상 필요 없어 제거함 — CLAUDE_1.md §5·§15)
    public void Init(IInputSource source, string fighterId)
    {
        inputSource = source;
        FighterId = fighterId;
        // 여기서 처음으로 state를 만든다 — idleAction(인스펙터 값)이 이 시점엔 이미 확정돼 있으므로 안전하게 넘길 수 있다
        state = new ActionState(idleAction);
    }

    // 호출: CombatClock.OnCombatTick (1/60초마다). 순서: Idle이면 입력 확인 → state.Begin으로 시작 → state.Advance로 프레임 +1
    public void ExecuteTick()
    {
        // Idle일 때만 새 행동을 받아들인다 — 공격 도중(선딜/활성/후딜)에 입력이 들어와도 씹히는 게
        // 정상 동작이고, 그 판단 기준(CanAcceptNewAction)은 ActionState가 Phase로 이미 갖고 있으므로 여기선 그냥 물어보기만 한다
        if (state.CanAcceptNewAction)
        {
            // 입력원한테 "이번 틱에 하고 싶은 행동이 있냐"고 물어본다. 없으면 null이 온다(§8 IInputSource 계약)
            ActionData requested = inputSource?.GetDesiredAction();
            if (requested != null)
            {
                requested = ResolveReplacement(requested); // 최. 추가
                state.Begin(requested);
                // 구간별로 프레임 수를 전부 찍어두면, 나중에 "훅이 정말 선딜 3프레임에 나가는지" 같은
                // 걸 로그만 보고도 검증할 수 있다 (§9 "상태 로그" 규칙).
                // 원본이 아니라 확정값(Timeline)을 찍는다 — 보정이 걸린 뒤엔 실제로 도는 값이 이쪽이다
                ResolvedAction timeline = state.Timeline;
                Debug.Log($"[{requested.ActionName}] Startup {timeline.StartupFrames}f → Active {timeline.ActiveFrames}f → Recovery {timeline.RecoveryFrames}f");
            }
        }

        // 입력을 받았든 안 받았든 매 틱 반드시 한 번은 프레임을 전진시켜야 한다 (안 그러면 시간이 안 흐름)
        state.Advance();
    }

    #region 최.추가
    private EquipmentEffectSet effects;

    public void SetEffects(EquipmentEffectSet set)
    {
        if (state == null)
        {
            Debug.LogWarning("[ActionExecutor] SetEffects 호출 시점에 state가 아직 없음 - Init(PlayerRobotBootstrap.Awake) 이후에 호출해야 함");
            return;
        }

        effects?.Unequip();

        effects = set;
        state.SetModifierResolver(set != null ? (Func<ActionData, ResolvedModifiers>)ResolveModifiers : null);

        effects?.Equip();
    }

    private void OnDestroy()
    {
        effects?.Unequip();
        effects = null;
    }

    private ResolvedModifiers ResolveModifiers(ActionData action)
    {
        return effects != null ? effects.Resolve(action, IsPartBroken) : ResolvedModifiers.Identity;
    }

    private ActionData ResolveReplacement(ActionData requested)
    {
        if (effects == null) return requested;

        ActionData replaced = effects.ResolveReplacement(requested, IsPartBroken);
        if (replaced == requested) return requested;

        var hub = CombatDataHub.Instance;
        if (hub != null && !hub.CanExecuteAction(FighterId, replaced)) return requested;

        return replaced;
    }

    private bool IsPartBroken(BodyPart part)
    {
        var hub = CombatDataHub.Instance;
        return hub != null && hub.GetSnapshot(FighterId) != null && hub.IsPartBroken(FighterId, part);
    }


    #endregion
}
