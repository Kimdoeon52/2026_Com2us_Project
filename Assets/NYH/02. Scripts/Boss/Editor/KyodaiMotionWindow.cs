using System.Collections.Generic;
using DG.Tweening;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 쿄다이 동작을 만드는 창 (2026-10-29, 메뉴 NYH/쿄다이/모션 창).
///
///   위    모션 에셋 · 재생/정지 · 시간 막대 · 미리보기(플레이 중)
///   띠    트랙(부위)마다 2줄: ◆ 손 키 / 색 띠 = 레이어(배경·전이·공격·잔류·복귀). 맨 아래 이벤트 줄(판정·그림자)
///         빈 곳을 누르거나 끌면 그 시각으로, ◆를 누르면 선택, 끌면 시각 이동
///   Scene 뷰  노란 십자 = 목표(플레이어 자리 대신) — 끌어서 옮김. 트랙 경로와 ◆ 키 — 선택한 키는 화살표로 끌어서 손 위치 수정.
///             빨간 네모 = 판정 범위, 검은 띠 = 그림자. 흰 점 = 지금 손이 보이는 자리
///   미리보기  플레이 전: 편집 미리보기가 씬의 실제 로봇을 움직인다(끄면 원래 자세로). 플레이 중: '미리보기 시작'(보스 AI는 멈춤).
///             편집 내용은 에셋에 바로 저장 — 플레이를 멈춰도 남는다
/// </summary>
public class KyodaiMotionWindow : EditorWindow
{
    [MenuItem("NYH/쿄다이/모션 창")]
    private static void Open() => GetWindow<KyodaiMotionWindow>("쿄다이 모션");

    private static readonly Color[] LayerColors =
    {
        new Color(0.35f, 0.35f, 0.4f),  // 배경
        new Color(0.95f, 0.75f, 0.2f),  // 전이
        new Color(0.9f, 0.25f, 0.25f),  // 공격
        new Color(0.95f, 0.95f, 0.95f), // 잔류
        new Color(0.45f, 0.55f, 0.9f),  // 복귀
    };
    private static readonly string[] LayerNames = { "배경", "전이", "공격", "잔류", "복귀" };
    private static readonly Color[] TrackColors = { new Color(0.3f, 0.9f, 1f), new Color(1f, 0.5f, 0.9f), new Color(0.6f, 1f, 0.4f), new Color(1f, 0.7f, 0.3f) };

    private KyodaiMotionData motion;
    private KyodaiBossController boss;
    private float time;
    private bool playing;
    private bool loop = true;
    private float speed = 1f;
    private bool preview;
    private double lastTick;
    private Vector2 target;
    private bool targetSet;
    private Vector2 scroll;

    private int selTrack;
    private int selKey = -1;
    private int selEvent = -1;
    private bool selLayerMark;
    private int selMark = -1;

    // 플레이 전 미리보기 (씬의 실제 로봇을 움직임) — 2026-10-29, 2026-10-30
    private readonly KyodaiEditPreview editPreview = new KyodaiEditPreview();
    private bool editPreviewOn = true;
    private bool previewDirty = true;
    private float renderedTime = -1f;
    private Vector2 renderedTarget;
    private double lastClipRefresh;
    private bool pausedForAnimationWindow;

    // 타임라인 드래그 상태
    private enum Drag { None, Time, Key, Event, Mark }
    private Drag drag;

