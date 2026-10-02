using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [스탯 종합 빌더 (CombatantBuilder)]
/// 인벤토리/세이브 데이터 및 코어/파츠 마스터 에셋으로부터
/// 기획서 복합 공식을 적용해서 런타임 전투체인 CombatantSnapshot을 빌드하는 팩토리 클래스임.
/// </summary>
public static class CombatantBuilder
{
    /// <summary>
    /// 코어 데이터와 5부위 장착 파츠 목록으로부터 CombatantSnapshot 생성함
    /// </summary>
    public static CombatantSnapshot Build(
        string fighterId,
        bool isPlayer,
        CoreMasterData core,
        IEnumerable<PartMasterData> equippedParts)
    {
        var snapshot = new CombatantSnapshot
        {
            fighterID = fighterId,
            isPlayer = isPlayer,
            currentHp = core != null ? core.GetMaxHp() : 1000,
            maxHp = core != null ? core.GetMaxHp() : 1000,
            baseDefense = core != null ? core.GetDefense() : 50
        };

        int coreAtk = core != null ? core.GetBaseAttackPower() : 20;

        // 부위별 파츠 추출함
        PartMasterData head = null;
        PartMasterData leftArm = null;
        PartMasterData rightArm = null;
        PartMasterData leftLeg = null;
        PartMasterData rightLeg = null;

        if (equippedParts != null)
        {
            foreach (var part in equippedParts)
            {
                if (part == null) continue;

                switch (part.slotType)
                {
                    case BodyPart.Head:
                        head = part;
                        break;
                    case BodyPart.LeftArm:
                        leftArm = part;
                        break;
                    case BodyPart.RightArm:
                        rightArm = part;
                        break;
                    case BodyPart.LeftLeg:
                        leftLeg = part;
                        break;
                    case BodyPart.RightLeg:
                        rightLeg = part;
                        break;
                }

                // 부위 런타임 상태 등록함
                snapshot.partStates[part.slotType] = new PartRuntimeState(
                    part.partID,
                    part.slotType,
                    part.baseDurability
                );
            }
        }

        // 1. 공격력: 기본Atk + (왼팔Atk + 오른팔Atk) / 2
        int leftAtk = leftArm != null ? leftArm.armStatData.armAttackPower : 0;
        int rightAtk = rightArm != null ? rightArm.armStatData.armAttackPower : 0;
        snapshot.totalAttackPower = coreAtk + Mathf.RoundToInt((leftAtk + rightAtk) / 2f);

        // 2. 이동 속도: (왼다리Speed + 오른다리Speed) / 2
        float leftSpeed = leftLeg != null ? leftLeg.legStatData.moveSpeedBonus : 0f;
        float rightSpeed = rightLeg != null ? rightLeg.legStatData.moveSpeedBonus : 0f;
        // snapshot.finalMoveSpeed = (leftSpeed + rightSpeed) / 2f;

        // 3. 머리 파츠: 치명타 저항 및 치명타 피해 삭감
        if (head != null)
        {
            // snapshot.critResistance = head.headStatData.critResistance;
            // snapshot.critDamageReduction = head.headStatData.critDamageReduction;
        }

        // 4. 팔 파츠: 가드 시 방어력 추가 보정 합산함
        int leftGuard = leftArm != null ? leftArm.armStatData.guardDefBonus : 0;
        int rightGuard = rightArm != null ? rightArm.armStatData.guardDefBonus : 0;
        // snapshot.guardDefBonus = leftGuard + rightGuard;

        // 5. 다리 파츠: 위빙(회피) 무적 시간 추가 보정 합산함
        float leftInv = leftLeg != null ? leftLeg.legStatData.invincibleFramesBonus : 0f;
        float rightInv = rightLeg != null ? rightLeg.legStatData.invincibleFramesBonus : 0f;
        // snapshot.invincibleBonus = leftInv + rightInv;

        return snapshot;
    }

    /// <summary>
    /// 테스트 및 NPC(더미 봇)용 간이 스냅샷 생성기임
    /// </summary>
    public static CombatantSnapshot CreateDummy(
        string fighterId,
        bool isPlayer,
        int maxHp = 1000,
        int baseDefense = 100,
        int baseAtk = 20,
        int leftArmAtk = 100,
        int rightArmAtk = 100,
        float leftLegSpd = 5f,
        float rightLegSpd = 5f,
        int partMaxDurability = 100)
    {
        var snapshot = new CombatantSnapshot
        {
            fighterID = fighterId,
            isPlayer = isPlayer,
            currentHp = maxHp,
            maxHp = maxHp,
            baseDefense = baseDefense,
            totalAttackPower = baseAtk + Mathf.RoundToInt((leftArmAtk + rightArmAtk) / 2f),
            // finalMoveSpeed = (leftLegSpd + rightLegSpd) / 2f,
            // critResistance = 0.1f,
            // critDamageReduction = 0.2f,
            // guardDefBonus = 50,
            // invincibleBonus = 0.1f
        };

        // 5부위 기본 런타임 상태 등록함
        var parts = new[] { BodyPart.Head, BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg };
        foreach (var p in parts)
        {
            snapshot.partStates[p] = new PartRuntimeState($"DUMMY_{p}", p, partMaxDurability);
        }

        return snapshot;
    }
}
