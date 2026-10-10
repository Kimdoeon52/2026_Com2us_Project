using System;
using UnityEngine;

/// <summary>
/// [보스 공격 패턴 마스터 데이터 SO (BossPatternData)]
/// 기획서 §6.18 지역 1 보스(정크 휠러) 및 향후 보스들의 개별 공격 패턴을 정의하는 불변 데이터 에셋임.
/// 전조 시간, 후딜레이, 저지 요구 대미지(DPS 체크), 플레이어 피격 부위 등을 포함함.
/// </summary>
[CreateAssetMenu(fileName = "BossPattern_", menuName = "RealSteel/Combat/BossPatternData", order = 3)]
public class BossPatternData : ScriptableObject
{
    [Header("1. 패턴 식별 정보")]
    [Tooltip("패턴 고유 식별자 (예: PATTERN_JUMP_BARRAGE, PATTERN_CHARGE, PATTERN_SLAM)")]
    public string patternID;

    [Tooltip("패턴 명칭 (예: 튀어오르기 탄막, 돌진, 3단 내려찍기)")]
    public string patternName;

    [TextArea(2, 4)]
    [Tooltip("패턴 설명 및 기획 의도")]
    public string description;

    [Header("2. 타이밍 및 딜레이")]
    [Tooltip("패턴 전조(예고) 지속 시간 (초 단위, 기획서 기준 약 2초)")]
    public float tellDuration = 2.0f;

    [Tooltip("패턴 종료 후 확정 딜타임 (초 단위, 기획서 공통 1.0초 보장)")]
    public float postDelay = 1.0f;

    [Header("3. DPS 체크 저지 기믹 (돌진 등)")]
    [Tooltip("전조 중 일정 피해를 누적하여 패턴을 차단/스턴할 수 있는지 여부")]
    public bool hasDpsCheck = false;

    [Tooltip("패턴 차단/저지에 필요한 누적 대미지 임계치 (기획서: 머리 위 게이지 300)")]
    public int requiredDamageToInterrupt = 300;

    [Tooltip("저지 성공 시 보스가 받는 스턴 시간 (초 단위, 기획서: 3초)")]
    public float interruptStunDuration = 3.0f;

    [Header("4. 피격 시 플레이어 파츠 타격")]
    [Tooltip("기획서 §12.7: 보스 스킬 피격 시에만 내구도를 차감할 대상 플레이어 부위 (일반 피격은 Core만 감소)")]
    public BodyPart targetPlayerPart = BodyPart.Core;

    [Tooltip("피격 시 차감할 플레이어 대상 부위 내구도 수치")]
    public int partDurabilityDamage = 25;

    [Header("5. 기본 공격력 계수 및 액션 타임라인")]
    [Tooltip("패턴 기본 피해량 계수 (%)")]
    public float damageRatio = 100f;

    [Tooltip("NYH 프레임 타임라인 액션 데이터 연동 (히트박스/모션 프레임)")]
    public ActionData actionData;
}
