using UnityEngine;
using System.Collections.Generic;

// 공통 Enum 참조 안내:
// - BodyPart: NYH 파트(Assets/NYH/02. Scripts/Combat/Data/CombatEnums.cs) 공통 enum 사용
// - PartGrade: KDU 파트(Assets/KDU/02.Scripts/02.Scrapyard/PartGrade.cs) 공통 enum 사용

/// <summary>
/// [부품 마스터 데이터 SO (ScriptableObject)]
/// 부품 원형(Master) 스탯 및 속성을 정의하는 불변 데이터 에셋임
/// 
/// ─────────────────────────────────────────────────────────────────────────────
/// ■ 파트별 협업 및 참조 가이드:
///   1. 크래프팅 / 내구도 파괴 파트: 
///      - 3x3 제작대 조합 완성 시 생성할 파츠 원형 및 기본 내구도(baseDurability), 
///        전투 패배 시 영구 파괴 확률(destructionResistance) 연동.
///   2. 전투 행동 / 그리드 / 부위 파괴 파트:
///      - BodyPart(Head, LeftArm, RightArm, LeftLeg, RightLeg, Core) 공통 enum 사용.
///      - 팔: 기획서 공식 [수치 공격력 = 기본 공격력 + (왼팔 공격력 + 오른팔 공격력) / 2], 가드 보정 연동.
///      - 다리: 기획서 공식 [이동 속도 = (왼다리 속도 + 오른다리 속도) / 2], 위빙(회피) 무적 시간 연동.
///   3. 고물상 / 자원 수집 / 인벤토리 파트:
///      - PartGrade(Common ~ Prototype) 공통 enum 사용.
///      - 인벤토리 인스턴스(PartsInstance)가 본 SO의 partID를 식별 키로 참조.
///   4. 전투 스킬 파트:
///      - 부품 장착 시 활성화되는 고유 액티브 스킬(activeSkillId) 및 부위별 패시브 ID 목록(passiveSkillIds) 참조.
/// ─────────────────────────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "PartMasterData", menuName = "RealSteel/DB/PartMasterData", order = 1)]
public class PartMasterData : ScriptableObject
{
    [Header("1. 기본 식별 정보")]
    [Tooltip("부품 고유 식별 번호 (DB 및 인벤토리 연동 키)")]
    public string partID;

    [Tooltip("게임 내 UI에 표시될 부품 명칭")]
    public string partName;

    [Tooltip("장착 대상 부위 (전투 행동 파트의 BodyPart 공통 enum 사용)")]
    public BodyPart slotType;

    [Tooltip("부품 등급 (자원/인벤토리 파트의 PartGrade 공통 enum 사용: Common ~ Prototype)")]
    public PartGrade partGrade;


    [Header("2. 내구도 및 파괴 리스크 (기획서 §5.6)")]
    [Tooltip("부품의 기본 최대 내구도")]
    public int baseDurability;

    [Tooltip("전투 패배 시 부품 영구 파괴 확률 (0.0 ~ 1.0)\n" +
             "기획서 공식 테이블: 일반(70%), 레어(45%), 에픽(20%), 전설(5%), 프로토타입(1%)")]
    [Range(0f, 1f)]
    public float destructionResistance;


    [Header("3. 부위별 특화 스탯 (해당 슬롯에 맞는 항목만 기입)")]
    [Tooltip("머리 파츠 전용: 치명타 저항 및 치명타 피해 감쇄 스탯")]
    public HeadStatData headStatData;

    [Tooltip("팔 파츠 전용: 공격력(양팔 평균 반영) 및 가드 방어력 보정 스탯")]
    public ArmStatData armStatData;

    [Tooltip("다리 파츠 전용: 이동 속도(양다리 평균 반영) 및 위빙(저스트 회피) 무적 시간 보정 스탯")]
    public LegStatData legStatData;


    [Header("4. 스킬 연동 (전투 스킬 파트 매핑)")]
    [Tooltip("파츠 장착 시 부여되는 고유 액티브 스킬 ID (스킬 없을 시 -1)")]
    public int activeSkillId = -1;

    [Tooltip("기획서 §5.2.6 부위별 고유 패시브 스킬 ID 목록 (예: 충격 흡수 프레임, 반발 장갑, 잔상 회로 등)")]
    public List<int> passiveSkillIds = new List<int>();
}

/// <summary>
/// 머리(Head) 파츠 특화 스탯 구조체
/// 치명타 방어 및 대미지 감쇄를 담당함
/// </summary>
[System.Serializable]
public struct HeadStatData
{
    [Tooltip("상대 치명타 공격 발생 확률을 합연산으로 감소시키는 수치 (0.0 ~ 1.0)")]
    public float critResistance;

    [Tooltip("치명타 피격 시 추가 대미지를 삭감하는 배율 (0.0 ~ 1.0)")]
    public float critDamageReduction;
}

/// <summary>
/// 팔(LeftArm / RightArm) 파츠 특화 스탯 구조체
/// 공격력 및 가드 시의 일시적 방어력 보정을 담당함
/// 공격력 공식: 기본 공격력 + (왼팔 공격력 + 오른팔 공격력) / 2
/// </summary>
[System.Serializable]
public struct ArmStatData
{
    [Tooltip("해당 팔 파츠의 기본 공격력 수치 (기획서 등급 테이블 기준: 일반 100, 레어 120, 에픽 144, 전설 173, 프로토 207)")]
    public int armAttackPower;

    [Tooltip("타격 기술(잽, 스트레이트, 훅 등)에 곱해지는 공격력 계수 (기본 1.0)")]
    public float atkMultiplier;

    [Tooltip("가드(C키) 성공 시 코어 본체 방어력에 일시적으로 합산되는 보정치")]
    public int guardDefBonus;
}

/// <summary>
/// 다리(LeftLeg / RightLeg) 파츠 특화 스탯 구조체
/// 이동 속도 및 위빙(Weaving / 저스트 패링 및 회피) 무적 시간을 담당함
/// 이동 속도 공식: (왼다리 속도 + 오른다리 속도) / 2
/// </summary>
[System.Serializable]
public struct LegStatData
{
    [Tooltip("해당 다리 파츠의 이동 속도 보정 수치 (기획서: 양다리 평균값으로 최종 이동 속도 산출)")]
    public float moveSpeedBonus;

    [Tooltip("위빙(저스트 회피) 성공 시 제공되는 기본 무적 시간에 추가되는 보정치 (초 또는 프레임 단위)")]
    public float invincibleFramesBonus;
}
