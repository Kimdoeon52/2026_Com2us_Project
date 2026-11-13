using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    // JunkShop_Map(LJS)에 있는 제작 · 수리 창 묶음을 현재 씬(예: Home_Workshop 1)으로 복사해 옴
    // 원본 씬은 잠깐 열어 복사만 하고 저장하지 않고 닫음 (원본 · 다른 팀 파일은 바뀌지 않음)
    internal static class CraftingSystemImport
    {
        internal const string SourceScenePath = "Assets/LJS/01. Scene/JunkShop_Map.unity";
        internal const string CraftingUiTypeName = "CraftingUIManager";

        // 서로 참조하는 묶음 — 하나라도 빠지면 복사본의 연결이 끊김 (씬 파일 참조 분석으로 확인한 최소 묶음)
        // ExclusiveWindowManager는 캐릭터 · 조명 · 고철장까지 끌고 와서 제외 (창 겹침 방지 기능만 빠짐)
        internal static readonly string[] RootNames =
        {
            "Canvas_Window",      // 제작창 · 수리창(FixWindow) · 상점창
            "CraftingManager",    // CraftingManager + CraftingUIManager (KDU 프리팹)
            "InventorySystem",    // InventoryHost · InventoryView
            "Canvas_Inventory",   // 제작할 때 왼쪽에 뜨는 인벤토리 그리드
            "Canvas_Equipment",   // 인벤토리가 참조하는 장비창
            "GoldSystem",         // GlobalGold (수리비)
        };

        internal static bool HasCraftingUi(Scene scene)
        {
            return JunkShopDialogueSetup.FindInScene<MonoBehaviour>(scene).Any(b => b != null && b.GetType().Name == CraftingUiTypeName);
        }

        // 이미 같은 이름이 있는 묶음은 건너뜀. 복사한 루트 수를 돌려줌 (-1 = 실패)
        internal static int Import(Scene target, StringBuilder log)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
            {
                log.AppendLine($"- 오류: 원본 씬이 없습니다 ({SourceScenePath})");
                return -1;
            }

            var existing = new HashSet<string>(target.GetRootGameObjects().Select(g => g.name));
            string[] wanted = RootNames.Where(n => !existing.Contains(n)).ToArray();
            foreach (string name in RootNames.Except(wanted)) log.AppendLine($"- {name}: 이미 있어 건너뜀");
            if (wanted.Length == 0) return 0;

            Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = new List<GameObject>();
                foreach (string name in wanted)
                {
                    GameObject root = source.GetRootGameObjects().FirstOrDefault(g => g.name == name);
                    if (root == null) log.AppendLine($"- 주의: 원본 씬에 '{name}'이 없음");
                    else roots.Add(root);
                }
                if (roots.Count == 0) return 0;

                // 여러 개를 한 번에 복제해야 서로 가리키던 참조가 복제본끼리 다시 이어짐 (Ctrl+D와 같은 방식, 프리팹 연결도 유지)
                Selection.objects = roots.ToArray();
                Unsupported.DuplicateGameObjectsUsingPasteboard();
                GameObject[] copies = Selection.gameObjects.Where(g => g.transform.parent == null && g.scene == source).ToArray();
                if (copies.Length != roots.Count)
                {
                    log.AppendLine($"- 오류: 복제 결과가 예상과 다릅니다 ({copies.Length}/{roots.Count}) — 아무것도 옮기지 않음");
                    foreach (GameObject copy in copies) Object.DestroyImmediate(copy);
                    return -1;
                }

                foreach (GameObject copy in copies)
                {
                    string original = roots.Select(r => r.name).FirstOrDefault(n => copy.name == n || copy.name.StartsWith(n + " ("));
                    if (original != null) copy.name = original;
                    SceneManager.MoveGameObjectToScene(copy, target);
                    log.AppendLine($"- 가져옴: {copy.name}{(PrefabUtility.IsPartOfPrefabInstance(copy) ? " (프리팹 연결 유지)" : "")}");
                }

                return copies.Length;
            }
            finally
            {
                Selection.objects = new Object[0];
                // 복제하느라 원본 씬이 '수정됨' 상태가 됐어도 저장하지 않고 닫음
                EditorSceneManager.CloseScene(source, true);
                SceneManager.SetActiveScene(target);
            }
        }
    }
}
