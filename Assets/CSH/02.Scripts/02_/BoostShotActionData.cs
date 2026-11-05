using UnityEngine;

/// <summary>
/// 산탄 행동 데이터: ActionData를 상속하고, Hit박스를 저장하지 않고 설정값(BoostShotSettings)에서 계산해 낸다.
/// 쿨타임/실린더/대체행동/타이밍 등 나머지는 ActionData 필드를 그대로 쓴다.
///
/// 박스를 .asset에 구워 넣지 않으므로 수치(산탄 수, 각도, 사거리)를 인스펙터에서 바꾸면 바로 반영되고,
/// 일반/강화 에셋이 같은 클래스에 값만 다르게 가진다. 직접 저장한 Hurt/Push 박스는 그대로 쓰이고,
/// 직접 저장한 Hit박스는 무시된다(부채꼴이 대신한다).
///
/// 생성: 프로젝트 창 우클릭 > NYH/Combat/BoostShot Action Data. 총구 위치는 ActionData.projectileMuzzle을 쓴다.
/// perBoxDamage는 항상 켜진 것으로 취급한다 (Damage = 1발당 피해, 겹친 Hit박스 배율 합 x Damage).
/// </summary>
[CreateAssetMenu(menuName = "NYH/Combat/BoostShot Action Data", fileName = "NewBoostShotActionData")]
public class BoostShotActionData : TapSkillData
{
    [Header("부채꼴 산탄")]
    [SerializeField] private BoostShotSettings fan = BoostShotSettings.BoosterNormal;

    // 계산 결과 캐시. 에셋은 불변 데이터이므로 한 번 만들면 값이 바뀌기 전까지 재사용한다 (GetActiveBoxes가 매 틱 불리므로)
    [System.NonSerialized] private FrameBox[] cache;

    public BoostShotSettings Fan => fan;
    public override bool PerBoxDamage => true;

    public override FrameBox[] FrameBoxes => cache ??= Build();

    private void OnValidate() => cache = null; // 인스펙터에서 값을 바꾸면 다시 계산
    private void OnEnable() => cache = null;

    // 직접 저장된 Hit박스는 버리고(부채꼴이 대신함) Hurt/Push는 유지한다
    private FrameBox[] Build() => FanBoxBuilder.BuildWithStored(StoredFrameBoxes, fan.ToShape(), ProjectileMuzzle, TotalFrames);
}