    private const float LabelW = 70f;
    private const float RulerH = 18f, KeyRowH = 20f, LayerRowH = 14f, EventRowH = 20f;

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnScene;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += editPreview.End;
        UnityEditor.SceneManagement.EditorSceneManager.sceneClosing += OnSceneClosing;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorApplication.quitting += editPreview.End;
        Undo.undoRedoPerformed += OnUndoRedo;
        lastTick = EditorApplication.timeSinceStartup;
        if (motion == null && Selection.activeObject is KyodaiMotionData m) motion = m;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnScene;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= editPreview.End;
        UnityEditor.SceneManagement.EditorSceneManager.sceneClosing -= OnSceneClosing;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorApplication.quitting -= editPreview.End;
        Undo.undoRedoPerformed -= OnUndoRedo;
        StopPreview();
        editPreview.End();
    }

    private void OnSceneClosing(UnityEngine.SceneManagement.Scene scene, bool removing) => editPreview.End();

    // 미리보기로 꺾인 자세가 씬에 저장되지 않게 — 저장 직전에 원래 자세로. 다음 Tick에서 다시 시작한다
    private void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) { editPreview.End(); previewDirty = true; }
    private void OnUndoRedo() { previewDirty = true; Repaint(); }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode) { preview = false; playing = false; }
        if (change == PlayModeStateChange.ExitingEditMode) editPreview.End();
        boss = null;
        previewDirty = true;
        Repaint();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is KyodaiMotionData m && m != motion) { motion = m; selKey = selEvent = selMark = -1; selTrack = 0; Repaint(); }
    }

    // ── 시간 진행 + 미리보기 적용 ──

    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - lastTick);
        lastTick = now;

        if (motion == null) return;
        if (playing)
        {
            time += dt * speed;
            if (time > motion.duration)
            {
                if (loop) time = 0f;
                else { time = motion.duration; playing = false; }
            }
            Repaint();
            SceneView.RepaintAll();
        }

        if (preview)
        {
            if (!Application.isPlaying || FindBoss() == null) { preview = false; return; }
            KyodaiMotionPlayer.ApplyPose(motion, time, target, boss);
        }

        // 플레이 전: 씬의 실제 로봇을 움직인다. 애니메이션 창이 녹화·미리보기 중이면 그쪽이 로봇을 쓰니 비켜준다
        // 멈출 때 기본 자세 기록은 버리지 않고(End 아님) 원래 자세로만 돌린다 — 꺾인 자세를 기본 자세로 다시 읽는 일이 없게
        bool animWindowBusy = AnimationMode.InAnimationMode();
        if (animWindowBusy != pausedForAnimationWindow)
        {
            pausedForAnimationWindow = animWindowBusy;
            if (animWindowBusy) editPreview.Pause();
            else previewDirty = true;
            Repaint();
        }
        if (animWindowBusy && !Application.isPlaying) return;
        if (!Application.isPlaying && editPreviewOn && FindBoss() != null)
        {
            if (!editPreview.Active || editPreview.Boss != boss) { editPreview.Begin(boss); previewDirty = true; }
            // 클립이 있으면 애니메이션 창에서 키를 고친 것도 따라오게 가끔 다시 적용한다
            if (motion.clip != null && now - lastClipRefresh > 0.25) { previewDirty = true; lastClipRefresh = now; }
            if (editPreview.Active && (previewDirty || playing || renderedTime != time || renderedTarget != target))
            {
                editPreview.Apply(motion, time, target);
                previewDirty = false;
                renderedTime = time;
                renderedTarget = target;
                SceneView.RepaintAll();
                Repaint();
            }
        }
        else if (editPreview.Active) editPreview.End();
    }

    private KyodaiBossController FindBoss()
    {
        if (boss == null) boss = Object.FindAnyObjectByType<KyodaiBossController>();
        return boss;
    }

    private void StartPreview()
    {
        if (!Application.isPlaying || FindBoss() == null) return;
        boss.CancelCurrentStateToken(); // 보스 AI(패턴 선택) 멈춤 — 진행 중 패턴은 정리되며 대기 자세로
        KyodaiMotionPlayer.Clear(boss);
        preview = true;
    }

    private void StopPreview()
    {
        if (!preview) return;
        preview = false;
        if (Application.isPlaying && boss != null)
        {
            KyodaiMotionPlayer.Clear(boss);
            boss.InterruptToStun(0.05f); // 잠깐 스턴 → 딜타임 → 패턴 선택으로 AI가 다시 돈다
        }
    }

    private void EnsureTarget()
    {
        if (targetSet || FindBoss() == null) return;
        target = new Vector2((boss.ArenaMinX + boss.ArenaMaxX) * 0.5f, boss.GroundY);
        targetSet = true;
    }

    // ── 창 ──

    private void OnGUI()
    {
        FindBoss();
        EnsureTarget();

        using (new EditorGUILayout.HorizontalScope())
        {
            var m = (KyodaiMotionData)EditorGUILayout.ObjectField("모션", motion, typeof(KyodaiMotionData), false);
            if (m != motion) { motion = m; selKey = selEvent = selMark = -1; selTrack = 0; }
            if (GUILayout.Button("새 모션", GUILayout.Width(60))) CreateMotion();
        }
        boss = (KyodaiBossController)EditorGUILayout.ObjectField("보스", boss, typeof(KyodaiBossController), true);

        if (motion == null)
        {
            EditorGUILayout.HelpBox("모션 에셋을 고르거나 '새 모션'을 누르세요. Project 창에서 모션 에셋을 클릭해도 열립니다.", MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        Undo.RecordObject(motion, "쿄다이 모션 편집");
        EditorGUI.BeginChangeCheck();

        DrawTransport();
        DrawTimeline();
        DrawTrackTools();
        DrawSelection();

        if (EditorGUI.EndChangeCheck()) MarkDirty();
        EditorGUILayout.EndScrollView();
    }

    private void DrawTransport()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(playing ? "■ 정지" : "▶ 재생", GUILayout.Width(70))) { playing = !playing; if (playing && time >= motion.duration) time = 0f; }
            if (GUILayout.Button("|◀", GUILayout.Width(30))) { time = 0f; playing = false; }
            if (GUILayout.Button("◀", GUILayout.Width(26))) time = Mathf.Max(0f, time - 1f / 60f);
            if (GUILayout.Button("▶", GUILayout.Width(26))) time = Mathf.Min(motion.duration, time + 1f / 60f);
            loop = GUILayout.Toggle(loop, "반복", GUILayout.Width(44));
            speed = EditorGUILayout.Popup(speed == 1f ? 0 : speed == 0.5f ? 1 : 2, new[] { "1배", "½배", "¼배" }, GUILayout.Width(50)) switch { 0 => 1f, 1 => 0.5f, _ => 0.25f };
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{time:0.00}s / {motion.duration:0.00}s");
        }

        motion.duration = Mathf.Max(0.1f, EditorGUILayout.FloatField("전체 길이(초)", motion.duration));
        motion.targetMargin = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("벽 여유", "목표를 경기장 안으로 자를 때 벽에서 띄울 거리 — 보통 공격 너비의 절반"), motion.targetMargin));
        using (new EditorGUILayout.HorizontalScope())
        {
            motion.clip = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("온몸 애니메이션", "Unity 애니메이션 창에서 리깅 모델로 녹화한 클립. 모션 0초 = 클립 0초. 손 키의 IK 세기가 0이면 애니메이션 그대로"), motion.clip, typeof(AnimationClip), false);
            if (motion.clip != null && GUILayout.Button("길이 맞추기", GUILayout.Width(80))) motion.duration = Mathf.Max(0.1f, motion.clip.length);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            target = EditorGUILayout.Vector2Field(new GUIContent("목표 지점(미리보기)", "게임에서는 플레이어 발밑. Scene 뷰의 노란 십자를 끌어도 됨"), target);
            if (Application.isPlaying && boss != null && GUILayout.Button("플레이어 위치로", GUILayout.Width(100)))
                target = KyodaiMotionPattern.PickTarget(boss, motion);
        }

        if (!Application.isPlaying)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool on = GUILayout.Toggle(editPreviewOn, new GUIContent(" 편집 미리보기 (플레이 안 해도 로봇이 움직임)",
                    "씬의 실제 로봇을 이 모션 자세로 꺾는다. 끄거나 씬을 저장하면 원래 자세로 돌아간다 — Scene 뷰에서 돌려보고 확대해서 확인"));
                if (on != editPreviewOn) { editPreviewOn = on; previewDirty = true; }
                if (GUILayout.Button(new GUIContent("새로고침", "뼈 기본 자세를 직접 고쳤으면 미리보기를 껐다 켜거나 이걸 눌러 기본 자세를 다시 읽는다"), GUILayout.Width(70)))
                {
                    editPreview.End();
                    previewDirty = true;
                }
            }
            if (editPreviewOn && pausedForAnimationWindow)
                EditorGUILayout.HelpBox("애니메이션 창이 녹화·미리보기 중이라 편집 미리보기를 잠시 멈췄습니다. 애니메이션 창의 미리보기를 끄면 다시 움직입니다.", MessageType.Info);
            else if (editPreviewOn)
                EditorGUILayout.HelpBox("미리보기 중에는 로봇 뼈를 직접 돌려도 다음 프레임에 덮어씌워집니다. 뼈를 고칠 땐 미리보기를 끄세요.", MessageType.None);
        }
        else if (boss == null)
            EditorGUILayout.HelpBox("씬에 KyodaiBossController가 없습니다.", MessageType.Warning);
        else
        {
            var bg = GUI.backgroundColor;
            if (preview) GUI.backgroundColor = new Color(1f, 0.6f, 0.4f);
            if (GUILayout.Button(preview ? "미리보기 끝 (보스 AI 다시 켜기)" : "미리보기 시작 (보스 AI 멈추고 이 모션으로 움직이기)", GUILayout.Height(24)))
            {
                if (preview) StopPreview(); else StartPreview();
            }
            GUI.backgroundColor = bg;
        }
    }

    // ── 타임라인 ──

    private float TimeToX(Rect r, float t) => r.x + LabelW + t / motion.duration * (r.width - LabelW - 8f);
    private float XToTime(Rect r, float x) => Mathf.Clamp((x - r.x - LabelW) / (r.width - LabelW - 8f) * motion.duration, 0f, motion.duration);

    private void DrawTimeline()
    {
        float height = RulerH + motion.tracks.Count * (KeyRowH + LayerRowH) + EventRowH + 4f;
        Rect r = GUILayoutUtility.GetRect(position.width - 20f, height);
        EditorGUI.DrawRect(r, new Color(0.16f, 0.16f, 0.16f));
        Event ev = Event.current;

        // 눈금
        Rect ruler = new Rect(r.x, r.y, r.width, RulerH);
        float step = motion.duration > 6f ? 0.5f : 0.1f;
        for (float t = 0f; t <= motion.duration + 1e-4f; t += step)
        {
            float x = TimeToX(r, t);
            bool major = Mathf.Abs(t / (step * 5f) - Mathf.Round(t / (step * 5f))) < 1e-3f;
            EditorGUI.DrawRect(new Rect(x, ruler.yMax - (major ? 8f : 4f), 1f, major ? 8f : 4f), Color.gray);
            if (major) GUI.Label(new Rect(x + 2f, ruler.y, 40f, 14f), $"{t:0.#}", EditorStyles.miniLabel);
        }

        float y = ruler.yMax;
        var hits = new List<(Rect rect, Drag kind, int track, int index)>();

        for (int ti = 0; ti < motion.tracks.Count; ti++)
        {
            var track = motion.tracks[ti];
            Color tc = TrackColors[ti % TrackColors.Length];
            Rect keyRow = new Rect(r.x, y, r.width, KeyRowH);
            Rect layerRow = new Rect(r.x, y + KeyRowH, r.width, LayerRowH);
            if (ti == selTrack) EditorGUI.DrawRect(new Rect(r.x, y, LabelW - 2f, KeyRowH + LayerRowH), new Color(1f, 1f, 1f, 0.08f));
            GUI.Label(new Rect(r.x + 4f, y + 2f, LabelW, 16f), track.partId, EditorStyles.boldLabel);

            // 레이어 띠
            for (int mi = 0; mi < track.layers.Count; mi++)
            {
                var mark = track.layers[mi];
                float x0 = TimeToX(r, mark.time);
                float x1 = mi + 1 < track.layers.Count ? TimeToX(r, track.layers[mi + 1].time) : TimeToX(r, motion.duration);
                Rect band = new Rect(x0, layerRow.y + 2f, Mathf.Max(1f, x1 - x0), LayerRowH - 4f);
                EditorGUI.DrawRect(band, LayerColors[(int)mark.layer]);
                if (band.width > 26f) GUI.Label(band, LayerNames[(int)mark.layer], CenteredMini(mark.layer == LimbLayer.Linger));
                Rect handle = new Rect(x0 - 3f, layerRow.y, 6f, LayerRowH);
                bool sel = selLayerMark && ti == selTrack && mi == selMark;
                EditorGUI.DrawRect(new Rect(x0 - 1f, layerRow.y, sel ? 3f : 2f, LayerRowH), sel ? Color.yellow : Color.black);
                hits.Add((handle, Drag.Mark, ti, mi));
            }

            // 손 키
            for (int ki = 0; ki < track.keys.Count; ki++)
            {
                float x = TimeToX(r, track.keys[ki].time);
                Rect d = new Rect(x - 6f, keyRow.y + 3f, 12f, 14f);
                bool sel = ti == selTrack && ki == selKey && !selLayerMark;
                GUI.color = sel ? Color.yellow : track.keys[ki].rest ? Color.gray : tc;
                GUI.Label(d, "◆", CenteredBig());
                GUI.color = Color.white;
                hits.Add((d, Drag.Key, ti, ki));
            }
            y += KeyRowH + LayerRowH;
        }

        // 이벤트 줄
        Rect evRow = new Rect(r.x, y, r.width, EventRowH);
        GUI.Label(new Rect(r.x + 4f, y + 2f, LabelW, 16f), "이벤트", EditorStyles.boldLabel);
        for (int ei = 0; ei < motion.events.Count; ei++)
        {
            var e = motion.events[ei];
            float x = TimeToX(r, e.time);
            Rect d = new Rect(x - 7f, evRow.y + 2f, 14f, 16f);
            GUI.color = ei == selEvent ? Color.yellow : e.type == KyodaiMotionEventType.Hit ? new Color(1f, 0.3f, 0.3f) : new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(d, e.type == KyodaiMotionEventType.Hit ? "✖" : e.type == KyodaiMotionEventType.TelegraphShow ? "▼" : "▲", CenteredBig());
            GUI.color = Color.white;
            hits.Add((d, Drag.Event, -1, ei));
        }

        // 지금 시각
        float cx = TimeToX(r, time);
        EditorGUI.DrawRect(new Rect(cx, r.y, 1.5f, r.height), new Color(1f, 0.25f, 0.25f));

        // 마우스
        int id = GUIUtility.GetControlID(FocusType.Passive);
        switch (ev.GetTypeForControl(id))
        {
            case EventType.MouseDown when ev.button == 0 && r.Contains(ev.mousePosition):
                drag = Drag.Time;
                for (int i = hits.Count - 1; i >= 0; i--)
                {
                    if (!hits[i].rect.Contains(ev.mousePosition)) continue;
                    drag = hits[i].kind;
                    selLayerMark = drag == Drag.Mark;
                    if (drag == Drag.Key) { selTrack = hits[i].track; selKey = hits[i].index; selEvent = -1; selMark = -1; }
                    else if (drag == Drag.Mark) { selTrack = hits[i].track; selMark = hits[i].index; selKey = -1; selEvent = -1; }
                    else if (drag == Drag.Event) { selEvent = hits[i].index; selKey = -1; selMark = -1; }
                    break;
                }
                if (drag == Drag.Time)
                {
                    // 트랙 줄을 누르면 그 트랙 선택
                    float ry = ev.mousePosition.y - ruler.yMax;
                    int row = Mathf.FloorToInt(ry / (KeyRowH + LayerRowH));
                    if (ry >= 0f && row < motion.tracks.Count) selTrack = row;
                    time = XToTime(r, ev.mousePosition.x);
                }
                GUIUtility.hotControl = id;
                ev.Use();
                SceneView.RepaintAll();
                break;

            case EventType.MouseDrag when GUIUtility.hotControl == id:
                float t = XToTime(r, ev.mousePosition.x);
                if (ev.shift == false) t = Mathf.Round(t * 60f) / 60f; // 1프레임 단위로 붙는다 (Shift = 자유)
                if (drag == Drag.Time) time = t;
                else if (drag == Drag.Key && Valid(selTrack, selKey)) { motion.tracks[selTrack].keys[selKey].time = t; time = t; MarkDirty(); }
                else if (drag == Drag.Mark && selTrack < motion.tracks.Count && selMark >= 0 && selMark < motion.tracks[selTrack].layers.Count) { motion.tracks[selTrack].layers[selMark].time = t; time = t; MarkDirty(); }
                else if (drag == Drag.Event && selEvent >= 0 && selEvent < motion.events.Count) { motion.events[selEvent].time = t; time = t; MarkDirty(); }
                ev.Use();
                SceneView.RepaintAll();
                break;

            case EventType.MouseUp when GUIUtility.hotControl == id:
                GUIUtility.hotControl = 0;
                if (drag != Drag.Time) ResortKeepingSelection();
                drag = Drag.None;
                ev.Use();
                break;
        }
    }

    private static GUIStyle centeredMini, centeredMiniDark, centeredBig;
    private static GUIStyle CenteredMini(bool dark)
    {
        centeredMini ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        centeredMiniDark ??= new GUIStyle(centeredMini) { normal = { textColor = Color.black } };
        return dark ? centeredMiniDark : centeredMini;
    }
    private static GUIStyle CenteredBig() => centeredBig ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };

    private bool Valid(int track, int key) => track >= 0 && track < motion.tracks.Count && key >= 0 && key < motion.tracks[track].keys.Count;

    // 시각 순으로 다시 정렬하면서 선택한 항목을 놓치지 않게
    private void ResortKeepingSelection()
    {
        KyodaiMotionKey k = Valid(selTrack, selKey) ? motion.tracks[selTrack].keys[selKey] : null;
        KyodaiLayerMark m = selTrack < motion.tracks.Count && selMark >= 0 && selMark < motion.tracks[selTrack].layers.Count ? motion.tracks[selTrack].layers[selMark] : null;
        KyodaiMotionEvent e = selEvent >= 0 && selEvent < motion.events.Count ? motion.events[selEvent] : null;
        motion.SortAll();
        if (k != null) selKey = motion.tracks[selTrack].keys.IndexOf(k);
        if (m != null) selMark = motion.tracks[selTrack].layers.IndexOf(m);
        if (e != null) selEvent = motion.events.IndexOf(e);
        MarkDirty();
    }

    // ── 트랙·키·띠·이벤트 버튼 ──

    private void DrawTrackTools()
    {
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("트랙", EditorStyles.boldLabel, GUILayout.Width(40));
            if (GUILayout.Button("+ 트랙", GUILayout.Width(60)))
            {
                var menu = new GenericMenu();
                var ids = new List<string> { "오른팔", "왼팔", "오른다리", "왼다리" };
                if (boss != null) foreach (var l in boss.Limbs) if (!ids.Contains(l.PartId)) ids.Add(l.PartId);
                foreach (var pid in ids)
                {
                    string p = pid;
                    menu.AddItem(new GUIContent(p), false, () =>
                    {
                        Undo.RecordObject(motion, "트랙 추가");
                        motion.tracks.Add(new KyodaiMotionTrack { partId = p, keys = { new KyodaiMotionKey { time = 0f, rest = true } }, layers = { new KyodaiLayerMark { time = 0f, layer = LimbLayer.Background } } });
                        selTrack = motion.tracks.Count - 1;
                        MarkDirty();
                    });
                }
                menu.ShowAsContext();
            }
            using (new EditorGUI.DisabledScope(selTrack >= motion.tracks.Count))
                if (GUILayout.Button("트랙 삭제", GUILayout.Width(70)) && EditorUtility.DisplayDialog("쿄다이 모션", $"'{motion.tracks[selTrack].partId}' 트랙을 지울까요?", "지우기", "취소"))
                {
                    motion.tracks.RemoveAt(selTrack);
                    selTrack = 0; selKey = -1; selMark = -1;
                    MarkDirty();
                    GUIUtility.ExitGUI();
                }
        }

        if (selTrack >= motion.tracks.Count) return;
        var track = motion.tracks[selTrack];

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label($"손 키 ({track.partId})", EditorStyles.boldLabel, GUILayout.Width(110));
            if (GUILayout.Button(new GUIContent("+ 지금 자세로 키", "지금 시각의 손 위치를 그대로 키로. 그다음 Scene 뷰에서 끌어 고친다"), GUILayout.Width(120))) AddKeyHere(track, false);
            if (GUILayout.Button(new GUIContent("+ 대기 자세 키", "배경에 있던 손 자리"), GUILayout.Width(100))) AddKeyHere(track, true);
            using (new EditorGUI.DisabledScope(!Valid(selTrack, selKey)))
                if (GUILayout.Button("키 삭제", GUILayout.Width(60))) { track.keys.RemoveAt(selKey); selKey = -1; MarkDirty(); GUIUtility.ExitGUI(); }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("여기부터 레이어", EditorStyles.boldLabel, GUILayout.Width(110));
            var bg = GUI.backgroundColor;
            for (int i = 0; i < LayerNames.Length; i++)
            {
                GUI.backgroundColor = LayerColors[i];
                if (GUILayout.Button(LayerNames[i], GUILayout.Width(46))) SetLayerHere(track, (LimbLayer)i);
            }
            GUI.backgroundColor = bg;
            using (new EditorGUI.DisabledScope(!(selLayerMark && selMark >= 0 && selMark < track.layers.Count)))
                if (GUILayout.Button("표시 삭제", GUILayout.Width(66))) { track.layers.RemoveAt(selMark); selMark = -1; MarkDirty(); GUIUtility.ExitGUI(); }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("여기에 이벤트", EditorStyles.boldLabel, GUILayout.Width(110));
            if (GUILayout.Button("✖ 판정", GUILayout.Width(60))) AddEvent(KyodaiMotionEventType.Hit);
            if (GUILayout.Button("▼ 그림자 켜기", GUILayout.Width(90))) AddEvent(KyodaiMotionEventType.TelegraphShow);
            if (GUILayout.Button("▲ 그림자 끄기", GUILayout.Width(90))) AddEvent(KyodaiMotionEventType.TelegraphHide);
            using (new EditorGUI.DisabledScope(selEvent < 0 || selEvent >= motion.events.Count))
                if (GUILayout.Button("이벤트 삭제", GUILayout.Width(80))) { motion.events.RemoveAt(selEvent); selEvent = -1; MarkDirty(); GUIUtility.ExitGUI(); }
        }
    }

    private void AddKeyHere(KyodaiMotionTrack track, bool rest)
    {
        var key = new KyodaiMotionKey { time = time, rest = rest };
        if (!rest) SampleInto(track, key);
        int existing = track.keys.FindIndex(k => Mathf.Abs(k.time - time) < 1e-3f);
        if (existing >= 0) track.keys[existing] = key; else track.keys.Add(key);
        track.Sort();
        selKey = track.keys.IndexOf(key);
        selLayerMark = false; selEvent = -1;
        MarkDirty();
    }

    // 지금 시각에 손이 있는 자리를 키 값으로 (앞뒤 키 사이 보간). 대기 키끼리라면 목표 위 4칸
    private void SampleInto(KyodaiMotionTrack track, KyodaiMotionKey key)
    {
        KyodaiMotionKey a = null, b = null;
        foreach (var k in track.keys)
        {
            if (k.time <= time + 1e-4f) a = k;
            else { b = k; break; }
        }
        KyodaiMotionKey src = a != null && !a.rest ? a : b != null && !b.rest ? b : null;
        if (src == null) return;
        key.forward = src.forward;
        key.offset = src.offset;
        key.handRotation = src.handRotation;
        key.ikWeight = src.ikWeight;
        if (a != null && b != null && !a.rest && !b.rest)
        {
            float u = KyodaiMotionPlayer.Ease01(b.ease, Mathf.InverseLerp(a.time, b.time, time));
            key.offset = Vector2.LerpUnclamped(a.offset, b.offset, u);
            key.forward = Mathf.LerpUnclamped(a.forward, b.forward, u);
            key.handRotation = Vector3.LerpUnclamped(a.handRotation, b.handRotation, u);
        }
    }

    private void SetLayerHere(KyodaiMotionTrack track, LimbLayer layer)
    {
        int existing = track.layers.FindIndex(m => Mathf.Abs(m.time - time) < 1e-3f);
        KyodaiLayerMark mark;
        if (existing >= 0) { mark = track.layers[existing]; mark.layer = layer; }
        else { mark = new KyodaiLayerMark { time = time, layer = layer }; track.layers.Add(mark); }
        track.Sort();
        selLayerMark = true; selMark = track.layers.IndexOf(mark); selKey = -1; selEvent = -1;
        MarkDirty();
    }

    private void AddEvent(KyodaiMotionEventType type)
    {
        var e = new KyodaiMotionEvent { time = time, type = type };
        // 그림자 끄기는 범위가 필요 없고, 켜기는 앞의 판정 너비를 따라간다
        var hit = motion.events.Find(x => x.type == KyodaiMotionEventType.Hit);
        if (type == KyodaiMotionEventType.TelegraphShow && hit != null) { e.center = new Vector2(hit.center.x, 0f); e.size = new Vector2(hit.size.x, 0.5f); }
        motion.events.Add(e);
        motion.SortAll();
        selEvent = motion.events.IndexOf(e); selKey = -1; selMark = -1;
        MarkDirty();
    }

    // ── 선택한 것 세부 값 ──

    private void DrawSelection()
    {
        EditorGUILayout.Space(6);
        if (selTrack < motion.tracks.Count)
        {
            var track = motion.tracks[selTrack];
            EditorGUILayout.LabelField($"트랙: {track.partId}", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                track.partId = EditorGUILayout.TextField("부위 이름", track.partId);
                track.hurtSize = EditorGUILayout.Vector2Field(new GUIContent("잔류 중 맞는 범위", "손 위치 중심, 월드 크기"), track.hurtSize);
            }

            if (Valid(selTrack, selKey))
            {
                var key = track.keys[selKey];
                EditorGUILayout.LabelField($"키 {selKey + 1}", EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    key.time = Mathf.Clamp(EditorGUILayout.FloatField("시각(초)", key.time), 0f, motion.duration);
                    key.rest = EditorGUILayout.Toggle(new GUIContent("대기 자세", "배경에 있던 손 자리"), key.rest);
                    using (new EditorGUI.DisabledScope(key.rest))
                    {
                        key.offset = EditorGUILayout.Vector2Field(new GUIContent("목표에서 손까지", "Scene 뷰에서 화살표로 끌어도 됨"), key.offset);
                        key.forward = EditorGUILayout.FloatField(new GUIContent("앞으로 나온 거리", "경기장 평면에서 카메라 쪽으로(월드). 0 = 플레이어와 같은 평면 — 판정 자리와 보이는 자리가 맞는다"), key.forward);
                    }
                    key.handRotation = EditorGUILayout.Vector3Field("손목 회전(도)", key.handRotation);
                    key.ikWeight = EditorGUILayout.Slider(new GUIContent("IK 세기", "1 = 손이 이 키 자리에 정확히 / 0 = 온몸 애니메이션 그대로(위치 무시)"), key.ikWeight, 0f, 1f);
                    key.ease = (Ease)EditorGUILayout.EnumPopup(new GUIContent("앞 키에서 오는 방식", "InQuad = 점점 빨라짐(내려찍기) / OutCubic = 빨리 왔다가 멈춤 / OutBack = 살짝 지나쳤다 돌아옴"), key.ease);
                }
            }
            else if (selLayerMark && selMark >= 0 && selMark < track.layers.Count)
            {
                var mark = track.layers[selMark];
                EditorGUILayout.LabelField("레이어 표시", EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    mark.time = Mathf.Clamp(EditorGUILayout.FloatField("시각(초)", mark.time), 0f, motion.duration);
                    mark.layer = (LimbLayer)EditorGUILayout.Popup("레이어", (int)mark.layer, LayerNames);
                }
            }
        }

        if (selEvent >= 0 && selEvent < motion.events.Count)
        {
            var e = motion.events[selEvent];
            EditorGUILayout.LabelField("이벤트", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                e.time = Mathf.Clamp(EditorGUILayout.FloatField("시각(초)", e.time), 0f, motion.duration);
                e.type = (KyodaiMotionEventType)EditorGUILayout.Popup("종류", (int)e.type, new[] { "판정", "그림자 켜기", "그림자 끄기" });
                if (e.type != KyodaiMotionEventType.TelegraphHide)
                {
                    e.center = EditorGUILayout.Vector2Field(new GUIContent("중심(목표 기준)", "그림자는 x만 씀"), e.center);
                    e.size = EditorGUILayout.Vector2Field(new GUIContent("크기", "그림자는 x = 너비"), e.size);
                }
            }
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "만드는 순서: ① + 트랙(오른팔) → ② 시간 막대를 옮기고 '+ 지금 자세로 키' → Scene 뷰에서 손을 끌어 자리 잡기 → 반복\n" +
            "③ '여기부터 레이어'로 전이·공격·잔류·복귀 구간 찍기 (잔류일 때만 플레이어가 때릴 수 있음)\n" +
            "④ 쾅 하는 순간에 '✖ 판정', 예고 시작에 '▼ 그림자 켜기', 끝에 '▲ 그림자 끄기'\n" +
            "⑤ 플레이 중 '미리보기 시작'으로 실제 로봇 확인. 패턴에 쓰려면 쿄다이 밑 KyodaiMotionPattern의 Motion 칸에 이 에셋",
            MessageType.None);
    }

    private void CreateMotion()
    {
        string path = EditorUtility.SaveFilePanelInProject("새 쿄다이 모션", "Kyodai_모션", "asset", "모션 에셋 저장 위치", "Assets/NYH/04. SO/Boss/Kyodai");
        if (string.IsNullOrEmpty(path)) return;
        var m = CreateInstance<KyodaiMotionData>();
        m.tracks.Add(new KyodaiMotionTrack { keys = { new KyodaiMotionKey { time = 0f, rest = true } }, layers = { new KyodaiLayerMark { time = 0f, layer = LimbLayer.Background } } });
        AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        motion = m;
        selTrack = 0; selKey = 0;
    }

    private void MarkDirty()
    {
        previewDirty = true;
        if (motion != null) EditorUtility.SetDirty(motion);
        SceneView.RepaintAll();
        Repaint();
    }

    // ── Scene 뷰 ──

    private void OnScene(SceneView sv)
    {
        if (motion == null) return;
        FindBoss();
        EnsureTarget();

        // 목표 (노란 십자) — 끌어서 옮김
        Handles.color = Color.yellow;
        float s = HandleUtility.GetHandleSize(target) * 0.15f;
        Handles.DrawLine(target + Vector2.left * s * 2f, target + Vector2.right * s * 2f);
        Handles.DrawLine(target + Vector2.down * s * 2f, target + Vector2.up * s * 2f);
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.FreeMoveHandle(target, s, Vector3.zero, Handles.RectangleHandleCap);
        if (EditorGUI.EndChangeCheck()) { target = moved; targetSet = true; Repaint(); }
        Handles.Label(target + Vector2.up * s * 3f, "목표");

        // 이벤트 범위
        foreach (var e in motion.events)
        {
            bool now = Mathf.Abs(e.time - time) < 0.08f;
            if (e.type == KyodaiMotionEventType.Hit)
            {
                Vector2 c = target + e.center;
                var rect = new Rect(c - e.size * 0.5f, e.size);
                Handles.DrawSolidRectangleWithOutline(rect, new Color(1f, 0.2f, 0.2f, now ? 0.35f : 0.06f), new Color(1f, 0.3f, 0.3f, now ? 1f : 0.5f));
            }
            else if (e.type == KyodaiMotionEventType.TelegraphShow)
            {
                Vector2 c = new Vector2(target.x + e.center.x, target.y);
                var rect = new Rect(c.x - e.size.x * 0.5f, c.y - 0.25f, e.size.x, 0.5f);
                Handles.DrawSolidRectangleWithOutline(rect, new Color(0f, 0f, 0f, 0.25f), new Color(0f, 0f, 0f, 0.7f));
            }
        }
        if (selEvent >= 0 && selEvent < motion.events.Count && motion.events[selEvent].type != KyodaiMotionEventType.TelegraphHide)
        {
            var e = motion.events[selEvent];
            EditorGUI.BeginChangeCheck();
            Vector3 p = Handles.PositionHandle(target + e.center, Quaternion.identity);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(motion, "이벤트 위치"); e.center = (Vector2)p - target; MarkDirty(); }
        }

        // 트랙 경로와 키
        for (int ti = 0; ti < motion.tracks.Count; ti++)
        {
            var track = motion.tracks[ti];
            KyodaiLimb limb = boss != null ? boss.FindLimb(track.partId) : null;
            Color tc = TrackColors[ti % TrackColors.Length];

            // 대기 손 자리: 플레이 중이면 실제 부위, 아니면 편집 미리보기가 적어 둔 자리
            Vector2? restWorld = Application.isPlaying
                ? (limb != null && limb.Ready ? (Vector2)limb.RestPosition : (Vector2?)null)
                : editPreview.Active ? editPreview.RestWorld(track.partId) : null;

            var pts = new List<Vector3>();
            foreach (var k in track.keys)
                if (!k.rest || restWorld.HasValue) pts.Add(KyodaiMotionPlayer.KeyWorld(k, target, restWorld));
            Handles.color = new Color(tc.r, tc.g, tc.b, ti == selTrack ? 0.9f : 0.35f);
            if (pts.Count > 1) Handles.DrawAAPolyLine(ti == selTrack ? 3f : 2f, pts.ToArray());

            for (int ki = 0; ki < track.keys.Count; ki++)
            {
                var k = track.keys[ki];
                if (k.rest && !restWorld.HasValue) continue;
                Vector3 p = KyodaiMotionPlayer.KeyWorld(k, target, restWorld);
                float hs = HandleUtility.GetHandleSize(p) * 0.08f;
                bool sel = ti == selTrack && ki == selKey && !selLayerMark;
                Handles.color = sel ? Color.yellow : k.rest ? Color.gray : tc;
                if (Handles.Button(p, Quaternion.identity, hs, hs * 1.4f, Handles.DotHandleCap))
                {
                    selTrack = ti; selKey = ki; selLayerMark = false; selEvent = -1;
                    time = k.time;
                    Repaint();
                }
                Handles.Label(p + Vector3.up * hs * 2.5f, $"{ki + 1} ({k.time:0.00}s){(k.rest ? " 대기" : "")}");

                if (sel && !k.rest)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 np = Handles.PositionHandle(p, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(motion, "손 위치"); k.offset = (Vector2)np - target; MarkDirty(); }
                }
            }

            // 지금 손이 보이는 자리
            if (preview && limb != null && limb.Ready)
            {
                Handles.color = Color.white;
                Vector3 hp = limb.WorldPosition;
                Handles.DrawSolidDisc(hp, Vector3.forward, HandleUtility.GetHandleSize(hp) * 0.05f);
            }
        }
    }
}
