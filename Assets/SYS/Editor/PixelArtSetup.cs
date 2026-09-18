using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RealSteel.EditorTools
{
    /// <summary>
    /// 픽셀 텍스처가 뿌옇게 번지는 원인을 한 곳에서 잡는다.
    ///
    /// 3D 면에 픽셀아트를 붙였을 때 흐려지는 이유는 거의 정해져 있다.
    ///
    ///   1. Filter Mode 가 Bilinear      — 픽셀 사이를 보간해서 뭉갠다 (기본값이라 제일 흔하다)
    ///   2. 압축(DXT/BC)                 — 4x4 블록 단위로 색을 뭉개서 픽셀 경계가 번진다
    ///   3. NPOT 리스케일                — 2의 거듭제곱이 아닌 크기를 강제로 늘려 리샘플한다
    ///   4. 밉맵                          — 멀어지면 저해상도 판으로 갈아탄다
    ///   5. Depth of Field               — 초점 밖 물체를 엔진이 직접 흐린다
    ///   6. Bloom                        — 밝은 픽셀이 주변으로 번진다
    ///
    /// 1~4 는 텍스처 임포트 설정, 5~6 은 포스트 프로세싱이다. 원인이 둘로 나뉘어 있어서
    /// 한쪽만 고치면 여전히 흐릿하다 — 그래서 두 가지를 같이 본다.
    ///
    /// 텍스처는 경로로 찾지 않고 <b>씬 오브젝트의 머티리얼에서 역추적</b>하므로
    /// 어디에 두었든 알아서 찾아낸다.
    /// </summary>
    public class PixelArtSetup : EditorWindow
    {
        bool keepMipmaps = false;
        float mipBias = -0.5f;
        bool convertToDefaultType = false;

        Vector2 scroll;
        string report = "";

        [MenuItem("Tools/RE_AL STEEL/Stage/픽셀 텍스처 선명하게 (진단 + 일괄 설정)", false, 44)]
        static void Open()
        {
            var w = GetWindow<PixelArtSetup>("픽셀 텍스처");
            w.minSize = new Vector2(520f, 560f);
        }

        // ─────────────────────────────────────────────────────────

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "씬 오브젝트의 머티리얼에서 텍스처를 역추적해 임포트 설정을 점검한다.\n" +
                "포스트 프로세싱(DOF·Bloom)도 같이 본다 — 텍스처만 고쳐선 안 선명해진다.",
                MessageType.None);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("텍스처 설정", EditorStyles.boldLabel);

            keepMipmaps = EditorGUILayout.Toggle(
                new GUIContent("밉맵 유지",
                    "끄면 가까이서 제일 선명하지만, 멀어질 때 지글거린다(에일리어싱).\n" +
                    "카메라 거리가 고정된 이 프로젝트에서는 끄는 쪽이 보통 낫다."),
                keepMipmaps);

            using (new EditorGUI.DisabledScope(!keepMipmaps))
            {
                mipBias = EditorGUILayout.Slider(
                    new GUIContent("밉맵 바이어스",
                        "음수일수록 고해상도 판을 오래 쓴다 = 선명해진다."),
                    mipBias, -2f, 0f);
            }

            convertToDefaultType = EditorGUILayout.Toggle(
                new GUIContent("Sprite 타입 → Default 로 변환",
                    "2D 패키지가 깔린 프로젝트는 PNG 를 기본으로 Sprite 타입으로 임포트한다.\n" +
                    "3D 면에 붙이는 타일 텍스처는 Default 가 맞다 — 반복(Repeat)이 확실해진다.\n\n" +
                    "주의: 같은 텍스처를 SpriteRenderer 에서도 쓰고 있으면 그쪽이 깨진다.\n" +
                    "여기서 수집되는 건 머티리얼 슬롯에 꽂힌 텍스처뿐이라 보통은 안전하다."),
                convertToDefaultType);

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("진단 (아무것도 안 바꿈)", GUILayout.Height(28f))) Diagnose();

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("텍스처 설정 일괄 적용", GUILayout.Height(30f))) FixTextures();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("포스트 프로세싱", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "텍스처가 선명한지 확인할 때는 DOF 를 잠깐 꺼 보는 게 확실하다.\n" +
                "끄고도 흐리면 텍스처 문제, 끄니까 선명하면 DOF 문제다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("DOF 끄기")) ToggleDof(false);
                if (GUILayout.Button("DOF 켜기")) ToggleDof(true);
                if (GUILayout.Button("Bloom 끄기")) ToggleBloom(false);
                if (GUILayout.Button("Bloom 켜기")) ToggleBloom(true);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("머티리얼 무광 처리", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Smoothness 0 은 '반사를 끈다'가 아니라 '반사를 최대로 흐린다'는 뜻이다.\n" +
                "그래서 면 전체에 번들거림이 넓게 깔린다 — 이걸 끄려면 스페큘러와 환경반사를\n" +
                "따로 꺼야 한다. 조명과 그림자는 그대로 받는다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("선택한 머티리얼")) Matte(SelectedMaterials(), true);
                if (GUILayout.Button("씬 전체")) Matte(SceneMaterials(), true);
                if (GUILayout.Button("되돌리기 (광택 켬)")) Matte(SceneMaterials(), false);
            }

            EditorGUILayout.Space(8f);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────────────────
        // 무광 처리
        // ─────────────────────────────────────────────────────────

        static List<Material> SelectedMaterials()
        {
            var list = new HashSet<Material>();
            foreach (var o in Selection.objects)
            {
                if (o is Material m) { list.Add(m); continue; }
                if (o is GameObject go)
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        foreach (var sm in r.sharedMaterials) if (sm != null) list.Add(sm);
            }
            return list.ToList();
        }

        static List<Material> SceneMaterials()
        {
            var list = new HashSet<Material>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials) if (m != null) list.Add(m);
            return list.ToList();
        }

        /// <summary>
        /// URP Lit 을 완전 무광으로 만든다.
        ///
        /// 핵심은 <b>float 만 0 으로 두면 아무 일도 안 일어난다</b>는 것 —
        /// 셰이더는 <c>_SPECULARHIGHLIGHTS_OFF</c> / <c>_ENVIRONMENTREFLECTIONS_OFF</c>
        /// 키워드로 분기하므로 키워드를 같이 켜 줘야 실제로 꺼진다.
        ///
        /// 끄더라도 직사광·점광·그림자는 그대로 받는다. Unlit 으로 바꾸는 것과 다르다.
        /// </summary>
        void Matte(List<Material> mats, bool matte)
        {
            if (mats == null || mats.Count == 0) { report = "머티리얼을 못 찾았다."; return; }

            var log = new StringBuilder();
            int n = 0;

            foreach (var m in mats)
            {
                if (m == null) continue;
                Undo.RecordObject(m, "머티리얼 무광 처리");

                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", matte ? 0f : 0.35f);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", matte ? 0f : 0.35f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);

                if (m.HasProperty("_SpecularHighlights"))
                {
                    m.SetFloat("_SpecularHighlights", matte ? 0f : 1f);
                    if (matte) m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    else m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                }
                if (m.HasProperty("_EnvironmentReflections"))
                {
                    m.SetFloat("_EnvironmentReflections", matte ? 0f : 1f);
                    if (matte) m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    else m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                }

                EditorUtility.SetDirty(m);
                n++;
                log.AppendLine($"  {m.name,-26} spec={(matte ? "off" : "on")} env={(matte ? "off" : "on")}");
            }

            AssetDatabase.SaveAssets();
            log.Insert(0, $"{n}개 머티리얼을 {(matte ? "무광" : "광택")}으로 바꿨다\n\n");
            report = log.ToString();
        }

        // ─────────────────────────────────────────────────────────
        // 진단
        // ─────────────────────────────────────────────────────────

        void Diagnose()
        {
            var log = new StringBuilder();
            var paths = CollectTexturePaths(log);

            log.AppendLine();
            log.AppendLine("── 텍스처 임포트 ──");
            if (paths.Count == 0) log.AppendLine("  씬 머티리얼에서 텍스처를 못 찾았다. BaseMap 이 비어 있는지 확인할 것.");

            foreach (var p in paths)
            {
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (ti == null || tex == null) continue;

                var issues = new List<string>();
                if (ti.filterMode != FilterMode.Point) issues.Add($"Filter={ti.filterMode} (Point 이어야 함)");
                if (ti.textureCompression != TextureImporterCompression.Uncompressed)
                    issues.Add($"압축={ti.textureCompression}");
                if (ti.npotScale != TextureImporterNPOTScale.None && !IsPot(tex))
                    issues.Add($"NPOT={ti.npotScale} — 크기를 강제로 늘려 리샘플한다");
                if (ti.mipmapEnabled && !keepMipmaps) issues.Add("밉맵 켜짐");
                if (ti.anisoLevel > 1 && !ti.mipmapEnabled) issues.Add($"이방성={ti.anisoLevel}");

                // 임포트된 실제 크기가 원본보다 작으면 그것부터가 리샘플이다
                ti.GetSourceTextureWidthAndHeight(out int sw, out int sh);
                if (sw > 0 && (tex.width < sw || tex.height < sh))
                    issues.Add($"원본 {sw}x{sh} → 임포트 {tex.width}x{tex.height} (Max Size 가 작다)");

                string mark = issues.Count == 0 ? "OK " : "!! ";
                log.AppendLine($"  {mark}{System.IO.Path.GetFileName(p),-30} {tex.width}x{tex.height}  " +
                               $"[{ti.textureType}]");
                foreach (var s in issues) log.AppendLine($"        · {s}");
            }

            log.AppendLine();
            log.AppendLine("── 포스트 프로세싱 ──");
            log.Append(PostReport());

            log.AppendLine();
            log.AppendLine("── 렌더 파이프라인 ──");
            log.Append(PipelineReport());

            report = log.ToString();
        }

        static bool IsPot(Texture2D t) => IsPot(t.width) && IsPot(t.height);
        static bool IsPot(int v) => v > 0 && (v & (v - 1)) == 0;

        // ─────────────────────────────────────────────────────────
        // 수정
        // ─────────────────────────────────────────────────────────

        void FixTextures()
        {
            var log = new StringBuilder();
            var paths = CollectTexturePaths(log);
            if (paths.Count == 0) { report = "고칠 텍스처를 못 찾았다.\n\n" + log; return; }

            int n = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var p in paths)
                {
                    var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                    if (ti == null) continue;

                    // 여기 오는 건 전부 머티리얼 슬롯에 꽂힌 '면 텍스처'다.
                    // SpriteRenderer 가 쓰는 캐릭터 스프라이트는 애초에 수집되지 않으므로
                    // 타입으로 걸러낼 이유가 없다 — 2D 패키지가 깔린 프로젝트에서는
                    // 타일 텍스처도 Sprite 타입으로 임포트되기 때문에, 거르면 정작 고칠 것을 건너뛴다.
                    if (convertToDefaultType && ti.textureType == TextureImporterType.Sprite)
                        ti.textureType = TextureImporterType.Default;

                    ti.filterMode = FilterMode.Point;                                  // 보간 금지
                    ti.textureCompression = TextureImporterCompression.Uncompressed;   // 블록 압축 금지
                    ti.npotScale = TextureImporterNPOTScale.None;                      // 크기 리샘플 금지
                    ti.mipmapEnabled = keepMipmaps;
                    ti.mipMapBias = keepMipmaps ? mipBias : 0f;
                    ti.anisoLevel = keepMipmaps ? 4 : 0;
                    ti.wrapMode = TextureWrapMode.Repeat;
                    ti.alphaIsTransparency = true;
                    ti.sRGBTexture = true;
                    if (ti.maxTextureSize < 2048) ti.maxTextureSize = 2048;

                    EditorUtility.SetDirty(ti);
                    AssetDatabase.WriteImportSettingsIfDirty(p);
                    n++;
                    log.AppendLine($"  {System.IO.Path.GetFileName(p)}");
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();

            log.Insert(0, $"{n}개 텍스처를 픽셀아트 설정으로 바꿨다 " +
                          $"(Point / 무압축 / NPOT 없음 / 밉맵 {(keepMipmaps ? "유지" : "끔")})\n\n");
            log.AppendLine();
            log.AppendLine("── 포스트 프로세싱 ──");
            log.Append(PostReport());
            report = log.ToString();
        }

        // ─────────────────────────────────────────────────────────
        // 수집 — 경로가 아니라 씬 머티리얼에서 역추적한다
        // ─────────────────────────────────────────────────────────

        static List<string> CollectTexturePaths(StringBuilder log)
        {
            var mats = new HashSet<Material>();

            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials)
                    if (m != null) mats.Add(m);

            log.AppendLine($"씬 머티리얼 {mats.Count}개에서 텍스처를 찾는다.");

            var paths = new List<string>();
            foreach (var m in mats)
            {
                foreach (var prop in new[] { "_BaseMap", "_MainTex", "_EmissionMap", "_BumpMap" })
                {
                    if (!m.HasProperty(prop)) continue;
                    var t = m.GetTexture(prop);
                    if (t == null) continue;
                    string p = AssetDatabase.GetAssetPath(t);
                    if (!string.IsNullOrEmpty(p) && !paths.Contains(p) && p.StartsWith("Assets/"))
                        paths.Add(p);
                }
            }
            paths.Sort();
            return paths;
        }

        // ─────────────────────────────────────────────────────────
        // 포스트 프로세싱
        // ─────────────────────────────────────────────────────────

        static IEnumerable<VolumeProfile> Profiles()
        {
            foreach (var v in FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var p = v.sharedProfile != null ? v.sharedProfile : v.profile;
                if (p != null) yield return p;
            }
        }

        static string PostReport()
        {
            var sb = new StringBuilder();
            bool any = false;

            foreach (var prof in Profiles())
            {
                any = true;
                sb.AppendLine($"  프로파일 {prof.name}");

                if (prof.TryGet<DepthOfField>(out var dof))
                    sb.AppendLine($"    DOF   {(dof.active ? "켜짐" : "꺼짐")}  mode={dof.mode.value} " +
                                  $"focus={dof.focusDistance.value:0.#} aperture={dof.aperture.value:0.#}" +
                                  (dof.active ? "   ← 초점 밖은 엔진이 직접 흐린다" : ""));
                if (prof.TryGet<Bloom>(out var bloom))
                    sb.AppendLine($"    Bloom {(bloom.active ? "켜짐" : "꺼짐")}  " +
                                  $"threshold={bloom.threshold.value:0.##} intensity={bloom.intensity.value:0.##}" +
                                  (bloom.active && bloom.intensity.value > 0.5f ? "   ← 밝은 픽셀이 번진다" : ""));
            }

            if (!any) sb.AppendLine("  씬에 Volume 이 없다.");

            foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null) continue;
                sb.AppendLine($"  카메라 {cam.name}: 포스트={(data.renderPostProcessing ? "켜짐" : "꺼짐")} " +
                              $"AA={data.antialiasing}");
            }
            return sb.ToString();
        }

        void ToggleDof(bool on)
        {
            int n = 0;
            foreach (var prof in Profiles())
                if (prof.TryGet<DepthOfField>(out var dof)) { dof.active = on; EditorUtility.SetDirty(prof); n++; }
            AssetDatabase.SaveAssets();
            report = $"DOF {(on ? "켬" : "끔")} — 프로파일 {n}개\n\n" + PostReport();
        }

        void ToggleBloom(bool on)
        {
            int n = 0;
            foreach (var prof in Profiles())
                if (prof.TryGet<Bloom>(out var b)) { b.active = on; EditorUtility.SetDirty(prof); n++; }
            AssetDatabase.SaveAssets();
            report = $"Bloom {(on ? "켬" : "끔")} — 프로파일 {n}개\n\n" + PostReport();
        }

        /// <summary>
        /// URP 에셋은 타입을 직접 참조하지 않고 직렬화 필드로 읽는다 —
        /// 버전마다 프로퍼티 이름이 바뀌어도 깨지지 않는다.
        /// </summary>
        static string PipelineReport()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null) return "  현재 렌더 파이프라인 에셋이 없다 (빌트인).\n";

            var so = new SerializedObject(rp);
            var sb = new StringBuilder();
            sb.AppendLine($"  {rp.name}");

            var msaa = so.FindProperty("m_MSAA");
            if (msaa != null)
                sb.AppendLine($"    MSAA        {msaa.intValue}" +
                              (msaa.intValue > 1 ? "   ← 픽셀 경계를 뭉갠다. 1 로 내릴 것" : ""));

            var rs = so.FindProperty("m_RenderScale");
            if (rs != null)
                sb.AppendLine($"    RenderScale {rs.floatValue:0.##}" +
                              (Mathf.Abs(rs.floatValue - 1f) > 0.001f ? "   ← 1.0 이 아니면 화면이 리샘플된다" : ""));

            var uf = so.FindProperty("m_UpscalingFilter");
            if (uf != null) sb.AppendLine($"    Upscaling   {uf.intValue}");

            return sb.ToString();
        }
    }
}
