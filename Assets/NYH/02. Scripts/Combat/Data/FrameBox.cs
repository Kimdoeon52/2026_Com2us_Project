using UnityEngine;

/// <summary>
/// 판정 상자 하나 (CLAUDE.md §4).
/// Hit(활성 구간당 1개) / Hurt(도트 1장당 1세트) / Push(액션당 1개) — 전부 이 구조 하나로 표현한다.
///
/// rect는 피벗(발밑 중앙) 기준 로컬 좌표다. 월드 변환·좌우 반전은 여기서 하지 않는다 —
/// BoxResolver 한 곳에서만 처리한다 (여러 곳에 흩어지면 반드시 한쪽을 빠뜨린다).
///
/// startFrame/endFrame은 이 행동의 전체 타임라인 기준(ActionState.GlobalFrame과 동일 기준,
/// Begin 후 1틱째가 1)이며 endFrame도 포함(inclusive)이다.
///
/// 아직 비어있음 — 실제 좌표값은 프레임표가 아니라 Scene 뷰에서 스프라이트를 보며 맞춰야 한다 (§4).
/// </summary>
[System.Serializable]
public struct FrameBox
{
    public BoxType type;
    public BodyPart bodyPart;   // Hurt에서만 의미 있음 — 치명타/부위 파괴 판정의 기준. Hit/Push는 무시
    public Rect rect;
    public int startFrame;
    public int endFrame;
}
