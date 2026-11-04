using System;
using System.Collections.Generic;
using RealSteel.Dialogue;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RealSteel.BossRace
{
    public class BossRaceSelectUI : MonoBehaviour
    {
        [Header("보스 목록 (지역 순서)")]
        [SerializeField] private List<BossRaceEntry> entries = new List<BossRaceEntry>();

        [Header("연결")]
        [SerializeField] private GameObject root;
        [SerializeField] private BossRaceCard[] cards = new BossRaceCard[0];
        [SerializeField] private Button backButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Image confirmImage;
        [SerializeField] private TMP_Text confirmText;

        [Header("상단")]
        [SerializeField] private TMP_Text seasonText;
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text routeHintText;

        [Header("선택한 보스")]
        [SerializeField] private TMP_Text detailNameText;
        [SerializeField] private TMP_Text detailSubText;
        [SerializeField] private TMP_Text detailDescriptionText;
        [SerializeField] private TMP_Text detailStatsText;

        [Header("표시")]
        [SerializeField] private string routeHint = "시즌 1위 달성 시 다음 지역 레이스 개방";
        [SerializeField, Min(0f)] private float lockHintSeconds = 1.8f;
        [SerializeField, Min(0f)] private float openInputDelay = 0.2f;

        private static readonly Color ConfirmOn = new Color32(0xF2, 0xA9, 0x00, 0xFF);
        private static readonly Color ConfirmOff = new Color32(0x3A, 0x3D, 0x42, 0xFF);
        private static readonly Color HintLocked = new Color32(0xE8, 0x74, 0x3B, 0xFF);
        private static readonly Color HintNormal = new Color32(0x9A, 0x9E, 0xA4, 0xFF);

        private IBossRaceProgress progress;
        private Action<string> onConfirm;
        private Action onCancel;
        private int selected = -1;
        private bool canCancel = true;
        private float openedAt;
        private float hintUntil;

        public bool IsOpen { get; private set; }
        public IReadOnlyList<BossRaceEntry> Entries => entries;

        private void Awake()
        {
            if (root == null) root = gameObject;

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                cards[i].Index = i;
                cards[i].Hovered += OnCardHovered;
                cards[i].Clicked += OnCardClicked;
            }

            if (backButton != null) backButton.onClick.AddListener(Cancel);
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (root != gameObject) root.SetActive(false);
        }

        private void OnDisable()
        {
            if (IsOpen) Close();
        }

        private void Update()
        {
            if (!IsOpen) return;

            if (hintUntil > 0f && Time.unscaledTime >= hintUntil)
            {
                hintUntil = 0f;
                SetRouteHint(routeHint, HintNormal);
            }

            if (Time.unscaledTime - openedAt < openInputDelay) return;

            Keyboard keyboard = Keyboard.current;
            bool left = keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame);
            bool right = keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame);
            bool confirm, cancel;

            InputModeController input = InputModeController.Current;
            if (input != null)
            {
                confirm = input.WasPressed(InputMode.Dialogue, "Select");
                cancel = input.WasPressed(InputMode.Dialogue, "Cancel");
            }
            else
            {
                confirm = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame);
                cancel = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            }

            if (left) Move(-1);
            else if (right) Move(1);
            else if (confirm) Confirm();
            else if (cancel) Cancel();
        }

        public bool Open(IBossRaceProgress source, Action<string> confirmed, Action cancelled = null, bool allowCancel = true)
        {
            if (IsOpen || source == null) return false;

            progress = source;
            onConfirm = confirmed;
            onCancel = cancelled;
            canCancel = allowCancel;
            if (backButton != null) backButton.gameObject.SetActive(allowCancel);

            Refresh();
            root.SetActive(true);
            IsOpen = true;
            openedAt = Time.unscaledTime;
            UiStack.Push(this);

            Debug.Log($"[BossRace] 보스 선택 열림 — {DescribeStates()}");
            return true;
        }

        public void Refresh()
        {
            if (progress == null) return;

            SetText(seasonText, $"D-{Mathf.Max(0, progress.SeasonDaysLeft)}");
            SetText(rankText, progress.RankText);
            SetText(scoreText, progress.ScoreText);
            SetRouteHint(routeHint, HintNormal);
            hintUntil = 0f;

            int firstOpen = -1;
            int lastSelectable = -1;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;

                bool hasEntry = i < entries.Count && entries[i] != null;
                cards[i].gameObject.SetActive(hasEntry);
                if (!hasEntry) continue;

                BossRaceCardState state = StateOf(i);
                cards[i].Bind(entries[i], state, progress.BestDifficulty(entries[i].bossId));

                if (state != BossRaceCardState.Locked) lastSelectable = i;
                if (state == BossRaceCardState.InProgress && firstOpen < 0) firstOpen = i;
            }

            bool keep = selected >= 0 && selected < cards.Length && cards[selected] != null && cards[selected].gameObject.activeSelf && cards[selected].IsSelectable;
            Select(keep ? selected : firstOpen >= 0 ? firstOpen : lastSelectable);
        }

        public BossRaceCardState StateOf(int index)
        {
            if (progress == null || index < 0 || index >= entries.Count) return BossRaceCardState.Locked;
            if (progress.IsRegionCleared(entries[index].bossId)) return BossRaceCardState.Cleared;

            bool previousCleared = index == 0 || progress.IsRegionCleared(entries[index - 1].bossId);
            return previousCleared ? BossRaceCardState.InProgress : BossRaceCardState.Locked;
        }

        private void Move(int direction)
        {
            if (cards.Length == 0) return;

            int next = selected;
            for (int step = 0; step < cards.Length; step++)
            {
                next += direction;
                if (next < 0 || next >= cards.Length) break;

                BossRaceCard card = cards[next];
                if (card == null || !card.gameObject.activeSelf) continue;
                if (card.IsSelectable)
                {
                    Select(next);
                    return;
                }

                card.Shake();
                ShowLockHint(next);
                return;
            }
        }

        private void Select(int index)
        {
            selected = index;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null && cards[i].gameObject.activeSelf) cards[i].SetSelected(i == selected);
            }

            RefreshDetail();
        }

        private void RefreshDetail()
        {
            bool valid = selected >= 0 && selected < entries.Count && cards[selected] != null && cards[selected].IsSelectable;

            if (confirmButton != null) confirmButton.interactable = valid;
            if (confirmImage != null) confirmImage.color = valid ? ConfirmOn : ConfirmOff;
            if (confirmText != null) confirmText.alpha = valid ? 1f : 0.4f;

            if (!valid)
            {
                SetText(detailNameText, "-");
                SetText(detailSubText, "");
                SetText(detailDescriptionText, "선택할 수 있는 보스가 없습니다.");
                SetText(detailStatsText, "");
                return;
            }

            BossRaceEntry entry = entries[selected];
            int best = progress.BestDifficulty(entry.bossId);
            int loses = progress.LosesOf(entry.bossId);
            bool hint = loses >= BossRaceDifficulty.HintLoseCount;

            SetText(detailNameText, entry.bossName);
            SetText(detailSubText, $"{entry.RegionText} · {entry.typeLabel}");
            SetText(detailDescriptionText, entry.description);
            SetText(detailStatsText,
                $"최고 클리어 <color=#FFFFFF><b>{BossRaceDifficulty.Label(best)}</b></color>      " +
                $"패배 <color=#FFFFFF><b>{loses}회</b></color>      " +
                $"기믹 힌트 <color=#FFFFFF><b>{(hint ? "개방" : $"잠김 ({loses}/{BossRaceDifficulty.HintLoseCount})")}</b></color>");
        }

        private void OnCardHovered(BossRaceCard card)
        {
            if (!IsOpen) return;
            if (card.IsSelectable) Select(card.Index);
        }

        private void OnCardClicked(BossRaceCard card)
        {
            if (!IsOpen) return;

            if (!card.IsSelectable)
            {
                card.Shake();
                ShowLockHint(card.Index);
                return;
            }

            if (selected == card.Index) Confirm();
            else Select(card.Index);
        }

        private void ShowLockHint(int index)
        {
            string previous = index > 0 && index - 1 < entries.Count ? entries[index - 1].RegionText : "이전 지역";
            SetRouteHint($"잠김 — {previous} 레이스 시즌 1위 달성 시 개방", HintLocked);
            hintUntil = Time.unscaledTime + lockHintSeconds;
            Debug.Log($"[BossRace] 잠긴 지역 선택 시도: {(index < entries.Count ? entries[index].RegionText : index.ToString())} (선택 불가)");
        }

        private void Confirm()
        {
            if (!IsOpen || selected < 0 || selected >= entries.Count || !cards[selected].IsSelectable) return;

            string bossId = entries[selected].bossId;
            Action<string> callback = onConfirm;
            Debug.Log($"[BossRace] 선택 확정: {entries[selected].bossName} ({bossId}) → 난이도 선택");
            Close();
            callback?.Invoke(bossId);
        }

        private void Cancel()
        {
            if (!IsOpen || !canCancel) return;

            Action callback = onCancel;
            Debug.Log("[BossRace] 길드 접수처로 돌아감");
            Close();
            callback?.Invoke();
        }

        private void Close()
        {
            IsOpen = false;
            if (root != null && root != gameObject) root.SetActive(false);
            UiStack.Remove(this);
            onConfirm = null;
            onCancel = null;
        }

        private void SetRouteHint(string value, Color color)
        {
            if (routeHintText == null) return;
            routeHintText.text = value;
            routeHintText.color = color;
        }

        private string DescribeStates()
        {
            var parts = new List<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                parts.Add($"{entries[i].RegionText} {entries[i].bossName}: {StateOf(i)}");
            }
            return string.Join(" / ", parts);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null) text.text = value ?? "";
        }
    }
}
