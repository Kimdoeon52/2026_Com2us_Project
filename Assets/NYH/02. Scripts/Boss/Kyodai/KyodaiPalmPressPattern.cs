using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using RealSteel.Boss.Core;
using UnityEngine;

/// <summary>
/// 쿄다이 2페이즈 "손바닥 압착" (2026-10-28, 기획서10_28 §지역 2 보스 2페이즈 표).
///
///   전경 진입 부위: 오른팔 / 예고: 경기장 1/3을 덮는 그림자 1.2초 / 공격: 손바닥이 내려와 해당 구역을 짓누름
///   잔류: 3초 (손등 위 타격) / 파츠 피해: —
///
/// 흐름(KKH BossPatternGimmickBase 골격): 전조(tellDuration = 1.2초, 그림자 + 손이 전경으로 들어옴 = 전이)
///   → 내려찍기(공격) → 3초 박힘(잔류, 이때만 맞음) → 배경으로 복귀 → KKH 딜타임.
/// 예고 시간·데미지는 패턴 데이터(BossPatternData) 값을 쓴다.
/// </summary>
public class KyodaiPalmPressPattern : BossPatternGimmickBase
{
    [SerializeField] private KyodaiLimb limb;
    [SerializeField] private GroundTelegraph telegraph;

    [Tooltip("덮는 너비 = 경기장 너비 × 이 값. 기획서: 경기장 1/3")]
    [Range(0.05f, 1f)] [SerializeField] private float areaRatio = 1f / 3f;

    [Tooltip("잔류(손이 박혀 있는) 시간. 기획서: 3초")]
    [Min(0f)] [SerializeField] private float lingerSeconds = 3f;

    [Header("동작 연출 — 기획 수치 없음(임시)")]
    [Tooltip("TODO: 기획 수치 없음. 예고 동안 손이 떠 있는 높이(바닥 기준, 월드)")]
    [SerializeField] private float hoverHeight = 5f;

    [Tooltip("TODO: 기획 수치 없음. 내려찍는 데 걸리는 시간")]
    [Min(0.01f)] [SerializeField] private float slamSeconds = 0.18f;

    [Tooltip("TODO: 기획 수치 없음. 배경으로 돌아가는 시간")]
    [Min(0.01f)] [SerializeField] private float returnSeconds = 0.8f;

    [Tooltip("TODO: 기획 수치 없음. 잔류 중 맞을 수 있는 손등 두께(월드)")]
    [Min(0.1f)] [SerializeField] private float palmThickness = 1.5f;

    [Tooltip("TODO: 기획 수치 없음. 짓누르는 판정 높이(바닥에서 위로, 월드)")]
    [Min(0.1f)] [SerializeField] private float crushHeight = 3f;

    private KyodaiBossController kyodai;
    private float centerX;
    private float width;

    protected override void OnAttached()
    {
        kyodai = OwnerBoss as KyodaiBossController;
        if (kyodai == null) Debug.LogError("[손바닥 압착] KyodaiBossController 밑에 있어야 함");
    }

    // 1) 자리 정하기: 플레이어가 있는 곳을 중심으로 경기장 1/3. 벽 밖으로 안 나가게 자른다
    protected override void OnPreExecute()
    {
        width = kyodai.ArenaWidth * areaRatio;
        var player = GameObject.FindWithTag("Player");
        float x = player != null ? player.transform.position.x : (kyodai.ArenaMinX + kyodai.ArenaMaxX) * 0.5f;
        centerX = kyodai.ClampToArena(x, width * 0.5f);
    }

    // 2) 전조 = 전이: 그림자를 깔고, 손이 배경에서 그림자 위 공중으로 들어온다(가까워지며 커짐)
    public override void OnTellStarted()
    {
        if (telegraph != null) telegraph.Show(centerX, kyodai.GroundY, width);
        limb.SetLayer(LimbLayer.Transition);
        float tell = patternData != null ? patternData.tellDuration : 1.2f;
        limb.MoveToWorld(new Vector2(centerX, kyodai.GroundY + hoverHeight), tell, Ease.OutCubic, destroyCancellationToken).Forget();
    }

    protected override async UniTask ExecuteActionAsync(CancellationToken token)
    {
        // 3) 공격: 내려찍기. 닿는 순간 구역 안에 있으면 맞는다
        limb.KillTweens();
        limb.SetLayer(LimbLayer.Attack);
        await limb.MoveToWorld(new Vector2(centerX, kyodai.GroundY + palmThickness * 0.5f), slamSeconds, Ease.InQuad, token);
        if (telegraph != null) telegraph.Hide();
        CrushPlayer();

        // 4) 잔류: 손등 위를 때릴 수 있다
        if (limb.HurtZone != null) limb.HurtZone.SetWorldSize(new Vector2(width, palmThickness));
        limb.SetLayer(LimbLayer.Linger);
        await UniTask.Delay(TimeSpan.FromSeconds(lingerSeconds), cancellationToken: token);

        // 5) 복귀
        limb.SetLayer(LimbLayer.Return);
        await limb.ReturnToRest(returnSeconds, token);
        limb.SetLayer(LimbLayer.Background);
    }

    private void CrushPlayer()
    {
        Vector2 center = new Vector2(centerX, kyodai.GroundY + crushHeight * 0.5f);
        foreach (var hit in Physics2D.OverlapBoxAll(center, new Vector2(width, crushHeight), 0f))
        {
            if (!hit.CompareTag("Player")) continue;
            ApplyDamageToPlayer(hit.gameObject); // KKH: 회피 무적 검사 + 패턴 데이터의 데미지·부위 피해
            break;
        }
    }

    protected override void CleanupEffects()
    {
        if (telegraph != null) telegraph.Hide();
        if (limb != null && limb.Layer != LimbLayer.Background) limb.SnapToRest();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var k = GetComponentInParent<KyodaiBossController>();
        if (k == null) return;
        float w = k.ArenaWidth * areaRatio;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
        Gizmos.DrawWireCube(new Vector3(k.ArenaMinX + w * 0.5f, k.GroundY + crushHeight * 0.5f), new Vector3(w, crushHeight, 0f));
    }
#endif
}
