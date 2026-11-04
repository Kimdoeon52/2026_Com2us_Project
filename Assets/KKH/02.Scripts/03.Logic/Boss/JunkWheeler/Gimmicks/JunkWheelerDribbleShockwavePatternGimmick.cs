using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using RealSteel.Boss.Core;
using RealSteel.Boss.Visual;

namespace RealSteel.Boss.JunkWheeler
{
    /// <summary>
    /// [정크 휠러 드리블 충격파 기믹 (JunkWheelerDribbleShockwavePatternGimmick)]
    /// 기획서 §6.18 (드리블 충격파) 및 X축 점진적 확장 시스템:
    /// 1. 동작:
    ///    - 제자리에서 짧고 빠른 4회 리듬 도약(Dribble: Y축 높이 0.5m 내외 고정).
    ///    - 착지할 때마다 좌/우로 지면을 타고 퍼지는 충격파(JunkWheelerShockwaveHazard) 방출.
    /// 2. X축 중심의 점진적 범위 확장:
    ///    - 1회차: X축 사거리 3.0m, 가로 판정폭 0.75m, 비주얼 X스케일 1.0x (소형)
    ///    - 2회차: X축 사거리 6.0m, 가로 판정폭 1.30m, 비주얼 X스케일 1.6x (중형)
    ///    - 3회차: X축 사거리 9.0m, 가로 판정폭 2.00m, 비주얼 X스케일 2.3x (대형)
    ///    - 4회차: X축 사거리 13.5m, 가로 판정폭 3.00m, 비주얼 X스케일 3.2x (특대형 / 전장 전체 횡단)
    ///    - 파동이 이동하면서 실시간으로 X축 가로 폭이 추가로 팽창(Dynamic X-Expansion)하여 광범위 지면 커버.
    /// 3. 예고 (Tell):
    ///    - 보스 바로 아래 바닥이 X축으로 찌이익 갈라지며(가로 폭 0.2m ➔ 3.0m) 0.6초간 적색 경고 점멸.
    /// 4. 회피:
    ///    - 충격파는 지면(Ground) 판정이므로 플레이어의 점프(Airborne / !IsGrounded) 또는 Space 무적 대시로 회피.
    ///    - 지면에 서 있으면 LeftLeg(다리) 내구도 피해 및 코어 대미지 적용.
    /// 5. 딜 타이밍:
    ///    - 4번째 착지 후 0.8초간 지면 정지 확정 딜타임(무방비 상태).
    /// </summary>
    public class JunkWheelerDribbleShockwavePatternGimmick : BossPatternGimmickBase
    {
        public const string PatternDribbleShockwaveID = "PATTERN_DRIBBLE_SHOCKWAVE";
        public override string GimmickID => PatternDribbleShockwaveID;
        public override string GimmickName => "드리블 충격파";

        [System.Serializable]
        public struct DribbleRoundConfig
        {
            [Tooltip("제자리 도약 높이 (m) - 기획서: 제자리에서 짧고 빠른 리듬 유지")]
            public float jumpHeight;
            [Tooltip("상승 체공 시간 (초)")]
            public float jumpDuration;
            [Tooltip("하강 급강하 시간 (초)")]
            public float fallDuration;

            [Header("★ X축 점진적 확장 파라미터")]
            [Tooltip("충격파 X축 최대 전파 사거리 (m)")]
            public float shockwaveMaxDistance;
            [Tooltip("충격파 X축 판정 전폭 (m) - 회차별 가로 두께 확장")]
            public float shockwaveHitWidth;
            [Tooltip("충격파 비주얼 X축 가로 스케일 배율")]
            public float shockwaveScaleX;

            [Header("속도 및 피해량")]
            [Tooltip("충격파 전파 속도 (m/s)")]
            public float shockwaveSpeed;
            [Tooltip("적중 시 코어 피해량")]
            public int coreDamage;
            [Tooltip("적중 시 LeftLeg(다리) 내구도 피해량")]
            public int legDamage;
            [Tooltip("착지 순간 찌그러짐 비율 (Squash)")]
            public Vector2 landSquashScale;
        }

        [Header("전조(Tell) 바닥 균열 파라미터 (기획서: 0.6초)")]
        [SerializeField] private float tellCrackDuration = 0.6f;
        [SerializeField] private Color crackWarningColor = new Color(1.0f, 0.28f, 0.08f, 0.95f);
        [SerializeField] private float crackWidth = 1.2f;

