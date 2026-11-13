using System;
using System.Linq;
using System.Text;
using Michsky.UI.Heat;
using RealSteel.Dialogue.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.MainMenu.EditorTools
{
    public static class MainMenuSetup
    {
        private const string Title = "메인 메뉴 (Heat UI)";
        internal const string ScenePath = "Assets/LHM/01.Scene/MainMenu.unity";
        private static readonly string[] FlowScenes = { "MainMenu", "OpeningCutscene", "JunkShop_Map" };

        internal static bool Exists()
        {
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
        }

        internal static void Setup()
        {
            if (!HeatKit.IsInstalled())
            {
                SetupKit.Dialog(Title, $"Heat UI 에셋을 찾지 못했습니다.\n{HeatKit.Root}", "확인");
                return;
            }

            bool sceneExists = Exists();
            if (!SetupKit.Dialog(Title,
                    "Heat UI로 메인 메뉴 씬을 만듭니다.\n" +
                    "- 스플래시 (아무 키나 눌러서 시작) → 홈: 새 게임 · 이어하기 · 설정 · 크레딧, Esc 종료 확인\n" +
                    "- 설정: 해상도 · 화면 모드 · 수직 동기화 · 전체 음량\n" +
                    $"- 데이터: {MainMenuAssets.DataFolder} (UI Manager · 크레딧 복사본, Heat 원본은 그대로)\n" +
                    $"- 한글 폰트: {MainMenuAssets.FontFolder} (없을 때만 만듦)\n" +
                    $"- 씬: {ScenePath} ({(sceneExists ? "있음 → 지우고 다시 만듦" : "새로 만듦")})\n" +
                    "씬 · 데이터 파일은 Ctrl+Z로 되돌릴 수 없습니다.",
                    sceneExists ? "다시 만들기" : "만들기", "취소"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var log = new StringBuilder($"[{Title}]\n");
            try
            {
                UIManager ui = MainMenuAssets.EnsureUIManager(log);
                CreditsPreset credits = MainMenuAssets.EnsureCredits(log);
                AssetDatabase.SaveAssets();

                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CreateCamera();
                MainMenuBuilder.Build(ui, credits, log);
                EditorSceneManager.SaveScene(scene, ScenePath);
                log.AppendLine($"- 씬 저장: {ScenePath}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                // 만들다 만 씬을 그대로 Play하면 연결 누락 오류가 이어서 나므로 빈 씬으로 바꿈 (씬 파일은 저장 전이라 남지 않음)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SetupKit.Dialog(Title, $"만드는 중 오류가 나서 중단했습니다 (씬은 저장하지 않음). 콘솔을 확인하세요.\n{e.Message}", "확인");
                return;
            }

            AppendBuildSettingsCheck(log);
            log.AppendLine("완료. Play → 아무 키 → 홈 화면. 새 게임은 OpeningCutscene으로 이동");
            Debug.Log(log.ToString());
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = MainMenuBuilder.Dark;
            cameraObject.AddComponent<AudioListener>();
        }

        // Build Settings는 ProjectSettings라 건드리지 않고 빠진 씬만 알려 줌
        private static void AppendBuildSettingsCheck(StringBuilder log)
        {
            var listed = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
                .ToHashSet();
            string[] missing = FlowScenes.Where(s => !listed.Contains(s)).ToArray();
            if (missing.Length == 0) return;

            log.AppendLine($"- 주의: Build Settings에 없는 씬: {string.Join(", ", missing)} — 없으면 새 게임 · 이어하기 버튼이 씬을 옮기지 못함 (File > Build Profiles에서 추가)");
        }
    }
}
