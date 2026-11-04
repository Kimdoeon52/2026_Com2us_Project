using System;
using UnityEngine;
using DG.Tweening;
using RealSteel.Boss.Visual;

namespace RealSteel.Boss.JunkWheeler
{
    /// <summary>
    /// [정크 휠러 드리블 지면 충격파 개체 (JunkWheelerShockwaveHazard)]
    /// 기획서 §6.18 (드리블 충격파) 및 X축 점진적 확장 시스템:
    /// 1. 정크 휠러가 제자리에서 튀어 착지할 때마다 좌/우 지면을 타고 퍼져나가는 충격파 개체.
    /// 2. X축 중심의 점진적 범위 확장:
    ///    - 회차(1~4회)에 따라 X축 전파 사거리(3.0m ➔ 6.0m ➔ 9.0m ➔ 13.5m)뿐만 아니라,
    ///      충격파 자체의 가로 폭(HitWidth: 0.75m ➔ 1.3m ➔ 2.0m ➔ 3.0m)과 비주얼 X축 스케일(ScaleX: 1.0x ➔ 1.6x ➔ 2.3x ➔ 3.2x)이 대폭 확장됨.
    ///    - 또한 충격파가 지면을 타고 바깥으로 이동할수록 실시간으로 X축 가로 폭이 추가 팽창(Dynamic X-Expansion)하여 파동이 넓게 퍼짐.
    /// 3. 지면(Ground) 판정 규칙:
    ///    - 플레이어가 지면에 서 있으면(IsGrounded == true) 피격되어 LeftLeg(다리) 내구도 및 코어 체력 피해 적용.
    ///    - 플레이어가 점프 중이거나(Airborne / !IsGrounded) Space 무적 회피 중이면 충격파를 안전하게 뛰어넘어 회피 성공!
    /// 4. 충격파 개체는 물리 레이어 오염 없이 스크립트 기반 고속 정밀 X축 거리 검사를 수행하며,
    ///    최대 도달 거리에 도달하면 부드럽게 소멸(Destroy)됨.
    /// </summary>
    public class JunkWheelerShockwaveHazard : MonoBehaviour
    {
        [Header("판정 파라미터 (런타임 정밀 연산)")]
        [Tooltip("현재 실시간 충격파 판정 전폭 (X축 접촉 허용 거리)")]
        [SerializeField] private float currentHitWidth = 0.25f;
        [Tooltip("점프 회피 인정 최소 높이 오프셋")]
        [SerializeField] private float jumpAvoidHeightThreshold = 0.12f;
        [Tooltip("이동 거리에 따른 X축 실시간 가로 팽창 배율")]
        [SerializeField] private float dynamicXExpandRatio = 0.20f;

        private float _directionX = 1f;
        private float _speed = 7f;
        private float _maxDistance = 5f;
        private float _baseHitWidth = 0.25f;
        private float _baseScaleX = 1.0f;
        private float _baseScaleY = 1.0f;
        private float _baseScaleZ = 1.0f;

        private int _coreDamage = 25;
        private int _legDamage = 20;
        private float _groundY = 0f;
        private int _roundIndex = 1;
        private JunkWheelerDribbleShockwavePatternGimmick _owner;
        private GameObject _hitVfxPrefab;

        private float _traveledDistance = 0f;
        private bool _hasProcessedPlayer = false;
        private bool _isDisintegrating = false;
        private Transform _playerTransform;
        private GameObject _visualInstance;

        public float TraveledDistance => _traveledDistance;
        public float MaxDistance => _maxDistance;
        public int RoundIndex => _roundIndex;
        public float CurrentHitWidth => currentHitWidth;

        /// <summary>
        /// 스폰 즉시 충격파 물리/피해/X축 확장 속성 초기화
        /// </summary>
        public void Initialize(
            float directionX,
            float speed,
            float maxDistance,
            float hitWidth,
            float scaleX,
            int coreDamage,
            int legDamage,
            float groundY,
            int roundIndex,
            JunkWheelerDribbleShockwavePatternGimmick owner,
            GameObject visualPrefab = null,
            GameObject hitVfxPrefab = null)
        {
            _directionX = Mathf.Sign(directionX);
            _speed = speed;
            _maxDistance = maxDistance;
            _baseHitWidth = hitWidth;
            _baseScaleX = scaleX;
            currentHitWidth = _baseHitWidth;

            _coreDamage = coreDamage;
            _legDamage = legDamage;
            _groundY = groundY;
            _roundIndex = roundIndex;
            _owner = owner;
            _hitVfxPrefab = hitVfxPrefab;

            _traveledDistance = 0f;
            _hasProcessedPlayer = false;
            _isDisintegrating = false;

            // 순수 비주얼/스크립트 판정 개체로 격리 (물리 푸시/충돌 간섭 방지)
            BossVfxHelper.ConfigureVisualOnly(gameObject);

            // 비주얼 생성 및 X축 가로 스케일 적용
            SetupVisual(visualPrefab);

            // 초기 높이를 지면에 밀착
            Vector3 pos = transform.position;
            pos.y = _groundY;
            transform.position = pos;
        }

