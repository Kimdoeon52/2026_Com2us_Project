using System;
using System.Collections.Generic;
using RealSteel.Dialogue;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RealSteel.BossRace
{
    public class BossDifficultySelectUI : MonoBehaviour
    {
        private static readonly BodyPart[] Parts = { BodyPart.Head, BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg };
        private static readonly Color TextSub = new Color32(0x9A, 0x9E, 0xA4, 0xFF);

        [Header("색")]
        [SerializeField] private Color accentColor = new Color32(0xF2, 0xA9, 0x00, 0xFF);
        [SerializeField] private Color partGood = new Color32(0x7F, 0xD3, 0xA0, 0xFF);
        [SerializeField] private Color partLow = new Color32(0xF2, 0x8A, 0x3C, 0xFF);
        [SerializeField] private Color partBroken = new Color32(0xE0, 0x4E, 0x4E, 0xFF);

        [Header("난이도 (위에서부터)")]
        [SerializeField] private List<DifficultyEntry> difficulties = new List<DifficultyEntry>();

        [Header("연결")]
        [SerializeField] private GameObject root;
        [SerializeField] private DifficultyRow[] rows = new DifficultyRow[0];
        [SerializeField] private Button backButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button maintenanceButton;

        [Header("상단")]
        [SerializeField] private TMP_Text seasonText;
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text scoreText;

        [Header("보스")]
        [SerializeField] private TMP_Text regionTagText;
        [SerializeField] private Image bossIcon;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private TMP_Text bossTagsText;
        [SerializeField] private GameObject targetPartsLabel;
        [SerializeField] private RectTransform[] partChips = new RectTransform[0];
        [SerializeField] private TMP_Text[] partChipTexts = new TMP_Text[0];
        [SerializeField] private TMP_Text loseText;
        [SerializeField] private TMP_Text loseHintText;

        [Header("선택한 난이도")]
        [SerializeField] private TMP_Text selectedNameText;
        [SerializeField] private TMP_Text selectedDescriptionText;
        [SerializeField] private TMP_Text clearScoreText;
        [SerializeField] private TMP_Text bestRecordText;

        [Header("파츠 내구도 (머리·왼팔·오른팔·왼다리·오른다리)")]
        [SerializeField] private RectTransform[] partFills = new RectTransform[0];
        [SerializeField] private TMP_Text[] partValueTexts = new TMP_Text[0];

        [Header("입력")]
        [SerializeField, Min(0f)] private float openInputDelay = 0.2f;
        [SerializeField, Min(0f)] private float chipGap = 6f;

        private IBossRaceProgress progress;
        private IRobotPartStatus partStatus;
        private BossRaceEntry boss;
        private Action<string> onStart;
        private Action onBack;
        private int selected;
        private float openedAt;

        public bool IsOpen { get; private set; }

        public event Action MaintenanceRequested;

        private void Awake()
        {
            if (root == null) root = gameObject;

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;
                rows[i].Index = i;
                rows[i].Hovered += row => { if (IsOpen) Select(row.Index); };
                rows[i].Clicked += OnRowClicked;
            }

            if (backButton != null) backButton.onClick.AddListener(Back);
            if (startButton != null) startButton.onClick.AddListener(StartRace);
            if (maintenanceButton != null) maintenanceButton.onClick.AddListener(RequestMaintenance);
            if (root != gameObject) root.SetActive(false);
        }

        private void OnDisable()
        {
            if (IsOpen) Close();
        }

        public bool Open(IBossRaceProgress source, BossRaceEntry target, IRobotPartStatus parts, string initialDifficulty, Action<string> started, Action back, string backLabel = null)
        {
            if (IsOpen || source == null || target == null) return false;

            if (backButton != null && !string.IsNullOrEmpty(backLabel))
            {
                TMP_Text label = backButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = $"<   {backLabel}";
            }

            progress = source;
            boss = target;
            partStatus = parts;
            onStart = started;
            onBack = back;

            root.SetActive(true);
            IsOpen = true;
            openedAt = Time.unscaledTime;
            UiStack.Push(this);

            RefreshHeader();
            RefreshBoss();
            RefreshParts();

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;
                bool has = i < difficulties.Count;
                rows[i].gameObject.SetActive(has);
                if (has) rows[i].Bind(difficulties[i], ColorOf(i));
            }

            int initial = difficulties.FindIndex(d => d.id == initialDifficulty);
            Select(initial >= 0 ? initial : Mathf.Min(2, difficulties.Count - 1));

            Debug.Log($"[Difficulty] 난이도 선택 열림 — {boss.RegionText} {boss.bossName}");
            return true;
        }

        private void Update()
        {
            if (!IsOpen || Time.unscaledTime - openedAt < openInputDelay) return;

            bool up, down, confirm, cancel;
            InputModeController input = InputModeController.Current;
            Keyboard keyboard = Keyboard.current;

            if (input != null)
            {
                up = input.WasPressed(InputMode.Dialogue, "Up");
                down = input.WasPressed(InputMode.Dialogue, "Down");
                confirm = input.WasPressed(InputMode.Dialogue, "Select");
                cancel = input.WasPressed(InputMode.Dialogue, "Cancel");
            }
            else
            {
                up = keyboard != null && (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame);
                down = keyboard != null && (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame);
                confirm = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame);
                cancel = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            }

            if (up) Select(Mathf.Max(0, selected - 1));
            else if (down) Select(Mathf.Min(difficulties.Count - 1, selected + 1));
            else if (confirm) StartRace();
            else if (cancel) Back();
        }

        private void Select(int index)
        {
            if (index < 0 || index >= difficulties.Count) return;

            selected = index;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null && rows[i].gameObject.activeSelf) rows[i].SetSelected(i == selected);
            }

            DifficultyEntry entry = difficulties[selected];
            int best = progress.BestDifficulty(boss.bossId);
            int order = BossRaceDifficulty.IndexOf(entry.id);

            SetText(selectedNameText, entry.label);
            if (selectedNameText != null) selectedNameText.color = ColorOf(selected);
            SetText(selectedDescriptionText, entry.description);
            SetText(clearScoreText, entry.scoreLabel);
            SetText(bestRecordText, order >= 0 && best >= order ? "클리어" : "기록 없음");
        }

        private void OnRowClicked(DifficultyRow row)
        {
            if (!IsOpen) return;
            if (row.Index == selected) StartRace();
            else Select(row.Index);
        }

        private void RefreshHeader()
        {
            SetText(seasonText, $"D-{Mathf.Max(0, progress.SeasonDaysLeft)}");
            SetText(rankText, progress.RankText);
            SetText(scoreText, progress.ScoreText);
        }

        private void RefreshBoss()
        {
            SetText(regionTagText, $"{boss.RegionText} 보스");
            SetText(bossNameText, boss.bossName);
            SetText(bossTagsText, boss.tags);
            if (bossIcon != null)
            {
                bossIcon.sprite = boss.icon;
                bossIcon.enabled = boss.icon != null;
            }

            string[] parts = string.IsNullOrWhiteSpace(boss.targetParts)
                ? new string[0]
                : boss.targetParts.Split(new[] { ',', '/', '·' }, StringSplitOptions.RemoveEmptyEntries);
            if (targetPartsLabel != null) targetPartsLabel.SetActive(parts.Length > 0);

            float x = partChips.Length > 0 && partChips[0] != null ? partChips[0].anchoredPosition.x : 0f;
            for (int i = 0; i < partChips.Length; i++)
            {
                if (partChips[i] == null) continue;
                bool has = i < parts.Length;
                partChips[i].gameObject.SetActive(has);
                if (!has || i >= partChipTexts.Length || partChipTexts[i] == null) continue;

                TMP_Text text = partChipTexts[i];
                text.text = parts[i].Trim();
                float width = text.GetPreferredValues(text.text).x + 24f;
                partChips[i].sizeDelta = new Vector2(width, partChips[i].sizeDelta.y);
                partChips[i].anchoredPosition = new Vector2(x, partChips[i].anchoredPosition.y);
                x += width + chipGap;
            }

            int loses = progress.LosesOf(boss.bossId);
            bool hint = loses >= BossRaceDifficulty.HintLoseCount;
            SetText(loseText, $"이 보스 상대 패배 <color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>{loses}회</color>");
            SetText(loseHintText, hint ? "기믹 힌트 개방됨 — 다음 진입 때 접수원이 알려 줍니다" : $"{BossRaceDifficulty.HintLoseCount}패 시 기믹 힌트 개방 (난이도 무관)");
        }

        private void RefreshParts()
        {
            for (int i = 0; i < Parts.Length; i++)
            {
                RectTransform fill = i < partFills.Length ? partFills[i] : null;
                TMP_Text value = i < partValueTexts.Length ? partValueTexts[i] : null;

                if (partStatus == null || !partStatus.TryGetDurability(Parts[i], out float ratio))
                {
                    SetFill(fill, 0f, TextSub);
                    if (value != null)
                    {
                        value.text = "-";
                        value.color = TextSub;
                    }
                    continue;
                }

                ratio = Mathf.Clamp01(ratio);
                Color color = ratio <= 0f ? partBroken : ratio < 0.5f ? partLow : partGood;
                SetFill(fill, ratio, color);
                if (value != null)
                {
                    value.text = ratio <= 0f ? "파괴" : $"{Mathf.RoundToInt(ratio * 100f)}%";
                    value.color = color;
                }
            }
        }

        private static void SetFill(RectTransform fill, float ratio, Color color)
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(ratio, fill.anchorMax.y);
            Image image = fill.GetComponent<Image>();
            if (image != null) image.color = color;
        }

        private void StartRace()
        {
            if (!IsOpen || selected < 0 || selected >= difficulties.Count) return;

            string id = difficulties[selected].id;
            Action<string> callback = onStart;
            Debug.Log($"[Difficulty] 레이스 시작 — {boss.bossName} / {difficulties[selected].label} ({id})");
            Close();
            callback?.Invoke(id);
        }

        private void Back()
        {
            if (!IsOpen) return;

            Action callback = onBack;
            Debug.Log("[Difficulty] 보스 선택으로 돌아감");
            Close();
            callback?.Invoke();
        }

        private void RequestMaintenance()
        {
            Debug.Log("[Difficulty] 정비하기 — 수리 UI 연결 전 (MaintenanceRequested 이벤트)");
            MaintenanceRequested?.Invoke();
        }

        private void Close()
        {
            IsOpen = false;
            if (root != null && root != gameObject) root.SetActive(false);
            UiStack.Remove(this);
            onStart = null;
            onBack = null;
        }

        private Color ColorOf(int index)
        {
            int order = index < difficulties.Count ? BossRaceDifficulty.IndexOf(difficulties[index].id) : -1;
            return order >= 0 && order < BossRaceDifficulty.Colors.Length ? BossRaceDifficulty.Colors[order] : Color.gray;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null) text.text = value ?? "";
        }
    }
}
