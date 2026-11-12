using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using UnityEngine;

/// <summary>
/// 양손을 같이 쓰는 쿄다이 패턴 (2026-11-04 2페이즈 — 양손 협공·양손 번갈아 연타).
/// 왼손·오른손에 동작 에셋을 하나씩 걸고, 한쪽만 늦게 시작시킬 수 있다(delayFrames). 둘 다 끝날 때까지 기다린다.
///
///   · shareHit 켜기(협공): 두 손이 한 번에 닿아도 한 번만 맞는다 — 손뼉으로 양쪽에서 동시에 닿는 동작
///   · shareHit 끄기(연타): 손마다, 찍기마다 따로 맞을 수 있다
/// 손마다 바닥 예고를 따로 둔다(두 손이 동시에 그림자를 쓰면 하나를 서로 뺏는다).
/// 데미지·부위 피해는 패턴 데이터(KKH ApplyDamageToPlayer — 회피 무적 검사 포함).
/// </summary>
public class KyodaiDualHandPattern : BossPatternGimmickBase, IKyodaiPatternUse
{
    [Header("왼손")]
    [SerializeField] private KyodaiHandMotion leftMotion;
    [Tooltip("왼손이 시작하기 전 기다리는 프레임")]
    [Min(0)] [SerializeField] private int leftDelayFrames;
    [SerializeField] private GroundTelegraph leftTelegraph;

    [Header("오른손")]
    [SerializeField] private KyodaiHandMotion rightMotion;
    [Tooltip("오른손이 시작하기 전 기다리는 프레임")]
    [Min(0)] [SerializeField] private int rightDelayFrames;
    [SerializeField] private GroundTelegraph rightTelegraph;

    [Tooltip("켜면 두 손을 합쳐 패턴당 한 번만 맞는다 (손뼉처럼 양쪽에서 동시에 닿는 동작)")]
    [SerializeField] private bool shareHit;

    private KyodaiBossController kyodai;
    private KyodaiHand left, right;
    private bool sharedHitDone;

    public KyodaiHandMotion LeftMotion => leftMotion;
    public KyodaiHandMotion RightMotion => rightMotion;
    public bool UsesHands => true;
    public int HandsNeeded => (leftMotion != null ? 1 : 0) + (rightMotion != null ? 1 : 0);
    public int LasersNeeded => 0;

    protected override void OnAttached()
    {
        kyodai = OwnerBoss as KyodaiBossController;
        if (kyodai == null) Debug.LogError("[KyodaiDualHandPattern] KyodaiBossController(2D) 밑에 있어야 함");
        if (leftMotion == null && rightMotion == null) Debug.LogWarning($"[KyodaiDualHandPattern] {name}: 손 동작이 비어 있음");
    }

    protected override void OnPreExecute()
    {
        int dummy = 0;
        left = kyodai != null && leftMotion != null ? kyodai.PickHand(KyodaiHandChoice.Left, ref dummy) : null;
        right = kyodai != null && rightMotion != null ? kyodai.PickHand(KyodaiHandChoice.Right, ref dummy) : null;
        if (left == right) right = null; // 손이 하나뿐인 보스면 왼손만
        sharedHitDone = false;
    }

    public override void OnTellStarted()
    {
        Func<GameObject, bool> touch = shareHit ? TouchShared : (Func<GameObject, bool>)ApplyDamageToPlayer;
        if (left != null) left.Play(leftMotion, touch, leftTelegraph, leftDelayFrames);
        if (right != null) right.Play(rightMotion, touch, rightTelegraph, rightDelayFrames);
        if (kyodai != null) kyodai.OnPatternTellStarted(this);
    }

    // 두 손 중 먼저 닿은 쪽만 데미지. 이미 맞았으면 "맞은 걸로" 돌려줘서 다른 손도 더 안 본다
    private bool TouchShared(GameObject player)
    {
        if (sharedHitDone) return true;
        if (!ApplyDamageToPlayer(player)) return false;
        sharedHitDone = true;
        return true;
    }

    protected override async UniTask ExecuteActionAsync(CancellationToken token)
    {
        await UniTask.WaitUntil(() => (left == null || !left.IsPlaying) && (right == null || !right.IsPlaying), cancellationToken: token);
        if (kyodai != null) await kyodai.WaitConcurrent(this, token);
    }

    protected override void CleanupEffects()
    {
        if (left != null) left.Stop();
        if (right != null) right.Stop();
        if (leftTelegraph != null) leftTelegraph.Hide();
        if (rightTelegraph != null) rightTelegraph.Hide();
        if (kyodai != null) kyodai.EndConcurrent(this);
    }
}
