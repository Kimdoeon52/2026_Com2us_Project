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
    /// [정크 휠러 바이킹 순차 반원 탄막 기믹 (JunkWheelerBarragePatternGimmick)]
    /// 기획서 §6.18 및 §3.2 (4) (최신 개편 사양):
    /// 1. 타이어 사출 대신 보스 본체가 직접 [보스와 플레이어의 중간 상단]으로 도약 (기본 높이 1.7m).
    /// 2. 공중에서 몸체를 회전(Roll)하며 지상 반원 호를 따라 순차적 탄환 투사:
    ///    - 순방향: 0번(180°) ➔ 끝번(360°) 차례대로 발사 (틱-틱-틱...)
    ///    - 정점 딜레이: 바이킹 진자 최고점 도달 후 반전 대기 (turnaroundDelay)
    ///    - 역방향: 끝번(360°) ➔ 0번(180°) 역순으로 차례대로 발사
    /// 3. 타이어 부위 파괴 시 도약 높이 30% 감축 (1.7m ➔ 1.19m) -> 플레이어 회피 용이.
    /// 4. 발사 완료 후 보스 본체가 지면으로 고속 착지(Slam Land)하며 바닥 충격 연출 후 Idle 복귀.
    /// 5. 비대칭 펀치(Squash & Stretch) 및 도약 텐션 연출 일괄 적용.
    /// </summary>
    public class JunkWheelerBarragePatternGimmick : BossPatternGimmickBase
    {
        [Header("도약 및 탄막 파라미터")]
        [Tooltip("공중 도약 높이 (기준 1.7m)")]
        [SerializeField] private float launchHeight = 1.7f;
        [Tooltip("공중 도약 상승 시간")]
        [SerializeField] private float launchDuration = 0.4f;
        [Tooltip("탄막 발사 후 지면 착지 급강하 시간")]
        [SerializeField] private float landDuration = 0.25f;
        [Tooltip("탄환 발사 간격 (초)")]
        [SerializeField] private float bulletInterval = 0.35f;
        [Tooltip("바이킹 진자 반전 정점 대기 시간 (초)")]
        [SerializeField] private float turnaroundDelay = 0.2f;
        [Tooltip("스윕 반복 횟수 (기본 1회: 왕복 1세트)")]
        [SerializeField] private int sweepRounds = 1;
        [Tooltip("탄환 투사 속도")]
        [SerializeField] private float bulletSpeed = 4.0f;
        [Tooltip("1회 스윕당 발사할 탄환 개수 (기본 7발 권장: 30도 간격)")]
        [SerializeField] private int bulletCountPerSweep = 7;
        [SerializeField] private GameObject bulletPrefab;

        [Header("비대칭 임팩트 펀치 파라미터")]
        [SerializeField] private Vector2 landSquashScale = new Vector2(1.40f, 0.55f);
        [SerializeField] private float squashFastInDuration = 0.04f;
        [SerializeField] private float restoreElasticDuration = 0.25f;

        private Tweener _bossMoveTween;
        private Tweener _bossPunchTween;
        private bool _isTireBrokenDebuffed = false;
        private float _groundY;

        public void ApplyTireBrokenDebuff(bool debuffed)
        {
            _isTireBrokenDebuffed = debuffed;
            Debug.Log($"<color=yellow>[JunkWheelerBarragePattern] 타이어 파괴 디버프 적용 상태: {_isTireBrokenDebuffed} (도약 높이: {(debuffed ? launchHeight * 0.7f : launchHeight):F2}m)</color>");
        }

        protected override void OnPreExecute()
        {
            base.OnPreExecute();
            OwnerBoss?.FacePlayer();
        }

        /// <summary>
        /// 전조(1.5초) 동안 정지 화면 대신 바퀴 공회전(차지 루프) 모션 재생
        /// </summary>
        public override void OnTellStarted()
        {
            OwnerBoss?.Animation?.PlayChargeLoop();
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (OwnerBoss == null) return;

            // 도약 전 플레이어 방향 응시
            OwnerBoss.FacePlayer();

            // 1단계: 원래 지면 Y좌표 기록 및 중간 상단(Apex) 도약 좌표 산출
            _groundY = OwnerBoss.GroundY;
            OwnerBoss.SnapToGroundY();
            Vector3 apexPos = CalculateMidpointApex();

            Debug.Log($"<color=cyan>[JunkWheelerBarragePattern] 보스 본체 공중 도약 개시 ➔ {apexPos}</color>");

            // [도약 텐션]: 바닥 웅크림 (0.06s, Y 0.75) 후 도약 상승
            if (OwnerBoss.Animation != null)
            {
                await OwnerBoss.Animation.PlayImpactSquashAndStretchAsync(
                    OwnerBoss.transform, new Vector2(1.15f, 0.75f), 0.06f, 0.08f, token);
            }

            // 2단계: 공중 회전 모션 재생 및 본체 직접 도약
            if (OwnerBoss.Animation != null)
            {
                OwnerBoss.Animation.PlayRollBarrage();
            }

            _bossMoveTween = OwnerBoss.transform.DOMove(apexPos, launchDuration).SetEase(Ease.OutQuad);
            await _bossMoveTween.ToUniTask(cancellationToken: token);

            // 3단계: 바이킹 진자 순차 탄막 발사 (보스 본체 중심에서 투사)
            int maxIndex = Mathf.Max(1, bulletCountPerSweep - 1);
            float angleStep = 180f / maxIndex;

            for (int round = 0; round < sweepRounds; round++)
            {
                token.ThrowIfCancellationRequested();

                // [순방향 스윕]: 0번부터 maxIndex까지 순차 발사 (180도 -> 360도)
                Debug.Log($"<color=cyan>[JunkWheelerBarragePattern] 바이킹 탄막 정방향 스윕 (0번 ➔ {maxIndex}번)</color>");
                for (int i = 0; i <= maxIndex; i++)
                {
                    token.ThrowIfCancellationRequested();

                    Vector3 fireOrigin = OwnerBoss.transform.position;
                    float angle = 180f + (i * angleStep);
                    FireSingleBullet(fireOrigin, angle);

                    // 본체 반동 틸트
                    float tiltZ = Mathf.Sin((i / (float)maxIndex) * Mathf.PI) * 15f;
                    OwnerBoss.transform.rotation = Quaternion.Euler(0, 0, tiltZ);

                    await UniTask.Delay(TimeSpan.FromSeconds(bulletInterval), cancellationToken: token);
                }

                // [정점 대기 (바이킹 반전)]
                await UniTask.Delay(TimeSpan.FromSeconds(turnaroundDelay), cancellationToken: token);

                // [역방향 스윕]: maxIndex부터 0번으로 거꾸로 발사
                Debug.Log($"<color=yellow>[JunkWheelerBarragePattern] 바이킹 탄막 역방향 스윕 ({maxIndex}번 ➔ 0번)</color>");
                for (int i = maxIndex; i >= 0; i--)
                {
                    token.ThrowIfCancellationRequested();

                    Vector3 fireOrigin = OwnerBoss.transform.position;
                    float angle = 180f + (i * angleStep);
                    FireSingleBullet(fireOrigin, angle);

                    float tiltZ = -Mathf.Sin((i / (float)maxIndex) * Mathf.PI) * 15f;
                    OwnerBoss.transform.rotation = Quaternion.Euler(0, 0, tiltZ);

                    await UniTask.Delay(TimeSpan.FromSeconds(bulletInterval), cancellationToken: token);
                }

                if (round < sweepRounds - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(0.15f), cancellationToken: token);
                }
            }

            // 본체 회전 복원
            OwnerBoss.transform.rotation = Quaternion.identity;

            // 4단계: 지면으로 착지 급강하 (X축은 체공 지점 유지, Y축은 원지면으로 복귀)
            Vector3 currentPos = OwnerBoss.transform.position;
            Vector3 landPos = new Vector3(currentPos.x, _groundY, currentPos.z);

            if (OwnerBoss.Animation != null)
            {
                OwnerBoss.Animation.PlaySlamFall();
            }

            Debug.Log($"<color=cyan>[JunkWheelerBarragePattern] 탄막 종료, 지면 착지 급강하 ➔ {landPos}</color>");
            _bossMoveTween = OwnerBoss.transform.DOMove(landPos, landDuration).SetEase(Ease.InQuad);
            await _bossMoveTween.ToUniTask(cancellationToken: token);

            // 5단계: 착지 충격 연출 및 Idle 복귀
            if (OwnerBoss.Animation != null)
            {
                OwnerBoss.Animation.PlaySlamLand();
                // 비대칭 펀치(Squash & Stretch) 적용: 0.04s 압착 -> 0.25s 복원
                await OwnerBoss.Animation.PlayImpactSquashAndStretchAsync(
                    OwnerBoss.transform, landSquashScale, squashFastInDuration, restoreElasticDuration, token);
            }

            // 착지 후 스케일 완전 복원 및 플레이어 응시 후 Idle 복귀
            if (OwnerBoss.Animation != null)
            {
                OwnerBoss.Animation.ResetScale();
                OwnerBoss.FacePlayer();
                OwnerBoss.Animation.PlayIdle();
            }
        }

        private Vector3 CalculateMidpointApex()
        {
            Transform player = GameObject.FindWithTag("Player")?.transform;
            Vector3 bossPos = OwnerBoss.transform.position;

            float midX;
            float baseY;

            if (player != null)
            {
                midX = (bossPos.x + player.position.x) * 0.5f;
                baseY = Mathf.Max(bossPos.y, player.position.y);
            }
            else
            {
                float forwardDir = OwnerBoss.transform.localScale.x >= 0 ? -1f : 1f;
                midX = bossPos.x + (forwardDir * 2.0f);
                baseY = bossPos.y;
            }

            if (OwnerBoss != null)
            {
                midX = OwnerBoss.ClampToStageWallX(midX, 0.8f);
            }

            float effectiveLaunchHeight = _isTireBrokenDebuffed ? launchHeight * 0.7f : launchHeight;
            float apexY = baseY + effectiveLaunchHeight;

            return new Vector3(midX, apexY, bossPos.z);
        }

        private void FireSingleBullet(Vector3 originPos, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f).normalized;

            GameObject bullet;
            if (bulletPrefab != null)
            {
                bullet = Instantiate(bulletPrefab, originPos, Quaternion.identity);
            }
            else
            {
                bullet = new GameObject("BossFallbackBullet");
                bullet.transform.position = originPos;
                bullet.transform.localScale = Vector3.one * 0.35f;

                var sr = bullet.AddComponent<SpriteRenderer>();
                sr.sprite = GetOrCreateFallbackBulletSprite();
                sr.color = new Color(1.0f, 0.45f, 0.05f, 1f);

                var col2d = bullet.AddComponent<CircleCollider2D>();
                col2d.isTrigger = true;
                col2d.radius = 0.5f;

                var fallbackRb2d = bullet.AddComponent<Rigidbody2D>();
                fallbackRb2d.gravityScale = 0f;
            }

            // 탄환 히트 트리거 부착
            var trigger = bullet.AddComponent<BossBulletHitTrigger>();
            trigger.Initialize(this);

            float rotAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0f, 0f, rotAngle);

            if (bullet.TryGetComponent<Rigidbody2D>(out var rb2d))
            {
                rb2d.linearVelocity = dir * bulletSpeed;
            }

            Destroy(bullet, 4.0f);
            Debug.DrawRay(originPos, dir * 2.0f, Color.yellow, 0.5f);
        }

        private static Sprite _fallbackSprite;
        private static Sprite GetOrCreateFallbackBulletSprite()
        {
            if (_fallbackSprite != null) return _fallbackSprite;

            int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color c = Color.white;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size * 0.5f, size * 0.5f));
                    if (dist <= size * 0.45f)
                    {
                        tex.SetPixel(x, y, c);
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

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            if (_bossMoveTween != null && _bossMoveTween.IsActive()) _bossMoveTween.Kill();
            if (_bossPunchTween != null && _bossPunchTween.IsActive()) _bossPunchTween.Kill();

            if (OwnerBoss != null)
            {
                OwnerBoss.transform.rotation = Quaternion.identity;
                OwnerBoss.SnapToGroundY();
                OwnerBoss.Animation?.ResetScale();
            }
        }
    }

    /// <summary>
    /// 탄환 트리거 충돌 시 플레이어 피격 처리 헬퍼 컴포넌트
    /// </summary>
    public class BossBulletHitTrigger : MonoBehaviour
    {
        private JunkWheelerBarragePatternGimmick _ownerPattern;
        private bool _hasHit = false;

        public void Initialize(JunkWheelerBarragePatternGimmick pattern)
        {
            _ownerPattern = pattern;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (_hasHit || _ownerPattern == null) return;

            if (collision.CompareTag("Player"))
            {
                _hasHit = true;
                _ownerPattern.ApplyDamageToPlayer(collision.gameObject);
                Destroy(gameObject);
            }
            else if (collision.CompareTag("Wall") || collision.CompareTag("Ground"))
            {
                _hasHit = true;
                Destroy(gameObject);
            }
        }
    }
}
