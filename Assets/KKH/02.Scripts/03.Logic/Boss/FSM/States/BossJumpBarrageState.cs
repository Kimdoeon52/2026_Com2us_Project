using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace RealSteel.Boss.FSM
{
    /// <summary>
    /// [보스 패턴 1: 타이어 사출 바이킹 반원 탄막 (BossJumpBarrageState)]
    /// 기획서 및 최신 피드백 준수:
    /// 1. 보스 본체는 지상 유지, 등 뒤의 타이어를 [보스와 플레이어의 중간 지점 상단]으로 사출 (높이 대폭 하향: 1.7m)
    /// 2. 사출된 타이어에서 180도 지상 반원 호를 따라 15도 간격(0~12번)으로 순차적 탄환 투사:
    ///    - 순방향 스윕: 0번(180°) ➔ 1번(195°) ➔ ... ➔ 12번(360°) 차례대로 발사 (틱-틱-틱...)
    ///    - 정점 딜레이: 바이킹 진자 최고점 도달 후 짧은 반전 대기 (0.2초)
    ///    - 역방향 스윕: 12번(360°) ➔ 11번(345°) ➔ ... ➔ 0번(180°) 역순으로 차례대로 발사
    /// 3. 발사 시퀀스 완료 후 타이어가 보스 본체로 고속 회수되며, 보스는 1.0초 확정 딜타임(BossIdleState) 복귀
    /// 4. 타이어 부위 파괴(펑크 2회 누적) 시 사출 높이 30% 감속 반영 (1.7m ➔ 1.2m)
    /// </summary>
    public class BossJumpBarrageState : BossStateBase
    {
        private readonly BossPatternData _pattern;
        private readonly float _launchHeight;       // 중간 상단 타이어 높이 (피드백 반영: 기존 3.5m -> 1.7m로 하향)
        private readonly float _launchDuration;     // 사출 시간 (기본 0.35f)
        private readonly float _bulletInterval;     // 탄환 1발당 순차 발사 딜레이 (기본 0.05f)
        private readonly float _turnaroundDelay;    // 12번 도달 후 역방향 복귀 전 정점 대기시간 (기본 0.2f)
        private readonly int _sweepRounds;          // 왕복 횟수 (기본 1회 = 정방향 1회 + 역방향 1회)
        private readonly float _bulletSpeed;        // 탄환 비행 속도 (기본 6.0f)
        private readonly GameObject _bulletPrefab;  // 탄환 프리팹 (미할당 시 임시 시각 탄환 폴백 생성)
        private readonly GameObject _tirePrefab;    // 타이어 프리팹 (미할당 시 폴백 오브젝트 생성)

        private GameObject _activeTireObject;       // 공중에 사출된 타이어 오브젝트
        private Tweener _tireMoveTween;
        private Tweener _tireRotateTween;
        private Tweener _bossTween;

        public override string StateName => "BossJumpBarrageState (바이킹 순차 반원 탄막)";

        public BossJumpBarrageState(
            BossController boss, 
            CancellationToken token, 
            BossPatternData pattern, 
            GameObject bulletPrefab = null, 
            GameObject tirePrefab = null,
            float launchHeight = 1.7f,
            float launchDuration = 0.35f,
            float bulletInterval = 0.05f,
            float turnaroundDelay = 0.2f,
            int sweepRounds = 1,
            float bulletSpeed = 6.0f) : base(boss, token)
        {
            _pattern = pattern;
            _bulletPrefab = bulletPrefab;
            _tirePrefab = tirePrefab;
            _launchHeight = launchHeight;
            _launchDuration = launchDuration;
            _bulletInterval = bulletInterval;
            _turnaroundDelay = turnaroundDelay;
            _sweepRounds = sweepRounds;
            _bulletSpeed = bulletSpeed;
        }

        public override void Enter()
        {
            ExecuteVikingSequentialBarrageAsync().Forget();
        }

        public override void Exit()
        {
            base.Exit();
            CleanupAllTweensAndTire();
        }

        private async UniTaskVoid ExecuteVikingSequentialBarrageAsync()
        {
            try
            {
                // 1단계: 보스와 플레이어의 중간 지점 상단(Midpoint Apex) 좌표 산출 (높이 1.7m)
                Vector3 apexPos = CalculateMidpointApex();

                // 2단계: 등 뒤의 타이어를 중간 상단으로 고속 사출
                _activeTireObject = SpawnOrGetTireObject(boss.transform.position);
                
                _tireMoveTween = _activeTireObject.transform.DOMove(apexPos, _launchDuration).SetEase(Ease.OutBack);
                _tireRotateTween = _activeTireObject.transform.DORotate(new Vector3(0, 0, -360), _launchDuration, RotateMode.FastBeyond360);
                
                // 보스 본체: 타이어 사출 반동 모션
                _bossTween = boss.transform.DOPunchScale(new Vector3(0.12f, -0.12f, 0f), 0.25f);
                
                await _tireMoveTween.ToUniTask(cancellationToken: cancellationToken);

                // 3단계: 바이킹 진자 순차 탄막 (15도 간격, 0번~12번 순차 발사 후 12번~0번 역순 복귀)
                // 지상 반원 180도 커버 공식: Base Angle 180°(좌측 수평) + (index * 15°)
                // 0번 = 180° (좌측 수평)
                // 6번 = 270° (지상 수직 아래)
                // 12번 = 360° / 0° (우측 수평)
                const int maxIndex = 12;
                Vector3 fireOrigin = _activeTireObject.transform.position;

                for (int round = 0; round < _sweepRounds; round++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // --- [순방향 스윕]: 0번부터 12번까지 차례대로 발사 (좌 ➔ 아래 ➔ 우) ---
                    Debug.Log($"<color=cyan>[BossJumpBarrage] 바이킹 탄막 정방향 스윕 시작 (0번 ➔ 12번)</color>");
                    for (int i = 0; i <= maxIndex; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        float angle = 180f + (i * 15f);
                        FireSingleBullet(fireOrigin, angle);

                        // 타이어 틸트 연출: 발사 각도 방향으로 살짝 흔들림
                        float tiltZ = Mathf.Sin((i / (float)maxIndex) * Mathf.PI) * 20f;
                        _activeTireObject.transform.rotation = Quaternion.Euler(0, 0, tiltZ);

                        await UniTask.Delay(TimeSpan.FromSeconds(_bulletInterval), cancellationToken: cancellationToken);
                    }

                    // --- [정점 대기 (바이킹 반전)]: 최고점에서 잠시 멈춤 ---
                    await UniTask.Delay(TimeSpan.FromSeconds(_turnaroundDelay), cancellationToken: cancellationToken);

                    // --- [역방향 스윕]: 12번부터 0번으로 거꾸로 차례대로 발사 (우 ➔ 아래 ➔ 좌) ---
                    Debug.Log($"<color=yellow>[BossJumpBarrage] 바이킹 탄막 역방향 스윕 시작 (12번 ➔ 0번)</color>");
                    for (int i = maxIndex; i >= 0; i--)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        float angle = 180f + (i * 15f);
                        FireSingleBullet(fireOrigin, angle);

                        // 타이어 틸트 연출: 역방향 흔들림
                        float tiltZ = -Mathf.Sin((i / (float)maxIndex) * Mathf.PI) * 20f;
                        _activeTireObject.transform.rotation = Quaternion.Euler(0, 0, tiltZ);

                        await UniTask.Delay(TimeSpan.FromSeconds(_bulletInterval), cancellationToken: cancellationToken);
                    }

                    // 다음 라운드가 있을 경우 짧은 간격
                    if (round < _sweepRounds - 1)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(0.15f), cancellationToken: cancellationToken);
                    }
                }

                // 4단계: 타이어 회수 (지상의 보스 본체로 날아와 결합)
                if (_activeTireObject != null)
                {
                    _tireMoveTween = _activeTireObject.transform.DOMove(boss.transform.position, 0.3f).SetEase(Ease.InQuad);
                    _tireRotateTween = _activeTireObject.transform.DORotate(new Vector3(0, 0, 720), 0.3f, RotateMode.FastBeyond360);
                    await _tireMoveTween.ToUniTask(cancellationToken: cancellationToken);

                    // 보스 타이어 장착 충격 찌그러짐 (Squash)
                    _bossTween = boss.transform.DOPunchScale(new Vector3(0.2f, -0.2f, 0f), 0.2f);
                    await _bossTween.ToUniTask(cancellationToken: cancellationToken);

                    // 타이어 오브젝트 정리
                    UnityEngine.Object.Destroy(_activeTireObject);
                    _activeTireObject = null;
                }

                // 5단계: 기획서 공통 확정 딜타임 (BossIdleState, 1.0초) 자동 복귀
                if (!cancellationToken.IsCancellationRequested && boss.Snapshot != null && boss.Snapshot.IsAlive)
                {
                    float postDelay = _pattern != null ? _pattern.postDelay : 1.0f;
                    var nextToken = boss.RenewStateCancellationToken();
                    boss.StateMachine.ChangeState(new BossIdleState(boss, nextToken, postDelay));
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[BossJumpBarrageState] 바이킹 순차 탄막 시퀀스 취소됨");
            }
            finally
            {
                CleanupAllTweensAndTire();
            }
        }

        /// <summary>
        /// 보스와 플레이어의 중간 지점 X좌표 및 상단 높이를 계산함 (피드백 반영: 기본 높이 1.7m)
        /// </summary>
        private Vector3 CalculateMidpointApex()
        {
            Transform player = GameObject.FindWithTag("Player")?.transform;
            Vector3 bossPos = boss.transform.position;

            float midX;
            float baseY;

            if (player != null)
            {
                // 보스와 플레이어의 중간 X좌표
                midX = (bossPos.x + player.position.x) * 0.5f;
                baseY = Mathf.Max(bossPos.y, player.position.y);
            }
            else
            {
                // 플레이어가 없으면 보스 앞쪽 2.0m 지점
                float forwardDir = boss.transform.localScale.x >= 0 ? -1f : 1f;
                midX = bossPos.x + (forwardDir * 2.0f);
                baseY = bossPos.y;
            }

            // 타이어 파괴 상태 검사: 펑크 2회 누적 시 사출 높이 30% 감소 (1.7m -> 1.19m)
            bool isTireBroken = false;
            var tireGimmick = boss.Snapshot?.GetGimmick("TIRE_PART");
            if (tireGimmick != null && (tireGimmick.isBroken || tireGimmick.triggerCount >= 2))
            {
                isTireBroken = true;
            }

            float finalHeight = isTireBroken ? _launchHeight * 0.7f : _launchHeight;
            return new Vector3(midX, baseY + finalHeight, bossPos.z);
        }

        /// <summary>
        /// 지정 각도로 단일 탄환 발사 (프리팹 미할당 시 폴백 탄환 자동 생성 지원)
        /// </summary>
        private void FireSingleBullet(Vector3 originPos, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f).normalized;

            GameObject bullet;
            if (_bulletPrefab != null)
            {
                bullet = UnityEngine.Object.Instantiate(_bulletPrefab, originPos, Quaternion.identity);
            }
            else
            {
                // 프리팹 미할당 시 순수 2D 전용 폴백 탄환 생성 (주황색 에너지 볼)
                bullet = new GameObject("BossFallbackBullet");
                bullet.transform.position = originPos;
                bullet.transform.localScale = Vector3.one * 0.35f;

                var sr = bullet.AddComponent<SpriteRenderer>();
                sr.sprite = GetOrCreateFallbackBulletSprite();
                sr.color = new Color(1.0f, 0.45f, 0.05f, 1f); // 주황색 발광 느낌

                var col2d = bullet.AddComponent<CircleCollider2D>();
                if (col2d != null)
                {
                    col2d.isTrigger = true;
                    col2d.radius = 0.5f;
                }

                var fallbackRb2d = bullet.AddComponent<Rigidbody2D>();
                fallbackRb2d.gravityScale = 0f;
            }

            // 탄환 진행 방향 회전
            float rotAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0f, 0f, rotAngle);

            // Unity 6 호환 linearVelocity 속도 부여
            if (bullet.TryGetComponent<Rigidbody2D>(out var rb2d))
            {
                rb2d.linearVelocity = dir * _bulletSpeed;
            }
            else if (bullet.TryGetComponent<Rigidbody>(out var rb3d))
            {
                rb3d.linearVelocity = dir * _bulletSpeed;
            }

            // 4초 후 자동 파괴
            UnityEngine.Object.Destroy(bullet, 4.0f);

            // 씬 뷰 궤적 가시화 (주황색 레이)
            Debug.DrawRay(originPos, dir * 2.0f, Color.yellow, 0.5f);
        }

        /// <summary>
        /// 타이어 프리팹을 인스턴스화하거나, 미할당 시 임시 타이어 오브젝트 생성
        /// </summary>
        private GameObject SpawnOrGetTireObject(Vector3 spawnPos)
        {
            if (_tirePrefab != null)
            {
                return UnityEngine.Object.Instantiate(_tirePrefab, spawnPos, Quaternion.identity);
            }

            // 폴백: 보스 스프라이트를 복사한 타이어 비주얼 생성
            GameObject tire = new GameObject("BossFlyingTire");
            tire.transform.position = spawnPos;
            var sr = tire.AddComponent<SpriteRenderer>();

            var bossSr = boss.GetComponentInChildren<SpriteRenderer>();
            if (bossSr != null && bossSr.sprite != null)
            {
                sr.sprite = bossSr.sprite;
                sr.color = new Color(0.25f, 0.25f, 0.25f, 1f); // 짙은 고철 타이어 색상
                tire.transform.localScale = boss.transform.localScale * 0.65f;
            }
            else
            {
                tire.transform.localScale = Vector3.one * 0.7f;
            }

            return tire;
        }

        private static Sprite _fallbackSprite;

        /// <summary>
        /// 탄환 프리팹 미할당 시 사용할 원형 텍스처 스프라이트를 동적으로 생성 및 캐싱함
        /// </summary>
        private static Sprite GetOrCreateFallbackBulletSprite()
        {
            if (_fallbackSprite != null) return _fallbackSprite;

            int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        float alpha = Mathf.Clamp01((radius - dist) + 0.5f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
            return _fallbackSprite;
        }

        private void CleanupAllTweensAndTire()
        {
            if (_tireMoveTween != null && _tireMoveTween.IsActive()) _tireMoveTween.Kill();
            if (_tireRotateTween != null && _tireRotateTween.IsActive()) _tireRotateTween.Kill();
            if (_bossTween != null && _bossTween.IsActive()) _bossTween.Kill();

            if (_activeTireObject != null)
            {
                UnityEngine.Object.Destroy(_activeTireObject);
                _activeTireObject = null;
            }
        }
    }
}
