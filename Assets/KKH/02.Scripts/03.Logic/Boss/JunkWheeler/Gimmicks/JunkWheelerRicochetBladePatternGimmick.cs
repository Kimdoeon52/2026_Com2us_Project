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
        [SerializeField] private float cameraPaddingX = 0.85f;
        [Tooltip("카메라 상단 천장 경계 패딩 (m)")]
        [SerializeField] private float cameraPaddingY = 0.85f;
        [Tooltip("지면 착지 높이 오프셋")]
        [SerializeField] private float groundPaddingY = 0.1f;
        [Tooltip("사선 발사 수직 각도 (도)")]
        [SerializeField] private float initialAngleDeg = 40.0f;

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
        [SerializeField] private Vector3 bladeLocalOffset = new Vector3(0f, 0.80f, -0.05f);

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

        private static readonly int AnimHashDeploy = Animator.StringToHash("Boss_Blades_Deploy");
        private static readonly int AnimHashSpin = Animator.StringToHash("Boss_Blades_Spin");
        private static readonly int AnimHashRetract = Animator.StringToHash("Boss_Blades_Retract");

        private void Awake()
        {
            EnsureBladeVfxObject();
            LoadSpritesIfNeeded();
        }

        protected override void OnPreExecute()
        {
            base.OnPreExecute();
            if (OwnerBoss != null)
            {
                _groundY = OwnerBoss.transform.position.y;
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
            float tellDuration = PatternData != null ? PatternData.tellDuration : 1.8f;
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

            // 1단계: 톱날 전개 애니메이션 가동
            PlayBladeDeployAsync(ct).Forget();

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

                    // 1) 지수 가속 진동 (등속 ➔ 로그/지수 가속)
                    float currentShakeStrength = Mathf.Lerp(minShakeStrength, maxShakeStrength, nonLinearT);
                    float currentVibrato = Mathf.Lerp(minShakeVibrato, maxShakeVibrato, nonLinearT);
                    float shakePhase = elapsed * currentVibrato * Mathf.PI * 2f;
                    float offsetX = Mathf.Sin(shakePhase) * currentShakeStrength;
                    float offsetY = Mathf.Cos(shakePhase * 1.33f) * (currentShakeStrength * 0.4f);

                    if (OwnerBoss != null)
                    {
                        OwnerBoss.transform.position = _baseWorldPosBeforeTell + new Vector3(offsetX, offsetY, 0f);
                    }

                    // 2) 비선형 점멸 주기
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

                // 전조 완료: 톱날 초고속 Spin 회전 상태 돌입
                PlayBladeSpin();
            }
            finally
            {
                if (sr != null) sr.color = _originalColor;
                if (OwnerBoss != null && _baseWorldPosBeforeTell != Vector3.zero)
                {
                    OwnerBoss.transform.position = _baseWorldPosBeforeTell;
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
                PlayBladeSpin();

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

                // 3단계: 당구 3쿠션 바운스 루프
                int bounceCount = 0;
                float cooldownDamage = 0f;

                while (bounceCount < maxBounceCount)
                {
                    token.ThrowIfCancellationRequested();

                    float dt = Time.deltaTime;
                    Vector3 currentPos = OwnerBoss.transform.position;
                    Vector3 nextPos = currentPos + (Vector3)(moveDir * (rushSpeed * dt));

                    bool bounced = false;
                    Vector2 impactNormal = Vector2.zero;

                    // 좌/우 벽면 충돌 체크
                    if (nextPos.x <= minX)
                    {
                        nextPos.x = minX;
                        moveDir.x = Mathf.Abs(moveDir.x);
                        bounced = true;
                        impactNormal = Vector2.right;
                    }
                    else if (nextPos.x >= maxX)
                    {
                        nextPos.x = maxX;
                        moveDir.x = -Mathf.Abs(moveDir.x);
                        bounced = true;
                        impactNormal = Vector2.left;
                    }

                    // 상단 천장 / 하단 지면 충돌 체크
                    if (nextPos.y >= maxY)
                    {
                        nextPos.y = maxY;
                        moveDir.y = -Mathf.Abs(moveDir.y);
                        bounced = true;
                        impactNormal = Vector2.down;
                    }
                    else if (nextPos.y <= minY)
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
                        Debug.Log($"<color=yellow>[JunkWheelerRicochet] 당구 바운스 #{bounceCount}/{maxBounceCount}! 법선: {impactNormal}</color>");

                        // 이동 방향에 맞춰 스프라이트 반전
                        OwnerBoss.Animation?.FaceDirection(moveDir.x);

                        // 벽 충돌 비대칭 임팩트 스쿼시 (충돌 법선 축으로 찌그러짐)
                        Vector2 squashScale = Mathf.Abs(impactNormal.x) > 0.5f 
                            ? new Vector2(0.55f, 1.45f) // 좌우 벽 충돌: X 압착, Y 팽창
                            : new Vector2(1.45f, 0.55f); // 천장/바닥 충돌: Y 압착, X 팽창

                        TriggerBounceSquash(squashScale, token).Forget();
                    }

                    // 플레이어 충돌 검사
                    cooldownDamage -= dt;
                    if (cooldownDamage <= 0f)
                    {
                        if (CheckPlayerCollision())
                        {
                            cooldownDamage = 0.5f;
                        }
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                }

                // 4단계: 3차 바운스 후 지면 착지 하강
                Debug.Log($"<color=cyan>[JunkWheelerRicochet] 3쿠션 완료! 원지면({_groundY:F2})으로 착지 하강</color>");
                OwnerBoss.Animation?.PlaySlamFall();

                Vector3 beforeLandPos = OwnerBoss.transform.position;
                Vector3 landTargetPos = new Vector3(OwnerBoss.ClampToCameraX(beforeLandPos.x, 0.7f), _groundY, beforeLandPos.z);
                
                _currentTween = OwnerBoss.transform.DOMove(landTargetPos, 0.28f).SetEase(Ease.InQuad);
                await _currentTween.ToUniTask(cancellationToken: token);

                // 5단계: 착지 임팩트 펀치 & 톱날 수납 & 롤 감속
                OwnerBoss.Animation?.PlaySlamLand();
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
                if (_bladeVfxObj != null)
                {
                    _bladeVfxObj.SetActive(false);
                }
                OwnerBoss?.Animation?.ResetScale();
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
            maxY = _groundY + 4.5f;
            minX = -7.5f;
            maxX = 7.5f;

            Camera cam = OwnerBoss != null ? OwnerBoss.TargetCamera : Camera.main;
            if (cam != null && OwnerBoss != null)
            {
                float zDist = Mathf.Abs(cam.transform.position.z - OwnerBoss.transform.position.z);
                Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, zDist));
                Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, zDist));

                minX = Mathf.Min(bl.x, tr.x) + cameraPaddingX;
                maxX = Mathf.Max(bl.x, tr.x) - cameraPaddingX;
                maxY = Mathf.Max(bl.y, tr.y) - cameraPaddingY;
            }
        }

        private bool CheckPlayerCollision()
        {
            if (OwnerBoss == null) return false;
            var player = OwnerBoss.PlayerTransform;
            if (player == null) return false;

            float dist = Vector2.Distance(OwnerBoss.transform.position, player.position);
            if (dist <= 1.35f)
            {
                Debug.Log("<color=red>[JunkWheelerRicochet] 회전 톱날 돌진 ➔ 플레이어 피격 충돌!</color>");
                ApplyDamageToPlayer(player.gameObject);
                return true;
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
            if (_currentTween != null && _currentTween.IsActive())
            {
                _currentTween.Kill();
                _currentTween = null;
            }
            if (_bladeVfxObj != null)
            {
                _bladeVfxObj.SetActive(false);
            }
            if (OwnerBoss != null && OwnerBoss.Animation != null)
            {
                OwnerBoss.Animation.ResetScale();
                OwnerBoss.Animation.PlayIdle();
            }
        }

        // ========================================================================
        // 블레이드 시각화 서브시스템
        // ========================================================================
        private void EnsureBladeVfxObject()
        {
            if (_bladeVfxObj != null) return;

            Transform existing = transform.Find("BladeVfxRenderer");
            if (existing != null)
            {
                _bladeVfxObj = existing.gameObject;
            }
            else
            {
                _bladeVfxObj = new GameObject("BladeVfxRenderer");
                _bladeVfxObj.transform.SetParent(transform, false);
                _bladeVfxObj.transform.localPosition = bladeLocalOffset;
                _bladeVfxObj.transform.localScale = Vector3.one;
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

        private void LoadSpritesIfNeeded()
        {
#if UNITY_EDITOR
            if (deploySprites == null || deploySprites.Length == 0)
            {
                deploySprites = LoadSpritesFromAsset("Assets/Sprites/1Boss_Robot/Blade_Vfx/1Boss_robot_blades_deploy.png");
            }
            if (spinSprites == null || spinSprites.Length == 0)
            {
                spinSprites = LoadSpritesFromAsset("Assets/Sprites/1Boss_Robot/Blade_Vfx/1Boss_robot_blades_spin.png");
            }
            if (retractSprites == null || retractSprites.Length == 0)
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
            return list.ToArray();
        }
#endif

        private async UniTask PlayBladeDeployAsync(CancellationToken token)
        {
            if (_bladeRenderer == null) return;

            if (_bladeAnimator != null && _bladeAnimator.HasState(0, AnimHashDeploy))
            {
                _bladeAnimator.Play(AnimHashDeploy);
                await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: token);
                return;
            }

            if (deploySprites != null && deploySprites.Length > 0)
            {
                float frameDelay = 0.25f / deploySprites.Length;
                for (int i = 0; i < deploySprites.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    _bladeRenderer.sprite = deploySprites[i];
                    await UniTask.Delay(TimeSpan.FromSeconds(frameDelay), cancellationToken: token);
                }
            }
        }

        private void PlayBladeSpin()
        {
            if (_bladeRenderer == null) return;

            if (_bladeAnimator != null && _bladeAnimator.HasState(0, AnimHashSpin))
            {
                _bladeAnimator.Play(AnimHashSpin);
                return;
            }

            // 폴백: 스프라이트 스핀 루프 가동
            RunSpinLoopAsync().Forget();
        }

        private async UniTaskVoid RunSpinLoopAsync()
        {
            if (spinSprites == null || spinSprites.Length == 0) return;
            int idx = 0;
            while (_isActionRunning && _bladeVfxObj != null && _bladeVfxObj.activeInHierarchy)
            {
                if (_bladeRenderer != null)
                {
                    _bladeRenderer.sprite = spinSprites[idx % spinSprites.Length];
                }
                idx++;
                await UniTask.Delay(TimeSpan.FromSeconds(0.045f));
            }
        }

        private async UniTask PlayBladeRetractAsync(CancellationToken token)
        {
            if (_bladeRenderer == null) return;

            if (_bladeAnimator != null && _bladeAnimator.HasState(0, AnimHashRetract))
            {
                _bladeAnimator.Play(AnimHashRetract);
                await UniTask.Delay(TimeSpan.FromSeconds(0.20f), cancellationToken: token);
                return;
            }

            if (retractSprites != null && retractSprites.Length > 0)
            {
                float frameDelay = 0.20f / retractSprites.Length;
                for (int i = 0; i < retractSprites.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    _bladeRenderer.sprite = retractSprites[i];
                    await UniTask.Delay(TimeSpan.FromSeconds(frameDelay), cancellationToken: token);
                }
            }
        }
    }
}

