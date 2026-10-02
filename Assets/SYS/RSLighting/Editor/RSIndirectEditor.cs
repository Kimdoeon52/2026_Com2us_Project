// RE:AL STEEL - 간접광 인스펙터 (지도 미리보기 · 다시 계산) + 씬이 바뀌면 자동 다시 계산
using UnityEditor;
using UnityEngine;
using RealSteel.Common.EditorTools;

namespace RealSteel.Lighting.EditorTools
{
    [CustomEditor(typeof(RSIndirectLight))]
    public class RSIndirectLightEditor : Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            var il = (RSIndirectLight)target;

            bool changed = RSLookInspector.Draw(this);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("지금 다시 계산", GUILayout.Height(26f))) { il.RecalculateNow(); SceneView.RepaintAll(); }

            if (il.CellsX > 0)
            {
                EditorGUILayout.LabelField(
                    $"칸 {il.CellsX} x {il.CellsZ} · 바닥 찾은 칸 {il.ValidCells} · 바닥 스캔 {il.LastGeometryMs:0} ms · 햇빛 {il.LastSunMs:0} ms",
                    EditorStyles.miniLabel);
                if (il.ValidCells == 0)
                    EditorGUILayout.HelpBox("바닥을 하나도 못 찾았습니다. 지형에 콜라이더가 있는지(RS 지형 'Generate Collider'), '바닥 레이어' 가 맞는지 확인하세요.", MessageType.Warning);
            }

            if (il.PreviewVis != null && il.PreviewBounce != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("지도 미리보기 (위 = +Z 북쪽)", EditorStyles.boldLabel);
                float w = Mathf.Min((EditorGUIUtility.currentViewWidth - 40f) * 0.5f, 220f);
                float h = w * il.CellsZ / Mathf.Max(1f, il.CellsX);
                Rect r = GUILayoutUtility.GetRect(w * 2f + 8f, h + 16f);
                Rect a = new Rect(r.x, r.y + 14f, w, h), b = new Rect(r.x + w + 8f, r.y + 14f, w, h);
                GUI.Label(new Rect(a.x, r.y, w, 14f), "하늘 보임 (어두울수록 구석)", EditorStyles.miniLabel);
                GUI.Label(new Rect(b.x, r.y, w, 14f), "튄 빛 색", EditorStyles.miniLabel);
                // 텍스처의 아래 줄 = -Z. GUI 는 위가 위라 뒤집어서 그린다
                GUI.DrawTextureWithTexCoords(a, il.PreviewVis, new Rect(0, 0, 1, 1));
                GUI.DrawTextureWithTexCoords(b, il.PreviewBounce, new Rect(0, 0, 1, 1));
                EditorGUILayout.LabelField("빨간 칸 = 바닥 못 찾음 (콜라이더 없음)", EditorStyles.miniLabel);
            }

            if (Object.FindAnyObjectByType<RSBounceSource>() == null)
                EditorGUILayout.HelpBox("색 번짐을 더하려면 네온 간판 · 모닥불 같은 오브젝트에 '간접광 색 번짐' 을 붙이세요.\n(GameObject → RE_AL STEEL → 간접광 색 번짐)", MessageType.None);

            if (changed) { il.MarkGeometryDirty(); SceneView.RepaintAll(); }
        }
    }

    [CustomEditor(typeof(RSBounceSource)), CanEditMultipleObjects]
    public class RSBounceSourceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            RSInspector.Draw(serializedObject);
            if (RSIndirectLight.Active == null)
                EditorGUILayout.HelpBox("씬에 '간접광' 이 없어서 안 보입니다. Tools → RE_AL STEEL → Stage → 스테이지 연출 한 번에 설치", MessageType.Warning);
        }
    }

    /// <summary>씬에서 무언가 바뀌면(옮기기 · 추가 · 지형 브러시 등) 잠시 뒤 간접광 바닥을 다시 잰다</summary>
    [InitializeOnLoad]
    static class RSIndirectAutoRefresh
    {
        static RSIndirectAutoRefresh()
        {
            ObjectChangeEvents.changesPublished += OnChanges;
        }

        static void OnChanges(ref ObjectChangeEventStream stream)
        {
            var il = RSIndirectLight.Active;
            if (il == null || Application.isPlaying) return;
            for (int i = 0; i < stream.length; i++)
            {
                Object o = null;
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var e1);
                        o = EditorUtility.InstanceIDToObject(e1.instanceId);
                        if (!Relevant(o)) continue;
                        break;
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                        stream.GetChangeAssetObjectPropertiesEvent(i, out var e2);
                        o = EditorUtility.InstanceIDToObject(e2.instanceId);
                        if (!(o is Material) && !(o is Texture) && (o == null || !(o.GetType().Namespace ?? "").StartsWith("RealSteel.Terrain"))) continue;
                        if (o is Texture) RSIndirectLight.ClearAlbedoCache();
                        break;
                    case ObjectChangeKind.ChangeGameObjectStructure:
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                    case ObjectChangeKind.ChangeGameObjectParent:
                        break;
                    default:
                        continue;
                }
                il.MarkGeometryDirty(0.6f);   // 드래그 중엔 계속 미뤄진다
                return;
            }
        }

        /// <summary>바닥 모양 · 색에 영향을 주는 변경만 (시간대 슬라이더 · 조명 값 등은 무시)</summary>
        static bool Relevant(Object o)
        {
            if (o == null || o is GameObject) return true;
            if (o is Transform || o is Collider || o is MeshFilter || o is Renderer) return true;
            return (o.GetType().Namespace ?? "").StartsWith("RealSteel.Terrain");
        }
    }
}
