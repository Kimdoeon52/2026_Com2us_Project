// RE:AL STEEL - 설치 메뉴 (색감 · 간접광 · 젖은 바닥) + 젖은 바닥 인스펙터 + GameObject 메뉴
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RealSteel.Common.EditorTools;

namespace RealSteel.Lighting.EditorTools
{
    public static class RSSurfaceLookMenu
    {
        static string MatPath => RSPaths.Materials + "/MAT_RS_WetReflection.mat";
        const string WetShader = "RE_AL STEEL/Wet Reflection";

        public static void Setup()
        {
            var log = new System.Text.StringBuilder("[색감 · 간접광 · 젖은 바닥] ");

            // 붙일 곳: 시간대 오브젝트 (없으면 새로)
            var tod = Object.FindAnyObjectByType<RSTimeOfDay>();
            GameObject host;
            if (tod != null) host = tod.gameObject;
            else
            {
                host = GameObject.Find("LIGHTING_Extras");
                if (host == null)
                {
                    host = new GameObject("LIGHTING_Extras");
                    Undo.RegisterCreatedObjectUndo(host, "조명 추가 오브젝트");
                }
                log.Append("시간대가 없어 'LIGHTING_Extras' 에 붙입니다 (색감은 고정 시각으로). ");
            }

            // 1) 색감
            var grade = Object.FindAnyObjectByType<RSColorGrade>();
            if (grade == null)
            {
                grade = Undo.AddComponent<RSColorGrade>(host);
                if (tod == null) grade.fixedHour = true;
                log.Append("색감을 붙였습니다. ");
            }
            if (grade.L.profile == null)
            {
                RSLookEdit.Record(grade, grade.UsesStageLook, "색감 프리셋");
                var guids = AssetDatabase.FindAssets("t:RSColorGradeProfile", new[] { RSPaths.Root });
                grade.L.profile = guids.Length > 0
                    ? AssetDatabase.LoadAssetAtPath<RSColorGradeProfile>(AssetDatabase.GUIDToAssetPath(guids[0]))
                    : RSColorGradeGUI.CreateDefaultProfile();
                RSLookEdit.Dirty(grade, grade.UsesStageLook);
                log.Append($"색감 프리셋 '{grade.L.profile.name}' 을 연결했습니다. ");
            }

            // 2) 간접광
            if (Object.FindAnyObjectByType<RSIndirectLight>() == null)
            {
                Undo.AddComponent<RSIndirectLight>(host);
                log.Append("간접광을 붙였습니다 (지형 크기에 자동으로 맞춤). ");
            }

            // 3) 젖은 바닥
            var wet = Object.FindAnyObjectByType<RSWetness>();
            if (wet == null)
            {
                wet = Undo.AddComponent<RSWetness>(host);
                log.Append("젖은 바닥 · 반사를 붙였습니다. ");
            }
            if (wet.overlayMaterial == null)
            {
                Undo.RecordObject(wet, "반사 겹 머티리얼");
                wet.overlayMaterial = FindOrCreateWetMaterial();
                EditorUtility.SetDirty(wet);
                wet.Apply();
            }

            // 4) 스테이지 룩 (씬 값을 에셋으로 모을 자리 — 프로필은 인스펙터에서 만든다)
            RSLookInspector.EnsureBinding(host);

            // 5) Opaque · Depth Texture (반사가 화면 색 · 깊이를 읽는다)
            EnsureCameraTextures(log, true);

            Selection.activeGameObject = host;
            Debug.Log(log.ToString());
        }

        public static Material FindOrCreateWetMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (m != null) return m;
            var sh = Shader.Find(WetShader);
            if (sh == null) { Debug.LogError($"셰이더 '{WetShader}' 를 찾지 못했습니다."); return null; }
            m = new Material(sh) { name = "MAT_RS_WetReflection" };
            RSPaths.Ensure(RSPaths.Materials);
            AssetDatabase.CreateAsset(m, MatPath);
            AssetDatabase.SaveAssets();
            return m;
        }

