using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace RealSteel.EditorTools
{
    /// <summary>
    /// ProBuilder 오브젝트의 UV 밀도를 머티리얼에 붙은 <b>텍스처 크기에서 자동으로</b> 맞춘다.
    ///
    /// 생성기는 타일 텍스처를 64px 로 가정하고 UV 스케일을 한 번에 박는다. 그런데 실제
    /// 텍스처는 16px 일 수도 512px 일 수도 있어서, 상수 하나로는 절대 안 맞는다 —
    /// 벽마다 텍셀 크기가 달라지고 어떤 면은 늘어나 보인다.
    ///
    /// 여기서는 면마다 붙은 텍스처의 실제 픽셀 수를 읽어 이렇게 계산한다.
    ///
    ///     UV 스케일 = PPU / 텍스처 픽셀 수
    ///
    /// 32PPU 기준으로 64px 텍스처는 2유닛, 512px 텍스처는 16유닛을 덮는다 —
    /// 둘 다 화면에서는 같은 크기의 픽셀로 보인다. 그게 텍셀 밀도 통일이다.
    ///
    /// 정사각형이 아닌 텍스처도 가로·세로를 따로 계산하므로 찌그러지지 않는다.
    /// </summary>
    public class StageUvTool : EditorWindow
    {
        /// <summary>픽셀/유닛. 지형 셰이더 PPU 와 같은 값을 쓴다.</summary>
        float ppu = 32f;

        /// <summary>0 이면 머티리얼 텍스처에서 읽는다. 1 이상이면 이 값을 강제한다.</summary>
        int forcedTexturePixels = 0;

        bool worldSpace = true;
        bool perAxis = true;
        Vector2 extraScale = Vector2.one;

        Vector2 scroll;
        string report = "";

        [MenuItem("Tools/RE_AL STEEL/Stage/UV 밀도 맞추기 (텍스처 크기 자동)", false, 24)]
        static void Open()
        {
            var w = GetWindow<StageUvTool>("UV 밀도");
            w.minSize = new Vector2(440f, 460f);
        }

        // ─────────────────────────────────────────────────────────

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "머티리얼에 붙은 텍스처의 픽셀 수를 읽어 UV 스케일을 계산한다.\n" +
                "UV 스케일 = PPU ÷ 텍스처 픽셀 수",
                MessageType.None);

            EditorGUILayout.Space(4f);
            ppu = EditorGUILayout.FloatField(
                new GUIContent("PPU", "픽셀/유닛. 프로젝트 전체가 같은 값이어야 한다 (기본 32)"), ppu);

            forcedTexturePixels = EditorGUILayout.IntField(
                new GUIContent("텍스처 픽셀 강제",
                    "0 = 머티리얼 텍스처에서 자동으로 읽는다.\n" +
                    "1 이상 = 이 값으로 강제한다. 텍스처가 아직 없을 때 쓴다."),
                Mathf.Max(0, forcedTexturePixels));

            EditorGUILayout.Space(4f);
            worldSpace = EditorGUILayout.Toggle(
                new GUIContent("월드 스페이스 투영",
                    "켜면 오브젝트를 옮기거나 돌려도 밀도가 유지된다. 배경에는 거의 항상 켠다."),
                worldSpace);

            perAxis = EditorGUILayout.Toggle(
                new GUIContent("가로·세로 따로",
                    "정사각형이 아닌 텍스처가 찌그러지지 않게 축마다 계산한다."),
                perAxis);

            extraScale = EditorGUILayout.Vector2Field(
                new GUIContent("추가 배율", "1 = 계산값 그대로. 2 = 두 배로 촘촘하게."),
                extraScale);

            EditorGUILayout.Space(10f);

            if (GUILayout.Button("선택한 오브젝트에 적용", GUILayout.Height(30f)))
                Apply(Selection.gameObjects);

            if (GUILayout.Button("씬 전체 ProBuilder 오브젝트에 적용", GUILayout.Height(24f)))
                Apply(FindObjectsByType<ProBuilderMesh>(FindObjectsSortMode.None)
                      .Select(m => m.gameObject).ToArray());

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("선택한 오브젝트 진단만 (적용 안 함)"))
                Diagnose(Selection.gameObjects);

            EditorGUILayout.Space(8f);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────────────────

        void Apply(GameObject[] targets)
        {
            var meshes = Collect(targets);
            if (meshes.Count == 0) { report = "ProBuilder 오브젝트를 못 찾았다."; return; }

            var log = new StringBuilder();
            int touched = 0;

            foreach (var pb in meshes)
            {
                var tex = TextureOf(pb);
                Vector2 px = PixelsOf(tex);

                Vector2 scale = perAxis
                    ? new Vector2(ppu / Mathf.Max(1f, px.x), ppu / Mathf.Max(1f, px.y))
                    : Vector2.one * (ppu / Mathf.Max(1f, px.x));
                scale = Vector2.Scale(scale, extraScale);

                Undo.RecordObject(pb, "UV 밀도 맞추기");

                var faces = pb.faces;
                for (int i = 0; i < faces.Count; i++)
                {
                    var f = faces[i];
                    f.manualUV = false;                 // 수동 UV 면은 자동 투영을 무시한다

                    var uv = f.uv;
                    uv.useWorldSpace = worldSpace;
                    // Fill 이 Fit/Stretch 면 면 크기에 맞춰 늘어난다 — 타일링이 아니게 된다
                    uv.fill = AutoUnwrapSettings.Fill.Tile;
                    uv.anchor = AutoUnwrapSettings.Anchor.None;
                    uv.rotation = 0f;
                    uv.offset = Vector2.zero;
                    uv.scale = scale;
                    f.uv = uv;
                }

                pb.ToMesh();
                pb.Refresh(RefreshMask.All);
                EditorUtility.SetDirty(pb);
                touched++;

                float unitsX = px.x / Mathf.Max(0.0001f, ppu);
                float unitsY = px.y / Mathf.Max(0.0001f, ppu);
                log.AppendLine(
                    $"{pb.name,-26} {(tex != null ? tex.name : "(텍스처 없음)"),-22} " +
                    $"{px.x:0}x{px.y:0}px → scale ({scale.x:0.###}, {scale.y:0.###})  " +
                    $"= {unitsX:0.##}x{unitsY:0.##}유닛에 한 번");
            }

            if (meshes.Count > 0)
            {
                var scene = meshes[0].gameObject.scene;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            }

            log.Insert(0, $"{touched}개 오브젝트에 적용 (PPU {ppu:0.##})\n\n");
            report = log.ToString();
            Debug.Log($"[Stage] UV 밀도 맞추기 — {touched}개 오브젝트");
        }

        void Diagnose(GameObject[] targets)
        {
            var meshes = Collect(targets);
            if (meshes.Count == 0) { report = "ProBuilder 오브젝트를 못 찾았다."; return; }

            var log = new StringBuilder();
            log.AppendLine($"PPU {ppu:0.##} 기준 진단\n");

            foreach (var pb in meshes)
            {
                var tex = TextureOf(pb);
                Vector2 px = PixelsOf(tex);
                var faces = pb.faces;

                // 면마다 설정이 다르면 그 자체가 문제다
                var scales = new HashSet<string>();
                var fills = new HashSet<string>();
                int manual = 0;
                for (int i = 0; i < faces.Count; i++)
                {
                    var f = faces[i];
                    if (f.manualUV) manual++;
                    scales.Add($"{f.uv.scale.x:0.###},{f.uv.scale.y:0.###}");
                    fills.Add(f.uv.fill.ToString() + (f.uv.useWorldSpace ? "/World" : "/Local"));
                }

                Vector2 want = perAxis
                    ? new Vector2(ppu / Mathf.Max(1f, px.x), ppu / Mathf.Max(1f, px.y))
                    : Vector2.one * (ppu / Mathf.Max(1f, px.x));

                log.AppendLine($"● {pb.name}   면 {faces.Count}개");
                log.AppendLine($"    텍스처 : {(tex != null ? $"{tex.name} {px.x:0}x{px.y:0}px" : "없음 — 머티리얼에 BaseMap 이 비어 있다")}");
                log.AppendLine($"    현재   : scale [{string.Join(" / ", scales)}]  fill [{string.Join(" / ", fills)}]");
                log.AppendLine($"    맞는값 : scale ({want.x:0.###}, {want.y:0.###})");
                if (manual > 0) log.AppendLine($"    ! 수동 UV 면 {manual}개 — 자동 투영이 안 먹는다");
                if (fills.Any(s => s.StartsWith("Fit") || s.StartsWith("Stretch")))
                    log.AppendLine("    ! Fill 이 Tile 이 아니다 — 면 크기에 맞춰 늘어난다");
                log.AppendLine();
            }
            report = log.ToString();
        }

        // ─────────────────────────────────────────────────────────

        static List<ProBuilderMesh> Collect(GameObject[] targets)
        {
            var list = new List<ProBuilderMesh>();
            if (targets == null) return list;

            foreach (var go in targets)
            {
                if (go == null) continue;
                foreach (var pb in go.GetComponentsInChildren<ProBuilderMesh>(true))
                    if (!list.Contains(pb)) list.Add(pb);
            }
            return list;
        }

        /// <summary>URP Lit 은 _BaseMap, 빌트인은 _MainTex 를 쓴다. 둘 다 본다.</summary>
        static Texture TextureOf(ProBuilderMesh pb)
        {
            var mr = pb.GetComponent<MeshRenderer>();
            var mat = mr != null ? mr.sharedMaterial : null;
            if (mat == null) return null;

            if (mat.HasProperty("_BaseMap"))
            {
                var t = mat.GetTexture("_BaseMap");
                if (t != null) return t;
            }
            if (mat.HasProperty("_MainTex"))
            {
                var t = mat.GetTexture("_MainTex");
                if (t != null) return t;
            }
            return mat.mainTexture;
        }

        Vector2 PixelsOf(Texture tex)
        {
            if (forcedTexturePixels > 0)
                return new Vector2(forcedTexturePixels, forcedTexturePixels);
            if (tex == null) return new Vector2(64f, 64f);   // 텍스처가 없으면 생성기 기본값
            return new Vector2(Mathf.Max(1, tex.width), Mathf.Max(1, tex.height));
        }
    }
}