        [Header("드리블 4회 라운드 설정 (X축 점진적 범위 확장 - 전 회차 반경 50% 축소)")]
        [SerializeField]
        private DribbleRoundConfig[] roundConfigs = new DribbleRoundConfig[4]
        {
            // 1회차: 소형 (X사거리 1.25m, X판정 반폭 0.15m, X스케일 0.8x)
            new DribbleRoundConfig
            {
                jumpHeight = 0.45f,
                jumpDuration = 0.16f,
                fallDuration = 0.12f,
                shockwaveMaxDistance = 1.25f,
                shockwaveHitWidth = 0.15f,
                shockwaveScaleX = 0.8f,
                shockwaveSpeed = 6.5f,
                coreDamage = 20,
                legDamage = 15,
                landSquashScale = new Vector2(1.20f, 0.75f)
            },
            // 2회차: 중형 (X사거리 2.25m, X판정 반폭 0.18m, X스케일 1.1x)
            new DribbleRoundConfig
            {
                jumpHeight = 0.48f,
                jumpDuration = 0.17f,
                fallDuration = 0.12f,
                shockwaveMaxDistance = 2.25f,
                shockwaveHitWidth = 0.18f,
                shockwaveScaleX = 1.1f,
                shockwaveSpeed = 7.0f,
                coreDamage = 25,
                legDamage = 20,
                landSquashScale = new Vector2(1.28f, 0.70f)
            },
            // 3회차: 대형 (X사거리 3.25m, X판정 반폭 0.22m, X스케일 1.4x)
            new DribbleRoundConfig
            {
                jumpHeight = 0.50f,
                jumpDuration = 0.18f,
                fallDuration = 0.13f,
                shockwaveMaxDistance = 3.25f,
                shockwaveHitWidth = 0.22f,
                shockwaveScaleX = 1.4f,
                shockwaveSpeed = 8.0f,
                coreDamage = 30,
                legDamage = 25,
                landSquashScale = new Vector2(1.35f, 0.65f)
            },
            // 4회차: 특대형 (X사거리 5.25m, X판정 반폭 0.26m, X스케일 1.8x)
            new DribbleRoundConfig
            {
                jumpHeight = 0.55f,
                jumpDuration = 0.20f,
                fallDuration = 0.14f,
                shockwaveMaxDistance = 5.25f,
                shockwaveHitWidth = 0.26f,
                shockwaveScaleX = 1.8f,
                shockwaveSpeed = 9.5f,
                coreDamage = 40,
                legDamage = 35,
                landSquashScale = new Vector2(1.48f, 0.52f)
            }
        };

        [Header("리듬 타이밍 및 확정 딜타임")]
        [Tooltip("드리블 튀기기 사이 짧은 리듬 간격 (초)")]
        [SerializeField] private float pauseBetweenDribbles = 0.10f;
        [Tooltip("4번째 착지 후 확정 딜타임 (기획서: 0.8초)")]
        [SerializeField] private float postLandVulnerableDelay = 0.8f;

        [Header("시각 효과(VFX) 프리팹 (인스펙터 할당 가능)")]
        [Tooltip("지면 전파 충격파 프리팹 (예: FX_Flipbook_GroundShock)")]
        [SerializeField] private GameObject shockwaveVisualPrefab;
        [Tooltip("착지 순간 중앙 임팩트 VFX (예: FX_Flipbook_Blast_Ground)")]
        [SerializeField] private GameObject dribbleLandVfxPrefab;
        [Tooltip("도약 순간 바닥 먼지 연출")]
        [SerializeField] private GameObject jumpDustVfxPrefab;
        [Tooltip("전조 단계 바닥 균열 이펙트 (선택 사항)")]
        [SerializeField] private GameObject tellCrackVfxPrefab;
        [Tooltip("플레이어 피격 순간 스파크/충격 VFX")]
        [SerializeField] private GameObject hitVfxPrefab;

        private float _groundY;
        private Tweener _currentTween;
        private GameObject _activeCrackIndicator;
        private readonly List<JunkWheelerShockwaveHazard> _activeHazards = new List<JunkWheelerShockwaveHazard>();
        private readonly HashSet<int> _hitRounds = new HashSet<int>();

        private void Awake()
        {
            EnsurePatternDataConfigured();
        }

