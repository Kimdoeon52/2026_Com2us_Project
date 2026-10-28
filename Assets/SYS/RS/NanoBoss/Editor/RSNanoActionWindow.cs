// RE:AL STEEL - 나노봇 동작 창 (창 → RE_AL STEEL → 나노봇 동작)
//
// 애니메이션 창 옆에 띄워 두고 쓴다. 픽셀 애니메이션처럼 '키 포즈 → 타이밍' 으로 만든다.
//   위    지금 프레임 · 재생(1 · ½ · ¼ 배속) · 한 프레임씩 · 어니언 스킨
//   띠    부위마다 한 줄: 회색 = 보임(양) · 파랑 = 유체 · 노랑 = 발광(예고) · 빨강 = 판정 / 리그 줄: 하늘 = 플레이어 고정 · 보라 = 흔들림
//         발사기 줄: 주황 = 발사. 띠를 누르거나 끌면 그 시각으로 간다
//   동작      지금 프레임에 버튼 한 번으로 키 (나타나기 · 사라지기 · 자라기 · 판정 · 빛 펄스 · 풀림 · 결속 · 흔들림 · 고정 · 발사)
//   행동 블록  돌진 · 휘두르기 · 내려찍기 · 모였다 흩어지기 — 프레임 몇 개만 정하면 키 묶음을 만든다
//   포즈      리그 모양을 이름 붙여 저장 → 지금 프레임에 키로 (달라진 값만)
//   점검      예고가 짧은 판정, 봇 모자람, 원점 키 … 고치기 버튼
using System.Collections.Generic;
using RealSteel.Common.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RealSteel.NanoBoss.EditorTools
{
    public class RSNanoActionWindow : EditorWindow
    {
        [MenuItem("Window/RE_AL STEEL/나노봇 동작")]
        static void MenuOpen() => Open(null);

        public static void Open(RSNanoRig rig)
        {
            var w = GetWindow<RSNanoActionWindow>("나노봇 동작");
            if (rig) w.rig = rig;
            w.minSize = new Vector2(420, 360);
            w.Show();
        }

        RSNanoRig rig;
        int tab;
        Vector2 scroll;
        [SerializeField] float lengthFrames = 12f;

        // 행동 블록
        [SerializeField] int blockType;
        Transform blockTarget;
        RSNanoLimb blockLimb;
        [SerializeField] float dashDist = 6f, dashPrep = 18f, dashFrames = 8f, dashHold = 12f, dashBack = 20f;
        [SerializeField] float swingFrom = 150f, swingTo = -60f, swingRaise = 20f, swingHold = 10f, swingFrames = 8f, swingBack = 20f;
        [SerializeField] float slamHeight = 4f, slamRaise = 16f, slamHold = 8f, slamDrop = 6f, slamScatter = 18f;
        [SerializeField] float gatherHold = 30f, gatherOut = 12f;

        // 포즈
        string poseName = "포즈";

        static readonly string[] Tabs = { "동작", "행동 블록", "포즈", "점검" };
        static readonly string[] Blocks = { "돌진", "휘두르기", "내려찍기", "모였다 흩어지기" };

        static GUIStyle rich, bold;
        static GUIStyle Rich => rich ??= new GUIStyle(EditorStyles.miniLabel) { richText = true, wordWrap = true };
        static GUIStyle Bold => bold ??= new GUIStyle(EditorStyles.boldLabel) { richText = true };

        double lastRepaint;

        void OnSelectionChange()
        {
            var go = Selection.activeGameObject;
            var r = go ? go.GetComponentInParent<RSNanoRig>() : null;
            if (r) rig = r;
            var t = Selection.activeTransform;
            if (rig && t && t.IsChildOf(rig.transform) && t != rig.transform) blockTarget = t;
            var l = t ? t.GetComponent<RSNanoLimb>() : null;
            if (l) blockLimb = l;
            Repaint();
        }

        void Update()
        {
            if (EditorApplication.timeSinceStartup - lastRepaint < 1.0 / 30.0) return;
            if (RSNanoRigPreview.Current == rig && rig) { lastRepaint = EditorApplication.timeSinceStartup; Repaint(); }
        }

        void OnGUI()
        {
            if (!rig)
            {
                var go = Selection.activeGameObject;
                rig = go ? go.GetComponentInParent<RSNanoRig>() : null;
            }
            if (!rig)
            {
                EditorGUILayout.HelpBox("나노봇 리그(또는 그 부위)를 선택하세요.\nGameObject → RE_AL STEEL → 나노봇 리그 에서 빈 리그 · 예제를 만들 수 있다.", MessageType.Info);
                return;
            }
            var clip = RSNanoKeys.CurrentClip(rig);
            Header(clip);
            if (clip) Timeline(clip);
            EditorGUILayout.Space(2);
            tab = GUILayout.Toolbar(tab, Tabs);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: ActionsTab(clip); break;
                case 1: BlocksTab(clip); break;
                case 2: PosesTab(clip); break;
                default: CheckTab(clip); break;
            }
            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────────────────────
        // 위: 리그 · 클립 · 재생 · 어니언
        // ─────────────────────────────────────────────────────────────
        void Header(AnimationClip clip)
        {
            var aw = RSNanoKeys.AnimWindow();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("<b>" + rig.name + "</b>", Bold, GUILayout.Width(140));
                var clips = RSNanoKeys.ClipsOf(rig);
                if (clips.Length > 0)
                {
                    int cur = System.Array.IndexOf(clips, clip);
                    var names = new string[clips.Length];
                    for (int i = 0; i < clips.Length; i++) names[i] = clips[i] ? clips[i].name : "-";
                    int pick = EditorGUILayout.Popup(Mathf.Max(0, cur), names);
                    if (pick != cur && aw) { aw.animationClip = clips[pick]; RSNanoRigPreview.MarkDirty(); }
                }
                if (GUILayout.Button("새 클립", EditorStyles.miniButton, GUILayout.Width(52)))
                {
                    var c = RSNanoKeys.NewClip(rig, "새 공격", 3f);
                    RSNanoKeys.OpenAnimWindow(rig);
                    var w = RSNanoKeys.AnimWindow();
                    if (w) w.animationClip = c;
                }
            }

            if (!aw || !aw.previewing || !clip)
            {
                EditorGUILayout.HelpBox("애니메이션 창에서 미리보기를 켜면 어느 프레임이든 봇 모습이 고정되고, 여기서 재생 · 키 넣기를 할 수 있다.", MessageType.None);
                if (GUILayout.Button("애니메이션 창 열고 미리보기 켜기"))
                {
                    RSNanoRigEditor.EnsureAnimator(rig, null);
                    RSNanoKeys.OpenAnimWindow(rig);
                    var w = RSNanoKeys.AnimWindow();
                    if (w)
                    {
                        if (clip) w.animationClip = clip;
                        if (w.canPreview) w.previewing = true;
                    }
                }
                return;
            }

            float t = aw.time;
            int f = Mathf.RoundToInt(t * RSNanoKeys.Fps), total = Mathf.RoundToInt(clip.length * RSNanoKeys.Fps);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"<b>프레임 {f}</b> / {total}   ({t:0.00}초)", Bold, GUILayout.Width(170));
                if (GUILayout.Button("⏮", EditorStyles.miniButtonLeft, GUILayout.Width(26))) { RSNanoRigPreview.PlaybackSpeed = 0f; RSNanoKeys.SetNow(0f); }
                if (GUILayout.Button("◀", EditorStyles.miniButtonMid, GUILayout.Width(24))) { RSNanoRigPreview.PlaybackSpeed = 0f; RSNanoKeys.SetNow((f - 1) / RSNanoKeys.Fps); }
                Play("▶ 1", 1f); Play("▶ ½", 0.5f); Play("▶ ¼", 0.25f);
                if (GUILayout.Button("■", EditorStyles.miniButtonMid, GUILayout.Width(24))) RSNanoRigPreview.PlaybackSpeed = 0f;
                if (GUILayout.Button("▶", EditorStyles.miniButtonMid, GUILayout.Width(24))) { RSNanoRigPreview.PlaybackSpeed = 0f; RSNanoKeys.SetNow((f + 1) / RSNanoKeys.Fps); }
                if (GUILayout.Button("⏭", EditorStyles.miniButtonRight, GUILayout.Width(26))) { RSNanoRigPreview.PlaybackSpeed = 0f; RSNanoKeys.SetNow(clip.length); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("어니언", GUILayout.Width(40));
                RSNanoRigPreview.OnionCount = EditorGUILayout.IntSlider(RSNanoRigPreview.OnionCount, 0, 3, GUILayout.Width(130));
                using (new EditorGUI.DisabledScope(RSNanoRigPreview.OnionKeys))
                {
                    GUILayout.Label("간격", GUILayout.Width(28));
                    RSNanoRigPreview.OnionStep = EditorGUILayout.IntSlider(RSNanoRigPreview.OnionStep, 1, 15, GUILayout.Width(120));
                }
                RSNanoRigPreview.OnionKeys = GUILayout.Toggle(RSNanoRigPreview.OnionKeys, "키 포즈만", EditorStyles.miniButton, GUILayout.Width(62));
                bool loop = GUILayout.Toggle(RSNanoRigPreview.Loop, "반복", EditorStyles.miniButton, GUILayout.Width(36));
                if (loop != RSNanoRigPreview.Loop) RSNanoRigPreview.Loop = loop;
            }
            if (GUI.changed) RSNanoRigPreview.RefreshOnion();
        }

        void Play(string label, float speed)
        {
            bool on = Mathf.Approximately(RSNanoRigPreview.PlaybackSpeed, speed);
            if (GUILayout.Toggle(on, label, EditorStyles.miniButtonMid, GUILayout.Width(34)) != on)
                RSNanoRigPreview.PlaybackSpeed = on ? 0f : speed;
        }

        // ─────────────────────────────────────────────────────────────
        // 띠 (부위마다 한 줄)
        // ─────────────────────────────────────────────────────────────
        class Row
        {
            public string name;
            public Object target;
            public List<(Vector2 r, Color c, float h)> bars = new List<(Vector2, Color, float)>();
            public List<float> keys = new List<float>();
            public List<float> ticks = new List<float>();
        }

        readonly List<Row> rows = new List<Row>();
        AnimationClip rowsClip;
        int rowsDirty = -1, rowsLimbs = -1;

        static readonly Color CAmount = new Color(0.55f, 0.6f, 0.7f, 0.55f);
        static readonly Color CFluid = new Color(0.35f, 0.6f, 1f, 0.75f);
        static readonly Color CGlow = new Color(1f, 0.85f, 0.25f, 0.9f);
        static readonly Color CDanger = new Color(1f, 0.3f, 0.25f, 0.95f);
        static readonly Color CLock = new Color(0.4f, 0.9f, 1f, 0.8f);
        static readonly Color CShake = new Color(0.75f, 0.45f, 1f, 0.9f);
        static readonly Color CFire = new Color(1f, 0.6f, 0.2f, 1f);

        void BuildRows(AnimationClip clip)
        {
            int dc = EditorUtility.GetDirtyCount(clip);
            var limbs = rig.GetComponentsInChildren<RSNanoLimb>(false);
            var emitters = rig.GetComponentsInChildren<RSNanoEmitter>(false);
            if (rowsClip == clip && rowsDirty == dc && rowsLimbs == limbs.Length + emitters.Length * 100) return;
            rowsClip = clip; rowsDirty = dc; rowsLimbs = limbs.Length + emitters.Length * 100;
            rows.Clear();
            float len = clip.length;
            var keyTimes = new Dictionary<string, List<float>>();
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                var c = AnimationUtility.GetEditorCurve(clip, b);
                if (c == null) continue;
                if (!keyTimes.TryGetValue(b.path, out var list)) keyTimes[b.path] = list = new List<float>();
                foreach (var k in c.keys) list.Add(k.time);
            }

            foreach (var l in limbs)
            {
                var row = new Row { name = l.name, target = l };
                var am = RSNanoKeys.Curve(clip, rig.transform, l, "amount");
                foreach (var r in am != null ? RSNanoKeys.Ranges(am, len, 0.01f) : (l.amount > 0.01f ? new List<Vector2> { new Vector2(0, len) } : new List<Vector2>()))
                    row.bars.Add((r, CAmount, 1f));
                var bi = RSNanoKeys.Curve(clip, rig.transform, l, "bind");
                foreach (var r in bi != null ? RSNanoKeys.Ranges(bi, len, 0.5f, false) : (l.bind < 0.5f ? new List<Vector2> { new Vector2(0, len) } : new List<Vector2>()))
                    row.bars.Add((r, CFluid, 0.6f));
                var gl = RSNanoKeys.Curve(clip, rig.transform, l, "glow");
                if (gl != null) foreach (var r in RSNanoKeys.Ranges(gl, len, 0.05f)) row.bars.Add((r, CGlow, 0.45f));
                var dg = RSNanoKeys.Curve(clip, rig.transform, l, "danger");
                foreach (var r in dg != null ? RSNanoKeys.Ranges(dg, len, 0.5f) : (l.danger ? new List<Vector2> { new Vector2(0, len) } : new List<Vector2>()))
                    row.bars.Add((r, CDanger, 0.35f));
                // 이 부위와 그 관절의 키
                foreach (var kv in keyTimes)
                {
                    var tr = string.IsNullOrEmpty(kv.Key) ? rig.transform : rig.transform.Find(kv.Key);
                    if (!tr) continue;
                    var owner = tr.GetComponentInParent<RSNanoLimb>();
                    if (owner == l) row.keys.AddRange(kv.Value);
                }
                rows.Add(row);
            }
            {
                var row = new Row { name = "리그", target = rig };
                var lk = RSNanoKeys.Curve(clip, rig.transform, rig, "lockPlayer");
                if (lk != null) foreach (var r in RSNanoKeys.Ranges(lk, len, 0.5f)) row.bars.Add((r, CLock, 0.6f));
                var sh = RSNanoKeys.Curve(clip, rig.transform, rig, "shake");
                if (sh != null) foreach (var r in RSNanoKeys.Ranges(sh, len, 0.02f)) row.bars.Add((r, CShake, 0.35f));
                rows.Add(row);
            }
            foreach (var e in emitters)
            {
                var row = new Row { name = e.name, target = e };
                var fc = RSNanoKeys.Curve(clip, rig.transform, e, "fire");
                if (fc != null)
                {
                    bool prev = fc.Evaluate(0f) > 0.5f;
                    for (int f = 1; f <= Mathf.CeilToInt(len * RSNanoKeys.Fps); f++)
                    {
                        bool on = fc.Evaluate(f / RSNanoKeys.Fps) > 0.5f;
                        if (on && !prev) row.ticks.Add(f / RSNanoKeys.Fps);
                        prev = on;
                    }
                }
                rows.Add(row);
            }
        }

        void Timeline(AnimationClip clip)
        {
            BuildRows(clip);
            const float rowH = 15f, labelW = 96f, rulerH = 14f;
            Rect area = GUILayoutUtility.GetRect(100, rulerH + rows.Count * rowH + 4, GUILayout.ExpandWidth(true));
            Rect band = new Rect(area.x + labelW, area.y, area.width - labelW - 6, area.height);
            float len = Mathf.Max(0.01f, clip.length);
            float X(float t) => band.x + band.width * Mathf.Clamp01(t / len);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(band, new Color(0f, 0f, 0f, 0.22f));
                int frames = Mathf.CeilToInt(len * RSNanoKeys.Fps);
                for (int f = 0; f <= frames; f += 6)
                {
                    float x = X(f / RSNanoKeys.Fps);
                    bool big = f % 30 == 0;
                    EditorGUI.DrawRect(new Rect(x, area.y, 1, big ? rulerH : rulerH * 0.45f), new Color(1f, 1f, 1f, big ? 0.35f : 0.15f));
                    if (big) GUI.Label(new Rect(x + 2, area.y - 2, 40, 14), f.ToString(), EditorStyles.miniLabel);
                }
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    float y = area.y + rulerH + i * rowH;
                    GUI.Label(new Rect(area.x, y, labelW - 4, rowH), row.name, EditorStyles.miniLabel);
                    EditorGUI.DrawRect(new Rect(band.x, y + rowH - 1, band.width, 1), new Color(1f, 1f, 1f, 0.06f));
                    foreach (var (r, c, h) in row.bars)
                    {
                        float hh = (rowH - 3) * h;
                        EditorGUI.DrawRect(new Rect(X(r.x), y + (rowH - 2 - hh) * 0.5f + 1, Mathf.Max(1f, X(r.y) - X(r.x)), hh), c);
                    }
                    foreach (var k in row.keys) EditorGUI.DrawRect(new Rect(X(k) - 1, y + rowH - 4, 3, 3), new Color(1f, 1f, 1f, 0.7f));
                    foreach (var k in row.ticks) EditorGUI.DrawRect(new Rect(X(k) - 1, y + 1, 3, rowH - 3), CFire);
                }
                var aw = RSNanoKeys.AnimWindow();
                if (aw) EditorGUI.DrawRect(new Rect(X(aw.time), area.y, 2, area.height), Color.white);
            }

            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && band.Contains(e.mousePosition))
            {
                float t = (e.mousePosition.x - band.x) / band.width * len;
                RSNanoRigPreview.PlaybackSpeed = 0f;
                RSNanoKeys.SetNow(Mathf.Round(t * RSNanoKeys.Fps) / RSNanoKeys.Fps);
                int ri = Mathf.FloorToInt((e.mousePosition.y - area.y - rulerH) / rowH);
                if (e.type == EventType.MouseDown && ri >= 0 && ri < rows.Count && rows[ri].target is Component comp) Selection.activeGameObject = comp.gameObject;
                e.Use();
                Repaint();
            }
            GUILayout.Label("<color=#9aa4b8>회색 보임 · </color><color=#5c9aff>파랑 유체</color> · <color=#ffd84a>노랑 발광</color> · <color=#ff5040>빨강 판정</color> · <color=#66e6ff>하늘 고정</color> · <color=#bf73ff>보라 흔들림</color> · <color=#ff9933>주황 발사</color> · 흰 점 = 키", Rich);
        }

        // ─────────────────────────────────────────────────────────────
        // 동작
        // ─────────────────────────────────────────────────────────────
        float Len(RSNanoLimb l) => (lengthFrames > 0f ? lengthFrames : (l ? l.gather : 12f)) / RSNanoKeys.Fps;

        bool Ready(AnimationClip clip)
        {
            if (clip && RSNanoKeys.AnimWindow()) return true;
            EditorGUILayout.HelpBox("클립과 애니메이션 창이 있어야 키를 넣는다 (위의 버튼).", MessageType.Warning);
            return false;
        }

        void ActionsTab(AnimationClip clip)
        {
            if (!Ready(clip)) return;
            float t = RSNanoKeys.Now();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"지금 프레임 {Mathf.RoundToInt(t * 60f)} 에 키.  길이", GUILayout.Width(170));
                lengthFrames = EditorGUILayout.Slider(lengthFrames, 0f, 60f);
                GUILayout.Label("프레임", GUILayout.Width(40));
            }
            GUILayout.Label("길이 0 = 부위의 '모이는 시간'. 나타나기 · 빛 펄스 · 풀림 · 흔들림이 이 길이를 쓴다", Rich);
            var root = rig.transform;

            foreach (var l in rig.GetComponentsInChildren<RSNanoLimb>(false))
            {
                EditorGUILayout.Space(3);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("<b>" + l.name + "</b>", Bold, GUILayout.Width(150))) Selection.activeGameObject = l.gameObject;
                        GUILayout.Label($"양 {RSNanoKeys.ValueAt(clip, root, l, "amount", t, l.amount):0.00}  결속 {RSNanoKeys.ValueAt(clip, root, l, "bind", t, l.bind):0.00}" +
                                        (RSNanoKeys.ValueAt(clip, root, l, "danger", t, l.danger ? 1 : 0) > 0.5f ? "  <color=#ff6655>판정</color>" : ""), Rich);
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (Btn("나타나기", "양 0 → 1 (봇이 흘러들어 와 앞에서부터 채운다)")) Do(clip, "나타나기", () => { float L = Len(l); Key(clip, l, "amount", t, 0f); Key(clip, l, "amount", t + L, 1f); });
                        if (Btn("사라지기", "양 → 0 (끝 자리부터 풀려 돌아간다)")) Do(clip, "사라지기", () => { float L = Len(l); Key(clip, l, "amount", t, Val(clip, l, "amount", t, l.amount)); Key(clip, l, "amount", t + L, 0f); });
                        if (l.type == LimbType.Sprite && l.picture && l.picture.Ready)
                        {
                            if (Btn("다음 그림 프레임", "지금 프레임에 그림 프레임 +1 (계단 키). 자동 넘김(초당 프레임)은 끈다"))
                                Do(clip, "그림 프레임", () =>
                                {
                                    if (l.fps > 0f) { Undo.RecordObject(l, "초당 프레임"); l.fps = 0f; }
                                    float cur = Mathf.Floor(Val(clip, l, "frame", t, l.frame));
                                    var c = RSNanoKeys.Curve(clip, root, l, "frame");
                                    float next = c == null || c.length == 0 ? cur : (cur + 1f) % l.picture.frameCount;
                                    if (c == null && t > 1f / 60f) RSNanoKeys.Key(clip, root, l, "frame", 0f, 0f, true);
                                    RSNanoKeys.Key(clip, root, l, "frame", t, next, true);
                                });
                        }
                        if (l.type == LimbType.Chain)
                        {
                            if (Btn("자라기", "자라기 0 → 1 (뿌리부터 뻗는다)")) Do(clip, "자라기", () => { float L = Len(l); Key(clip, l, "grow", t, 0f); Key(clip, l, "grow", t + L, 1f); });
                            if (Btn("접기", "자라기 → 0")) Do(clip, "접기", () => { float L = Len(l); Key(clip, l, "grow", t, Val(clip, l, "grow", t, l.grow)); Key(clip, l, "grow", t + L, 0f); });
                        }
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (Btn("판정 시작", "여기부터 플레이어를 해친다 (계단 키)")) Do(clip, "판정 시작", () => RSNanoKeys.Bool(clip, root, l, "danger", t, true));
                        if (Btn("판정 끝", "여기서 판정을 끈다")) Do(clip, "판정 끝", () => RSNanoKeys.Bool(clip, root, l, "danger", t, false));
                        if (Btn("빛 펄스", "발광 0 → 2.5 (길이) → 0 — 예고")) Do(clip, "빛 펄스", () => { float L = Len(l); Key(clip, l, "glow", t, 0f); Key(clip, l, "glow", t + L, 2.5f); Key(clip, l, "glow", t + L + 4f / 60f, 0f); });
                        if (Btn("풀림 ≈", "결속 → 0 (흐르는 유체로)")) Do(clip, "풀림", () => { float L = Len(l); Key(clip, l, "bind", t, Val(clip, l, "bind", t, l.bind)); Key(clip, l, "bind", t + L, 0f); });
                        if (Btn("결속 착!", "결속 → 1 (4프레임 만에 뿌리부터 잠긴다)")) Do(clip, "결속", () => { Key(clip, l, "bind", t, Val(clip, l, "bind", t, l.bind)); Key(clip, l, "bind", t + 4f / 60f, 1f); });
                    }
                }
            }

            EditorGUILayout.Space(3);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("<b>리그</b>", Bold);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (Btn("흔들림", "화면 흔들림 0 → 0.3 → 0 (길이)")) Do(clip, "흔들림", () => { float L = Len(null); Key(clip, rig, "shake", t, 0f); Key(clip, rig, "shake", t + 1f / 60f, 0.3f); Key(clip, rig, "shake", t + L, 0f); });
                    if (Btn("플레이어 고정", "이 순간 플레이어 발밑을 기억한다 (가시가 솟을 자리)")) Do(clip, "플레이어 고정", () => RSNanoKeys.Bool(clip, root, rig, "lockPlayer", t, true));
                    if (Btn("고정 풀기", "다시 플레이어를 따라간다")) Do(clip, "고정 풀기", () => RSNanoKeys.Bool(clip, root, rig, "lockPlayer", t, false));
                }
                foreach (var e in rig.GetComponentsInChildren<RSNanoEmitter>(false))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(e.name, GUILayout.Width(120));
                        if (Btn("발사", "이 순간 쏜다 (발사 0 → 1 → 0)"))
                            Do(clip, "발사", () =>
                            {
                                if (RSNanoKeys.Curve(clip, root, e, "fire") == null && t > 1f / 60f) RSNanoKeys.Key(clip, root, e, "fire", 0f, 0f, true);
                                RSNanoKeys.Key(clip, root, e, "fire", t, 1f, true);
                                RSNanoKeys.Key(clip, root, e, "fire", t + 6f / 60f, 0f, true);
                            });
                    }
            }
        }

        static bool Btn(string label, string tip) => GUILayout.Button(new GUIContent(label, tip), EditorStyles.miniButton);

        void Key(AnimationClip clip, Object target, string prop, float t, float v) => RSNanoKeys.Key(clip, rig.transform, target, prop, Mathf.Max(0f, t), v);
        float Val(AnimationClip clip, Object target, string prop, float t, float fallback) => RSNanoKeys.ValueAt(clip, rig.transform, target, prop, t, fallback);

        void Do(AnimationClip clip, string label, System.Action a)
        {
            RSNanoKeys.BeginEdit(clip, label);
            a();
            RSNanoKeys.EndEdit(clip);
            rowsDirty = -1;
        }

        // ─────────────────────────────────────────────────────────────
        // 행동 블록
        // ─────────────────────────────────────────────────────────────
        void BlocksTab(AnimationClip clip)
        {
            if (!Ready(clip)) return;
            blockType = GUILayout.Toolbar(blockType, Blocks);
            float t0 = RSNanoKeys.Now();
            EditorGUILayout.Space(4);
            float total;
            switch (blockType)
            {
                case 0:
                    blockTarget = (Transform)EditorGUILayout.ObjectField(new GUIContent("움직일 것", "보통 팔 · 주먹 부위 (리그 안)"), blockTarget, typeof(Transform), true);
                    dashDist = EditorGUILayout.FloatField("거리 (m, +x = 플레이어 쪽)", dashDist);
                    dashPrep = Frames("준비 (뒤로 당김 · 빛)", dashPrep);
                    dashFrames = Frames("돌진", dashFrames);
                    dashHold = Frames("뻗은 채", dashHold);
                    dashBack = Frames("복귀", dashBack);
                    total = dashPrep + dashFrames + dashHold + dashBack;
                    Hint("준비 동안 뒤로 살짝 당기며 빛난다 → 돌진 (판정 · 흔들림) → 뻗은 채 → 제자리로. 판정은 '움직일 것' 아래 부위 전부");
                    if (Make(total) && blockTarget) Do(clip, "돌진", () => Dash(clip, t0));
                    break;
                case 1:
                    blockTarget = (Transform)EditorGUILayout.ObjectField(new GUIContent("돌릴 것", "보통 칼 · 팔 줄기의 뿌리"), blockTarget, typeof(Transform), true);
                    swingFrom = EditorGUILayout.FloatField("들어 올린 각 (도)", swingFrom);
                    swingTo = EditorGUILayout.FloatField("휘두른 뒤 각 (도)", swingTo);
                    swingRaise = Frames("들기", swingRaise);
                    swingHold = Frames("멈춤 (정적)", swingHold);
                    swingFrames = Frames("휘두름", swingFrames);
                    swingBack = Frames("복귀", swingBack);
                    total = swingRaise + swingHold + swingFrames + swingBack;
                    Hint("들어 올려 멈췄다가 (빛) 한 번에 휘두른다 (판정 · 흔들림). 줄기에 '끝 늦음' 을 주면 궤적이 잔상으로 남는다");
                    if (Make(total) && blockTarget) Do(clip, "휘두르기", () => Swing(clip, t0));
                    break;
                case 2:
                    blockTarget = (Transform)EditorGUILayout.ObjectField(new GUIContent("내리찍을 것", "보통 주먹 · 덩어리 부위"), blockTarget, typeof(Transform), true);
                    slamHeight = EditorGUILayout.FloatField("높이 (m)", slamHeight);
                    slamRaise = Frames("들기", slamRaise);
                    slamHold = Frames("멈춤", slamHold);
                    slamDrop = Frames("낙하", slamDrop);
                    slamScatter = Frames("흩어짐", slamScatter);
                    total = slamRaise + slamHold + slamDrop + slamScatter;
                    Hint("플레이어 고정 → 위로 들었다 → 바닥까지 낙하 (판정 · 흔들림) → 결속이 풀려 흩어지고 사라진다");
                    if (Make(total) && blockTarget) Do(clip, "내려찍기", () => Slam(clip, t0));
                    break;
                default:
                    blockLimb = (RSNanoLimb)EditorGUILayout.ObjectField("부위", blockLimb, typeof(RSNanoLimb), true);
                    gatherHold = Frames("유지", gatherHold);
                    gatherOut = Frames("흩어짐", gatherOut);
                    total = (blockLimb ? blockLimb.gather : 20f) + gatherHold + gatherOut;
                    Hint("양 0 → 1 (모이는 시간) → 유지 → 결속이 풀리며 흩어지고 사라진다");
                    if (Make(total) && blockLimb) Do(clip, "모였다 흩어지기", () => Gather(clip, t0));
                    break;
            }
        }

        static float Frames(string label, float v)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                v = Mathf.Max(0f, EditorGUILayout.FloatField(label + " (프레임)", v));
                GUILayout.Label($"{v / 60f:0.00}초", EditorStyles.miniLabel, GUILayout.Width(44));
            }
            return v;
        }

        static void Hint(string s) => GUILayout.Label(s, Rich);

        bool Make(float totalFrames)
        {
            float t0 = RSNanoKeys.Now();
            EditorGUILayout.Space(4);
            GUILayout.Label($"프레임 {Mathf.RoundToInt(t0 * 60f)} → {Mathf.RoundToInt(t0 * 60f + totalFrames)}", Rich);
            return GUILayout.Button("지금 프레임부터 만들기", GUILayout.Height(26));
        }

        RSNanoLimb[] LimbsUnder(Transform t) => t ? t.GetComponentsInChildren<RSNanoLimb>(false) : new RSNanoLimb[0];

        void Shake(AnimationClip clip, float t, float s)
        {
            Key(clip, rig, "shake", t - 1f / 60f, 0f);
            Key(clip, rig, "shake", t + 1f / 60f, s);
            Key(clip, rig, "shake", t + 16f / 60f, 0f);
        }

        void Dash(AnimationClip clip, float t)
        {
            var tr = blockTarget;
            float a = t + dashPrep / 60f, b = a + dashFrames / 60f, c = b + dashHold / 60f, d = c + dashBack / 60f;
            float x0 = Val(clip, tr, "m_LocalPosition.x", t, tr.localPosition.x);
            Key(clip, tr, "m_LocalPosition.x", t, x0);
            Key(clip, tr, "m_LocalPosition.x", a, x0 - dashDist * 0.12f);
            Key(clip, tr, "m_LocalPosition.x", b, x0 + dashDist);
            Key(clip, tr, "m_LocalPosition.x", c, x0 + dashDist);
            Key(clip, tr, "m_LocalPosition.x", d, x0);
            foreach (var l in LimbsUnder(tr))
            {
                Key(clip, l, "glow", t, 0f); Key(clip, l, "glow", a, 2.2f); Key(clip, l, "glow", a + 3f / 60f, 0f);
                RSNanoKeys.Bool(clip, rig.transform, l, "danger", a, true);
                RSNanoKeys.Bool(clip, rig.transform, l, "danger", b + (c - b) * 0.5f, false);
            }
            Shake(clip, b, 0.3f);
        }

        void Swing(AnimationClip clip, float t)
        {
            var tr = blockTarget;
            float a = t + swingRaise / 60f, b = a + swingHold / 60f, c = b + swingFrames / 60f, d = c + swingBack / 60f;
            float z0 = Val(clip, tr, "localEulerAnglesRaw.z", t, RSNanoKeys.NormDeg(tr.localEulerAngles.z));
            RSNanoKeys.EulerZ(clip, rig.transform, tr, t, z0);
            RSNanoKeys.EulerZ(clip, rig.transform, tr, a, swingFrom);
            RSNanoKeys.EulerZ(clip, rig.transform, tr, b, swingFrom);
            RSNanoKeys.EulerZ(clip, rig.transform, tr, c, swingTo);
            RSNanoKeys.EulerZ(clip, rig.transform, tr, d, z0);
            foreach (var l in LimbsUnder(tr))
            {
                Key(clip, l, "glow", a, 0f); Key(clip, l, "glow", b, 2f); Key(clip, l, "glow", c, 0f);
                RSNanoKeys.Bool(clip, rig.transform, l, "danger", b, true);
                RSNanoKeys.Bool(clip, rig.transform, l, "danger", c + 4f / 60f, false);
            }
            Shake(clip, c, 0.35f);
        }

        void Slam(AnimationClip clip, float t)
        {
            var tr = blockTarget;
            float a = t + slamRaise / 60f, b = a + slamHold / 60f, c = b + slamDrop / 60f, d = c + slamScatter / 60f;
            float y0 = Val(clip, tr, "m_LocalPosition.y", t, tr.localPosition.y);
            float floor = RSNanoRigPreview.FloorY(rig);
            float yFloor = tr.parent ? tr.parent.InverseTransformPoint(new Vector3(tr.position.x, floor + 0.4f, tr.position.z)).y : floor + 0.4f;
            RSNanoKeys.Bool(clip, rig.transform, rig, "lockPlayer", t, true);
            Key(clip, tr, "m_LocalPosition.y", t, y0);
            Key(clip, tr, "m_LocalPosition.y", a, y0 + slamHeight);
            Key(clip, tr, "m_LocalPosition.y", b, y0 + slamHeight);
            Key(clip, tr, "m_LocalPosition.y", c, yFloor);
            Key(clip, tr, "m_LocalPosition.y", d, yFloor);
            foreach (var l in LimbsUnder(tr))
            {
                Key(clip, l, "glow", a, 0f); Key(clip, l, "glow", b, 2f); Key(clip, l, "glow", c, 0f);
                RSNanoKeys.Bool(clip, rig.transform, l, "danger", b + (c - b) * 0.4f, true);
                RSNanoKeys.Bool(clip, rig.transform, l, "danger", c + 4f / 60f, false);
                Key(clip, l, "bind", c, 1f); Key(clip, l, "bind", c + 4f / 60f, 0f);
                Key(clip, l, "amount", c + (d - c) * 0.4f, Val(clip, l, "amount", c, l.amount)); Key(clip, l, "amount", d, 0f);
            }
            Shake(clip, c, 0.45f);
            RSNanoKeys.Bool(clip, rig.transform, rig, "lockPlayer", d, false);
        }

        void Gather(AnimationClip clip, float t)
        {
            var l = blockLimb;
            float a = t + l.gather / 60f, b = a + gatherHold / 60f, c = b + gatherOut / 60f;
            Key(clip, l, "amount", t, 0f); Key(clip, l, "amount", a, 1f);
            Key(clip, l, "bind", b, 1f); Key(clip, l, "bind", b + 4f / 60f, 0f);
            Key(clip, l, "amount", b + (c - b) * 0.4f, 1f); Key(clip, l, "amount", c, 0f);
        }

        // ─────────────────────────────────────────────────────────────
        // 포즈
        // ─────────────────────────────────────────────────────────────
        void PosesTab(AnimationClip clip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var lib = (RSNanoPoseLibrary)EditorGUILayout.ObjectField("포즈 모음", rig.poses, typeof(RSNanoPoseLibrary), false);
                if (lib != rig.poses) { Undo.RecordObject(rig, "포즈 모음"); rig.poses = lib; }
                if (!rig.poses && GUILayout.Button("새로", GUILayout.Width(44)))
                {
                    string dir = RSPaths.Ensure(RSPaths.Data + "/NanoBoss/포즈");
                    var asset = CreateInstance<RSNanoPoseLibrary>();
                    AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(dir + "/" + rig.name + " 포즈.asset"));
                    AssetDatabase.SaveAssets();
                    Undo.RecordObject(rig, "포즈 모음");
                    rig.poses = asset;
                }
            }
            if (!rig.poses) { Hint("포즈 모음을 만들면 지금 리그 모양을 '키 포즈' 로 저장해 두고, 다른 프레임에 바로 불러 키로 넣을 수 있다."); return; }

            using (new EditorGUILayout.HorizontalScope())
            {
                poseName = EditorGUILayout.TextField(poseName);
                if (GUILayout.Button("지금 모양 저장", GUILayout.Width(100)))
                {
                    Undo.RecordObject(rig.poses, "포즈 저장");
                    rig.poses.poses.Add(Capture(poseName));
                    EditorUtility.SetDirty(rig.poses);
                }
            }
            Hint("키로 넣기 = 지금 프레임에, 지금과 달라진 값만 키. 적용 = 키 없이 모양만 (기록 중이면 키가 잡힌다)");

            var ps = rig.poses.poses;
            for (int i = 0; i < ps.Count; i++)
            {
                var p = ps[i];
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    Rect th = GUILayoutUtility.GetRect(72, 46, GUILayout.Width(72), GUILayout.Height(46));
                    DrawSketch(th, p);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        string n = EditorGUILayout.TextField(p.name);
                        if (n != p.name) { Undo.RecordObject(rig.poses, "포즈 이름"); p.name = n; EditorUtility.SetDirty(rig.poses); }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            using (new EditorGUI.DisabledScope(!clip || !RSNanoKeys.AnimWindow()))
                                if (GUILayout.Button("키로 넣기", EditorStyles.miniButtonLeft)) Do(clip, "포즈 키", () => ApplyPose(p, clip, RSNanoKeys.Now()));
                            if (GUILayout.Button("적용", EditorStyles.miniButtonMid)) ApplyPose(p, null, 0f);
                            if (GUILayout.Button("덮어쓰기", EditorStyles.miniButtonMid)) { Undo.RecordObject(rig.poses, "포즈 덮어쓰기"); ps[i] = Capture(p.name); EditorUtility.SetDirty(rig.poses); }
                            if (GUILayout.Button("×", EditorStyles.miniButtonRight, GUILayout.Width(22))) { Undo.RecordObject(rig.poses, "포즈 지우기"); ps.RemoveAt(i); EditorUtility.SetDirty(rig.poses); GUIUtility.ExitGUI(); }
                        }
                    }
                }
            }
        }

        bool Skip(Transform t) => t == rig.transform || (rig.playerAnchor && t == rig.playerAnchor);

        RSNanoPoseLibrary.Pose Capture(string name)
        {
            var p = new RSNanoPoseLibrary.Pose { name = name };
            var root = rig.transform;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (Skip(t)) continue;
                var e = t.localEulerAngles;
                p.transforms.Add(new RSNanoPoseLibrary.TransformPose
                {
                    path = RSNanoKeys.PathOf(root, t), position = t.localPosition,
                    euler = new Vector3(RSNanoKeys.NormDeg(e.x), RSNanoKeys.NormDeg(e.y), RSNanoKeys.NormDeg(e.z)), scale = t.localScale,
                });
            }
            foreach (var l in rig.GetComponentsInChildren<RSNanoLimb>(true))
            {
                p.limbs.Add(new RSNanoPoseLibrary.LimbPose { path = RSNanoKeys.PathOf(root, l.transform), radius = l.radius, grow = l.grow, wobble = l.wobble, bind = l.bind, size = l.size, extent = l.extent });
                l.RefreshJoints();
                l.CachePose();
                if (l.type == LimbType.Chain)
                {
                    Vector3 prev = root.InverseTransformPoint(l.Joints[0].position);
                    for (int j = 1; j < l.Joints.Count; j++)
                    {
                        Vector3 q = root.InverseTransformPoint(l.Joints[j].position);
                        p.sketch.Add(new Vector4(prev.x, prev.y, q.x, q.y));
                        prev = q;
                    }
                }
                else if (l.type == LimbType.Blob)
                {
                    for (int k = 0; k < 8; k++)
                    {
                        float a0 = k / 8f * Mathf.PI * 2f, a1 = (k + 1) / 8f * Mathf.PI * 2f;
                        Vector3 q0 = root.InverseTransformPoint(l.transform.TransformPoint(new Vector3(Mathf.Cos(a0) * l.size.x, Mathf.Sin(a0) * l.size.y, 0f)));
                        Vector3 q1 = root.InverseTransformPoint(l.transform.TransformPoint(new Vector3(Mathf.Cos(a1) * l.size.x, Mathf.Sin(a1) * l.size.y, 0f)));
                        p.sketch.Add(new Vector4(q0.x, q0.y, q1.x, q1.y));
                    }
                }
            }
            foreach (var jc in rig.GetComponentsInChildren<RSNanoJoint>(true))
                p.joints.Add(new RSNanoPoseLibrary.JointPose { path = RSNanoKeys.PathOf(root, jc.transform), thickness = jc.thickness });
            return p;
        }

        Transform Find(string path) => string.IsNullOrEmpty(path) ? rig.transform : rig.transform.Find(path);

        void ApplyPose(RSNanoPoseLibrary.Pose p, AnimationClip clip, float t)
        {
            var root = rig.transform;
            foreach (var tp in p.transforms)
            {
                var tr = Find(tp.path);
                if (!tr || Skip(tr)) continue;
                if (clip) RSNanoKeys.TransformPose(clip, root, tr, t, tp.position, tp.euler, tp.scale, true);
                else { Undo.RecordObject(tr, "포즈 적용"); tr.localPosition = tp.position; tr.localEulerAngles = tp.euler; tr.localScale = tp.scale; }
            }
            foreach (var lp in p.limbs)
            {
                var tr = Find(lp.path);
                var l = tr ? tr.GetComponent<RSNanoLimb>() : null;
                if (!l) continue;
                if (clip)
                {
                    RSNanoKeys.KeyIf(clip, root, l, "radius", t, lp.radius, true);
                    RSNanoKeys.KeyIf(clip, root, l, "grow", t, lp.grow, true);
                    RSNanoKeys.KeyIf(clip, root, l, "wobble", t, lp.wobble, true);
                    RSNanoKeys.KeyIf(clip, root, l, "bind", t, lp.bind, true);
                    RSNanoKeys.KeyIf(clip, root, l, "size.x", t, lp.size.x, true);
                    RSNanoKeys.KeyIf(clip, root, l, "size.y", t, lp.size.y, true);
                    RSNanoKeys.KeyIf(clip, root, l, "extent.x", t, lp.extent.x, true);
                    RSNanoKeys.KeyIf(clip, root, l, "extent.y", t, lp.extent.y, true);
                }
                else { Undo.RecordObject(l, "포즈 적용"); l.radius = lp.radius; l.grow = lp.grow; l.wobble = lp.wobble; l.bind = lp.bind; l.size = lp.size; l.extent = lp.extent; }
            }
            foreach (var jp in p.joints)
            {
                var tr = Find(jp.path);
                var jc = tr ? tr.GetComponent<RSNanoJoint>() : null;
                if (!jc) continue;
                if (clip) RSNanoKeys.KeyIf(clip, root, jc, "thickness", t, jp.thickness, true);
                else { Undo.RecordObject(jc, "포즈 적용"); jc.thickness = jp.thickness; }
            }
            RSNanoRigPreview.MarkDirty();
        }

        static void DrawSketch(Rect r, RSNanoPoseLibrary.Pose p)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.3f));
            if (p.sketch.Count == 0) return;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var s in p.sketch)
            {
                x0 = Mathf.Min(x0, Mathf.Min(s.x, s.z)); x1 = Mathf.Max(x1, Mathf.Max(s.x, s.z));
                y0 = Mathf.Min(y0, Mathf.Min(s.y, s.w)); y1 = Mathf.Max(y1, Mathf.Max(s.y, s.w));
            }
            float k = Mathf.Min((r.width - 8) / Mathf.Max(0.01f, x1 - x0), (r.height - 8) / Mathf.Max(0.01f, y1 - y0));
            Vector2 c = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
            Vector3 P(float x, float y) => new Vector3(r.center.x + (x - c.x) * k, r.center.y - (y - c.y) * k, 0f);
            Handles.color = new Color(0.55f, 0.9f, 1f, 0.9f);
            foreach (var s in p.sketch) Handles.DrawAAPolyLine(2f, P(s.x, s.y), P(s.z, s.w));
        }

        // ─────────────────────────────────────────────────────────────
        // 점검
        // ─────────────────────────────────────────────────────────────
        void CheckTab(AnimationClip clip)
        {
            var issues = RSNanoRigCheck.Run(rig, clip);
            if (issues.Count == 0) EditorGUILayout.HelpBox("문제 없음", MessageType.Info);
            foreach (var i in issues)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(i.text, i.level >= 2 ? MessageType.Error : i.level == 1 ? MessageType.Warning : MessageType.Info);
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(70)))
                    {
                        if (i.fix != null && GUILayout.Button(i.fixLabel ?? "고치기", EditorStyles.miniButton)) { i.fix(); RSNanoRigPreview.MarkDirty(); rowsDirty = -1; GUIUtility.ExitGUI(); }
                        if (i.focus && GUILayout.Button("고르기", EditorStyles.miniButton)) Selection.activeObject = i.focus;
                    }
                }
            }
            EditorGUILayout.Space(6);
            var sw = rig.swarm;
            if (sw) GUILayout.Label($"군집 봇 {sw.Count} · 격자 간격 {sw.spacing * 100f:0.0}cm · 시뮬 {sw.SimMs:0.00}ms (에디터, 대상 PC 는 Burst 기준 1ms 이하 목표)", Rich);
            if (RSNanoRigPreview.Scrubbing) GUILayout.Label($"되감기 재시뮬 {RSNanoRigPreview.LastResimMs:0}ms", Rich);
        }
    }
}
