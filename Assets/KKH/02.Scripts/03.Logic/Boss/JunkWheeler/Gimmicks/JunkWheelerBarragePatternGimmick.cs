using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using RealSteel.Boss.Core;

namespace RealSteel.Boss.JunkWheeler
{
    /// <summary>
    /// [정크 휠러 바이킹 순차 반원 탄막 기믹 (JunkWheelerBarragePatternGimmick)]
    /// 기획서 §6.18 및 §3.2 (4):
    /// 1. 보스 본체는 지상 유지, 등 뒤의 타이어를 [보스와 플레이어의 중간 상단]으로 사출 (기본 높이 1.7m).
    /// 2. 타이어 부위 파괴 시 사출 높이 30% 감축 (1.7m ➔ 1.19m).
    /// 3. 사출된 타이어에서 180도 지상 반원 호를 따라 15도 간격(0~12번)으로 순차적 탄환 투사:
    ///    - 순방향: 0번(180°) ➔ 12번(360°) 차례대로 발사 (틱-틱-틱...)
    ///    - 정점 딜레이: 바이킹 진자 최고점 도달 후 짧은 반전 대기 (0.2초)
    ///    - 역방향: 12번(360°) ➔ 0번(180°) 역순으로 차례대로 발사
    /// 4. 발사 시퀀스 완료 후 타이어가 보스 본체로 고속 회수.
    /// </summary>
    public class JunkWheelerBarragePatternGimmick : BossPatternGimmickBase
    {
        [Header("사출 및 탄막 파라미터")]
        [SerializeField] private float launchHeight = 1.7f;
        [SerializeField] private float launchDuration = 0.35f;
        [SerializeField] private float bulletInterval = 0.05f;
        [SerializeField] private float turnaroundDelay = 0.2f;
        [SerializeField] private int sweepRounds = 1;
        [SerializeField] private float bulletSpeed = 6.0f;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private GameObject tirePrefab;

        private GameObject _activeTireObject;
        private Tweener _tireMoveTween;
        private Tweener _tireRotateTween;
        private Tweener _bossTween;
        private bool _isTireBrokenDebuffed = false;

        public void ApplyTireBrokenDebuff(bool debuffed)
        {
            _isTireBrokenDebuffed = debuffed;
            Debug.Log($"<color=yellow>[JunkWheelerBarragePattern] 타이어 파괴 디버프 적용 상태: {_isTireBrokenDebuffed} (사출 높이: {(debuffed ? launchHeight * 0.7f : launchHeight):F2}m)</color>");
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (OwnerBoss == null) return;

            // 1단계: 보스와 플레이어의 중간 지점 상단(Midpoint Apex) 좌표 산출
            Vector3 apexPos = CalculateMidpointApex();

            // 2단계: 등 뒤의 타이어를 중간 상단으로 고속 사출
            _activeTireObject = SpawnOrGetTireObject(OwnerBoss.transform.position);

            _tireMoveTween = _activeTireObject.transform.DOMove(apexPos, launchDuration).SetEase(Ease.OutBack);
            _tireRotateTween = _activeTireObject.transform.DORotate(new Vector3(0, 0, -360), launchDuration, RotateMode.FastBeyond360);
            _bossTween = OwnerBoss.transform.DOPunchScale(new Vector3(0.12f, -0.12f, 0f), 0.25f);

            await _tireMoveTween.ToUniTask(cancellationToken: token);

            // 3단계: 바이킹 진자 순차 탄막 (15도 간격, 0번~12번 순차 발사 후 12번~0번 역순 복귀)
            const int maxIndex = 12;
            Vector3 fireOrigin = _activeTireObject.transform.position;

            for (int round = 0; round < sweepRounds; round++)
            {
                token.ThrowIfCancellationRequested();

                // [순방향 스윕]: 0번부터 12번까지 차례대로 발사 (180도 -> 360도)
                Debug.Log($"<color=cyan>[JunkWheelerBarragePattern] 바이킹 탄막 정방향 스윕 시작 (0번 ➔ 12번)</color>");
                for (int i = 0; i <= maxIndex; i++)
                {
                    token.ThrowIfCancellationRequested();

                    float angle = 180f + (i * 15f);
                    FireSingleBullet(fireOrigin, angle);

                    // 타이어 틸트 연출
                    float tiltZ = Mathf.Sin((i / (float)maxIndex) * Mathf.PI) * 20f;
                    _activeTireObject.transform.rotation = Quaternion.Euler(0, 0, tiltZ);

                    await UniTask.Delay(TimeSpan.FromSeconds(bulletInterval), cancellationToken: token);
                }

                // [정점 대기 (바이킹 반전)]
                await UniTask.Delay(TimeSpan.FromSeconds(turnaroundDelay), cancellationToken: token);

                // [역방향 스윕]: 12번부터 0번으로 거꾸로 발사
                Debug.Log($"<color=yellow>[JunkWheelerBarragePattern] 바이킹 탄막 역방향 스윕 시작 (12번 ➔ 0번)</color>");
                for (int i = maxIndex; i >= 0; i--)
                {
                    token.ThrowIfCancellationRequested();

                    float angle = 180f + (i * 15f);
                    FireSingleBullet(fireOrigin, angle);

                    float tiltZ = -Mathf.Sin((i / (float)maxIndex) * Mathf.PI) * 20f;
                    _activeTireObject.transform.rotation = Quaternion.Euler(0, 0, tiltZ);

                    await UniTask.Delay(TimeSpan.FromSeconds(bulletInterval), cancellationToken: token);
                }

                if (round < sweepRounds - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(0.15f), cancellationToken: token);
                }
            }

