using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using UnityEngine;

/// <summary>
/// 모션 에셋(KyodaiMotionData) 하나를 재생하는 범용 쿄다이 패턴 (2026-10-29).
/// 패턴마다 클래스를 만들지 않고, 모션 에셋 + 패턴 데이터(BossPatternData)만 바꿔 끼운다.
///
/// 흐름(KKH BossPatternGimmickBase 골격 위):
///   OnPreExecute  목표 = 플레이어 x(경기장 안으로 자름), 바닥 높이
///   OnTellStarted 모션을 0초부터 재생 시작 — 예고(전이) 구간도 모션 안에 있다
///   ExecuteAction 모션이 끝날 때까지 기다림. 판정·그림자는 모션 이벤트대로
/// BossPatternData.tellDuration은 DPS 저지(전조 중 누적 피해) 시간 — 모션의 예고 구간 길이와 맞춰두면 된다.
/// </summary>
public class KyodaiMotionPattern : BossPatternGimmickBase
{
    [Tooltip("재생할 동작 (NYH/쿄다이/모션 창에서 만든다)")]
    [SerializeField] private KyodaiMotionData motion;

    [Tooltip("바닥 예고 그림자 (모션의 그림자 이벤트가 켜고 끈다)")]
    [SerializeField] private GroundTelegraph telegraph;

    private KyodaiBossController kyodai;
    private Vector2 target;
    private bool finished;

    public KyodaiMotionData Motion => motion;

    protected override void OnAttached()
    {
        kyodai = OwnerBoss as KyodaiBossController;
        if (kyodai == null) Debug.LogError("[KyodaiMotionPattern] KyodaiBossController 밑에 있어야 함");
        if (motion == null) Debug.LogWarning($"[KyodaiMotionPattern] {name}: 모션이 비어 있음");
    }

    protected override void OnPreExecute()
    {
        finished = false;
        target = PickTarget(kyodai, motion);
    }

    /// <summary>플레이어 발밑 x를 경기장 안으로 자른 자리. 모션 창 미리보기도 같은 규칙</summary>
    public static Vector2 PickTarget(KyodaiBossController boss, KyodaiMotionData motion)
    {
        var player = GameObject.FindWithTag("Player");
        float x = player != null ? player.transform.position.x : (boss.ArenaMinX + boss.ArenaMaxX) * 0.5f;
        return new Vector2(boss.ClampToArena(x, motion != null ? motion.targetMargin : 0f), boss.GroundY);
    }

    public override void OnTellStarted()
    {
        if (motion == null) { finished = true; return; }
        Run(_patternCTS.Token).Forget();
    }

    protected override async UniTask ExecuteActionAsync(CancellationToken token)
    {
        await UniTask.WaitUntil(() => finished, cancellationToken: token);
    }

    private async UniTaskVoid Run(CancellationToken token)
    {
        float t = 0f, prev = -1f;
        try
        {
            while (true)
            {
                KyodaiMotionPlayer.ApplyPose(motion, t, target, kyodai);
                KyodaiMotionPlayer.FireEvents(motion, prev, t, OnEvent);
                if (t >= motion.duration) break;
                prev = t;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                t = Mathf.Min(t + Time.deltaTime, motion.duration);
            }
            finished = true;
        }
        catch (OperationCanceledException) { }
    }

    private void OnEvent(KyodaiMotionEvent e)
    {
        switch (e.type)
        {
            case KyodaiMotionEventType.Hit:
                foreach (var hit in Physics2D.OverlapBoxAll(target + e.center, e.size, 0f))
                {
                    if (!hit.CompareTag("Player")) continue;
                    ApplyDamageToPlayer(hit.gameObject); // KKH: 회피 무적 검사 + 패턴 데이터의 데미지·부위 피해
                    break;
                }
                break;
            case KyodaiMotionEventType.TelegraphShow:
                if (telegraph != null) telegraph.Show(target.x + e.center.x, kyodai.GroundY, e.size.x);
                break;
            case KyodaiMotionEventType.TelegraphHide:
                if (telegraph != null) telegraph.Hide();
                break;
        }
    }

    protected override void CleanupEffects()
    {
        if (telegraph != null) telegraph.Hide();
        KyodaiMotionPlayer.Clear(kyodai); // 온몸 애니메이션 풀고 대기 자세로
    }
}
