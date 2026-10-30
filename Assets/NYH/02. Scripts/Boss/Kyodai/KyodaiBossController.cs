using System.Collections.Generic;
using RealSteel.Boss.Core;
using UnityEngine;

/// <summary>
/// 지역 2 보스 "쿄다이" (2026-10-28, 기획서10_28 §지역 2 보스). KKH BossControllerBase를 그대로 탄다 —
/// HP·페이즈·FSM(딜타임 → 패턴 → 딜타임)·그로기/스턴은 KKH 것, 여기는 쿄다이 전용 부분만.
///
///   · 패턴·부위는 자식에 붙은 기믹 컴포넌트를 전부 자동 등록한다(정크 휠러와 달리 이름 매칭 없음)
///   · 페이즈별 패턴: BossMasterData.phases의 availableSkillIDs(= 패턴 ID)로 거른다. 비어 있으면 전부
///   · 경기장 범위·바닥 높이: 패턴들이 "경기장 1/3", "화면 양 끝" 같은 자리를 계산할 때 쓴다
///   · 그림: 3D 모델을 경기장 뒤(z+)에 그대로 두고 메인 카메라가 찍는다(픽셀화는 ProPixelizer 머티리얼, 2026-10-30).
///     부위 하나하나는 KyodaiLimb
/// </summary>
public class KyodaiBossController : BossControllerBase
{
    [Tooltip("경기장 평면 z(월드) — 플레이어가 있는 평면. 모션 키의 '앞으로 나온 거리'는 여기서 카메라 쪽(-z)으로 잰다")]
    [SerializeField] private float playZ;

    [Tooltip("경기장 왼쪽 끝 x(월드)")]
    [SerializeField] private float arenaMinX = -10f;

    [Tooltip("경기장 오른쪽 끝 x(월드)")]
    [SerializeField] private float arenaMaxX = 10f;

    [Tooltip("바닥 높이 y(월드) — 플레이어가 서 있는 높이")]
    [SerializeField] private float groundY;

    // 등록된 패턴 전부 — 페이즈가 바뀌면 여기서 다시 골라 activePatternPool을 채운다
    private readonly List<BossPatternGimmickBase> allPatterns = new List<BossPatternGimmickBase>();

    public float PlayZ => playZ;
    public float ArenaMinX => arenaMinX;
    public float ArenaMaxX => arenaMaxX;
    public float ArenaWidth => arenaMaxX - arenaMinX;
    public float GroundY => groundY;

    private KyodaiLimb[] limbs;

    /// <summary>부위 이름으로 KyodaiLimb 찾기 (모션 재생·미리보기용)</summary>
    public KyodaiLimb FindLimb(string partId)
    {
        if (limbs == null || limbs.Length == 0) limbs = GetComponentsInChildren<KyodaiLimb>(true);
        foreach (var limb in limbs) if (limb != null && limb.PartId == partId) return limb;
        return null;
    }

    public KyodaiLimb[] Limbs
    {
        get
        {
            if (limbs == null || limbs.Length == 0) limbs = GetComponentsInChildren<KyodaiLimb>(true);
            return limbs;
        }
    }

    protected override void RegisterGimmicks()
    {
        // 꺼둔 패턴(오브젝트 비활성·컴포넌트 체크 해제)은 등록하지 않는다 — 옛 패턴을 끄고 새 패턴만 쓰게
        foreach (var gimmick in GetComponentsInChildren<BossGimmickBase>(false))
            if (gimmick.enabled) RegisterGimmick(gimmick);

        allPatterns.Clear();
        allPatterns.AddRange(activePatternPool);
        if (allPatterns.Count == 0)
            Debug.LogWarning("[Kyodai] 자식에 패턴 기믹이 하나도 없음 — 딜타임만 반복한다");
    }

    protected override void BuildPhasePatternPool(int phase)
    {
        BossPhaseData phaseData = masterData != null && masterData.phases != null
            ? masterData.phases.Find(p => p.phaseIndex == phase)
            : null;

        activePatternPool.Clear();
        foreach (var pattern in allPatterns)
        {
            bool allowed = phaseData == null || phaseData.availableSkillIDs == null || phaseData.availableSkillIDs.Count == 0
                           || phaseData.availableSkillIDs.Contains(pattern.GimmickID);
            if (allowed) activePatternPool.Add(pattern);
        }
        _patternIndex = 0;
    }

    /// <summary>경기장 point 자리에서 카메라 쪽(-z)으로 forward만큼 나온 3D 좌표 (손 목표·IK 타깃)</summary>
    public Vector3 PlayPoint(Vector2 point, float forward) => new Vector3(point.x, point.y, playZ - forward);

    /// <summary>경기장 안으로 x를 자른다. halfWidth만큼 안쪽 여유를 둔다(넓은 공격이 벽 밖으로 안 나가게)</summary>
    public float ClampToArena(float x, float halfWidth = 0f)
    {
        float min = arenaMinX + halfWidth;
        float max = arenaMaxX - halfWidth;
        return min > max ? (arenaMinX + arenaMaxX) * 0.5f : Mathf.Clamp(x, min, max);
    }

    protected override void OnPhaseChanged(int prevPhase, int newPhase)
    {
        Debug.Log($"[Kyodai] 페이즈 {prevPhase} → {newPhase}");
    }

    protected override void OnBossDefeated()
    {
        Debug.Log("[Kyodai] 쿄다이 격파");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(arenaMinX, groundY), new Vector3(arenaMaxX, groundY));
        Gizmos.DrawLine(new Vector3(arenaMinX, groundY), new Vector3(arenaMinX, groundY + 3f));
        Gizmos.DrawLine(new Vector3(arenaMaxX, groundY), new Vector3(arenaMaxX, groundY + 3f));
    }
#endif
}
