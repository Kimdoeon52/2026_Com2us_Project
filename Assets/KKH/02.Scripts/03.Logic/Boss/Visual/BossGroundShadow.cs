using UnityEngine;
using RealSteel.Boss.Core;

namespace RealSteel.Boss.Visual
{
    /// <summary>
    /// [2.5D(3D 필드) 환경 보스용 바닥 그림자 드라이버 (BossGroundShadow)]
    /// 3D 지형(RSTerrain)과 3D 콜라이더 굴곡을 따라 XZ 평면에 투영되는 RSBlobShadow(RE_AL STEEL/Blob Shadow)를
    /// 보스(BossControllerBase)의 생존 상태, 스프라이트 가시성 및 착지 스쿼시 연출과 연동함.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(RSBlobShadow))]
    public class BossGroundShadow : MonoBehaviour
    {
        [Header("참조 (미지정 시 자동 탐색)")]
        [SerializeField] private BossControllerBase bossController;
        [SerializeField] private BossAnimationController animationController;
        [SerializeField] private SpriteRenderer bossSpriteRenderer;
        [SerializeField] private RSBlobShadow blobShadow;

        [Header("그림자 표시 설정")]
        [SerializeField] private bool castGroundShadow = true;

        [Header("착지 스쿼시 가로폭 연동")]
        [SerializeField] private bool followSquashWidth = true;
        [Range(0f, 1f)] [SerializeField] private float squashWidthInfluence = 0.5f;

        private float _baseWidthScale = 1.25f;
        private bool _hasBaseWidthScale;

        private void Reset()
        {
            AutoBindReferences();
            ApplyDefaultBossPreset();
        }

        private void OnValidate()
        {
            AutoBindReferences();
        }

        private void Awake()
        {
            AutoBindReferences();
            if (blobShadow != null)
            {
                _baseWidthScale = blobShadow.widthScale;
                _hasBaseWidthScale = true;
            }
        }

        private void OnEnable()
        {
            if (blobShadow != null && _hasBaseWidthScale)
            {
                blobShadow.widthScale = _baseWidthScale;
            }
        }

        private void OnDisable()
        {
            if (blobShadow != null)
            {
                if (_hasBaseWidthScale)
                {
                    blobShadow.widthScale = _baseWidthScale;
                }
                blobShadow.enabled = false;
            }
        }

        private void AutoBindReferences()
        {
            if (bossController == null)
            {
                bossController = GetComponent<BossControllerBase>();
            }
            if (animationController == null)
            {
                animationController = GetComponent<BossAnimationController>();
            }
            if (bossSpriteRenderer == null)
            {
                bossSpriteRenderer = animationController != null && animationController.SpriteRenderer != null
                    ? animationController.SpriteRenderer
                    : GetComponent<SpriteRenderer>();
            }
            if (blobShadow == null)
            {
                blobShadow = GetComponent<RSBlobShadow>();
            }
        }

        private void ApplyDefaultBossPreset()
        {
            if (blobShadow == null) return;

            blobShadow.radius = 0.72f;
            blobShadow.widthScale = 1.25f;
            blobShadow.strength = 0.62f;
            blobShadow.color = new Color(0.12f, 0.10f, 0.20f, 1f);
            blobShadow.softness = 0.45f;
            blobShadow.bands = 3;
            blobShadow.pixelSnap = true;
            blobShadow.pixelsPerMeter = 32f;
            blobShadow.offset = Vector2.zero;
            blobShadow.fadeHeight = 3.8f;
            blobShadow.minScale = 0.45f;
            blobShadow.minStrength = 0.35f;
            blobShadow.resolution = 7;
            blobShadow.maxStep = 0.45f;
            blobShadow.probeUp = 0.8f;
            blobShadow.probeDown = 8f;
            blobShadow.lift = 0.02f;
        }

        private void LateUpdate()
        {
            if (blobShadow == null) return;

            bool isDefeated = bossController != null && bossController.Snapshot != null && !bossController.Snapshot.IsAlive;
            bool isHidden = bossSpriteRenderer != null
                && (!bossSpriteRenderer.enabled || !bossSpriteRenderer.gameObject.activeInHierarchy);

            bool shouldShow = castGroundShadow && !isDefeated && !isHidden;
            if (blobShadow.enabled != shouldShow)
            {
                blobShadow.enabled = shouldShow;
            }
            if (!shouldShow) return;

            if (!_hasBaseWidthScale)
            {
                _baseWidthScale = blobShadow.widthScale;
                _hasBaseWidthScale = true;
            }

            if (followSquashWidth && animationController != null && animationController.BaseScaleX > 0.0001f)
            {
                float squashRatioX = Mathf.Abs(transform.localScale.x) / animationController.BaseScaleX;
                float widthMul = Mathf.Lerp(1f, squashRatioX, squashWidthInfluence);
                blobShadow.widthScale = Mathf.Clamp(_baseWidthScale * widthMul, 0.5f, 2f);
            }
            else
            {
                blobShadow.widthScale = _baseWidthScale;
            }
        }
    }
}
