// RE:AL STEEL - 분위기 설치 메뉴 (볼류메트릭 안개 · 캐릭터 밤 빛 · 자동 초점) + 인스펙터
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RealSteel.Common.EditorTools;

namespace RealSteel.Lighting.EditorTools
{
    public static class RSAtmosphereMenu
    {
        const string GenName = "__RST_Generated";

        public static void Setup()
        {
            var log = new System.Text.StringBuilder("[분위기] ");

            // 1) 안개 상자 — RS 지형을 덮게
            var fog = Object.FindAnyObjectByType<RSFogVolume>();
            if (fog == null)
            {
                var go = new GameObject("FOG_Volume");
                Undo.RegisterCreatedObjectUndo(go, "안개 상자");
                fog = go.AddComponent<RSFogVolume>();
                if (TerrainBounds(out Bounds b))
                {
                    go.transform.position = new Vector3(b.center.x, b.min.y + 4f, b.center.z);
                    fog.size = new Vector3(b.size.x + 4f, 8f, b.size.z + 4f);
                }
                log.Append("안개 상자를 만들었습니다. ");
            }
            else log.Append("안개 상자는 이미 있습니다. ");

            // 2) 캐릭터 밤 빛
            var ch = FindCharacter();
            if (ch != null)
            {
                var glow = ch.GetComponent<RSCharacterGlow>();
                if (glow == null)
                {
                    glow = Undo.AddComponent<RSCharacterGlow>(ch.gameObject);
                    // 이미 붙여 둔 포인트 라이트가 있으면 그걸 쓴다
                    foreach (var l in ch.GetComponentsInChildren<Light>(true))
                        if (l.type == LightType.Point && !l.name.StartsWith("__RS_")) { glow.glowLight = l; break; }
                    log.Append($"'{ch.name}' 에 캐릭터 밤 빛을 붙였습니다{(glow.glowLight != null ? " (붙여 둔 라이트 사용)" : "")}. ");
                }
                else log.Append("캐릭터 밤 빛은 이미 있습니다. ");
            }
            else log.Append("캐릭터(Player 태그 또는 CharacterController)를 못 찾아 밤 빛은 건너뜁니다. ");

            // 3) 자동 초점 — 화면에 실제로 보이는 카메라에 (MainCamera 태그가 둘이어도 맞는 쪽)
            var cam = RSAutoFocus.FindViewCamera();
            if (cam != null)
            {
                var existing = Object.FindAnyObjectByType<RSAutoFocus>();
                if (existing == null)
                {
                    Undo.AddComponent<RSAutoFocus>(cam.gameObject);
                    log.Append($"'{cam.name}' 카메라에 자동 초점을 붙였습니다. ");
                }
                else log.Append($"자동 초점은 이미 '{existing.name}' 에 있습니다. ");
                var ad = cam.GetUniversalAdditionalCameraData();
                if (ad != null && !ad.renderPostProcessing)
                {
                    Undo.RecordObject(ad, "Post Processing");
                    ad.renderPostProcessing = true;
                    log.Append("카메라 Post Processing 을 켰습니다. ");
                }
                if (ad != null && ad.requiresDepthOption == CameraOverrideOption.Off)
                {
                    Undo.RecordObject(ad, "Depth Texture");
                    ad.requiresDepthOption = CameraOverrideOption.On;
                    log.Append("카메라 Depth Texture 를 켰습니다 (안개가 벽 뒤로 새지 않게). ");
                }
                var hidden = RSAutoFocusEditor.HiddenCameras(cam);
                if (hidden.Count > 0)
                    log.Append($"※ 화면에 안 보이는데 켜져 있는 카메라가 있습니다 ({string.Join(", ", hidden.ConvertAll(h => h.name))}) — 자동 초점 인스펙터에서 끌 수 있습니다. ");
            }
            else log.Append("화면을 그리는 카메라가 없어 자동 초점은 건너뜁니다. ");

            if (fog != null) Selection.activeGameObject = fog.gameObject;
            Debug.Log(log.ToString());
        }