        /// <summary>URP 에셋 Opaque · Depth Texture 와 보이는 카메라 설정 확인. 에셋(SYS 밖)은 물어보고 켠다</summary>
        public static bool EnsureCameraTextures(System.Text.StringBuilder log, bool ask)
        {
            bool ok = true;
            var urp = UniversalRenderPipeline.asset;
            if (urp != null && (!urp.supportsCameraOpaqueTexture || !urp.supportsCameraDepthTexture))
            {
                ok = false;
                string what = (!urp.supportsCameraOpaqueTexture ? "Opaque Texture " : "") + (!urp.supportsCameraDepthTexture ? "Depth Texture" : "");
                if (ask && EditorUtility.DisplayDialog("URP 설정",
                        $"젖은 바닥 반사는 화면 색 · 깊이를 읽어야 해서 URP 에셋 '{urp.name}' 의 {what} 를 켜야 합니다.\n" +
                        "(프로젝트 설정이라 SYS 폴더 밖 파일이 바뀝니다)", "켜기", "나중에"))
                {
                    Undo.RecordObject(urp, "URP Opaque · Depth Texture");
                    urp.supportsCameraOpaqueTexture = true;
                    urp.supportsCameraDepthTexture = true;
                    EditorUtility.SetDirty(urp);
                    AssetDatabase.SaveAssetIfDirty(urp);
                    ok = true;
                    log?.Append($"URP 에셋의 {what} 를 켰습니다. ");
                }
                else log?.Append($"※ URP 에셋 {what} 가 꺼져 있어 반사가 안 보입니다. ");
            }
            var cam = RSAutoFocus.FindViewCamera();
            var ad = cam != null ? cam.GetUniversalAdditionalCameraData() : null;
            if (ad != null && (ad.requiresColorOption == CameraOverrideOption.Off || ad.requiresDepthOption == CameraOverrideOption.Off))
            {
                Undo.RecordObject(ad, "카메라 Opaque · Depth Texture");
                if (ad.requiresColorOption == CameraOverrideOption.Off) ad.requiresColorOption = CameraOverrideOption.UsePipelineSettings;
                if (ad.requiresDepthOption == CameraOverrideOption.Off) ad.requiresDepthOption = CameraOverrideOption.UsePipelineSettings;
                EditorUtility.SetDirty(ad);
                log?.Append($"'{cam.name}' 카메라의 Opaque · Depth Texture 꺼짐을 풀었습니다. ");
            }
            return ok;
        }

        // ── GameObject 메뉴 (요소 추가) ──

        [MenuItem("GameObject/RE_AL STEEL/간접광 색 번짐", false, 13)]
        static void CreateBounce()
        {
            var go = new GameObject("간접광_색번짐");
            Undo.RegisterCreatedObjectUndo(go, "간접광 색 번짐");
            var sv = SceneView.lastActiveSceneView;
            if (Selection.activeTransform != null) go.transform.position = Selection.activeTransform.position;
            else if (sv != null) go.transform.position = sv.pivot;
            go.AddComponent<RSBounceSource>();
            Selection.activeGameObject = go;
        }

