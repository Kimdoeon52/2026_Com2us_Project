using System.Text;
using RealSteel.Dialogue;
using RealSteel.Dialogue.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Ui = RealSteel.BossRace.EditorTools.BossRaceSetup;

namespace RealSteel.BossRace.EditorTools
{
    public static class SeasonCallSetup
    {
        private const string UndoName = "시즌 시작 전화 배치";
        private const string CanvasName = "[SeasonCall] Canvas";
        private const string CallName = "[SeasonCall] SeasonRaceCall";

        private static readonly Color Panel = new Color32(0x17, 0x19, 0x1C, 0xF2);
        private static readonly Color Gold = new Color32(0xF2, 0xA9, 0x00, 0xFF);
        private static readonly Color TextMain = new Color32(0xF0, 0xF1, 0xF3, 0xFF);
        private static readonly Color TextSub = new Color32(0x9A, 0x9E, 0xA4, 0xFF);

        internal static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!SetupKit.RequireLhmScene(scene, UndoName)) return;

            var existing = Object.FindFirstObjectByType<SeasonRaceCall>(FindObjectsInactive.Include);
            if (existing != null)
            {
                SetupKit.Dialog(UndoName, $"이미 배치되어 있습니다. ({existing.name})\n다시 만들려면 {CallName}와 {CanvasName}를 지우고 실행하세요.", "확인");
                return;
            }

            var manager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            var selectUI = Object.FindFirstObjectByType<BossRaceSelectUI>(FindObjectsInactive.Include);
            var debug = Object.FindFirstObjectByType<Step09_DialogueDebug>(FindObjectsInactive.Include);
            if (manager == null || selectUI == null || debug == null)
            {
                SetupKit.Dialog(UndoName, "DialogueManager, 보스 레이스 선택 UI, Step09_DialogueDebug가 모두 필요합니다.\n9단계 배치와 '보스 레이스 선택 UI 배치'를 먼저 실행하세요.", "확인");
                return;
            }

