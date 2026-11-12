using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>난이도 하나의 쿄다이 규칙 (2026-11-12)</summary>
[Serializable]
public class KyodaiDifficultyRule
{
    [Tooltip("이 칸이 어느 난이도인지 (표시용 — 순서는 아주 쉬움·쉬움·보통·어려움·헬)")]
    public BossDifficulty difficulty;

    [Tooltip("1페이즈에 나오는 패턴 ID. 비우면 마스터 데이터 1페이즈 목록 전부. 적힌 순서대로 돌아간다")]
    public List<string> phase1Patterns = new List<string>();

    [Tooltip("2페이즈에 나오는 패턴 ID. 비우면 마스터 데이터 2페이즈 목록 전부 (2페이즈는 어려움·헬만 — KKH allowPhase2)")]
    public List<string> phase2Patterns = new List<string>();

    [Tooltip("1페이즈에 동시에 나오는 패턴 수. 2 = 손 패턴과 레이저(손 안 쓰는) 패턴이 같이 나온다")]
    [Range(1, 2)] public int phase1Concurrent = 1;

    [Tooltip("2페이즈에 동시에 나오는 패턴 수")]
    [Range(1, 2)] public int phase2Concurrent = 1;

    [Tooltip("내려찍는 기물 빈도 배율. 2 = 두 배 자주(대기 절반), 0 = 안 나옴")]
    [Min(0f)] public float crusherFrequency = 1f;

    [Tooltip("촉수 최대 수. 0 = 안 나옴")]
    [Min(0)] public int tentacleMax = 10;

    public List<string> Patterns(int phase) => phase >= 2 ? phase2Patterns : phase1Patterns;
    public int Concurrent(int phase) => phase >= 2 ? phase2Concurrent : phase1Concurrent;
}

/// <summary>
/// 쿄다이 난이도별 차이 (2026-11-12 사용자: 난이도마다 패턴 구성·동시에 나오는 수로 차이를 두기).
/// KKH 난이도 데이터(HP·공격 배율·2페이즈 허용·헬 연계)는 그대로 쓰고, 그 위에 쿄다이 전용으로:
///   · 페이즈별 나오는 패턴 — 이 에셋이 있으면 KKH '패턴 수 제한(무작위로 고르기)' 대신 여기 목록을 그대로 쓴다
///   · 동시 패턴 수 — 2면 손 패턴과 손을 안 쓰는 레이저 패턴(쓸기·X자)이 같이 나온다(같은 부위는 안 겹침)
///   · 내려찍는 기물 빈도, 촉수 최대 수
/// 값은 전부 임시값 — 기획 수치 없음.
/// </summary>
[CreateAssetMenu(menuName = "NYH/Boss/Kyodai Difficulty Data", fileName = "KyodaiDifficultyData")]
public class KyodaiDifficultyData : ScriptableObject
{
    [Tooltip("아주 쉬움·쉬움·보통·어려움·헬 순서 (BossDifficulty 값과 같은 순서)")]
    public KyodaiDifficultyRule[] rules = new KyodaiDifficultyRule[0];

    /// <summary>그 난이도 규칙. 없으면 null (= 예전 방식)</summary>
    public KyodaiDifficultyRule Get(BossDifficulty difficulty)
    {
        if (rules == null) return null;
        foreach (var r in rules) if (r != null && r.difficulty == difficulty) return r;
        int i = (int)difficulty;
        return i >= 0 && i < rules.Length ? rules[i] : null;
    }
}
