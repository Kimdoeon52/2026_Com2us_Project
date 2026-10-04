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
            var f = (RSFogVolume)target;
            if (RSDebugToggle.Draw(f.debugShafts, "빛줄기만 보기 (조절용)",
                    "켜면 안개 대신 빛기둥(빛줄기 강조 부분)만 4배로 보여 준다. '갓레이 (안개 속 빛줄기)' 묶음 값을 맞춘 뒤 끈다.", out bool on))
            {
                foreach (var t in targets)
                {
                    var v = (RSFogVolume)t;
                    Undo.RecordObject(v, "빛줄기만 보기");
                    v.debugShafts = on;
                    EditorUtility.SetDirty(v);
                }
                SceneView.RepaintAll();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("빛줄기 세기", GUILayout.Width(70f));
                foreach (RSFogVolume.ShaftPreset p in System.Enum.GetValues(typeof(RSFogVolume.ShaftPreset)))
                    if (GUILayout.Button(new GUIContent(p.ToString(), "밀도 · 햇빛 세기 · 빛줄기 강조 · 대비를 한 번에 맞춘다 (나머지는 그대로)"), EditorStyles.miniButton))
                        foreach (var t in targets)
                        {
                            var v = (RSFogVolume)t;
                            RSLookEdit.Record(v, v.UsesStageLook, "빛줄기 세기");
                            v.ApplyShaftPreset(p);
                            RSLookEdit.Dirty(v, v.UsesStageLook);
                        }
            }
            EditorGUILayout.Space(2f);
            RSLookInspector.Draw(this, "zones");
            if (targets.Length == 1) DrawZones(f);
        }

        // ─────────────────────────────────────────────────────────────
        // 안개 구역 목록 (이 상자 안 네모 구역마다 밀도 · 빛줄기 · 색)
        // ─────────────────────────────────────────────────────────────
        static int editing = -1;   // 씬 뷰 손잡이를 띄울 구역

        void DrawZones(RSFogVolume f)
        {
            serializedObject.Update();
            var list = serializedObject.FindProperty("zones");
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("안개 구역 " + list.arraySize + (list.arraySize > RSFogVolume.MaxZones ? " (8개까지만 적용)" : ""), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("+ 선택한 오브젝트 크기로", "하이어라키에서 고른 오브젝트(예: 방 프리팹)의 렌더러 범위로 구역을 만들고 그 오브젝트를 따라가게 한다"), EditorStyles.miniButton))
                    AddFromSelection(f);
                if (GUILayout.Button(new GUIContent("+ 빈 구역", "씬 뷰 가운데에 10×4×10 구역"), EditorStyles.miniButton))
                {
                    var sv = SceneView.lastActiveSceneView;
                    Vector3 c = sv != null && f.WorldBounds.Contains(sv.pivot) ? sv.pivot : f.transform.position;
                    AddZone(f, new RSFogVolume.Zone { name = "구역 " + (f.zones.Count + 1), center = Quaternion.Inverse(f.transform.rotation) * (c - f.transform.position) });
                }
            }
            if (list.arraySize == 0)
            {
                EditorGUILayout.HelpBox("실내 · 골목 · 늪처럼 일부만 안개를 다르게 하려면 구역을 추가하세요. 위에서부터 차례로 적용됩니다.\n" +
                    "곱하기 ×3 = 진하게 / ×0 = 없앰 / 더하기 = 안개 추가 / 덮어쓰기 = 이 값으로", MessageType.None);
                return;
            }

            Bounds fb = f.WorldBounds;
            bool anyOut = false;
            int remove = -1, up = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                var z = f.zones[i];
                Bounds zb = z.WorldBounds(f.transform);
                bool covered = fb.Contains(zb.min) && fb.Contains(zb.max);
                anyOut |= !covered && z.enabled;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var en = e.FindPropertyRelative("enabled");
                        en.boolValue = EditorGUILayout.Toggle(en.boolValue, GUILayout.Width(16f));
                        bool open = editing == i;
                        var old = GUI.color;
                        if (i >= RSFogVolume.MaxZones) GUI.color = new Color(1f, 0.6f, 0.5f);
                        string head = (i + 1) + ". " + z.name + "   안개 " + z.Summary +
                            (Mathf.Approximately(z.shaftMultiplier, 1f) ? "" : " · 빛줄기 ×" + z.shaftMultiplier.ToString("0.##")) +
                            (z.follow != null ? "   ← " + z.follow.name : "") + (!covered ? "   (상자 밖으로 나감)" : "") + (i >= RSFogVolume.MaxZones ? "   (무시됨)" : "");
                        bool now = EditorGUILayout.Foldout(open, head, true);
                        GUI.color = old;
                        if (now != open) { editing = now ? i : -1; SceneView.RepaintAll(); }
                        if (!covered && GUILayout.Button(new GUIContent("늘리기", "이 안개 상자를 이 구역까지 덮게 늘린다"), EditorStyles.miniButton, GUILayout.Width(46f)))
                            Grow(f, zb);
                        GUI.enabled = i > 0;
                        if (GUILayout.Button(new GUIContent("▲", "먼저 적용"), EditorStyles.miniButtonLeft, GUILayout.Width(22f))) up = i;
                        GUI.enabled = true;
                        if (GUILayout.Button(new GUIContent("✕", "삭제"), EditorStyles.miniButtonRight, GUILayout.Width(22f))) remove = i;
                    }
                    if (editing == i)
                    {
                        EditorGUI.indentLevel++;
                        foreach (var n in new[] { "name", "follow", "center", "size", "edgeSoftness", "mode", "density", "shaftMultiplier", "tint" })
                            EditorGUILayout.PropertyField(e.FindPropertyRelative(n));
                        EditorGUI.indentLevel--;
                        float baseD = f.L.density;
                        string eff = z.mode == RSFogVolume.ZoneMode.곱하기 ? baseD.ToString("0.###") + " × " + z.density.ToString("0.##") + " = " + (baseD * z.density).ToString("0.###")
                                   : z.mode == RSFogVolume.ZoneMode.더하기 ? baseD.ToString("0.###") + " + " + z.density.ToString("0.###") + " = " + (baseD + z.density).ToString("0.###")
                                   : z.density.ToString("0.###") + " (원래 " + baseD.ToString("0.###") + " 무시)";
                        EditorGUILayout.LabelField("밀도 (바닥 기준): " + eff, EditorStyles.miniLabel);
                    }
                }
            }
            if (remove >= 0) { list.DeleteArrayElementAtIndex(remove); if (editing == remove) editing = -1; }
            if (up > 0) { list.MoveArrayElement(up, up - 1); if (editing == up) editing = up - 1; }
            serializedObject.ApplyModifiedProperties();
            if (anyOut)
                EditorGUILayout.HelpBox("상자 밖으로 나간 구역 부분은 효과가 없습니다. '늘리기' 로 이 안개 상자를 그 구역까지 늘리세요.", MessageType.Warning);
        }

        static void AddZone(RSFogVolume f, RSFogVolume.Zone z)
        {
            Undo.RecordObject(f, "안개 구역 추가");
            f.zones.Add(z);
            EditorUtility.SetDirty(f);
            editing = f.zones.Count - 1;
            SceneView.RepaintAll();
        }

        static void AddFromSelection(RSFogVolume f)
        {
            var t = Selection.activeTransform;
            if (t == null || t == f.transform)
            {
                EditorUtility.DisplayDialog("안개 구역", "하이어라키에서 구역으로 만들 오브젝트(예: 방 프리팹)를 먼저 고른 뒤, 이 안개 상자를 잠그거나(인스펙터 자물쇠) 다시 이 버튼을 누르세요.\n\n" +
                    "팁: 인스펙터 오른쪽 위 자물쇠로 이 안개 상자를 고정하면 다른 오브젝트를 골라도 이 화면이 유지됩니다.", "확인");
                return;
            }
            bool any = false; Bounds b = new Bounds();
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) b = new Bounds(t.position, new Vector3(10f, 4f, 10f));
            // 따라가는 오브젝트 기준 (회전은 따라가므로 월드 범위를 그 회전으로 되돌린다)
            var inv = Quaternion.Inverse(t.rotation);
            Vector3 c = inv * (b.center - t.position);
            Vector3 sz = b.size;
            if (Mathf.Abs(Quaternion.Angle(t.rotation, Quaternion.identity)) > 1f)
            {
                // 회전된 오브젝트: 축 정렬 범위를 오브젝트 축으로 다시 잰다
                Vector3 mn = Vector3.one * float.MaxValue, mx = -mn;
                for (int k = 0; k < 8; k++)
                {
                    var corner = new Vector3((k & 1) != 0 ? b.max.x : b.min.x, (k & 2) != 0 ? b.max.y : b.min.y, (k & 4) != 0 ? b.max.z : b.min.z);
                    var q = inv * (corner - t.position);
                    mn = Vector3.Min(mn, q); mx = Vector3.Max(mx, q);
                }
                c = (mn + mx) * 0.5f; sz = mx - mn;
            }
            var z = new RSFogVolume.Zone { name = t.name, follow = t, center = c, size = sz, edgeSoftness = 0.3f };
            AddZone(f, z);
            Bounds zb = z.WorldBounds(f.transform);
            Bounds fb = f.WorldBounds;
            if (!(fb.Contains(zb.min) && fb.Contains(zb.max))) Grow(f, zb);
        }

        /// <summary>안개 상자를 구역까지 덮게 늘린다 (원래 덮던 곳은 그대로)</summary>
        static void Grow(RSFogVolume f, Bounds zoneWorld)
        {
            var t = f.transform;
            var l2w = Matrix4x4.TRS(t.position, t.rotation, t.lossyScale);
            var w2l = l2w.inverse;
            Vector3 h = f.size * 0.5f;
            Vector3 mn = zoneWorld.min, mx = zoneWorld.max;
            var zl = new Bounds(w2l.MultiplyPoint3x4(mn), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) != 0 ? mx.x : mn.x, (i & 2) != 0 ? mx.y : mn.y, (i & 4) != 0 ? mx.z : mn.z);
                zl.Encapsulate(w2l.MultiplyPoint3x4(c));
            }
            zl.Expand(1f);   // 구역 둘레 0.5m 여유
            var lb = new Bounds(Vector3.zero, f.size);
            lb.Encapsulate(zl);
            bool topMoves = lb.max.y > h.y + 0.01f;
            bool fixFade = false;
            if (topMoves && f.edgeFade > h.y * 0.5f)
            {
                int r = EditorUtility.DisplayDialogComplex("안개 상자 늘리기",
                    "'" + f.name + "' 의 '가장자리 옅어지는 폭' 이 " + f.edgeFade.ToString("0.#") + " m 로 상자 높이(" + f.size.y.ToString("0.#") + " m)에 비해 커서, " +
                    "지금은 상자 전체가 윗면 쪽으로 옅어지고 있습니다.\n\n윗면을 올리면 바닥 쪽 안개도 같이 진해집니다 (맵 전체 느낌이 바뀜).\n\n" +
                    "'가장자리 2m 로' 를 고르면 폭을 2m 로 줄여 윗면 위치와 상관없게 합니다 (이것도 맵 느낌이 조금 바뀜 — 밀도로 다시 맞추세요).",
                    "그냥 늘리기", "취소", "늘리고 가장자리 2m 로");
                if (r == 1) return;
                fixFade = r == 2;
            }
            // 구역은 상자 기준 위치일 수 있으니 상자가 움직여도 구역 월드 위치는 그대로 두게 보정
            Vector3 oldPos = t.position;
            Undo.RecordObjects(new Object[] { f, t }, "안개 상자 늘리기");
            if (fixFade) f.edgeFade = 2f;
            t.position = l2w.MultiplyPoint3x4(lb.center);
            f.size = lb.size;
            Vector3 shift = Quaternion.Inverse(t.rotation) * (t.position - oldPos);
            foreach (var z in f.zones) if (z != null && z.follow == null) z.center -= shift;
            EditorUtility.SetDirty(f);
            Debug.Log("[안개 구역] '" + f.name + "' 상자를 " + h * 2f + " → " + f.size + " 로 늘렸습니다 (Ctrl+Z 로 되돌리기)", f);
        }

        // 씬 뷰: 펼친 구역에 상자 손잡이, 모든 구역에 이름
        readonly UnityEditor.IMGUI.Controls.BoxBoundsHandle box = new UnityEditor.IMGUI.Controls.BoxBoundsHandle();

        void OnSceneGUI()
        {
            var f = (RSFogVolume)target;
            if (f.zones == null) return;
            for (int i = 0; i < f.zones.Count; i++)
            {
                var z = f.zones[i];
                if (z == null) continue;
                var m = z.ToWorld(f.transform);
                Handles.Label(m.MultiplyPoint3x4(new Vector3(0f, z.size.y * 0.5f + 0.3f, 0f)), (i + 1) + ". " + z.name + "  안개 " + z.Summary, EditorStyles.boldLabel);
                if (i != editing) continue;
                using (new Handles.DrawingScope(RSFogVolume.ZoneColor(z), m))
                {
                    box.center = Vector3.zero;
                    box.size = z.size;
                    EditorGUI.BeginChangeCheck();
                    box.DrawHandle();
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(f, "안개 구역 크기");
                        z.center += box.center;      // 구역 축 = 따라가는 오브젝트(또는 상자) 축이라 그대로 더한다
                        z.size = box.size;
                        EditorUtility.SetDirty(f);
                    }
                }
            }
        }
    }

    /// <summary>인스펙터 맨 위 '조절용 보기' 토글 버튼 (켜져 있으면 노랗게)</summary>
    public static class RSDebugToggle
    {
        public static bool Draw(bool current, string label, string tip, out bool next)
        {
            var old = GUI.backgroundColor;
            if (current) GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
            bool clicked = GUILayout.Button(new GUIContent((current ? "● " : "○ ") + label + (current ? " — 켜짐 (끄려면 클릭)" : ""), tip), GUILayout.Height(24f));
            GUI.backgroundColor = old;
            next = clicked ? !current : current;
            EditorGUILayout.Space(2f);
            return clicked;
        }
    }

    [CustomEditor(typeof(RSGodRays))]
    public class RSGodRaysEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            var g = (RSGodRays)target;
            if (RSDebugToggle.Draw(g.debugView, "빛살만 보기 (조절용)",
                    "켜면 빛살이 어디서 나오는지 주황색으로 크게 보여 준다 (세기 · 시간대와 상관없이). 값을 맞춘 뒤 끈다.", out bool on))
            {
                Undo.RecordObject(g, "빛살만 보기");
                g.debugView = on;
                EditorUtility.SetDirty(g);
                SceneView.RepaintAll();
            }
            RSLookInspector.Draw(this);
            var urp = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset;
            if (urp != null && (!urp.supportsCameraOpaqueTexture || !urp.supportsCameraDepthTexture))
                EditorGUILayout.HelpBox("URP 에셋의 Opaque Texture · Depth Texture 가 꺼져 있어 갓레이가 안 보입니다. (젖은 바닥 · 반사 인스펙터의 'URP 설정 켜기')", MessageType.Warning);
            var fog = Object.FindAnyObjectByType<RSFogVolume>();
            EditorGUILayout.HelpBox(fog != null
                ? "안개 속 빛기둥은 '볼류메트릭 안개' 인스펙터의 '갓레이 (안개 속 빛줄기)' 묶음에서 따로 조절합니다."
                : "안개 속 빛기둥도 원하면 볼류메트릭 안개 상자를 놓으세요 (GameObject → RE_AL STEEL → 볼류메트릭 안개 상자).", MessageType.None);
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
