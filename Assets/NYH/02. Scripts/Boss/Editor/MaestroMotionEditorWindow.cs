using System.Collections.Generic;
using System.IO;
using RealSteel.Boss.Core;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 마에스트로 모션 편집기 (2026-11-25 사용자 요청 — 매번 말로 설명하고 결과를 확인하는 대신 직접 끌어서 자세·모션을 만든다. 1단계).
/// 메뉴 NYH/보스3/모션 편집기. 플레이 모드에서 마에스트로가 있는 씬을 실행한 뒤 쓴다(나노봇·천 물리가 실제로 돌아야 하므로).
///   · Scene 뷰 손잡이: 몸 이동·몸 기울기·상체·머리·팔꿈치·손(2관절 IK)·손끝/지휘봉 끝을 끌면 지금 시간의 키가 바뀐다(키가 없으면 자동으로 생김)
///   · 타임라인: 키 찍기·옮기기·지우기·복제, 재생(속도·반복), 키마다 곡선(부드럽게/일정/가속/감속/멈춤)
///   · 값 패널: 채널 11개 + 몸 기울기·이동·눌림을 숫자로
///   · 코드 몸짓 가져오기(휘두르기·베기 등 → 모션 에셋) / 몸짓에 연결(패턴이 그 몸짓을 걸면 내 모션이 나옴) / 게임처럼 재생
/// 저장: 모션은 에셋이라 플레이를 꺼도 남는다(`Assets/NYH/04. SO/Boss/Maestro/Motions/`). 연결표 = `04. SO/Boss/Maestro/Resources/Maestro_MotionSet.asset`.
/// 편집하는 동안 보스 자동 패턴은 멈춘다(끝내면 원래대로).
/// </summary>
public class MaestroMotionEditorWindow : EditorWindow
{
    private const string MotionFolder = "Assets/NYH/04. SO/Boss/Maestro/Motions";
    private const string ResourcesFolder = "Assets/NYH/04. SO/Boss/Maestro/Resources";

    private MaestroLook look;
    private MaestroBossController boss;
    private MaestroMotion motion;
    private bool editing, playing, snap = true, still = true, showSkeleton = true, showLabels = true;
    private bool prevPauseAuto;
    private float time, speed = 1f;
    private double lastTick;
    private int selKey = -1;
    private MaestroMotion.Key clipboard;
    private Vector2 scroll;
    private MaestroGesture importGesture = MaestroGesture.SlashLeft, linkGesture = MaestroGesture.SlashLeft;
    private float importSeconds = 1f;

    // 손잡이 끌기 — 씬 GUI 이벤트는 한 프레임에 여러 번 오므로, 그림이 갱신된 뒤 한 번만 적용한다
    private int dragHandle = -1, lastApplyFrame = -1;
    private Vector3 dragWorld;
    private bool hasPendingDrag, snapBeforeDrag;

    // 타임라인 끌기
    private int dragKey = -1, dragEvent = -1;
    private bool scrubbing;

    // 이벤트 (점 · 선)
    private int selEvent = -1;
    private MaestroMotion.MotionEvent eventClipboard;
    private readonly Dictionary<MaestroMotion.MotionEvent, Vector3> anchorCache = new Dictionary<MaestroMotion.MotionEvent, Vector3>();
    private float lastSceneTime = -1f;
    private bool showEvents = true;

    // Scene 뷰에서 자유 배치 — Shift+클릭: 시작 자리 옮김, Ctrl+클릭: 경로 점 추가, Ctrl+끌기: 경로 그리기
    private readonly List<Vector3> stroke = new List<Vector3>();
    private bool stroking;
    private static readonly int PlaceHash = "MaestroEventPlace".GetHashCode();

    // 보스 이동(BossMove) 미리보기 — 편집 시작 때 자세를 기억해 두고 끝날 때 되돌린다
    private bool movePreview, savedPoseBack;
    private Vector2 savedPoseOffset;
    private float savedPoseGrow = 1f;

    private static readonly string[] AnchorNames = { "지휘봉 끝", "지휘 손", "반대 손끝", "머리", "몸 가운데", "발밑 (이동 따라감)", "자유 자리 (무대 고정)" };

    private static readonly Color[] HandleColors =
    {
        new Color(1f, 0.85f, 0.2f), new Color(1f, 0.6f, 0.2f), new Color(0.95f, 0.45f, 0.9f), new Color(0.95f, 0.45f, 0.9f),
        new Color(0.3f, 0.85f, 1f), new Color(0.3f, 0.85f, 1f), new Color(0.6f, 1f, 1f),
        new Color(0.45f, 1f, 0.45f), new Color(0.45f, 1f, 0.45f), new Color(0.75f, 1f, 0.6f),
        new Color(0.15f, 0.55f, 1f), new Color(0.15f, 0.8f, 0.3f),                 // 지휘 어깨 · 반대 어깨
        new Color(1f, 0.95f, 0.5f), new Color(1f, 0.45f, 0.1f), new Color(1f, 0.6f, 0.85f), // 허리 · 하체 자락 · 머리 장식
    };

