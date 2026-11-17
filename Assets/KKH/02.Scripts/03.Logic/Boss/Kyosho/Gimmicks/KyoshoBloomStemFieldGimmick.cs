using System;
using UnityEngine;
using TMPro;
using RealSteel.Boss.Core;
using RealSteel.Dialogue;
using RealSteel.NanoBoss;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [전장 기물: 개화 줄기 (KyoshoBloomStemFieldGimmick)]
    /// 1페이즈 개화 패턴 시 6개 풀에서 활성화되며, 2~3박 성장 중 타격 시 즉시 절단 가능(D 2타 / 스킬 1타).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class KyoshoBloomStemFieldGimmick : BossFieldGimmickBase, IPlayerHitTarget
    {
        public override string FieldGimmickID => "GIMMICK_KYOSHO_BLOOM_STEM";

        [SerializeField] private int maxHp = 15;

        private RSNanoLimb _limb;
        private Transform _tipJoint;
        private RSNanoSwarm _swarm;
        private KyoshoBossController _kyosho;
        private BoxCollider2D _collider;
        private TextMeshPro _iconText;
        private SpriteRenderer _seedMarker;
        private GameObject _progressBgGo;
        private Transform _progressFillTransform;
        private SpriteRenderer _progressFillSr;
        private GameObject _attackPreviewGo;
        private KyoshoBloomType _previewBuiltForType = (KyoshoBloomType)(-1);

        private int _currentHp;
        private bool _isActiveStem;
        private bool _isCut;
        private float _floorY;
        private KyoshoBloomType _bloomType;

        public bool IsAliveAndUncut => _isActiveStem && !_isCut;
        public KyoshoBloomType BloomType => _bloomType;
        public Vector3 TipPosition => _tipJoint != null ? _tipJoint.position : transform.position + Vector3.up * 2f;
        public bool IsHittable => IsAliveAndUncut && IsInteractable;

        public void BindPoolLimb(RSNanoLimb limb, RSNanoSwarm swarm, KyoshoBossController kyosho = null)
        {
            _limb = limb;
            _swarm = swarm;
            if (kyosho != null) _kyosho = kyosho;
            if (_limb != null)
            {
                _limb.type = LimbType.Chain;
                _limb.radius = 0.24f;
                _limb.grow = 0f;
                _limb.amount = 0f;
                _limb.smooth = true;
                if (_limb.transform.childCount > 0)
                {
                    _tipJoint = _limb.transform.GetChild(0);
                }
                else
                {
                    var tipGo = new GameObject("TipJoint");
                    tipGo.transform.SetParent(_limb.transform, false);
                    tipGo.transform.localPosition = new Vector3(0f, 2.1f, 0f);
                    _tipJoint = tipGo.transform;
                }
            }
            EnsureVisuals();
            DeactivateStem();
        }

        public void ActivateSeedPreview(Vector3 floorPos, KyoshoBloomType bloomType, KyoshoBossController kyosho = null)
        {
            if (kyosho != null) _kyosho = kyosho;
            EnsureVisuals();
            _floorY = floorPos.y;
            _bloomType = bloomType;
            _currentHp = maxHp;
            _isActiveStem = true;
            _isCut = false;
            IsInteractable = true;

            transform.position = floorPos;
            if (_limb != null)
            {
                _limb.transform.position = floorPos;
                _limb.tint = GetTypeColor(bloomType);
                _limb.glow = 0.5f;
                _limb.grow = 0f;
                _limb.amount = 0f;
            }
            if (_tipJoint != null)
            {
                _tipJoint.localPosition = new Vector3(0f, 2.1f, 0f);
            }

            if (_collider != null) _collider.enabled = false;
            if (_seedMarker != null)
            {
                _seedMarker.enabled = true;
                _seedMarker.color = GetTypeColor(bloomType);
            }
            if (_progressBgGo != null)
            {
                _progressBgGo.SetActive(true);
                SetGrowthBarVisual(0f, GetTypeColor(bloomType));
            }
            RebuildAttackPreviewForType(bloomType);
            if (_attackPreviewGo != null) _attackPreviewGo.SetActive(false);

            if (_iconText != null)
            {
                _iconText.gameObject.SetActive(true);
                _iconText.color = GetTypeColor(bloomType);
                _iconText.text = bloomType switch
                {
                    KyoshoBloomType.Note => "♪ 음표\n[CUT!]",
                    KyoshoBloomType.Trumpet => "▶ 나팔\n[CUT!]",
                    KyoshoBloomType.Cymbal => "◎ 심벌\n[CUT!]",
                    _ => "♪\n[CUT!]"
                };
                _iconText.transform.localPosition = new Vector3(0f, 0.72f, -0.1f);
            }
        }

        public void SetGrowthProgress(float progress01, float swayX = 0f, Vector3? playerPos = null)
        {
            if (!_isActiveStem || _isCut) return;
            float p = Mathf.Clamp01(progress01);

            if (_collider != null && !_collider.enabled && p > 0.05f)
            {
                _collider.enabled = true;
            }

            if (_limb != null)
            {
                _limb.amount = Mathf.Lerp(0.25f, 1f, p);
                _limb.grow = p;
                _limb.glow = Mathf.Lerp(0.4f, 1.4f, p);
            }
            if (_tipJoint != null)
            {
                _tipJoint.localPosition = new Vector3(swayX, Mathf.Lerp(0.3f, 2.1f, p), 0f);
            }
            if (_iconText != null)
            {
                _iconText.transform.localPosition = new Vector3(swayX, Mathf.Lerp(0.72f, 2.65f, p), -0.1f);
            }
            SetGrowthBarVisual(p, GetTypeColor(_bloomType));

            if (_attackPreviewGo != null)
            {
                bool showPreview = p >= 0.45f;
                if (_attackPreviewGo.activeSelf != showPreview) _attackPreviewGo.SetActive(showPreview);
                if (showPreview && _bloomType == KyoshoBloomType.Trumpet)
                {
                    float dirX = (playerPos.HasValue && playerPos.Value.x < transform.position.x) ? -1f : 1f;
                    _attackPreviewGo.transform.position = new Vector3(transform.position.x + dirX * 1.6f, _floorY + 0.85f, -0.04f);
                }
            }
        }

        public void DeactivateStem()
        {
            _isActiveStem = false;
            IsInteractable = false;
            if (_collider != null) _collider.enabled = false;
            if (_limb != null)
            {
                _limb.grow = 0f;
                _limb.amount = 0f;
            }
            if (_seedMarker != null) _seedMarker.enabled = false;
            if (_progressBgGo != null) _progressBgGo.SetActive(false);
            if (_attackPreviewGo != null) _attackPreviewGo.SetActive(false);
            if (_iconText != null) _iconText.gameObject.SetActive(false);
        }

        public void TakePlayerHit(float damage, Vector2 point)
        {
            if (!IsHittable) return;
            float atkScale = 1f;
            var hub = CombatDataHub.Instance;
            if (hub != null && hub.PlayerSnapshot != null && hub.PlayerSnapshot.totalAttackPower > 0)
            {
                atkScale = Mathf.Clamp(hub.PlayerSnapshot.totalAttackPower / 220f, 1f, 1.35f);
            }
            int dmg = Mathf.Max(1, Mathf.RoundToInt(damage * atkScale));
            _currentHp -= dmg;
            if (_limb != null) _limb.glow = 2.8f;

            if (_currentHp <= 0)
            {
                SeverStem();
            }
        }

        protected override bool CanInteract(GameObject actor) => IsHittable && actor != null;
        protected override void ExecuteGimmickEffect(GameObject actor) => SeverStem();

        private void SeverStem()
        {
            if (_isCut) return;
            _isCut = true;
            IsInteractable = false;
            if (_collider != null) _collider.enabled = false;

            if (_swarm != null)
            {
                _swarm.Hit(transform.position + Vector3.up * 0.9f, 1.8f, new Vector3(0f, 3.2f, 0f), 0.7f, 1 << RSNanoSwarm.G_Rig);
            }
            if (_limb != null)
            {
                _limb.grow = 0f;
                _limb.amount = 0f;
            }
            if (_seedMarker != null) _seedMarker.enabled = false;
            if (_progressBgGo != null) _progressBgGo.SetActive(false);
            if (_attackPreviewGo != null) _attackPreviewGo.SetActive(false);
            if (_iconText != null) _iconText.gameObject.SetActive(false);

            KyoshoNanobotAttackHelper.SpawnPulseRingEffect(
                transform.position + Vector3.up * 0.85f, 0.20f, 1.15f, GetTypeColor(_bloomType), 0.22f);
            KyoshoSfx.PlayCue(KyoshoSfx.Data?.bloomStemCut, "bloom_cut", transform.position);

            var bark = _kyosho != null ? _kyosho.GetBarkPlayer() : FindAnyObjectByType<BattleBarkPlayer>();
            bark?.NotifyBossPartBroken(KyoshoConstants.BarkBloomStem);
        }

        private void EnsureVisuals()
        {
            if (_collider == null) _collider = GetComponent<BoxCollider2D>();
            _collider.isTrigger = true;
            _collider.size = new Vector2(1.25f, 2.2f);
            _collider.offset = new Vector2(0f, 1.1f);

            if (_seedMarker == null)
            {
                var markerGo = new GameObject("SeedMarker");
                markerGo.transform.SetParent(transform, false);
                markerGo.transform.localPosition = new Vector3(0f, 0.14f, -0.05f);
                markerGo.transform.localScale = new Vector3(0.55f, 0.22f, 1f);
                _seedMarker = markerGo.AddComponent<SpriteRenderer>();
                _seedMarker.sprite = KyoshoNanobotAttackHelper.GetCircleSprite();
                _seedMarker.sortingOrder = 15;
                _seedMarker.enabled = false;
            }

            if (_iconText == null)
            {
                var txtGo = new GameObject("BloomTypeGlyph");
                txtGo.transform.SetParent(transform, false);
                _iconText = txtGo.AddComponent<TextMeshPro>();
                _iconText.alignment = TextAlignmentOptions.Center;
                _iconText.fontSize = 3.8f;
                _iconText.gameObject.SetActive(false);
            }

            if (_progressBgGo == null)
            {
                _progressBgGo = new GameObject("GrowthBarBg");
                _progressBgGo.transform.SetParent(transform, false);
                _progressBgGo.transform.localPosition = new Vector3(0f, 0.36f, -0.08f);

                var bg = new GameObject("Bg");
                bg.transform.SetParent(_progressBgGo.transform, false);
                bg.transform.localScale = new Vector3(0.95f, 0.13f, 1f);
                var bgSr = bg.AddComponent<SpriteRenderer>();
                bgSr.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
                bgSr.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);
                bgSr.sortingOrder = 16;

                var fill = new GameObject("Fill");
                fill.transform.SetParent(_progressBgGo.transform, false);
                fill.transform.localPosition = new Vector3(-0.45f, 0f, -0.01f);
                fill.transform.localScale = new Vector3(0.01f, 0.08f, 1f);
                _progressFillTransform = fill.transform;
                _progressFillSr = fill.AddComponent<SpriteRenderer>();
                _progressFillSr.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
                _progressFillSr.color = Color.white;
                _progressFillSr.sortingOrder = 17;

                _progressBgGo.SetActive(false);
            }
        }

        private void SetGrowthBarVisual(float progress01, Color baseCol)
        {
            if (_progressFillTransform == null) return;
            float w = Mathf.Lerp(0.02f, 0.90f, progress01);
            _progressFillTransform.localScale = new Vector3(w, 0.08f, 1f);
            _progressFillTransform.localPosition = new Vector3(-0.45f + w * 0.5f, 0f, -0.01f);
            if (_progressFillSr != null)
            {
                float urgent = Mathf.Clamp01((progress01 - 0.55f) / 0.45f);
                _progressFillSr.color = Color.Lerp(baseCol, new Color(1.00f, 0.24f, 0.24f, 1f), urgent);
            }
        }

        private void RebuildAttackPreviewForType(KyoshoBloomType bloomType)
        {
            if (_attackPreviewGo != null && _previewBuiltForType == bloomType) return;
            if (_attackPreviewGo != null) Destroy(_attackPreviewGo);

            _previewBuiltForType = bloomType;
            Color col = GetTypeColor(bloomType);
            if (bloomType == KyoshoBloomType.Trumpet)
            {
                _attackPreviewGo = KyoshoNanobotAttackHelper.SpawnBorderedZoneHint(
                    "TrumpetBeamPreview",
                    transform.position + new Vector3(1.6f, 0.85f, -0.04f),
                    3.2f,
                    0.70f,
                    col,
                    0.10f,
                    0.65f,
                    0.04f,
                    "나팔 빔 위험!",
                    0f,
                    2.6f,
                    14);
                _attackPreviewGo.transform.SetParent(transform, true);
            }
            else if (bloomType == KyoshoBloomType.Cymbal)
            {
                _attackPreviewGo = KyoshoNanobotAttackHelper.SpawnRingHint(
                    "CymbalRingPreview",
                    transform.position + Vector3.up * 0.9f,
                    1.55f,
                    col,
                    0.10f,
                    0.68f,
                    14,
                    "충격파 범위",
                    2.6f);
                _attackPreviewGo.transform.SetParent(transform, true);
            }
            else
            {
                _attackPreviewGo = KyoshoNanobotAttackHelper.SpawnRingHint(
                    "NoteBurstPreview",
                    transform.position + Vector3.up * 1.4f,
                    0.55f,
                    col,
                    0.14f,
                    0.72f,
                    14,
                    "3연사 예고",
                    2.5f);
                _attackPreviewGo.transform.SetParent(transform, true);
            }
        }

        private static Color GetTypeColor(KyoshoBloomType t)
        {
            return t switch
            {
                KyoshoBloomType.Note => new Color(0.55f, 0.90f, 1.00f, 1f),
                KyoshoBloomType.Trumpet => new Color(1.00f, 0.72f, 0.25f, 1f),
                KyoshoBloomType.Cymbal => new Color(1.00f, 0.42f, 0.75f, 1f),
                _ => Color.white
            };
        }
    }
}