            if (!SetupKit.Dialog(UndoName,
                    $"'{scene.name}' 씬에 시즌 시작 전화를 배치합니다.\n" +
                    $"- {CanvasName}: 전화 알림 창, 예/아니요 선택지\n" +
                    $"- {CallName}: 벨소리(코드로 생성), 흐름 관리\n" +
                    "- DialogueManager 대사 풀에 event_dialogues 확인\n" +
                    "- 00시 테스트(Step112)에 연결: 시즌이 끝나면 전화\n" +
                    "씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            BossRaceArt.Generate();
            Ui.font = Ui.FindFont();
            Ui.uiLayer = LayerMask.NameToLayer("UI");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[SeasonCall 배치] {scene.path}\n");

            var canvasObject = new GameObject(CanvasName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasObject, UndoName);
            if (Ui.uiLayer >= 0) canvasObject.layer = Ui.uiLayer;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform layout = Ui.Rect("Layout", canvasObject.transform);
            layout.anchorMin = layout.anchorMax = layout.pivot = new Vector2(0.5f, 0.5f);
            layout.sizeDelta = new Vector2(1280f, 720f);

            RectTransform ring = Ui.Place("RingPanel", layout, 440f, 70f, 400f, 120f);
            Ui.AddImage(ring, Panel);
            Ui.Sliced(Ui.AddImage(Ui.Place("Frame", ring, 0f, 0f, 400f, 120f), Gold), 1f);
            Image phone = Ui.AddImage(Ui.Place("Phone", ring, 26f, 22f, 76f, 76f), Gold);
            phone.sprite = BossRaceArt.Phone;
            phone.preserveAspect = true;
            phone.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            phone.rectTransform.anchoredPosition = new Vector2(26f + 38f, -22f - 38f);
            Ui.Text(ring, "Caller", 122f, 20f, 260f, 30f, "길드 접수처", 22f, TextMain, true, TextAlignmentOptions.MidlineLeft);
            Ui.Text(ring, "Message", 122f, 52f, 260f, 20f, "전화가 걸려 왔습니다...", 13f, TextSub, false, TextAlignmentOptions.MidlineLeft);
            TMP_Text hint = Ui.Text(ring, "AnswerHint", 122f, 78f, 260f, 24f, "[Z] 받기", 15f, Gold, true, TextAlignmentOptions.MidlineLeft);
            ring.gameObject.SetActive(false);
            log.AppendLine("- RingPanel (화면 위쪽 가운데, 평소에는 꺼짐)");

            RectTransform choiceRoot = Ui.Place("Choice", layout, 0f, 0f, 1f, 1f);
            var choice = choiceRoot.gameObject.AddComponent<DialogueChoice>();
            RectTransform choicePanel = Ui.Place("Panel", layout, 1010f, 372f, 200f, 104f);
            Ui.AddImage(choicePanel, Panel);
            Ui.Sliced(Ui.AddImage(Ui.Place("Frame", choicePanel, 0f, 0f, 200f, 104f), Gold), 1f);
            var buttons = new Button[2];
            var labels = new TMP_Text[2];
            string[] defaults = { "예", "아니요" };
            for (int i = 0; i < 2; i++)
            {
                RectTransform option = Ui.Place($"Option{i + 1}", choicePanel, 12f, 12f + i * 42f, 176f, 38f);
                Image hit = Ui.AddImage(option, new Color(1f, 1f, 1f, 0f), true);
                buttons[i] = option.gameObject.AddComponent<Button>();
                buttons[i].targetGraphic = hit;
                buttons[i].transition = Selectable.Transition.None;
                labels[i] = Ui.Text(option, "Label", 14f, 0f, 162f, 38f, defaults[i], 18f, TextMain, true, TextAlignmentOptions.MidlineLeft);
            }
            choicePanel.gameObject.SetActive(false);

            var choiceSo = new SerializedObject(choice);
            choiceSo.FindProperty("panel").objectReferenceValue = choicePanel.gameObject;
            SerializedProperty buttonsProp = choiceSo.FindProperty("buttons");
            SerializedProperty labelsProp = choiceSo.FindProperty("labels");
            buttonsProp.arraySize = 2;
            labelsProp.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                buttonsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                labelsProp.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
            }
            choiceSo.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("- Choice (예/아니요, 대화창 오른쪽 위, 평소에는 꺼짐)");

            var callObject = new GameObject(CallName);
            Undo.RegisterCreatedObjectUndo(callObject, UndoName);
            var audio = callObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var call = callObject.AddComponent<SeasonRaceCall>();
            var callSo = new SerializedObject(call);
            callSo.FindProperty("manager").objectReferenceValue = manager;
            callSo.FindProperty("selectUI").objectReferenceValue = selectUI;
            callSo.FindProperty("choice").objectReferenceValue = choice;
            callSo.FindProperty("progressSource").objectReferenceValue = debug;
            callSo.FindProperty("ringPanel").objectReferenceValue = ring.gameObject;
            callSo.FindProperty("phoneIcon").objectReferenceValue = phone.rectTransform;
            callSo.FindProperty("answerHintText").objectReferenceValue = hint;
            callSo.FindProperty("audioSource").objectReferenceValue = audio;
            callSo.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"- {CallName} (받기 키 Z, 벨소리는 코드 생성 — 효과음 파일을 Ring Clip에 넣으면 교체)");

            var poolAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/LHM/03.Data/Dialogue/event_dialogues.json");
            if (poolAsset != null)
            {
                SetupKit.AddPool(manager, poolAsset, log);
            }
            else
            {
                log.AppendLine("- 주의: event_dialogues.json을 찾지 못함 — 대사 풀에 연결되어 있는지 직접 확인");
            }

            foreach (Step112_MidnightTest test in Object.FindObjectsByType<Step112_MidnightTest>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var testSo = new SerializedObject(test);
                Undo.RecordObject(test, UndoName);
                testSo.FindProperty("seasonCall").objectReferenceValue = call;
                testSo.ApplyModifiedProperties();
                log.AppendLine($"- {test.name}: 시즌 종료 시 전화 연결");
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → Shift+Y(시즌 마지막 날) → T(00시) → 벨소리 → Z.");
            Debug.Log(log.ToString());
        }
    }
}
