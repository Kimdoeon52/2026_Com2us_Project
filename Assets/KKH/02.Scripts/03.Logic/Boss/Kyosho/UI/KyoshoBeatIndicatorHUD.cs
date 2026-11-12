using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [화면 하단 박 표시기 HUD (KyoshoBeatIndicatorHUD)]
    /// 음소거/청각 접근성 대응을 위해 4박(● ○ ○ ○) 메트로놈 진행과 강박/약박 펄스, 현재 악상/BPM을 표시함.
    /// </summary>
    public class KyoshoBeatIndicatorHUD : MonoBehaviour
    {
        [Header("표시 설정")]
        [SerializeField] private bool showIndicator = true;
        [SerializeField] private Vector2 anchoredOffset = new Vector2(0f, 38f);

        private KyoshoBeatClock _clock;
        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI _tempoLabel;
        private readonly List<Image> _beatDots = new List<Image>();
        private readonly List<float> _dotPulse = new List<float>();

        private static readonly Color DownbeatColor = new Color(1.00f, 0.86f, 0.28f, 1f);
        private static readonly Color SubbeatColor = new Color(0.45f, 0.90f, 1.00f, 1f);
        private static readonly Color InactiveColor = new Color(0.22f, 0.26f, 0.34f, 0.75f);

        public bool ShowIndicator
        {
            get => showIndicator;
            set
            {
                showIndicator = value;
                if (_canvasGroup != null) _canvasGroup.alpha = showIndicator ? 1f : 0f;
            }
        }

        public void Bind(KyoshoBeatClock clock, BossDifficulty difficulty)
        {
            Unbind();
            _clock = clock;
            EnsureUI();

            if (_tempoLabel != null)
            {
                string marking = KyoshoConstants.GetTempoMarking(difficulty);
                float bpm = _clock != null ? _clock.Bpm : KyoshoConstants.GetBpmForDifficulty(difficulty);
                _tempoLabel.text = $"{marking}  ♪ = {bpm:F0}";
            }

            if (_clock != null)
            {
                _clock.OnBeat += HandleBeat;
            }
            ShowIndicator = showIndicator;
        }

        public void Unbind()
        {
            if (_clock != null)
            {
                _clock.OnBeat -= HandleBeat;
                _clock = null;
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandleBeat(int beatInBar, int totalBeat)
        {
            for (int i = 0; i < _dotPulse.Count; i++)
            {
                _dotPulse[i] = (i == beatInBar) ? 1f : 0f;
            }
        }

        private void Update()
        {
            if (!showIndicator || _beatDots.Count == 0) return;

            for (int i = 0; i < _beatDots.Count; i++)
            {
                float p = _dotPulse[i];
                if (p > 0f)
                {
                    p = Mathf.MoveTowards(p, 0f, Time.unscaledDeltaTime * 3.8f);
                    _dotPulse[i] = p;
                }

                Color activeCol = i == 0 ? DownbeatColor : SubbeatColor;
                _beatDots[i].color = Color.Lerp(InactiveColor, activeCol, p);
                float scale = i == 0 ? Mathf.Lerp(1.0f, 1.48f, p) : Mathf.Lerp(0.88f, 1.28f, p);
                _beatDots[i].rectTransform.localScale = Vector3.one * scale;
            }
        }

        private void EnsureUI()
        {
            if (_canvasGroup != null) return;

            var canvasGo = new GameObject("[KyoshoBeatIndicatorCanvas]");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            var panelGo = new GameObject("MetronomeBar");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRt = panelGo.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0f);
            panelRt.anchorMax = new Vector2(0.5f, 0f);
            panelRt.pivot = new Vector2(0.5f, 0f);
            panelRt.anchoredPosition = anchoredOffset;
            panelRt.sizeDelta = new Vector2(320f, 54f);

            var bg = panelGo.AddComponent<Image>();
            bg.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            bg.color = new Color(0.05f, 0.07f, 0.11f, 0.72f);

            var tempoGo = new GameObject("TempoLabel");
            tempoGo.transform.SetParent(panelGo.transform, false);
            var tempoRt = tempoGo.AddComponent<RectTransform>();
            tempoRt.anchorMin = new Vector2(0f, 0.55f);
            tempoRt.anchorMax = new Vector2(1f, 1f);
            tempoRt.offsetMin = Vector2.zero;
            tempoRt.offsetMax = Vector2.zero;
            _tempoLabel = tempoGo.AddComponent<TextMeshProUGUI>();
            _tempoLabel.alignment = TextAlignmentOptions.Center;
            _tempoLabel.fontSize = 15f;
            _tempoLabel.color = new Color(0.85f, 0.93f, 1f, 0.95f);

            Sprite circle = KyoshoNanobotAttackHelper.GetCircleSprite();
            _beatDots.Clear();
            _dotPulse.Clear();
            for (int i = 0; i < 4; i++)
            {
                var dotGo = new GameObject($"BeatDot_{i}");
                dotGo.transform.SetParent(panelGo.transform, false);
                var dotRt = dotGo.AddComponent<RectTransform>();
                dotRt.anchorMin = new Vector2(0.5f, 0.3f);
                dotRt.anchorMax = new Vector2(0.5f, 0.3f);
                dotRt.pivot = new Vector2(0.5f, 0.5f);
                dotRt.anchoredPosition = new Vector2((i - 1.5f) * 44f, 0f);
                dotRt.sizeDelta = i == 0 ? new Vector2(18f, 18f) : new Vector2(14f, 14f);

                var img = dotGo.AddComponent<Image>();
                img.sprite = circle;
                img.color = InactiveColor;
                _beatDots.Add(img);
                _dotPulse.Add(0f);
            }
        }
    }
}
