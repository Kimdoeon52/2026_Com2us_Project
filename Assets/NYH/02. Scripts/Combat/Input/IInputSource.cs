/// <summary>
/// 플레이어와 AI를 가르는 유일한 지점 (CLAUDE.md §8).
/// 로봇 본체는 하나 — 입력만 바꿔 끼우면 플레이어도 되고 보스도 된다.
/// PlayerInputSource(키보드) / AIInputSource(패턴)는 별도 파일로 구현 예정 (§11-9).
/// </summary>
public interface IInputSource
{
    /// <summary>이번 틱에 내고 싶은 행동. 없으면 null</summary>
    ActionData GetDesiredAction();

    /// <summary>-1 ~ 1. 좌우 이동 입력</summary>
    float GetMoveInput();
}
