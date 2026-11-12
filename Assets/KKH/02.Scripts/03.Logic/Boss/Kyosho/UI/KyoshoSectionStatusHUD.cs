using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [2페이즈 오케스트라 악기 섹션 상태 HUD (KyoshoSectionStatusHUD)]
    /// 현악(좌), 타악(우), 금관(중앙 상단)의 생존 여부와 남은 체력, 전부 파괴 보너스 활성 여부를 표시함.
    /// </summary>
    public class KyoshoSectionStatusHUD : MonoBehaviour
    {
        private class CardView
        {
            public GameObject Root;
            public Image Fill;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI HpText;
            public Color ThemeColor;
        }

        private GameObject _rootCanvas;
        private TextMeshProUGUI _bonusBanner;
        private readonly Dictionary<KyoshoSectionType, CardView> _cards = new Dictionary<KyoshoSectionType, CardView>();

        public void SetVisible(bool visible)
        {
            if (visible) EnsureUI();
            if (_rootCanvas != null) _rootCanvas.SetActive(visible);
        }

        public void Refresh(IReadOnlyList<KyoshoSectionPartGimmick> sections, bool allBrokenBonusActive)
        {
            if (sections == null || sections.Count == 0)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            foreach (var kvp in _cards)
            {
                kvp.Value.Root.SetActive(false);
            }

            foreach (var sec in sections)
            {
                if (sec == null || !sec.HasEverSpawned) continue;
                if (!_cards.TryGetValue(sec.SectionType, out var card)) continue;

                card.Root.SetActive(true);
                float ratio = sec.MaxHp > 0 ? Mathf.Clamp01((float)sec.CurrentHp / sec.MaxHp) : 0f;
                card.Fill.fillAmount = ratio;
                card.Fill.color = sec.IsBroken ? new Color(0.35f, 0.35f, 0.40f, 0.6f) : card.ThemeColor;
                card.HpText.text = sec.IsBroken ? "파괴됨 (강화 해제)" : $"{sec.CurrentHp} / {sec.MaxHp}";
                card.Title.color = sec.IsBroken ? new Color(0.6f, 0.6f, 0.65f, 0.8f) : card.ThemeColor;
            }

            if (_bonusBanner != null)
            {
                _bonusBanner.gameObject.SetActive(allBrokenBonusActive);
            }
        }

        private void EnsureUI()
        {
            if (_rootCanvas != null) return;

            _rootCanvas = new GameObject("[KyoshoSectionStatusCanvas]");
            _rootCanvas.transform.SetParent(transform, false);
            var canvas = _rootCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 44;
            var scaler = _rootCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var cg = _rootCanvas.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var container = new GameObject("SectionContainer");
            container.transform.SetParent(_rootCanvas.transform, false);
            var crt = container.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 0.5f);
            crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-24f, 60f);
            crt.sizeDelta = new Vector2(230f, 220f);

            CreateCard(container.transform, KyoshoSectionType.Strings, "현악 섹션 (스타카토)", KyoshoConstants.StringsMintColor, 65f);
            CreateCard(container.transform, KyoshoSectionType.Percussion, "타악 섹션 (지휘)", KyoshoConstants.PercussionAmberColor, 5f);
            CreateCard(container.transform, KyoshoSectionType.Brass, "금관 섹션 (페르마타)", KyoshoConstants.BrassMagentaColor, -55f);

            var bonusGo = new GameObject("AllBrokenBonus");
            bonusGo.transform.SetParent(container.transform, false);
            var brt = bonusGo.AddComponent<RectTransform>();
            brt.anchoredPosition = new Vector2(0f, -110f);
            brt.sizeDelta = new Vector2(230f, 34f);
            _bonusBanner = bonusGo.AddComponent<TextMeshProUGUI>();
            _bonusBanner.alignment = TextAlignmentOptions.Center;
            _bonusBanner.fontSize = 13f;
            _bonusBanner.fontStyle = FontStyles.Bold;
            _bonusBanner.color = new Color(1f, 0.88f, 0.35f, 1f);
            _bonusBanner.text = "전 섹션 파괴! (피격+30% / +1박)";
            bonusGo.SetActive(false);
        }

        private void CreateCard(Transform parent, KyoshoSectionType type, string title, Color color, float y)
        {
            var root = new GameObject($"Card_{type}");
            root.transform.SetParent(parent, false);
            var rt = root.AddComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(220f, 50f);

            var bg = root.AddComponent<Image>();
            bg.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            bg.color = new Color(0.05f, 0.07f, 0.11f, 0.80f);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(root.transform, false);
            var trt = titleGo.AddComponent<RectTransform>();
            trt.anchoredPosition = new Vector2(0f, 13f);
            trt.sizeDelta = new Vector2(204f, 18f);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.alignment = TextAlignmentOptions.Left;
            titleTxt.fontSize = 13f;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.text = title;
            titleTxt.color = color;

            var barBgGo = new GameObject("BarBg");
            barBgGo.transform.SetParent(root.transform, false);
            var bgrt = barBgGo.AddComponent<RectTransform>();
            bgrt.anchoredPosition = new Vector2(0f, -4f);
            bgrt.sizeDelta = new Vector2(204f, 10f);
            var barBg = barBgGo.AddComponent<Image>();
            barBg.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            barBg.color = new Color(0.15f, 0.17f, 0.22f, 0.9f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barBgGo.transform, false);
            var frt = fillGo.AddComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            var fill = fillGo.AddComponent<Image>();
            fill.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.color = color;

            var hpGo = new GameObject("Hp");
            hpGo.transform.SetParent(root.transform, false);
            var hrt = hpGo.AddComponent<RectTransform>();
            hrt.anchoredPosition = new Vector2(0f, -16f);
            hrt.sizeDelta = new Vector2(204f, 16f);
            var hpTxt = hpGo.AddComponent<TextMeshProUGUI>();
            hpTxt.alignment = TextAlignmentOptions.Right;
            hpTxt.fontSize = 11f;
            hpTxt.color = Color.white;

            _cards[type] = new CardView
            {
                Root = root,
                Fill = fill,
                Title = titleTxt,
                HpText = hpTxt,
                ThemeColor = color
            };
        }
    }
}
