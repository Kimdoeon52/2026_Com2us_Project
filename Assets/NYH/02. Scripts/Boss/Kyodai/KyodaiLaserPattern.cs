using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using UnityEngine;

/// <summary>
/// 쿄다이 레이저 패턴 (2026-11-04 2D 개편). 눈(KyodaiLaser)이 플레이어를 노리고 한 발 쏜다 — 손에 맞으면 그 손이 정지한다.
/// 흐름(KKH BossPatternGimmickBase 골격 위):
///   OnTellStarted 레이저 시작(조준·고정 단계가 곧 예고). 동반 손 동작이 있으면 같이 시작
///   ExecuteAction 레이저와 동반 손 동작이 둘 다 끝날 때까지 기다림
/// BossPatternData.tellDuration은 조준 + 고정 길이와 맞춰두면 된다(DPS 저지 시간). 데미지·부위 피해는 패턴 데이터.
///
/// 동반 손 동작(선택): 패턴은 하나씩 돌아서 레이저만 쏘면 손은 대기 자리(본체 양옆 위)에 있다 — 그러면 레이저를 손으로
/// 유도하려면 플레이어가 경기장 끝 근처에 가야 한다. 손 동작을 같이 걸면 "레이저를 공격 중인 손에 맞혀 멈추게 하는" 판단이 생긴다.
/// 어떤 조합으로 갈지는 플레이해 보며 정한다(기획 미정).
/// </summary>
public class KyodaiLaserPattern : BossPatternGimmickBase
{
    [SerializeField] private KyodaiLaser laser;

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
    private int alternate;

    public KyodaiLaserData LaserData => laserData;
    public KyodaiHandMotion CompanionMotion => companionMotion;

    protected override void OnAttached()
    {
        kyodai = OwnerBoss as KyodaiBossController;
        if (laser == null) laser = OwnerBoss != null ? OwnerBoss.GetComponentInChildren<KyodaiLaser>(true) : null;
        if (laser == null) Debug.LogError($"[KyodaiLaserPattern] {name}: 쿄다이 밑에 KyodaiLaser(눈)가 없음");
        if (laserData == null) Debug.LogWarning($"[KyodaiLaserPattern] {name}: 레이저 수치가 비어 있음");
    }

    protected override void OnPreExecute()
    {
        hand = companionMotion != null && kyodai != null ? kyodai.PickHand(companionHand, ref alternate) : null;
    }

    public override void OnTellStarted()
    {
        if (laser != null && laserData != null) laser.Play(laserData, ApplyDamageToPlayer);
        if (hand != null) hand.Play(companionMotion, ApplyDamageToPlayer, telegraph);
    }

    protected override async UniTask ExecuteActionAsync(CancellationToken token)
    {
        await UniTask.WaitUntil(() => (laser == null || !laser.IsActive) && (hand == null || !hand.IsPlaying), cancellationToken: token);
    }

    protected override void CleanupEffects()
    {
        if (laser != null) laser.Stop();
        if (hand != null) hand.Stop();
        if (telegraph != null) telegraph.Hide();
    }
}
