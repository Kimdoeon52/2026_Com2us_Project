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
    /// [정크 휠러 회전 톱날 당구 돌진 기믹 (JunkWheelerRicochetBladePatternGimmick)]
    /// 신규 패턴 (PATTERN_BLADE_RICOCHET / 당구 3쿠션 궤적):
    /// 1. 전조(Tell):
    ///    - 휠 가속 및 톱날 전개 (Deploy VFX ➔ 초고속 Spin 회전).
    ///    - 지수 가속 진동/적색 점멸 스트로보 연출.
    /// 2. 본 액션 (당구 3쿠션 돌진):
    ///    - 플레이어 방향으로 사선(입사각 40~45도) 고속 돌진 (14.0 m/s).
    ///    - 카메라 뷰포트 기반 전장 4면(좌, 우, 상단 천장, 하단 지면)을 감지하여 실시간 벡터 반사 (3회 바운스).
    ///    - 벽/천장/지면 충돌 시마다 법선 방향에 맞춘 비대칭 스쿼시 펀치(Squash & Stretch) 및 스파크 연출.
    /// 3. 착지 및 복귀:
    ///    - 3회 바운스 종료 후 지면으로 급강하 착지.
    ///    - 강력한 착지 임팩트 펀치 연출.
    ///    - 톱날 수납(Retract VFX) 및 휠 관성 지면 구름 감속(PlayRollDecelAsync).
    ///    - 플레이어를 바라보며 Idle 상태로 복귀.
    /// </summary>
    public class JunkWheelerRicochetBladePatternGimmick : BossPatternGimmickBase
    {
        public const string PatternRicochetBladeID = "PATTERN_BLADE_RICOCHET";
        public override string GimmickID => PatternRicochetBladeID;

        [Header("비행 및 당구 물리 파라미터")]
        [Tooltip("당구 사선 돌진 속도 (m/s)")]
        [SerializeField] private float rushSpeed = 14.0f;
        [Tooltip("최대 바운스 횟수 (당구 3쿠션)")]
        [SerializeField] private int maxBounceCount = 3;
        [Tooltip("카메라 좌우 경계 패딩 (m)")]
        [SerializeField] private float cameraPaddingX = 0.50f;
        [Tooltip("카메라 상단 천장 경계 패딩 (m)")]
        [SerializeField] private float cameraPaddingY = 0.50f;
        [Tooltip("지면 착지 높이 오프셋")]
        [SerializeField] private float groundPaddingY = 0.1f;
        [Tooltip("사선 발사 수직 각도 (도)")]
        [SerializeField] private float initialAngleDeg = 40.0f;
        [Tooltip("회전 톱날 당구 돌진 시 플레이어 피격 감지 반경 (정밀 타격: 0.70m)")]
        [SerializeField] private float bladeHitRadius = 0.70f;

        [Header("비선형 전조(Tell) 파라미터")]
        [SerializeField] private Color flashColor = new Color(1.0f, 0.2f, 0.1f, 1f);
        [SerializeField] private float flashStartInterval = 0.25f;
        [SerializeField] private float flashEndInterval = 0.04f;
        [SerializeField] private float minShakeStrength = 0.02f;
        [SerializeField] private float maxShakeStrength = 0.18f;
        [SerializeField] private float minShakeVibrato = 12f;
        [SerializeField] private float maxShakeVibrato = 50f;
        [SerializeField] private float shakeCurveExponent = 2.8f;

        [Header("블레이드 VFX 스프라이트 (비어있을 시 자동 로드)")]
        [SerializeField] private Sprite[] deploySprites;
        [SerializeField] private Sprite[] spinSprites;
        [SerializeField] private Sprite[] retractSprites;
        [SerializeField] private Vector3 bladeLocalOffset = new Vector3(0f, 0.375f, -0.05f);

        [Header("시각 효과(VFX) 프리팹 (인스펙터 할당)")]
        [Tooltip("당구 3쿠션 벽/천장/바닥 충돌 시 발생하는 충격/스파크 VFX (예: FX_Flipbook_ImpactAir, FX_Flipbook_StarShock)")]
        [SerializeField] private GameObject bounceImpactVfxPrefab;
        [Tooltip("회전 톱날로 플레이어 타격 시 발생하는 참격/타격 VFX (예: FX_Flipbook_OrangeSlash, FX_Flipbook_SplashHit4)")]
        [SerializeField] private GameObject bladeHitVfxPrefab;
        [Tooltip("3쿠션 종료 후 지면 착지 충격파 VFX (예: FX_Flipbook_Blast_Ground, FX_Flipbook_GroundShock)")]
        [SerializeField] private GameObject landImpactVfxPrefab;

        // 런타임 제어 변수
        private GameObject _bladeVfxObj;
        private SpriteRenderer _bladeRenderer;
        private Animator _bladeAnimator;
        private float _groundY;
        private Tweener _currentTween;
        private CancellationTokenSource _tellCts;
        private Vector3 _baseWorldPosBeforeTell;
        private Color _originalColor = Color.white;
        private bool _isActionRunning = false;
        public bool IsActionRunning => _isActionRunning;
        private bool _isBladeSpinning = false;
        private float _currentSpinInterval = 0.045f;
        private float _spinFrameTimer = 0f;
        private int _spinFrameIndex = 0;

        private static readonly int AnimHashDeploy = Animator.StringToHash("Boss_Blades_Deploy");
        private static readonly int AnimHashSpin = Animator.StringToHash("Boss_Blades_Spin");
        private static readonly int AnimHashRetract = Animator.StringToHash("Boss_Blades_Retract");

        private void Awake()
        {
            if (Mathf.Abs(bladeLocalOffset.y - 0.80f) < 0.05f)
            {
                bladeLocalOffset = new Vector3(0f, 0.375f, -0.05f);
            }
            EnsureBladeVfxObject();
            LoadSpritesIfNeeded();
        }

        private void Update()
        {
            if (!_isBladeSpinning) return;

            EnsureBladeVfxObject();
            if (_bladeVfxObj == null || _bladeRenderer == null) return;

            if (!_bladeVfxObj.activeSelf)
            {
                _bladeVfxObj.SetActive(true);
            }

            float dt = Time.deltaTime;

            // 1) 자식 오브젝트의 부모 스케일 왜곡 방지:
            // 보스가 좌우 반전(scale.x = -1)되거나 임팩트 스쿼시(scale.y = 0.55)될 때
            // 원형 톱날 회전 VFX가 타원형으로 찌그러지지 않고 월드 상에서 정원(1, 1, 1)을 유지하도록 보정
            Transform parent = _bladeVfxObj.transform.parent;
            if (parent != null)
            {
                Vector3 parentScale = parent.localScale;
                float px = Mathf.Approximately(parentScale.x, 0f) ? 1f : Mathf.Abs(parentScale.x);
                float py = Mathf.Approximately(parentScale.y, 0f) ? 1f : Mathf.Abs(parentScale.y);
                float pz = Mathf.Approximately(parentScale.z, 0f) ? 1f : Mathf.Abs(parentScale.z);
                float sign = parentScale.x < 0f ? -1f : 1f;
                _bladeVfxObj.transform.localScale = new Vector3(sign / px, 1f / py, 1f / pz);
            }

            // 2) 물리적 Z축 연속 고속 회전 (전조 진동/점멸 중에도 매 프레임 끊김 없이 회전 보장)
            float rotSpeed = _currentSpinInterval <= 0.045f ? -1080f : -540f;
            _bladeVfxObj.transform.Rotate(0f, 0f, rotSpeed * dt);

            // 2) 스핀 스프라이트 프레임 순환 (비어있거나 null이면 deploySprites 마지막 프레임으로 안전 폴백)
            _spinFrameTimer += dt;
            float interval = Mathf.Max(0.02f, _currentSpinInterval);
            if (_spinFrameTimer >= interval)
            {
                _spinFrameTimer -= interval;
                Sprite nextSprite = GetValidSpinSprite(_spinFrameIndex);
                if (nextSprite != null)
                {
                    _bladeRenderer.sprite = nextSprite;
                }
                _spinFrameIndex++;
            }
            else if (_bladeRenderer.sprite == null)
            {
                Sprite fallback = GetValidSpinSprite(_spinFrameIndex);
                if (fallback != null) _bladeRenderer.sprite = fallback;
            }
        }

        private Sprite GetValidSpinSprite(int index)
        {
            LoadSpritesIfNeeded();
            if (spinSprites != null && spinSprites.Length > 0)
            {
                Sprite s = spinSprites[Mathf.Abs(index) % spinSprites.Length];
                if (s != null) return s;
            }
            if (deploySprites != null && deploySprites.Length > 0)
            {
                for (int i = deploySprites.Length - 1; i >= 0; i--)
                {
                    if (deploySprites[i] != null) return deploySprites[i];
                }
            }
            return null;
        }

        private void Reset()
        {
            LoadSpritesIfNeeded();
        }

        private void OnValidate()
        {
            if (Mathf.Abs(bladeLocalOffset.y - 0.80f) < 0.05f)
            {
                bladeLocalOffset = new Vector3(0f, 0.375f, -0.05f);
            }
            if (_bladeVfxObj != null)
            {
                _bladeVfxObj.transform.localPosition = bladeLocalOffset;
            }
            LoadSpritesIfNeeded();
        }

        protected override void OnPreExecute()
        {
            base.OnPreExecute();
            if (OwnerBoss != null)
            {
                _groundY = OwnerBoss.GroundY;
                OwnerBoss.SnapToGroundY();
                OwnerBoss.Animation?.ResetScale();
                OwnerBoss.FacePlayer();
            }
            EnsureBladeVfxObject();
        }

        public override void OnTellStarted()
        {
            if (OwnerBoss == null) return;
            OwnerBoss.Animation?.PlayChargeLoop();

            StopTellVisuals();
            _tellCts = new CancellationTokenSource();
            float baseDur = PatternData != null ? PatternData.tellDuration : 1.8f;
            float multiplier = (OwnerBoss != null && OwnerBoss.Snapshot != null) ? OwnerBoss.Snapshot.tellDurationMultiplier : 1.0f;
            float tellDuration = baseDur * multiplier;
            RunTellSequenceAsync(tellDuration, _tellCts.Token).Forget();
        }

        public override void OnTellEnded()
        {
            StopTellVisuals();
        }

        private void StopTellVisuals()
        {
            if (_tellCts != null)
            {
                if (!_tellCts.IsCancellationRequested) _tellCts.Cancel();
                _tellCts.Dispose();
                _tellCts = null;
            }

            if (OwnerBoss != null && OwnerBoss.Animation != null && OwnerBoss.Animation.SpriteRenderer != null)
            {
                OwnerBoss.Animation.SpriteRenderer.color = _originalColor;
                if (_baseWorldPosBeforeTell != Vector3.zero)
                {
                    OwnerBoss.transform.position = _baseWorldPosBeforeTell;
                    _baseWorldPosBeforeTell = Vector3.zero;
                }
            }
        }

        private async UniTaskVoid RunTellSequenceAsync(float duration, CancellationToken ct)
        {
            if (OwnerBoss == null || OwnerBoss.Animation == null) return;
            var sr = OwnerBoss.Animation.SpriteRenderer;
            if (sr != null) _originalColor = sr.color;

            _baseWorldPosBeforeTell = OwnerBoss.transform.position;
            EnsureBladeVfxObject();
            if (_bladeVfxObj != null)
            {
                _bladeVfxObj.SetActive(true);
            }

            // Deploy on Tell start -> start Spin loop immediately after Deploy
            PlayBladeDeployAndSpinAsync(ct).Forget();

            float elapsed = 0f;
            float flashTimer = 0f;
            bool isFlash = false;

            try
            {
                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();

                    float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
                    float nonLinearT = Mathf.Pow(progress, shakeCurveExponent);

                    // 1) Shake position
                    float currentShakeStrength = Mathf.Lerp(minShakeStrength, maxShakeStrength, nonLinearT);
                    float currentVibrato = Mathf.Lerp(minShakeVibrato, maxShakeVibrato, nonLinearT);
                    float shakePhase = elapsed * currentVibrato * Mathf.PI * 2f;
                    float offsetX = Mathf.Sin(shakePhase) * currentShakeStrength;
                    float offsetY = Mathf.Cos(shakePhase * 1.33f) * (currentShakeStrength * 0.4f);

                    if (OwnerBoss != null)
                    {
                        OwnerBoss.transform.position = _baseWorldPosBeforeTell + new Vector3(offsetX, offsetY, 0f);
                    }

                    // 2) Flash intervals
                    float currentFlashInterval = Mathf.Lerp(flashStartInterval, flashEndInterval, nonLinearT);
                    flashTimer += Time.deltaTime;
                    if (flashTimer >= currentFlashInterval)
                    {
                        flashTimer -= currentFlashInterval;
                        isFlash = !isFlash;
                    }

                    if (sr != null)
                    {
                        if (progress >= 0.92f)
                        {
                            sr.color = flashColor;
                        }
                        else
                        {
                            sr.color = isFlash ? flashColor : _originalColor;
                        }
                    }

                    elapsed += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)
            {
                // 외부 인터럽트 또는 OnTellEnded 시 정상 종료
            }
            finally
            {
                if (sr != null) sr.color = _originalColor;
                if (OwnerBoss != null && _baseWorldPosBeforeTell != Vector3.zero)
                {
                    OwnerBoss.transform.position = _baseWorldPosBeforeTell;
                    _baseWorldPosBeforeTell = Vector3.zero;
                }
            }
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (OwnerBoss == null) return;
            _isActionRunning = true;
            StopTellVisuals();

            try
            {
                // 1단계: 발사 방향 설정 (플레이어를 향한 사선 상향 또는 하향)
                EnsureBladeVfxObject();
                if (_bladeVfxObj != null) _bladeVfxObj.SetActive(true);
                StartSpinLoop();

                Transform player = OwnerBoss.PlayerTransform;
                float dirX = 1f;
                if (player != null)
                {
                    dirX = player.position.x >= OwnerBoss.transform.position.x ? 1f : -1f;
                }
                else
                {
                    dirX = OwnerBoss.transform.localScale.x >= 0 ? 1f : -1f;
                }

                OwnerBoss.Animation?.FaceDirection(dirX);
                OwnerBoss.Animation?.PlayRollBarrage();

                // 사선 발사 벡터 (초기 각도: 지면에서 상단 천장 쪽으로 튀어오름)
                float rad = initialAngleDeg * Mathf.Deg2Rad;
                Vector2 moveDir = new Vector2(Mathf.Cos(rad) * Mathf.Sign(dirX), Mathf.Sin(rad)).normalized;

                // 2단계: 카메라 및 전장 바운더리 산출
                GetArenaBounds(out float minX, out float maxX, out float minY, out float maxY);

                Debug.Log($"<color=cyan>[JunkWheelerRicochet] 회전 톱날 당구 돌진 개시! 속도: {rushSpeed}m/s, 경계: X[{minX:F1}~{maxX:F1}], Y[{minY:F1}~{maxY:F1}]</color>");

                // 3단계: 당구 3쿠션 바운스 루프 (최대 5회 안전 클램프)
                int bounceLimit = Mathf.Clamp(maxBounceCount, 1, 5);
                int bounceCount = 0;
                float bounceVfxCooldown = 0f;
                bool hasHitInCurrentSegment = false;

                while (bounceCount < bounceLimit)
                {
                    token.ThrowIfCancellationRequested();

                    // 매 프레임 카메라/스테이지 경계를 동적으로 갱신 (카메라 줌아웃/패닝 실시간 반영)
                    GetArenaBounds(out minX, out maxX, out minY, out maxY);

                    float dt = Time.deltaTime;
                    if (bounceVfxCooldown > 0f) bounceVfxCooldown -= dt;

                    Vector3 currentPos = OwnerBoss.transform.position;
                    Vector3 nextPos = currentPos + (Vector3)(moveDir * (rushSpeed * dt));

                    bool bounced = false;
                    Vector2 impactNormal = Vector2.zero;

                    // 좌/우 벽면 충돌 체크 (실제로 해당 벽을 향해 진행 중일 때만 바운스 인정하여 카메라 수축에 의한 2연속 중복 바운스 차단)
                    if (nextPos.x <= minX && moveDir.x < 0f)
                    {
                        nextPos.x = minX;
                        moveDir.x = Mathf.Abs(moveDir.x);
                        bounced = true;
                        impactNormal = Vector2.right;
                    }
                    else if (nextPos.x >= maxX && moveDir.x > 0f)
                    {
                        nextPos.x = maxX;
                        moveDir.x = -Mathf.Abs(moveDir.x);
                        bounced = true;
                        impactNormal = Vector2.left;
                    }

                    // 상단 천장 / 하단 지면 충돌 체크 (출발 직후 상승 중이거나 이미 튕긴 후 카메라 경계 변동으로 인한 중복 바운스 차단)
                    if (nextPos.y >= maxY && moveDir.y > 0f)
                    {
                        nextPos.y = maxY;
                        moveDir.y = -Mathf.Abs(moveDir.y);
                        bounced = true;
                        impactNormal = Vector2.down;
                    }
                    else if (nextPos.y <= minY && moveDir.y < 0f)
                    {
                        nextPos.y = minY;
                        moveDir.y = Mathf.Abs(moveDir.y);
                        bounced = true;
                        impactNormal = Vector2.up;
                    }

                    OwnerBoss.transform.position = nextPos;

                    // 바운스 발생 시 연출
                    if (bounced)
                    {
                        bounceCount++;
                        hasHitInCurrentSegment = false; // 벽에 튕겨 새로운 궤적이 시작될 때만 재피격 허용
                        Debug.Log($"<color=yellow>[JunkWheelerRicochet] 당구 바운스 #{bounceCount}/{bounceLimit}! 법선: {impactNormal}</color>");

                        // [VFX 1] 벽/천장/바닥 바운스 임팩트 스폰 (모서리 코너 동시 충돌 시 중복 스폰 방지 쿨타임 적용)
                        if (bounceVfxCooldown <= 0f)
                        {
                            bounceVfxCooldown = 0.08f;
                            float rotZ = Mathf.Atan2(impactNormal.y, impactNormal.x) * Mathf.Rad2Deg;
                            SpawnVfx(bounceImpactVfxPrefab, nextPos, Quaternion.Euler(0f, 0f, rotZ));
                        }

                        // 이동 방향에 맞춰 스프라이트 반전 (스케일 부호 먼저 동기화)
                        OwnerBoss.Animation?.FaceDirection(moveDir.x);

                        // 벽 충돌 비대칭 임팩트 스쿼시 (충돌 법선 축으로 찌그러짐)
                        Vector2 squashScale = Mathf.Abs(impactNormal.x) > 0.5f 
                            ? new Vector2(0.55f, 1.45f) // 좌우 벽 충돌: X 압착, Y 팽창
                            : new Vector2(1.45f, 0.55f); // 천장/바닥 충돌: Y 압착, X 팽창

                        TriggerBounceSquash(squashScale, token).Forget();
                    }

                    // 플레이어 충돌 검사 (동일 직선 구간 내 다단 히트 및 이펙트 2회 발생 차단)
                    if (!hasHitInCurrentSegment && CheckPlayerCollision())
                    {
                        hasHitInCurrentSegment = true;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                }

                // 4단계: 3차 바운스 후 지면 착지 하강 (바운스 스쿼시 DOKill에 끊기지 않도록 프레임 루프로 확실히 지면까지 강하)
                _groundY = OwnerBoss != null ? OwnerBoss.GroundY : _groundY;
                Debug.Log($"<color=cyan>[JunkWheelerRicochet] 3쿠션 완료! 원지면({_groundY:F2})으로 착지 하강</color>");
                OwnerBoss.Animation?.PlaySlamFall();

                Vector3 fallStartPos = OwnerBoss.transform.position;
                Vector3 landTargetPos = new Vector3(OwnerBoss.ClampToStageWallX(fallStartPos.x, 0.5f), _groundY, fallStartPos.z);
                float fallDistY = Mathf.Abs(fallStartPos.y - _groundY);
                if (fallDistY > 0.02f)
                {
                    float fallDuration = Mathf.Clamp(fallDistY / Mathf.Max(8f, rushSpeed * 1.15f), 0.14f, 0.32f);
                    float fallElapsed = 0f;
                    while (fallElapsed < fallDuration)
                    {
                        token.ThrowIfCancellationRequested();
                        fallElapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(fallElapsed / fallDuration);
                        float easedT = t * t; // Ease.InQuad 급강하 가속
                        OwnerBoss.transform.position = Vector3.Lerp(fallStartPos, landTargetPos, easedT);
                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                    }
                }
                OwnerBoss.transform.position = landTargetPos;

                // [VFX 3] 지면 착지 충격파 스폰
                SpawnVfx(landImpactVfxPrefab, landTargetPos);

                // 5단계: 착지 임팩트 펀치 & 톱날 수납 & 롤 감속
                OwnerBoss.Animation?.PlaySlamLand();
                _currentSpinInterval = 0.065f; // 착지 충격 시 톱날 회전 감속
                if (OwnerBoss.Animation != null)
                {
                    await OwnerBoss.Animation.PlayImpactSquashAndStretchAsync(
                        OwnerBoss.transform, new Vector2(1.45f, 0.50f), 0.04f, 0.28f, token);
                }

                // 톱날 수납 VFX
                await PlayBladeRetractAsync(token);

                // 휠 지면 구름 감속 연출 (0.40초)
                if (OwnerBoss.Animation != null)
                {
                    await OwnerBoss.Animation.PlayRollDecelAsync(0.40f, 1.2f, 0.3f, token);
                }

                // 복귀: 플레이어 대면 및 Idle
                OwnerBoss.FacePlayer();
                OwnerBoss.Animation?.PlayIdle();
            }
            finally
            {
                _isActionRunning = false;
                _isBladeSpinning = false;
                if (_bladeVfxObj != null)
                {
                    _bladeVfxObj.transform.DOKill();
                    _bladeVfxObj.SetActive(false);
                    if (_bladeRenderer != null) _bladeRenderer.sprite = null;
                    _bladeVfxObj.transform.localPosition = bladeLocalOffset;
                    _bladeVfxObj.transform.localScale = Vector3.one;
                    _bladeVfxObj.transform.localRotation = Quaternion.identity;
                }
                if (OwnerBoss != null)
                {
                    OwnerBoss.SnapToGroundY();
                    OwnerBoss.Animation?.ResetScale();
                }
            }
        }

        private async UniTaskVoid TriggerBounceSquash(Vector2 squashScale, CancellationToken ct)
        {
            if (OwnerBoss == null || OwnerBoss.Animation == null) return;
            try
            {
                await OwnerBoss.Animation.PlayImpactSquashAndStretchAsync(OwnerBoss.transform, squashScale, 0.03f, 0.14f, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogWarning($"[JunkWheelerRicochet] BounceSquash 연출 예외: {ex.Message}");
            }
        }

        private void GetArenaBounds(out float minX, out float maxX, out float minY, out float maxY)
        {
            minY = _groundY + groundPaddingY;
            maxY = _groundY + 4.2f;
            minX = -7.5f;
            maxX = 7.5f;

            if (OwnerBoss != null)
            {
                // 좌우 경계: 스테이지 최대 카메라 확장 범위(StageWallBounds)를 사용하여 줌인 상태에서도 끝까지 튕기도록 보장
                if (!OwnerBoss.TryGetStageWallBoundsX(out minX, out maxX, cameraPaddingX))
                {
                    OwnerBoss.TryGetCameraBoundsX(out minX, out maxX, cameraPaddingX);
                }

                Camera cam = OwnerBoss.TargetCamera;
                if (cam != null)
                {
                    // 3D 틸트 카메라(X=34도)에서 ViewportToWorldPoint 왜곡을 방지하기 위해 보스 Z 평면 기준 가시 높이를 삼각함수로 산출
                    float zDist = Mathf.Abs(cam.transform.position.z - OwnerBoss.transform.position.z);
                    if (zDist < 0.01f) zDist = 10f;
                    float halfHeight = cam.orthographic
                        ? cam.orthographicSize
                        : Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * zDist;
                    maxY = Mathf.Max(_groundY + 3.0f, _groundY + (halfHeight * 1.6f) - cameraPaddingY);
                }
            }
        }

        private bool CheckPlayerCollision()
        {
            if (OwnerBoss == null) return false;
            var player = OwnerBoss.PlayerTransform;
            if (player == null) return false;

            // 1. 플레이어가 무적 회피(Space 회피 등) 중이면 피격 판정 및 피격 이펙트 완전 배제
            if (IsPlayerInvincible(player.gameObject)) return false;

            Vector2 center = OwnerBoss.transform.position;
            float radius = Mathf.Max(0.1f, bladeHitRadius);

            // 2. HurtboxColliderSync와 연동된 Physics2D Overlap 검사 (플레이어 Hurt 콜라이더 유효성 검증)
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
            bool collided = false;
            GameObject targetPlayerObj = null;

            foreach (var hit in hits)
            {
                if (hit.gameObject == OwnerBoss.gameObject) continue;
                if (hit.CompareTag("Player") || (hit.transform.root != null && hit.transform.root.CompareTag("Player")) || hit.transform == player)
                {
                    collided = true;
                    targetPlayerObj = hit.gameObject;
                    break;
                }
            }

            // 폴백: 콜라이더가 비활성화/누락된 테스트 환경 지원 (단, HurtboxColliderSync로 꺼진 상태면 충돌 아님)
            if (!collided)
            {
                var playerCollider = player.GetComponentInChildren<Collider2D>();
                if (playerCollider != null && !playerCollider.enabled)
                {
                    // 회피 무적이나 사망으로 콜라이더가 비활성화된 상태이면 피격 무시
                    return false;
                }

                // 콜라이더 컴포넌트 자체가 전혀 없는 경우에만 거리 기반 폴백 적용
                if (playerCollider == null)
                {
                    float dist = Vector2.Distance(center, player.position);
                    if (dist <= radius)
                    {
                        collided = true;
                        targetPlayerObj = player.gameObject;
                    }
                }
            }

            if (collided && targetPlayerObj != null)
            {
                // 최종 무적 재검사
                if (IsPlayerInvincible(targetPlayerObj)) return false;

                // 3. 실제 데미지 적용에 성공한 경우에만 피격 로그 및 피격 파티클 스폰
                if (ApplyDamageToPlayer(targetPlayerObj))
                {
                    Debug.Log("<color=red>[JunkWheelerRicochet] 회전 톱날 돌진 ➔ 플레이어 피격 충돌 및 피해 적용!</color>");
                    // [VFX 2] 플레이어 피격 참격 이펙트 스폰
                    Vector3 hitPos = (OwnerBoss.transform.position + player.position) * 0.5f;
                    SpawnVfx(bladeHitVfxPrefab, hitPos);
                    return true;
                }
            }

            return false;
        }

        protected override void OnInterrupted(InterruptReason reason)
        {
            base.OnInterrupted(reason);
            CleanupBladePattern();
        }

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            CleanupBladePattern();
        }

        private void CleanupBladePattern()
        {
            StopTellVisuals();
            _isActionRunning = false;
            _isBladeSpinning = false;
            if (_currentTween != null && _currentTween.IsActive())
            {
                _currentTween.Kill();
                _currentTween = null;
            }
            if (_bladeVfxObj != null)
            {
                _bladeVfxObj.transform.DOKill();
                _bladeVfxObj.SetActive(false);
                if (_bladeRenderer != null) _bladeRenderer.sprite = null;
                _bladeVfxObj.transform.localPosition = bladeLocalOffset;
                _bladeVfxObj.transform.localScale = Vector3.one;
                _bladeVfxObj.transform.localRotation = Quaternion.identity;
            }
            if (OwnerBoss != null)
            {
                OwnerBoss.SnapToGroundY();
                if (OwnerBoss.Animation != null)
                {
                    OwnerBoss.Animation.ResetScale();
                    OwnerBoss.Animation.PlayIdle();
                }
            }
        }

        // ========================================================================
        // 블레이드 시각화 서브시스템
        // ========================================================================
        private void EnsureBladeVfxObject()
        {
            if (_bladeVfxObj != null)
            {
                _bladeVfxObj.transform.localPosition = bladeLocalOffset;
                return;
            }

            Transform existing = transform.Find("BladeVfxRenderer");
            if (existing != null)
            {
                _bladeVfxObj = existing.gameObject;
                _bladeVfxObj.transform.localPosition = bladeLocalOffset;
                _bladeVfxObj.transform.localScale = Vector3.one;
                _bladeVfxObj.transform.localRotation = Quaternion.identity;
            }
            else
            {
                _bladeVfxObj = new GameObject("BladeVfxRenderer");
                _bladeVfxObj.transform.SetParent(transform, false);
                _bladeVfxObj.transform.localPosition = bladeLocalOffset;
                _bladeVfxObj.transform.localScale = Vector3.one;
                _bladeVfxObj.transform.localRotation = Quaternion.identity;
            }

            _bladeRenderer = _bladeVfxObj.GetComponent<SpriteRenderer>();
            if (_bladeRenderer == null)
            {
                _bladeRenderer = _bladeVfxObj.AddComponent<SpriteRenderer>();
            }

            // 소팅 오더 설정: 보스보다 한 레이어 앞
            if (OwnerBoss != null && OwnerBoss.Animation != null && OwnerBoss.Animation.SpriteRenderer != null)
            {
                _bladeRenderer.sortingLayerID = OwnerBoss.Animation.SpriteRenderer.sortingLayerID;
                _bladeRenderer.sortingOrder = OwnerBoss.Animation.SpriteRenderer.sortingOrder + 1;
            }
            else
            {
                _bladeRenderer.sortingOrder = 1;
            }

            _bladeAnimator = _bladeVfxObj.GetComponent<Animator>();
            _bladeVfxObj.SetActive(false);
        }

        private static bool NeedsSpriteReload(Sprite[] arr)
        {
            if (arr == null || arr.Length == 0) return true;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] == null) return true;
            }
            return false;
        }

        private void LoadSpritesIfNeeded()
        {
#if UNITY_EDITOR
            if (NeedsSpriteReload(deploySprites))
            {
                deploySprites = LoadSpritesFromAsset("Assets/Sprites/1Boss_Robot/Blade_Vfx/1Boss_robot_blades_deploy.png");
            }
            if (NeedsSpriteReload(spinSprites))
            {
                spinSprites = LoadSpritesFromAsset("Assets/Sprites/1Boss_Robot/Blade_Vfx/1Boss_robot_blades_spin.png");
            }
            if (NeedsSpriteReload(retractSprites))
            {
                retractSprites = LoadSpritesFromAsset("Assets/Sprites/1Boss_Robot/Blade_Vfx/1Boss_robot_blades_retract.png");
            }
#endif
        }

