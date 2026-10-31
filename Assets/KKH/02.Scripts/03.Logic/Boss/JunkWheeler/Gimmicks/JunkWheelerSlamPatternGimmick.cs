using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using RealSteel.Boss.Core;
using RealSteel.Boss.Visual;

namespace RealSteel.Boss.JunkWheeler
{
    /// <summary>
    /// [정크 휠러 3단 내려찍기 & 못판 유도 기믹 (JunkWheelerSlamPatternGimmick)]
    /// 기획서 §6.18 및 §3.2 (5), §4.1:
    /// 1. 공중으로 높이 도약(Jump) ➔ 1초간 플레이어 X좌표 추적 ➔ 0.4초 공중 정지 ➔ 급강하 내려찍기 (총 3회 반복).
    /// 2. 피격 시 플레이어의 LeftLeg(왼다리) 부위 내구도 30 타격 및 코어 50 피해 동시 적용.
    /// 3. 착지 지점 반경 1.0m 내에 고철 못판(NailBoardFieldGimmick)이 감지되면:
    ///    - 못판 기믹 트리거 ➔ 타이어 펑크 발생 ➔ 남은 내려찍기 즉시 취소!
    ///    - 보스를 4초 그로기(받는 피해 1.5배)로 전이시키며 패턴 즉각 종료.
    /// 4. 비선형 텐션 & 비대칭 펀치(Squash & Stretch) 연출 탑재:
    ///    - 도약 전 웅크림(Squash) ➔ 도약 순간 상단 신장(Stretch).
    ///    - 급강하 지면 충돌 순간 초고속 압착(0.04s) ➔ 탄성 로그 복원(0.28s).
    /// </summary>
    public class JunkWheelerSlamPatternGimmick : BossPatternGimmickBase
    {
        [Header("내려찍기 파라미터")]
        [SerializeField] private float jumpHeight = 3.0f;
        [SerializeField] private float jumpDuration = 0.4f;
        [SerializeField] private float trackingDuration = 1.0f;
        [SerializeField] private float hoverDuration = 0.4f;
        [SerializeField] private float slamDuration = 0.22f;
        [SerializeField] private float impactRadius = 1.2f;
        [SerializeField] private float nailBoardDetectRadius = 1.0f;
        [SerializeField] private int slamRounds = 3;

        [Header("비대칭 임팩트 펀치 파라미터")]
        [SerializeField] private Vector2 slamSquashScale = new Vector2(1.45f, 0.50f);
        [SerializeField] private float squashFastInDuration = 0.04f;
        [SerializeField] private float restoreElasticDuration = 0.28f;

        private Tweener _currentTween;
        private float _groundY;
        private Transform _playerTransform;

        protected override void OnPreExecute()
        {
            _playerTransform = GameObject.FindWithTag("Player")?.transform;
            if (OwnerBoss != null)
            {
                _groundY = OwnerBoss.GroundY;
                OwnerBoss.SnapToGroundY();
                OwnerBoss.FacePlayer();
            }
        }

