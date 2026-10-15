using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Unity.AppUI.Core;
using UnityEngine;

namespace RealSteel.Boss.FSM
{
    /// <summary>
    /// [보스패턴 1: 튀어오르기 탄막 상태 (BossJumpBarrageState)]
    /// 1. 제자리에서 점프 (DOTween)
    /// 2. 공중에서 360도 방향으로 12방향 탄막을 3회(매 회차 15도 오프셋) 발사 후 착지
    /// 3. 착지 후 기획서 확정 딜타임(BossIdleState)으로 복귀
    /// </summary>
    public class BossJumpBarrageState : BossStateBase
    {
        private readonly BossPatternData _pattern;
        private readonly float _jumpHeight; 
        private readonly float _jumpDuration;
        private readonly float _fallDuration;
        private readonly float _waveInterval;
        private readonly int _waveCount;
        private readonly int _bulletCountPerWave;
        private readonly float _bulletSpeed;
        private readonly GameObject _bulletPrefab;

        private Vector3 _originalPos; // 점프 시작 전 위치
        private Tweener _currentTween;
        
        public override string StateName => "BossJumpBarrageState (튀어오르기 탄막 발사)";

        public BossJumpBarrageState(
            BossController boss, 
            CancellationToken token, 
            BossPatternData pattern, 
            GameObject bulletPrefab = null, 
            float jumpHeight = 1.5f,
            float jumpDuration = 0.6f,
            float fallDuration = 0.4f,
            float waveInterval = 0.35f,
            int waveCount = 3,
            int bulletCountPerWave = 12,
            float bulletSpeed = 7.0f) : base(boss, token)
        {
            _pattern = pattern;
            _jumpHeight = jumpHeight;
            _jumpDuration = jumpDuration;
            _fallDuration = fallDuration;
            _waveInterval = waveInterval;
            _waveCount = waveCount;
            _bulletCountPerWave = bulletCountPerWave;
            _bulletSpeed = bulletSpeed;
            _bulletPrefab = bulletPrefab;
        }

        public override void Enter()
        {
            _originalPos = boss.transform.position;
            ExecuteJumpBarrageSequenceAsync().Forget();
        }

        private async UniTask ExecuteJumpBarrageSequenceAsync()
        {
            try
            {
                // 1단계 : 제자리 수직 도약 (ease, OutQuad로 상승 감속 연출)
                Vector3 apexPosition = _originalPos + Vector3.up * _jumpHeight;
                _currentTween = boss.transform.DOMove(apexPosition, _jumpDuration).SetEase(Ease.OutQuad);
                await _currentTween.ToUniTask(cancellationToken: cancellationToken);

                // 2단계 : 공중 체공 및 12방향 탄막 3회 루프
                for (int wave = 0; wave < _waveCount; wave++)
                {
                    // 상공에서 매 회 탄막 웨이브 발사 (매 회차 15도 오프셋)
                    float angleOffset = wave * 15.0f;
                    FireWave(angleOffset, wave);

                    // 마지막 발사가 아니면 딜레이 대기
                    if (wave < _waveCount - 1)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(_waveInterval), cancellationToken: cancellationToken);
                    }
                }

                // 마지막 발사 후 짧은 체공 후 착지
                await UniTask.Delay(TimeSpan.FromSeconds(0.2f), cancellationToken: cancellationToken);
                
                _currentTween = boss.transform.DOMove(_originalPos, _fallDuration).SetEase(Ease.InQuad);
                await _currentTween.ToUniTask(cancellationToken: cancellationToken);
                
                // 3단계 : 착지 후  충격
                _currentTween = boss.transform.DOPunchScale(new Vector3(0.2f, 0.2f, 0), 0.15f);
                await _currentTween.ToUniTask(cancellationToken: cancellationToken);

                // 4단계 : 패턴 종료 후 기획서 확정 딜타임 (1.0f초) 후 Idle 상태로 복귀
                if(!cancellationToken.IsCancellationRequested && boss.Snapshot != null && boss.Snapshot.IsAlive)
                {
                    float postDelay = _pattern != null ? _pattern.postDelay : 1.0f;
                    var nextToken = boss.RenewStateCancellationToken();
                    boss.StateMachine.ChangeState(new BossIdleState(boss, nextToken, postDelay));
                }
            }
            catch (OperationCanceledException)
            {
                // 외부 기믹 파훼 (못판 착지 -> 4초 그로기 상태) 또는 스턴 인터럽트 발생 시 안전 종료
            }
            finally
            {
                KillCurrentTween();
            }
        }


        private void FireWave(float angleOffset, int waveIndex)
        {
            /// <summary>
            /// 12방향 탄막 발사, 30도 간격 + 회차별 angleOffset 적용
            /// </summary>
            Vector3 spawnPosition = boss.transform.position;
            float stepAngle = 360f / _bulletCountPerWave;

            for (int i = 0; i < _bulletCountPerWave; i++)
            {
                float currentAngle = (i * stepAngle) + angleOffset;
                float radian = currentAngle * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(radian), Mathf.Sin(radian), 0f).normalized;

                if(_bulletPrefab != null)
                {
                    GameObject bullet = UnityEngine.Object.Instantiate(_bulletPrefab, spawnPosition, Quaternion.identity);

                    // 탄환 진행 방향으로 회전 정렬
                    float rotAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    bullet.transform.rotation = Quaternion.Euler(0f, 0f, rotAngle);
                    if (bullet.TryGetComponent<Rigidbody2D>(out var rb2d))
                    {
                        rb2d.linearVelocity = direction * _bulletSpeed;
                    }
                    else if (bullet.TryGetComponent<Rigidbody>(out var rb3d))
                    {
                        rb3d.linearVelocity = direction * _bulletSpeed;
                    }
                    UnityEngine.Object.Destroy(bullet, 5.0f); // 5초 후 자동 제거(메모리 누수 방지)
                }
                Debug.DrawRay(spawnPosition, direction * 2.0f, Color.red, 1.0f); // 디버그용 발사 방향 시각화
            }
            Debug.Log($"<color=orange>[BossJumpBarrageState] 웨이브 {waveIndex + 1}/{_waveCount}발사 완료! (회전 오프셋 : {angleOffset}도, 탄환 수 : {_bulletCountPerWave}개)</color>");
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

/// <summary>
/// DOTween과 UniTask CancellationToken 간의 생명주기 연결 확장 메서드
/// </summary>
public static class TweenAsyncExtensions
{
    public static async UniTask ToUniTask(this Tween tween, CancellationToken cancellationToken = default)
    {
        if (tween == null || !tween.IsActive()) return;

        var utcs = new UniTaskCompletionSource();

        using (cancellationToken.Register(() =>
        {
            if (tween.IsActive()) tween.Kill();
            utcs.TrySetCanceled(cancellationToken);
        }))
        {
            tween.OnComplete(() => utcs.TrySetResult());
            tween.OnKill(() => utcs.TrySetCanceled(cancellationToken));
            await utcs.Task;
        }
    }
}
