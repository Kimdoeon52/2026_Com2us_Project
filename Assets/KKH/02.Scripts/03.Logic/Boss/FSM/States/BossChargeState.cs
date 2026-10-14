using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEditor.PackageManager;
using UnityEngine;

namespace RealSteel.Boss.FSM
{
    /// <summary>
    /// [보스패턴 2: 돌진 상태 (BossChargeState)]
    /// 1. 맵 한쪽 끝에서 반대편 끝까지 가로지르는 고속 횡단 돌진
    /// 2. 타이어 부위 파괴(펑크 2회 누적) 시 돌진 속도 영구 감속 (18m/s -> 11m/s)
    /// 3. 플레이어 충돌 시 Space 회피 무적(0.3s) 판정: 무적 중이면 통과, 아니면 큰 피해
    /// 4. 폭발 화물 상자 충돌 시 상자 자폭 및 보스 2초 스턴 즉시 인터럽트
    /// 5. 반대편 벽 충돌 후 기획서 공통 1.0초 확정 딜타임(BossIdleState)으로 자동 복귀
    /// </summary>
    public class BossChargeState : BossStateBase
    {
        private readonly BossPatternData _pattern; // 돌진 패턴 데이터
        private readonly float _chargeSpeed; // 돌진 속도 (m/s)
        private readonly float _brokenTireSpeed; // 타이어 부위 파괴 시 감속 속도 (m/s)
        private readonly float _anticipatedDuration; // 예상 돌진 지속 시간 (기본 11.0f초)
        private readonly float _wallStunDelay; // 벽 충돌 후 스턴 딜레이 (기본 0.2f)
        private readonly float _maxMapDistance; // 맵 가로 길이 (기본 22.0f)
        private readonly int _chargeDamage; // 돌진 충돌 시 플레이어에게 입히는 피해량 (기본 50)

        private Tweener _currentTween; // 현재 진행 중인 DOTween Tween 객체
        private bool _hasHitPlayer = false; // 플레이어 충돌 여부
        private float _chargeDirection = 1.0f; // 돌진 방향 (1.0f: 오른쪽, -1.0f: 왼쪽)
        
        public override string StateName => "BossChargeState (돌진 상태)";

        public BossChargeState(
            BossController boss, 
            CancellationToken token, 
            BossPatternData pattern, 
            float chargeSpeed = 18.0f,
            float brokenTireSpeed = 11.0f,
            float anticipatedDuration = 0.2f,
            float wallStunDelay = 0.3f,
            float maxMapDistance = 22.0f,
            int chargeDamage = 50) : base(boss, token)
        {
            _pattern = pattern;
            _chargeSpeed = chargeSpeed;
            _brokenTireSpeed = brokenTireSpeed;
            _anticipatedDuration = anticipatedDuration;
            _wallStunDelay = wallStunDelay;
            _maxMapDistance = maxMapDistance;
            _chargeDamage = chargeDamage;
        }
        public override void Enter()
        {
            _hasHitPlayer = false;
            DetermineChargeDirection();
            ExecuteChargeSequenceAsync().Forget();
        }
        public override void Exit()
        {
            base.Exit();
            KillCurrentTween();
        }

        private void DetermineChargeDirection()
        {
            Transform playerTransform = GameObject.FindWithTag("Player")?.transform;
            if (playerTransform != null)
            {
                _chargeDirection = playerTransform.position.x >= boss.transform.position.x ? 1.0f : -1.0f;
            }
            else
            {
                _chargeDirection = boss.transform.localScale.x >= 0 ? 1.0f : -1.0f; // 기본값: 오른쪽
            }
            // 보스 모델/스프라이트 x축 반전 정렬
            Vector3 localScale = boss.transform.localScale;
            boss.transform.localScale = new Vector3(Mathf.Abs(localScale.x) * _chargeDirection, localScale.y, localScale.z);
        }

