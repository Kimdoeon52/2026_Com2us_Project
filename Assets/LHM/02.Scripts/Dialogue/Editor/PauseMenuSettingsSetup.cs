using System.Linq;
using RealSteel.MainMenu;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RealSteel.Dialogue.EditorTools
{
    internal static class PauseMenuSettingsSetup
    {
        private const string Title = "ESC 메뉴 환경설정 창";
        private const string PrefabPath = "Assets/YJW/02.Prefabs/PauseMenu.prefab";
        private const string WindowName = "SettingsWindow";

        internal static bool IsApplied()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            PauseMenu menu = root != null ? root.GetComponentInChildren<PauseMenu>(true) : null;
            return menu != null && new SerializedObject(menu).FindProperty("_settingsWindow").objectReferenceValue is GameObject window
                   && window.GetComponent<GameSettingsWindow>() != null;
        }

        internal static void Setup()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                EditorUtility.DisplayDialog(Title, $"프리팹이 없습니다: {PrefabPath}", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(Title,
                    $"{PrefabPath} (YJW) 에 환경설정 창을 붙입니다.\n" +
                    "- 전체 · 배경음 · 효과음 음량 / 화면 모드 / 해상도 (1600×900 이상) / 수직 동기화\n" +
                    "- 메인 메뉴 설정과 같은 저장 키 (어느 쪽에서 바꿔도 이어짐)\n" +
                    "- PauseMenu의 '환경설정 창' 칸에 연결, 메뉴 글꼴 사용\n" +
                    (IsApplied() ? "- 이미 있어 다시 만듭니다\n" : "") +
                    "\n프리팹 파일을 바로 저장합니다 (Ctrl+Z 안 됨). 되돌리려면 git으로 프리팹을 되돌리세요.",
                    "적용", "취소"))
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                PauseMenu menu = root.GetComponentInChildren<PauseMenu>(true);
                var so = new SerializedObject(menu);
                var content = so.FindProperty("_content").objectReferenceValue as RectTransform;
                var panel = so.FindProperty("_panel").objectReferenceValue as GameObject;
                Transform parent = content != null ? content : panel != null ? panel.transform : null;
                if (parent == null)
                {
                    EditorUtility.DisplayDialog(Title, "PauseMenu의 Panel · Content 칸이 비어 있습니다.", "확인");
                    return;
                }

                foreach (Transform old in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == WindowName).ToArray())
                {
                    Object.DestroyImmediate(old.gameObject);
                }

                var window = new GameObject(WindowName, typeof(RectTransform));
                window.layer = parent.gameObject.layer;
                window.transform.SetParent(parent, false);
                window.transform.SetAsLastSibling();
                var rect = (RectTransform)window.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;

                var settings = window.AddComponent<GameSettingsWindow>();
                TMP_FontAsset font = root.GetComponentsInChildren<TMP_Text>(true).Select(t => t.font).FirstOrDefault(f => f != null);
                var settingsSo = new SerializedObject(settings);
                settingsSo.FindProperty("font").objectReferenceValue = font;
                settingsSo.ApplyModifiedPropertiesWithoutUndo();
                window.SetActive(false);

                so.FindProperty("_settingsWindow").objectReferenceValue = window;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{Title}] {PrefabPath} → {parent.name}/{WindowName} 연결 (글꼴 {(font != null ? font.name : "기본")}). Play → Esc → 환경설정");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
