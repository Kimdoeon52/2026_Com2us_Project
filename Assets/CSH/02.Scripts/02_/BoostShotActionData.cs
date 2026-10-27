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

    private FrameBox[] Build()
    {
        var list = new System.Collections.Generic.List<FrameBox>();

        // 직접 저장된 박스 중 Hit는 버린다 (부채꼴이 대신함). Hurt/Push는 유지
        foreach (var box in StoredFrameBoxes)
            if (box.type != BoxType.Hit) list.Add(box);

        int pellets = Mathf.Max(1, fan.pellets);
        int steps = Mathf.Max(1, fan.steps);
        Vector2 muzzle = ProjectileMuzzle;

        for (int p = 0; p < pellets; p++)
        {
            // 1발이면 정면, 아니면 -spread/2 ~ +spread/2 로 균등 분할
            float t = pellets == 1 ? 0f : (float)p / (pellets - 1) - 0.5f;
            float angle = t * fan.spreadDegrees;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            for (int step = 0; step < steps; step++)
            {
                // step 0: 사거리 전체(배율 farScale), 이후: 점점 짧은 박스(나머지 배율을 균등 분배). 겹치면 합이 1.0
                float length = fan.range * (steps - step) / steps;
                float scale = steps == 1 ? 1f : (step == 0 ? fan.farScale : (1f - fan.farScale) / (steps - 1));

                // 모든 박스가 총구에서 시작하도록 중심을 광선 방향으로 length/2 만큼 민다
                Vector2 center = muzzle + dir * (length * 0.5f);
                list.Add(new FrameBox
                {
                    type = BoxType.Hit,
                    bodyPart = BodyPart.Core,
                    rect = new Rect(center.x - length * 0.5f, center.y - fan.thickness * 0.5f, length, fan.thickness),
                    rotation = angle,
                    damageScale = scale,
                    anchor = FrameAnchor.Active,
                    startOffset = 0,
                    length = Mathf.Max(0, fan.hitLength),
                    startFrame = 1,                      // Legacy 앵커에서만 읽히는 값
                    endFrame = Mathf.Max(1, TotalFrames)
                });
            }
        }
        return list.ToArray();
    }
}