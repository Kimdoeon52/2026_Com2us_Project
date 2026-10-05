using System.Collections.Generic;
using UnityEngine;

public enum NPCType
{
    NPC_A, // 판돈 0원 구제 경기 (일반급 파츠, 기물 파괴 기믹 1개)
    NPC_B, // 기준가 x 2.5 (일반+레어 파츠, 기물 파괴 기믹 1개)
    NPC_C  // 기준가 x 4 (에픽급 지역 메타 파츠, 부위 파괴 기믹 1개)
}

/// <summary>
/// [스탯 종합 빌더 (CombatantBuilder)]
/// 인벤토리/세이브 데이터 및 코어/파츠 마스터 에셋으로부터
/// 기획서 복합 공식을 적용해서 런타임 전투체인 CombatantSnapshot을 빌드하는 팩토리 클래스임.
/// </summary>
public static class CombatantBuilder
{
    /// <summary>
    /// 코어 데이터와 5부위 장착 파츠 목록으로부터 CombatantSnapshot 생성함
    /// 기획서 §6.8 미장착 부위는 기본 파츠(일반 70% 스탯)로 자동 채움
    /// </summary>
    public static CombatantSnapshot Build(
        string fighterId,
        bool isPlayer,
        CoreMasterData core,
        IEnumerable<PartMasterData> equippedParts)
    {
        int coreHp = core != null ? core.GetMaxHp() : 1000;
        int coreDef = core != null ? core.GetDefense() : 50;
        int coreAtk = core != null ? core.GetBaseAttackPower() : 20;

        var snapshot = new CombatantSnapshot
        {
            fighterID = fighterId,
            isPlayer = isPlayer,
            currentHp = coreHp,
            maxHp = coreHp,
            baseDefense = coreDef,
            coreLevel = core != null ? core.coreLevel : 1,
            currentCylinder = 1
        };

        // 부위별 파츠 추출
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
                    case BodyPart.Head: head = part; break;
                    case BodyPart.LeftArm: leftArm = part; break;
                    case BodyPart.RightArm: rightArm = part; break;
                    case BodyPart.LeftLeg: leftLeg = part; break;
                    case BodyPart.RightLeg: rightLeg = part; break;
                }

                snapshot.partStates[part.slotType] = new PartRuntimeState(
                    part.partID,
                    part.slotType,
                    part.baseDurability
                );
            }
        }

        // 기본 파츠 자동 채움 (기획서 §6.8: 일반 등급 70% 기본 내구도 머리: 42, 팔: 70, 다리: 56)
        FillDefaultPartIfMissing(snapshot, BodyPart.Head, 42);
        FillDefaultPartIfMissing(snapshot, BodyPart.LeftArm, 70);
        FillDefaultPartIfMissing(snapshot, BodyPart.RightArm, 70);
        FillDefaultPartIfMissing(snapshot, BodyPart.LeftLeg, 56);
        FillDefaultPartIfMissing(snapshot, BodyPart.RightLeg, 56);

        // 1. 공격력: 기본Atk + (왼팔Atk + 오른팔Atk) / 2
        int leftAtk = leftArm != null ? leftArm.attackPower : 50;
        int rightAtk = rightArm != null ? rightArm.attackPower : 50;
        snapshot.totalAttackPower = coreAtk + Mathf.RoundToInt((leftAtk + rightAtk) / 2f);

        // 2. 이동 속도: (왼다리Speed + 오른다리Speed) / 2
        float leftSpeed = leftLeg != null ? leftLeg.moveSpeedBonus : 3f;
        float rightSpeed = rightLeg != null ? rightLeg.moveSpeedBonus : 3f;
        snapshot.finalMoveSpeed = (leftSpeed + rightSpeed) / 2f;

        return snapshot;
    }

    private static void FillDefaultPartIfMissing(CombatantSnapshot snapshot, BodyPart part, int defaultDurability)
    {
        if (!snapshot.partStates.ContainsKey(part))
        {
            snapshot.partStates[part] = new PartRuntimeState($"DEFAULT_{part}", part, defaultDurability);
        }
    }

    /// <summary>
    /// [LJS AI 파트 협업] 경기장 상주 일반 적 NPC(A, B, C) 프리셋 생성
    /// </summary>
    public static CombatantSnapshot CreateNPCPreset(NPCType type)
    {
        switch (type)
        {
            case NPCType.NPC_A:
                return CreateDummy(
                    fighterId: "NPC_A",
                    isPlayer: false,
                    maxHp: 500,
                    baseDefense: 25,
                    baseAtk: 15,
                    leftArmAtk: 45,
                    rightArmAtk: 45,
                    leftLegSpd: 2.8f,
                    rightLegSpd: 2.8f,
                    partMaxDurability: 60
                );

            case NPCType.NPC_B:
                return CreateDummy(
                    fighterId: "NPC_B",
                    isPlayer: false,
                    maxHp: 800,
                    baseDefense: 40,
                    baseAtk: 25,
                    leftArmAtk: 75,
                    rightArmAtk: 75,
                    leftLegSpd: 3.2f,
                    rightLegSpd: 3.2f,
                    partMaxDurability: 80
                );

            case NPCType.NPC_C:
            default:
                return CreateDummy(
                    fighterId: "NPC_C",
                    isPlayer: false,
                    maxHp: 1200,
                    baseDefense: 55,
                    baseAtk: 35,
                    leftArmAtk: 115,
                    rightArmAtk: 115,
                    leftLegSpd: 3.6f,
                    rightLegSpd: 3.6f,
                    partMaxDurability: 100
                );
        }
    }

    /// <summary>
    /// 테스트 및 간이 스냅샷 생성기임
    /// </summary>
    public static CombatantSnapshot CreateDummy(
        string fighterId,
        bool isPlayer,
        int maxHp = 1000,
        int baseDefense = 50,
        int baseAtk = 20,
        int leftArmAtk = 80,
        int rightArmAtk = 80,
        float leftLegSpd = 3f,
        float rightLegSpd = 3f,
        int partMaxDurability = 80)
    {
        var snapshot = new CombatantSnapshot
        {
            fighterID = fighterId,
            isPlayer = isPlayer,
            currentHp = maxHp,
            maxHp = maxHp,
            baseDefense = baseDefense,
            totalAttackPower = baseAtk + Mathf.RoundToInt((leftArmAtk + rightArmAtk) / 2f),
            finalMoveSpeed = (leftLegSpd + rightLegSpd) / 2f,
            currentCylinder = 1
        };

        // 5부위 기본 런타임 상태 등록
        var parts = new[] { BodyPart.Head, BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg };
        foreach (var p in parts)
        {
            snapshot.partStates[p] = new PartRuntimeState($"DUMMY_{p}", p, partMaxDurability);
        }

        return snapshot;
    }
}
