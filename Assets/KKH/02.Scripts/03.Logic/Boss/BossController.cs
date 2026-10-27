using UnityEngine;
using RealSteel.Boss.Core;

/// <summary>
/// [보스 총괄 컨트롤러 (BossController)]
/// BossControllerBase를 상속하여 기존 테스트 도구(BossFSMTester) 및 레거시 씬과의 100% 호환을 보장하는 래퍼 클래스임.
/// </summary>
public class BossController : BossControllerBase
{
    protected override void RegisterGimmicks()
    {
        // 씬 내에 부착된 기믹 컴포넌트들을 자동 탐색하여 레지스트리에 등록
        var gimmicks = GetComponentsInChildren<BossGimmickBase>(true);
        foreach (var g in gimmicks)
        {
            RegisterGimmick(g);
        }
    }

    /// <summary>
    /// 기존 FSM 상태 전환용 레거시 호환 패턴 반환 함수
    /// </summary>
    public BossPatternData GetNextPattern()
    {
        var patternGimmick = GetNextPatternGimmick();
        if (patternGimmick != null)
        {
            return patternGimmick.PatternData;
        }

        // 폴백: 마스터 데이터 직접 순환
        if (masterData == null || masterData.patterns == null || masterData.patterns.Count == 0) return null;
        var valid = masterData.patterns.FindAll(p => p != null && !Snapshot.IsSkillSealed(p.patternID));
        if (valid.Count == 0) valid = masterData.patterns;

        var p = valid[_patternIndex % valid.Count];
        _patternIndex++;
        return p;
    }
}