        [MenuItem("GameObject/RE_AL STEEL/선택한 바닥을 젖는 바닥으로", false, 14)]
        static void MakeWet()
        {
            int n = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (go.GetComponent<MeshFilter>() == null || go.GetComponent<RSWetSurface>() != null) continue;
                Undo.AddComponent<RSWetSurface>(go);
                n++;
            }
            Debug.Log($"[젖는 바닥] {n}개에 붙였습니다." + (RSWetness.Active == null ? " (씬에 '젖은 바닥 · 반사' 가 없어 아직 안 보입니다 — 메뉴 '스테이지 연출 한 번에 설치')" : ""));
        }

        [MenuItem("GameObject/RE_AL STEEL/선택한 바닥을 젖는 바닥으로", true)]
        static bool MakeWetValidate()
        {
            foreach (var go in Selection.gameObjects) if (go.GetComponent<MeshFilter>() != null) return true;
            return false;
        }
    }

    [CustomEditor(typeof(RSWetness))]
    public class RSWetnessEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            var w = (RSWetness)target;
            EditorGUILayout.LabelField("프리셋 (느낌 한 번에 맞추기 — 비 온 정도 · 품질은 그대로)", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("빗길", "비 오는 밤 돌길 (Replaced 식): 강한 반사, 세로 번짐, 돌마다 반짝임, 불빛 빛줄기"))) SetPreset(w, RSWetness.Preset.빗길);
                if (GUILayout.Button(new GUIContent("은은하게", "비 그친 뒤 (옥토패스 식)"))) SetPreset(w, RSWetness.Preset.은은하게);
                if (GUILayout.Button(new GUIContent("거울 웅덩이", "고인 물: 또렷한 거울"))) SetPreset(w, RSWetness.Preset.거울웅덩이);
            }
            EditorGUILayout.Space(2f);
            RSLookInspector.Draw(this);

            var urp = UniversalRenderPipeline.asset;
            if (urp != null && (!urp.supportsCameraOpaqueTexture || !urp.supportsCameraDepthTexture))
            {
                EditorGUILayout.HelpBox("URP 에셋의 Opaque Texture 또는 Depth Texture 가 꺼져 있어 반사가 안 보입니다.", MessageType.Warning);
                if (GUILayout.Button("URP 설정 켜기")) RSSurfaceLookMenu.EnsureCameraTextures(null, true);
            }
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox("어디를 적시나\n· RS 지형: 지형 인스펙터 브러시 '젖음 칠'\n· 다른 바닥: 오브젝트를 고르고 아래 버튼 (또는 GameObject → RE_AL STEEL → 선택한 바닥을 젖는 바닥으로)", MessageType.None);
            GUI.enabled = Selection.gameObjects.Length > 0;
            if (GUILayout.Button("선택한 오브젝트를 젖는 바닥으로"))
            {
                foreach (var go in Selection.gameObjects)
                    if (go.GetComponent<MeshFilter>() != null && go.GetComponent<RSWetSurface>() == null) Undo.AddComponent<RSWetSurface>(go);
            }
            GUI.enabled = true;
        }

        static void SetPreset(RSWetness w, RSWetness.Preset p)
        {
            RSLookEdit.Record(w, w.UsesStageLook, "젖은 바닥 프리셋");
            w.ApplyPreset(p);
            RSLookEdit.Dirty(w, w.UsesStageLook);
        }
    }

    [CustomEditor(typeof(RSWetSurface)), CanEditMultipleObjects]
    public class RSWetSurfaceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            RSInspector.Draw(serializedObject);
            var s = (RSWetSurface)target;
            if (s.GetComponent<MeshFilter>() == null)
                EditorGUILayout.HelpBox("MeshFilter 가 없는 오브젝트입니다 (메시가 있는 바닥에 붙이세요).", MessageType.Warning);
            if (RSWetness.Active == null)
                EditorGUILayout.HelpBox("씬에 '젖은 바닥 · 반사' 가 없어서 안 보입니다. Tools → RE_AL STEEL → Stage → 스테이지 연출 한 번에 설치", MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("비 온 뒤 바닥"))
                {
                    Undo.RecordObjects(targets, "젖는 바닥 프리셋");
                    foreach (RSWetSurface t in targets) { t.wetness = 0f; t.rainResponse = 1f; t.darkenScale = 1f; t.Refresh(); }
                }
                if (GUILayout.Button("대리석 · 광택"))
                {
                    Undo.RecordObjects(targets, "젖는 바닥 프리셋");
                    foreach (RSWetSurface t in targets) { t.wetness = 0.8f; t.rainResponse = 0.3f; t.darkenScale = 0f; t.Refresh(); }
                }
                if (GUILayout.Button("물웅덩이"))
                {
                    Undo.RecordObjects(targets, "젖는 바닥 프리셋");
                    foreach (RSWetSurface t in targets) { t.wetness = 1f; t.rainResponse = 1f; t.darkenScale = 1f; t.Refresh(); }
                }
                if (GUILayout.Button("지붕 밑"))
                {
                    Undo.RecordObjects(targets, "젖는 바닥 프리셋");
                    foreach (RSWetSurface t in targets) { t.rainResponse = 0f; t.Refresh(); }
                }
            }
        }
    }
}
