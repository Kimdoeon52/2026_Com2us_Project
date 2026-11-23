using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealSteel.Dialogue.EditorTools
{
    internal static class PauseMenuTestSceneSetup
    {
        private const string Title = "ESC 메뉴 테스트 씬";
        internal const string ScenePath = "Assets/LHM/01.Scene/PauseMenuTest.unity";
        private const string MapPath = "Assets/SYS/Prefabs/Auction/MAP_Auction.prefab";
        private const string CharacterPath = "Assets/SYS/Prefabs/TestCharacter.prefab";
        private const string CoreSystemPath = "Assets/LJS/04. Prefab/CoreSystemRoot.prefab";
        private const string PauseMenuPath = "Assets/YJW/02.Prefabs/PauseMenu.prefab";
        private const int PauseCanvasOrder = 200;

        internal static bool Exists()
        {
            return File.Exists(ScenePath);
        }

        internal static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            string[] required = { MapPath, CharacterPath, PauseMenuPath, DialoguePrefabBuilder.SystemPrefabPath };
            string missing = string.Join("\n", required.Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p) == null));
            if (missing.Length > 0)
            {
                EditorUtility.DisplayDialog(Title, $"필요한 프리팹이 없습니다.\n{missing}", "확인");
                return;
            }

            bool hasCore = AssetDatabase.LoadAssetAtPath<GameObject>(CoreSystemPath) != null;
            if (!EditorUtility.DisplayDialog(Title,
                    $"{ScenePath} 를 {(Exists() ? "다시 만듭니다 (기존 파일 덮어씀)" : "새로 만듭니다")}.\n" +
                    "- SYS MAP_Auction + TestCharacter\n" +
                    "- 대화 시스템 + NPC_Auctioneer (맵 SLOT 안)\n" +
                    (hasCore ? "- LJS CoreSystemRoot (게임 시계 · 달력 · 지도 — 메뉴 날짜 표시, Esc 양보 확인용)\n" : "") +
                    $"- YJW PauseMenu (전용 캔버스, 정렬 {PauseCanvasOrder})\n" +
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

            Instantiate(MapPath, scene, log);
            Instantiate(CharacterPath, scene, log);
            if (hasCore) Instantiate(CoreSystemPath, scene, log);

            SetupKit.BeginBatch();
            ScenePrefabBuilder.PlaceInActiveScene();
            foreach (string note in SetupKit.EndBatch()) log.AppendLine(note.TrimEnd());

            CreatePauseMenu(scene, log);
            EnsureEventSystem(scene, log);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            log.AppendLine("저장 완료. Play → 필드에서 Esc = 메뉴 / 경매사와 대화 · 선택지 · M(지도) 중 Esc = 메뉴 안 열림");
            Debug.Log(log.ToString());
            EditorUtility.DisplayDialog(Title, log.ToString(), "확인");
        }

        private static void CreatePauseMenu(Scene scene, StringBuilder log)
        {
            var canvasGo = new GameObject("[Test] PauseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            canvasGo.layer = LayerMask.NameToLayer("UI");

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = PauseCanvasOrder;
            canvasGo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var menu = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuPath), canvasGo.transform);
            var rect = (RectTransform)menu.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            log.AppendLine($"- {menu.name} → {canvasGo.name} (정렬 {PauseCanvasOrder}, 화면 전체)");
        }

        private static void EnsureEventSystem(Scene scene, StringBuilder log)
        {
            bool exists = scene.GetRootGameObjects().Any(r => r.GetComponentInChildren<EventSystem>(true) != null);
            if (exists)
            {
                log.AppendLine("- EventSystem: 이미 있음");
                return;
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            log.AppendLine("- EventSystem 생성");
        }

        private static void Instantiate(string path, Scene scene, StringBuilder log)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            log.AppendLine($"- {go.name}");
        }
    }
}
