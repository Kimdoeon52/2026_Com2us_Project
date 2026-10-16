// RE:AL STEEL - 물 설치 메뉴 + 물 반사 인스펙터
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RealSteel.Common.EditorTools;
using RealSteel.Terrain;

namespace RealSteel.Water.EditorTools
{
    public static class RSWaterMenu
    {
        const string ShaderName = "RE_AL STEEL/Water Pixel Lit";
        const string MatName = "MAT_RS_Water";

        [MenuItem("Tools/RE_AL STEEL/Stage/물 (반사 · 굴절 · 거품) 설치", false, 2)]
        public static void Setup()
        {
            var mat = FindOrCreateMaterial();
            if (mat == null) return;

            // 1) RS 지형 수면에 머티리얼
            var terrains = Object.FindObjectsByType<RSTerrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var t in terrains)
            {
                Undo.RecordObject(t, "물 설치");
                t.waterMaterial = mat;
                t.MarkDirty();
                EditorUtility.SetDirty(t);
            }

            // 2) 반사
            var refl = Object.FindFirstObjectByType<RSWaterReflection>();
            if (refl == null)
            {
                var go = new GameObject("WATER_Reflection");
                Undo.RegisterCreatedObjectUndo(go, "물 설치");
                refl = go.AddComponent<RSWaterReflection>();
            }
            float h;
            if (RSWaterReflectionEditor.TerrainWaterHeight(out h))
            {
                Undo.RecordObject(refl.transform, "물 설치");
                var p = refl.transform.position; p.y = h; refl.transform.position = p;
            }

            // 3) 메인 카메라: 깊이 · 화면 텍스처 켜기 (거품 · 깊이 색 · 굴절에 필요)
            var cam = Camera.main;
            if (cam != null)
            {
                var ad = cam.GetUniversalAdditionalCameraData();
                if (ad != null)
                {
                    Undo.RecordObject(ad, "물 설치");
                    ad.requiresDepthOption = CameraOverrideOption.On;
                    ad.requiresColorOption = CameraOverrideOption.On;
                    EditorUtility.SetDirty(ad);
                }
            }

            // 4) URP 설정 (씬 뷰에서도 보이려면) — 프로젝트 설정이라 물어본다
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null && (!urp.supportsCameraDepthTexture || !urp.supportsCameraOpaqueTexture))
            {
                if (EditorUtility.DisplayDialog("물 — URP 설정",
                        "URP 설정의 Depth Texture / Opaque Texture 가 꺼져 있습니다.\n" +
                        "게임 카메라는 방금 켰지만, 씬 뷰에서도 물의 거품 · 깊이 색 · 굴절이 보이려면 URP 설정에서 켜야 합니다.\n\n" +
                        "'" + urp.name + "' 에서 두 옵션을 켤까요?", "켜기", "그대로"))
                {
                    Undo.RecordObject(urp, "물 설치");
                    urp.supportsCameraDepthTexture = true;
                    urp.supportsCameraOpaqueTexture = true;
                    EditorUtility.SetDirty(urp);
                }
            }

            Selection.activeGameObject = refl.gameObject;
            SceneView.RepaintAll();
            Debug.Log("[물] 설치 완료 — 지형 " + terrains.Length + "개에 " + mat.name + " 적용, 반사 높이 " + refl.transform.position.y.ToString("0.00") +
                      "\n· 물 색 · 물결 · 반사 세기는 머티리얼(" + mat.name + ")에서, 반사 화질은 WATER_Reflection 에서 조절하세요.");
        }

        public static Material FindOrCreateMaterial()
        {
            foreach (var g in AssetDatabase.FindAssets(MatName + " t:Material"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == MatName) return AssetDatabase.LoadAssetAtPath<Material>(p);
            }
            var sh = Shader.Find(ShaderName);
            if (sh == null) { Debug.LogWarning("[물] 셰이더 '" + ShaderName + "' 를 찾지 못했습니다. 콘솔에 셰이더 에러가 없는지 확인하세요."); return null; }
            string dir = RSPaths.Ensure(RSPaths.Materials);
            var m = new Material(sh) { name = MatName };
            m.EnableKeyword("_REFRACTION");
            m.EnableKeyword("_DEPTH_EFFECTS");
            AssetDatabase.CreateAsset(m, dir + "/" + MatName + ".mat");
            AssetDatabase.SaveAssets();
            return m;
        }
    }

    [CustomEditor(typeof(RSWaterReflection))]
    public class RSWaterReflectionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            RSInspector.Draw(serializedObject);
            var rr = (RSWaterReflection)target;
            if (rr.mode == RSWaterReflection.Mode.Planar && rr.PlanarFailed)
                EditorGUILayout.HelpBox("평면 반사가 이 프로젝트 설정에서 렌더 오류를 내서 화면 반사로 대신하고 있습니다. 모드를 '화면 반사' 로 바꾸면 경고가 다시 안 뜹니다.", MessageType.Warning);
            var urp = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset;
            if (urp != null && (!urp.supportsCameraOpaqueTexture || !urp.supportsCameraDepthTexture))
                EditorGUILayout.HelpBox("화면 반사는 URP 에셋의 Opaque Texture · Depth Texture 가 켜져 있어야 보입니다.", MessageType.Warning);
            EditorGUILayout.Space(4f);
            if (GUILayout.Button("RS 지형 수면 높이에 맞추기"))
            {
                if (TerrainWaterHeight(out float h))
                {
                    var r = (RSWaterReflection)target;
                    Undo.RecordObject(r.transform, "수면 높이");
                    var p = r.transform.position; p.y = h; r.transform.position = p;
                }
                else Debug.LogWarning("[물] 씬에서 RS 지형 수면을 찾지 못했습니다 (배수로 · 웅덩이의 Water 가 켜져 있는지 확인).");
            }
        }

        /// <summary>RS 지형 수면 메시에서 가장 넓게 쓰인 높이 (배수로 수면)</summary>
        public static bool TerrainWaterHeight(out float h)
        {
            h = 0f;
            var count = new Dictionary<int, int>();
            foreach (var t in Object.FindObjectsByType<RSTerrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.gameObject.name != "Water" || mf.sharedMesh == null) continue;
                    foreach (var v in mf.sharedMesh.vertices)
                    {
                        int key = Mathf.RoundToInt(mf.transform.TransformPoint(v).y * 100f);
                        count.TryGetValue(key, out int c); count[key] = c + 1;
                    }
                }
            if (count.Count == 0) return false;
            h = count.OrderByDescending(kv => kv.Value).First().Key / 100f;
            return true;
        }
    }
}
