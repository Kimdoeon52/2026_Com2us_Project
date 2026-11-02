using System;
using UnityEngine;
using RealSteel.Boss.Visual;

namespace RealSteel.Boss.JunkWheeler
{
    /// <summary>
    /// [헬 난이도 전용 패시브: 배기 잔화 화염 지대 (JunkWheelerFireTrailHazard)]
    /// 기획서 §6.16 및 §6.10.4:
    /// 1. 정크 휠러가 헬(Hell) 난이도에서 돌진(Charge) 시 경로 뒤로 2초간 남기는 화염 바닥 장판.
    /// 2. 지속 시간(2.0초) 동안 접촉한 플레이어에게 0.4초마다 지속 도트 화염 피해를 입힘.
    /// 3. 수명 2초 만료 시 페이드아웃 및 자동 파괴(소멸).
    /// </summary>
    public class JunkWheelerFireTrailHazard : MonoBehaviour
    {
        [Header("화염 장판 파라미터")]
        [SerializeField] private float lifetime = 2.0f;
        [SerializeField] private float tickInterval = 0.4f;
        [SerializeField] private int tickDamage = 15;
        [SerializeField] private float damageRadius = 0.45f;

        [Header("시각 효과 (선택적)")]
        [SerializeField] private ParticleSystem fireParticle;

        private float _spawnTime;
        private float _lastTickTime;
        private Transform _cachedPlayerTransform;

        private void Awake()
        {
            _spawnTime = Time.time;
            _lastTickTime = Time.time;

            // 시각적 효과를 위해 스프라이트 렌더러가 있으면 주황/빨간 불꽃색 설정
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(1.0f, 0.45f, 0.1f, 0.85f);
            }

            // VFX Helper를 통한 충돌체 무시 레이어 적용 (피격 판정 오염 방지)
            BossVfxHelper.ConfigureVisualOnly(gameObject);

            Destroy(gameObject, lifetime);
        }

        private void Start()
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _cachedPlayerTransform = playerObj.transform;
            }
        }

        private void Update()
        {
            if (Time.time - _lastTickTime < tickInterval) return;

            if (_cachedPlayerTransform == null)
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null) _cachedPlayerTransform = playerObj.transform;
            }

            if (_cachedPlayerTransform != null)
            {
                float dist = Vector2.Distance(transform.position, _cachedPlayerTransform.position);
                if (dist <= damageRadius)
                {
                    _lastTickTime = Time.time;
                    ApplyFireDamageToPlayer();
                }
            }
        }

        private void ApplyFireDamageToPlayer()
        {
            var hub = CombatDataHub.Instance;
            if (hub != null && hub.PlayerSnapshot != null && hub.PlayerSnapshot.IsAlive)
            {
                // Space 무적 회피 중인지 검사
                var executor = _cachedPlayerTransform.GetComponent<ActionExecutor>() ?? _cachedPlayerTransform.GetComponentInParent<ActionExecutor>();
                if (executor != null && executor.State != null && executor.State.CurrentAction != null && executor.State.CurrentAction.IsInvincibleDuringActive)
                {
                    return; // 무적 회피 중
                }

                hub.ApplyCoreDamage("Player", tickDamage);
                Debug.Log($"<color=orange>[배기 잔화] 플레이어가 화염 장판에 접촉하여 도트 피해 {tickDamage}를 입었습니다!</color>");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1.0f, 0.3f, 0.0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, damageRadius);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1.0f, 0.45f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, damageRadius);
        }
    }
}

