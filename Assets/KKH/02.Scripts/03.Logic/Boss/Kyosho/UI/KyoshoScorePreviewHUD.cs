using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [쿄쇼 악보 · 페르마타 · 문양 시각 예고 HUD (KyoshoScorePreviewHUD)]
    /// 1) 스타카토 1마디 콜: 보스 머리 위 오선지 악보 위젯 & 박 진행 커서 표시 (현악 강화 ♬ 민트색 표기 포함)
    /// 2) 페르마타(⌒) 정지: 화면 상단 중앙 거대 늘임표 아이콘 점등
    /// 3) 변주곡(Variation): 진행 단계 및 남은 시간 안내
    /// </summary>
    public class KyoshoScorePreviewHUD : MonoBehaviour
    {
        private KyoshoBossController _kyosho;
        private Canvas _canvas;
        private RectTransform _staffRoot;
        private RectTransform _playheadBar;
        private TextMeshProUGUI _staffCaption;
        private readonly List<NoteIconEntry> _noteIcons = new List<NoteIconEntry>();

        private RectTransform _fermataBanner;
        private TextMeshProUGUI _fermataIconText;
        private CanvasGroup _fermataGroup;

        private RectTransform _variationBanner;
        private TextMeshProUGUI _variationStatusText;

        private struct NoteIconEntry
        {
            public KyoshoNoteEvent Ev;
            public TextMeshProUGUI Text;
            public Image GlowBg;
        }

        public void Initialize(KyoshoBossController kyosho)
        {
            _kyosho = kyosho;
            EnsureUI();
            HideAll();
        }

        public void HideAll()
        {
            if (_staffRoot != null) _staffRoot.gameObject.SetActive(false);
            if (_fermataBanner != null) _fermataBanner.gameObject.SetActive(false);
            if (_variationBanner != null) _variationBanner.gameObject.SetActive(false);
        }

        public void ShowStaccatoScore(KyoshoRhythmBar bar, bool hasStringsBonus)
        {
            EnsureUI();
            if (_staffRoot == null) return;

            ClearNoteIcons();
            _staffRoot.gameObject.SetActive(true);
            if (_staffCaption != null)
            {
                _staffCaption.text = hasStringsBonus ? "STACCATO  (+현악 16분음표♬)" : "STACCATO";
                _staffCaption.color = hasStringsBonus ? KyoshoConstants.StringsMintColor : Color.white;
            }

            const float staffWidth = 280f;
            if (bar != null && bar.notes != null)
            {
                foreach (var ev in bar.notes)
                {
                    float x = ((ev.beatOffset / 4f) - 0.5f) * (staffWidth - 36f);
                    float y = ev.noteType == KyoshoNoteType.Rest ? 0f : (ev.pitchIndex - 2.5f) * 8f;
                    AddNoteIcon(ev, x, y, KyoshoConstants.OriginalPatternColor, false);

                    if (hasStringsBonus && ev.noteType != KyoshoNoteType.Rest)
                    {
                        float extraOffset = ev.beatOffset + 0.25f;
                        if (extraOffset < 3.95f)
                        {
                            float ex = ((extraOffset / 4f) - 0.5f) * (staffWidth - 36f);
                            var bonusEv = new KyoshoNoteEvent(extraOffset, KyoshoNoteType.Sixteenth, ev.pitchIndex);
                            AddNoteIcon(bonusEv, ex, y + 10f, KyoshoConstants.StringsMintColor, true);
                        }
                    }
                }
            }

            UpdateStaccatoCursor(0f);
        }

        public void UpdateStaccatoCursor(float barBeatProgress0To4)
        {
            if (_staffRoot == null || !_staffRoot.gameObject.activeSelf) return;

            const float staffWidth = 280f;
            float norm = Mathf.Clamp01(barBeatProgress0To4 / 4f);
            if (_playheadBar != null)
            {
                _playheadBar.anchoredPosition = new Vector2((norm - 0.5f) * (staffWidth - 36f), 0f);
            }

            for (int i = 0; i < _noteIcons.Count; i++)
            {
                var entry = _noteIcons[i];
                bool passed = barBeatProgress0To4 >= entry.Ev.beatOffset;
                bool activeNow = passed && barBeatProgress0To4 <= entry.Ev.beatOffset + 0.35f;
                if (entry.GlowBg != null)
                {
                    entry.GlowBg.enabled = activeNow;
                }
                if (entry.Text != null)
                {
                    entry.Text.transform.localScale = activeNow ? Vector3.one * 1.35f : Vector3.one;
                }
            }
        }

        public void HideStaccatoScore()
        {
            if (_staffRoot != null) _staffRoot.gameObject.SetActive(false);
        }

        public void ShowFermataHold(bool show)
        {
            EnsureUI();
            if (_fermataBanner == null) return;
            _fermataBanner.gameObject.SetActive(show);
            if (show && _fermataGroup != null) _fermataGroup.alpha = 1f;
        }

        public void ShowVariationStatus(string message)
        {
            EnsureUI();
            if (_variationBanner == null) return;
            _variationBanner.gameObject.SetActive(!string.IsNullOrEmpty(message));
            if (_variationStatusText != null) _variationStatusText.text = message;
        }

        private void LateUpdate()
        {
            if (_staffRoot != null && _staffRoot.gameObject.activeSelf && _kyosho != null)
            {
                Camera cam = _kyosho.TargetCamera != null ? _kyosho.TargetCamera : Camera.main;
                if (cam != null)
                {
                    Vector3 worldHead = _kyosho.GetHeadAnchorWorldPosition();
                    Vector3 screenPos = cam.WorldToScreenPoint(worldHead);
                    if (screenPos.z > 0f)
                    {
                        _staffRoot.position = new Vector3(
                            Mathf.Clamp(screenPos.x, 180f, Screen.width - 180f),
                            Mathf.Clamp(screenPos.y, 140f, Screen.height - 90f),
                            0f);
                    }
                }
            }

            if (_fermataBanner != null && _fermataBanner.gameObject.activeSelf && _fermataGroup != null)
            {
                float pulse = 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * 10f);
                _fermataGroup.alpha = pulse;
                _fermataBanner.localScale = Vector3.one * (0.96f + 0.08f * pulse);
            }
        }

        private void AddNoteIcon(KyoshoNoteEvent ev, float x, float y, Color col, bool isBonus)
        {
            var go = new GameObject($"Note_{ev.noteType}");
            go.transform.SetParent(_staffRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, y - 4f);
            rt.sizeDelta = new Vector2(28f, 28f);

            var bg = go.AddComponent<Image>();
            bg.sprite = KyoshoNanobotAttackHelper.GetCircleSprite();
            bg.color = new Color(col.r, col.g, col.b, 0.45f);
            bg.enabled = false;

            var txtGo = new GameObject("Glyph");
            txtGo.transform.SetParent(go.transform, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = isBonus ? 18f : 22f;
            txt.color = col;
            txt.text = ev.noteType switch
            {
                KyoshoNoteType.Quarter => "♩",
                KyoshoNoteType.Eighth => "♪",
                KyoshoNoteType.Sixteenth => "♬",
                KyoshoNoteType.Triplet => "♪3",
                _ => "𝄽"
            };

            _noteIcons.Add(new NoteIconEntry { Ev = ev, Text = txt, GlowBg = bg });
        }

        private void ClearNoteIcons()
        {
            for (int i = 0; i < _noteIcons.Count; i++)
            {
                if (_noteIcons[i].Text != null && _noteIcons[i].Text.transform.parent != null)
                {
                    Destroy(_noteIcons[i].Text.transform.parent.gameObject);
                }
            }
            _noteIcons.Clear();
        }

        private void EnsureUI()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("[KyoshoScorePreviewCanvas]");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 46;
            var cg = canvasGo.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // 1. 스타카토 머리 위 악보 패널
            var staffGo = new GameObject("StaccatoStaff");
            staffGo.transform.SetParent(canvasGo.transform, false);
            _staffRoot = staffGo.AddComponent<RectTransform>();
            _staffRoot.sizeDelta = new Vector2(300f, 82f);

            var staffBg = staffGo.AddComponent<Image>();
            staffBg.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            staffBg.color = new Color(0.06f, 0.08f, 0.13f, 0.85f);

            for (int line = -2; line <= 2; line++)
            {
                var lineGo = new GameObject($"Line_{line}");
                lineGo.transform.SetParent(staffGo.transform, false);
                var lrt = lineGo.AddComponent<RectTransform>();
                lrt.anchoredPosition = new Vector2(0f, line * 8f - 4f);
                lrt.sizeDelta = new Vector2(276f, 1.5f);
                var limg = lineGo.AddComponent<Image>();
                limg.color = new Color(0.65f, 0.78f, 0.95f, 0.35f);
            }

            var barGo = new GameObject("Playhead");
            barGo.transform.SetParent(staffGo.transform, false);
            _playheadBar = barGo.AddComponent<RectTransform>();
            _playheadBar.sizeDelta = new Vector2(3f, 52f);
            var barImg = barGo.AddComponent<Image>();
            barImg.color = new Color(1f, 0.9f, 0.35f, 0.9f);

            var capGo = new GameObject("Caption");
            capGo.transform.SetParent(staffGo.transform, false);
            var capRt = capGo.AddComponent<RectTransform>();
            capRt.anchoredPosition = new Vector2(0f, 30f);
            capRt.sizeDelta = new Vector2(290f, 22f);
            _staffCaption = capGo.AddComponent<TextMeshProUGUI>();
            _staffCaption.alignment = TextAlignmentOptions.Center;
            _staffCaption.fontSize = 14f;
            _staffCaption.fontStyle = FontStyles.Bold;

            // 2. 페르마타(⌒) 상단 중앙 배너
            var fermataGo = new GameObject("FermataBanner");
            fermataGo.transform.SetParent(canvasGo.transform, false);
            _fermataBanner = fermataGo.AddComponent<RectTransform>();
            _fermataBanner.anchorMin = new Vector2(0.5f, 1f);
            _fermataBanner.anchorMax = new Vector2(0.5f, 1f);
            _fermataBanner.pivot = new Vector2(0.5f, 1f);
            _fermataBanner.anchoredPosition = new Vector2(0f, -108f);
            _fermataBanner.sizeDelta = new Vector2(210f, 74f);
            _fermataGroup = fermataGo.AddComponent<CanvasGroup>();

            var fBg = fermataGo.AddComponent<Image>();
            fBg.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            fBg.color = new Color(0.08f, 0.05f, 0.14f, 0.82f);

            var fTxtGo = new GameObject("FermataGlyph");
            fTxtGo.transform.SetParent(fermataGo.transform, false);
            var fTxtRt = fTxtGo.AddComponent<RectTransform>();
            fTxtRt.anchorMin = Vector2.zero;
            fTxtRt.anchorMax = Vector2.one;
            fTxtRt.offsetMin = Vector2.zero;
            fTxtRt.offsetMax = Vector2.zero;
            _fermataIconText = fTxtGo.AddComponent<TextMeshProUGUI>();
            _fermataIconText.alignment = TextAlignmentOptions.Center;
            _fermataIconText.fontSize = 32f;
            _fermataIconText.fontStyle = FontStyles.Bold;
            _fermataIconText.color = new Color(1f, 0.82f, 0.35f, 1f);
            _fermataIconText.text = "⌒  FERMATA";

            // 3. 변주곡 안내 배너
            var varGo = new GameObject("VariationBanner");
            varGo.transform.SetParent(canvasGo.transform, false);
            _variationBanner = varGo.AddComponent<RectTransform>();
            _variationBanner.anchorMin = new Vector2(0.5f, 1f);
            _variationBanner.anchorMax = new Vector2(0.5f, 1f);
            _variationBanner.pivot = new Vector2(0.5f, 1f);
            _variationBanner.anchoredPosition = new Vector2(0f, -96f);
            _variationBanner.sizeDelta = new Vector2(560f, 44f);
            var vBg = varGo.AddComponent<Image>();
            vBg.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            vBg.color = new Color(0.07f, 0.09f, 0.16f, 0.85f);

            var vTxtGo = new GameObject("Text");
            vTxtGo.transform.SetParent(varGo.transform, false);
            var vTxtRt = vTxtGo.AddComponent<RectTransform>();
            vTxtRt.anchorMin = Vector2.zero;
            vTxtRt.anchorMax = Vector2.one;
            vTxtRt.offsetMin = Vector2.zero;
            vTxtRt.offsetMax = Vector2.zero;
            _variationStatusText = vTxtGo.AddComponent<TextMeshProUGUI>();
            _variationStatusText.alignment = TextAlignmentOptions.Center;
            _variationStatusText.fontSize = 18f;
            _variationStatusText.fontStyle = FontStyles.Bold;
            _variationStatusText.color = Color.white;
        }
    }
}
