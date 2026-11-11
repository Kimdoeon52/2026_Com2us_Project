using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealSteel.BossRace
{
    public class BossRaceCard : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        private static readonly Color Green = new Color32(0x7F, 0xD3, 0xA0, 0xFF);
        private static readonly Color Muted = new Color32(0x8A, 0x8E, 0x94, 0xFF);
        private static readonly Color SegmentEmpty = new Color32(0x2E, 0x31, 0x35, 0xFF);

        [Header("연결")]
        [SerializeField] private RectTransform shakeTarget;
        [SerializeField] private CanvasGroup content;
        [SerializeField] private Image background;
        [SerializeField] private Image frame;
        [SerializeField] private Image regionTag;
        [SerializeField] private TMP_Text regionText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image lockIcon;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text tagsText;
        [SerializeField] private Image[] segments = new Image[0];
        [SerializeField] private GameObject lockOverlay;

        [Header("색")]
        [SerializeField] private Color backgroundNormal = new Color32(0x1B, 0x1E, 0x22, 0xFF);
        [SerializeField] private Color backgroundSelected = new Color32(0x22, 0x25, 0x2A, 0xFF);
        [SerializeField] private Color frameNormal = new Color32(0x33, 0x36, 0x3B, 0xFF);
        [SerializeField] private Color iconNormal = new Color32(0x9A, 0x9E, 0xA4, 0xFF);
        [SerializeField] private Color accentColor = new Color32(0xF2, 0xA9, 0x00, 0xFF);
        [SerializeField] private Color frameSelected = new Color32(0xF2, 0xA9, 0x00, 0xFF);
        [SerializeField, Min(0.01f)] private float frameScaleNormal = 2f;
        [SerializeField, Min(0.01f)] private float frameScaleSelected = 1f;
        [SerializeField] private Color tagActive = new Color32(0xF2, 0xA9, 0x00, 0xFF);
        [SerializeField] private Color tagInactive = new Color32(0x2E, 0x31, 0x35, 0xFF);

        [Header("지역 태그 이미지 (비우면 바꾸지 않음)")]
        [SerializeField] private Sprite tagSpriteActive;
        [SerializeField] private Sprite tagSpriteInactive;
        [SerializeField, Range(0f, 1f)] private float lockedAlpha = 0.45f;

        [Header("흔들림")]
        [SerializeField, Min(0f)] private float shakeSeconds = 0.3f;
        [SerializeField, Min(0f)] private float shakeDistance = 8f;

        private Vector2 basePosition;
        private float shakeLeft;

        public BossRaceEntry Entry { get; private set; }
        public BossRaceCardState State { get; private set; }
        public bool IsSelectable => State != BossRaceCardState.Locked;
        public int Index { get; set; }

        public event Action<BossRaceCard> Hovered;
        public event Action<BossRaceCard> Clicked;

        private void Awake()
        {
            if (shakeTarget == null) shakeTarget = content != null ? (RectTransform)content.transform : (RectTransform)transform;
            basePosition = shakeTarget.anchoredPosition;
        }

        private void Update()
        {
            if (shakeLeft <= 0f) return;

            shakeLeft -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(shakeLeft / Mathf.Max(0.01f, shakeSeconds));
            float offset = Mathf.Sin(Time.unscaledTime * 60f) * shakeDistance * t;
            shakeTarget.anchoredPosition = basePosition + new Vector2(shakeLeft > 0f ? offset : 0f, 0f);
        }

        public void Bind(BossRaceEntry entry, BossRaceCardState state, int bestDifficulty)
        {
            Entry = entry;
            State = state;
            bool locked = state == BossRaceCardState.Locked;

            SetText(regionText, entry.RegionText);
            SetText(nameText, entry.bossName);
            SetText(tagsText, entry.tags);
            if (icon != null)
            {
                icon.sprite = entry.icon;
                icon.enabled = entry.icon != null;
            }

            switch (state)
            {
                case BossRaceCardState.InProgress:
                    SetText(statusText, "진행 중", accentColor);
                    break;
                case BossRaceCardState.Cleared:
                    SetText(statusText, "클리어", Green);
                    break;
                default:
                    SetText(statusText, "잠김", Muted);
                    break;
            }

            if (lockIcon != null) lockIcon.gameObject.SetActive(locked);
            if (lockOverlay != null) lockOverlay.SetActive(locked);
            if (content != null) content.alpha = locked ? lockedAlpha : 1f;

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;
                bool cleared = !locked && i <= bestDifficulty;
                segments[i].color = cleared && i < BossRaceDifficulty.Colors.Length ? BossRaceDifficulty.Colors[i] : SegmentEmpty;
            }

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            bool active = State == BossRaceCardState.InProgress || selected;

            if (background != null) background.color = selected ? backgroundSelected : backgroundNormal;
            if (frame != null)
            {
                frame.color = selected ? frameSelected : frameNormal;
                frame.pixelsPerUnitMultiplier = selected ? frameScaleSelected : frameScaleNormal;
            }

            bool tagOn = active && State != BossRaceCardState.Locked;
            if (regionTag != null)
            {
                Sprite sprite = tagOn ? tagSpriteActive : tagSpriteInactive;
                if (sprite != null) regionTag.sprite = sprite;
                regionTag.color = tagOn ? tagActive : tagInactive;
            }
            if (regionText != null) regionText.color = tagOn ? new Color32(0x14, 0x16, 0x19, 0xFF) : new Color32(0xD8, 0xDA, 0xDE, 0xFF);
            if (icon != null) icon.color = iconNormal;
        }

        public void Shake()
        {
            shakeLeft = shakeSeconds;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hovered?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null) text.text = value ?? "";
        }

        private static void SetText(TMP_Text text, string value, Color color)
        {
            if (text == null) return;
            text.text = value;
            text.color = color;
        }
    }
}