        private void SetupVisual(GameObject visualPrefab)
        {
            if (visualPrefab != null)
            {
                _visualInstance = Instantiate(visualPrefab, transform);
                _visualInstance.transform.localPosition = Vector3.zero;

                _baseScaleY = 1.0f;
                _baseScaleZ = 1.0f;
                _visualInstance.transform.localScale = new Vector3(
                    _baseScaleX,
                    _baseScaleY,
                    _baseScaleZ
                );

                BossVfxHelper.ConfigureVisualOnly(_visualInstance);

                // 지면보다 앞쪽으로 나오도록 Sorting Order 보정 및 좌측(-1) 진행 시 빌보드 파티클/스프라이트 정식 좌우 반전
                var renderers = _visualInstance.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    r.sortingOrder = Mathf.Max(r.sortingOrder, 5);

                    if (_directionX < 0f)
                    {
                        if (r is ParticleSystemRenderer psr)
                        {
                            psr.flip = new Vector3(1f, psr.flip.y, psr.flip.z);
                        }
                        else if (r is SpriteRenderer sr)
                        {
                            sr.flipX = !sr.flipX;
                        }
                    }
                }
            }
            else
            {
                // 프리팹 미할당 시 내장 기본 스프라이트로 폴백 연출
                CreateProceduralVisual();
            }
        }

        private void CreateProceduralVisual()
        {
            _visualInstance = new GameObject("ProceduralShockwave");
            _visualInstance.transform.SetParent(transform, false);
            _visualInstance.transform.localPosition = new Vector3(0f, 0.25f, 0f);

            var sr = _visualInstance.AddComponent<SpriteRenderer>();
            sr.color = new Color(1.0f, 0.45f, 0.1f, 0.85f);
            sr.sortingOrder = 5;

            Texture2D tex = Texture2D.whiteTexture;
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

            _baseScaleY = 0.55f;
            _baseScaleZ = 1f;
            _visualInstance.transform.localScale = new Vector3(_baseScaleX * 0.8f * (_directionX < 0 ? -1f : 1f), _baseScaleY, _baseScaleZ);
        }

        private void Update()
        {
            if (_isDisintegrating) return;

            float dt = Time.deltaTime;
            float moveStep = _speed * dt;
            _traveledDistance += moveStep;

            Vector3 pos = transform.position;
            pos.x += _directionX * moveStep;
            pos.y = _groundY;
            transform.position = pos;

            // ★ [X축 실시간 동적 팽창]: 전파 거리에 비례하여 X축 판정 폭과 가로 비주얼이 실시간으로 점점 넓어짐!
            float progress = Mathf.Clamp01(_traveledDistance / _maxDistance);
            currentHitWidth = _baseHitWidth * (1f + progress * dynamicXExpandRatio);

            if (_visualInstance != null)
            {
                float dynamicScaleX = _baseScaleX * (1f + progress * dynamicXExpandRatio);
                _visualInstance.transform.localScale = new Vector3(
                    dynamicScaleX,
                    _baseScaleY,
                    _baseScaleZ
                );
            }

            // 플레이어 지면 피격 및 점프 회피 판정 검사 (확장된 X축 폭 적용)
            if (!_hasProcessedPlayer)
            {
                CheckPlayerInteraction();
            }

            // 최대 사거리 도달 시 소멸
            if (_traveledDistance >= _maxDistance)
            {
                Disintegrate();
            }
        }