    [MenuItem("NYH/보스3/모션 편집기")]
    public static void Open() => GetWindow<MaestroMotionEditorWindow>("마에스트로 모션");

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayMode;
        lastTick = EditorApplication.timeSinceStartup;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        StopEdit();
    }

    private void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingPlayMode) StopEdit();
        if (s == PlayModeStateChange.EnteredEditMode || s == PlayModeStateChange.EnteredPlayMode) { look = null; boss = null; }
    }

    // ───────────────────────── 매 프레임 ─────────────────────────

    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Min(0.1f, (float)(now - lastTick));
        lastTick = now;
        if (!editing) return;
        if (!Application.isPlaying || look == null || motion == null) { StopEdit(); Repaint(); return; }

        if (playing && !EditorApplication.isPaused)
        {
            time += dt * speed;
            if (time >= motion.length)
            {
                if (motion.loop) time = Mathf.Repeat(time, motion.length);
                else { time = motion.length; playing = false; }
            }
        }

        look.EditingMotion = motion;
        look.EditStill = still;
        look.EditSnap = dragHandle >= 0 || snap; // 끄는 동안은 항상 정확히 (스프링이 늦게 따라오면 손잡이가 튄다)
        look.EditTime = time;
        TickMovePreview();

        if (hasPendingDrag && Time.frameCount != lastApplyFrame)
        {
            lastApplyFrame = Time.frameCount;
            hasPendingDrag = false;
            var key = KeyForEdit();
            if (key != null)
            {
                Undo.RecordObject(motion, "모션 손잡이");
                if (look.EditDrag((MaestroLook.EditHandle)dragHandle, dragWorld, key)) EditorUtility.SetDirty(motion);
            }
        }

        SceneView.RepaintAll();
        Repaint();
    }

    // ───────────────────────── 편집 시작·끝 ─────────────────────────

    private bool FindLook()
    {
        if (look == null) look = FindFirstObjectByType<MaestroLook>();
        if (look != null && boss == null) boss = look.GetComponentInParent<MaestroBossController>();
        return look != null;
    }

    private void StartEdit()
    {
        if (!Application.isPlaying || !FindLook() || !look.Ready || motion == null) return;
        if (boss != null)
        {
            prevPauseAuto = boss.debugPauseAuto;
            boss.debugPauseAuto = true;
            if (boss.DebugPatterns != null)
                foreach (var p in boss.DebugPatterns) if (p != null) p.InterruptPattern(InterruptReason.Manual);
        }
        look.EditBegin(motion);
        SavePose();
        editing = true;
        time = Mathf.Clamp(time, 0f, motion.length);
        if (selKey >= 0 && selKey < motion.keys.Count) time = motion.keys[selKey].time;
    }

    private void StopEdit()
    {
        if (!editing) return;
        editing = false;
        playing = false;
        dragHandle = -1;
        stroking = false;
        RestorePose();
        if (look != null) look.EditEnd();
        if (boss != null) boss.debugPauseAuto = prevPauseAuto;
    }

    // ── 보스 이동 미리보기 (BossMove 선) ──

    private void SavePose()
    {
        movePreview = false;
        if (boss != null) boss.GetBodyPose(out savedPoseOffset, out savedPoseGrow, out savedPoseBack);
        else if (look != null) look.GetPose(out savedPoseOffset, out savedPoseGrow, out savedPoseBack);
    }

    private void RestorePose()
    {
        if (!movePreview) return;
        movePreview = false;
        if (boss != null) boss.SetBodyPose(savedPoseOffset, savedPoseGrow, savedPoseBack);
        else if (look != null) look.SetPose(savedPoseOffset, savedPoseGrow, savedPoseBack);
        if (look != null) look.SetDepth(-1f);
    }

    // 모션의 보스 이동을 지금 시간까지 쌓아 몸 자세로 건다 (뒤판이면 판정도 꺼짐 — 컨트롤러 SetBodyPose)
    private void TickMovePreview()
    {
        if (!motion.HasMove) { RestorePose(); return; }
        motion.EvaluateMove(time, out var off, out float grow, out float depth);
        bool back = depth >= motion.hideDepth;
        Vector3 w = look.MotionToWorldDelta(off, false);
        Vector2 o = savedPoseOffset + new Vector2(w.x, w.y);
        float g = savedPoseGrow * grow;
        if (boss != null) boss.SetBodyPose(o, g, back);
        else look.SetPose(o, g, back);
        look.SetDepth(depth);
        movePreview = true;
    }

    // 지금 시간의 키 (없으면 지금 자세로 새로 찍음 — 자동 키)
    private MaestroMotion.Key KeyForEdit()
    {
        int k = motion.KeyAt(time);
        if (k < 0)
        {
            Undo.RecordObject(motion, "모션 키 추가");
            var key = motion.Sample(time);
            motion.keys.Add(key);
            motion.SortKeys();
            k = motion.keys.IndexOf(key);
            EditorUtility.SetDirty(motion);
        }
        selKey = k;
        return motion.keys[k];
    }

    // ───────────────────────── 창 ─────────────────────────

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawTop();
        if (motion != null)
        {
            EditorGUILayout.Space(4);
            DrawEditBar();
            EditorGUILayout.Space(4);
            DrawMotionSettings();
            EditorGUILayout.Space(4);
            DrawTimeline();
            EditorGUILayout.Space(4);
            DrawKeyPanel();
            EditorGUILayout.Space(8);
            DrawEvents();
            EditorGUILayout.Space(8);
            DrawLinks();
        }
        else
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("① 먼저 모션을 고르세요 — '새 모션'(빈 모션) 또는 '코드 몸짓 가져오기 → 새 모션으로'(기존 몸짓에서 시작, 추천).\n" +
                                    "② 모션이 생기면 여기에 '● 편집 시작' 버튼과 타임라인이 나타납니다 (플레이 중일 때 누를 수 있음).", MessageType.Warning);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawTop()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드에서 마에스트로가 있는 씬을 실행한 뒤 '편집 시작'을 누르세요.\n" +
                                    "(나노봇·천 물리가 실제로 돌아야 자세를 볼 수 있음. 모션 에셋 만들기·가져오기는 플레이 전에도 됨)", MessageType.Info);
            if (GUILayout.Button("플레이 시작")) EditorApplication.isPlaying = true;
        }
        else if (!FindLook())
            EditorGUILayout.HelpBox("씬에 MaestroLook이 없음 — 마에스트로 보스가 있는 씬인지 확인", MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        var m = (MaestroMotion)EditorGUILayout.ObjectField("모션", motion, typeof(MaestroMotion), false);
        if (EditorGUI.EndChangeCheck()) SetMotion(m);
        if (GUILayout.Button("새 모션", GUILayout.Width(70))) CreateMotion(null);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        importGesture = (MaestroGesture)EditorGUILayout.EnumPopup("코드 몸짓 가져오기", importGesture);
        importSeconds = EditorGUILayout.FloatField(importSeconds, GUILayout.Width(40));
        GUILayout.Label("초", GUILayout.Width(16));
        if (GUILayout.Button("새 모션으로", GUILayout.Width(80))) ImportGesture();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawEditBar()
    {
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = Application.isPlaying && look != null && look.Ready;
        var c = GUI.backgroundColor;
        GUI.backgroundColor = editing ? new Color(1f, 0.55f, 0.5f) : new Color(0.55f, 1f, 0.6f);
        if (GUILayout.Button(editing ? "■ 편집 끝" : "● 편집 시작", GUILayout.Height(26))) { if (editing) StopEdit(); else StartEdit(); }
        GUI.backgroundColor = c;
        if (GUILayout.Button("게임처럼 재생", GUILayout.Height(26), GUILayout.Width(100)))
        {
            // 편집을 끄고 실제 게임 재생 경로(스프링·평소 지휘에서 섞여 들어감)로 한 번
            StopEdit();
            look.PlayMotion(motion);
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        snap = GUILayout.Toggle(snap, new GUIContent("물리 끄기 (정확히)", "켜면 관절 스프링 없이 키 자세 그대로. 끄면 출렁임을 보며 편집(끄는 동안엔 잠깐 정확히)"), "Button");
        still = GUILayout.Toggle(still, new GUIContent("흔들림 끄기", "떠다니기·평소 흔들림·손가락 꼼지락 끔"), "Button");
        showSkeleton = GUILayout.Toggle(showSkeleton, "뼈대", "Button", GUILayout.Width(44));
        showLabels = GUILayout.Toggle(showLabels, "이름", "Button", GUILayout.Width(44));
        showEvents = GUILayout.Toggle(showEvents, new GUIContent("점·선", "이벤트(점·선) 보기"), "Button", GUILayout.Width(44));
        if (GUILayout.Button("Scene 뷰 맞추기", GUILayout.Width(100))) AlignSceneView();
        EditorGUILayout.EndHorizontal();
        if (editing) EditorGUILayout.HelpBox("Scene 뷰의 동그라미를 끄세요. 지금 시간에 키가 없으면 자동으로 생깁니다. 일시정지(⏸) 중에는 그림이 안 바뀝니다.", MessageType.None);
    }

    private void DrawMotionSettings()
    {
        EditorGUI.BeginChangeCheck();
        float len = EditorGUILayout.FloatField("길이 (초)", motion.length);
        bool loop = EditorGUILayout.Toggle(new GUIContent("반복", "마지막 키 → 길이 끝 동안 첫 키로 넘어가며 계속 돈다"), motion.loop);
        bool crisp = EditorGUILayout.Toggle(new GUIContent("절도 있게", "관절 스프링을 단단하게 — 끄면 출렁이며 무게감 있게"), motion.crisp);
        string memo = EditorGUILayout.TextField("메모", motion.memo);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(motion, "모션 설정");
            motion.length = Mathf.Max(0.05f, len); motion.loop = loop; motion.crisp = crisp; motion.memo = memo;
            EditorUtility.SetDirty(motion);
        }
    }

    // ───────────────────────── 타임라인 ─────────────────────────

    private void DrawTimeline()
    {
        EditorGUILayout.LabelField($"타임라인 — {time:0.000}초 / {motion.length:0.00}초   (키 {motion.keys.Count}개)", EditorStyles.boldLabel);
        Rect r = GUILayoutUtility.GetRect(10f, 74f, GUILayout.ExpandWidth(true));
        r.xMin += 8f; r.xMax -= 8f;
        EditorGUI.DrawRect(r, new Color(0.16f, 0.16f, 0.18f));
        float len = Mathf.Max(0.05f, motion.length);
        float X(float t) => r.x + r.width * Mathf.Clamp01(t / len);
        float T(float x) => Mathf.Clamp((x - r.x) / r.width * len, 0f, len);

        // 눈금 0.1초
        for (float t = 0f; t <= len + 1e-4f; t += 0.1f)
        {
            bool major = Mathf.Abs(t - Mathf.Round(t * 2f) / 2f) < 1e-3f;
            EditorGUI.DrawRect(new Rect(X(t), r.yMax - (major ? 12f : 6f), 1f, major ? 12f : 6f), new Color(1f, 1f, 1f, major ? 0.35f : 0.15f));
        }
        // 키
        for (int i = 0; i < motion.keys.Count; i++)
        {
            var k = motion.keys[i];
            var kr = new Rect(X(k.time) - 5f, r.y + 8f, 10f, 18f);
            EditorGUI.DrawRect(kr, i == selKey ? new Color(1f, 0.8f, 0.2f) : new Color(0.75f, 0.75f, 0.8f));
            if (!string.IsNullOrEmpty(k.note)) GUI.Label(new Rect(kr.x, r.y + 26f, 80f, 16f), k.note, EditorStyles.miniLabel);
        }
        // 이벤트 줄 (아래) — 점 = 동그라미 칸, 선 = 시작~끝 막대
        float ey = r.y + 48f;
        EditorGUI.DrawRect(new Rect(r.x, ey - 3f, r.width, 1f), new Color(1f, 1f, 1f, 0.15f));
        GUI.Label(new Rect(r.x + 2f, ey - 2f, 40f, 14f), "점·선", EditorStyles.miniLabel);
        if (motion.events != null)
            for (int i = 0; i < motion.events.Count; i++)
            {
                var ev = motion.events[i];
                if (ev == null) continue;
                Color col = EventColor(ev);
                if (i == selEvent) col = Color.Lerp(col, Color.white, 0.5f);
                if (ev.type == MaestroMotion.EventType.Line)
                    EditorGUI.DrawRect(new Rect(X(ev.time), ey + 4f, Mathf.Max(4f, X(ev.End) - X(ev.time)), 8f), new Color(col.r, col.g, col.b, 0.8f));
                else
                    EditorGUI.DrawRect(new Rect(X(ev.time) - 5f, ey + 2f, 10f, 12f), col);
                if (!string.IsNullOrEmpty(ev.name)) GUI.Label(new Rect(X(ev.time) + 6f, ey + 12f, 90f, 14f), ev.name, EditorStyles.miniLabel);
            }

        // 재생 위치
        EditorGUI.DrawRect(new Rect(X(time) - 1f, r.y, 2f, r.height), new Color(1f, 0.3f, 0.3f));

        var e = Event.current;
        if (e.type == EventType.MouseDown && r.Contains(e.mousePosition) && e.mousePosition.y >= ey - 2f && motion.events != null)
        {
            // 이벤트 줄 클릭 — 선택하고 그 시간으로 (끌면 시간 이동)
            dragEvent = -1;
            for (int i = motion.events.Count - 1; i >= 0 && dragEvent < 0; i--)
            {
                var ev = motion.events[i];
                if (ev == null) continue;
                bool hit = ev.type == MaestroMotion.EventType.Line
                    ? e.mousePosition.x >= X(ev.time) - 5f && e.mousePosition.x <= Mathf.Max(X(ev.time) + 5f, X(ev.End) + 3f)
                    : Mathf.Abs(X(ev.time) - e.mousePosition.x) <= 6f;
                if (hit) dragEvent = i;
            }
            if (dragEvent >= 0)
            {
                selEvent = dragEvent;
                time = motion.events[dragEvent].time;
                playing = false;
                Undo.RecordObject(motion, "이벤트 옮기기");
                e.Use();
            }
        }
        if (e.type == EventType.MouseDrag && dragEvent >= 0 && dragEvent < motion.events.Count)
        {
            var ev = motion.events[dragEvent];
            ev.time = Mathf.Round(T(e.mousePosition.x) * 100f) / 100f;
            time = ev.time;
            anchorCache.Remove(ev);
            EditorUtility.SetDirty(motion);
            e.Use();
        }
        if (e.type == EventType.MouseUp) dragEvent = -1;

        if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
        {
            dragKey = -1;
            for (int i = 0; i < motion.keys.Count; i++)
                if (Mathf.Abs(X(motion.keys[i].time) - e.mousePosition.x) <= 6f) { dragKey = i; break; }
            if (dragKey >= 0) { selKey = dragKey; time = motion.keys[dragKey].time; Undo.RecordObject(motion, "키 옮기기"); }
            else { scrubbing = true; time = T(e.mousePosition.x); selKey = motion.KeyAt(time, len * 0.01f); if (selKey >= 0) time = motion.keys[selKey].time; }
            playing = false;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && (dragKey >= 0 || scrubbing))
        {
            if (dragKey >= 0 && dragKey < motion.keys.Count)
            {
                var k = motion.keys[dragKey];
                k.time = Mathf.Round(T(e.mousePosition.x) * 100f) / 100f; // 0.01초 단위
                motion.SortKeys();
                dragKey = selKey = motion.keys.IndexOf(k);
                time = k.time;
                EditorUtility.SetDirty(motion);
            }
            else time = T(e.mousePosition.x);
            e.Use();
        }
        else if (e.type == EventType.MouseUp) { dragKey = -1; scrubbing = false; }

        time = EditorGUILayout.Slider(time, 0f, len);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("|◀", GUILayout.Width(32))) { time = 0f; playing = false; }
        if (GUILayout.Button("◀ 키", GUILayout.Width(44))) JumpKey(-1);
        if (GUILayout.Button(playing ? "■ 정지" : "▶ 재생", GUILayout.Width(64)))
        {
            playing = !playing;
            if (playing && time >= motion.length - 1e-4f && !motion.loop) time = 0f;
        }
        if (GUILayout.Button("키 ▶", GUILayout.Width(44))) JumpKey(1);
        GUILayout.Label("속도", GUILayout.Width(28));
        speed = EditorGUILayout.Slider(speed, 0.05f, 2f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 키 (지금 자세)")) { int k = motion.KeyAt(time); if (k < 0) KeyForEdit(); else selKey = k; }
        GUI.enabled = SelValid;
        if (GUILayout.Button("키 복제 (+0.1초)"))
        {
            Undo.RecordObject(motion, "키 복제");
            var k = motion.keys[selKey].Clone();
            k.time = Mathf.Min(motion.length, k.time + 0.1f);
            motion.keys.Add(k); motion.SortKeys(); selKey = motion.keys.IndexOf(k); time = k.time;
            EditorUtility.SetDirty(motion);
        }
        if (GUILayout.Button("복사", GUILayout.Width(44))) clipboard = motion.keys[selKey].Clone();
        GUI.enabled = SelValid && clipboard != null;
        if (GUILayout.Button("붙여넣기", GUILayout.Width(60)))
        {
            Undo.RecordObject(motion, "키 붙여넣기");
            var k = clipboard.Clone();
            k.time = motion.keys[selKey].time;
            motion.keys[selKey] = k;
            EditorUtility.SetDirty(motion);
        }
        GUI.enabled = SelValid;
        if (GUILayout.Button("키 삭제", GUILayout.Width(60)))
        {
            Undo.RecordObject(motion, "키 삭제");
            motion.keys.RemoveAt(selKey); selKey = -1;
            EditorUtility.SetDirty(motion);
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    private bool SelValid => motion != null && selKey >= 0 && selKey < motion.keys.Count;

    private void JumpKey(int dir)
    {
        playing = false;
        int best = -1;
        for (int i = 0; i < motion.keys.Count; i++)
        {
            float kt = motion.keys[i].time;
            if (dir > 0 ? kt > time + 1e-4f : kt < time - 1e-4f)
                if (best < 0 || (dir > 0 ? kt < motion.keys[best].time : kt > motion.keys[best].time)) best = i;
        }
        if (best >= 0) { selKey = best; time = motion.keys[best].time; }
    }

    // ───────────────────────── 키 값 ─────────────────────────

    private void DrawKeyPanel()
    {
        if (!SelValid)
        {
            EditorGUILayout.HelpBox("키를 고르면 값을 숫자로 고칠 수 있습니다. (타임라인의 네모를 클릭 / '+ 키')", MessageType.None);
            return;
        }
        var k = motion.keys[selKey];
        k.EnsureSize();
        EditorGUILayout.LabelField($"키 {selKey + 1}", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        float kt = EditorGUILayout.Slider("시간 (초)", k.time, 0f, motion.length);
        var ease = (MaestroMotion.Ease)EditorGUILayout.EnumPopup(new GUIContent("다음 키로 가는 곡선", "Smooth 부드럽게 · Linear 일정 · EaseIn 점점 빠르게(내리치기) · EaseOut 확 갔다 버팀 · Hold 그대로 있다 딱 바뀜"), k.ease);
        string note = EditorGUILayout.TextField("메모", k.note);
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("몸 전체", EditorStyles.miniBoldLabel);
        float lean = EditorGUILayout.Slider(new GUIContent("기울기 (도)", "+ = 앞(그림이 보는 쪽)"), k.lean, -60f, 60f);
        float rx = EditorGUILayout.Slider(new GUIContent("이동 앞뒤 (키 대비)", "+ = 앞"), k.root.x, -0.6f, 0.6f);
        float ry = EditorGUILayout.Slider(new GUIContent("이동 위아래 (키 대비)", "− = 웅크림"), k.root.y, -0.4f, 0.4f);
        float st = EditorGUILayout.Slider(new GUIContent("눌림·늘어남", "1 = 그대로"), k.stretch, 0.6f, 1.4f);
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("어깨 · 허리 · 자락 · 장식 (키 대비, x + = 앞, y + = 위)", EditorStyles.miniBoldLabel);
        Vector2 bsh = EditorGUILayout.Vector2Field("지휘 어깨 들썩임", k.bShoulder);
        Vector2 osh = EditorGUILayout.Vector2Field("반대 어깨 들썩임", k.oShoulder);
        Vector2 hip = EditorGUILayout.Vector2Field(new GUIContent("허리", "상체 전체를 하체 위에서 옮김"), k.hip);
        Vector2 hem = EditorGUILayout.Vector2Field(new GUIContent("하체 자락", "아랫단을 옮김 — 아래로 갈수록 크게"), k.hem);
        float moon = EditorGUILayout.Slider(new GUIContent("머리 장식 (도)", "머리를 따라 돈 뒤 이만큼 더"), k.moon, -90f, 90f);
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("관절", EditorStyles.miniBoldLabel);
        var ch = (float[])k.ch.Clone();
        for (int i = 0; i < MaestroMotion.ChannelCount; i++)
        {
            Vector2 range = MaestroMotion.ChannelRange(i);
            string unit = i == MaestroMotion.ChBGrip || i == MaestroMotion.ChOCurl ? " (0 폄 · 1 쥠)" : " (도)";
            ch[i] = EditorGUILayout.Slider(MaestroMotion.ChannelNames[i] + unit, ch[i], range.x, range.y);
        }
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(motion, "키 값");
            k.ease = ease; k.note = note; k.lean = lean; k.root = new Vector2(rx, ry); k.stretch = st; k.ch = ch;
            k.bShoulder = bsh; k.oShoulder = osh; k.hip = hip; k.hem = hem; k.moon = moon;
            if (!Mathf.Approximately(kt, k.time)) { k.time = kt; motion.SortKeys(); selKey = motion.keys.IndexOf(k); time = kt; }
            EditorUtility.SetDirty(motion);
        }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("기본 자세로 (그림 그대로)"))
        {
            Undo.RecordObject(motion, "기본 자세");
            k.ch = MaestroMotion.NewChannels(); k.lean = 0f; k.root = Vector2.zero; k.stretch = 1f;
            k.bShoulder = k.oShoulder = k.hip = k.hem = Vector2.zero; k.moon = 0f;
            EditorUtility.SetDirty(motion);
        }
        if (GUILayout.Button("이 키 시간으로 이동")) { time = k.time; playing = false; }
        EditorGUILayout.EndHorizontal();
    }

    // ───────────────────────── 이벤트 (점 · 선) ─────────────────────────

    private static Color EventColor(MaestroMotion.MotionEvent e)
    {
        if (e.type == MaestroMotion.EventType.Point)
            switch (e.pointRole)
            {
                case MaestroMotion.PointRole.Spawn: return new Color(0.4f, 0.9f, 1f);
                case MaestroMotion.PointRole.Fire: return new Color(1f, 0.55f, 0.15f);
                case MaestroMotion.PointRole.Hit: return new Color(1f, 0.25f, 0.25f);
                case MaestroMotion.PointRole.Telegraph: return new Color(1f, 0.95f, 0.35f);
                case MaestroMotion.PointRole.Effect: return new Color(0.85f, 0.6f, 1f);
                case MaestroMotion.PointRole.Sound: return new Color(0.5f, 1f, 0.6f);
                default: return new Color(0.8f, 0.8f, 0.8f);
            }
        switch (e.lineRole)
        {
            case MaestroMotion.LineRole.Projectile: return new Color(1f, 0.6f, 0.2f);
            case MaestroMotion.LineRole.Sweep: return new Color(1f, 0.3f, 0.45f);
            case MaestroMotion.LineRole.Telegraph: return new Color(1f, 0.95f, 0.4f);
            case MaestroMotion.LineRole.BossMove: return new Color(0.45f, 0.75f, 1f);
            default: return new Color(0.8f, 0.8f, 0.8f);
        }
    }

    private static string EventLabel(MaestroMotion.MotionEvent e)
    {
        string kind = e.type == MaestroMotion.EventType.Point ? $"점·{RoleName(e.pointRole)}" : $"선·{RoleName(e.lineRole)}";
        return $"{e.time:0.00}초  {kind}  {e.name}";
    }

    private static string RoleName(MaestroMotion.PointRole r) =>
        r switch
        {
            MaestroMotion.PointRole.Spawn => "생성", MaestroMotion.PointRole.Fire => "발사", MaestroMotion.PointRole.Hit => "판정",
            MaestroMotion.PointRole.Telegraph => "예고", MaestroMotion.PointRole.Effect => "이펙트", MaestroMotion.PointRole.Sound => "소리", _ => "메모",
        };

    private static string RoleName(MaestroMotion.LineRole r) =>
        r switch
        {
            MaestroMotion.LineRole.Projectile => "탄 궤적", MaestroMotion.LineRole.Sweep => "휩쓸기", MaestroMotion.LineRole.Telegraph => "예고선",
            MaestroMotion.LineRole.BossMove => "보스 이동", _ => "메모",
        };

    private bool SelEventValid => motion != null && motion.events != null && selEvent >= 0 && selEvent < motion.events.Count && motion.events[selEvent] != null;

    private void AddEvent(MaestroMotion.EventType type)
    {
        Undo.RecordObject(motion, "이벤트 추가");
        if (motion.events == null) motion.events = new List<MaestroMotion.MotionEvent>();
        var e = new MaestroMotion.MotionEvent
        {
            type = type,
            time = Mathf.Round(time * 100f) / 100f,
            name = type == MaestroMotion.EventType.Point ? "점" + (motion.events.Count + 1) : "선" + (motion.events.Count + 1),
            duration = type == MaestroMotion.EventType.Point ? 0.4f : 0.6f,
        };
        motion.events.Add(e);
        selEvent = motion.events.Count - 1;
        EditorUtility.SetDirty(motion);
    }

    private void DrawEvents()
    {
        EditorGUILayout.LabelField("이벤트 — 점(어디서 무엇이 생기나) · 선(어떻게 나가나)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 점 (지금 시간)")) AddEvent(MaestroMotion.EventType.Point);
        if (GUILayout.Button("+ 선 (지금 시간)")) AddEvent(MaestroMotion.EventType.Line);
        GUI.enabled = SelEventValid;
        if (GUILayout.Button("복제", GUILayout.Width(44)))
        {
            Undo.RecordObject(motion, "이벤트 복제");
            var c = motion.events[selEvent].Clone();
            c.time = Mathf.Min(motion.length, c.time + 0.1f);
            c.name += "_복제";
            motion.events.Add(c); selEvent = motion.events.Count - 1;
            EditorUtility.SetDirty(motion);
        }
        if (GUILayout.Button("복사", GUILayout.Width(44))) eventClipboard = motion.events[selEvent].Clone();
        GUI.enabled = eventClipboard != null;
        if (GUILayout.Button("붙여넣기(지금 시간)", GUILayout.Width(120)))
        {
            Undo.RecordObject(motion, "이벤트 붙여넣기");
            var c = eventClipboard.Clone();
            c.time = Mathf.Round(time * 100f) / 100f;
            if (motion.events == null) motion.events = new List<MaestroMotion.MotionEvent>();
            motion.events.Add(c); selEvent = motion.events.Count - 1;
            EditorUtility.SetDirty(motion);
        }
        GUI.enabled = SelEventValid;
        if (GUILayout.Button("삭제", GUILayout.Width(44)))
        {
            Undo.RecordObject(motion, "이벤트 삭제");
            anchorCache.Remove(motion.events[selEvent]);
            motion.events.RemoveAt(selEvent); selEvent = -1;
            EditorUtility.SetDirty(motion);
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (motion.events != null)
            for (int i = 0; i < motion.events.Count; i++)
            {
                var ev = motion.events[i];
                if (ev == null) continue;
                var c = GUI.backgroundColor;
                GUI.backgroundColor = i == selEvent ? Color.Lerp(EventColor(ev), Color.white, 0.3f) : EventColor(ev) * 0.75f;
                if (GUILayout.Button(EventLabel(ev), EditorStyles.miniButton)) { selEvent = i; time = ev.time; playing = false; }
                GUI.backgroundColor = c;
            }

        if (!SelEventValid)
        {
            EditorGUILayout.HelpBox("점: 음표가 '몇 초에, 어느 손에서' 생기는지 · 판정·예고·이펙트·소리 자리\n" +
                                    "선: '어느 방향으로 어떻게' 나가는지 — 경로(곡선), 속도 느낌(가속·탄력·튕김…), 물결\n" +
                                    "Scene 뷰에서 점·경로 점을 끌어 자리를 잡고, 재생하면 움직임이 미리 보입니다.", MessageType.None);
            return;
        }

        var e = motion.events[selEvent];
        if (e.path == null || e.path.Count < 2) e.path = new List<Vector2> { Vector2.zero, new Vector2(0.8f, 0f) };
        EditorGUI.BeginChangeCheck();
        Undo.RecordObject(motion, "이벤트 값");
        EditorGUILayout.Space(2);
        e.name = EditorGUILayout.TextField(new GUIContent("이름", "패턴 코드에서 찾는 이름"), e.name);
        e.time = EditorGUILayout.Slider("시간 (초)", e.time, 0f, motion.length);
        var newAnchor = (MaestroMotion.Anchor)EditorGUILayout.Popup(new GUIContent("기준", "이 시간에 이 자리에서 시작 — 자유 자리는 무대에 고정 (Scene 뷰 Shift+클릭으로 바로 놓기)"), (int)e.anchor, AnchorNames);
        if (newAnchor != e.anchor) ChangeAnchor(e, newAnchor);
        e.offset = EditorGUILayout.Vector2Field(new GUIContent(e.anchor == MaestroMotion.Anchor.Free ? "자리 (키 대비, 원래 발밑에서)" : "기준에서 (키 대비)", "x + = 앞, y + = 위"), e.offset);
        e.duration = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent(e.type == MaestroMotion.EventType.Point ? "남아 있는 시간 (초)" : "끝까지 가는 시간 (초)"), e.duration));
        e.count = Mathf.Max(1, EditorGUILayout.IntField("개수", e.count));

        if (e.type == MaestroMotion.EventType.Point)
        {
            e.pointRole = (MaestroMotion.PointRole)EditorGUILayout.EnumPopup(new GUIContent("역할", "Spawn 생성 · Fire 발사 · Hit 판정 · Telegraph 예고 · Effect 이펙트 · Sound 소리 · Memo 메모"), e.pointRole);
            e.pointFeel = (MaestroMotion.PointFeel)EditorGUILayout.EnumPopup(new GUIContent("등장 느낌", "Appear 그냥 · Pop 톡 튀어나옴(탄력) · Gather 모여듦 · Flash 번쩍 · Pulse 두근두근 · Shake 떨림"), e.pointFeel);
            e.feelStrength = EditorGUILayout.Slider("느낌 세기", e.feelStrength, 0f, 2f);
            e.size = EditorGUILayout.Slider(new GUIContent("크기 (키 대비)", "판정이면 판정 반경"), e.size, 0.005f, 0.4f);
            if (e.count > 1) e.spread = EditorGUILayout.Slider(new GUIContent("퍼짐 반경 (키 대비)", "여러 개가 둘러 놓이는 반경"), e.spread, 0f, 0.6f);
        }
        else
        {
            e.lineRole = (MaestroMotion.LineRole)EditorGUILayout.EnumPopup(new GUIContent("역할", "Projectile 탄 궤적 · Sweep 휩쓸기(지나간 자리 판정) · Telegraph 예고선 · BossMove 보스 이동 · Memo"), e.lineRole);
            e.lineFeel = (MaestroMotion.LineFeel)EditorGUILayout.EnumPopup(new GUIContent("속도 느낌",
                "Constant 일정 · Accelerate 점점 빠르게 · Decelerate 확 나가서 느려짐 · Anticipate 뒤로 당겼다 튀어나감 · Elastic 탄력(지나쳤다 출렁) · Bounce 통통 튕김 · Smooth 부드럽게"), e.lineFeel);
            e.feelStrength = EditorGUILayout.Slider("느낌 세기", e.feelStrength, 0f, 2f);
            e.curved = EditorGUILayout.Toggle(new GUIContent("곡선으로 잇기", "끄면 꺾인 직선"), e.curved);
            e.waveAmp = EditorGUILayout.Slider(new GUIContent("물결 크기 (키 대비)", "경로 옆으로 흔들리며 감"), e.waveAmp, 0f, 0.4f);
            if (e.waveAmp > 0f) e.waveCount = EditorGUILayout.Slider("물결 횟수", e.waveCount, 0f, 10f);
            e.aimAtPlayer = EditorGUILayout.Toggle(new GUIContent("플레이어 쪽으로 돌리기", "경로 모양은 그대로, 첫 점 → 끝 점 방향이 플레이어를 향함"), e.aimAtPlayer);
            e.width = EditorGUILayout.Slider(new GUIContent("두께 (키 대비)", "판정 굵기"), e.width, 0f, 0.4f);
            if (e.count > 1) e.interval = EditorGUILayout.Slider(new GUIContent("연속 간격 (초)", "하나씩 이 선을 타는 간격 (0 = 한꺼번에)"), e.interval, 0f, 1f);
            if (e.lineRole == MaestroMotion.LineRole.BossMove)
            {
                EditorGUILayout.HelpBox("보스 이동: 경로 모양만큼 몸이 옮겨 가고(화면 x·y), 깊이를 바꾸면 z로 배경 쪽에 스르륵 들어갑니다.\n" +
                                        "이동은 이어서 쌓여요 — 뒤로 들어간 뒤 다른 이동 선으로 나오기. 깊이·크기도 경로와 같은 속도 느낌으로 바뀜.", MessageType.None);
                e.changeDepth = EditorGUILayout.ToggleLeft(new GUIContent("깊이(z)·크기 바꾸기", "끄면 지금 깊이·크기 그대로 옆으로만 이동"), e.changeDepth);
                if (e.changeDepth)
                {
                    EditorGUI.indentLevel++;
                    e.moveDepth = EditorGUILayout.Slider(new GUIContent("도착 깊이 (z)", "0 = 앞판(플레이어 줄) · 1 = 뒤판(배경) · 1보다 크면 더 뒤"), e.moveDepth, 0f, 1.5f);
                    e.moveGrow = EditorGUILayout.Slider(new GUIContent("도착 크기", "1 = 그대로 (멀어지면 원근으로 이미 작아 보임)"), e.moveGrow, 0.3f, 1.5f);
                    EditorGUI.indentLevel--;
                }
                motion.hideDepth = EditorGUILayout.Slider(new GUIContent("이 깊이부터 공격 안 맞음", "모션 공통 — 보스가 이 깊이보다 뒤에 있으면 판정이 꺼짐"), motion.hideDepth, 0.05f, 1f);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("뒤로 들어가기 (z)")) MovePreset(e, new Vector2(0f, 0.15f), true, 1f, 1f, MaestroMotion.LineFeel.Anticipate);
                if (GUILayout.Button("앞으로 나오기 (z)")) MovePreset(e, new Vector2(0f, -0.15f), true, 0f, 1f, MaestroMotion.LineFeel.Smooth);
                if (GUILayout.Button("돌진")) MovePreset(e, new Vector2(1.2f, 0f), false, 0f, 1f, MaestroMotion.LineFeel.Accelerate);
                EditorGUILayout.EndHorizontal();
            }
            else
                e.startAtPoint = EditorGUILayout.TextField(new GUIContent("이 점에서 시작", "점 이벤트 이름 — 비우면 기준+offset"), e.startAtPoint);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"경로 점 {e.path.Count}개", GUILayout.Width(80));
            if (GUILayout.Button("+ 경로 점"))
            {
                Vector2 last = e.path.Count > 0 ? e.path[e.path.Count - 1] : Vector2.zero;
                Vector2 dir = e.path.Count > 1 ? (last - e.path[e.path.Count - 2]) : new Vector2(0.4f, 0f);
                e.path.Add(last + (dir.sqrMagnitude > 1e-6f ? dir.normalized * 0.3f : new Vector2(0.3f, 0f)));
            }
            GUI.enabled = e.path.Count > 2;
            if (GUILayout.Button("- 마지막 점")) e.path.RemoveAt(e.path.Count - 1);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.LabelField("메모");
        e.memo = EditorGUILayout.TextArea(e.memo, GUILayout.MinHeight(36));
        if (EditorGUI.EndChangeCheck()) { anchorCache.Remove(e); EditorUtility.SetDirty(motion); }
        if (GUILayout.Button("이 이벤트 시간으로 이동")) { time = e.time; playing = false; }
    }

    // 기준을 바꿔도 화면 속 자리는 그대로 (offset을 새 기준에 맞춰 다시 계산)
    private void ChangeAnchor(MaestroMotion.MotionEvent e, MaestroMotion.Anchor a)
    {
        if (editing && look != null && look.Ready && string.IsNullOrEmpty(e.startAtPoint))
        {
            Vector3 w = EventStart(e);
            e.anchor = a;
            anchorCache.Remove(e);
            e.offset = ToM(e, w - AnchorAt(e));
        }
        else e.anchor = a;
    }

    // 보스 이동 프리셋 — to: 화면 이동(키 대비), depth: 도착 깊이(changeDepth일 때)
    private void MovePreset(MaestroMotion.MotionEvent e, Vector2 to, bool changeDepth, float depth, float grow, MaestroMotion.LineFeel feel)
    {
        e.anchor = MaestroMotion.Anchor.Feet;
        e.offset = Vector2.zero;
        e.path = new List<Vector2> { Vector2.zero, to * 0.5f + new Vector2(0f, Mathf.Abs(to.x) * 0.12f), to };
        e.curved = true;
        e.changeDepth = changeDepth;
        e.moveDepth = depth;
        e.moveGrow = grow;
        e.lineFeel = feel;
        if (string.IsNullOrEmpty(e.name)) e.name = !changeDepth ? "돌진" : depth > 0.5f ? "뒤로들어가기" : "앞으로나오기";
    }

    // 보스 이동 선은 원래 크기 기준 (뒤판에서 작아져도 경로 길이는 그대로)
    private static bool IsMove(MaestroMotion.MotionEvent e) => e.type == MaestroMotion.EventType.Line && e.lineRole == MaestroMotion.LineRole.BossMove;
    private Vector3 ToW(MaestroMotion.MotionEvent e, Vector2 r) => look.MotionToWorldDelta(r, !IsMove(e));
    private Vector2 ToM(MaestroMotion.MotionEvent e, Vector3 d) => look.WorldDeltaToMotion(d, !IsMove(e));

    private Vector3 AnchorAt(MaestroMotion.MotionEvent e)
        => anchorCache.TryGetValue(e, out var b) ? b : look.MotionAnchorWorld(e.anchor);

    // ── Scene 뷰: 이벤트 그리기 ──

    // 이벤트 시작 자리 (월드). 기준 자리는 '그 이벤트 시간의 자세'로 기억해 둔다(재생 위치가 그 시간을 지날 때 잡음) — 없으면 지금 자세
    private Vector3 EventStart(MaestroMotion.MotionEvent e, int depth = 0)
    {
        Vector3 b;
        var from = depth < 3 && !string.IsNullOrEmpty(e.startAtPoint) ? motion.FindEvent(e.startAtPoint) : null;
        if (from != null && from != e) b = EventStart(from, depth + 1);
        else b = AnchorAt(e);
        return b + ToW(e, e.offset);
    }

    // 경로 회전 (플레이어 쪽으로) — 라디안
    private float EventAim(MaestroMotion.MotionEvent e, Vector3 start)
    {
        if (!e.aimAtPlayer || IsMove(e) || e.path == null || e.path.Count < 2) return 0f;
        var player = GameObject.FindWithTag("Player");
        if (player == null) return 0f;
        Vector3 d = ToW(e, e.path[e.path.Count - 1] - e.path[0]);
        Vector3 to = player.transform.position - start;
        if (d.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) return 0f;
        return Mathf.Atan2(to.y, to.x) - Mathf.Atan2(d.y, d.x);
    }

    private Vector3 EventPathWorld(MaestroMotion.MotionEvent e, Vector3 start, float aim, float s)
    {
        Vector3 d = ToW(e, MaestroMotion.PathPoint(e, s));
        float c = Mathf.Cos(aim), sn = Mathf.Sin(aim);
        var p = start + new Vector3(c * d.x - sn * d.y, sn * d.x + c * d.y, 0f);
        if (IsMove(e) && e.changeDepth)
        {
            // 보스 이동: 이 이동 전 깊이 → 도착 깊이로 z도 같이 (선이 배경 쪽으로 들어가는 게 보이게)
            float before = motion.DepthAt(Mathf.Max(0f, e.time - 1e-3f));
            p.z = look.DepthToZ(Mathf.LerpUnclamped(before, e.moveDepth, Mathf.Clamp01(s)));
        }
        return p;
    }

    private void CaptureAnchors()
    {
        if (motion.events == null) return;
        float lo = Mathf.Min(lastSceneTime, time), hi = Mathf.Max(lastSceneTime, time);
        foreach (var e in motion.events)
        {
            if (e == null) continue;
            bool at = Mathf.Abs(time - e.time) <= 1f / 60f;
            bool crossed = lastSceneTime >= 0f && e.time >= lo && e.time <= hi && hi - lo < 0.5f;
            if (at || crossed || !anchorCache.ContainsKey(e)) anchorCache[e] = look.MotionAnchorWorld(e.anchor);
        }
        lastSceneTime = time;
    }

    private void DrawEventsInScene()
    {
        if (motion.events == null) return;
        CaptureAnchors();
        float H = look.MotionToWorldDelta(Vector2.up).y;
        for (int i = 0; i < motion.events.Count; i++)
        {
            var e = motion.events[i];
            if (e == null) continue;
            bool sel = i == selEvent;
            Color col = EventColor(e);
            bool active = time >= e.time - 1e-4f && time <= e.End + 1e-4f;
            float alpha = sel || active ? 1f : 0.35f;
            Vector3 start = EventStart(e);

            if (e.type == MaestroMotion.EventType.Point)
            {
                float u = e.duration > 0f ? (time - e.time) / e.duration : 0f;
                float scale = active ? MaestroMotion.PointScale(e.pointFeel, u, e.feelStrength) : 1f;
                Vector3 jitter = active && e.pointFeel == MaestroMotion.PointFeel.Shake ? new Vector3(Mathf.Sin(time * 70f), Mathf.Cos(time * 83f), 0f) * H * 0.006f * (1f + e.feelStrength) : Vector3.zero;
                float rad = Mathf.Max(0.01f, e.size * H);
                for (int n = 0; n < e.count; n++)
                {
                    Vector3 p = start + jitter;
                    if (e.count > 1 && e.spread > 0f)
                    {
                        float a = n * Mathf.PI * 2f / e.count;
                        p += new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * e.spread * H;
                    }
                    if (e.pointFeel == MaestroMotion.PointFeel.Gather && active)
                        p += (p - start + Vector3.up * 0.0001f).normalized * (1f - Mathf.Clamp01(u / 0.6f)) * H * 0.25f;
                    Handles.color = new Color(col.r, col.g, col.b, alpha * (active ? 0.45f : 0.15f));
                    if (active) Handles.DrawSolidDisc(p, Vector3.forward, rad * scale);
                    Handles.color = new Color(col.r, col.g, col.b, alpha);
                    Handles.DrawWireDisc(p, Vector3.forward, rad * Mathf.Max(0.3f, active ? scale : 1f), sel ? 3f : 1.5f);
                }
                if (e.pointRole == MaestroMotion.PointRole.Hit) { Handles.DrawLine(start + new Vector3(-rad, -rad), start + new Vector3(rad, rad)); Handles.DrawLine(start + new Vector3(-rad, rad), start + new Vector3(rad, -rad)); }
            }
            else
            {
                float aim = EventAim(e, start);
                const int N = 48;
                var pts = new Vector3[N + 1];
                for (int k = 0; k <= N; k++) pts[k] = EventPathWorld(e, start, aim, k / (float)N);
                Handles.color = new Color(col.r, col.g, col.b, alpha * 0.9f);
                float thick = Mathf.Clamp(e.width * 120f, 2f, 10f) * (sel ? 1.4f : 1f);
                if (e.lineRole == MaestroMotion.LineRole.Telegraph || e.lineRole == MaestroMotion.LineRole.Memo)
                    for (int k = 0; k < N; k += 2) Handles.DrawLine(pts[k], pts[k + 1], thick * 0.7f);
                else Handles.DrawAAPolyLine(thick, pts);
                // 화살촉
                Vector3 tip = pts[N], back = pts[N - 2];
                Vector3 dir = (tip - back).normalized, nrm = new Vector3(-dir.y, dir.x, 0f);
                float ah = H * 0.05f;
                Handles.DrawAAConvexPolygon(tip, tip - dir * ah + nrm * ah * 0.5f, tip - dir * ah - nrm * ah * 0.5f);

                // 미리보기: 재생 위치에 따라 선을 타는 탄 (연속 발사면 간격마다)
                for (int n = 0; n < e.count; n++)
                {
                    float t0 = e.time + e.interval * n;
                    if (time < t0 - 1e-4f || time > t0 + e.duration + 0.25f) continue;
                    float u = Mathf.Clamp01((time - t0) / e.duration);
                    float s = MaestroMotion.LineProgress(e.lineFeel, u, e.feelStrength);
                    Vector3 p = EventPathWorld(e, start, aim, s);
                    if (e.lineRole == MaestroMotion.LineRole.Sweep)
                    {
                        // 휩쓸기: 지나간 자리를 굵게 칠함
                        int m = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(s) * N), 1, N);
                        var swept = new Vector3[m + 1];
                        for (int k = 0; k <= m; k++) swept[k] = EventPathWorld(e, start, aim, Mathf.Clamp01(s) * k / m);
                        Handles.color = new Color(col.r, col.g, col.b, 0.55f);
                        Handles.DrawAAPolyLine(Mathf.Max(6f, e.width * 300f), swept);
                    }
                    Handles.color = Color.white;
                    Handles.DrawSolidDisc(p, Vector3.forward, Mathf.Max(0.02f, e.width * H * 0.8f));
                    Handles.color = col;
                    Handles.DrawWireDisc(p, Vector3.forward, Mathf.Max(0.03f, e.width * H * 1.2f), 2f);
                }
            }
            if (showLabels)
            {
                Handles.color = col;
                Handles.Label(start + Vector3.up * H * 0.06f, (sel ? "▶ " : "") + EventLabel(e), EditorStyles.whiteMiniLabel);
            }
        }
        DrawSelectedEventHandles();
        HandleEventPlacement();
        DrawBackBadge();
    }

    // 뒤판에 있는 동안 표시 (공격 안 맞음)
    private void DrawBackBadge()
    {
        if (!movePreview) return;
        float depth = motion.DepthAt(time);
        if (depth <= 0.01f) return;
        var c = look.BodyCenter;
        bool hidden = depth >= motion.hideDepth;
        Handles.color = new Color(0.45f, 0.75f, 1f, 0.9f);
        Handles.Label(c + Vector3.up * look.MotionToWorldDelta(Vector2.up).y * 0.6f,
            $"깊이 {depth:0.00}" + (hidden ? " — 공격 안 맞음" : ""), EditorStyles.whiteBoldLabel);
    }

    // Scene 뷰 자유 배치: Shift+클릭 = 고른 이벤트 시작 자리를 거기로(자유 자리), Ctrl+클릭 = 경로 점 추가, Ctrl+끌기 = 경로를 손으로 그림
    private void HandleEventPlacement()
    {
        if (!SelEventValid) return;
        var e = motion.events[selEvent];
        var ev = Event.current;
        int id = GUIUtility.GetControlID(PlaceHash, FocusType.Passive);
        bool line = e.type == MaestroMotion.EventType.Line;
        Vector3 start = EventStart(e);
        float H = look.MotionToWorldDelta(Vector2.up).y;

        if (ev.type == EventType.Layout && (ev.shift || (ev.control && line)) && !ev.alt) HandleUtility.AddDefaultControl(id);

        switch (ev.type)
        {
            case EventType.MouseDown when ev.button == 0 && !ev.alt && HandleUtility.nearestControl == id:
                if (!PlanePoint(ev.mousePosition, start.z, out var w)) break;
                if (ev.shift)
                {
                    Undo.RecordObject(motion, "이벤트 자리 (클릭)");
                    PlaceStart(e, w);
                    EditorUtility.SetDirty(motion);
                    ev.Use();
                }
                else if (ev.control && line)
                {
                    stroking = true;
                    stroke.Clear(); stroke.Add(w);
                    GUIUtility.hotControl = id;
                    ev.Use();
                }
                break;
            case EventType.MouseDrag when stroking && GUIUtility.hotControl == id:
                if (PlanePoint(ev.mousePosition, start.z, out var wd) && Vector3.Distance(wd, stroke[stroke.Count - 1]) > H * 0.02f) stroke.Add(wd);
                ev.Use();
                break;
            case EventType.MouseUp when stroking && GUIUtility.hotControl == id:
                GUIUtility.hotControl = 0;
                stroking = false;
                Undo.RecordObject(motion, "경로 그리기");
                ApplyStroke(e, start, H);
                EditorUtility.SetDirty(motion);
                stroke.Clear();
                ev.Use();
                break;
        }

        if (stroking && stroke.Count > 1)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.9f);
            Handles.DrawAAPolyLine(4f, stroke.ToArray());
        }
        if (ev.shift || (ev.control && line))
        {
            Handles.BeginGUI();
            var r = new Rect(ev.mousePosition.x + 14f, ev.mousePosition.y + 10f, 230f, 20f);
            GUI.Label(r, ev.shift ? "클릭: 시작 자리를 여기로" : "클릭: 경로 점 추가 · 끌기: 경로 그리기", EditorStyles.helpBox);
            Handles.EndGUI();
            if (ev.type == EventType.MouseMove) SceneView.RepaintAll();
        }
    }

    // 시작 자리를 월드 w로 — 보스 이동은 발밑 기준 그대로(offset만), 나머지는 자유 자리로 바꿔 무대에 고정
    private void PlaceStart(MaestroMotion.MotionEvent e, Vector3 w)
    {
        if (!IsMove(e)) { e.anchor = MaestroMotion.Anchor.Free; e.startAtPoint = ""; }
        anchorCache.Remove(e);
        e.offset = ToM(e, w - AnchorAt(e));
    }

    // 그린 선 → 경로 점 (길이 따라 고르게 최대 8개, 곡선으로 이음). 시작 자리 근처에서 그리기 시작하면 시작 자리는 그대로
    private void ApplyStroke(MaestroMotion.MotionEvent e, Vector3 start, float H)
    {
        if (stroke.Count == 0) return;
        float len = 0f;
        for (int i = 1; i < stroke.Count; i++) len += Vector3.Distance(stroke[i - 1], stroke[i]);
        bool click = stroke.Count < 3 || len < H * 0.05f;

        if (!click && !IsMove(e) && Vector3.Distance(stroke[0], start) > H * 0.08f)
        {
            PlaceStart(e, stroke[0]);
            start = EventStart(e);
        }
        float aim = EventAim(e, start);
        float c = Mathf.Cos(-aim), sn = Mathf.Sin(-aim);
        Vector2 Local(Vector3 w)
        {
            Vector3 d = w - start;
            return ToM(e, new Vector3(c * d.x - sn * d.y, sn * d.x + c * d.y, 0f));
        }

        if (click)
        {
            // 클릭 한 번 = 끝에 경로 점 하나 추가
            e.path.Add(Local(stroke[stroke.Count - 1]));
            return;
        }

        int n = Mathf.Clamp(Mathf.RoundToInt(len / (H * 0.15f)) + 1, 2, 8);
        Vector2 first = Local(stroke[0]);
        var path = new List<Vector2> { Vector2.zero };
        for (int k = 1; k < n; k++)
        {
            float target = len * k / (n - 1), acc = 0f;
            Vector3 p = stroke[stroke.Count - 1];
            for (int i = 1; i < stroke.Count; i++)
            {
                float d = Vector3.Distance(stroke[i - 1], stroke[i]);
                if (acc + d >= target) { p = Vector3.Lerp(stroke[i - 1], stroke[i], d > 1e-6f ? (target - acc) / d : 0f); break; }
                acc += d;
            }
            path.Add(Local(p) - first); // 첫 점은 시작 자리(0,0) 기준
        }
        e.path = path;
        e.curved = n > 2;
    }

    private void DrawSelectedEventHandles()
    {
        if (!SelEventValid) return;
        var e = motion.events[selEvent];
        var ev = Event.current;
        Vector3 start = EventStart(e);
        float H = look.MotionToWorldDelta(Vector2.up).y;
        float size = HandleUtility.GetHandleSize(start) * 0.06f;

        // 시작 자리 (기준에서 offset) — 네모
        Handles.color = Color.white;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        EditorGUI.BeginChangeCheck();
        Handles.FreeMoveHandle(id, start, size, Vector3.zero, Handles.RectangleHandleCap);
        if (EditorGUI.EndChangeCheck() && PlanePoint(ev.mousePosition, start.z, out var w))
        {
            Undo.RecordObject(motion, "이벤트 자리");
            e.offset += ToM(e, w - start);
            EditorUtility.SetDirty(motion);
        }

        if (e.type != MaestroMotion.EventType.Line || e.path == null) return;
        float aim = EventAim(e, start);
        float c = Mathf.Cos(aim), sn = Mathf.Sin(aim);
        for (int k = 1; k < e.path.Count; k++)
        {
            Vector3 d = ToW(e, e.path[k]);
            Vector3 p = start + new Vector3(c * d.x - sn * d.y, sn * d.x + c * d.y, 0f);
            Handles.color = new Color(1f, 1f, 1f, 0.5f);
            Vector3 dPrev = ToW(e, e.path[k - 1]);
            Handles.DrawDottedLine(start + new Vector3(c * dPrev.x - sn * dPrev.y, sn * dPrev.x + c * dPrev.y, 0f), p, 2f);
            Handles.color = EventColor(e);
            int pid = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUI.BeginChangeCheck();
            Handles.FreeMoveHandle(pid, p, size * 0.85f, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck() && PlanePoint(ev.mousePosition, p.z, out var wp))
            {
                Undo.RecordObject(motion, "경로 점");
                Vector3 dw = wp - p;
                Vector3 un = new Vector3(c * dw.x + sn * dw.y, -sn * dw.x + c * dw.y, 0f); // 회전 되돌림
                e.path[k] += ToM(e, un);
                EditorUtility.SetDirty(motion);
            }
            if (showLabels) Handles.Label(p + Vector3.up * size * 1.5f, $"경로 {k}", EditorStyles.whiteMiniLabel);
        }
    }

    // ───────────────────────── 몸짓 연결 · 저장 ─────────────────────────

    private void DrawLinks()
    {
        EditorGUILayout.LabelField("게임에 쓰기", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        linkGesture = (MaestroGesture)EditorGUILayout.EnumPopup(new GUIContent("이 몸짓에 연결", "패턴이 이 몸짓을 걸면 코드 키 대신 이 모션이 나온다"), linkGesture);
        if (GUILayout.Button("연결", GUILayout.Width(50)))
        {
            var set = LoadOrCreateSet();
            Undo.RecordObject(set, "몸짓 연결");
            set.Set(linkGesture, motion);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            if (look != null && look.motionSet == null) look.motionSet = set;
        }
        EditorGUILayout.EndHorizontal();

        var cur = AssetDatabase.LoadAssetAtPath<MaestroMotionSet>(ResourcesFolder + "/" + MaestroMotionSet.ResourcePath + ".asset");
        if (cur != null && cur.links != null)
            foreach (var l in cur.links)
            {
                if (l == null || l.motion == null) continue;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  {l.gesture} → {l.motion.name}");
                if (GUILayout.Button("끊기", GUILayout.Width(44)))
                {
                    Undo.RecordObject(cur, "몸짓 연결 끊기");
                    l.motion = null;
                    EditorUtility.SetDirty(cur);
                    AssetDatabase.SaveAssets();
                }
                EditorGUILayout.EndHorizontal();
            }
        EditorGUILayout.HelpBox("베기는 키 포즈(MaestroPoseSet)가 있으면 키 포즈가 우선 보입니다 — 연결한 모션은 퍼펫 관절 쪽에 적용됩니다.", MessageType.None);

        EditorGUILayout.Space(4);
        if (GUILayout.Button("저장", GUILayout.Height(24))) { EditorUtility.SetDirty(motion); AssetDatabase.SaveAssets(); }
    }

    // ───────────────────────── Scene 뷰 손잡이 ─────────────────────────

    private void OnSceneGUI(SceneView sv)
    {
        if (!editing || look == null || !look.Ready || motion == null) return;
        var e = Event.current;

        if (showSkeleton)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.55f);
            for (int b = 0; b < look.EditBoneCount; b++)
                if (look.EditBone(b, out var p, out var pp)) Handles.DrawLine(pp, p, 2f);
        }

        if (showEvents) DrawEventsInScene();

        int n = MaestroLook.EditHandleNames.Length;
        for (int h = 0; h < n; h++)
        {
            if (!look.EditHandlePos((MaestroLook.EditHandle)h, out var pos, out var anchor)) continue;
            Color col = HandleColors[h % HandleColors.Length];
            Handles.color = new Color(col.r, col.g, col.b, 0.6f);
            Handles.DrawDottedLine(anchor, pos, 3f);
            Handles.color = dragHandle == h ? Color.white : col;
            float size = HandleUtility.GetHandleSize(pos) * (h <= 1 ? 0.09f : 0.07f);
            int id = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUI.BeginChangeCheck();
            Handles.FreeMoveHandle(id, pos, size, Vector3.zero, h == 0 ? Handles.RectangleHandleCap : Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck() && PlanePoint(e.mousePosition, pos.z, out var world))
            {
                if (dragHandle != h) { dragHandle = h; playing = false; }
                dragWorld = world;
                hasPendingDrag = true;
            }
            if (showLabels) Handles.Label(pos + Vector3.up * size * 1.6f, MaestroLook.EditHandleNames[h], EditorStyles.whiteMiniLabel);
        }
        if (e.type == EventType.MouseUp && e.button == 0) dragHandle = -1;
    }

    // 마우스 → 보스 그림 평면(z) 위 점
    private static bool PlanePoint(Vector2 mouse, float z, out Vector3 world)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
        world = Vector3.zero;
        if (Mathf.Abs(ray.direction.z) < 1e-6f) return false;
        float t = (z - ray.origin.z) / ray.direction.z;
        if (t <= 0f) return false;
        world = ray.origin + ray.direction * t;
        return true;
    }

    private void AlignSceneView()
    {
        var sv = SceneView.lastActiveSceneView;
        var cam = Camera.main;
        if (sv == null || cam == null) return;
        sv.in2DMode = false;
        sv.orthographic = cam.orthographic;
        Vector3 focus = look != null ? look.BodyCenter : cam.transform.position + cam.transform.forward * 10f;
        float dist = Vector3.Distance(cam.transform.position, focus);
        sv.LookAt(focus, cam.transform.rotation, Mathf.Max(1f, dist * 0.35f));
    }

    // ───────────────────────── 에셋 ─────────────────────────

    private void SetMotion(MaestroMotion m)
    {
        motion = m;
        selKey = -1;
        time = 0f;
        playing = false;
        if (editing)
        {
            if (m == null) StopEdit();
            else look.EditingMotion = m;
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private MaestroMotion CreateMotion(string defaultName)
    {
        EnsureFolder(MotionFolder);
        string path = EditorUtility.SaveFilePanelInProject("새 모션", defaultName ?? "Maestro_Motion_새모션", "asset", "모션 에셋 이름", MotionFolder);
        if (string.IsNullOrEmpty(path)) return null;
        var m = CreateInstance<MaestroMotion>();
        m.keys.Add(new MaestroMotion.Key { time = 0f, note = "시작" });
        AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        SetMotion(m);
        return m;
    }

    private void ImportGesture()
    {
        var src = look != null ? look : FindFirstObjectByType<MaestroLook>();
        if (src == null)
        {
            EditorUtility.DisplayDialog("모션 편집기", "씬에 MaestroLook이 없어 코드 몸짓의 배율 값을 읽을 수 없습니다. 마에스트로 씬을 연 뒤 다시 해주세요.", "확인");
            return;
        }
        var probe = CreateInstance<MaestroMotion>();
        if (!src.ExportGesture(importGesture, probe, importSeconds))
        {
            DestroyImmediate(probe);
            EditorUtility.DisplayDialog("모션 편집기", $"'{importGesture}'는 박자로 도는 몸짓이라 키로 꺼낼 수 없습니다.\n(가져올 수 있음: Swing, RaiseBoth, Spread, Strike, SlashLeft, SlashRight)", "확인");
            return;
        }
        DestroyImmediate(probe);
        var m = CreateMotion("Maestro_Motion_" + importGesture);
        if (m == null) return;
        src.ExportGesture(importGesture, m, importSeconds);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        SetMotion(m);
    }

    private static MaestroMotionSet LoadOrCreateSet()
    {
        string path = ResourcesFolder + "/" + MaestroMotionSet.ResourcePath + ".asset";
        var set = AssetDatabase.LoadAssetAtPath<MaestroMotionSet>(path);
        if (set != null) return set;
        EnsureFolder(ResourcesFolder);
        set = CreateInstance<MaestroMotionSet>();
        AssetDatabase.CreateAsset(set, path);
        AssetDatabase.SaveAssets();
        return set;
    }
}
