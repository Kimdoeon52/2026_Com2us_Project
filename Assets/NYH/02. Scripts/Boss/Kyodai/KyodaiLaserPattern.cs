using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using UnityEngine;

/// <summary>어느 레이저(어깨)가 쏠지 (2026-11-05 어깨 레이저 2개)</summary>
public enum KyodaiLaserChoice
{
    Fixed,     // laser 칸에 넣은 것 하나
    Alternate, // 왼어깨·오른어깨 번갈아
    Both       // 둘 다 동시에 (X자 쓸기) — 두 레이저를 합쳐 패턴당 한 번만 맞는다
    // 새 값은 맨 뒤에만 추가할 것 — 에셋에 숫자로 저장된다
}

/// <summary>
/// 쿄다이 레이저 패턴 (2026-11-04 2D 개편). 레이저가 플레이어를 노리고 한 발 쏜다 — 손에 맞으면 그 손이 정지한다.
/// 흐름(KKH BossPatternGimmickBase 골격 위):
///   OnTellStarted 레이저 시작(조준·고정 단계가 곧 예고). 동반 손 동작이 있으면 같이 시작
///   ExecuteAction 레이저와 동반 손 동작이 둘 다 끝날 때까지 기다림
/// BossPatternData.tellDuration은 조준 + 고정 길이와 맞춰두면 된다(DPS 저지 시간). 데미지·부위 피해는 패턴 데이터.
///
/// 동반 손 동작(선택): 패턴은 하나씩 돌아서 레이저만 쏘면 손은 대기 자리(본체 양옆 위)에 있다 — 그러면 레이저를 손으로
/// 유도하려면 플레이어가 경기장 끝 근처에 가야 한다. 손 동작을 같이 걸면 "레이저를 공격 중인 손에 맞혀 멈추게 하는" 판단이 생긴다.
///
/// 2026-11-05 어깨 레이저 2개: emitter로 어느 어깨가 쏠지 — 번갈아(일반·쓸기) / 둘 다(X자 쓸기).
/// </summary>
public class KyodaiLaserPattern : BossPatternGimmickBase, IKyodaiPatternUse
{
    [Tooltip("emitter = Fixed일 때 쏘는 레이저")]
    [SerializeField] private KyodaiLaser laser;

    [Tooltip("어느 레이저가 쏠지 (2026-11-05)")]
    [SerializeField] private KyodaiLaserChoice emitter = KyodaiLaserChoice.Fixed;

    [Tooltip("레이저 수치 (조준·발사 길이, 굵기, 손 정지 시간)")]
    [SerializeField] private KyodaiLaserData laserData;

    [Header("동반 손 동작 (선택)")]
    [Tooltip("레이저와 동시에 재생할 손 동작. 비우면 레이저만")]
    [SerializeField] private KyodaiHandMotion companionMotion;

    [SerializeField] private KyodaiHandChoice companionHand = KyodaiHandChoice.NearestToPlayer;

    [Tooltip("동반 손 동작의 바닥 예고 그림자")]
    [SerializeField] private GroundTelegraph telegraph;

    private KyodaiBossController kyodai;
    private KyodaiHand hand;
    private int alternate, laserAlternate;
    private readonly List<KyodaiLaser> firing = new List<KyodaiLaser>(2);
    private int sharedHitFrame = -1;

    public KyodaiLaserData LaserData => laserData;
    public KyodaiHandMotion CompanionMotion => companionMotion;
    public KyodaiLaserChoice Emitter => emitter;
    public bool UsesHands => companionMotion != null;
    public int HandsNeeded => companionMotion != null ? 1 : 0;
    public int LasersNeeded => emitter == KyodaiLaserChoice.Both && kyodai != null ? Mathf.Max(1, kyodai.Lasers.Count) : 1;

    protected override void OnAttached()
    {
        kyodai = OwnerBoss as KyodaiBossController;
        if (laser == null) laser = OwnerBoss != null ? OwnerBoss.GetComponentInChildren<KyodaiLaser>(true) : null;
        if (laser == null) Debug.LogError($"[KyodaiLaserPattern] {name}: 쿄다이 밑에 KyodaiLaser가 없음");
        if (laserData == null) Debug.LogWarning($"[KyodaiLaserPattern] {name}: 레이저 수치가 비어 있음");
    }

    protected override void OnPreExecute()
    {
        hand = companionMotion != null && kyodai != null ? kyodai.PickHand(companionHand, ref alternate) : null;
        firing.Clear();
        sharedHitFrame = -1;
        if (emitter == KyodaiLaserChoice.Fixed || kyodai == null) { if (laser != null) firing.Add(laser); return; }
        if (emitter == KyodaiLaserChoice.Both) { foreach (var l in kyodai.Lasers) if (l != null) firing.Add(l); return; }
        var picked = kyodai.PickLaser(ref laserAlternate);
        if (picked != null) firing.Add(picked);
    }

    public override void OnTellStarted()
    {
        if (laserData != null)
        {
            Func<GameObject, bool> touch = firing.Count > 1 ? TouchShared : (Func<GameObject, bool>)ApplyDamageToPlayer;
            foreach (var l in firing) l.Play(laserData, touch);
        }
        if (hand != null) hand.Play(companionMotion, ApplyDamageToPlayer, telegraph);
        if (kyodai != null) kyodai.OnPatternTellStarted(this);
    }

    // 여러 레이저가 같이 쏠 때: 같은 순간(같은 프레임)에 두 줄에 닿으면 한 번만 — X자 엇갈림에 서 있다고 두 배로 맞지 않게.
    // 레이저마다 한 발·폭발마다 한 번은 따로 (혼자 쏘는 쓸기와 같다)
    private bool TouchShared(GameObject player)
    {
        if (sharedHitFrame == Time.frameCount) return true;
        if (!ApplyDamageToPlayer(player)) return false;
        sharedHitFrame = Time.frameCount;
        return true;
    }

    protected override async UniTask ExecuteActionAsync(CancellationToken token)
    {
        await UniTask.WaitUntil(() => !AnyFiring() && (hand == null || !hand.IsPlaying), cancellationToken: token);
        if (kyodai != null) await kyodai.WaitConcurrent(this, token);
    }

    private bool AnyFiring()
    {
        foreach (var l in firing) if (l != null && l.IsActive) return true;
        return false;
    }

    protected override void CleanupEffects()
    {
        foreach (var l in firing) if (l != null) l.Stop();
        if (hand != null) hand.Stop();
        if (telegraph != null) telegraph.Hide();
        if (kyodai != null) kyodai.EndConcurrent(this);
    }
}