        /// <summary>
        /// 전조(1.0초) 동안 정지 화면 대신 바퀴 공회전(차지 루프) 모션 재생
        /// </summary>
        public override void OnTellStarted()
        {
            OwnerBoss?.Animation?.PlayChargeLoop();
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (OwnerBoss == null) return;

            for (int round = 1; round <= slamRounds; round++)
            {
                token.ThrowIfCancellationRequested();

                Debug.Log($"<color=yellow>[JunkWheelerSlamPattern] ▶ 내려찍기 {round}/{slamRounds}회차 개시!</color>");

                // [텐션 1단계]: 도약 직전 바닥 웅크림 (Squash Anticipation)
                if (OwnerBoss.Animation != null)
                {
                    await OwnerBoss.Animation.PlayImpactSquashAndStretchAsync(
                        OwnerBoss.transform, new Vector2(1.20f, 0.70f), 0.08f, 0.10f, token);
                }

                // [텐션 2단계]: 공중 도약 및 도약 애니메이션
                if (OwnerBoss.Animation != null)
                {
                    OwnerBoss.Animation.PlaySlamJump();
                }

                _currentTween = OwnerBoss.transform.DOMoveY(_groundY + jumpHeight, jumpDuration).SetEase(Ease.OutQuad);
                await _currentTween.ToUniTask(cancellationToken: token);

                // 상승 완료 후 공중 추적 구간: 정지 화면 대신 공중 회전 루프
                OwnerBoss.Animation?.PlayRollBarrage();

                // 2단계: 1초간 플레이어 X좌표 실시간 추적 (그림자/타겟팅)
                float elapsed = 0f;
                while (elapsed < trackingDuration)
                {
                    token.ThrowIfCancellationRequested();

                    if (_playerTransform != null)
                    {
                        float currentX = OwnerBoss.transform.position.x;
                        float targetX = _playerTransform.position.x;
                        float newX = Mathf.MoveTowards(currentX, targetX, 7.0f * Time.deltaTime);
                        if (OwnerBoss != null)
                        {
                            newX = OwnerBoss.ClampToStageWallX(newX, 0.50f);
                            // 추적 방향으로 스프라이트 반전
                            float dirX = targetX >= currentX ? 1f : -1f;
                            OwnerBoss.Animation?.FaceDirection(dirX);
                        }
                        OwnerBoss.transform.position = new Vector3(newX, OwnerBoss.transform.position.y, OwnerBoss.transform.position.z);
                    }

                    elapsed += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                }

                // 3단계: 급강하 직전 공중 정지 (예고) - 회전 루프 유지
                await UniTask.Delay(TimeSpan.FromSeconds(hoverDuration), cancellationToken: token);

                // 4단계: 급강하 내려찍기 (지면 충돌) - 실제 낙하 시점에 낙하 루프 재생
                OwnerBoss.Animation?.PlaySlamFall();
                _currentTween = OwnerBoss.transform.DOMoveY(_groundY, slamDuration).SetEase(Ease.InQuad);
                await _currentTween.ToUniTask(cancellationToken: token);

                // [지면 충돌 즉시 판정 (Frame 0)]: 충돌하는 그 순간 딜레이 없이 못판 및 피격 판정!
                if (CheckNailBoardInteraction())
                {
                    Debug.Log("<color=red>[JunkWheelerSlamPattern] ▶ 고철 못판 착지 감지! 패턴 즉시 중단 및 4초 그로기 전이!</color>");
                    if (OwnerBoss.Animation != null)
                    {
                        OwnerBoss.Animation.PlayGroggy();
                    }
                    InterruptPattern(InterruptReason.GimmickGroggy);
                    return; // 남은 내려찍기 회차 즉시 중단
                }

                // 고철 못판이 없으면 착지 순간 즉시 플레이어 피격 판정 (쾅! 충돌과 동시에 대미지 적용)
                CheckPlayerHit();

                // 5단계: 착지 충격 연출 (바닥 찌그러짐 및 착지 모션)
                if (OwnerBoss.Animation != null)
                {
                    OwnerBoss.Animation.PlaySlamLand();
                    // 비대칭 펀치(Squash & Stretch): 0.04s 초고속 지면 압착 -> 0.28s 탄성 복원
                    await OwnerBoss.Animation.PlayImpactSquashAndStretchAsync(
                        OwnerBoss.transform, slamSquashScale, squashFastInDuration, restoreElasticDuration, token);
                }
                Debug.Log($"<color=orange>[JunkWheelerSlamPattern] 내려찍기 {round}회차 착지 완료! (쾅! 비대칭 펀치 적용)</color>");

                // 다음 회차 전 짧은 딜레이
                if (round < slamRounds)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(0.2f), cancellationToken: token);
                }
            }

            // 모든 회차 종료 후 스케일 완전 복원 및 플레이어 응시 후 Idle 복귀
            if (OwnerBoss.Animation != null)
            {
                OwnerBoss.Animation.ResetScale();
                OwnerBoss.FacePlayer();
                OwnerBoss.Animation.PlayIdle();
            }
        }

        private bool CheckNailBoardInteraction()
        {
            if (OwnerBoss == null) return false;

            Vector2 center = OwnerBoss.transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, nailBoardDetectRadius);

            foreach (var hit in hits)
            {
                var nailBoard = hit.GetComponent<NailBoardFieldGimmick>() ?? hit.GetComponentInParent<NailBoardFieldGimmick>();
                if (nailBoard != null && nailBoard.IsInteractable)
                {
                    nailBoard.TriggerInteraction(OwnerBoss.gameObject);
                    return true;
                }
            }
            return false;
        }

        private void CheckPlayerHit()
        {
            if (OwnerBoss == null) return;

            Vector2 center = (Vector2)OwnerBoss.transform.position + Vector2.up * 0.4f;
            float radius = Mathf.Max(impactRadius, 1.4f);
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player") || (hit.transform.root != null && hit.transform.root.CompareTag("Player")))
                {
                    ApplyDamageToPlayer(hit.gameObject);
                    break;
                }
            }
        }

        protected override void OnInterrupted(InterruptReason reason)
        {
            base.OnInterrupted(reason);
            KillCurrentTween();
            ResetToGround();
            OwnerBoss?.Animation?.ResetScale();
        }

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            KillCurrentTween();
            ResetToGround();
            OwnerBoss?.Animation?.ResetScale();
        }

        private void KillCurrentTween()
        {
            if (_currentTween != null && _currentTween.IsActive())
            {
                _currentTween.Kill();
                _currentTween = null;
            }
        }

        private void ResetToGround()
        {
            if (OwnerBoss != null)
            {
                OwnerBoss.SnapToGroundY();
            }
        }
    }
}