        private void EnsurePatternDataConfigured()
        {
            if (patternData == null)
            {
                patternData = ScriptableObject.CreateInstance<BossPatternData>();
                patternData.patternID = PatternDribbleShockwaveID;
                patternData.patternName = GimmickName;
                patternData.description = "제자리에서 4회 드리블 도약하며 X축으로 점진적으로 사거리와 가로 폭이 확장(3m->6m->9m->13.5m)되는 좌/우 지면 충격파를 방출합니다.";
                patternData.tellDuration = tellCrackDuration;
                patternData.postDelay = postLandVulnerableDelay;
                patternData.hasDpsCheck = false;
                patternData.targetPlayerPart = BodyPart.LeftLeg;
                patternData.partDurabilityDamage = 30;
                patternData.baseDamage = 40;
            }
            else
            {
                patternData.tellDuration = tellCrackDuration;
                patternData.postDelay = postLandVulnerableDelay;
                patternData.targetPlayerPart = BodyPart.LeftLeg;
            }
        }

        protected override void OnPreExecute()
        {
            EnsurePatternDataConfigured();
            if (OwnerBoss != null)
            {
                _groundY = OwnerBoss.GroundY;
                OwnerBoss.SnapToGroundY();
                OwnerBoss.FacePlayer();
            }
            _hitRounds.Clear();

            ClearActiveHazards();
        }

        /// <summary>
        /// 동일 드리블 회차에서 좌/우 충격파 중 한찪에 이미 피격되었는지 확인하고 최초 1회만 등록함.
        /// </summary>
        public bool TryRegisterRoundHit(int roundIndex)
        {
            return _hitRounds.Add(roundIndex);
        }

        /// <summary>
        /// 전조(0.6초): 보스 발밑 바닥이 X축으로 찌이익 갈라지는 균열 및 예열 진동 연출
        /// </summary>
        public override void OnTellStarted()
        {
            if (OwnerBoss == null) return;

            _groundY = OwnerBoss.GroundY;
            OwnerBoss.Animation?.PlayChargeLoop();

            // 1. 바닥 균열 인디케이터 스폰 (X축으로 확장되는 연출)
            SpawnFloorCrackIndicator();

            // 2. 보스 차징 예열 미세 진동
            OwnerBoss.transform.DOShakePosition(tellCrackDuration * 0.9f, new Vector3(0.04f, 0f, 0f), 25, 90f, false, true);

            Debug.Log($"<color=yellow>[JunkWheelerDribbleShockwave] ⚡ 전조 개시! 바닥 균열 X축 확장 예고 (0.6초)</color>");
        }

