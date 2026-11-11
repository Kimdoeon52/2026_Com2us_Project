using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealSteel.BossRace
{
    public class DifficultyRow : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        private static readonly Color BarEmpty = new Color32(0x2E, 0x31, 0x35, 0xFF);

        [Header("연결")]
        [SerializeField] private RectTransform shiftTarget;
        [SerializeField] private Image background;
        [SerializeField] private Image frame;
        [SerializeField] private Image stripe;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private Image[] riskBars = new Image[0];
        [SerializeField] private TMP_Text scoreText;

        [Header("표시")]
        [SerializeField] private Color backgroundNormal = new Color32(0x1B, 0x1E, 0x22, 0xFF);
        [SerializeField] private Color backgroundSelected = new Color32(0x22, 0x25, 0x2A, 0xFF);
        [SerializeField] private Color frameNormal = new Color32(0x33, 0x36, 0x3B, 0xFF);
        [SerializeField] private Color frameSelected = new Color32(0xF2, 0xA9, 0x00, 0xFF);
        [SerializeField, Min(0.01f)] private float frameScaleNormal = 2f;
        [SerializeField, Min(0.01f)] private float frameScaleSelected = 1f;
        [SerializeField] private float selectedShift = 8f;
        [SerializeField] private float stripeNormal = 4f;
        [SerializeField] private float stripeSelected = 10f;

        private Vector2 basePosition;
        private bool hasBase;

        public int Index { get; set; }
        public DifficultyEntry Entry { get; private set; }

        public event Action<DifficultyRow> Hovered;
        public event Action<DifficultyRow> Clicked;

        public void Bind(DifficultyEntry entry, Color color)
        {
            Entry = entry;
            if (labelText != null) labelText.text = entry.label;
            if (subtitleText != null) subtitleText.text = entry.subtitle;
            if (scoreText != null) scoreText.text = entry.scoreLabel;
            if (stripe != null) stripe.color = color;

            for (int i = 0; i < riskBars.Length; i++)
            {
                if (riskBars[i] != null) riskBars[i].color = i < entry.risk ? color : BarEmpty;
            }
        }

        public void SetSelected(bool selected)
        {
            RectTransform target = shiftTarget != null ? shiftTarget : (RectTransform)transform;
            if (!hasBase)
            {
                basePosition = target.anchoredPosition;
                hasBase = true;
            }

            target.anchoredPosition = basePosition + new Vector2(selected ? selectedShift : 0f, 0f);
            if (background != null) background.color = selected ? backgroundSelected : backgroundNormal;
            if (frame != null)
            {
                frame.color = selected ? frameSelected : frameNormal;
                frame.pixelsPerUnitMultiplier = selected ? frameScaleSelected : frameScaleNormal;
            }
            if (stripe != null)
            {
                Vector2 size = stripe.rectTransform.sizeDelta;
                stripe.rectTransform.sizeDelta = new Vector2(selected ? stripeSelected : stripeNormal, size.y);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hovered?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }
    }
}
