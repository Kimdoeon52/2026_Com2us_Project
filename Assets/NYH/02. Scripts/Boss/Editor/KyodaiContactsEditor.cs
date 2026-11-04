using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// KyodaiContacts 인스펙터 + Scene 뷰 핸들 (2026-11-04).
///   · 쿄다이를 선택하고 메뉴 NYH/쿄다이/접촉(못 뚫기·먼지) 붙이기 → 팔마다 손목(못 뚫게+먼지)·팔꿈치(먼지만) 접촉점을 만든다
///   · KyodaiContacts를 선택하면 Scene 뷰에서 접촉점마다 화살표(위치)·원(반지름)으로 조절. 바닥·물체를 뚫으면 빨간 구
/// </summary>
[CustomEditor(typeof(KyodaiContacts))]
public class KyodaiContactsEditor : Editor
{
    [MenuItem("NYH/쿄다이/접촉(못 뚫기·먼지) 붙이기")]
    private static void AddToBoss()
    {
        var boss = Object.FindAnyObjectByType<KyodaiBossController>();
        if (boss == null) { EditorUtility.DisplayDialog("쿄다이", "씬에 쿄다이(KyodaiBossController)가 없습니다.", "확인"); return; }
        var contacts = boss.GetComponent<KyodaiContacts>();
        if (contacts == null) contacts = Undo.AddComponent<KyodaiContacts>(boss.gameObject);
        if (contacts.Points.Count == 0) FillDefaults(contacts);
        Selection.activeObject = contacts;
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
    }

    // 팔마다: 손목 = 못 뚫게 + 먼지, 팔꿈치 = 먼지만. 반지름은 아래팔 길이로 대충 — Scene 뷰에서 맞춘다
    private static void FillDefaults(KyodaiContacts contacts)
    {
        var so = new SerializedObject(contacts);
        so.FindProperty("boss").objectReferenceValue = contacts.Boss;
        var list = so.FindProperty("points");
        list.ClearArray();
        foreach (var limb in contacts.Boss.Limbs)
        {
            Transform lower = contacts.FindBone(limb.LowerBone);
            Transform end = contacts.FindBone(limb.EndBone);
            if (end == null) continue;
            float forearm = lower != null ? Vector3.Distance(lower.position, end.position) : 1f;
            Add(list, limb.PartId, limb.EndBone, forearm * 0.35f, true);
            if (lower != null) Add(list, "", limb.LowerBone, forearm * 0.3f, false);
        }
        so.ApplyModifiedProperties();
    }

    private static void Add(SerializedProperty list, string partId, string bone, float radius, bool solid)
    {
        list.InsertArrayElementAtIndex(list.arraySize);
        var e = list.GetArrayElementAtIndex(list.arraySize - 1);
        e.FindPropertyRelative("partId").stringValue = partId;
        e.FindPropertyRelative("bone").stringValue = bone;
        e.FindPropertyRelative("offset").vector3Value = Vector3.zero;
        e.FindPropertyRelative("radius").floatValue = Mathf.Max(0.05f, radius);
        e.FindPropertyRelative("solid").boolValue = solid;
        e.FindPropertyRelative("effects").boolValue = true;
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "초록 구 = 못 뚫는 점, 노란 구 = 먼지만, 빨간 구 = 지금 바닥·물체를 뚫고 있음.\n" +
            "Scene 뷰에서 화살표로 위치, 원으로 반지름을 맞추세요. 주먹이면 손가락 마디 쪽, 손바닥이면 손바닥 면에 두면 됩니다.\n" +
            "못 뚫게는 모션 창 편집 미리보기와 게임에서 적용되고, 먼지·흔들림은 게임(플레이)에서만 나옵니다.",
            MessageType.None);
        DrawDefaultInspector();
        var contacts = (KyodaiContacts)target;
        if (contacts.Boss != null && GUILayout.Button("팔마다 기본 접촉점 다시 만들기 (지금 목록 지움)"))
        {
            Undo.RecordObject(contacts, "기본 접촉점");
            FillDefaults(contacts);
        }
    }

    private void OnSceneGUI()
    {
        var contacts = (KyodaiContacts)target;
        var so = new SerializedObject(contacts);
        var list = so.FindProperty("points");
        for (int i = 0; i < list.arraySize; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            Transform bone = contacts.FindBone(e.FindPropertyRelative("bone").stringValue);
            if (bone == null) continue;
            var offset = e.FindPropertyRelative("offset");
            var radius = e.FindPropertyRelative("radius");
            Vector3 p = KyodaiContacts.PointWorld(bone, offset.vector3Value, 1f);

            Handles.Label(p + Vector3.up * radius.floatValue * 1.2f, $"{i + 1} {bone.name}", EditorStyles.whiteMiniLabel);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(p, Tools.pivotRotation == PivotRotation.Local ? bone.rotation : Quaternion.identity);
            Handles.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            float r = Handles.RadiusHandle(Quaternion.identity, p, radius.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                offset.vector3Value = Quaternion.Inverse(bone.rotation) * (moved - bone.position);
                radius.floatValue = Mathf.Max(0.01f, r);
            }
        }
        so.ApplyModifiedProperties();
    }
}
