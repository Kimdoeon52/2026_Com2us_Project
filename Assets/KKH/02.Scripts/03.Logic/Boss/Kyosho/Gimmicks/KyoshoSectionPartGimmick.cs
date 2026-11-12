using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RealSteel.Boss.Core;
using RealSteel.Dialogue;
using RealSteel.NanoBoss;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [2페이즈 오케스트라 악기 섹션 기믹 (KyoshoSectionPartGimmick)]
    /// 현악(좌), 타악(우), 금관(중앙 상단) 각 섹션의 독립 HP(보스 MaxHP의 8%), 피격 콜라이더(IPlayerHitTarget),
    /// RSNanoLimb(Sprite) 시각 표현, 파괴 시 모래 낙하 및 대사 바크 연동을 담당함.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class KyoshoSectionPartGimmick : BossPartGimmickBase, IPlayerHitTarget
    {
        [SerializeField] private KyoshoSectionType sectionType;
        [SerializeField] private int allocatedBotCount = 3200;

        private RSNanoLimb _limb;
        private RSNanoSwarm _swarm;
        private KyoshoBossController _kyosho;
        private BoxCollider2D _collider;
        private Action<KyoshoSectionPartGimmick> _onBrokenCallback;
        private Action _onChangedCallback;

        private float _flashTimer;
        private Vector3 _baseWorldPos;
        private Image _worldHpFill;
        private GameObject _worldHpRoot;
        private TextMeshPro _labelText;

        public KyoshoSectionType SectionType => sectionType;
        public int AllocatedBotCount => allocatedBotCount;
        public int CurrentHp => CurrentDurability;
        public int MaxHp => MaxDurability;
        public bool HasEverSpawned { get; private set; }
        public bool TemporarilyGathered { get; private set; }
        public Vector3 AnchorWorldPosition => _baseWorldPos;

        public bool IsHittable => isActiveAndEnabled && HasEverSpawned && !IsBroken && !TemporarilyGathered;

        public void Configure(
            KyoshoBossController kyosho,
            KyoshoSectionType type,
            RSNanoLimb limb,
            RSNanoSwarm swarm,
            int maxHp,
            Vector3 worldPos,
            Action<KyoshoSectionPartGimmick> onBroken,
            Action onChanged)
        {
            _kyosho = kyosho;
            sectionType = type;
            _limb = limb;
            _swarm = swarm;
            _baseWorldPos = worldPos;
            _onBrokenCallback = onBroken;
            _onChangedCallback = onChanged;

            gimmickID = type switch
            {
                KyoshoSectionType.Strings => KyoshoConstants.SectionStringsID,
                KyoshoSectionType.Percussion => KyoshoConstants.SectionPercussionID,
                KyoshoSectionType.Brass => KyoshoConstants.SectionBrassID,
                _ => KyoshoConstants.SectionStringsID
            };
            gimmickName = type switch
            {
                KyoshoSectionType.Strings => "현악 섹션",
                KyoshoSectionType.Percussion => "타악 섹션",
                KyoshoSectionType.Brass => "금관 섹션",
                _ => "악기 섹션"
            };
            maxDurability = Mathf.Max(1, maxHp);
            targetSkillIDToSeal = "";

            EnsureColliderAndVisuals();
            transform.position = worldPos;
            if (_limb != null)
            {
                _limb.transform.position = worldPos;
                _limb.amount = 0f;
            }

            HasEverSpawned = false;
            TemporarilyGathered = false;
            SetActiveVisuals(false);
        }

        public void SpawnSection(int maxHp, Vector3 worldPos)
        {
            maxDurability = Mathf.Max(1, maxHp);
            CurrentDurability = maxDurability;
            _baseWorldPos = worldPos;
            transform.position = worldPos;
            HasEverSpawned = true;
            TemporarilyGathered = false;

            if (_limb != null)
            {
                _limb.transform.position = worldPos;
                _limb.tint = GetThemeColor();
                _limb.glow = 0.65f;
                _limb.amount = 1f;
            }

            SetActiveVisuals(true);
            UpdateHpVisual();
            _onChangedCallback?.Invoke();
        }

        public void RestoreSection(int maxHp)
        {
            maxDurability = Mathf.Max(1, maxHp);
            CurrentDurability = maxDurability;
            TemporarilyGathered = false;
            HasEverSpawned = true;

            if (OwnerBoss?.Snapshot != null && OwnerBoss.Snapshot.gimmickStates.TryGetValue(GimmickID, out var state))
            {
                state.maxDurability = maxDurability;
                state.currentDurability = maxDurability;
            }

            if (_limb != null)
            {
                _limb.transform.position = _baseWorldPos;
                _limb.tint = GetThemeColor();
                _limb.glow = 0.8f;
                _limb.amount = 1f;
            }

            SetActiveVisuals(true);
            UpdateHpVisual();
            _onChangedCallback?.Invoke();
        }

        public void SetTemporarilyGathered(bool gathered)
        {
            TemporarilyGathered = gathered;
            if (!HasEverSpawned || IsBroken)
            {
                SetActiveVisuals(false);
                return;
            }

            if (gathered)
            {
                if (_limb != null) _limb.amount = 0f;
                SetActiveVisuals(false);
            }
            else
            {
                if (_limb != null)
                {
                    _limb.transform.position = _baseWorldPos;
                    _limb.amount = 1f;
                }
                SetActiveVisuals(true);
            }
        }

        public void SetWorldPosition(Vector3 pos)
        {
            transform.position = pos;
            if (_limb != null) _limb.transform.position = pos;
        }

        public void ResetToBasePosition()
        {
            SetWorldPosition(_baseWorldPos);
        }

        public void TakePlayerHit(float damage, Vector2 point)
        {
            if (!IsHittable) return;

            // 플레이어 공격력 스냅샷을 반영해 Lv.21~30 스펙 기준 800 HP가 체감상 적절히 파괴되도록 보정
            float atkScale = 1f;
            var hub = CombatDataHub.Instance;
            if (hub != null && hub.PlayerSnapshot != null && hub.PlayerSnapshot.totalAttackPower > 0)
            {
                atkScale = Mathf.Max(1f, hub.PlayerSnapshot.totalAttackPower / 100f);
            }
            int finalDmg = Mathf.Max(1, Mathf.RoundToInt(damage * atkScale));

            _flashTimer = 0.14f;
            if (_limb != null) _limb.glow = 2.6f;

            if (_swarm != null)
            {
                _swarm.Hit(new Vector3(point.x, point.y, _swarm.FrontZ), 0.7f, Vector3.up * 2f, 0.08f, 1 << RSNanoSwarm.G_Rig);
            }

            TakePartDamage(finalDmg);
        }

        protected override void OnPartDamaged(int amount)
        {
            base.OnPartDamaged(amount);
            UpdateHpVisual();
            _onChangedCallback?.Invoke();
        }

        protected override void OnPartBroken()
        {
            // 묶여 있던 나노봇을 모래로 쏟아낸 뒤 림 점유 해제
            if (_swarm != null && _limb != null)
            {
                _swarm.Hit(transform.position, 2.5f, new Vector3(0f, 3.5f, 0f), 0.35f, 1 << RSNanoSwarm.G_Rig);
                _limb.amount = 0f;
            }

            SetActiveVisuals(false);
            KyoshoSfx.PlayCue(KyoshoSfx.Data?.sectionBroken, "section_broken", transform.position);

            // 대사 시스템 부위 파괴 바크 연동 (§7.1)
            string barkId = sectionType switch
            {
                KyoshoSectionType.Strings => KyoshoConstants.BarkStringsBroken,
                KyoshoSectionType.Percussion => KyoshoConstants.BarkPercussionBroken,
                KyoshoSectionType.Brass => KyoshoConstants.BarkBrassBroken,
                _ => GimmickID
            };
            var bark = FindAnyObjectByType<BattleBarkPlayer>();
            bark?.NotifyBossPartBroken(barkId);

            _onBrokenCallback?.Invoke(this);
            _onChangedCallback?.Invoke();
        }

        private void Update()
        {
            if (!HasEverSpawned || IsBroken || TemporarilyGathered) return;

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && _limb != null)
                {
                    _limb.glow = 0.65f;
                }
            }

            // 섹션 부유 호흡 모션 (본체와 박자 느낌 공유)
            float bob = Mathf.Sin(Time.time * 2.4f + (int)sectionType * 1.3f) * 0.09f;
            Vector3 cur = new Vector3(transform.position.x, _baseWorldPos.y + bob, _baseWorldPos.z);
            transform.position = cur;
            if (_limb != null) _limb.transform.position = cur;
        }

        private void EnsureColliderAndVisuals()
        {
            if (_collider == null) _collider = GetComponent<BoxCollider2D>();
            _collider.isTrigger = true;
            if (sectionType == KyoshoSectionType.Brass)
            {
                // 금관 섹션은 중앙 상단(floorY+3.2) 시각 위치를 유지하되 점프 근접 타격이 닿도록 하방 판정 포함
                _collider.size = new Vector2(1.7f, 2.4f);
                _collider.offset = new Vector2(0f, -0.55f);
            }
            else
            {
                _collider.size = new Vector2(1.4f, 1.8f);
                _collider.offset = Vector2.zero;
            }

            if (_worldHpRoot == null)
            {
                _worldHpRoot = new GameObject("SectionWorldUI");
                _worldHpRoot.transform.SetParent(transform, false);
                _worldHpRoot.transform.localPosition = new Vector3(0f, 1.15f, -0.1f);

                var canvas = _worldHpRoot.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 25;
                var crt = _worldHpRoot.GetComponent<RectTransform>();
                crt.sizeDelta = new Vector2(1.4f, 0.22f);

                var bgGo = new GameObject("Bg");
                bgGo.transform.SetParent(_worldHpRoot.transform, false);
                var bgrt = bgGo.AddComponent<RectTransform>();
                bgrt.anchorMin = Vector2.zero;
                bgrt.anchorMax = Vector2.one;
                bgrt.offsetMin = Vector2.zero;
                bgrt.offsetMax = Vector2.zero;
                var bgImg = bgGo.AddComponent<Image>();
                bgImg.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
                bgImg.color = new Color(0.05f, 0.06f, 0.10f, 0.85f);

                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(bgGo.transform, false);
                var frt = fillGo.AddComponent<RectTransform>();
                frt.anchorMin = Vector2.zero;
                frt.anchorMax = Vector2.one;
                frt.offsetMin = new Vector2(0.03f, 0.03f);
                frt.offsetMax = new Vector2(-0.03f, -0.03f);
                _worldHpFill = fillGo.AddComponent<Image>();
                _worldHpFill.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
                _worldHpFill.type = Image.Type.Filled;
                _worldHpFill.fillMethod = Image.FillMethod.Horizontal;
                _worldHpFill.color = GetThemeColor();

                var lblGo = new GameObject("Label");
                lblGo.transform.SetParent(transform, false);
                lblGo.transform.localPosition = new Vector3(0f, 1.42f, -0.1f);
                _labelText = lblGo.AddComponent<TextMeshPro>();
                _labelText.alignment = TextAlignmentOptions.Center;
                _labelText.fontSize = 2.6f;
                _labelText.color = GetThemeColor();
                _labelText.text = gimmickName;
            }
        }

        private void SetActiveVisuals(bool active)
        {
            if (_collider != null) _collider.enabled = active;
            if (_worldHpRoot != null) _worldHpRoot.SetActive(active);
            if (_labelText != null) _labelText.gameObject.SetActive(active);
        }

        private void UpdateHpVisual()
        {
            if (_worldHpFill != null)
            {
                _worldHpFill.fillAmount = MaxDurability > 0 ? Mathf.Clamp01((float)CurrentDurability / MaxDurability) : 0f;
            }
        }

        private Color GetThemeColor()
        {
            return sectionType switch
            {
                KyoshoSectionType.Strings => KyoshoConstants.StringsMintColor,
                KyoshoSectionType.Percussion => KyoshoConstants.PercussionAmberColor,
                KyoshoSectionType.Brass => KyoshoConstants.BrassMagentaColor,
                _ => Color.white
            };
        }
    }
}
