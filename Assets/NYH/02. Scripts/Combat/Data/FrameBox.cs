using UnityEngine;

/// <summary>
/// 판정 상자 하나. Hit(공격이 닿는 범위) / Hurt(맞는 범위) / Push(몸통끼리 못 지나가게 미는 상자)를
/// 전부 이 구조체 하나로 표현한다. 종류마다 클래스를 따로 만들면 셋 다 똑같은 필드(좌표, 프레임 구간)를
/// 복붙하게 되고, 이 박스를 다루는 쪽(HitDetection, BoxDrawer)에서도 타입마다 분기 코드가 늘어난다 —
/// 그래서 "무슨 종류인지"는 코드 구조가 아니라 아래 type 필드 값 하나로만 구분한다.
/// </summary>
[System.Serializable] // 이 표시가 있어야 인스펙터에 필드가 보이고, ActionData 에셋(.asset) 파일에 값이 저장된다
public struct FrameBox
{
    // Hit / Hurt / Push 중 뭔지. 이 값 하나로 판정 로직(HitDetection이 만들어지면 거기서)과
    // 표시 색(BoxDrawer.ColorFor)이 전부 갈린다 — 그래서 이 한 필드가 이 구조체의 핵심이다
    public BoxType type;

    // 이 박스가 몸의 어느 부위인지(Head/LeftArm/...). Hurt 박스에서만 의미가 있다.
    // 예: "머리 Hurt박스에 맞았을 때만 치명타로 친다", "팔이 파괴되면 그 팔의 Hurt박스를 없앤다"처럼
    // 부위별로 다르게 반응해야 하는 판정에 쓰인다. Hit/Push 박스는 부위 구분이 필요 없어서 이 값을 그냥 안 읽는다
    public BodyPart bodyPart;

    // 박스의 위치와 크기. 캐릭터 피벗(발밑 중앙) 기준 "로컬" 좌표로 저장한다.
    // 월드 좌표로 저장하면 캐릭터가 한 걸음만 움직여도 모든 박스 좌표를 다시 계산해야 하고,
    // 왼쪽을 볼 때 좌우 반전도 박스마다 따로 처리해야 해서 어딘가 하나는 빠뜨리기 쉽다.
    // 로컬로만 저장해두면 "지금 위치 + 이 로컬값"으로 월드 좌표를 그때그때 계산하기만 하면 되고,
    // 그 계산 자체도 BoxResolver라는 한 곳에만 몰아둘 수 있다
    public Rect rect;

    // 이 박스가 어느 구간에 붙는가. Legacy면 아래 startFrame/endFrame 절대값을 그대로 쓴다.
    // 절대 프레임은 "활성 구간이 언제인지"를 ActionData의 프레임 값에서 손으로 계산해 옮겨적은 사본이라,
    // 선딜 프레임 하나만 바뀌어도 조용히 어긋난다. 앵커는 그 사본을 없애고 구간을 가리키기만 한다
    public FrameAnchor anchor;

    // 앵커한 구간에 진입한 뒤 몇 프레임째부터 켜지는지. 0이면 구간 시작과 동시에 켜진다
    public int startOffset;

    // 몇 프레임 동안 켜져 있는지. 0이면 그 구간이 끝날 때까지 자동으로 따라간다 —
    // "활성 구간 전체"처럼 길이를 구간에 맡기고 싶은 경우가 대부분이라 이게 기본값이다
    public int length;

    // 이 박스가 몇 번째 프레임부터 활성화되는지 — ActionState.GlobalFrame(행동이 시작된 뒤로
    // Phase(선딜/활성/후딜)가 바뀌어도 리셋되지 않고 계속 올라가는 전체 타임라인 카운터)과 같은 기준값이다.
    // 예: 잽(선딜 4·활성 2·후딜 6)의 Hit박스가 활성 구간에만 나가야 한다면 startFrame=5(선딜이 끝난 다음 프레임)
    public int startFrame;

    // 몇 번째 프레임까지 활성화되는지. endFrame 자기 자신도 포함해서 비교한다 (ActionData.GetActiveBoxes에서
    // globalFrame >= startFrame && globalFrame <= endFrame 로 검사하는 것 참고 — 이상·이하라 양끝 다 포함)
    public int endFrame;
}
