using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RealSteel.Dialogue
{
    public class ShortcutHelpOverlay : MonoBehaviour
    {
        private const string KeyColor = "#F2A900";
        private const string SectionColor = "#9CC3FF";

        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private int sortingOrder = 300;
        [SerializeField, Range(1, 4)] private int columns = 3;
        [SerializeField] private bool showTestKeys = true;

        private GameObject root;
        private TMP_Text[] columnTexts;
        private TMP_Text titleText;
        private HashSet<string> present;
        private float openedAt;

        public bool IsOpen => root != null && root.activeSelf;

        private struct Entry
        {
            public string key;
            public string description;
        }

        private class Section
        {
            public string title;
            public readonly List<Entry> entries = new List<Entry>();

            public Section Add(string key, string description)
            {
                entries.Add(new Entry { key = key, description = description });
                return this;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool shift = keyboard.shiftKey.isPressed;
            if (IsOpen)
            {
                if (Time.unscaledTime - openedAt < 0.1f) return;
                if ((keyboard.homeKey.wasPressedThisFrame && !shift) || keyboard.escapeKey.wasPressedThisFrame) Close();
                return;
            }

            if (keyboard.homeKey.wasPressedThisFrame && !shift) Open();
        }

        private void OnDisable()
        {
            if (IsOpen) Close();
        }

        public void Open()
        {
            if (root == null) Build();

            List<Section> sections = Collect();
            Fill(sections);
            root.SetActive(true);
            openedAt = Time.unscaledTime;
            UiStack.Push(this);
            Debug.Log($"[Help] 단축키 안내 열림 ({sections.Sum(s => s.entries.Count)}개)");
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
            UiStack.Remove(this);
        }

        private List<Section> Collect()
        {
            present = new HashSet<string>(FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => b != null)
                .Select(b => b.GetType().Name));

            var list = new List<Section>();

            if (Has("RSSimpleCharacter"))
            {
                list.Add(new Section { title = "이동 · 카메라" }
                    .Add("WASD / 방향키", "이동")
                    .Add("Shift (누름)", "달리기")
                    .Add("Q / E", "카메라 45° 회전 (NPC · 문 앞에서는 꺼짐)")
                    .Add("R / F (누름)", "카메라 기울이기")
                    .Add("+ / - · 휠", "줌")
                    .Add("우클릭 드래그", "카메라 돌리기")
                    .Add("Shift + Home", "카메라 초기화"));
            }

            var field = new Section { title = "필드 상호작용 · 메뉴" };
            if (Has(nameof(PlayerInteractor))) field.Add("E", "NPC와 상호작용");
            if (Has(nameof(DoorInteractable))) field.Add("E", "문 들어가기 · 나가기 (문 앞)");
            MonoBehaviour scrap = Find("ScrapInteractor");
            if (scrap != null) field.Add(KeyName(ReadKey(scrap, "InteractKey", "_interactKey")), "고철 뒤지기");
            MonoBehaviour inventory = Find("InventoryToggle");
            if (inventory != null) field.Add(InventoryKeys(inventory), "인벤토리 / 인벤토리 + 제작창 (Esc 닫기)");
            // 단축키는 KDU ExclusiveWindowManager가 처리하고, 창은 LJS 상주 프리팹에 있을 때만 뜬다
            if (Has("ExclusiveWindowManager"))
            {
                if (Has("IsoMapSystemManager")) field.Add("M", "지도");
                if (Has("CalendarUiManager")) field.Add("O", "달력");
                if (Has("RankingUiManager")) field.Add("Shift + R", "랭킹");
            }
            if (Has("GlobalGold")) field.Add("Shift + S", "골드 수동 저장");
            if (Has("TimingBarMinigame")) field.Add("Space", "고철 미니게임 타이밍 맞추기");
            MonoBehaviour skill = Find("SkillWindowView");
            if (skill != null) field.Add(KeyName(ReadKey(skill, "ToggleKey", "_toggleKey")), "스킬창 (Esc 닫기)");
            if (Has("TutorialDirector")) field.Add("F10", "도움말 · 튜토리얼 다시 보기");
            if (Has("PauseMenu")) field.Add("Esc", "일시정지 메뉴 (다른 창 · 대화가 열려 있으면 그 창이 먼저)");
            field.Add("Home", "단축키 안내 (Home · Esc 닫기)");
            list.Add(field);

            if (Has(nameof(DialogueManager)))
            {
                list.Add(new Section { title = "대화" }
                    .Add("Space · Enter · 클릭", "다음 / 즉시 완성")
                    .Add("Esc", "스킵 (안 읽은 대사면 한 번 더)")
                    .Add("Ctrl (누름)", "읽은 대사 빨리감기")
                    .Add("Tab", "오토")
                    .Add("L", "백로그 (↑↓ · PgUp/PgDn · 휠)")
                    .Add("LOG · AUTO · SKIP 클릭", "대화창 오른쪽 버튼 (백로그 · 오토 · 스킵)"));

                list.Add(new Section { title = "선택 화면" }
                    .Add("1 ~ 9", "NPC 선택지 바로 고르기")
                    .Add("W/S · ↑↓", "NPC 선택지 · 난이도 이동")
                    .Add("A/D · ←→", "보스(지역) 이동")
                    .Add("E · Space · Enter", "결정")
                    .Add("Enter · Z · E", "예 / 아니요 결정 (Esc = 아니요)")
                    .Add("Esc", "나가기 · 뒤로 · 상점 닫기")
                    .Add("Z", "시즌 시작 전화 받기"));
            }

            // ======================== [TEST ONLY] 테스트 키 도움말 ========================
            // 아래는 전부 테스트 · 디버그 컴포넌트 안내 (해당 컴포넌트가 씬에 있을 때만 표시)
            // ==============================================================================
            if (!showTestKeys) return list;

            var test = new Section { title = "테스트 키" };
            if (Has(nameof(Step09_DialogueDebug)))
            {
                test.Add("1 ~ 5", "다음 토벌 난이도")
                    .Add("Shift + G", "대상 보스 전환")
                    .Add("Shift + B", "대상 보스 지역 시즌 1위 전환")
                    .Add("F1 / F2 / F3", "패배 / 승리 / 경매 정산 기록")
                    .Add("F4 / F5", "말 걸기 / 대사 기록 출력")
                    .Add("F6 / F7 / F8", "저장 / 다시 읽기 / 세이브 폴더")
                    .Add("F9 · Shift + F9", "순위 구간 올리기 · 내리기")
                    .Add("F10 / F11", "풀스크린 / 인터미션 대사")
                    .Add("F12 · Shift + F12", "기록 초기화 · 세이브 삭제 (테스트 패널이 있으면 F12는 패널)");
            }
            if (Has(nameof(Step112_MidnightTest)))
            {
                test.Add("Shift + T", "00시 처리")
                    .Add("Shift + U", "LJS 시계를 23시로")
                    .Add("Y · Shift + Y", "시즌 D-8로 · 마지막 날로")
                    .Add("U", "이벤트 대기열 보기");
            }
            if (Has(nameof(Step11_BossEntryTest))) test.Add("B", "테스트 보스 진입 (전투 중 N 패배 · M 승리)");
            if (Has(nameof(Step15_BubbleTest)))
            {
                test.Add("H · Shift + H", "귀가 처리 · 토벌 난이도 순환")
                    .Add("J · Shift + J", "관중 켜기/끄기 · 기본 파츠 전환")
                    .Add("Shift + K · Shift + L", "관중 말풍선 · 대기 목록");
            }
            if (Has("TutorialTest")) test.Add("Insert · Shift + Insert", "튜토리얼 화면 (←→ · Space · Esc) · 본 기록 지우기");
            if (Has("TutorialDirector")) test.Add("Ctrl + F10", "튜토리얼 초기화 (본 기록 전부 지우기)");
            SeasonResetHotkey seasonReset = FindFirstObjectByType<SeasonResetHotkey>(FindObjectsInactive.Include);
            if (seasonReset != null)
            {
                test.Add("Ctrl + " + seasonReset.ResetKey, "시즌 초기화 (프리시즌으로 · 시작일 지났으면 전화 다시)")
                    .Add("Ctrl + " + seasonReset.NextBossKey, "다음 보스 (지금 지역 시즌 1위 처리 → 다음 지역 시즌)");
            }
            if (Has(nameof(Step14_BarkTest))) test.Add("6 / 7 / 8 / 9 · 0", "전투 바크: 파츠 파괴 / 자막 T10 / 부위 파괴 / 그로기 (Shift = 체력 20%) · 기록 초기화");
            if (Has(nameof(Step18_TutorialTest))) test.Add("P / I", "가짜 지도 UI (T3) / 가짜 토벌 신청 UI (T9)");
            if (Has("WorkshopTestKit")) test.Add("F9", "제작 · 수리 테스트 재료 다시 지급");
            if (Has(nameof(Step10_InputTest))) test.Add("` (1 왼쪽)", "필드 ↔ 전투 입력 전환");
            if (Has("TestPanel")) test.Add("F12", "테스트 패널 (씬이동 · 파츠 · 골드 · 전투 · 일정 · 데이터)");
            if (TestConditionResetHotkeys.ActiveIn(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name))
            {
                test.Add("Ctrl + F9", "대화 조건 · 세이브 초기화")
                    .Add("Ctrl + F11", "시즌 초기화")
                    .Add("Ctrl + F12 · Ctrl + Shift + F12", "받을 시즌 포상 추가 (1위 · 3위)");
            }
            if (Has(nameof(SeasonRewardTestKeys))) test.Add("Ctrl + J · Ctrl + Shift + J", "LJS 분기 보상 처리 (플레이어 1위 · 10위 밖)").Add("Ctrl + G", "골드 · 받을 포상 보기");
            if (Has("DebugLeftArmSkillSwapper")) test.Add("Shift + Alt + 1 ~ 6", "왼팔 스킬 바꾸기 (CSH, 전투)");
            if (Has("FrameStepper")) test.Add("키패드 7 / 8 / 9", "일시정지 / 1프레임 전진 / 판정 박스 (NYH, 전투)");
            if (test.entries.Count > 0) list.Add(test);
            return list;
        }

        private bool Has(string typeName) => present.Contains(typeName);

        private static MonoBehaviour Find(string typeName)
        {
            return FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(b => b != null && b.GetType().Name == typeName);
        }

        private static KeyCode ReadKey(object target, string property, string field)
        {
            Type type = target.GetType();
            PropertyInfo info = type.GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
            if (info != null && info.PropertyType == typeof(KeyCode)) return (KeyCode)info.GetValue(target);
            FieldInfo fieldInfo = type.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return fieldInfo != null && fieldInfo.FieldType == typeof(KeyCode) ? (KeyCode)fieldInfo.GetValue(target) : KeyCode.None;
        }

        private static string InventoryKeys(object toggle)
        {
            FieldInfo modes = toggle.GetType().GetField("_modes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (modes?.GetValue(toggle) is Array array && array.Length > 0)
            {
                var keys = new List<string>();
                foreach (object mode in array)
                {
                    FieldInfo key = mode?.GetType().GetField("Key");
                    if (key != null && key.FieldType == typeof(KeyCode)) keys.Add(KeyName((KeyCode)key.GetValue(mode)));
                }
                if (keys.Count > 0) return string.Join(" / ", keys);
            }
            return "Tab";
        }

        private static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None: return "-";
                case KeyCode.Escape: return "Esc";
                case KeyCode.Return: return "Enter";
                case KeyCode.Alpha0: case KeyCode.Alpha1: case KeyCode.Alpha2: case KeyCode.Alpha3: case KeyCode.Alpha4:
                case KeyCode.Alpha5: case KeyCode.Alpha6: case KeyCode.Alpha7: case KeyCode.Alpha8: case KeyCode.Alpha9:
                    return ((int)key - (int)KeyCode.Alpha0).ToString();
                default: return key.ToString();
            }
        }

        private void Fill(List<Section> sections)
        {
            int total = sections.Sum(s => s.entries.Count + 2);
            int perColumn = Mathf.CeilToInt(total / (float)columns);
            var builders = Enumerable.Range(0, columns).Select(_ => new StringBuilder()).ToArray();
            int column = 0;
            int used = 0;

            foreach (Section section in sections)
            {
                int size = section.entries.Count + 2;
                if (used > 0 && used + size > perColumn && column < columns - 1)
                {
                    column++;
                    used = 0;
                }

                StringBuilder b = builders[column];
                b.Append($"<size=120%><b><color={SectionColor}>{section.title}</color></b></size>\n");
                foreach (Entry entry in section.entries)
                {
                    b.Append($"<b><color={KeyColor}>{entry.key}</color></b><pos=44%>{entry.description}\n");
                }
                b.Append('\n');
                used += size;
            }

            for (int i = 0; i < columnTexts.Length; i++) columnTexts[i].text = builders[i].ToString();
            titleText.text = $"단축키 안내  <size=60%><color=#9A9EA4>{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} · Home / Esc 닫기</color></size>";
        }

        private void Build()
        {
            if (font == null)
            {
                TMP_Text any = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.font != null);
                if (any != null) font = any.font;
            }

            root = new GameObject("[Help] ShortcutCanvas", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var dim = CreateRect("Dim", root.transform);
            Stretch(dim, Vector2.zero, Vector2.zero);
            dim.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.07f, 0.9f);

            titleText = CreateText("Title", root.transform, 40f);
            Stretch(titleText.rectTransform, new Vector2(80f, -130f), new Vector2(-80f, -50f));
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(80f, -130f);
            titleText.rectTransform.offsetMax = new Vector2(-80f, -50f);

            columnTexts = new TMP_Text[columns];
            for (int i = 0; i < columns; i++)
            {
                TMP_Text text = CreateText($"Column{i + 1}", root.transform, 24f);
                RectTransform rect = text.rectTransform;
                rect.anchorMin = new Vector2(i / (float)columns, 0f);
                rect.anchorMax = new Vector2((i + 1) / (float)columns, 1f);
                rect.offsetMin = new Vector2(80f, 60f);
                rect.offsetMax = new Vector2(-20f, -150f);
                text.alignment = TextAlignmentOptions.TopLeft;
                text.enableAutoSizing = true;
                text.fontSizeMin = 14f;
                text.fontSizeMax = 24f;
                columnTexts[i] = text;
            }

            root.SetActive(false);
        }

        private TMP_Text CreateText(string name, Transform parent, float size)
        {
            RectTransform rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = new Color32(0xE2, 0xE4, 0xE7, 0xFF);
            text.richText = true;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            int layer = LayerMask.NameToLayer("UI");
            if (layer >= 0) go.layer = layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
