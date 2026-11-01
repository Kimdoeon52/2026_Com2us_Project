using System.Collections.Generic;
using System.Linq;
using System.Text;
using Michsky.UI.MTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    public static class DialogueBoxMotionSetup
    {
        private const string AllowedFolder = "Assets/LHM/";
        private const string UndoName = "대화창 모션 적용";
        private const string PrefabPath = "Assets/Motion Titles Pack/Prefabs/MTP-2.prefab";
        private const string RootName = "DialogueBoxMotion (MTP-2)";
        private const string MaskPath = "Content/Mask Content";

        [MenuItem("Tools/Dialogue/대화창 모션 적용 (Motion Titles MTP-2)")]
        private static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.path.StartsWith(AllowedFolder))
            {
                EditorUtility.DisplayDialog(UndoName, $"Assets/LHM 안의 씬에서만 실행할 수 있습니다.\n현재 씬: {(string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path)}", "확인");
                return;
            }

            var manager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            DialogueBox dialogueBox = manager != null ? new SerializedObject(manager).FindProperty("dialogueBox").objectReferenceValue as DialogueBox : null;
            if (dialogueBox == null)
            {
                EditorUtility.DisplayDialog(UndoName, "DialogueManager 또는 연결된 DialogueBox가 없습니다. 9단계 배치를 먼저 실행하세요.", "확인");
                return;
            }

            var boxSo = new SerializedObject(dialogueBox);
            SerializedProperty motionProp = boxSo.FindProperty("motion");
            if (motionProp.objectReferenceValue != null)
            {
                EditorUtility.DisplayDialog(UndoName, $"이미 대화창 모션이 연결되어 있습니다 ({motionProp.objectReferenceValue.name}).\n다시 적용하려면 Ctrl+Z로 되돌리거나, 대화창 박스를 원래 자리로 옮기고 모션 오브젝트를 지운 뒤 실행하세요.", "확인");
                return;
            }

            var boxObject = boxSo.FindProperty("box").objectReferenceValue as GameObject;
            var boxRect = boxObject != null ? boxObject.transform as RectTransform : null;
            if (boxRect == null || boxRect.parent == null)
            {
                EditorUtility.DisplayDialog(UndoName, "DialogueBox의 Box 칸이 비어 있거나 UI 오브젝트가 아닙니다.", "확인");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(UndoName, $"프리팹을 찾지 못했습니다.\n{PrefabPath}", "확인");
                return;
            }

            List<string> clipped = FindOutside(boxRect);
            string clipNote = clipped.Count == 0
                ? "박스 밖으로 나가는 자식 UI 없음"
                : $"주의: 아래 UI는 박스 영역 밖으로 나가 있어 마스크에 잘릴 수 있습니다\n  {string.Join("\n  ", clipped.Take(8))}{(clipped.Count > 8 ? $"\n  … 외 {clipped.Count - 8}개" : "")}";

            if (!EditorUtility.DisplayDialog(UndoName, $"'{scene.name}' 씬의 대화창({boxObject.name})에 MTP-2 열기·닫기 모션을 적용합니다.\n- MTP-2 복사본을 대화창 자리에 만들고 (원본 프리팹은 수정 안 함)\n- 대화창 박스를 그 마스크 안으로 옮김\n- 예시 글자(Text 1~3)는 끔\n\n{clipNote}\n\n씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "적용", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[대화창 모션] {scene.path}\n");

            Transform parent = boxRect.parent;
            int siblingIndex = boxRect.GetSiblingIndex();

            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(root, UndoName);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = RootName;

            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = boxRect.anchorMin;
            rootRect.anchorMax = boxRect.anchorMax;
            rootRect.pivot = boxRect.pivot;
            rootRect.anchoredPosition = boxRect.anchoredPosition;
            rootRect.sizeDelta = boxRect.sizeDelta;
            rootRect.localScale = boxRect.localScale;
            rootRect.localRotation = boxRect.localRotation;
            root.transform.SetSiblingIndex(siblingIndex);
            log.AppendLine($"- {RootName} 생성 (대화창과 같은 위치·크기 {boxRect.rect.width:0}×{boxRect.rect.height:0})");

            Transform mask = root.transform.Find(MaskPath);
            if (mask == null)
            {
                Undo.RevertAllDownToGroup(group);
                EditorUtility.DisplayDialog(UndoName, $"MTP-2 안에서 '{MaskPath}'를 찾지 못했습니다. 프리팹 구조가 바뀌었을 수 있습니다.", "확인");
                return;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.StartsWith("Text ")) continue;
                Undo.RecordObject(child.gameObject, UndoName);
                child.gameObject.SetActive(false);
                log.AppendLine($"- 예시 글자 '{child.name}' 끔");
            }

            Undo.SetTransformParent(boxRect, mask, UndoName);
            Undo.RecordObject(boxRect, UndoName);
            boxRect.anchorMin = Vector2.zero;
            boxRect.anchorMax = Vector2.one;
            boxRect.offsetMin = Vector2.zero;
            boxRect.offsetMax = Vector2.zero;
            boxRect.localScale = Vector3.one;
            boxRect.localRotation = Quaternion.identity;
            boxRect.SetAsLastSibling();
            log.AppendLine($"- 대화창 박스 '{boxObject.name}'를 {RootName}/{MaskPath} 안으로 이동 (꽉 채움)");

            var style = root.GetComponent<StyleManager>();
            var styleSo = new SerializedObject(style);
            styleSo.FindProperty("playOnEnable").boolValue = false;
            styleSo.FindProperty("playOutAnimation").boolValue = false;
            styleSo.FindProperty("disableOnOut").boolValue = false;
            styleSo.FindProperty("loopAnimations").boolValue = false;
            SerializedProperty unscaled = styleSo.FindProperty("useUnscaledTime");
            if (unscaled != null) unscaled.boolValue = true;
            styleSo.ApplyModifiedPropertiesWithoutUndo();

            var motion = Undo.AddComponent<DialogueBoxMotion>(root);
            var motionSo = new SerializedObject(motion);
            motionSo.FindProperty("style").objectReferenceValue = style;
            motionSo.ApplyModifiedPropertiesWithoutUndo();

            Undo.RecordObject(dialogueBox, UndoName);
            motionProp.objectReferenceValue = motion;
            boxSo.ApplyModifiedProperties();
            log.AppendLine("- DialogueBox의 Motion 칸 연결 (열 때 In, 닫을 때 Out 끝난 뒤 숨김)");
            if (clipped.Count > 0) log.AppendLine($"- {clipNote.Replace("\n", "\n  ")}");

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → NPC와 대화해 열기·닫기 모션 확인.");
            Debug.Log(log.ToString());
        }

        private static List<string> FindOutside(RectTransform box)
        {
            var result = new List<string>();
            Rect bounds = WorldRect(box);
            bounds.xMin -= 1f;
            bounds.yMin -= 1f;
            bounds.xMax += 1f;
            bounds.yMax += 1f;

            foreach (RectTransform child in box.GetComponentsInChildren<RectTransform>(true))
            {
                if (child == box) continue;

                Rect rect = WorldRect(child);
                if (rect.width <= 0f || rect.height <= 0f) continue;
                if (rect.xMin < bounds.xMin || rect.yMin < bounds.yMin || rect.xMax > bounds.xMax || rect.yMax > bounds.yMax)
                {
                    result.Add(child.name);
                }
            }
            return result;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
