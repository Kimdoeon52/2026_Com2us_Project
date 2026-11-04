using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    public static class Step16_SceneSetup
    {
        private const string UndoName = "16단계 배치";

        internal static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!SetupKit.RequireLhmScene(scene, UndoName)) return;

            DialogueManager[] managers = Object.FindObjectsByType<DialogueManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (managers.Length == 0)
            {
                SetupKit.Dialog(UndoName, "DialogueManager가 없습니다. 9단계 배치를 먼저 실행하세요.", "확인");
                return;
            }

            if (!SetupKit.Dialog(UndoName, $"대사 데이터를 검사·패킹하고 '{scene.name}' 씬의 DialogueManager {managers.Length}개에 패킹 파일을 연결합니다.\n- {DialogueDataTools.PackPath}\n씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            if (!DialogueDataTools.TryPack(out string message))
            {
                SetupKit.Dialog(UndoName, message, "확인");
                return;
            }

            var pack = AssetDatabase.LoadAssetAtPath<TextAsset>(DialogueDataTools.PackPath);
            if (pack == null)
            {
                SetupKit.Dialog(UndoName, $"패킹 파일을 TextAsset으로 불러오지 못했습니다.\n{DialogueDataTools.PackPath}", "확인");
                return;
            }

            foreach (DialogueManager manager in managers)
            {
                var so = new SerializedObject(manager);
                so.FindProperty("packedData").objectReferenceValue = pack;
                so.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Step16 배치] {scene.path}\n- {message.Replace("\n", "\n  ")}\n- DialogueManager {managers.Length}개에 Packed Data 연결\n완료. Ctrl+S로 저장. 에디터에서 패킹 데이터로 확인하려면 DialogueManager의 Use Pack In Editor를 켜고 Play.");
        }

        internal static void CorruptPack()
        {
            string path = DialogueDataTools.PackPath;
            if (!File.Exists(path))
            {
                SetupKit.Dialog("패킹 파일 손상", $"패킹 파일이 없습니다. 먼저 데이터 패킹을 실행하세요.\n{path}", "확인");
                return;
            }

            if (!SetupKit.Dialog("패킹 파일 손상", $"폴백 확인용으로 패킹 파일 가운데 바이트를 바꿉니다.\n{path}\n원본 JSON은 건드리지 않습니다. 되돌리려면 대화 툴의 '데이터 패킹'을 다시 실행하세요.", "손상시키기", "취소"))
            {
                return;
            }

            byte[] bytes = File.ReadAllBytes(path);
            for (int i = bytes.Length / 2; i < bytes.Length / 2 + 16 && i < bytes.Length; i++) bytes[i] ^= 0x5A;
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.LogWarning($"[Step16] 패킹 파일 손상시킴: {path} — Play 시 '패킹 데이터 손상 → 원본 JSON' 로그가 나와야 합니다 (Use Pack In Editor 켜기).");
        }
    }
}
