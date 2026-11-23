using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    internal static class SeasonRewardTestSceneSetup
    {
        private const string Title = "시즌 보상 테스트 씬";
        internal const string ScenePath = "Assets/LHM/01.Scene/SeasonRewardTest.unity";
        private const string MapPath = "Assets/SYS/Prefabs/Lobby/MAP_Lobby.prefab";
        private const string CharacterPath = "Assets/SYS/Prefabs/TestCharacter.prefab";
        private const string CoreSystemPath = "Assets/LJS/04. Prefab/CoreSystemRoot.prefab";

        internal static bool Exists()
        {
            return File.Exists(ScenePath);
        }

        internal static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            string[] required = { MapPath, CharacterPath, CoreSystemPath, DialoguePrefabBuilder.SystemPrefabPath, ScenePrefabBuilder.NpcPath("receptionist") };
            string missing = string.Join("\n", required.Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p) == null));
            if (missing.Length > 0)
            {
                EditorUtility.DisplayDialog(Title, $"필요한 프리팹이 없습니다.\n{missing}", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(Title,
                    $"{ScenePath} 를 {(Exists() ? "다시 만듭니다 (기존 파일 덮어씀)" : "새로 만듭니다")}.\n" +
                    "- SYS MAP_Lobby + TestCharacter\n" +
                    "- 대화 시스템 + NPC_Receptionist (맵 SLOT 안)\n" +
                    "- LJS CoreSystemRoot (랭킹 · 분기 보상 · 골드 · 뉴스 · 시계)\n" +
                    "- 시즌 보상 테스트 키 (화면 왼쪽 위 안내)\n" +
                    "- EventSystem (없을 때만)\n" +
                    "\n씬을 만든 뒤 바로 저장하고 엽니다.",
                    "만들기", "취소"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var log = new StringBuilder($"[{Title}] {ScenePath}\n");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Directional Light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            PauseMenuTestSceneSetup.Instantiate(MapPath, scene, log);
            PauseMenuTestSceneSetup.Instantiate(CharacterPath, scene, log);
            PauseMenuTestSceneSetup.Instantiate(CoreSystemPath, scene, log);

            SetupKit.BeginBatch();
            ScenePrefabBuilder.PlaceInActiveScene();
            foreach (string note in SetupKit.EndBatch()) log.AppendLine(note.TrimEnd());

            var keys = new GameObject("[Test] SeasonRewardTestKeys", typeof(SeasonRewardTestKeys));
            SceneManager.MoveGameObjectToScene(keys, scene);
            log.AppendLine($"- {keys.name}");

            PauseMenuTestSceneSetup.EnsureEventSystem(scene, log);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            log.AppendLine("저장 완료. Play → Ctrl+J (LJS 분기 보상, 1위) → 뉴스 확인 → 접수원 E → 포상 수령 → Ctrl+G로 골드 확인");
            Debug.Log(log.ToString());
            EditorUtility.DisplayDialog(Title, log.ToString(), "확인");
        }
    }
}