        [MenuItem("GameObject/RE_AL STEEL/볼류메트릭 안개 상자", false, 12)]
        static void CreateFog()
        {
            var go = new GameObject("FOG_Volume");
            Undo.RegisterCreatedObjectUndo(go, "안개 상자");
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) go.transform.position = sv.pivot;
            go.AddComponent<RSFogVolume>();
            Selection.activeGameObject = go;
        }

        static Transform FindCharacter()
        {
            GameObject p = null;
            try { p = GameObject.FindWithTag("Player"); } catch { }
            if (p != null) return p.transform;
            var cc = Object.FindAnyObjectByType<CharacterController>();
            return cc != null ? cc.transform : null;
        }

        /// <summary>RS 지형이 만든 메시들의 경계 (없으면 false)</summary>
        static bool TerrainBounds(out Bounds b)
        {
            b = new Bounds(); bool any = false;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var t = r.transform; bool gen = false;
                for (var q = t; q != null; q = q.parent) if (q.name.StartsWith(GenName)) { gen = true; break; }
                if (!gen || !r.name.StartsWith("Chunk")) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }
    }

    // ── 인스펙터 (요약 상자) ──
    [CustomEditor(typeof(RSFogVolume)), CanEditMultipleObjects]
    public class RSFogVolumeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            RSLookInspector.Draw(this);
        }
    }

    [CustomEditor(typeof(RSCharacterGlow))]
    public class RSCharacterGlowEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            RSLookInspector.Draw(this);
        }
    }

    [CustomEditor(typeof(RSAutoFocus))]
    public class RSAutoFocusEditor : Editor
    {
        VolumeStack stack;   // 이 카메라가 실제로 받는 최종 후처리 값을 재기 위한 전용 스택

        void OnDisable() { if (stack != null) { stack.Dispose(); stack = null; } }

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            var af = (RSAutoFocus)target;

            var cam = af.CurrentCamera != null ? af.CurrentCamera : (af.cam != null ? af.cam : RSAutoFocus.FindViewCamera());
            var tgt = af.CurrentTarget != null ? af.CurrentTarget : (af.target != null ? af.target : RSAutoFocus.FindCharacter());
            var src = af.CurrentSource != null ? af.CurrentSource : (af.source != null ? af.source : RSAutoFocus.FindDofVolume());

            EditorGUILayout.HelpBox(
                $"DOF Volume: {(src != null ? src.name : "없음")}   카메라: {(cam != null ? cam.name : "없음")}   대상: {(tgt != null ? tgt.name : "없음")}\n" +
                $"캐릭터 깊이: {(af.TargetDepth > 0f ? af.TargetDepth.ToString("0.0") + " m" : "-")}   초점: {(af.CurrentFocus > 0f ? af.CurrentFocus.ToString("0.0") + " m" : "-")}",
                MessageType.None);
            if (src == null) EditorGUILayout.HelpBox("Depth Of Field 가 켜진 Volume 을 못 찾았습니다. 아래 'Source' 칸에 DOF Volume 을 넣으세요.", MessageType.Warning);
            else
            {
                // DOF 가 켜진 Volume 이 여럿이면 조리개 · 모드가 서로 섞인다 → 하나만 남기라고 알려 준다
                var others = new System.Collections.Generic.List<string>();
                foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                    if (v != null && v != src && v.isActiveAndEnabled && !v.name.StartsWith("__RS") && RSAutoFocus.TryGetDof(v, out _))
                        others.Add($"'{v.name}'(세기 {v.weight:0.##})");
                if (others.Count > 0)
                    EditorGUILayout.HelpBox($"DOF 가 켜진 Volume 이 더 있습니다: {string.Join(", ", others)}. 모드 · 조리개가 '{src.name}' 과 섞여 흐림 모양이 예상과 달라집니다. " +
                        "한쪽 프로필에서 Depth Of Field 를 끄세요 (초점 거리는 자동 초점이 맞춰 줍니다).", MessageType.Warning);
            }
            if (tgt == null) EditorGUILayout.HelpBox("초점 대상을 못 찾았습니다. 아래 '대상' 칸에 캐릭터를 넣거나 캐릭터에 Player 태그를 붙이세요.", MessageType.Warning);

            RSInspector.Draw(serializedObject);

            if (cam != null) DrawDiagnosis(af, cam);
            EditorGUILayout.Space(4f);
            RSVolumeReport.DrawOwned(af);

            if (cam != null)
            {
                var hidden = HiddenCameras(cam);
                if (hidden.Count > 0)
                {
                    EditorGUILayout.HelpBox($"화면엔 '{cam.name}' 만 보이는데 다른 카메라 {hidden.Count}대도 켜져 있어 씬을 매 프레임 한 번 더 그립니다 (보이지도 않는데 비용만 두 배). " +
                        "MainCamera 태그가 둘이면 Camera.main 도 엉뚱한 쪽을 가리킵니다.", MessageType.Warning);
                    foreach (var h in hidden)
                        if (GUILayout.Button($"'{h.name}' 카메라 끄기 (Camera 컴포넌트만 · 오디오 리스너는 그대로 · 되돌리기 가능)"))
                        {
                            Undo.RecordObject(h, "카메라 끄기");
                            h.enabled = false;
                            EditorUtility.SetDirty(h);
                        }
                }
            }
        }

        /// <summary>
        /// 이 카메라가 실제로 받는 최종 값(모든 Volume 을 섞은 결과)으로 '캐릭터가 왜 흐린가'를 짚는다.
        /// DOF 가 캐릭터를 흐리게 하는지 숫자로 보여주고, DOF 말고 화면을 뭉개는 다른 원인도 같이 나열한다.
        /// </summary>
        void DrawDiagnosis(RSAutoFocus af, Camera cam)
        {
            var ad = cam.GetUniversalAdditionalCameraData();
            if (ad != null && !ad.renderPostProcessing)
            {
                EditorGUILayout.HelpBox($"'{cam.name}' 카메라의 Post Processing 이 꺼져 있어 후처리(DOF 포함)가 전부 안 보입니다.", MessageType.Warning);
                return;
            }

            if (!VolumeManager.instance.isInitialized) return;
            if (stack == null) stack = VolumeManager.instance.CreateStack();
            LayerMask mask = ad != null ? ad.volumeLayerMask : (LayerMask)1;
            Transform trigger = ad != null && ad.volumeTrigger != null ? ad.volumeTrigger : cam.transform;
            VolumeManager.instance.Update(stack, trigger, mask);

            var lines = new System.Text.StringBuilder("진단 (이 카메라가 받는 최종 값)\n");
            bool dofBlursChar = false;

            var dof = stack.GetComponent<DepthOfField>();
            float d = af.TargetDepth;
            if (dof == null || !dof.IsActive()) lines.Append("· DOF: 꺼짐\n");
            else if (dof.mode.value == DepthOfFieldMode.Gaussian)
            {
                float s0 = dof.gaussianStart.value, s1 = dof.gaussianEnd.value;
                float k = d > 0f ? Mathf.Clamp01((d - s0) / Mathf.Max(0.01f, s1 - s0)) : 0f;
                lines.Append($"· DOF 가우시안: {s0:0.0} m 부터 흐려져 {s1:0.0} m 에서 최대 (반경 {dof.gaussianMaxRadius.value:0.0})");
                lines.Append(d > 0f ? $" → 캐릭터({d:0.0} m) 흐림 {k * 100f:0}%\n" : "\n");
                dofBlursChar = k > 0.05f;
            }
            else if (dof.mode.value == DepthOfFieldMode.Bokeh)
            {
                // URP 보케 CoC 식 그대로: maxCoC = (f/N · f) / (P - f), coc = (1 - P/D)·maxCoC, 화면 기준 최대 14px
                float F = dof.focalLength.value / 1000f;
                float A = dof.focalLength.value / Mathf.Max(0.1f, dof.aperture.value);
                float P = dof.focusDistance.value;
                float maxCoC = (A * F) / Mathf.Max(P - F, 1e-3f);
                float px = d > 0f ? Mathf.Abs(Mathf.Clamp((1f - P / d) * maxCoC, -1f, 1f)) * 14f : 0f;
                float near = Mathf.Abs(Mathf.Clamp((1f - P / Mathf.Max(0.5f, d * 0.6f)) * maxCoC, -1f, 1f)) * 14f;
                float far = Mathf.Abs(Mathf.Clamp((1f - P / (d * 2f + 0.01f)) * maxCoC, -1f, 1f)) * 14f;
                lines.Append($"· DOF 보케: 초점 {P:0.0} m · 렌즈 {dof.focalLength.value:0} mm · 조리개 f/{dof.aperture.value:0.0}");
                lines.Append(d > 0f ? $" → 캐릭터 번짐 약 {px:0.0} px (앞 바닥 {near:0.0} px · 두 배 먼 배경 {far:0.0} px)\n" : "\n");
                dofBlursChar = px > 1f;
            }

            // DOF 말고 화면 전체를 뭉개는 것들
            var mb = stack.GetComponent<MotionBlur>();
            if (mb != null && mb.IsActive()) lines.Append($"· 모션 블러 켜짐 (세기 {mb.intensity.value:0.00}) — 카메라가 따라 움직이면 캐릭터까지 전부 번진다 ← 흔한 범인\n");
            var ca = stack.GetComponent<ChromaticAberration>();
            if (ca != null && ca.IsActive()) lines.Append($"· 색수차 {ca.intensity.value:0.00} — 가장자리 색 번짐\n");
            var ld = stack.GetComponent<LensDistortion>();
            if (ld != null && ld.IsActive()) lines.Append($"· 렌즈 왜곡 {ld.intensity.value:0.00} — 픽셀이 휘어 뭉개짐\n");
            var pp = stack.GetComponent<PaniniProjection>();
            if (pp != null && pp.IsActive()) lines.Append($"· 파니니 투영 {pp.distance.value:0.00} — 픽셀이 휘어 뭉개짐\n");
            var fg = stack.GetComponent<FilmGrain>();
            if (fg != null && fg.IsActive()) lines.Append($"· 필름 그레인 {fg.intensity.value:0.00}\n");
            var bl = stack.GetComponent<Bloom>();
            if (bl != null && bl.IsActive() && bl.intensity.value > 1.5f) lines.Append($"· 블룸 세기 {bl.intensity.value:0.0} (산란 {bl.scatter.value:0.00}) — 밝은 곳이 뿌옇게 번짐\n");

            foreach (var fog in Object.FindObjectsByType<RSFogVolume>(FindObjectsSortMode.None))
                if (fog.isActiveAndEnabled)
                    lines.Append($"· 볼류메트릭 안개 '{fog.name}' 밀도 {fog.L.density:0.00} · 디더 {fog.ditherPixel}px — 캐릭터 앞에 뿌연 막 (0.03 이하면 옅음)\n");

            if (!dofBlursChar && af.TargetDepth > 0f)
                lines.Append("⇒ DOF 는 지금 캐릭터를 흐리게 하지 않습니다. 그래도 흐리면 위 목록(모션 블러 · 안개 등)이나 URP 에셋 Render Scale(1 미만이면 전체가 뭉개짐)을 보세요.");

            EditorGUILayout.HelpBox(lines.ToString().TrimEnd(), dofBlursChar ? MessageType.Warning : MessageType.Info);

            // 빠른 비교: 안개 끄고/켜고
            foreach (var fog in Object.FindObjectsByType<RSFogVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (GUILayout.Button($"비교: 안개 '{fog.name}' {(fog.enabled ? "끄기" : "켜기")}"))
                {
                    Undo.RecordObject(fog, "안개 켜기/끄기");
                    fog.enabled = !fog.enabled;
                }
        }

        /// <summary>view 카메라 밑에 깔려서 안 보이는데 켜져 있는 화면용 카메라들 (전체 화면 · 더 낮은 Depth)</summary>
        public static System.Collections.Generic.List<Camera> HiddenCameras(Camera view)
        {
            var list = new System.Collections.Generic.List<Camera>();
            if (view == null) return list;
            bool viewFull = view.rect == new Rect(0, 0, 1, 1);
            foreach (var c in Camera.allCameras)
            {
                if (c == null || c == view || c.targetTexture != null || c.cameraType != CameraType.Game || c.name.StartsWith("__RS")) continue;
                var ad = c.GetUniversalAdditionalCameraData();
                if (ad != null && ad.renderType == CameraRenderType.Overlay) continue;
                if (viewFull && c.depth < view.depth && c.targetDisplay == view.targetDisplay) list.Add(c);
            }
            return list;
        }
    }
}
