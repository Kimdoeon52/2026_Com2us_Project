using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
            float jumpHeight = 5.0f,
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
                    // FireWave(angleOffset);

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
                
                await boss.transform.DOPunchScale(new Vector3(0.2f, -0.2f, 0f), 0.15f)
                    .ToUniTask(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 이미 취소된 경우
            }
            finally
            {
                // finally
            }
        }
    }
}

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
