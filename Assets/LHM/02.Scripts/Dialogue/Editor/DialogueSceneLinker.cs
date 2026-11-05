using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    internal static class DialogueSceneLinker
    {
        private const string PlaceTitle = "대화 시스템 프리팹 배치";
        private const string LinkTitle = "씬 연결";
        private static readonly string[] NpcSpeakers = { "receptionist", "junkdealer" };

        internal static void PlaceAndLink()
        {
            Scene scene = SceneManager.GetActiveScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePrefabBuilder.SystemPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(PlaceTitle, "프리팹이 없습니다. '프리팹 만들기'를 먼저 실행하세요.", "확인");
                return;
            }

            if (JunkShopDialogueSetup.FindInScene<DialogueManager>(scene).Any())
            {
                EditorUtility.DisplayDialog(PlaceTitle,
                    "이 씬에 이미 대화 시스템이 있습니다. 복사본을 프리팹으로 바꾸는 작업은 팀과 교체 대상을 정한 뒤 진행합니다.\n이미 프리팹이면 '씬 연결 다시 실행'을 쓰세요.", "확인");
                return;
            }

            GameObject player = JunkShopDialogueSetup.FindPlayer(scene);
            if (player == null)
            {
                EditorUtility.DisplayDialog(PlaceTitle, "이 씬에서 플레이어를 찾지 못했습니다.", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(PlaceTitle,
                    $"'{scene.name}' 씬에 대화 시스템 프리팹과 NPC를 배치하고 씬 연결을 실행합니다.\n플레이어: {player.name}\n씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[{PlaceTitle}] {scene.path}\n");

            Place(prefab, scene, player, log);

            Undo.SetCurrentGroupName(PlaceTitle);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Report(PlaceTitle, log);
        }

        internal static void Place(GameObject prefab, Scene scene, GameObject player, StringBuilder log)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, PlaceTitle);
            PlaceNpcs(scene, player, log);
            Link(instance.GetComponent<DialogueSceneBinder>(), scene, log);
        }

        internal static void LinkActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            DialogueSceneBinder binder = JunkShopDialogueSetup.FindInScene<DialogueSceneBinder>(scene).FirstOrDefault();
            if (binder == null)
            {
                EditorUtility.DisplayDialog(LinkTitle, "이 씬에 DialogueSceneBinder가 없습니다. 프리팹을 먼저 배치하세요.", "확인");
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[{LinkTitle}] {scene.path}\n");

            Link(binder, scene, log);

            Undo.SetCurrentGroupName(LinkTitle);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Report(LinkTitle, log);
        }

        private static void Link(DialogueSceneBinder binder, Scene scene, StringBuilder log)
        {
            GameObject player = JunkShopDialogueSetup.FindPlayer(scene);
            Camera camera = JunkShopDialogueSetup.FindGameplayCamera(scene, player);
            GameObject shopWindow = JunkShopDialogueSetup.FindShopWindow(scene);
            JunkShopDialogueSetup.CollectBridgeTargets(player, scene, out Behaviour[] movement, out Behaviour[] interactions, out GameObject[] prompts);
            Behaviour[] hotkeys = JunkShopDialogueSetup.UiHotkeysIn(scene);

            using (var so = new SerializedObject(binder))
            {
                if (camera != null) so.FindProperty("gameplayCamera").objectReferenceValue = camera;
                if (player != null) so.FindProperty("player").objectReferenceValue = player.transform;
                if (shopWindow != null) so.FindProperty("shopWindow").objectReferenceValue = shopWindow;
                SetIfFound(so, "movement", movement);
                SetIfFound(so, "interactions", interactions);
                SetIfFound(so, "hiddenPrompts", prompts);
                SetIfFound(so, "uiHotkeys", hotkeys);
                so.ApplyModifiedProperties();
            }

            if (camera != null && player != null) JunkShopDialogueSetup.DisableSceneShortcuts(scene, log);

            log.AppendLine($"- 카메라: {NameOf(camera)} / 플레이어: {NameOf(player)} / 상점 창: {NameOf(shopWindow)}");
            log.AppendLine($"- 이동 {movement.Length}개, 상호작용 {interactions.Length}개, 안내 {prompts.Length}개, UI 단축키 {hotkeys.Length}개");

            if (camera == null) log.AppendLine("- 오류: 플레이어 카메라를 찾지 못했습니다. Binder에 직접 넣으세요.");
            if (player == null) log.AppendLine("- 오류: 플레이어를 찾지 못했습니다. Binder에 직접 넣으세요.");
            if (!JunkShopDialogueSetup.FindInScene<TimeSystemManager>(scene).Any()) log.AppendLine("- 주의: 이 씬에 LJS TimeSystemManager가 없어 시간 정지·00시 처리가 동작하지 않을 수 있습니다.");
            if (!JunkShopDialogueSetup.FindInScene<EventSystem>(scene).Any()) log.AppendLine("- 주의: 이 씬에 EventSystem이 없어 마우스 클릭이 동작하지 않습니다.");
        }

        private static void SetIfFound(SerializedObject so, string field, Object[] values)
        {
            if (values.Length > 0) JunkShopDialogueSetup.SetArray(so.FindProperty(field), values);
        }

        private static void PlaceNpcs(Scene scene, GameObject player, StringBuilder log)
        {
            var placed = new List<GameObject>();
            foreach (string speaker in NpcSpeakers)
            {
                if (JunkShopDialogueSetup.FindInScene<NpcInteractable>(scene).Any(n => n.SpeakerId == speaker)) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePrefabBuilder.NpcPrefabPath(speaker));
                if (prefab == null)
                {
                    log.AppendLine($"- 주의: {speaker} NPC 프리팹이 없음");
                    continue;
                }

                var npc = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(npc, PlaceTitle);
                placed.Add(npc);
            }

            JunkShopDialogueSetup.PlaceNpcs(placed, player, new Dictionary<string, Pose>(), log);
        }

        private static string NameOf(Object value)
        {
            return value != null ? value.name : "(없음)";
        }

        private static void Report(string title, StringBuilder log)
        {
            log.AppendLine("Ctrl+S로 저장 후 Play.");
            Debug.Log(log.ToString());
            EditorUtility.DisplayDialog(title, log.ToString(), "확인");
        }
    }
}
