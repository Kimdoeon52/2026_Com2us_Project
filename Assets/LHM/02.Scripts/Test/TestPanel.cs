using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealSteel.BossRace;
using RealSteel.Cutscene;
using RealSteel.Dialogue;
using RealSteel.MainMenu;
using RealSteel.Reward;
using RealSteel.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealSteel.TestTools
{
    public class TestPanel : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Key toggleKey = Key.F12;
        [SerializeField] private int sortingOrder = 900;
        [SerializeField] private Vector2 windowSize = new Vector2(980f, 700f);

        private static readonly string[] MainScenes =
        {
            "Main_Junk_Shop", "Main_Home_Workshop", "Main_League_Lobby", "Main_Auction_Hall",
            "Main_Fight_Map_1", "Main_Fight_Map_2", "Main_Boss1", "MainMenu", "OpeningCutscene",
        };
        private static readonly string[] Difficulties = { "veryEasy", "easy", "normal", "hard", "hell" };
        private static readonly string[] DifficultyNames = { "아주 쉬움", "쉬움", "보통", "어려움", "헬" };
        private static readonly string[] RankBands = { "101+", "31-100", "11-30", "2-10", "1" };
        private static readonly Slot[] PartSlots = { Slot.LeftArm, Slot.RightArm, Slot.LeftLeg, Slot.RightLeg, Slot.Head };
        private static readonly string[] TabNames = { "씬이동", "파츠 · 부품", "골드", "전투", "일정", "데이터" };

        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color WindowColor = new Color(0.08f, 0.09f, 0.1f, 0.97f);
        private static readonly Color Accent = new Color(1f, 0.86f, 0.04f, 1f);
        private static readonly Color TabColor = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.035f);
        private static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.1f);
        private static readonly Color DisabledText = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color SubText = new Color(1f, 1f, 1f, 0.6f);

        private const float RowHeight = 54f;
        private const float RowGap = 8f;

        private Canvas canvas;
        private RectTransform content;
        private RectTransform popup;
        private TMP_Text statusText;
        private readonly List<Image> tabImages = new List<Image>();
        private int tab;
        private float y;
        private bool open;

        private int bossIndex;
        private int difficultyIndex = 2;
        private float newGameConfirmUntil;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || toggleKey == Key.None || !keyboard[toggleKey].wasPressedThisFrame) return;
            if (keyboard.ctrlKey.isPressed || keyboard.shiftKey.isPressed || keyboard.altKey.isPressed) return;
            SetOpen(!open);
        }

        private void OnDisable()
        {
            if (open) UiStack.Remove(this);
            open = false;
        }

        public void SetOpen(bool value)
        {
            if (value == open) return;
            open = value;

            if (open)
            {
                Build();
                EnsureEventSystem();
                canvas.gameObject.SetActive(true);
                UiStack.Push(this);
                ShowTab(tab);
            }
            else
            {
                ClosePopup();
                if (canvas != null) canvas.gameObject.SetActive(false);
                UiStack.Remove(this);
            }
        }

        // ── 탭 내용 ─────────────────────────────

        private void BuildSceneTab()
        {
            Header($"현재 씬: {SceneManager.GetActiveScene().name}");
            Dropdown("메인씬 이동", MainScenes, 0, i => LoadScene(MainScenes[i]));
            Buttons("현재 씬", ("다시 불러오기", () => LoadScene(SceneManager.GetActiveScene().name)));
            Note("에디터에서는 Build Settings에 없는 씬도 열림. 빌드에서는 등록된 씬만");
        }

        private void BuildPartsTab()
        {
            InventoryHost host = InventoryHost.Instance;
            if (host == null || host.Grid == null)
            {
                Header("이 씬에 KDU InventoryHost(InventorySystem)가 없습니다");
                return;
            }

            string durability = string.Join("  ", PartSlots.Select(s => $"{SlotName(s)} {host.GetDurability(s)}"));
            Header($"코어 Lv.{host.CoreLevel}{(host.CanSortie ? "" : " (불안정)")}   /   {durability}");

            Buttons("파츠", ("무작위 지급", () => Run(host.GiveRandomPart() ? "무작위 파츠 지급" : "지급 실패 (인벤토리 공간 확인)")));

            PartsDefinition[] parts = host.Catalog != null ? host.Catalog.Parts.Where(p => p != null).ToArray() : new PartsDefinition[0];
            if (parts.Length > 0)
            {
                Dropdown("파츠 골라 지급", parts.Select(p => $"{p.DisplayName} ({p.Id})").ToArray(), -1,
                    i => Run(host.GivePart(parts[i]) ? $"파츠 지급: {parts[i].DisplayName}" : "지급 실패 (인벤토리 공간 확인)"));
            }

            ComponentDefinition[] components = host.ComponentCatalog != null ? host.ComponentCatalog.Components.Where(c => c != null).ToArray() : new ComponentDefinition[0];
            if (components.Length > 0)
            {
                Dropdown("부품 골라 5개 지급", components.Select(c => $"{c.DisplayName} ({c.Id})").ToArray(), -1,
                    i => Run($"부품 지급: {components[i].DisplayName} × {host.GiveComponent(components[i], 5)}"));
            }

            Buttons("장착 파츠 내구도",
                ("전부 0으로", () =>
                {
                    foreach (Slot slot in PartSlots) host.SetDurability(slot, 0);
                    host.NotifyChanged();
                    Run("장착 파츠 내구도 0");
                }),
                ("전부 수리", () =>
                {
                    foreach (Slot slot in PartSlots) host.RepairSlot(slot);
                    Run("장착 파츠 전부 수리");
                }));

            Buttons("코어 · 키트",
                ("코어 복구", () =>
                {
                    host.RestoreCore();
                    Run("코어 복구");
                }),
                ("제작 · 수리 테스트 키트", () =>
                {
                    WorkshopTestKit kit = FindAnyObjectByType<WorkshopTestKit>(FindObjectsInactive.Include);
                    if (kit == null)
                    {
                        Run("이 씬에 WorkshopTestKit이 없습니다 (대화 툴 → 작업대 → 테스트 지급)");
                        return;
                    }
                    kit.Apply();
                    Run("WorkshopTestKit 지급 (부품 · 손상 장비 · 골드)");
                }));
        }

        private void BuildGoldTab()
        {
            if (!GlobalGoldWallet.IsAvailable)
            {
                Header("이 씬에 LSH GlobalGold가 없습니다");
                return;
            }

            GlobalGold gold = GlobalGold.Instance;
            int id = gold.mainCharacterdata[0].mainCharacterID;
            Header($"플레이어 골드: {gold.PlayerGold:N0}");

            Buttons("더하기",
                ("+1,000", () => AddGold(1000)),
                ("+10,000", () => AddGold(10000)),
                ("+100,000", () => AddGold(100000)));

            Buttons("빼기",
                ("-1,000", () => Run(gold.UseGold(id, 1000) ? "골드 -1,000" : "골드 부족")),
                ("0으로", () =>
                {
                    gold.UseGold(id, gold.PlayerGold);
                    Run("골드 0");
                }));

            Buttons("저장", ("지금 저장 (mainGold.json)", () =>
            {
                gold.SaveGame();
                Run("골드 저장");
            }));
        }

        private void BuildBattleTab()
        {
            BossProgressStore store = FindAnyObjectByType<BossProgressStore>(FindObjectsInactive.Include);
            BossRaceSelectUI list = FindAnyObjectByType<BossRaceSelectUI>(FindObjectsInactive.Include);
            string[] bosses = list != null && list.Entries.Count > 0
                ? list.Entries.Where(e => e != null).Select(e => e.bossId).ToArray()
                : new[] { "boss.wheel" };
            string[] bossNames = list != null && list.Entries.Count > 0
                ? list.Entries.Where(e => e != null).Select(e => $"{e.RegionText} {e.bossName}").ToArray()
                : new[] { "boss.wheel" };
            bossIndex = Mathf.Clamp(bossIndex, 0, bosses.Length - 1);
            string boss = bosses[bossIndex];

            Header(store != null
                ? $"{bossNames[bossIndex]}: 도전 {store.AttemptsOf(boss)}회 · 패배 {store.LosesOf(boss)}회 · 최고 클리어 {BestName(store.BestDifficulty(boss))} · 지역 1위 {(store.IsRegionCleared(boss) ? "O" : "X")}"
                : "이 씬에 BossProgressStore가 없습니다 (대화 시스템이 있는 필드 · 전투 씬)");

            Selector("대상 보스", bossNames, bossIndex, i => { bossIndex = i; ShowTab(tab); });
            Selector("난이도", DifficultyNames, difficultyIndex, i => difficultyIndex = i);

            if (store != null)
            {
                Buttons("토벌 기록",
                    ("패배", () => { store.RecordHunt("lose", boss, Difficulties[difficultyIndex]); Run($"{boss} 패배 기록"); }),
                    ("승리", () => { store.RecordHunt("win", boss, Difficulties[difficultyIndex]); Run($"{boss} 승리 기록"); }),
                    ("도전 +1", () => { store.AddAttempt(boss); Run($"{boss} 도전 +1"); }));
                Buttons("지역", ("시즌 1위 전환", () => { store.ToggleRegionCleared(boss); Run($"{boss} 지역 1위 전환"); }));
            }

            Buttons("대사 · 출발",
                ("보스 진입 대사", () =>
                {
                    BossEntryDialogue entry = FindAnyObjectByType<BossEntryDialogue>(FindObjectsInactive.Include);
                    if (entry == null) { Run("이 씬에 BossEntryDialogue가 없습니다"); return; }
                    SetOpen(false);
                    entry.Play(boss, Difficulties[difficultyIndex], store != null ? store.AttemptsOf(boss) : 0, store != null ? store.LosesOf(boss) : 0, null);
                }),
                ("토벌 신청 창", () =>
                {
                    HuntDeparture departure = FindAnyObjectByType<HuntDeparture>(FindObjectsInactive.Include);
                    if (departure == null) { Run("이 씬에 HuntDeparture가 없습니다 (길드 씬)"); return; }
                    SetOpen(false);
                    if (!departure.Open()) Debug.Log("[TestPanel] 토벌 신청 창을 열지 못했습니다 (프리시즌 등 — 콘솔 확인)");
                }));
        }

        private void BuildScheduleTab()
        {
            DialogueManager manager = Manager();
            DialogueDayProcessor processor = FindAnyObjectByType<DialogueDayProcessor>(FindObjectsInactive.Include);
            TimeSystemManager clock = Clock();
            DialogueVariables variables = manager != null && manager.IsReady ? manager.Variables : null;

            string date = clock != null ? $"{clock.year}.{clock.month:D2}.{clock.day:D2} {clock.hour:D2}:00" : "LJS 시계 없음";
            string season = variables != null ? $"시즌 {variables.Get("season.day")}일차 · 레이스 {variables.Get("race.id")} · 순위 구간 {variables.Get("rank.band")}" : "대화 시스템 없음";
            Header($"{date}   /   {season}   /   받을 포상 {SeasonRewardStore.Pending.Count}건");

            Buttons("시간",
                ("00시 처리", () =>
                {
                    if (processor == null) { Run("DialogueDayProcessor 없음"); return; }
                    processor.RunMidnight(1);
                    Run("00시 처리 (하루)");
                }),
                ("LJS 시계 23시로", () =>
                {
                    if (processor == null || processor.Clock == null) { Run("LJS 시계 연결 없음"); return; }
                    processor.Clock.SetHour(23);
                    Run("LJS 시계 23시");
                }));

            if (variables != null && processor != null)
            {
                int length = processor.SeasonLength;
                Buttons("시즌 일차",
                    ("D-8로", () => SetVariable(manager, "season.day", length - 8, $"시즌 일차 {length - 8} (다음 00시 = 종료 D-7)")),
                    ("마지막 날로", () => SetVariable(manager, "season.day", length, $"시즌 일차 {length} (다음 00시 = 시즌 종료 전화)")));
            }

            Buttons("분기 랭킹 보상 (LJS)",
                ("플레이어 1위로 처리", () => RunSeasonReward(true)),
                ("10위 밖으로 처리", () => RunSeasonReward(false)));

            Buttons("받을 포상 직접 추가",
                ("1위 10,000", () => { SeasonRewardStore.Add(1, 10000, $"테스트({DateTime.Now:HH:mm:ss})"); Run("받을 포상 +1위 10,000"); }),
                ("3위 6,000", () => { SeasonRewardStore.Add(3, 6000, $"테스트({DateTime.Now:HH:mm:ss})"); Run("받을 포상 +3위 6,000"); }));

            Buttons("시즌",
                ("시즌 초기화 (프리시즌)", () => { SeasonHotkey().ResetSeason(); Run("시즌 초기화"); }),
                ("다음 보스", () => { SeasonHotkey().NextBoss(); Run("시즌 다음 보스"); }));

            if (variables != null)
            {
                Buttons("순위 구간 (대사 변수)",
                    ("▲ 올리기", () => ShiftRank(manager, 1)),
                    ("▼ 내리기", () => ShiftRank(manager, -1)));
            }
        }

        private void BuildDataTab()
        {
            DialogueManager manager = Manager();
            Header($"세이브 폴더: {Application.persistentDataPath}");

            Buttons("대화",
                ("조건 · 세이브 초기화", () =>
                {
                    if (manager == null) { Run("DialogueManager 없음"); return; }
                    manager.ResetAll(true);
                    Run("대화 조건 · 세이브 초기화");
                }),
                ("대사 기록 출력", () =>
                {
                    if (manager == null || manager.History == null) { Run("DialogueManager 없음"); return; }
                    Debug.Log("[TestPanel] 본 대사 기록\n" + manager.History.Dump());
                    Run("본 대사 기록 → 콘솔");
                }),
                ("이벤트 대기열", () =>
                {
                    DialogueEventQueue queue = FindAnyObjectByType<DialogueEventQueue>(FindObjectsInactive.Include);
                    if (queue == null) { Run("DialogueEventQueue 없음"); return; }
                    Debug.Log($"[TestPanel] 이벤트 대기열: {queue.Dump()}");
                    Run("이벤트 대기열 → 콘솔");
                }));

            Buttons("본 기록",
                ("튜토리얼 초기화", () =>
                {
                    TutorialDirector director = TutorialDirector.Instance != null ? TutorialDirector.Instance : FindAnyObjectByType<TutorialDirector>(FindObjectsInactive.Include);
                    if (director == null) { Run("TutorialDirector 없음"); return; }
                    director.ResetAll();
                    Run("튜토리얼 본 기록 초기화");
                }),
                ("컷신 초기화", () =>
                {
                    CutscenePlayer.ResetSeen("opening");
                    Run("오프닝 컷신 본 기록 초기화");
                }),
                ("받을 포상 비우기", () =>
                {
                    SeasonRewardStore.DeleteAll();
                    Run("받을 포상 비움");
                }));

            Buttons("전체",
                ("새 게임 초기화", () =>
                {
                    if (Time.unscaledTime > newGameConfirmUntil)
                    {
                        newGameConfirmUntil = Time.unscaledTime + 3f;
                        Run("한 번 더 누르면 새 게임 초기화 (대화 · 보스 · 포상 · 본 기록 · 인벤토리, 3초 안에)");
                        return;
                    }
                    newGameConfirmUntil = 0f;
                    GameSaveReset.ResetForNewGame();
                    Run("새 게임 초기화 완료 — 씬을 다시 불러오세요");
                }),
                ("세이브 폴더 열기", () =>
                {
                    Application.OpenURL("file:///" + Application.persistentDataPath.Replace('\\', '/'));
                    Run("세이브 폴더 열기");
                }));
        }

        // ── 기능 ─────────────────────────────

        private void LoadScene(string sceneName)
        {
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SetOpen(false);
                SceneLoader.Load(sceneName);
                return;
            }

