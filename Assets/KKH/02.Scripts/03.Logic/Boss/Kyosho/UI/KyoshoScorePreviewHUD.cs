using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// 쿄쇼 패턴 분석 및 기믹 가독성을 높이기 위한 화면 상단 고정 HUD.
    /// - Staccato: 1마디 예고 ~ 2마디 발사까지 4박 타임라인과 상/하단 레인을 컬러 카드로 표시
    /// - Fermata: 안전 틈새 대피 배너 표시
    /// - Variation: 변형 순서 [1] -> [2] -> [3] 기억 슬롯 및 입력 진행도 표시
    /// </summary>
    public class KyoshoScorePreviewHUD : MonoBehaviour
    {
        private class NoteIconEntry
        {
            public RectTransform Rect;
            public Image Bg;
            public float BeatOffset;
            public Color BaseBgColor;
        }

        private class VariationSlotView
        {
            public RectTransform Root;
            public Image Bg;
            public Image Border;
            public TextMeshProUGUI StepLabel;
            public TextMeshProUGUI GlyphText;
            public TextMeshProUGUI NameText;
        }

        private const float PanelWidth = 760f;
        private const float PanelHeight = 176f;
        private const float TrackWidth = 560f;
        private const float TrackOffsetX = 48f;
        private const float HighLaneY = 24f;
        private const float LowLaneY = -24f;

        private KyoshoBossController _kyosho;
        private Canvas _canvas;

        // Staccato UI
        private RectTransform _staffRoot;
        private Image _staffBg;
        private TextMeshProUGUI _staffCaption;
        private RectTransform _playheadBar;
        private Image _playheadImage;
        private readonly List<NoteIconEntry> _noteIcons = new List<NoteIconEntry>();

        // Fermata UI
        private RectTransform _fermataBanner;
        private TextMeshProUGUI _fermataText;

        // Pattern Hint Banner (공용 상단 패턴 가이드 & 카운트다운 게이지)
        private RectTransform _hintBanner;
        private Image _hintBg;
        private Image _hintAccentBar;
        private Image _hintTopBorder;
        private TextMeshProUGUI _hintTitle;
        private TextMeshProUGUI _hintTimerLabel;
        private TextMeshProUGUI _hintDetail;
        private GameObject _hintGaugeRoot;
        private Image _hintGaugeFill;

        // Variation UI
        private RectTransform _variationBanner;
        private TextMeshProUGUI _variationText;
        private RectTransform _variationSlotsRoot;
        private readonly List<VariationSlotView> _variationSlots = new List<VariationSlotView>();

        private bool _staccatoFiringPhase;

        public void Initialize(KyoshoBossController kyosho)
        {
            _kyosho = kyosho;
            EnsureUI();
            HideAll();
        }

        public void HideAll()
        {
            HideStaccatoScore();
            ShowFermataHold(false);
            ShowVariationStatus(null);
            HidePatternHint();
            HideVariationSequenceSlots();
        }

        public void ShowPatternHint(string title, string detail, Color accentColor, float progress01 = -1f, string timerLabel = null)
        {
            EnsureUI();
            if (_hintBanner == null) return;

            _hintBanner.gameObject.SetActive(true);
            if (_hintAccentBar != null) _hintAccentBar.color = accentColor;
            if (_hintTopBorder != null) _hintTopBorder.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.85f);
            if (_hintTitle != null)
            {
                _hintTitle.text = title ?? string.Empty;
                _hintTitle.color = Color.Lerp(accentColor, Color.white, 0.35f);
            }
            if (_hintDetail != null) _hintDetail.text = detail ?? string.Empty;

            UpdatePatternHintProgress(progress01, timerLabel);
        }

        public void UpdatePatternHintProgress(float progress01, string timerLabel = null, string detail = null)
        {
            if (_hintBanner == null || !_hintBanner.gameObject.activeSelf) return;

            if (detail != null && _hintDetail != null)
            {
                _hintDetail.text = detail;
            }
            if (_hintTimerLabel != null)
            {
                bool hasTimer = !string.IsNullOrEmpty(timerLabel);
                _hintTimerLabel.gameObject.SetActive(hasTimer);
                if (hasTimer) _hintTimerLabel.text = timerLabel;
            }
            if (_hintGaugeRoot != null)
            {
                bool showGauge = progress01 >= 0f;
                _hintGaugeRoot.SetActive(showGauge);
                if (showGauge && _hintGaugeFill != null)
                {
                    _hintGaugeFill.fillAmount = Mathf.Clamp01(progress01);
                    if (_hintAccentBar != null) _hintGaugeFill.color = _hintAccentBar.color;
                }
            }
        }

        public void HidePatternHint()
        {
            if (_hintBanner != null)
            {
                _hintBanner.gameObject.SetActive(false);
            }
        }

        public void ShowStaccatoScore(KyoshoRhythmBar bar, bool hasStringsBonus)
        {
            EnsureUI();
            ClearNoteIcons();

            _staccatoFiringPhase = false;
            _staffRoot.gameObject.SetActive(true);
            SetStaccatoPhase(false, hasStringsBonus);

            if (bar != null && bar.notes != null)
            {
                int rapidIndex = 0;
                foreach (var ev in bar.notes)
                {
                    float x = BeatOffsetToTrackX(ev.beatOffset);
                    bool isRapid = ev.noteType == KyoshoNoteType.Eighth || ev.noteType == KyoshoNoteType.Triplet;

                    float y;
                    Color noteCol;
                    if (ev.noteType == KyoshoNoteType.Rest)
                    {
                        y = 0f;
                        noteCol = new Color(0.55f, 0.60f, 0.70f, 0.90f);
                    }
                    else if (isRapid)
                    {
                        bool highLane = (rapidIndex % 2) == 1;
                        y = highLane ? HighLaneY : LowLaneY;
                        noteCol = ev.noteType == KyoshoNoteType.Triplet
                            ? new Color(1.00f, 0.68f, 0.16f, 1f)
                            : new Color(0.28f, 0.94f, 0.56f, 1f);
                        rapidIndex++;
                    }
                    else
                    {
                        y = LowLaneY;
                        noteCol = new Color(0.22f, 0.84f, 1.00f, 1f);
                    }

                    AddNoteCard(ev.noteType, ev.beatOffset, x, y, noteCol, false);

                    if (hasStringsBonus && (ev.noteType == KyoshoNoteType.Quarter || ev.noteType == KyoshoNoteType.Eighth))
                    {
                        float extraOffset = ev.beatOffset + 0.5f;
                        if (extraOffset < 3.95f)
                        {
                            float ex = BeatOffsetToTrackX(extraOffset);
                            AddNoteCard(KyoshoNoteType.Sixteenth, extraOffset, ex, HighLaneY, KyoshoConstants.StringsMintColor, true);
                        }
                    }
                }
            }

            UpdateStaccatoCursor(0f);
        }

        public void SetStaccatoPhase(bool isFiringPhase, bool hasStringsBonus)
        {
            EnsureUI();
            _staccatoFiringPhase = isFiringPhase;

            if (isFiringPhase)
            {
                _staffCaption.text = hasStringsBonus
                    ? "▶ 탄막 발사 중!  [하단: 점프 회피]  [상단: 서기]  [★민트: 현악 16분 추가탄]"
                    : "▶ 탄막 발사 중!  [하단: 점프 회피]  [상단: 서서 대기 / 점프 금지]";
                _staffCaption.color = new Color(1.00f, 0.84f, 0.25f, 1f);
                _staffBg.color = new Color(0.11f, 0.08f, 0.05f, 0.92f);
                _playheadImage.color = new Color(1.00f, 0.35f, 0.28f, 0.95f);
            }
            else
            {
                _staffCaption.text = hasStringsBonus
                    ? "♪ 스타카토 예고 — 4박 후 발사!  [하단: 점프]  [상단: 서기]  [+★현악 16분]"
                    : "♪ 스타카토 예고 — 하단(점프 회피) / 상단(서서 대기 · 점프 금지)";
                _staffCaption.color = new Color(0.84f, 0.94f, 1.00f, 1f);
                _staffBg.color = new Color(0.06f, 0.08f, 0.14f, 0.90f);
                _playheadImage.color = new Color(1.00f, 0.86f, 0.20f, 0.92f);
            }
        }

        public void UpdateStaccatoCursor(float barBeatProgress0To4)
        {
            if (_playheadBar == null) return;
            float norm = Mathf.Clamp01(barBeatProgress0To4 / 4f);
            _playheadBar.anchoredPosition = new Vector2((norm - 0.5f) * TrackWidth, 0f);

            foreach (var entry in _noteIcons)
            {
                if (entry?.Rect == null) continue;
                float dt = barBeatProgress0To4 - entry.BeatOffset;
                bool activeNow = _staccatoFiringPhase && dt >= -0.14f && dt <= 0.24f;
                bool approaching = dt >= -0.45f && dt < -0.14f;
                bool alreadyPassed = _staccatoFiringPhase && dt > 0.24f;

                entry.Rect.localScale = activeNow ? Vector3.one * 1.24f : (approaching ? Vector3.one * 1.10f : Vector3.one);
                if (entry.Bg != null)
                {
                    Color c = entry.BaseBgColor;
                    if (activeNow)
                        c = Color.Lerp(c, Color.white, 0.52f);
                    else if (approaching)
                        c = Color.Lerp(c, Color.white, 0.25f);
                    else if (alreadyPassed)
                        c = new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 0.55f);
                    entry.Bg.color = c;
                }
            }
        }

        public void HideStaccatoScore()
        {
            if (_staffRoot != null)
                _staffRoot.gameObject.SetActive(false);
            ClearNoteIcons();
        }

        public void ShowFermataHold(bool show)
        {
            EnsureUI();
            _fermataBanner.gameObject.SetActive(show);
        }

        public void ShowVariationStatus(string message)
        {
            EnsureUI();
            bool hasMsg = !string.IsNullOrEmpty(message);
            _variationBanner.gameObject.SetActive(hasMsg);
            if (hasMsg)
                _variationText.text = message;
        }

        public void ResetVariationSlots(int count = 3)
        {
            EnsureUI();
            _variationSlotsRoot.gameObject.SetActive(true);

            int slotCount = Mathf.Min(count, _variationSlots.Count);
            for (int i = 0; i < _variationSlots.Count; i++)
            {
                var slot = _variationSlots[i];
                bool active = i < slotCount;
                slot.Root.gameObject.SetActive(active);
                if (!active) continue;

                slot.StepLabel.text = $"{i + 1}번째";
                slot.GlyphText.text = "?";
                slot.GlyphText.color = new Color(0.55f, 0.62f, 0.74f, 1f);
                slot.NameText.text = "대기";
                slot.NameText.color = new Color(0.65f, 0.70f, 0.80f, 1f);
                slot.Bg.color = new Color(0.08f, 0.10f, 0.16f, 0.88f);
                slot.Border.color = new Color(0.28f, 0.34f, 0.46f, 0.85f);
                slot.Root.localScale = Vector3.one;
            }
        }

        public void RevealVariationSlot(int stepIndex, int symbolIndex)
        {
            EnsureUI();
            if (stepIndex < 0 || stepIndex >= _variationSlots.Count) return;

            var slot = _variationSlots[stepIndex];
            slot.Root.gameObject.SetActive(true);

            int safeSym = Mathf.Clamp(symbolIndex, 0, KyoshoConstants.SymbolGlyphs.Length - 1);
            Color col = KyoshoConstants.SymbolBorderColors[safeSym];
            string glyph = KyoshoConstants.SymbolGlyphs[safeSym];
            string solfege = KyoshoConstants.SolfegeNames[safeSym];
            string symName = KyoshoConstants.SymbolNames[safeSym];

            slot.StepLabel.text = $"{stepIndex + 1}번째";
            slot.GlyphText.text = glyph;
            slot.GlyphText.color = col;
            slot.NameText.text = $"{solfege} · {ShortSymbolName(symName)}";
            slot.NameText.color = Color.white;
            slot.Bg.color = new Color(col.r * 0.20f, col.g * 0.20f, col.b * 0.20f, 0.92f);
            slot.Border.color = col;
            slot.Root.localScale = Vector3.one * 1.06f;
        }

        public void UpdateVariationInputProgress(int completedSteps)
        {
            if (_variationSlotsRoot == null) return;

            for (int i = 0; i < _variationSlots.Count; i++)
            {
                var slot = _variationSlots[i];
                if (!slot.Root.gameObject.activeSelf) continue;

                if (i < completedSteps)
                {
                    slot.StepLabel.text = $"{i + 1} ✓완료";
                    slot.Bg.color = new Color(0.10f, 0.26f, 0.16f, 0.85f);
                    slot.Border.color = new Color(0.30f, 0.90f, 0.50f, 0.95f);
                    slot.Root.localScale = Vector3.one * 0.96f;
                }
                else if (i == completedSteps)
                {
                    slot.StepLabel.text = $"▶ {i + 1}번째 목표";
                    slot.Border.color = new Color(1.00f, 0.88f, 0.22f, 1f);
                    slot.Root.localScale = Vector3.one * 1.10f;
                }
                else
                {
                    slot.StepLabel.text = $"{i + 1}번째";
                    slot.Root.localScale = Vector3.one;
                }
            }
        }

        public void HideVariationSequenceSlots()
        {
            if (_variationSlotsRoot != null)
                _variationSlotsRoot.gameObject.SetActive(false);
        }

        private static string ShortSymbolName(string fullName)
        {
            return fullName switch
            {
                "높은음자리표" => "높은음",
                "낮은음자리표" => "낮은음",
                _ => fullName
            };
        }

        private static float BeatOffsetToTrackX(float beatOffset)
        {
            return ((Mathf.Clamp(beatOffset, 0f, 4f) / 4f) - 0.5f) * TrackWidth;
        }

        private void AddNoteCard(KyoshoNoteType noteType, float beatOffset, float x, float y, Color col, bool isBonus)
        {
            Vector2 cardSize = noteType switch
            {
                KyoshoNoteType.Quarter => new Vector2(68f, 42f),
                KyoshoNoteType.Eighth => new Vector2(56f, 38f),
                KyoshoNoteType.Triplet => new Vector2(48f, 36f),
                KyoshoNoteType.Sixteenth => new Vector2(44f, 34f),
                _ => new Vector2(54f, 34f)
            };

            string text = noteType switch
            {
                KyoshoNoteType.Quarter => "● 4분",
                KyoshoNoteType.Eighth => "♪ 8분",
                KyoshoNoteType.Triplet => "▲ 3연",
                KyoshoNoteType.Sixteenth => isBonus ? "★ 16" : "★ 16분",
                _ => "// 쉼표"
            };

            var go = new GameObject($"Note_{noteType}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_playheadBar.parent, false);
            go.transform.SetSiblingIndex(Mathf.Max(0, _playheadBar.GetSiblingIndex()));

            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = cardSize;

            Color bgCol = new Color(col.r * 0.28f, col.g * 0.28f, col.b * 0.28f, 0.94f);
            var bg = go.GetComponent<Image>();
            bg.color = bgCol;

            var outline = new GameObject("Outline", typeof(RectTransform), typeof(Image));
            outline.transform.SetParent(go.transform, false);
            var ort = outline.GetComponent<RectTransform>();
            ort.anchorMin = new Vector2(0f, 0f);
            ort.anchorMax = new Vector2(1f, 0.16f);
            ort.offsetMin = Vector2.zero;
            ort.offsetMax = Vector2.zero;
            outline.GetComponent<Image>().color = col;

            var txtGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(go.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = noteType == KyoshoNoteType.Sixteenth ? 13 : 15;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.text = text;

            _noteIcons.Add(new NoteIconEntry
            {
                Rect = rt,
                Bg = bg,
                BeatOffset = beatOffset,
                BaseBgColor = bgCol
            });
        }

        private void ClearNoteIcons()
        {
            foreach (var entry in _noteIcons)
            {
                if (entry?.Rect != null) Destroy(entry.Rect.gameObject);
            }
            _noteIcons.Clear();
        }

        private void EnsureUI()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("KyoshoScorePreviewCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 46;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            BuildStaccatoPanel(canvasGo.transform);
            BuildPatternHintBanner(canvasGo.transform);
            BuildFermataBanner(canvasGo.transform);
            BuildVariationPanel(canvasGo.transform);
        }

        private void BuildStaccatoPanel(Transform parent)
        {
            var staffGo = new GameObject("StaccatoScoreRoot", typeof(RectTransform), typeof(Image));
            staffGo.transform.SetParent(parent, false);
            _staffRoot = staffGo.GetComponent<RectTransform>();
            _staffRoot.anchorMin = new Vector2(0.5f, 1f);
            _staffRoot.anchorMax = new Vector2(0.5f, 1f);
            _staffRoot.pivot = new Vector2(0.5f, 1f);
            _staffRoot.anchoredPosition = new Vector2(0f, -104f);
            _staffRoot.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            _staffBg = staffGo.GetComponent<Image>();
            _staffBg.color = new Color(0.06f, 0.08f, 0.14f, 0.90f);

            // 상단 캡션
            _staffCaption = CreateLabel(staffGo.transform, "Caption", new Vector2(0f, -18f), new Vector2(PanelWidth - 24f, 28f), 18, TextAlignmentOptions.Center, FontStyles.Bold);
            ((RectTransform)_staffCaption.transform).anchorMin = new Vector2(0.5f, 1f);
            ((RectTransform)_staffCaption.transform).anchorMax = new Vector2(0.5f, 1f);

            // 타임라인 트랙 컨테이너
            var trackGo = new GameObject("TrackArea", typeof(RectTransform));
            trackGo.transform.SetParent(staffGo.transform, false);
            var trackRt = trackGo.GetComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0.5f, 0.5f);
            trackRt.anchorMax = new Vector2(0.5f, 0.5f);
            trackRt.anchoredPosition = new Vector2(TrackOffsetX, -14f);
            trackRt.sizeDelta = new Vector2(TrackWidth, 108f);

            // 상단 / 하단 레인 가이드선 및 좌측 행동 뱃지
            CreateGuideLine(trackGo.transform, "HighLane", HighLaneY, TrackWidth, 2.5f, new Color(0.45f, 0.82f, 1.00f, 0.34f));
            CreateGuideLine(trackGo.transform, "LowLane", LowLaneY, TrackWidth, 2.5f, new Color(1.00f, 0.78f, 0.35f, 0.38f));

            var highTag = CreateLabel(staffGo.transform, "HighTag", new Vector2(-PanelWidth * 0.5f + 72f, HighLaneY - 14f), new Vector2(126f, 36f), 13, TextAlignmentOptions.Center, FontStyles.Bold);
            highTag.text = "상단\n(서서 대기)";
            highTag.color = new Color(0.65f, 0.88f, 1.00f, 0.95f);

            var lowTag = CreateLabel(staffGo.transform, "LowTag", new Vector2(-PanelWidth * 0.5f + 72f, LowLaneY - 14f), new Vector2(126f, 36f), 13, TextAlignmentOptions.Center, FontStyles.Bold);
            lowTag.text = "하단\n(점프 회피)";
            lowTag.color = new Color(1.00f, 0.82f, 0.42f, 0.98f);

            // 4박 세로 격자선 + 박자 번호
            for (int beat = 0; beat <= 4; beat++)
            {
                float bx = BeatOffsetToTrackX(beat);
                var gridGo = new GameObject($"BeatGrid_{beat}", typeof(RectTransform), typeof(Image));
                gridGo.transform.SetParent(trackGo.transform, false);
                var grt = gridGo.GetComponent<RectTransform>();
                grt.anchoredPosition = new Vector2(bx, 0f);
                grt.sizeDelta = new Vector2(beat == 0 || beat == 4 ? 2.5f : 1.5f, 94f);
                gridGo.GetComponent<Image>().color = new Color(0.85f, 0.92f, 1f, beat == 0 ? 0.45f : 0.24f);

                if (beat < 4)
                {
                    var bLabel = CreateLabel(trackGo.transform, $"BeatNum_{beat + 1}", new Vector2(bx + 18f, 48f), new Vector2(40f, 18f), 12, TextAlignmentOptions.Left, FontStyles.Bold);
                    bLabel.text = $"{beat + 1}박";
                    bLabel.color = new Color(0.75f, 0.85f, 0.98f, 0.85f);
                }
            }

            // 재생 커서 바
            var playheadGo = new GameObject("PlayheadBar", typeof(RectTransform), typeof(Image));
            playheadGo.transform.SetParent(trackGo.transform, false);
            _playheadBar = playheadGo.GetComponent<RectTransform>();
            _playheadBar.anchoredPosition = Vector2.zero;
            _playheadBar.sizeDelta = new Vector2(4f, 98f);
            _playheadImage = playheadGo.GetComponent<Image>();
            _playheadImage.color = new Color(1f, 0.86f, 0.2f, 0.92f);
        }

        private void BuildPatternHintBanner(Transform parent)
        {
            var go = new GameObject("PatternHintBanner", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            _hintBanner = go.GetComponent<RectTransform>();
            _hintBanner.anchorMin = new Vector2(0.5f, 1f);
            _hintBanner.anchorMax = new Vector2(0.5f, 1f);
            _hintBanner.pivot = new Vector2(0.5f, 1f);
            _hintBanner.anchoredPosition = new Vector2(0f, -104f);
            _hintBanner.sizeDelta = new Vector2(740f, 70f);
            _hintBg = go.GetComponent<Image>();
            _hintBg.color = new Color(0.05f, 0.08f, 0.14f, 0.92f);

            var accentGo = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
            accentGo.transform.SetParent(go.transform, false);
            var art = accentGo.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0f, 0.5f);
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = new Vector2(7f, 0f);
            _hintAccentBar = accentGo.GetComponent<Image>();

            var topBorderGo = new GameObject("TopBorder", typeof(RectTransform), typeof(Image));
            topBorderGo.transform.SetParent(go.transform, false);
            var tbrt = topBorderGo.GetComponent<RectTransform>();
            tbrt.anchorMin = new Vector2(0f, 1f);
            tbrt.anchorMax = new Vector2(1f, 1f);
            tbrt.pivot = new Vector2(0.5f, 1f);
            tbrt.anchoredPosition = Vector2.zero;
            tbrt.sizeDelta = new Vector2(0f, 2.5f);
            _hintTopBorder = topBorderGo.GetComponent<Image>();

            _hintTitle = CreateLabel(go.transform, "Title", new Vector2(-95f, 16f), new Vector2(510f, 26f), 20, TextAlignmentOptions.Left, FontStyles.Bold);
            _hintTimerLabel = CreateLabel(go.transform, "TimerLabel", new Vector2(248f, 16f), new Vector2(210f, 26f), 17, TextAlignmentOptions.Right, FontStyles.Bold);
            _hintTimerLabel.color = new Color(1f, 0.88f, 0.35f, 1f);
            _hintDetail = CreateLabel(go.transform, "Detail", new Vector2(6f, -11f), new Vector2(704f, 24f), 16, TextAlignmentOptions.Left, FontStyles.Normal);

            _hintGaugeRoot = new GameObject("GaugeBg", typeof(RectTransform), typeof(Image));
            _hintGaugeRoot.transform.SetParent(go.transform, false);
            var grt = _hintGaugeRoot.GetComponent<RectTransform>();
            grt.anchorMin = new Vector2(0f, 0f);
            grt.anchorMax = new Vector2(1f, 0f);
            grt.pivot = new Vector2(0.5f, 0f);
            grt.anchoredPosition = Vector2.zero;
            grt.sizeDelta = new Vector2(0f, 6f);
            var gbg = _hintGaugeRoot.GetComponent<Image>();
            gbg.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            gbg.color = new Color(0.14f, 0.17f, 0.24f, 0.95f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(_hintGaugeRoot.transform, false);
            var frt = fillGo.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            _hintGaugeFill = fillGo.GetComponent<Image>();
            _hintGaugeFill.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            _hintGaugeFill.type = Image.Type.Filled;
            _hintGaugeFill.fillMethod = Image.FillMethod.Horizontal;
            go.SetActive(false);
        }

        private void BuildFermataBanner(Transform parent)
        {
            var fGo = new GameObject("FermataBanner", typeof(RectTransform), typeof(Image));
            fGo.transform.SetParent(parent, false);
            _fermataBanner = fGo.GetComponent<RectTransform>();
            _fermataBanner.anchorMin = new Vector2(0.5f, 1f);
            _fermataBanner.anchorMax = new Vector2(0.5f, 1f);
            _fermataBanner.pivot = new Vector2(0.5f, 1f);
            _fermataBanner.anchoredPosition = new Vector2(0f, -104f);
            _fermataBanner.sizeDelta = new Vector2(540f, 76f);
            fGo.GetComponent<Image>().color = new Color(0.18f, 0.11f, 0.04f, 0.92f);

            _fermataText = CreateLabel(fGo.transform, "FermataText", Vector2.zero, Vector2.zero, 22, TextAlignmentOptions.Center, FontStyles.Bold);
            var rt = (RectTransform)_fermataText.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _fermataText.color = new Color(1.00f, 0.88f, 0.30f, 1f);
            _fermataText.text = "⌒ FERMATA\n초록 'SAFE 틈새' 안으로 대피하세요!";
        }

        private void BuildVariationPanel(Transform parent)
        {
            var vGo = new GameObject("VariationBanner", typeof(RectTransform), typeof(Image));
            vGo.transform.SetParent(parent, false);
            _variationBanner = vGo.GetComponent<RectTransform>();
            _variationBanner.anchorMin = new Vector2(0.5f, 1f);
            _variationBanner.anchorMax = new Vector2(0.5f, 1f);
            _variationBanner.pivot = new Vector2(0.5f, 1f);
            _variationBanner.anchoredPosition = new Vector2(0f, -104f);
            _variationBanner.sizeDelta = new Vector2(720f, 52f);
            vGo.GetComponent<Image>().color = new Color(0.06f, 0.10f, 0.18f, 0.92f);

            _variationText = CreateLabel(vGo.transform, "VariationText", Vector2.zero, Vector2.zero, 21, TextAlignmentOptions.Center, FontStyles.Bold);
            var vrt = (RectTransform)_variationText.transform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            _variationText.color = Color.white;

            // 3슬롯 변형 기억 카드
            var slotsGo = new GameObject("VariationSlotsRoot", typeof(RectTransform));
            slotsGo.transform.SetParent(parent, false);
            _variationSlotsRoot = slotsGo.GetComponent<RectTransform>();
            _variationSlotsRoot.anchorMin = new Vector2(0.5f, 1f);
            _variationSlotsRoot.anchorMax = new Vector2(0.5f, 1f);
            _variationSlotsRoot.pivot = new Vector2(0.5f, 1f);
            _variationSlotsRoot.anchoredPosition = new Vector2(0f, -182f);
            _variationSlotsRoot.sizeDelta = new Vector2(620f, 96f);

            _variationSlots.Clear();
            float[] slotXs = { -204f, 0f, 204f };
            for (int i = 0; i < 3; i++)
            {
                if (i > 0)
                {
                    var arrow = CreateLabel(slotsGo.transform, $"Arrow_{i}", new Vector2(slotXs[i] - 102f, 0f), new Vector2(36f, 36f), 24, TextAlignmentOptions.Center, FontStyles.Bold);
                    arrow.text = "→";
                    arrow.color = new Color(0.85f, 0.90f, 1f, 0.85f);
                }

                var cardGo = new GameObject($"Slot_{i + 1}", typeof(RectTransform), typeof(Image));
                cardGo.transform.SetParent(slotsGo.transform, false);
                var cardRt = cardGo.GetComponent<RectTransform>();
                cardRt.anchoredPosition = new Vector2(slotXs[i], 0f);
                cardRt.sizeDelta = new Vector2(172f, 88f);
                var cardBg = cardGo.GetComponent<Image>();
                cardBg.color = new Color(0.08f, 0.10f, 0.16f, 0.88f);

                var topBarGo = new GameObject("BorderBar", typeof(RectTransform), typeof(Image));
                topBarGo.transform.SetParent(cardGo.transform, false);
                var topRt = topBarGo.GetComponent<RectTransform>();
                topRt.anchorMin = new Vector2(0f, 1f);
                topRt.anchorMax = new Vector2(1f, 1f);
                topRt.pivot = new Vector2(0.5f, 1f);
                topRt.anchoredPosition = Vector2.zero;
                topRt.sizeDelta = new Vector2(0f, 5f);
                var topBarImg = topBarGo.GetComponent<Image>();
                topBarImg.color = new Color(0.30f, 0.36f, 0.50f, 1f);

                var stepLbl = CreateLabel(cardGo.transform, "Step", new Vector2(0f, 28f), new Vector2(160f, 22f), 14, TextAlignmentOptions.Center, FontStyles.Bold);
                stepLbl.color = new Color(0.95f, 0.90f, 0.55f, 1f);

                var glyphLbl = CreateLabel(cardGo.transform, "Glyph", new Vector2(0f, 1f), new Vector2(160f, 36f), 28, TextAlignmentOptions.Center, FontStyles.Bold);
                var nameLbl = CreateLabel(cardGo.transform, "Name", new Vector2(0f, -29f), new Vector2(164f, 22f), 15, TextAlignmentOptions.Center, FontStyles.Bold);

                _variationSlots.Add(new VariationSlotView
                {
                    Root = cardRt,
                    Bg = cardBg,
                    Border = topBarImg,
                    StepLabel = stepLbl,
                    GlyphText = glyphLbl,
                    NameText = nameLbl
                });
            }
        }

        private static void CreateGuideLine(Transform parent, string name, float y, float width, float thickness, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(width, thickness);
            go.GetComponent<Image>().color = color;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            return tmp;
        }
    }
}