#if UNITY_EDITOR
        private static Sprite[] LoadSpritesFromAsset(string path)
        {
            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            var list = new System.Collections.Generic.List<Sprite>();
            foreach (var a in assets)
            {
                if (a is Sprite s) list.Add(s);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }
#endif

        private async UniTask PlayBladeDeployAndSpinAsync(CancellationToken token)
        {
            if (_bladeRenderer == null || _bladeVfxObj == null) return;
            LoadSpritesIfNeeded();

            // Step 1: Slide out from wheel + Scale 0 -> 1
            _isBladeSpinning = false;
            _bladeVfxObj.transform.DOKill();
            _bladeVfxObj.transform.localRotation = Quaternion.identity;
            Vector3 hiddenLocalPos = bladeLocalOffset - new Vector3(0f, 0.18f, 0f);
            _bladeVfxObj.transform.localPosition = hiddenLocalPos;
            _bladeVfxObj.transform.localScale = Vector3.zero;

            float deployDuration = 0.24f;
            _ = _bladeVfxObj.transform.DOLocalMove(bladeLocalOffset, deployDuration).SetEase(Ease.OutCubic);
            _ = _bladeVfxObj.transform.DOScale(Vector3.one, deployDuration).SetEase(Ease.OutBack);

            // Step 2: Deploy sprite sequence (4 frames)
            float elapsed = 0f;
            int frameCount = (deploySprites != null && deploySprites.Length > 0) ? deploySprites.Length : 1;
            float frameDelay = deployDuration / frameCount;

            while (elapsed < deployDuration)
            {
                if (token.IsCancellationRequested || _bladeVfxObj == null) return;

                if (deploySprites != null && deploySprites.Length > 0)
                {
                    int idx = Mathf.Clamp(Mathf.FloorToInt(elapsed / frameDelay), 0, deploySprites.Length - 1);
                    if (deploySprites[idx] != null)
                    {
                        _bladeRenderer.sprite = deploySprites[idx];
                    }
                }

                // 전개하는 동안에도 서서히 회전 가속 시작 (-360도 -> -1080도)
                float t = Mathf.Clamp01(elapsed / deployDuration);
                float deployRotSpeed = Mathf.Lerp(-360f, -1080f, t);
                _bladeVfxObj.transform.Rotate(0f, 0f, deployRotSpeed * Time.deltaTime);

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (token.IsCancellationRequested || _bladeVfxObj == null) return;

            _bladeVfxObj.transform.localPosition = bladeLocalOffset;
            _bladeVfxObj.transform.localScale = Vector3.one;

            // Step 3: Deploy finished -> Start spin loop immediately!
            _currentSpinInterval = 0.032f;
            StartSpinLoop();
        }

        private void StartSpinLoop()
        {
            EnsureBladeVfxObject();
            if (_bladeRenderer == null || _bladeVfxObj == null) return;
            LoadSpritesIfNeeded();

            if (!_bladeVfxObj.activeSelf)
            {
                _bladeVfxObj.SetActive(true);
            }

            Sprite initialSprite = GetValidSpinSprite(_spinFrameIndex);
            if (initialSprite != null)
            {
                _bladeRenderer.sprite = initialSprite;
            }

            _isBladeSpinning = true;
        }

        private async UniTask PlayBladeRetractAsync(CancellationToken token)
        {
            if (_bladeRenderer == null || _bladeVfxObj == null) return;
            LoadSpritesIfNeeded();

            // 스핀 루프 정지
            _isBladeSpinning = false;

            // 1단계: 휠 안쪽으로 슬라이드 인 + 스케일 1 -> 0 (InBack 쏙 빨려 들어감 연출)
            _bladeVfxObj.transform.DOKill();
            _bladeVfxObj.transform.localRotation = Quaternion.identity;
            float retractDuration = 0.22f;
            Vector3 hiddenLocalPos = bladeLocalOffset - new Vector3(0f, 0.18f, 0f);
            _ = _bladeVfxObj.transform.DOLocalMove(hiddenLocalPos, retractDuration).SetEase(Ease.InQuad);
            _ = _bladeVfxObj.transform.DOScale(Vector3.zero, retractDuration).SetEase(Ease.InBack);

            // 2단계: 수납 프레임 재생
            if (_bladeAnimator != null && _bladeAnimator.HasState(0, AnimHashRetract))
            {
                _bladeAnimator.Play(AnimHashRetract);
                await UniTask.Delay(TimeSpan.FromSeconds(retractDuration), cancellationToken: token);
            }
            else if (retractSprites != null && retractSprites.Length > 0)
            {
                float frameDelay = retractDuration / retractSprites.Length;
                for (int i = 0; i < retractSprites.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    if (retractSprites[i] != null)
                    {
                        _bladeRenderer.sprite = retractSprites[i];
                    }
                    await UniTask.Delay(TimeSpan.FromSeconds(frameDelay), cancellationToken: token);
                }
            }
            else
            {
                await UniTask.Delay(TimeSpan.FromSeconds(retractDuration), cancellationToken: token);
            }

            // 3단계: 수납 완료 즉시 스프라이트 및 오브젝트 클리어 (감속 모션 중 찌꺼기 방지)
            _bladeRenderer.sprite = null;
            _bladeVfxObj.SetActive(false);
            _bladeVfxObj.transform.localPosition = bladeLocalOffset;
            _bladeVfxObj.transform.localScale = Vector3.one;
            _bladeVfxObj.transform.localRotation = Quaternion.identity;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 회전 톱날 피격 판정 범위 (기획 고정 반경)
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, bladeHitRadius);
        }
#endif
    }
}