#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.FindAssets($"t:Scene {sceneName}")
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == sceneName && p.StartsWith("Assets/Scenes/MainScene"))
                ?? UnityEditor.AssetDatabase.FindAssets($"t:Scene {sceneName}")
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == sceneName);
            if (path != null)
            {
                SetOpen(false);
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
                Debug.Log($"[TestPanel] Build Settings에 없는 씬을 에디터 전용으로 엶: {path}");
                return;
            }
#endif
            Run($"'{sceneName}' 씬을 열 수 없습니다 (Build Settings 등록 필요)");
        }

        private void AddGold(int amount)
        {
            Run(GlobalGoldWallet.AddPlayerGold(amount) ? $"골드 +{amount:N0} (저장됨)" : "골드 지급 실패");
        }

        private void RunSeasonReward(bool top)
        {
            RankingSystemManager ranking = RankingSystemManager.Instance != null ? RankingSystemManager.Instance : FindAnyObjectByType<RankingSystemManager>(FindObjectsInactive.Include);
            if (ranking == null || ranking.seasonResult == null)
            {
                Run("LJS RankingSystemManager · seasonResult 없음 (CoreSystemRoot가 있는 씬)");
                return;
            }

            ranking.UpdatePlayerRecord("플레이어", 0, top ? 999999 : -1);
            int before = SeasonRewardStore.Pending.Count;
            ranking.seasonResult.ProcessSeasonReward(ranking.npcList);
            int added = SeasonRewardStore.Pending.Count - before;
            Run(added > 0 ? $"분기 보상 기록 +{added}건 → 접수원 포상 수령"
                : top ? "이번 시즌 포상이 이미 기록되어 있음 (중복 방지)"
                : "10위 밖 → 포상 없음");
        }

        private SeasonResetHotkey SeasonHotkey()
        {
            SeasonResetHotkey hotkey = FindAnyObjectByType<SeasonResetHotkey>(FindObjectsInactive.Include);
            return hotkey != null ? hotkey : gameObject.AddComponent<SeasonResetHotkey>();
        }

        private void SetVariable(DialogueManager manager, string key, int value, string message)
        {
            manager.Variables.Set(key, value);
            manager.LogState(message);
            manager.Save();
            Run(message);
        }

        private void ShiftRank(DialogueManager manager, int step)
        {
            DialogueVariables variables = manager.Variables;
            int current = Math.Max(0, Array.IndexOf(RankBands, variables.Get("rank.band")));
            int next = Mathf.Clamp(current + step, 0, RankBands.Length - 1);
            if (next == current)
            {
                Run($"순위 구간 {RankBands[current]} (끝)");
                return;
            }

            variables.Set("rank.band", RankBands[next]);
            variables.Set("rank.dailyChange", next - current);
            if (next >= Array.IndexOf(RankBands, "2-10")) variables.Set("rank.top10Reached", "yes");
            manager.LogState($"순위 구간 {RankBands[current]} → {RankBands[next]}");
            manager.Save();
            Run($"순위 구간 {RankBands[current]} → {RankBands[next]}");
        }

        private static DialogueManager Manager() => FindAnyObjectByType<DialogueManager>(FindObjectsInactive.Include);

        private static TimeSystemManager Clock() => TimeSystemManager.Instance != null ? TimeSystemManager.Instance : FindAnyObjectByType<TimeSystemManager>(FindObjectsInactive.Include);

        private static string BestName(int index) => index >= 0 && index < DifficultyNames.Length ? DifficultyNames[index] : "없음";

        private static string SlotName(Slot slot)
        {
            switch (slot)
            {
                case Slot.LeftArm: return "왼팔";
                case Slot.RightArm: return "오른팔";
                case Slot.LeftLeg: return "왼다리";
                case Slot.RightLeg: return "오른다리";
                case Slot.Head: return "머리";
                default: return slot.ToString();
            }
        }

        private void Run(string message)
        {
            Debug.Log($"[TestPanel] {message}", this);
            if (statusText != null) statusText.text = message;
            if (open) ShowTab(tab);
        }

        // ── UI 만들기 ─────────────────────────────

        private void Build()
        {
            if (canvas != null) return;

            var canvasGo = new GameObject("[TestPanel] Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var root = (RectTransform)canvasGo.transform;
            RectTransform dim = Rect("Dim", root);
            Stretch(dim);
            AddImage(dim, Dim, true);

            RectTransform window = Rect("Window", root);
            window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
            window.sizeDelta = windowSize;
            AddImage(window, WindowColor, true);

            RectTransform bar = Rect("TopBar", window);
            Band(bar, 0f, 5f);
            AddImage(bar, Accent, false);

            RectTransform title = Rect("Title", window);
            Band(title, 14f, 34f, 24f, 24f);
            AddText(title, "테스트 패널", 24f, Color.white, TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            RectTransform hint = Rect("Hint", window);
            Band(hint, 14f, 34f, 24f, 64f);
            AddText(hint, "F12 닫기", 16f, SubText, TextAlignmentOptions.Right);

            RectTransform close = Rect("Close", window);
            close.anchorMin = close.anchorMax = new Vector2(1f, 1f);
            close.pivot = new Vector2(1f, 1f);
            close.anchoredPosition = new Vector2(-16f, -14f);
            close.sizeDelta = new Vector2(36f, 34f);
            AddButton(close, "✕", 18f).onClick.AddListener(() => SetOpen(false));

            RectTransform tabs = Rect("Tabs", window);
            Band(tabs, 58f, 48f, 16f, 16f);
            float tabWidth = (windowSize.x - 32f) / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                RectTransform tabRect = Rect(TabNames[i], tabs);
                tabRect.anchorMin = new Vector2(0f, 0f);
                tabRect.anchorMax = new Vector2(0f, 1f);
                tabRect.pivot = new Vector2(0f, 0.5f);
                tabRect.anchoredPosition = new Vector2(i * tabWidth, 0f);
                tabRect.sizeDelta = new Vector2(tabWidth - 4f, 0f);
                Button button = AddButton(tabRect, TabNames[i], 20f);
                button.onClick.AddListener(() => ShowTab(index));
                tabImages.Add(button.GetComponent<Image>());
            }

            RectTransform line = Rect("TabLine", window);
            Band(line, 106f, 2f, 16f, 16f);
            AddImage(line, Accent, false);

            RectTransform viewport = Rect("Viewport", window);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(16f, 52f);
            viewport.offsetMax = new Vector2(-16f, -116f);
            AddImage(viewport, new Color(0f, 0f, 0f, 0.01f), true);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            RectTransform status = Rect("Status", window);
            status.anchorMin = new Vector2(0f, 0f);
            status.anchorMax = new Vector2(1f, 0f);
            status.pivot = new Vector2(0.5f, 0f);
            status.offsetMin = new Vector2(16f, 10f);
            status.offsetMax = new Vector2(-16f, 44f);
            AddImage(status, RowColor, false);
            RectTransform statusLabel = Rect("Text", status);
            Stretch(statusLabel, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            statusText = AddText(statusLabel, "버튼을 누르면 결과가 여기와 콘솔에 나옵니다", 17f, Accent, TextAlignmentOptions.Left);

            canvasGo.SetActive(false);
        }

        private void ShowTab(int index)
        {
            tab = index;
            ClosePopup();
            for (int i = 0; i < tabImages.Count; i++) tabImages[i].color = i == tab ? new Color(Accent.r, Accent.g, Accent.b, 0.35f) : TabColor;

            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            y = 0f;

            switch (tab)
            {
                case 0: BuildSceneTab(); break;
                case 1: BuildPartsTab(); break;
                case 2: BuildGoldTab(); break;
                case 3: BuildBattleTab(); break;
                case 4: BuildScheduleTab(); break;
                case 5: BuildDataTab(); break;
            }

            content.sizeDelta = new Vector2(0f, y);
        }

        private void Header(string text)
        {
            RectTransform rect = Rect("Header", content);
            Band(rect, y, 40f);
            AddText(rect, text, 18f, SubText, TextAlignmentOptions.Left).textWrappingMode = TextWrappingModes.Normal;
            y += 40f + RowGap;
        }

        private void Note(string text)
        {
            RectTransform rect = Rect("Note", content);
            Band(rect, y, 28f, 12f, 12f);
            AddText(rect, text, 15f, DisabledText, TextAlignmentOptions.Left);
            y += 28f + RowGap;
        }

        private RectTransform Row(string label)
        {
            RectTransform row = Rect(label, content);
            Band(row, y, RowHeight);
            AddImage(row, RowColor, false);
            y += RowHeight + RowGap;

            RectTransform mark = Rect("Mark", row);
            mark.anchorMin = Vector2.zero;
            mark.anchorMax = new Vector2(0f, 1f);
            mark.pivot = new Vector2(0f, 0.5f);
            mark.sizeDelta = new Vector2(4f, 0f);
            AddImage(mark, Accent, false);

            RectTransform text = Rect("Label", row);
            text.anchorMin = Vector2.zero;
            text.anchorMax = new Vector2(0.28f, 1f);
            text.offsetMin = new Vector2(18f, 0f);
            text.offsetMax = Vector2.zero;
            AddText(text, label, 20f, Color.white, TextAlignmentOptions.Left);
            return row;
        }

        private void Buttons(string label, params (string text, Action action)[] buttons)
        {
            RectTransform row = Row(label);
            float x = 0f;
            foreach ((string text, Action action) in buttons)
            {
                float width = Mathf.Max(120f, 22f + text.Length * 17f);
                RectTransform rect = Rect(text, row);
                rect.anchorMin = rect.anchorMax = new Vector2(0.28f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(x, 0f);
                rect.sizeDelta = new Vector2(width, 40f);
                AddButton(rect, text, 18f).onClick.AddListener(() => Safe(action, text));
                x += width + 10f;
            }
        }

        private void Selector(string label, string[] options, int index, Action<int> onChanged)
        {
            RectTransform row = Row(label);
            int current = Mathf.Clamp(index, 0, options.Length - 1);
            TMP_Text value = null;

            RectTransform prev = Rect("Prev", row);
            prev.anchorMin = prev.anchorMax = new Vector2(0.28f, 0.5f);
            prev.pivot = new Vector2(0f, 0.5f);
            prev.sizeDelta = new Vector2(44f, 40f);
            AddButton(prev, "◀", 18f).onClick.AddListener(() => Step(-1));

            RectTransform box = Rect("Value", row);
            box.anchorMin = box.anchorMax = new Vector2(0.28f, 0.5f);
            box.pivot = new Vector2(0f, 0.5f);
            box.anchoredPosition = new Vector2(52f, 0f);
            box.sizeDelta = new Vector2(320f, 40f);
            AddImage(box, ButtonColor, false);
            RectTransform valueRect = Rect("Text", box);
            Stretch(valueRect);
            value = AddText(valueRect, options[current], 18f, Color.white, TextAlignmentOptions.Center);

            RectTransform next = Rect("Next", row);
            next.anchorMin = next.anchorMax = new Vector2(0.28f, 0.5f);
            next.pivot = new Vector2(0f, 0.5f);
            next.anchoredPosition = new Vector2(380f, 0f);
            next.sizeDelta = new Vector2(44f, 40f);
            AddButton(next, "▶", 18f).onClick.AddListener(() => Step(1));

            void Step(int step)
            {
                current = (current + step + options.Length) % options.Length;
                value.text = options[current];
                Safe(() => onChanged(current), label);
            }
        }

        private void Dropdown(string label, string[] options, int index, Action<int> onPick)
        {
            RectTransform row = Row(label);
            RectTransform rect = Rect("Dropdown", row);
            rect.anchorMin = rect.anchorMax = new Vector2(0.28f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 40f);
            string shown = index >= 0 && index < options.Length ? options[index] : "선택…";
            Button button = AddButton(rect, $"{shown}   ▼", 18f);
            button.onClick.AddListener(() => OpenPopup(rect, options, i => Safe(() => onPick(i), label)));
        }

        private void OpenPopup(RectTransform anchor, string[] options, Action<int> onPick)
        {
            ClosePopup();
            popup = Rect("Popup", canvas.transform);
            const float itemHeight = 36f;
            float height = Mathf.Min(options.Length, 10) * itemHeight + 8f;

            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            popup.pivot = new Vector2(0f, 1f);
            popup.anchorMin = popup.anchorMax = new Vector2(0.5f, 0.5f);
            popup.position = corners[0];
            popup.sizeDelta = new Vector2(anchor.rect.width, height);
            AddImage(popup, new Color(0.12f, 0.13f, 0.15f, 1f), true);

            RectTransform viewport = Rect("Viewport", popup);
            Stretch(viewport, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            AddImage(viewport, new Color(0f, 0f, 0f, 0.01f), true);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform list = Rect("List", viewport);
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.sizeDelta = new Vector2(0f, options.Length * itemHeight);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = list;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                RectTransform item = Rect(options[i], list);
                Band(item, i * itemHeight, itemHeight - 2f);
                Button button = AddButton(item, options[i], 17f);
                button.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Left;
                button.GetComponentInChildren<TMP_Text>().margin = new Vector4(12f, 0f, 0f, 0f);
                button.onClick.AddListener(() =>
                {
                    ClosePopup();
                    onPick(index);
                });
            }
        }

        private void ClosePopup()
        {
            if (popup != null) Destroy(popup.gameObject);
            popup = null;
        }

        private void Safe(Action action, string label)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                if (statusText != null) statusText.text = $"'{label}' 실패: {e.Message}";
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Band(RectTransform rect, float top, float height, float left = 0f, float right = 0f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static Image AddImage(RectTransform rect, Color color, bool raycast)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private Button AddButton(RectTransform rect, string label, float size)
        {
            Image image = AddImage(rect, ButtonColor, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.95f, 0.6f, 1f);
            colors.pressedColor = Accent;
            button.colors = colors;

            RectTransform text = Rect("Text", rect);
            Stretch(text, new Vector2(6f, 0f), new Vector2(-6f, 0f));
            AddText(text, label, size, Color.white, TextAlignmentOptions.Center);
            return button;
        }

        private TMP_Text AddText(RectTransform rect, string value, float size, Color color, TextAlignmentOptions alignment)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }
    }
}