            // 4단계: 타이어 회수
            if (_activeTireObject != null)
            {
                _tireMoveTween = _activeTireObject.transform.DOMove(OwnerBoss.transform.position, 0.3f).SetEase(Ease.InQuad);
                _tireRotateTween = _activeTireObject.transform.DORotate(new Vector3(0, 0, 720), 0.3f, RotateMode.FastBeyond360);
                await _tireMoveTween.ToUniTask(cancellationToken: token);

                _bossTween = OwnerBoss.transform.DOPunchScale(new Vector3(0.2f, -0.2f, 0f), 0.2f);
                await _bossTween.ToUniTask(cancellationToken: token);

                Destroy(_activeTireObject);
                _activeTireObject = null;
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

            bool isTireBroken = _isTireBrokenDebuffed;
            var tireGimmick = OwnerBoss.GetGimmick<JunkWheelerTirePartGimmick>("TIRE_PART");
            if (tireGimmick != null && tireGimmick.IsPermanentlyBroken)
            {
                isTireBroken = true;
            }

            float finalHeight = isTireBroken ? launchHeight * 0.7f : launchHeight;
            return new Vector3(midX, baseY + finalHeight, bossPos.z);
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

        private GameObject SpawnOrGetTireObject(Vector3 spawnPos)
        {
            if (tirePrefab != null)
            {
                return Instantiate(tirePrefab, spawnPos, Quaternion.identity);
            }

            GameObject tire = new GameObject("BossFlyingTire");
            tire.transform.position = spawnPos;
            var sr = tire.AddComponent<SpriteRenderer>();

            var bossSr = OwnerBoss.GetComponentInChildren<SpriteRenderer>();
            if (bossSr != null && bossSr.sprite != null)
            {
                sr.sprite = bossSr.sprite;
                sr.color = new Color(0.25f, 0.25f, 0.25f, 1f);
                tire.transform.localScale = OwnerBoss.transform.localScale * 0.65f;
            }
            else
            {
                tire.transform.localScale = Vector3.one * 0.7f;
            }

            return tire;
        }

        private static Sprite _fallbackSprite;
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

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            if (_tireMoveTween != null && _tireMoveTween.IsActive()) _tireMoveTween.Kill();
            if (_tireRotateTween != null && _tireRotateTween.IsActive()) _tireRotateTween.Kill();
            if (_bossTween != null && _bossTween.IsActive()) _bossTween.Kill();

            if (_activeTireObject != null)
            {
                Destroy(_activeTireObject);
                _activeTireObject = null;
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