        public override void OnTellEnded()
        {
            DestroyFloorCrackIndicator();
            if (OwnerBoss != null)
            {
                OwnerBoss.transform.DOKill();
                OwnerBoss.SnapToGroundY();
            }
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (OwnerBoss == null) return;

            int totalRounds = roundConfigs != null ? roundConfigs.Length : 4;
            Debug.Log($"<color=orange>[JunkWheelerDribbleShockwave] ▶ 드리블 충격파 액션 개시! (총 {totalRounds}회 X축 확장 바운스)</color>");

            for (int r = 0; r < totalRounds; r++)
            {
                token.ThrowIfCancellationRequested();
                int roundIndex = r + 1;
                var config = roundConfigs[r];

                Debug.Log($"<color=cyan>[JunkWheelerDribbleShockwave] ── 드리블 {roundIndex}/{totalRounds}회차! (X사거리: {config.shockwaveMaxDistance}m, X판정폭: {config.shockwaveHitWidth * 2f:F2}m, X스케일: {config.shockwaveScaleX}x) ──</color>");

                // 1단계: 도약 직전 바닥 압착 웅크림 (Squash)
                if (OwnerBoss.Animation != null)
                {
                    await OwnerBoss.Animation.PlayImpactSquashAndStretchAsync(
                        OwnerBoss.transform, new Vector2(1.15f, 0.78f), 0.05f, 0.05f, token);
                }

                // 2단계: 도약 먼지 연출 및 공중 도약 (Jump Up - 제자리 짧은 도약)
                SpawnVfx(jumpDustVfxPrefab, new Vector3(OwnerBoss.transform.position.x, _groundY, OwnerBoss.transform.position.z));
                OwnerBoss.Animation?.PlaySlamJump();

                _currentTween = OwnerBoss.transform.DOMoveY(_groundY + config.jumpHeight, config.jumpDuration)
                    .SetEase(Ease.OutQuad);
                await _currentTween.ToUniTask(cancellationToken: token);

                // 3단계: 지면 급강하 (Fall Down)
                OwnerBoss.Animation?.PlaySlamFall();
                _currentTween = OwnerBoss.transform.DOMoveY(_groundY, config.fallDuration)
                    .SetEase(Ease.InQuad);
                await _currentTween.ToUniTask(cancellationToken: token);

                // 4단계: 착지 (Land 쾅!)
                OwnerBoss.SnapToGroundY();
                OwnerBoss.Animation?.PlaySlamLand();

                // 착지 중앙 충격 이펙트
                SpawnVfx(dribbleLandVfxPrefab, new Vector3(OwnerBoss.transform.position.x, _groundY, OwnerBoss.transform.position.z));

                // 비대칭 펀치 연출 (Squash & Stretch: 0.04s 압착 ➔ 탄성 복원)
                if (OwnerBoss.Animation != null)
                {
                    _ = OwnerBoss.Animation.PlayImpactSquashAndStretchAsync(
                        OwnerBoss.transform, config.landSquashScale, 0.04f, 0.18f, token);
                }

                // ★ [X축 점진적 확장 충격파 발사! (좌/우 방향)]
                SpawnShockwaveHazard(roundIndex, -1f, config);
                SpawnShockwaveHazard(roundIndex, +1f, config);

                Debug.Log($"<color=yellow>[JunkWheelerDribbleShockwave] 💥 {roundIndex}회차 착지 완료! 좌/우 광폭 충격파 전파 (X사거리: {config.shockwaveMaxDistance}m, X폭: {config.shockwaveHitWidth * 2f:F2}m)</color>");

                // 다음 회차 도약 전 짧은 리듬 딜레이
                if (r < totalRounds - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(pauseBetweenDribbles), cancellationToken: token);
                }
            }

            // 4회 바운스 종료 후 스케일 원상 복구 및 플레이어 응시
            if (OwnerBoss.Animation != null)
            {
                OwnerBoss.Animation.ResetScale();
                OwnerBoss.FacePlayer();
            }

            // 기획서 §1408: 4번째 착지 후 0.8초간 지면 정지 확정 딜타임 (무방비 상태)
            Debug.Log($"<color=green><b>[JunkWheelerDribbleShockwave] 🛡️ 4회 드리블 완료! {postLandVulnerableDelay:F1}초간 지면 정지 확정 딜타임(무방비) 진입!</b></color>");
            OwnerBoss.Animation?.PlayIdle();
            await UniTask.Delay(TimeSpan.FromSeconds(postLandVulnerableDelay), cancellationToken: token);
        }

        private void SpawnShockwaveHazard(int roundIndex, float directionX, DribbleRoundConfig config)
        {
            if (OwnerBoss == null) return;

            float spawnX = OwnerBoss.transform.position.x + directionX * 0.45f;
            Vector3 spawnPos = new Vector3(spawnX, _groundY, 0f);

            GameObject hazardObj = new GameObject($"ShockwaveHazard_R{roundIndex}_{(directionX < 0 ? "L" : "R")}");
            hazardObj.transform.position = spawnPos;

            var hazard = hazardObj.AddComponent<JunkWheelerShockwaveHazard>();
            hazard.Initialize(
                directionX: directionX,
                speed: config.shockwaveSpeed,
                maxDistance: config.shockwaveMaxDistance,
                hitWidth: config.shockwaveHitWidth,
                scaleX: config.shockwaveScaleX,
                coreDamage: config.coreDamage,
                legDamage: config.legDamage,
                groundY: _groundY,
                roundIndex: roundIndex,
                owner: this,
                visualPrefab: shockwaveVisualPrefab,
                hitVfxPrefab: hitVfxPrefab
            );

            _activeHazards.Add(hazard);
        }

