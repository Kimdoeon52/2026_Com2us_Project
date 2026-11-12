/// <summary>
/// 쿄다이 패턴이 쓰는 부위 (2026-11-12 동시 패턴). 같이 나올 패턴을 고를 때 부위가 안 겹치는 것만 고른다:
/// 손을 쓰는 패턴끼리는 같이 안 나온다(손·바닥 예고 그림자를 서로 뺏는다), 레이저는 남은 어깨 수만큼만.
/// </summary>
public interface IKyodaiPatternUse
{
    /// <summary>손을 쓰는지 (동반 손 동작이 있는 레이저 포함)</summary>
    bool UsesHands { get; }

    /// <summary>필요한 손 수 (양손 패턴 2)</summary>
    int HandsNeeded { get; }

    /// <summary>쏘는 레이저(어깨) 수</summary>
    int LasersNeeded { get; }
}

/// <summary>
/// 쿄다이 "상시 위협" 표시 (2026-11-04) — 내려찍는 기물·촉수 생성기처럼 패턴 순서와 따로 도는 것.
/// 데미지를 KKH 방식으로 내려고 BossPatternGimmickBase를 상속하지만, KyodaiBossController가 등록 직후 패턴 목록에서 뺀다.
/// (2026-11-12 IKyodaiHazard.cs에서 이 파일로 합침 — 쿄다이 패턴 분류용 인터페이스를 한 곳에)
/// </summary>
public interface IKyodaiHazard
{
}
