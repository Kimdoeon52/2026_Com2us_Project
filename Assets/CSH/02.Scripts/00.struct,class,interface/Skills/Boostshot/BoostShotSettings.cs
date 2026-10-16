
using UnityEngine;

/// <summary>부채꼴 산탄 Hit박스 생성 설정.. BoostShotActionData가 이 값으로 Hit박스를 계산한다 </summary>
[System.Serializable]
public struct BoostShotSettings
{
    [Min(1)]public int pellets;         // 산탄 수 (광선 개수)
    [Range(0f, 180f)] public float spreadDegrees; // 전체 각도(도). 정면 기준 좌우로 절반씩 퍼진다
    public float range;         // 사거리 (총구에서 광선 끝까지)
    public float thickness;     // 광선 두께. 인접 발이 사거리 끝에서 겹치도록 간격보다 크게
    public int steps;           // 거리 감쇠 단계 수 (1이면 감쇠 없음)
    public float farScale;      // 사거리 끝에서의 피해 배율 (steps가 2 이상일 때만 의미)
    public int hitLength;       // 박스가 켜져 있는 프레임 수. 1 = 활성 첫 프레임만(발사 순간 스냅샷), 0 = 활성 구간 전체

    // 부스터샷 일반 기준 기본값
    public static BoostShotSettings BoosterNormal => new BoostShotSettings
    {
        pellets = 5,
        spreadDegrees = 60f,
        range = 2.5f,
        thickness = 0.35f,
        steps = 2,
        farScale = 0.5f,
        hitLength = 1
    };
}
