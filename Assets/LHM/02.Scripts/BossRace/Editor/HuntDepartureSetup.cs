using System.Linq;
using System.Text;
using RealSteel.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.BossRace.EditorTools
{
    public static class HuntDepartureSetup
    {
        public const string RootName = "[Hunt] HuntDeparture";
        private const string UndoName = "토벌 출발 배치";

        internal static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!EditorUtility.DisplayDialog(UndoName,
                    $"'{scene.name}' 씬에 {RootName}을 배치합니다.\n" +
                    "길드 접수원 E → 토벌 신청 → 이번 시즌 보스 난이도 선택 → 보스 진입 대사 →\n" +
                    "BattleManager.SetPreBattleDifficulty(난이도) → 전투 씬 이동\n" +
                    "씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[토벌 출발 배치] {scene.path}\n");

            bool ok = Create(scene, log);

            Undo.CollapseUndoOperations(group);
            if (ok) EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine(ok ? "완료. Ctrl+S로 저장 후 Play → 접수원 E → 토벌 신청." : "배치하지 않았습니다.");
            Debug.Log(log.ToString());
        }

        internal static bool Create(Scene scene, StringBuilder log)
        {
            if (Find<HuntDeparture>(scene) != null)
            {
                log.AppendLine($"- {RootName}이 이미 있어 건너뜀");
                return false;
            }

            var manager = Find<DialogueManager>(scene);
            var menu = Find<NpcChoiceMenu>(scene);
            var bossList = Find<BossRaceSelectUI>(scene);
            var difficulty = Find<BossDifficultySelectUI>(scene);
            var entry = Find<BossEntryDialogue>(scene);
            MonoBehaviour progress = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .FirstOrDefault(b => b is IBossRaceProgress);

            string missing = string.Join(", ", new (Object value, string name)[]
            {
                (manager, "DialogueManager"), (menu, "NpcChoiceMenu"), (bossList, "BossRaceSelectUI"),
                (difficulty, "BossDifficultySelectUI"), (entry, "BossEntryDialogue"), (progress, "진행 정보(IBossRaceProgress)"),
            }.Where(p => p.value == null).Select(p => p.name));

            if (!string.IsNullOrEmpty(missing))
            {
                log.AppendLine($"- 실패: 이 씬에 {missing} 없음 (대화 시스템·보스 선택·난이도 선택 배치가 먼저 필요)");
                return false;
            }

            var go = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            var departure = go.AddComponent<HuntDeparture>();

            var so = new SerializedObject(departure);
            so.FindProperty("manager").objectReferenceValue = manager;
            so.FindProperty("menu").objectReferenceValue = menu;
            so.FindProperty("bossList").objectReferenceValue = bossList;
            so.FindProperty("difficultySelect").objectReferenceValue = difficulty;
            so.FindProperty("entryDialogue").objectReferenceValue = entry;
            so.FindProperty("progressSource").objectReferenceValue = progress;
            so.ApplyModifiedPropertiesWithoutUndo();

            string scenePath = so.FindProperty("defaultBattleScene").stringValue;
            bool inBuild = EditorBuildSettings.scenes.Any(s => s.enabled && s.path.Replace(".unity", "").EndsWith(scenePath));
            log.AppendLine($"- {RootName} 생성: 진행 정보 '{progress.name}', 전투 씬 '{scenePath}'{(inBuild ? " (빌드 설정 등록됨)" : " — 주의: 빌드 설정에 없음, Play 중 이동 불가")}");
            log.AppendLine("- 보스별로 다른 전투 씬을 쓰려면 Battle Scenes 목록에 보스 ID와 씬 이름 추가");
            return true;
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();
        }
    }
}
