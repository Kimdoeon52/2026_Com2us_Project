#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// [보스 SO 에셋 생성기 (BossAssetFactory)]
/// 지역 1 보스(정크 휠러)의 MasterData 및 패턴 SO 에셋 3종을 원클릭으로 생성/갱신하는 에디터 툴임.
/// </summary>
public static class BossAssetFactory
{
    private const string SO_DIR = "Assets/KKH/03.SOData/Boss";

    [MenuItem("RealSteel/Boss/Generate Region 1 Boss Assets", priority = 10)]
    public static void GenerateRegion1BossAssets()
    {
        if (!Directory.Exists(SO_DIR))
        {
            Directory.CreateDirectory(SO_DIR);
        }

        // 1. 패턴 1: 튀어오르기 탄막
        var pattern1 = CreateOrLoadPattern(
            "Pattern_01_JumpBarrage",
            "PATTERN_JUMP_BARRAGE",
            "튀어오르기 탄막",
            "맨 아래에서 위로 도약하며 360도 12방향 탄막을 3회 회전 발사함.",
            tellDuration: 1.5f,
            postDelay: 1.0f,
            hasDpsCheck: false,
            targetPart: BodyPart.Core,
            partDamage: 0
        );

        // 2. 패턴 2: 돌진 & 저지 (DPS 체크)
        var pattern2 = CreateOrLoadPattern(
            "Pattern_02_Charge",
            "PATTERN_CHARGE",
            "돌진 (DPS 체크 저지)",
            "2초 전조(바퀴 헛돔/붉은 점멸) 동안 300 누적 대미지 시 3초 스턴. 실패 시 맵 좌우 돌진(회피 무적 회피).",
            tellDuration: 2.0f,
            postDelay: 1.0f,
            hasDpsCheck: true,
            requiredDamage: 300,
            stunDuration: 3.0f,
            targetPart: BodyPart.Core,
            partDamage: 0
        );

        // 3. 패턴 3: 3단 내려찍기 & 못판 유도 (부위 파괴)
        var pattern3 = CreateOrLoadPattern(
            "Pattern_03_Slam",
            "PATTERN_SLAM",
            "3단 내려찍기 (못판 유도)",
            "플레이어 X좌표 추적 후 급강하 3회 반복. 못판 착지 유도 시 바퀴 펑크(4초 그로기/1.5배 대미지). 펑크 2회 시 타이어 부위 파괴.",
            tellDuration: 1.0f,
            postDelay: 1.0f,
            hasDpsCheck: false,
            targetPart: BodyPart.LeftLeg, // 다리 파츠 내구도 타격
            partDamage: 30
        );

        // 4. 보스 마스터 데이터 생성 및 패턴 연결
        string masterPath = $"{SO_DIR}/BossMasterData_JunkWheeler.asset";
        var masterData = AssetDatabase.LoadAssetAtPath<BossMasterData>(masterPath);
        if (masterData == null)
        {
            masterData = ScriptableObject.CreateInstance<BossMasterData>();
            AssetDatabase.CreateAsset(masterData, masterPath);
        }

        masterData.bossID = "BOSS_REGION_01";
        masterData.bossName = "정크 휠러 (폐타이어 & 폐엔진)";
        masterData.maxHp = 4000;
        masterData.baseDefense = 40;
        masterData.patternPostDelay = 1.0f;
        masterData.rewardGold = 1500;
        masterData.rewardCoreExp = 500;

        // 기믹 등록 (고철 못판, 타이어 부위)
        masterData.gimmickParts.Clear();
        masterData.gimmickParts.Add(new BossGimmickPartData
        {
            gimmickID = "GIMMICK_NAIL_BOARD",
            gimmickName = "고철 못판",
            isPhysicalBossPart = false,
            maxDurability = 1,
            groggyDuration = 4.0f,
            damageAmplification = 1.5f
        });
        masterData.gimmickParts.Add(new BossGimmickPartData
        {
            gimmickID = "TIRE_PART",
            gimmickName = "폐타이어 부위",
            isPhysicalBossPart = true,
            maxDurability = 2, // 펑크 2회 시 파괴
            targetSkillIDToSeal = "PATTERN_JUMP_BARRAGE",
            groggyDuration = 4.0f,
            damageAmplification = 1.5f
        });

        // 패턴 리스트 연결
        masterData.patterns.Clear();
        masterData.patterns.Add(pattern1);
        masterData.patterns.Add(pattern2);
        masterData.patterns.Add(pattern3);

        EditorUtility.SetDirty(masterData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>[BossAssetFactory] 지역 1 보스(정크 휠러) 마스터 데이터 및 패턴 SO 에셋 3종 생성 완료!</color>");
    }

    private static BossPatternData CreateOrLoadPattern(
        string fileName, string id, string name, string desc,
        float tellDuration, float postDelay, bool hasDpsCheck,
        int requiredDamage = 300, float stunDuration = 3.0f,
        BodyPart targetPart = BodyPart.Core, int partDamage = 0)
    {
        string path = $"{SO_DIR}/{fileName}.asset";
        var pattern = AssetDatabase.LoadAssetAtPath<BossPatternData>(path);
        if (pattern == null)
        {
            pattern = ScriptableObject.CreateInstance<BossPatternData>();
            AssetDatabase.CreateAsset(pattern, path);
        }

        pattern.patternID = id;
        pattern.patternName = name;
        pattern.description = desc;
        pattern.tellDuration = tellDuration;
        pattern.postDelay = postDelay;
        pattern.hasDpsCheck = hasDpsCheck;
        pattern.requiredDamageToInterrupt = requiredDamage;
        pattern.interruptStunDuration = stunDuration;
        pattern.targetPlayerPart = targetPart;
        pattern.partDurabilityDamage = partDamage;

        EditorUtility.SetDirty(pattern);
        return pattern;
    }
}
#endif
