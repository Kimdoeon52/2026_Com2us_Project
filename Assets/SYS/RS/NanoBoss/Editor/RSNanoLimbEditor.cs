// RE:AL STEEL - 나노봇 리그 부위 인스펙터 · 씬 뷰 핸들 · 실루엣
//
// 인스펙터: 모양 → 봇 → 결속 → 느낌(프리셋 · 프레임) → 판정 · 모습 순. 값 옆에 초 단위 · 판 크기를 같이 보여 준다.
// 씬 뷰 (부위를 고르면)
//   · 완성 모양을 반투명 면으로 늘 그린다 (봇이 아직 안 와도 모양이 보인다). 판정 = 빨강, 발광 = 노랑, 유체 = 파랑
//   · 줄기: 관절마다 위치 점(끌어 옮김) + 두께 점(끌어 굵기) / 덩어리: 가로 · 세로 크기 점 / 웅덩이: 너비 · 깊이 · 솟는 범위 점
//   · 줄기 관절 버튼: 끝에 하나 더 · 고른 관절 뒤에 끼우기 · 끝 관절 지우기
// 애니메이션 창 기록 중이면 핸들로 바꾼 값도 키로 잡힌다.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RealSteel.NanoBoss.EditorTools
{
    [CustomEditor(typeof(RSNanoLimb)), CanEditMultipleObjects]
    public class RSNanoLimbEditor : Editor
    {
        static GUIStyle mini;
        static GUIStyle Mini => mini ??= new GUIStyle(EditorStyles.miniLabel) { richText = true, wordWrap = true };

        SerializedProperty P(string n) => serializedObject.FindProperty(n);

        public override bool RequiresConstantRepaint() => RSNanoRigPreview.Current && targets.Length == 1;

        public override void OnInspectorGUI()
        {
            var l = (RSNanoLimb)target;
            var rig = l.GetComponentInParent<RSNanoRig>();
            var sw = rig ? (rig.swarm ? rig.swarm : rig.GetComponentInParent<RSNanoSwarm>()) : l.GetComponentInParent<RSNanoSwarm>();
            serializedObject.Update();

            string help = l.type == LimbType.Chain
                ? "줄기: 이 오브젝트 → 첫째 자식 → 그 첫째 자식 … 이 관절이다. 씬 뷰에서 관절 점을 끌어 휘고, 두께 점을 끌어 굵기를 바꾼다."
                : l.type == LimbType.Blob
                    ? "덩어리: 크기 × 트랜스폼 스케일 타원. 스케일에 키를 잡아 부풀리고 줄인다."
                    : "웅덩이: 바닥에 깔리는 층. '요동 지점' 을 주고 '솟는 높이' 에 키를 잡으면 그 자리로 모여 요동치며 솟는다.";
            EditorGUILayout.HelpBox(help + "\n양 = 앞(뿌리 · 가운데)부터 채운 비율. 결속 1 = 기계 격자, 0 = 흐르는 유체.", MessageType.None);

            // ── 상태 ──
            if (rig && rig.Active && targets.Length == 1)
            {
                Rect r = GUILayoutUtility.GetRect(100, 16, GUILayout.ExpandWidth(true));
                RSNanoRigEditor.FillBar(r, l.assigned, l.Desired, Mathf.Max(1, l.bots), new Color(0.5f, 0.85f, 0.95f));
            }

            // ── 모양 ──
            Section("모양");
            EditorGUILayout.PropertyField(P("type"), new GUIContent("종류"));
            switch (l.type)
            {
                case LimbType.Chain:
                    EditorGUILayout.PropertyField(P("radius"), new GUIContent("두께 (m)", "관절마다 '나노봇 리그 관절' 의 두께 배율이 곱해진다"));
                    EditorGUILayout.PropertyField(P("grow"), new GUIContent("자라기", "뿌리부터 얼마나 뻗었나 (키를 잡는다)"));
                    EditorGUILayout.PropertyField(P("fillToFloor"), new GUIContent("바닥까지 채움", "관절선 아래를 바닥까지 채운다 (파도)"));
                    EditorGUILayout.PropertyField(P("depth"), new GUIContent("앞뒤 두께 배율"));
                    EditorGUILayout.PropertyField(P("smooth"), new GUIContent("곡선으로 잇기"));
                    if (targets.Length == 1) JointList(l);
                    break;
                case LimbType.Blob:
                    EditorGUILayout.PropertyField(P("size"), new GUIContent("반지름 (m)"));
                    EditorGUILayout.PropertyField(P("wobble"), new GUIContent("출렁임"));
                    break;
                case LimbType.Sprite:
                    if (targets.Length == 1) SpriteSection(l, sw);
                    break;
                default:
                    EditorGUILayout.PropertyField(P("extent"), new GUIContent("너비 · 안쪽 깊이 (m)"));
                    EditorGUILayout.PropertyField(P("layer"), new GUIContent("층 두께 (m)"));
                    EditorGUILayout.PropertyField(P("ripple"), new GUIContent("물결 높이 (m)"));
                    EditorGUILayout.PropertyField(P("rippleSpeed"), new GUIContent("물결 속도"));
                    EditorGUILayout.PropertyField(P("bumpAt"), new GUIContent("요동 지점"));
                    EditorGUILayout.PropertyField(P("bump"), new GUIContent("솟는 높이 (m)"));
                    EditorGUILayout.PropertyField(P("bumpRadius"), new GUIContent("솟는 범위 (m)"));
                    EditorGUILayout.PropertyField(P("bumpShake"), new GUIContent("요동 세기"));
                    break;
            }

            // ── 봇 ──
            Section("봇");
            if (l.type == LimbType.Sprite)
                EditorGUILayout.LabelField($"   봇 {l.SlotTotal} — 그림에서 가장 많이 칠한 프레임의 픽셀 수 (픽셀 하나 = 봇 하나)", Mini);
            else
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(P("bots"), new GUIContent("자리 수 (봇)", "양이 1 일 때 쓰는 봇 수 = 격자 자리 수"));
                if (GUILayout.Button(new GUIContent("크기에 맞게", "지금 모양을 격자 간격으로 빈틈 없이 채우는 수"), GUILayout.Width(80)))
                {
                    serializedObject.ApplyModifiedProperties();
                    foreach (var t in targets) { Undo.RecordObject(t, "봇 수"); RSNanoRigEditor.AutoBots((RSNanoLimb)t, sw); }
                    serializedObject.Update();
                }
            }
            if (targets.Length == 1 && l.type != LimbType.Sprite)
            {
                float floor = RSNanoRigPreview.FloorY(rig);
                int est = l.EstimateBots(RSNanoRigEditor.Spacing(sw), floor);
                float ratio = l.bots / (float)Mathf.Max(1, est);
                string state = ratio < 0.7f ? "<color=#ffaa55>성김 — 판 사이가 벌어진다</color>" : ratio > 1.4f ? "<color=#ffaa55>빽빽 — 판이 겹친다</color>" : "<color=#9be89b>알맞음</color>";
                EditorGUILayout.LabelField($"   지금 모양에 알맞은 수 ≈ {est}  ({state})", Mini);
            }
            EditorGUILayout.PropertyField(P("amount"), new GUIContent("양", "앞(뿌리 · 가운데)부터 채운 비율 (키를 잡는다)"));
            EditorGUILayout.PropertyField(P("feedFrom"), new GUIContent("먼저 가져올 부위", "모자란 봇을 이 부위에서 가까운 것부터 가져온다 (웅덩이에서 솟는 가시 등)"));

            // ── 결속 ──
            Section("결속");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("유체", Mini, GUILayout.Width(26));
                EditorGUILayout.PropertyField(P("bind"), GUIContent.none);
                GUILayout.Label("기계", Mini, GUILayout.Width(26));
            }
            EditorGUILayout.LabelField("   1 = 판이 맞물린 기계 격자 · 0 = 이웃과 함께 흐르는 방울. 0 → 1 로 올리면 뿌리부터 '착착' 잠기며 빛난다", Mini);
            EditorGUILayout.PropertyField(P("flow"), new GUIContent("흐름 (m)", "유체일 때 덩어리째 일렁이는 폭"));

            // ── 느낌 ──
            Section("느낌 (프레임 = 1/60초)");
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var p in RSNanoLimb.Presets)
                    if (GUILayout.Button(new GUIContent(p.name, p.tip)))
                        foreach (var t in targets) { Undo.RecordObject(t, "느낌 프리셋"); ((RSNanoLimb)t).ApplyPreset(p); }
            }
            serializedObject.Update();
            FrameSlider(P("response"), "반응", "자리에 거의 도착하는 데 걸리는 프레임. 작을수록 날카롭다");
            EditorGUILayout.PropertyField(P("bounce"), new GUIContent("출렁임", "0 = 딱 멈춤 · 1 = 크게 튕겼다 자리 잡음"));
            FrameSlider(P("tipLag"), "끝 늦음", "끝이 몇 프레임 늦게 따라오나 (팔로스루 · 잔상). 빠르게 휘두르면 궤적이 남는다");
            FrameSlider(P("gather"), "모이는 시간", "멀리서 이 부위로 흘러들어 올 때의 반응");
            EditorGUILayout.PropertyField(P("gatherOrder"), new GUIContent("모이는 순서"));

            // ── 판정 · 모습 ──
            Section("판정 · 모습");
            EditorGUILayout.PropertyField(P("danger"), new GUIContent("판정", "켜져 있는 동안 플레이어를 해친다 (키를 잡는다)"));
            EditorGUILayout.PropertyField(P("glow"), new GUIContent("발광", "예고에 쓴다"));
            EditorGUILayout.PropertyField(P("tint"), new GUIContent("색"));

            serializedObject.ApplyModifiedProperties();

            // ── 도구 ──
            if (targets.Length == 1)
            {
                Section("도구");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("좌우 대칭 복제", "부모 기준 x 를 뒤집은 복제를 만든다"))) Mirror(l);
                    if (GUILayout.Button("동작 창")) RSNanoActionWindow.Open(rig);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 그림 (스프라이트 시트)
        // ─────────────────────────────────────────────────────────────
        int previewFrame;

        void SpriteSection(RSNanoLimb l, RSNanoSwarm sw)
        {
            EditorGUILayout.HelpBox("픽셀아트 스프라이트 시트를 넣으면 프레임마다 어두운 픽셀(검정)을 봇 자리로 굽는다. 흰색 · 투명은 무시.\n" +
                                    "픽셀 하나 = 봇 하나. 프레임이 바뀌면 남은 픽셀의 봇은 그대로, 새 픽셀은 가까운 봇이 옮겨 와 사이를 메운다.", MessageType.None);
            var cur = l.picture ? l.picture.sheet : null;
            var tex = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("스프라이트 시트", "PNG. 가로로 이어 붙인 시트면 칸 수가 자동으로 잡힌다"), cur, typeof(Texture2D), false);
            if (tex && tex != cur)
            {
                var shape = RSNanoSpriteBaker.CreateOrFind(tex);
                Undo.RecordObject(l, "그림");
                l.picture = shape;
                if (sw) l.pixelSize = sw.spacing;
                RSNanoRigPreview.MarkDirty();
                serializedObject.Update();
            }
            EditorGUILayout.PropertyField(P("picture"), new GUIContent("그림 모양 (구운 것)"));
            var pic = l.picture;
            if (!pic) return;

            var so = new SerializedObject(pic);
            so.Update();
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(so.FindProperty("columns"), new GUIContent("가로 칸"));
                EditorGUILayout.PropertyField(so.FindProperty("rows"), new GUIContent("세로 칸"));
            }
            EditorGUILayout.PropertyField(so.FindProperty("frameLimit"), new GUIContent("프레임 수 (0 = 전부)"));
            EditorGUILayout.PropertyField(so.FindProperty("darkness"), new GUIContent("어두운 기준", "이보다 어두운 픽셀을 따라간다"));
            EditorGUILayout.PropertyField(so.FindProperty("invert"), new GUIContent("반대로 (밝은 픽셀)"));
            EditorGUILayout.PropertyField(so.FindProperty("alphaCut"), new GUIContent("투명 기준"));
            EditorGUILayout.PropertyField(so.FindProperty("pivot"), new GUIContent("기준점"));
            if (EditorGUI.EndChangeCheck()) so.ApplyModifiedProperties();
            if (RSNanoSpriteBaker.NeedsBake(pic)) { RSNanoSpriteBaker.Bake(pic); RSNanoRigPreview.MarkDirty(); }

            if (!pic.Ready)
            {
                EditorGUILayout.HelpBox("칠한 픽셀을 못 찾았다 — 칸 수 · 어두운 기준 · '반대로' 를 확인", MessageType.Warning);
                return;
            }
            float w = (pic.maxX - pic.minX + 1) * l.pixelSize, h = (pic.maxY - pic.minY + 1) * l.pixelSize;
            EditorGUILayout.LabelField($"   프레임 {pic.frameCount}개 · 봇 {pic.botCount}개 · 크기 {w:0.0} × {h:0.0} m", Mini);

            previewFrame = Mathf.Clamp(EditorGUILayout.IntSlider("미리보기 프레임", previewFrame, 0, pic.frameCount - 1), 0, pic.frameCount - 1);
            var prev = RSNanoSpriteBaker.Preview(pic, previewFrame);
            if (prev)
            {
                float k = Mathf.Min(260f / prev.width, 120f / prev.height);
                Rect r = GUILayoutUtility.GetRect(prev.width * k, prev.height * k, GUILayout.ExpandWidth(false));
                r.x += 14;
                GUI.DrawTexture(r, prev, ScaleMode.ScaleToFit);
                EditorGUILayout.LabelField($"   이 프레임 칠한 픽셀 {pic.filled[previewFrame]} — 봇이 남으면 같은 픽셀에 겹쳐 선다", Mini);
            }

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(P("pixelSize"), new GUIContent("픽셀 크기 (m)", "그림 픽셀 하나 = 봇 하나의 간격"));
                if (sw && GUILayout.Button(new GUIContent("격자 간격에", "군집 격자 간격과 같게 → 다른 부위와 판 크기가 같다"), GUILayout.Width(70)))
                { Undo.RecordObject(l, "픽셀 크기"); l.pixelSize = sw.spacing; serializedObject.Update(); }
            }
            EditorGUILayout.PropertyField(P("fps"), new GUIContent("초당 프레임", "0 보다 크면 패턴 시작부터 저절로 넘긴다. 0 이면 '프레임' 키로 넘긴다"));
            using (new EditorGUI.DisabledScope(l.fps > 0f))
                EditorGUILayout.PropertyField(P("frame"), new GUIContent("프레임", "초당 프레임이 0 일 때 쓴다 (키를 잡는다 — 계단 키)"));
            EditorGUILayout.PropertyField(P("loop"), new GUIContent("반복"));
        }

        static void Section(string title)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        static void FrameSlider(SerializedProperty p, string label, string tip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(p, new GUIContent(label, tip));
                GUILayout.Label($"{p.floatValue / 60f:0.00}초", Mini, GUILayout.Width(44));
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 관절
        // ─────────────────────────────────────────────────────────────
        void JointList(RSNanoLimb l)
        {
            l.RefreshJoints();
            var J = l.Joints;
            EditorGUILayout.LabelField($"관절 {J.Count}개 — 두께 배율", Mini);
            bool legacy = false;
            for (int i = 0; i < J.Count; i++)
            {
                var jc = J[i] ? J[i].GetComponent<RSNanoJoint>() : null;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(J[i] ? J[i].name : "-", EditorStyles.linkLabel, GUILayout.Width(80)) && J[i]) Selection.activeTransform = J[i];
                    if (jc)
                    {
                        EditorGUI.BeginChangeCheck();
                        float v = EditorGUILayout.Slider(jc.thickness, 0f, 3f);
                        if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(jc, "관절 두께"); jc.thickness = v; }
                    }
                    else { legacy = true; GUILayout.Label("(두께 곡선)", Mini); }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 끝에 관절")) AddTipJoint(l);
                using (new EditorGUI.DisabledScope(J.Count < 3))
                    if (GUILayout.Button("끝 관절 지우기")) RemoveTipJoint(l);
            }
            if (legacy && GUILayout.Button("두께 곡선 → 관절별 두께로 바꾸기")) ConvertThickness(l);
        }

        public static void ConvertThickness(RSNanoLimb l)
        {
            l.RefreshJoints();
            var J = l.Joints;
            for (int i = 0; i < J.Count; i++)
            {
                if (!J[i] || J[i].GetComponent<RSNanoJoint>()) continue;
                var c = Undo.AddComponent<RSNanoJoint>(J[i].gameObject);
                c.thickness = Mathf.Max(0f, l.thickness.Evaluate(J.Count > 1 ? i / (J.Count - 1f) : 0f));
            }
            l.RefreshJoints();
        }

        public static void AddTipJoint(RSNanoLimb l)
        {
            l.RefreshJoints();
            var J = l.Joints;
            var tip = J[J.Count - 1];
            Vector3 step = J.Count > 1 ? tip.localPosition : new Vector3(1f, 0f, 0f);
            if (step.sqrMagnitude < 1e-4f) step = new Vector3(1f, 0f, 0f);
            var go = new GameObject("관절" + J.Count);
            Undo.RegisterCreatedObjectUndo(go, "관절 추가");
            go.transform.SetParent(tip, false);
            go.transform.localPosition = step;
            var tc = tip.GetComponent<RSNanoJoint>();
            var c = go.AddComponent<RSNanoJoint>();
            c.thickness = tc ? tc.thickness * 0.6f : 0.3f;
            l.RefreshJoints();
            Selection.activeGameObject = go;
        }

        /// <summary>관절 j 와 그 다음 관절 사이에 하나 끼운다</summary>
        public static void InsertJoint(RSNanoLimb l, int j)
        {
            l.RefreshJoints();
            var J = l.Joints;
            if (j < 0 || j >= J.Count - 1) { AddTipJoint(l); return; }
            var a = J[j];
            var b = J[j + 1];
            var go = new GameObject("관절");
            Undo.RegisterCreatedObjectUndo(go, "관절 끼우기");
            go.transform.SetParent(a, false);
            go.transform.position = Vector3.Lerp(a.position, b.position, 0.5f);
            go.transform.SetSiblingIndex(b.GetSiblingIndex());
            Undo.SetTransformParent(b, go.transform, "관절 끼우기");
            var ac = a.GetComponent<RSNanoJoint>();
            var bc = b.GetComponent<RSNanoJoint>();
            var c = go.AddComponent<RSNanoJoint>();
            c.thickness = ((ac ? ac.thickness : 1f) + (bc ? bc.thickness : 1f)) * 0.5f;
            l.RefreshJoints();
            Selection.activeGameObject = go;
        }

        public static void RemoveTipJoint(RSNanoLimb l)
        {
            l.RefreshJoints();
            var J = l.Joints;
            if (J.Count < 3) return;
            var tip = J[J.Count - 1];
            Selection.activeGameObject = J[J.Count - 2].gameObject;
            Undo.DestroyObjectImmediate(tip.gameObject);
            l.RefreshJoints();
        }

        /// <summary>부모 기준 x 를 뒤집은 복제</summary>
        public static void Mirror(RSNanoLimb l)
        {
            var copy = Object.Instantiate(l.gameObject, l.transform.parent);
            copy.name = l.name + " (대칭)";
            Undo.RegisterCreatedObjectUndo(copy, "좌우 대칭 복제");
            MirrorRec(copy.transform);
            Selection.activeGameObject = copy;
        }

        static void MirrorRec(Transform t)
        {
            var p = t.localPosition;
            t.localPosition = new Vector3(-p.x, p.y, p.z);
            var e = t.localEulerAngles;
            t.localEulerAngles = new Vector3(e.x, -e.y, -e.z);
            for (int i = 0; i < t.childCount; i++) MirrorRec(t.GetChild(i));
        }

        // ─────────────────────────────────────────────────────────────
        // 씬 뷰 핸들
        // ─────────────────────────────────────────────────────────────
        void OnSceneGUI()
        {
            var l = (RSNanoLimb)target;
            if (!l || Selection.objects.Length > 1) return;   // OnSceneGUI 에서는 targets 를 쓰면 안 된다
            var rig = l.GetComponentInParent<RSNanoRig>();
            float floor = RSNanoRigPreview.FloorY(rig);
            l.RefreshJoints();
            l.CachePose();

            switch (l.type)
            {
                case LimbType.Chain: ChainHandles(l); break;
                case LimbType.Blob: BlobHandles(l); break;
                case LimbType.Sprite: break;
                default: PuddleHandles(l, floor); break;
            }
            SceneButtons(l);
        }

        static float Size(Vector3 p) => HandleUtility.GetHandleSize(p) * 0.07f;

        static void ChainHandles(RSNanoLimb l)
        {
            var J = l.Joints;
            for (int i = 0; i < J.Count; i++)
            {
                var jt = J[i];
                if (!jt) continue;
                Vector3 p = jt.position;

                // 위치 점
                Handles.color = new Color(1f, 1f, 1f, 0.9f);
                EditorGUI.BeginChangeCheck();
                Vector3 np = Handles.FreeMoveHandle(p, Size(p), Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(jt, "관절 옮기기");
                    jt.position = new Vector3(np.x, np.y, p.z);
                }

                // 두께 점 — 관절선에 수직
                float u = J.Count > 1 ? i / (J.Count - 1f) : 0f;
                l.Sample(u, out Vector3 sp, out float r);
                l.Sample(Mathf.Clamp01(u + (u < 1f ? 0.02f : -0.02f)), out Vector3 sp2, out _);
                Vector3 tan = (u < 1f ? sp2 - sp : sp - sp2);
                if (tan.sqrMagnitude < 1e-8f) tan = Vector3.right;
                Vector3 nrm = Vector3.Cross(tan.normalized, Vector3.forward).normalized;
                Vector3 hp = p + nrm * Mathf.Max(0.05f, r);
                Handles.color = new Color(1f, 0.8f, 0.3f, 0.95f);
                Handles.DrawDottedLine(p, hp, 3f);
                EditorGUI.BeginChangeCheck();
                Vector3 nh = Handles.FreeMoveHandle(hp, Size(hp) * 0.8f, Vector3.zero, Handles.CubeHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    float dist = Vector3.Dot(nh - p, nrm);
                    var jc = jt.GetComponent<RSNanoJoint>();
                    if (!jc) { RSNanoLimbEditor.ConvertThickness(l); jc = jt.GetComponent<RSNanoJoint>(); }
                    if (jc)
                    {
                        Undo.RecordObject(jc, "관절 두께");
                        jc.thickness = Mathf.Max(0f, dist / Mathf.Max(1e-4f, l.radius * l.CurScale));
                    }
                }
            }
        }

        static void BlobHandles(RSNanoLimb l)
        {
            var t = l.transform;
            Handles.color = new Color(1f, 0.8f, 0.3f, 0.95f);
            for (int axis = 0; axis < 2; axis++)
            {
                Vector3 local = axis == 0 ? new Vector3(l.size.x, 0f, 0f) : new Vector3(0f, l.size.y, 0f);
                Vector3 wp = t.TransformPoint(local);
                EditorGUI.BeginChangeCheck();
                Vector3 nw = Handles.FreeMoveHandle(wp, Size(wp), Vector3.zero, Handles.CubeHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 nl = t.InverseTransformPoint(nw);
                    Undo.RecordObject(l, "덩어리 크기");
                    if (axis == 0) l.size.x = Mathf.Max(0.05f, Mathf.Abs(nl.x));
                    else l.size.y = Mathf.Max(0.05f, Mathf.Abs(nl.y));
                }
            }
        }

        static void PuddleHandles(RSNanoLimb l, float floor)
        {
            Vector3 c = l.transform.position;
            float y = floor + l.layer + 0.08f;
            Handles.color = new Color(1f, 0.8f, 0.3f, 0.95f);
            Vector3 hx = new Vector3(c.x + l.extent.x * 0.5f, y, c.z);
            EditorGUI.BeginChangeCheck();
            Vector3 nx = Handles.FreeMoveHandle(hx, Size(hx), Vector3.zero, Handles.CubeHandleCap);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(l, "웅덩이 너비"); l.extent.x = Mathf.Max(0.2f, Mathf.Abs(nx.x - c.x) * 2f); }
            Vector3 hz = new Vector3(c.x, y, c.z + l.extent.y * 0.5f);
            EditorGUI.BeginChangeCheck();
            Vector3 nz = Handles.Slider(hz, Vector3.forward, Size(hz), Handles.CubeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(l, "웅덩이 깊이"); l.extent.y = Mathf.Max(0.1f, Mathf.Abs(nz.z - c.z) * 2f); }
            if (l.bumpAt)
            {
                Vector3 b = l.bumpAt.position;
                b.y = y;
                Handles.DrawWireDisc(b, Vector3.up, l.bumpRadius);
                Vector3 hr = b + Vector3.right * l.bumpRadius;
                EditorGUI.BeginChangeCheck();
                Vector3 nr = Handles.FreeMoveHandle(hr, Size(hr), Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(l, "솟는 범위"); l.bumpRadius = Mathf.Max(0.1f, Mathf.Abs(nr.x - b.x)); }
            }
        }

        static void SceneButtons(RSNanoLimb l)
        {
            Handles.BeginGUI();
            var r = new Rect(10, 10, 300, l.type == LimbType.Chain ? 70 : 46);
            GUILayout.BeginArea(r, EditorStyles.helpBox);
            GUILayout.Label($"<b>{l.name}</b>  봇 {l.assigned}/{l.Desired}  결속 {l.bind:0.00}" + (l.danger ? "  <color=#ff7766>판정</color>" : ""), Mini);
            if (l.type == LimbType.Chain)
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ 끝 관절", EditorStyles.miniButton)) AddTipJoint(l);
                    int sel = -1;
                    var J = l.Joints;
                    for (int i = 0; i < J.Count; i++) if (J[i] && Selection.activeTransform == J[i]) sel = i;
                    if (GUILayout.Button(sel >= 0 ? "고른 관절 뒤에 끼우기" : "가운데 끼우기", EditorStyles.miniButton)) InsertJoint(l, sel >= 0 ? sel : J.Count / 2 - 1);
                    using (new EditorGUI.DisabledScope(J.Count < 3))
                        if (GUILayout.Button("끝 관절 지우기", EditorStyles.miniButton)) RemoveTipJoint(l);
                }
            }
            using (new GUILayout.HorizontalScope())
            {
                foreach (var p in RSNanoLimb.Presets)
                    if (GUILayout.Button(new GUIContent(p.name, p.tip), EditorStyles.miniButton)) { Undo.RecordObject(l, "느낌 프리셋"); l.ApplyPreset(p); }
            }
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        // ─────────────────────────────────────────────────────────────
        // 실루엣 (리그 안을 고르고 있을 때)
        // ─────────────────────────────────────────────────────────────
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.InSelectionHierarchy)]
        static void DrawLimb(RSNanoLimb l, GizmoType type)
        {
            var rig = l.GetComponentInParent<RSNanoRig>();
            var sel = Selection.activeTransform;
            if (!rig || !sel || !(sel == rig.transform || sel.IsChildOf(rig.transform))) return;
            bool selected = sel == l.transform || (sel.IsChildOf(l.transform) && !sel.GetComponent<RSNanoLimb>());
            Color c = l.danger ? new Color(1f, 0.35f, 0.3f) : l.glow > 0.05f ? new Color(1f, 0.85f, 0.3f) : l.bind < 0.5f ? new Color(0.45f, 0.65f, 1f) : new Color(0.45f, 0.9f, 1f);
            float a = l.amount < 0.01f ? 0.35f : 1f;
            RSNanoLimbShape.Fill(l, RSNanoRigPreview.FloorY(rig), new Color(c.r, c.g, c.b, (selected ? 0.16f : 0.06f) * a), new Color(c.r, c.g, c.b, (selected ? 0.9f : 0.45f) * a));
            if (selected) return;
            if (l.type == LimbType.Chain)
            {
                Handles.color = new Color(c.r, c.g, c.b, 0.6f * a);
                foreach (var j in l.Joints) if (j) Handles.DotHandleCap(0, j.position, Quaternion.identity, HandleUtility.GetHandleSize(j.position) * 0.03f, EventType.Repaint);
            }
        }
    }
}
