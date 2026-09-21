/// <summary>
/// 플레이어와 AI를 가르는 유일한 지점 (CLAUDE.md §8).
/// 로봇 본체는 하나 — 입력만 바꿔 끼우면 플레이어도 되고 보스도 된다.
/// PlayerInputSource(키보드) / AIInputSource(패턴)는 별도 파일로 구현 예정 (§11-9).
///
/// 왜 인터페이스로 뽑아뒀는가: ActionExecutor/RobotMover 같은 실행부는 "지금 입력이 사람 손에서
/// 오는지 AI 알고리즘에서 오는지" 전혀 몰라도 동작해야 한다. 만약 이 부분을 PlayerInputSource 타입으로
/// 직접 받았다면, 나중에 AIInputSource를 만들 때 ActionExecutor 코드 자체를 수정해야 했을 것이다.
/// 인터페이스 하나만 만족하면 되게 해두면, 새 입력 방식이 추가돼도 이 파일과 구현체만 늘어나고
/// 실행부 코드는 한 줄도 안 바뀐다.
/// </summary>
public interface IInputSource
{
    /// <summary>이번 틱에 내고 싶은 행동. 없으면 null</summary>
    // ActionData를 직접 반환하는 이유: "무슨 키를 눌렀는지"가 아니라 "무슨 행동을 하고 싶은지"를
    // 돌려줘야 ActionExecutor가 그대로 ActionState.Begin()에 넘길 수 있다. 키 코드(KeyCode)를 반환했다면
    // ActionExecutor가 "이 키는 무슨 기술이지?"를 다시 판단해야 했을 텐데, 그건 입력 방식(키보드/AI)마다
    // 다른 로직이라 구현체(PlayerInputSource 등) 쪽 책임으로 미리 넘겨둔 것
    ActionData GetDesiredAction();

    /// <summary>-1 ~ 1. 좌우 이동 입력</summary>
    // bool 두 개(MoveLeft/MoveRight) 대신 float 하나로 만든 이유: 나중에 아날로그 입력(게임패드 스틱 등)이
    // 생겨도 인터페이스를 안 바꿔도 되고, 왼쪽·오른쪽을 동시에 누른 경우의 애매한 처리(둘 다 true?)도 애초에 안 생긴다
    float GetMoveInput();
}
