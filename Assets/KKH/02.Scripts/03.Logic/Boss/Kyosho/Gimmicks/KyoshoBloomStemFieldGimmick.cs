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

        [SerializeField] private int maxHp = 20;

        private RSNanoLimb _limb;
        private Transform _tipJoint;
        private RSNanoSwarm _swarm;
        private BoxCollider2D _collider;
        private TextMeshPro _iconText;
        private SpriteRenderer _seedMarker;

        private int _currentHp;
        private bool _isActiveStem;
        private bool _isCut;
        private float _floorY;
        private KyoshoBloomType _bloomType;

        public bool IsAliveAndUncut => _isActiveStem && !_isCut;
        public KyoshoBloomType BloomType => _bloomType;
        public Vector3 TipPosition => _tipJoint != null ? _tipJoint.position : transform.position + Vector3.up * 2f;
        public bool IsHittable => IsAliveAndUncut && IsInteractable;

        public void BindPoolLimb(RSNanoLimb limb, RSNanoSwarm swarm)
        {
            _limb = limb;
            _swarm = swarm;
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

        public void ActivateSeedPreview(Vector3 floorPos, KyoshoBloomType bloomType)
        {
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
            if (_iconText != null)
            {
                _iconText.gameObject.SetActive(true);
                _iconText.color = GetTypeColor(bloomType);
                _iconText.text = bloomType switch
                {
                    KyoshoBloomType.Note => "♩",
                    KyoshoBloomType.Trumpet => "🎺",
                    KyoshoBloomType.Cymbal => "◎",
                    _ => "♩"
                };
                _iconText.transform.localPosition = new Vector3(0f, 0.55f, -0.1f);
            }
        }

        public void SetGrowthProgress(float progress01, float swayX = 0f)
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
                _iconText.transform.localPosition = new Vector3(swayX, Mathf.Lerp(0.6f, 2.55f, p), -0.1f);
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
            if (_iconText != null) _iconText.gameObject.SetActive(false);
        }

        public void TakePlayerHit(float damage, Vector2 point)
        {
            if (!IsHittable) return;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(damage));
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
            if (_iconText != null) _iconText.gameObject.SetActive(false);

            KyoshoSfx.PlayCue(KyoshoSfx.Data?.bloomStemCut, "bloom_cut", transform.position);

            var bark = FindAnyObjectByType<BattleBarkPlayer>();
            bark?.NotifyBossPartBroken(KyoshoConstants.BarkBloomStem);

            Debug.Log($"<color=green>[KyoshoBloomStem] 개화 줄기({ _bloomType }) 절단 성공!</color>");
        }

        private void EnsureVisuals()
        {
            if (_collider == null) _collider = GetComponent<BoxCollider2D>();
            _collider.isTrigger = true;
            _collider.size = new Vector2(0.95f, 2.0f);
            _collider.offset = new Vector2(0f, 1.0f);

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
                _iconText.fontSize = 4.2f;
                _iconText.gameObject.SetActive(false);
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