        private async UniTaskVoid ExecuteChargeSequenceAsync()
        {
            try
            {
                Vector3 startPosition = boss.transform.position;

                // 1단계: 돌진 전 약간 뒤로 당기기 (0.4m)
                Vector3 pullBackPosition = startPosition - new Vector3(_chargeDirection * 0.4f, 0f, 0f);
                _currentTween = boss.transform.DOMoveX(pullBackPosition.x,_anticipatedDuration).SetEase(Ease.OutQuad);
                await _currentTween.ToUniTask(cancellationToken: cancellationToken);

                // 2단계: 타이어 부위 파괴(2회) 여부 검사 -> 돌진 속도 결정
                bool isTireBroken = false; // TODO: 실제 타이어 부위 파괴 여부 확인 로직 필요
                var tireGimmick = boss.Snapshot?.GetGimmick("TIRE_PART");
                if (tireGimmick != null && (tireGimmick.isBroken || tireGimmick.triggerCount >= 2))
                {
                    isTireBroken = true;
                }
                float finalSpeed = isTireBroken ? _brokenTireSpeed : _chargeSpeed;
                if (isTireBroken)
                {
                    Debug.Log($"[BossChargeState] 타이어 부위 파괴 감지됨. 돌진 속도 감속: {_chargeSpeed} -> {_brokenTireSpeed}");
                }

                // 3단계: 반대편 맵 끝(벽) x좌표 및 이동시간 계산
                float targetX = CalculateTargetWallX(startPosition.x, _chargeDirection);
                float distance = Mathf.Abs(targetX - boss.transform.position.x);
                float duration = distance / Mathf.Max(1.0f, finalSpeed);
                Debug.Log($"<color=red>[BossChargeState] ▶ 맵 횡단 돌진 개시! (방향: {_chargeDirection}, 속도: {finalSpeed}m/s, 거리: {distance:F1}m)</color>");

                // 4단계: 맵 끝까지 등속 고속 주행 (DOMoveX Linear) & 실시간 충돌 루프
                _currentTween = boss.transform.DOMoveX(targetX,duration).SetEase(Ease.Linear);

                bool hitExplosiveBox = false;
                while(_currentTween != null && _currentTween.IsActive()&&!_currentTween.IsComplete())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // 충돌 감지 (폭발 상자 자폭 / 플레이어 피격 및 space 무적 회피)
                    if(CheckChargeCollisions())
                    {
                        hitExplosiveBox = true;
                        break;
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken:cancellationToken);
                }
                if(hitExplosiveBox)
                {
                    return;
                }
                // 5단계 : 반대편 벽 충돌 임팩트 연출
                _currentTween = boss.transform.DOPunchScale(new Vector3(-0.25f, 0.25f, 0f), 0.2f);
                await _currentTween.ToUniTask(cancellationToken: cancellationToken);
                Debug.Log("<color=orange>[BossChargeState] 맵 반대편 벽 충돌! 쿵! (반동 경직)</color>");

                // 짧은 벽 충돌 반동 대기
                await UniTask.Delay(TimeSpan.FromSeconds(_wallStunDelay), cancellationToken: cancellationToken);


                // 6단계 : 기획서 공통규칙 1.0초 확정 딜타임(BossIdleState) 복귀
                if(!cancellationToken.IsCancellationRequested && boss.Snapshot != null && boss.Snapshot.IsAlive)
                {
                    float postDelay = _pattern != null ? _pattern.postDelay : 1.0f;
                    var nextToken = boss.RenewStateCancellationToken();
                    boss.StateMachine.ChangeState(new BossIdleState(boss, nextToken, postDelay));
                }

            }
            catch (OperationCanceledException)
            {
                // 인터럽트 발생 시 안전 종료
                Debug.Log("[BossChargeState] 돌진 시퀀스 취소됨");
            }
            finally
            {
                KillCurrentTween();
            }
        }

        /// <summary>
        ///  돌진 방향으로 맵 끝 벽 좌표를 Raycast로 탐색함 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="chargeDirection"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private float CalculateTargetWallX(float currentX, float directionX)
        {
            RaycastHit2D hit = Physics2D.Raycast
            (
                boss.transform.position,
                Vector2.right * directionX,
                _maxMapDistance,
                LayerMask.GetMask("Wall", "Ground", "Default")
            );
            if (hit.collider != null && hit.collider.gameObject != boss.gameObject)
            {
                return hit.point.x - (directionX * 0.5f);
            }
            return currentX +(directionX * _maxMapDistance);
        }
        private bool CheckChargeCollisions()
        {
            Vector2 center = boss.transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, 1.3f);

            foreach (var hit in hits)
            {
                if(hit.gameObject == boss.gameObject) 
                {
                    continue;
                }

                // 1. 전장 기물 : 폭발 화물 상자 충돌 (즉시 자폭 및 보스 2초 스턴)
                if (hit.CompareTag("ExplosiveBox")||hit.name.IndexOf("Explosive", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log("<color=red>[BossChargeState] ▶ 폭발 화물 상자 충돌! 자폭 발생 및 보스 2초 스턴!</color>");
                    KillCurrentTween();
                    UnityEngine.Object.Destroy(hit.gameObject);
                    boss.InterruptToStun(2.0f);
                    return true;
                }
                // 2. 플레이어 충돌 : space회피 및 무적 체크
                if (!_hasHitPlayer && hit.CompareTag("Player"))
                {
                    _hasHitPlayer = true;

                    // 남윤호 ActionExecuter 연동 : 회피 무적 상태인지 검사
                    bool isInvincible = false;
                    var executor = hit.GetComponent<ActionExecutor>() ?? hit.GetComponentInParent<ActionExecutor>();
                    if(executor != null && executor.State != null && executor.State.CurrentAction!= null)
                    {
                        isInvincible = executor.State.CurrentAction.IsInvincibleDuringActive;
                    }
                    if (isInvincible)
                    {
                        // 기획서 : space회피 무적으로 돌진을 회피함
                        Debug.Log("<color=cyan>[BossChargeState] ▶ [회피 성공!] 플레이어가 Space 무적 회피로 돌진을 통과했습니다!</color>");
                    }
                    else
                    {
                        // 무적 회피 실패 시 코어 피해 적용
                        Debug.Log($"<color=red>[BossChargeState] ▶ [피격!] 플레이어가 돌진에 직격당함! (피해량: {_chargeDamage})</color>");
                        if (CombatDataHub.Instance != null)
                        {
                            CombatDataHub.Instance.ApplyCoreDamage("Player", _chargeDamage);
                        }
                    }
                }    
            }
            return false;
        }

       private void KillCurrentTween()
        {
            if (_currentTween != null && _currentTween.IsActive())
            {
                _currentTween.Kill();
                _currentTween = null;
            }
        }
    }

}