        private void CheckPlayerInteraction()
        {
            if (_playerTransform == null)
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    _playerTransform = playerObj.transform;
                }
            }

            if (_playerTransform == null) return;

            float playerX = _playerTransform.position.x;
            float currentX = transform.position.x;
            float dx = Mathf.Abs(playerX - currentX);

            // 실시간으로 점점 넓어지는 currentHitWidth로 판정!
            if (dx <= currentHitWidth)
            {
                _hasProcessedPlayer = true;

                // 1. Space 무적 회피 검사
                bool isInvincible = false;
                var executor = _playerTransform.GetComponent<ActionExecutor>() ?? _playerTransform.GetComponentInParent<ActionExecutor>();
                if (executor != null && executor.State != null && executor.State.CurrentAction != null && executor.State.CurrentAction.IsInvincibleDuringActive)
                {
                    isInvincible = true;
                }

                // 2. 기획서 핵심: 점프(공중) 회피 검사 (충격파는 지면 판정이므로 점프로 넘기)
                bool isAirborne = false;
                var mover = _playerTransform.GetComponent<RobotMover>() ?? _playerTransform.GetComponentInParent<RobotMover>();
                if (mover != null && (!mover.IsGrounded || mover.VerticalVelocity > 0.05f))
                {
                    isAirborne = true;
                }
                else if (_playerTransform.position.y > _groundY + jumpAvoidHeightThreshold)
                {
                    isAirborne = true;
                }

                if (!isAirborne && executor != null && executor.State != null && executor.State.CurrentAction != null)
                {
                    string actName = executor.State.CurrentAction.name;
                    if (actName.IndexOf("Jump", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isAirborne = true;
                    }
                }

                if (isInvincible)
                {
                    Debug.Log($"<color=cyan>[드리블 충격파] 플레이어가 Space 무적 대시로 {_roundIndex}회차 충격파(X폭: {currentHitWidth * 2f:F2}m)를 회피했습니다!</color>");
                }
                else if (isAirborne)
                {
                    Debug.Log($"<color=green><b>[드리블 충격파] ★ 점프 회피 성공! 플레이어가 점프로 {_roundIndex}회차 광폭 충격파(X폭: {currentHitWidth * 2f:F2}m, 사거리: {_maxDistance:F1}m)를 뛰어넘었습니다!</b></color>");
                }
                else
                {
                    // 지면 피격! 플레이어 다리 부위(LeftLeg) 및 코어 타격
                    ApplyHitToPlayer(_playerTransform.gameObject);
                }
            }
        }

        private void ApplyHitToPlayer(GameObject playerObj)
        {
            // 동일 회차(roundIndex)에서 좌/우 충격파에 연달아 2번 피격되는 현상 차단
            if (_owner != null && !_owner.TryRegisterRoundHit(_roundIndex))
            {
                return;
            }

            var hub = CombatDataHub.Instance;
            string attackerId = (_owner != null && _owner.OwnerBoss != null && _owner.OwnerBoss.Snapshot != null)
                ? _owner.OwnerBoss.Snapshot.bossID
                : "Boss";

            int finalCoreDmg = _coreDamage;
            int finalLegDmg = _legDamage;

            // 난이도 공격력 배율 반영
            if (_owner != null && _owner.OwnerBoss != null && _owner.OwnerBoss.Snapshot != null)
            {
                float mult = _owner.OwnerBoss.Snapshot.attackMultiplier;
                finalCoreDmg = Mathf.RoundToInt(finalCoreDmg * mult);
                finalLegDmg = Mathf.RoundToInt(finalLegDmg * mult);
            }

            if (hub != null)
            {
                hub.ProcessHit(attackerId, "Player", finalCoreDmg, BodyPart.LeftLeg, finalLegDmg);
            }

            Debug.Log($"<color=red><b>[드리블 충격파] 💥 지면 충격파 적중! 플레이어 LeftLeg 다리 부위 타격! (X폭: {currentHitWidth * 2f:F2}m, 코어 피해: {finalCoreDmg}, 다리 내구도 피해: {finalLegDmg})</b></color>");

            // 피격 지점 스파크/충격 VFX 스폰
            if (_hitVfxPrefab != null)
            {
                Vector3 hitPos = new Vector3(playerObj.transform.position.x, _groundY, 0f);
                BossVfxHelper.SpawnVfx(_hitVfxPrefab, hitPos, Quaternion.identity, Vector3.one, 1.2f);
            }
        }

        private void Disintegrate()
        {
            if (_isDisintegrating) return;
            _isDisintegrating = true;

            // 트윈 스케일 축소 후 파괴
            if (_visualInstance != null)
            {
                _visualInstance.transform.DOScaleY(0f, 0.12f).SetEase(Ease.InQuad).OnComplete(() =>
                {
                    Destroy(gameObject);
                });
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void ForceDestroy()
        {
            _isDisintegrating = true;
            Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 현재 X축 판정 전폭 시각화
            Gizmos.color = new Color(1f, 0.45f, 0f, 0.75f);
            Vector3 center = transform.position + Vector3.up * (jumpAvoidHeightThreshold * 0.5f);
            Gizmos.DrawWireCube(center, new Vector3(currentHitWidth * 2f, jumpAvoidHeightThreshold, 0.2f));

            Gizmos.color = Color.yellow;
            Vector3 endPos = transform.position + Vector3.right * (_directionX * (_maxDistance - _traveledDistance));
            Gizmos.DrawLine(transform.position, endPos);
        }
#endif
    }
}
