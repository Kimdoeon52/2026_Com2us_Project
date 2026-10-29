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

        private Tweener _currentTween;
        private float _groundY;
        private Transform _playerTransform;

        protected override void OnPreExecute()
        {
            _playerTransform = GameObject.FindWithTag("Player")?.transform;
            if (OwnerBoss != null)
            {
                _groundY = OwnerBoss.transform.position.y;
            }
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (OwnerBoss == null) return;

            for (int round = 1; round <= slamRounds; round++)
            {
                token.ThrowIfCancellationRequested();

                Debug.Log($"<color=yellow>[JunkWheelerSlamPattern] ▶ 내려찍기 {round}/{slamRounds}회차 개시!</color>");

                // 1단계: 공중 도약 및 도약 애니메이션
                if (OwnerBoss.Animation != null)
                {
                    OwnerBoss.Animation.PlaySlamJump();
                }

                _currentTween = OwnerBoss.transform.DOMoveY(_groundY + jumpHeight, jumpDuration).SetEase(Ease.OutQuad);
                await _currentTween.ToUniTask(cancellationToken: token);

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
                            newX = OwnerBoss.ClampToCameraX(newX, 0.65f);
                            // 추적 방향으로 스프라이트 반전
                            float dirX = targetX >= currentX ? 1f : -1f;
                            OwnerBoss.Animation?.FaceDirection(dirX);
                        }
                        OwnerBoss.transform.position = new Vector3(newX, OwnerBoss.transform.position.y, OwnerBoss.transform.position.z);
                    }

                    elapsed += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                }

                // 3단계: 급강하 직전 공중 정지 (예고)
                if (OwnerBoss.Animation != null)
                {
                    OwnerBoss.Animation.PlaySlamFall();
                }

                await UniTask.Delay(TimeSpan.FromSeconds(hoverDuration), cancellationToken: token);

                // 4단계: 급강하 내려찍기 (지면 충돌)
                _currentTween = OwnerBoss.transform.DOMoveY(_groundY, slamDuration).SetEase(Ease.InQuad);
                await _currentTween.ToUniTask(cancellationToken: token);

                // 착지 충격 연출 (바닥 찌그러짐 및 착지 모션)
                if (OwnerBoss.Animation != null)
                {
                    OwnerBoss.Animation.PlaySlamLand();
                }

                _currentTween = OwnerBoss.transform.DOPunchScale(new Vector3(0.35f, -0.35f, 0f), 0.25f);
                await _currentTween.ToUniTask(cancellationToken: token);
                Debug.Log($"<color=orange>[JunkWheelerSlamPattern] 내려찍기 {round}회차 착지 완료! (쾅!)</color>");

                // 5단계: 착지 반경 1.0m 내 고철 못판(NailBoardFieldGimmick) 감지 검사
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

                // 6단계: 고철 못판이 없으면 플레이어 피격 판정 (Space 무적 검사 및 LeftLeg 파츠 타격)
                CheckPlayerHit();

                // 다음 회차 전 짧은 딜레이
                if (round < slamRounds)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: token);
                }
            }

            // 모든 회차 종료 후 Idle 복귀
            if (OwnerBoss.Animation != null)
            {
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

            Vector2 center = OwnerBoss.transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, impactRadius);

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
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
        }

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            KillCurrentTween();
            ResetToGround();
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
            if (OwnerBoss != null && OwnerBoss.transform.position.y > _groundY)
            {
                OwnerBoss.transform.position = new Vector3(OwnerBoss.transform.position.x, _groundY, OwnerBoss.transform.position.z);
            }
        }
    }
}