        private void SpawnFloorCrackIndicator()
        {
            DestroyFloorCrackIndicator();

            if (tellCrackVfxPrefab != null)
            {
                _activeCrackIndicator = SpawnVfx(
                    tellCrackVfxPrefab,
                    new Vector3(OwnerBoss.transform.position.x, _groundY, 0f),
                    Quaternion.identity,
                    new Vector3(1.0f, 1.0f, 1.0f),
                    tellCrackDuration + 0.2f
                );
                // 0.6초 동안 X축으로 찌이익 넓어지는 연출
                if (_activeCrackIndicator != null)
                {
                    _activeCrackIndicator.transform.DOScaleX(crackWidth * 1.5f, tellCrackDuration).From(0.5f).SetEase(Ease.OutQuad);
                }
            }
            else
            {
                // 프로시저럴 바닥 균열 경고 바 생성
                _activeCrackIndicator = new GameObject("ProceduralCrackIndicator");
                _activeCrackIndicator.transform.position = new Vector3(OwnerBoss.transform.position.x, _groundY + 0.04f, 0f);

                var sr = _activeCrackIndicator.AddComponent<SpriteRenderer>();
                sr.color = crackWarningColor;
                sr.sortingOrder = 4;

                Texture2D tex = Texture2D.whiteTexture;
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                _activeCrackIndicator.transform.localScale = new Vector3(0.2f, 0.08f, 1f);

                // X축으로 0.2m -> crackWidth(3.0m)로 점점 찌이익 넓어지는 연출!
                _activeCrackIndicator.transform.DOScaleX(crackWidth, tellCrackDuration).SetEase(Ease.OutQuad);
                sr.DOFade(0.3f, 0.12f).SetLoops(-1, LoopType.Yoyo);
            }
        }

        private void DestroyFloorCrackIndicator()
        {
            if (_activeCrackIndicator != null)
            {
                _activeCrackIndicator.transform.DOKill();
                Destroy(_activeCrackIndicator);
                _activeCrackIndicator = null;
            }
        }

        private void ClearActiveHazards()
        {
            for (int i = _activeHazards.Count - 1; i >= 0; i--)
            {
                if (_activeHazards[i] != null)
                {
                    _activeHazards[i].ForceDestroy();
                }
            }
            _activeHazards.Clear();
        }

        protected override void OnInterrupted(InterruptReason reason)
        {
            base.OnInterrupted(reason);
            CleanupEffects();
        }

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            DestroyFloorCrackIndicator();
            ClearActiveHazards();

            if (_currentTween != null && _currentTween.IsActive())
            {
                _currentTween.Kill();
                _currentTween = null;
            }

            if (OwnerBoss != null)
            {
                OwnerBoss.transform.DOKill();
                OwnerBoss.SnapToGroundY();
                OwnerBoss.Animation?.ResetScale();
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            AutoAssignPrefabs();
        }

        private void OnValidate()
        {
            AutoAssignPrefabs();
        }

        private void AutoAssignPrefabs()
        {
            if (shockwaveVisualPrefab == null)
            {
                shockwaveVisualPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Eric VFX Studio/Flipbook VFX - Cartoon Skill/Prefab/FX_Flipbook_GroundShock.prefab");
            }
            if (dribbleLandVfxPrefab == null)
            {
                dribbleLandVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Eric VFX Studio/Flipbook VFX - Cartoon Skill/Prefab/FX_Flipbook_Blast_Ground.prefab");
            }
            if (hitVfxPrefab == null)
            {
                hitVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Eric VFX Studio/Flipbook VFX - Cartoon Skill/Prefab/FX_Flipbook_StarShock.prefab");
            }
            if (tellCrackVfxPrefab == null)
            {
                tellCrackVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Eric VFX Studio/Flipbook VFX - Cartoon Skill/Prefab/FX_Flipbook_SlashGround_02.prefab");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position;
            center.y = _groundY;

            // 1~4회차 X축 도달 사거리 및 가로 판정폭 기즈모 시각화
            if (roundConfigs != null)
            {
                Color[] colors = new Color[]
                {
                    new Color(1f, 0.9f, 0.2f, 0.4f), // 1회차 노랑
                    new Color(1f, 0.6f, 0.1f, 0.5f), // 2회차 주황
                    new Color(1f, 0.3f, 0.1f, 0.6f), // 3회차 다홍
                    new Color(1f, 0.1f, 0.1f, 0.7f)  // 4회차 빨강
                };

                for (int i = 0; i < roundConfigs.Length; i++)
                {
                    Gizmos.color = colors[i % colors.Length];
                    float d = roundConfigs[i].shockwaveMaxDistance;
                    float w = roundConfigs[i].shockwaveHitWidth * 2f;
                    Gizmos.DrawWireCube(center, new Vector3(d * 2f, 0.15f, 0.1f));
                }
            }
        }
#endif
    }
}
