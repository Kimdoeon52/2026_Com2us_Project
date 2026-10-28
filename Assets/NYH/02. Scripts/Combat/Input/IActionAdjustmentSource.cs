/// <summary>
/// 입력원이 "이 행동은 이번에만 이만큼 빠르게/느리게" 를 덧붙이고 싶을 때 같이 구현하는 선택 인터페이스 (2026-10-11).
///
/// ── 왜 필요한가 ───────────────────────────────────────────────────────────────────
///   적 연속 공격 패턴(하단 → 어퍼컷 → 중단)을 "패턴으로 쓸 때만" 빠르게 하고 싶었다. 같은 Skill1이라도 단독으로 쓸 땐 원래 속도.
///   ActionData(SO)를 고치면 원본 에셋이 바뀌어 단독 사용까지 빨라지고 에디터에 값이 영구히 남는다(§2 금지).
///   그래서 "이번 실행분만" 보정하는 FrameAdjustment를 행동 시작 순간에 넘긴다.
///
/// ── 흐름 ─────────────────────────────────────────────────────────────────────────
///   EnemyAIProfile.PatternEntry.speed (예: 1.5)
///     → AIInputSource.GetAdjustment(action): 지금 패턴이 내준 단계면 SpeedMultiplier = 1 / 1.5
///     → ActionExecutor.ExecuteTick: 입력원이 이 인터페이스를 구현했으면 물어봐서 ActionState.Begin(action, extra)에 넘김
///     → ResolvedAction.Resolve: 선딜·활성·후딜 프레임에 배율을 곱해 이번 실행 프레임 확정 (10/5/15 → 7/3/10)
///     → 그 확정 프레임(Timeline)을 RobotView(애니메이션 속도)·박스 창·전진 거리 분배가 전부 따라감 — 따로 맞출 곳 없음
///   장비 보정(CSH ResolvedModifiers → BuildAdjustment)과 동시에 걸리면 FrameAdjustment.Combine으로 합쳐진다(가감은 더하고 배율은 곱함).
///
/// ── 왜 IInputSource에 직접 안 넣었나 ─────────────────────────────────────────────
///   필요한 건 지금 AI 패턴 하나뿐인데, IInputSource에 넣으면 PlayerInputSource 같은 플레이어 입력원까지 전부
///   빈 구현을 달아야 한다. ActionExecutor가 "입력원이 이것도 구현했으면" 물어보는 선택 인터페이스로 뒀다.
///   나중에 플레이어 쪽에도 필요해지면(예: 특정 콤보 중 가속) PlayerInputSource가 이걸 구현하기만 하면 된다.
///
/// ── 왜 "미리 맡겨두기"가 아니라 "시작할 때 물어보기"인가 ──────────────────────────
///   "다음 행동에 걸 보정"을 ActionExecutor에 미리 맡겨두는 방식은, 그 행동이 경직 등으로 취소되면
///   보정이 남아 있다가 엉뚱한 다음 행동(예: 피격 행동)에 묻어 나간다. 행동을 시작하는 바로 그 순간에
///   "이 행동이 맞냐"를 확인하고 받으면 그런 일이 생길 수 없다.
/// </summary>
public interface IActionAdjustmentSource
{
    /// <summary>
    /// action을 지금 시작할 때 덧붙일 보정. 없으면 반드시 FrameAdjustment.None을 돌려줄 것 —
    /// default(FrameAdjustment)는 SpeedMultiplier가 0이라 (ResolvedAction이 막아주긴 하지만) 의도와 다르다
    /// </summary>
    // 호출: ActionExecutor.ExecuteTick (행동을 실제로 시작하기 직전, 부위 파손 게이트까지 통과한 뒤)
    FrameAdjustment GetAdjustment(ActionData action);
}
