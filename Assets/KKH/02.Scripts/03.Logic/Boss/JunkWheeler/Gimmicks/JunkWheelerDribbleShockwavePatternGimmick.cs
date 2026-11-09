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

        [Header("드리블 4회 라운드 설정 (Vfx_Slam_Sprak 이펙트 반경과 1:1 동기화)")]
        [SerializeField]
        private DribbleRoundConfig[] roundConfigs = new DribbleRoundConfig[4]
        {
            // 1회차: 소형 (이펙트 반경 ~1.0m, X스케일 0.8x)
            new DribbleRoundConfig
            {
                jumpHeight = 0.45f,
                jumpDuration = 0.16f,
                fallDuration = 0.12f,
                shockwaveMaxDistance = 0.70f,
                shockwaveHitWidth = 0.12f,
                shockwaveScaleX = 0.8f,
                shockwaveSpeed = 2.5f,
                coreDamage = 20,
                legDamage = 15,
                landSquashScale = new Vector2(1.20f, 0.75f)
            },
            // 2회차: 중형 (이펙트 반경 ~1.38m, X스케일 1.1x)
            new DribbleRoundConfig
            {
                jumpHeight = 0.48f,
                jumpDuration = 0.17f,
                fallDuration = 0.12f,
                shockwaveMaxDistance = 1.05f,
                shockwaveHitWidth = 0.14f,
                shockwaveScaleX = 1.1f,
                shockwaveSpeed = 3.2f,
                coreDamage = 25,
                legDamage = 20,
                landSquashScale = new Vector2(1.28f, 0.70f)
            },
            // 3회차: 대형 (이펙트 반경 ~1.75m, X스케일 1.4x)
            new DribbleRoundConfig
            {
                jumpHeight = 0.50f,
                jumpDuration = 0.18f,
                fallDuration = 0.13f,
                shockwaveMaxDistance = 1.40f,
                shockwaveHitWidth = 0.16f,
                shockwaveScaleX = 1.4f,
                shockwaveSpeed = 4.2f,
                coreDamage = 30,
                legDamage = 25,
                landSquashScale = new Vector2(1.35f, 0.65f)
            },
            // 4회차: 특대형 (이펙트 반경 ~2.25m, X스케일 1.8x)
            new DribbleRoundConfig
            {
                jumpHeight = 0.55f,
                jumpDuration = 0.20f,
                fallDuration = 0.14f,
                shockwaveMaxDistance = 1.88f,
                shockwaveHitWidth = 0.18f,
                shockwaveScaleX = 1.8f,
                shockwaveSpeed = 5.5f,
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

        [Header("★ 착지 이펙트 위치 오프셋 (인스펙터 미세 조정)")]
        [Tooltip("보스 중심(transform.position) 대비 착지 이펙트 및 충격파 스폰 오프셋 (보스가 바라보는 방향에 따라 X축 부호 자동 반전)")]
        [SerializeField] private Vector3 dribbleVfxOffset = Vector3.zero;

        private float _groundY;
        private Tweener _currentTween;
        private GameObject _activeCrackIndicator;
        private readonly List<JunkWheelerShockwaveHazard> _activeHazards = new List<JunkWheelerShockwaveHazard>();
        private readonly HashSet<int> _hitRounds = new HashSet<int>();

        /// <summary>
        /// 보스가 바라보는 방향(Facing) 및 월드 깊이(Z)를 반영하여 착지 중앙 이펙트 및 충격파가 스폰될 정확한 월드 지면 좌표를 반환함.
        /// </summary>
        public Vector3 GetLandingCenterPosition()
        {
            if (OwnerBoss == null) return transform.position;
            Vector3 bossPos = OwnerBoss.transform.position;
            float signX = OwnerBoss.transform.localScale.x >= 0f ? 1f : -1f;
            return new Vector3(
                bossPos.x + dribbleVfxOffset.x * signX,
                _groundY + dribbleVfxOffset.y,
                bossPos.z + dribbleVfxOffset.z
            );
        }

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

                Vector3 landingCenter = GetLandingCenterPosition();

                // 착지 중앙 충격 이펙트 (중복 생성 방지: shockwaveVisualPrefab과 동일하거나 수평 고리인 경우 지면 링 로직에서 단일 전담 처리)
                if (dribbleLandVfxPrefab != null && dribbleLandVfxPrefab != shockwaveVisualPrefab && !IsSymmetricGroundRingPrefab(dribbleLandVfxPrefab))
                {
                    SpawnVfx(dribbleLandVfxPrefab, landingCenter);
                }

                // 비대칭 펀치 연출 (Squash & Stretch: 0.04s 압착 ➔ 탄성 복원)
                if (OwnerBoss.Animation != null)
                {
                    _ = OwnerBoss.Animation.PlayImpactSquashAndStretchAsync(
                        OwnerBoss.transform, config.landSquashScale, 0.04f, 0.18f, token);
                }

                // ★ [X축 점진적 확장 충격파 발사! (이펙트 반경과 피격 판정 범위 1:1 완전 동기화)]
                // Vfx_Slam_Sprak(28프레임 2.5D 대칭형 타원 고리)은 보스 착지 중앙(landingCenter)에 1회 스폰하여
                // 회차별(1~4회) 및 프레임 진행(1~16f)에 맞춰 X축 크기를 점진적으로 확장시킴.
                // 좌/우 이동 해저드(JunkWheelerShockwaveHazard)는 실제 이펙트의 시각적 외곽 링 반경(visualOuterRadius)까지만 정확히 전파됨.
                bool isSymmetricGroundRing = IsSymmetricGroundRingPrefab(shockwaveVisualPrefab);
                GameObject groundRingPrefab = isSymmetricGroundRing ? shockwaveVisualPrefab : (IsSymmetricGroundRingPrefab(dribbleLandVfxPrefab) ? dribbleLandVfxPrefab : null);
                bool hasGroundRing = groundRingPrefab != null;

                float syncedVisualOuterRadius = -1f;
                if (hasGroundRing)
                {
                    syncedVisualOuterRadius = SpawnExpandingGroundShockwaveVisual(config, landingCenter, groundRingPrefab);
                }

                SpawnShockwaveHazard(roundIndex, -1f, config, !hasGroundRing, syncedVisualOuterRadius, landingCenter);
                SpawnShockwaveHazard(roundIndex, +1f, config, !hasGroundRing, syncedVisualOuterRadius, landingCenter);

                Debug.Log($"<color=yellow>[JunkWheelerDribbleShockwave] 💥 {roundIndex}회차 착지 완료! 좌/우 충격파 전파 (시각/판정 반경: {(syncedVisualOuterRadius > 0f ? syncedVisualOuterRadius : config.shockwaveMaxDistance):F2}m)</color>");

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

        private bool IsSymmetricGroundRingPrefab(GameObject prefab)
        {
            if (prefab == null) return false;
            var psr = prefab.GetComponentInChildren<ParticleSystemRenderer>(true);
            return psr != null && psr.renderMode == ParticleSystemRenderMode.HorizontalBillboard;
        }

        // Vfx_Slam_Sprak(localScale.x = 8.0일 때 실제 지면 밝은 고리 반경 = 1.0m -> X스케일 1.0당 0.125m 반경)
        private const float VisualRadiusPerScaleX = 0.125f;
        private const float VisualRingExpandDuration = 0.35f;

        private float SpawnExpandingGroundShockwaveVisual(DribbleRoundConfig config, Vector3 spawnCenter, GameObject prefabOverride = null)
        {
            GameObject prefab = prefabOverride != null ? prefabOverride : shockwaveVisualPrefab;
            if (OwnerBoss == null || prefab == null) return -1f;

            // 1. 보스 착지 지점 중앙 바닥(보스와 일치하는 월드 Z깊이)에 1회 스폰
            GameObject vfxObj = Instantiate(prefab, spawnCenter, Quaternion.identity);
            if (vfxObj == null) return -1f;

            // 판정 완전 분리
            BossVfxHelper.ConfigureVisualOnly(vfxObj);

            // 2. 인스턴스화된 오브젝트의 가로세로 원본 비율(360x154 = 약 2.338 : 1)에 맞춘 Z축 스케일 산출
            Vector3 baseScale = vfxObj.transform.localScale;
            if (baseScale.x < 0.1f) baseScale.x = 8.0f;

            // Z축을 3.42m(스프라이트 비율 154/360)로 제한하여 3D 투영 카메라 원근 왜곡 및 화면 좌측 쏠림 완전 차단
            float baseZ = baseScale.x * (154f / 360f);
            if (baseScale.z > 10f || baseScale.z < 0.5f)
            {
                baseScale = new Vector3(baseScale.x, 1f, baseZ);
            }
            else
            {
                baseScale = new Vector3(baseScale.x, baseScale.y, baseZ);
            }

            // 3. 드리블 회차별(1~4회차) X축 점진적 확장 (Z축은 스프라이트 비율 유지):
            // 1회차(0.8x): 4.8 -> 8.0 (반경 1.00m) | 2회차(1.1x): 6.6 -> 11.0 (반경 1.38m)
            // 3회차(1.4x): 8.4 -> 14.0 (반경 1.75m) | 4회차(1.8x): 10.8 -> 18.0 (반경 2.25m)
            float startScaleX = baseScale.x * config.shockwaveScaleX * 0.75f;
            float endScaleX = baseScale.x * config.shockwaveScaleX * 1.25f;
            float currentZ = baseZ * config.shockwaveScaleX;
            vfxObj.transform.localScale = new Vector3(startScaleX, baseScale.y, currentZ);

            // 4. Sorting Order 보정 및 MaxParticleSize(0.5 -> 5.0) 제한 해제로 대형 X축 스케일 잘림 방지
            var renderers = vfxObj.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.sortingOrder = Mathf.Max(r.sortingOrder, 10);
                if (r is ParticleSystemRenderer psr)
                {
                    psr.maxParticleSize = Mathf.Max(psr.maxParticleSize, 5.0f);
                }
            }

            // 5. 3D Start Size(size3D) 강제 활성화 후 Stop -> Play로 t=0 Burst 파티클 정상 방출 보장
            float duration = 0.6f;
            var particles = vfxObj.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particles)
            {
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = false;
                main.startSize3D = true;
                duration = Mathf.Max(duration, main.duration + main.startLifetime.constantMax);
                ps.Play(true);
            }

            // 6. 스프라이트 시트 고리 팽창 구간(1~16프레임, 약 0.35초)에 맞춰 X축 실시간 확장 트윈
            vfxObj.transform.DOScaleX(endScaleX, VisualRingExpandDuration).SetEase(Ease.OutCubic);

            Destroy(vfxObj, duration + 0.15f);

            // 실제 화면에 그려지는 이펙트 고리의 외곽 반경(m) 반환
            return endScaleX * VisualRadiusPerScaleX;
        }

        private void SpawnShockwaveHazard(
            int roundIndex,
            float directionX,
            DribbleRoundConfig config,
            bool spawnVisual = true,
            float syncedVisualOuterRadius = -1f,
            Vector3? landingCenterOverride = null)
        {
            if (OwnerBoss == null) return;

            Vector3 center = landingCenterOverride ?? GetLandingCenterPosition();

            // 대칭형 지면 고리(Vfx_Slam_Sprak) 사용 시 피격 판정 외곽 끝단이 이펙트 고리 반경을 절대 넘지 않도록 1:1 동기화
            float spawnOffset = syncedVisualOuterRadius > 0f ? 0.15f : 0.45f;
            float hitWidth = config.shockwaveHitWidth;
            float maxDistance = config.shockwaveMaxDistance;
            float speed = config.shockwaveSpeed;

            if (syncedVisualOuterRadius > 0f)
            {
                hitWidth = Mathf.Min(hitWidth, 0.18f);
                float finalExpandedHitHalfWidth = hitWidth * 1.20f;
                // 보스 중심에서 (spawnOffset + maxDistance + finalExpandedHitHalfWidth) == syncedVisualOuterRadius가 되도록 제한
                maxDistance = Mathf.Max(0.15f, syncedVisualOuterRadius - spawnOffset - finalExpandedHitHalfWidth);
                // 이펙트 고리가 팽창하는 시간(0.35초)에 맞춰 판정 파동이 고리 끝단에 정확히 도달하도록 속도 동기화
                speed = maxDistance / VisualRingExpandDuration;
            }

            float spawnX = center.x + directionX * spawnOffset;
            Vector3 spawnPos = new Vector3(spawnX, _groundY, center.z);

            GameObject hazardObj = new GameObject($"ShockwaveHazard_R{roundIndex}_{(directionX < 0 ? "L" : "R")}");
            hazardObj.transform.position = spawnPos;

            var hazard = hazardObj.AddComponent<JunkWheelerShockwaveHazard>();
            hazard.Initialize(
                directionX: directionX,
                speed: speed,
                maxDistance: maxDistance,
                hitWidth: hitWidth,
                scaleX: config.shockwaveScaleX,
                coreDamage: config.coreDamage,
                legDamage: config.legDamage,
                groundY: _groundY,
                roundIndex: roundIndex,
                owner: this,
                visualPrefab: spawnVisual ? shockwaveVisualPrefab : null,
                hitVfxPrefab: hitVfxPrefab,
                suppressProceduralVisual: !spawnVisual
            );

            _activeHazards.Add(hazard);
        }

        private void SpawnFloorCrackIndicator()
        {
            DestroyFloorCrackIndicator();

            Vector3 center = GetLandingCenterPosition();

            if (tellCrackVfxPrefab != null)
            {
                Vector3 origCrackScale = tellCrackVfxPrefab.transform.localScale;
                if (origCrackScale.sqrMagnitude < 0.0001f) origCrackScale = Vector3.one;

                _activeCrackIndicator = SpawnVfx(
                    tellCrackVfxPrefab,
                    center,
                    Quaternion.identity,
                    origCrackScale,
                    tellCrackDuration + 0.2f
                );
                // 0.6초 동안 원본 X축 스케일을 기준으로 찌이익 넓어지는 연출
                if (_activeCrackIndicator != null)
                {
                    _activeCrackIndicator.transform
                        .DOScaleX(origCrackScale.x * crackWidth * 1.5f, tellCrackDuration)
                        .From(origCrackScale.x * 0.5f)
                        .SetEase(Ease.OutQuad);
                }
            }
            else
            {
                // 프로시저럴 바닥 균열 경고 바 생성
                _activeCrackIndicator = new GameObject("ProceduralCrackIndicator");
                _activeCrackIndicator.transform.position = new Vector3(center.x, _groundY + 0.04f, center.z);

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
                    "Assets/Sprites/Vfx/Vfx_Slam_Sprak.prefab");
            }
            if (dribbleLandVfxPrefab == null)
            {
                dribbleLandVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Sprites/Vfx/Vfx_Crack_Small.prefab");
            }
            if (hitVfxPrefab == null)
            {
                hitVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Eric VFX Studio/Flipbook VFX - Cartoon Skill/Prefab/FX_Flipbook_StarShock.prefab");
            }
            if (tellCrackVfxPrefab == null)
            {
                tellCrackVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Sprites/Vfx/Vfx_Crack_Small.prefab");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = GetLandingCenterPosition();

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

                bool isSymmetric = IsSymmetricGroundRingPrefab(shockwaveVisualPrefab);
                for (int i = 0; i < roundConfigs.Length; i++)
                {
                    Gizmos.color = colors[i % colors.Length];
                    float outerRadius = isSymmetric
                        ? (8f * roundConfigs[i].shockwaveScaleX * 1.25f * VisualRadiusPerScaleX)
                        : (0.45f + roundConfigs[i].shockwaveMaxDistance + roundConfigs[i].shockwaveHitWidth * 1.2f);
                    Gizmos.DrawWireCube(center, new Vector3(outerRadius * 2f, 0.15f, 0.1f));
                }
            }
        }
#endif
    }
}
