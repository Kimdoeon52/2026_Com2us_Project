// RE:AL STEEL - 천막 (차양) 인스펙터 + 메뉴
using UnityEditor;
using UnityEngine;
using RealSteel.Common;
using RealSteel.Common.EditorTools;

namespace RealSteel.Build.EditorTools
{
    [CustomEditor(typeof(RSAwning)), CanEditMultipleObjects]
    public class RSAwningEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            EditorGUI.BeginChangeCheck();
            RSInspector.Draw(serializedObject);
            if (EditorGUI.EndChangeCheck())
                foreach (var t in targets) ((RSAwning)t).Rebuild();

            EditorGUILayout.Space(4);
            var a = (RSAwning)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(a.fitTarget == null))
                    if (GUILayout.Button(a.fitTarget != null ? "'" + a.fitTarget.name + "' 에 맞추기" : "맞출 물체 없음 (맞추기 칸에 넣기)", GUILayout.Height(24)))
                        Fit(a, a.fitTarget);
            }

            int verts = 0;
            var mf = a.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) verts = mf.sharedMesh.vertexCount;
            EditorGUILayout.HelpBox("정점 " + verts + "개 · 재질 2개 (천 · 막대). 펄럭임은 플레이 중에 보인다 (에디터에서 보려면 '에디터에서도 움직이기').", MessageType.None);
        }

        static void Fit(RSAwning a, Transform target)
        {
            Undo.RecordObjects(new Object[] { a, a.transform }, "천막 맞추기");
            if (!a.FitTo(target)) Debug.LogWarning("[천막] '" + target.name + "' 의 크기를 알 수 없어 맞추지 못했습니다 (메시 · 렌더러가 없음)", a);
            EditorUtility.SetDirty(a);
        }

        static RSSurface FindSurface(string assetName)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:RSSurface " + assetName))
            {
                var s = AssetDatabase.LoadAssetAtPath<RSSurface>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null && s.name == assetName) return s;
            }
            return null;
        }

        [MenuItem("GameObject/RE_AL STEEL/천막 (차양)", false, 10)]
        static void Create(MenuCommand cmd)
        {
            // 계층 창 우클릭은 고른 물체마다 한 번씩 불린다 — 첫 번째만
            if (cmd.context != null && Selection.objects.Length > 1 && cmd.context != Selection.objects[0]) return;

            var target = Selection.activeTransform;
            var go = new GameObject("천막");
            Undo.RegisterCreatedObjectUndo(go, "천막");
            var a = go.AddComponent<RSAwning>();
            a.clothSurface = FindSurface("예제_천막천");
            a.poleSurface = FindSurface("예제_짙은나무");
            a.seed = Random.Range(1, 1000);

            if (target != null && target.GetComponentInChildren<Renderer>() != null)
            {
                // 관중석 등을 고른 채 만들면 그 위에 맞춘다
                a.fitTarget = target;
                a.FitTo(target);
            }
            else
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null) go.transform.position = new Vector3(Mathf.Round(sv.pivot.x), 0f, Mathf.Round(sv.pivot.z));
                a.Rebuild();
            }
            Selection.activeGameObject = go;
        }
    }
}
