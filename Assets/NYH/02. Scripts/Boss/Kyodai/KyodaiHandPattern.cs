using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using UnityEngine;

/// <summary>패턴이 쓸 손</summary>
public enum KyodaiHandChoice { Left, Right, NearestToPlayer, Alternate, FarthestFromPlayer }

/// <summary>
/// 손 동작 에셋(KyodaiHandMotion) 하나를 재생하는 범용 쿄다이 패턴 (2026-11-04 2D 개편).
/// 패턴마다 클래스를 만들지 않고 동작 에셋 + 패턴 데이터(BossPatternData)만 바꿔 끼운다.
///
/// 흐름(KKH BossPatternGimmickBase 골격 위):
///   OnPreExecute  쓸 손 고르기
///   OnTellStarted 동작 재생 시작 — 예고(조준·끌어올림)도 동작 안에 있다
///   ExecuteAction 동작이 끝날 때까지 기다림. 판정·그림자는 동작 키대로
/// BossPatternData.tellDuration은 DPS 저지(전조 중 누적 피해) 시간 — 동작의 예고 키 길이와 맞춰두면 된다.
/// 데미지·부위 피해는 패턴 데이터(KKH ApplyDamageToPlayer — 회피 무적 검사 포함).
/// </summary>
public class KyodaiHandPattern : BossPatternGimmickBase, IKyodaiPatternUse
{
    [Tooltip("재생할 손 동작")]
    [SerializeField] private KyodaiHandMotion motion;

    [Tooltip("어느 손으로")]
    [SerializeField] private KyodaiHandChoice handChoice = KyodaiHandChoice.Alternate;

    [Tooltip("바닥 예고 그림자 (동작 키의 telegraphWidth가 켜고 끈다)")]
    [SerializeField] private GroundTelegraph telegraph;

    private KyodaiBossController kyodai;
    private KyodaiHand hand;
    private int alternate;

    public KyodaiHandMotion Motion => motion;
    public bool UsesHands => true;
    public int HandsNeeded => 1;
    public int LasersNeeded => 0;

    protected override void OnAttached()
    {
        kyodai = OwnerBoss as KyodaiBossController;
        if (kyodai == null) Debug.LogError("[KyodaiHandPattern] KyodaiBossController(2D) 밑에 있어야 함");
        if (motion == null) Debug.LogWarning($"[KyodaiHandPattern] {name}: 손 동작이 비어 있음");
    }

    protected override void OnPreExecute()
    {
        hand = kyodai != null ? kyodai.PickHand(handChoice, ref alternate) : null;
    }

    public override void OnTellStarted()
    {
        if (hand == null || motion == null) return;
        hand.Play(motion, ApplyDamageToPlayer, telegraph);
        if (kyodai != null) kyodai.OnPatternTellStarted(this); // 난이도에 따라 같이 나올 패턴
    }

    protected override async UniTask ExecuteActionAsync(CancellationToken token)
    {
        await UniTask.WaitUntil(() => hand == null || !hand.IsPlaying, cancellationToken: token);
        if (kyodai != null) await kyodai.WaitConcurrent(this, token); // 같이 나온 패턴도 끝나야 딜타임
    }

    protected override void CleanupEffects()
    {
        if (hand != null) hand.Stop();
        if (telegraph != null) telegraph.Hide();
        if (kyodai != null) kyodai.EndConcurrent(this);
    }
}
