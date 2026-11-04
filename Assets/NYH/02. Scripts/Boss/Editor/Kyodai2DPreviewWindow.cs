using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 쿄다이 2D 패턴 미리보기 (2026-11-04, 메뉴 NYH/쿄다이/2D 패턴 미리보기).
/// 플레이하지 않고 씬의 2D 쿄다이 손으로 동작 에셋(KyodaiHandMotion)을 재생해 본다.
///
///   · 매 갱신마다 0프레임부터 지금 프레임까지 다시 계산한다(손 틱 + 그림 갱신, 1/60초 고정) — 결과가 늘 같아서
///     프레임을 뒤로 넘길 수 있고, 동작·느낌 에셋을 고치면 바로 반영된다
///   · 가상 플레이어 x를 슬라이더로 옮겨 "플레이어가 여기 있으면" 어떻게 움직이는지 본다. 판정 상자와 겹치면 첫 접촉 프레임을 알려준다
///   · 미리보기를 끄면(■·창 닫기·플레이 시작·씬 저장·스크립트 다시 컴파일) 손·그림자·예고·맞는 자리를 원래대로 돌린다
/// 화면 흔들림·먼지 생성은 미리보기에서 빠진다(KyodaiHand.Impact).
/// </summary>
public class Kyodai2DPreviewWindow : EditorWindow
{
    private const float Tick = 1f / 60f;
    private static readonly float[] Speeds = { 1f, 0.5f, 0.25f, 0.1f };
    private static readonly string[] SpeedNames = { "1배", "0.5배", "0.25배", "0.1배" };

    private KyodaiBossController boss;
    private KyodaiHandMotion motion;
    private KyodaiHandSide side = KyodaiHandSide.Right;
    private bool useScenePlayer;
    private float playerX;
    private int tailFrames = 60;

    private bool previewing, playing, loop = true;
    private int speedIndex;
    private int frame;
    private float frameAccum;
    private double lastTime;

    // 마지막 계산 결과
    private int firstHitFrame = -1;
    private bool hitNow;
    private Rect hitBox, playerRect;
    private bool hasHitBox;

    private readonly List<HandSnapshot> snapshots = new List<HandSnapshot>();
    private TelegraphSnapshot telegraphSnapshot;
    private Vector2 scroll;

    [MenuItem("NYH/쿄다이/2D 패턴 미리보기", false, Kyodai2DBuilder.MenuPriority + 1)]
    private static void Open() => GetWindow<Kyodai2DPreviewWindow>("쿄다이 2D 미리보기");

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
        if (boss == null) boss = FindAnyObjectByType<KyodaiBossController>();
    }

    private void OnDisable()
    {
        StopPreview();
        EditorApplication.update -= OnEditorUpdate;
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
    }

    private void OnPlayModeChanged(PlayModeStateChange change) => StopPreview();
    private void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) => StopPreview();

    // ───────────────────────── 화면 ─────────────────────────

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 중에는 미리보기를 쓸 수 없습니다.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        // ── 대상 ──
        EditorGUI.BeginChangeCheck();
        var newBoss = (KyodaiBossController)EditorGUILayout.ObjectField("2D 쿄다이", boss, typeof(KyodaiBossController), true);
        if (EditorGUI.EndChangeCheck() && newBoss != boss)
        {
            StopPreview();
            boss = newBoss;
        }
        if (boss == null)
        {
            EditorGUILayout.HelpBox("씬에 2D 쿄다이가 없습니다.", MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("씬에서 찾기")) boss = FindAnyObjectByType<KyodaiBossController>();
                if (GUILayout.Button("2D 쿄다이 만들기"))
                {
                    Kyodai2DBuilder.BuildOrUpdate();
                    boss = FindAnyObjectByType<KyodaiBossController>();
                }
            }
            EditorGUILayout.EndScrollView();
            return;
        }

        // ── 패턴 고르기 ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("패턴", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (var pattern in boss.GetComponentsInChildren<KyodaiHandPattern>(true))
            {
                if (pattern.Motion == null) continue;
                bool selected = pattern.Motion == motion;
                if (GUILayout.Toggle(selected, pattern.name, EditorStyles.miniButton) && !selected) SelectPattern(pattern);
            }
        }
        motion = (KyodaiHandMotion)EditorGUILayout.ObjectField("손 동작", motion, typeof(KyodaiHandMotion), false);
        side = (KyodaiHandSide)EditorGUILayout.EnumPopup("손", side);

        // ── 가상 플레이어 ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("플레이어", EditorStyles.boldLabel);
        useScenePlayer = EditorGUILayout.Toggle("씬 플레이어 위치 사용", useScenePlayer);
        using (new EditorGUI.DisabledScope(useScenePlayer))
            playerX = EditorGUILayout.Slider("가상 플레이어 x", playerX, boss.ArenaMinX - 1f, boss.ArenaMaxX + 1f);

        // ── 재생 ──
        EditorGUILayout.Space();
        int total = TotalFrames();
        using (new EditorGUI.DisabledScope(motion == null))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "❚❚ 일시정지" : "▶ 재생", GUILayout.Height(24)))
                {
                    if (!previewing) StartPreview();
                    if (frame >= total) frame = 0;
                    playing = !playing;
                    lastTime = EditorApplication.timeSinceStartup;
                }
                if (GUILayout.Button("◀ 1f", GUILayout.Width(50), GUILayout.Height(24))) Step(-1);
                if (GUILayout.Button("1f ▶", GUILayout.Width(50), GUILayout.Height(24))) Step(+1);
                using (new EditorGUI.DisabledScope(!previewing))
                    if (GUILayout.Button("■ 끄기", GUILayout.Width(60), GUILayout.Height(24))) StopPreview();
            }

            EditorGUI.BeginChangeCheck();
            int scrub = EditorGUILayout.IntSlider("프레임", frame, 0, total);
            if (EditorGUI.EndChangeCheck())
            {
                if (!previewing) StartPreview();
                playing = false;
                frame = scrub;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                speedIndex = EditorGUILayout.Popup("속도", speedIndex, SpeedNames);
                loop = GUILayout.Toggle(loop, "반복", EditorStyles.miniButton, GUILayout.Width(50));
            }
            tailFrames = EditorGUILayout.IntSlider(new GUIContent("끝난 뒤 더 보기(f)", "동작이 끝나고 손이 대기 자리로 돌아가는 모습까지 볼 프레임"), tailFrames, 0, 180);
        }

        if (motion != null)
        {
            DrawTimeline();
            DrawInfo(total);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Scene 뷰에서 보세요. 미리보기 중엔 손 위치가 바뀌고, 끄면 원래대로 돌아갑니다. " +
                                "동작·느낌 에셋을 고치면 바로 반영됩니다. 화면 흔들림·먼지는 플레이에서만 나옵니다.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void SelectPattern(KyodaiHandPattern pattern)
    {
        motion = pattern.Motion;
        var choice = (KyodaiHandChoice)new SerializedObject(pattern).FindProperty("handChoice").enumValueIndex;
        float px = CurrentPlayerX();
        switch (choice)
        {
            case KyodaiHandChoice.Left: side = KyodaiHandSide.Left; break;
            case KyodaiHandChoice.Right: side = KyodaiHandSide.Right; break;
            case KyodaiHandChoice.NearestToPlayer: side = px < boss.ArenaCenterX ? KyodaiHandSide.Left : KyodaiHandSide.Right; break;
            case KyodaiHandChoice.FarthestFromPlayer: side = px < boss.ArenaCenterX ? KyodaiHandSide.Right : KyodaiHandSide.Left; break;
            // Alternate: 지금 고른 손 그대로
        }
        frame = 0;
        if (previewing) playing = true;
    }

    // 키마다 길이만큼 칸을 나눈 띠. 빨강 = 판정, 노랑 = 예고 그림자, 흰 선 = 끝 충격. 세로선 = 지금 위치
    private void DrawTimeline()
    {
        EditorGUILayout.Space();
        Rect bar = GUILayoutUtility.GetRect(10f, 22f, GUILayout.ExpandWidth(true));
        int sum = Mathf.Max(1, motion.TotalFrames);
        float x = bar.x;
        int keyStart = 0, cursorFrames = -1;
        var hand = FindHand();
        for (int i = 0; i < motion.keys.Count; i++)
        {
            var key = motion.keys[i];
            float w = bar.width * key.frames / sum;
            var r = new Rect(x, bar.y, w, bar.height);
            Color c = key.hitActive ? new Color(0.85f, 0.25f, 0.25f) : (key.telegraphFullArena || key.telegraphWidth > 0f) ? new Color(0.8f, 0.7f, 0.25f) : new Color(0.4f, 0.42f, 0.48f);
            EditorGUI.DrawRect(r, c);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), Color.black);
            if (key.impactOnEnd) EditorGUI.DrawRect(new Rect(r.xMax - 3f, r.y, 2f, r.height), Color.white);
            if (w > 40f) GUI.Label(r, key.label, EditorStyles.miniLabel);
            if (previewing && hand != null && hand.PlayingMotion == motion && hand.KeyIndex == i) cursorFrames = keyStart + hand.KeyFrame;
            keyStart += key.frames;
            x += w;
        }
        if (previewing && hand != null && hand.PlayingMotion == null && frame > 0) cursorFrames = sum;
        if (cursorFrames >= 0)
            EditorGUI.DrawRect(new Rect(bar.x + bar.width * cursorFrames / sum - 1f, bar.y - 2f, 2f, bar.height + 4f), Color.cyan);
    }

    private void DrawInfo(int total)
    {
        var hand = FindHand();
        EditorGUILayout.LabelField("길이", $"{motion.TotalFrames}f ({motion.TotalFrames / 60f:0.00}초) + 충격 멈춤 · 지금 {frame}f ({frame / 60f:0.00}초)");
        if (!previewing || hand == null) return;

        if (hand.PlayingMotion == motion && hand.KeyIndex < motion.keys.Count)
        {
            var key = motion.keys[hand.KeyIndex];
            string stop = hand.InHitStop ? " · 충격 멈춤 중" : "";
            EditorGUILayout.LabelField("키", $"{hand.KeyIndex + 1}/{motion.keys.Count} '{key.label}' {hand.KeyFrame}/{key.frames}f{stop}");
        }
        else
        {
            EditorGUILayout.LabelField("키", "동작 끝 — 대기 자리로 돌아가는 중");
        }

        string hit = firstHitFrame >= 0 ? $"{firstHitFrame}f ({firstHitFrame / 60f:0.00}초)에 처음 닿음" : "닿지 않음";
        var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = hitNow ? new Color(1f, 0.4f, 0.4f) : EditorStyles.label.normal.textColor } };
        EditorGUILayout.LabelField("플레이어 판정", hitNow ? $"지금 닿는 중 · {hit}" : hit, style);
    }

    // ───────────────────────── 재생 ─────────────────────────

    private int TotalFrames()
    {
        if (motion == null) return 0;
        int total = motion.TotalFrames + tailFrames;
        foreach (var key in motion.keys) if (key.impactOnEnd) total += key.impactFreezeFrames;
        return total;
    }

    private void Step(int delta)
    {
        if (!previewing) StartPreview();
        playing = false;
        frame = Mathf.Clamp(frame + delta, 0, TotalFrames());
    }

    private void OnEditorUpdate()
    {
        if (!previewing) return;
        if (boss == null || motion == null || Application.isPlaying)
        {
            StopPreview();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - lastTime);
        lastTime = now;
        if (playing)
        {
            frameAccum += Mathf.Min(dt, 0.1f) * 60f * Speeds[speedIndex];
            int total = TotalFrames();
            while (frameAccum >= 1f)
            {
                frameAccum -= 1f;
                frame++;
                if (frame <= total) continue;
                if (loop) frame = 0;
                else { frame = total; playing = false; }
            }
        }

        Simulate(frame);
        SceneView.RepaintAll();
        Repaint();
    }

    // 0프레임부터 다시 — 모든 손을 대기 자리로 되돌리고 고른 손만 동작을 재생
    private void Simulate(int upTo)
    {
        boss.PlayerXOverride = useScenePlayer ? (float?)null : playerX;
        var hands = boss.GetComponentsInChildren<KyodaiHand>(true);
        foreach (var h in hands) h.Init(boss);

        var telegraph = boss.GetComponentInChildren<GroundTelegraph>(true);
        if (telegraph != null) telegraph.Hide();

        KyodaiHand hand = FindHand();
        if (hand == null) return;
        hand.Play(motion, null, telegraph);

        playerRect = PlayerRect();
        firstHitFrame = -1;
        hitNow = hasHitBox = false;
        foreach (var h in hands) h.UpdateVisual(Tick, 1f, 0f);
        for (int f = 1; f <= upTo; f++)
        {
            foreach (var h in hands) h.Tick();
            foreach (var h in hands) h.UpdateVisual(Tick, 1f, f * Tick);
            hasHitBox = hand.TryGetHitBox(out hitBox);
            hitNow = hasHitBox && hitBox.Overlaps(playerRect);
            if (hitNow && firstHitFrame < 0) firstHitFrame = f;
        }
    }

    private KyodaiHand FindHand()
    {
        if (boss == null) return null;
        foreach (var h in boss.GetComponentsInChildren<KyodaiHand>(true)) if (h.Side == side) return h;
        return null;
    }

    private float CurrentPlayerX()
    {
        if (!useScenePlayer) return playerX;
        var p = GameObject.FindWithTag("Player");
        return p != null ? p.transform.position.x : boss.ArenaCenterX;
    }

    // 플레이어 콜라이더 크기(없으면 1×2)를 가상 x·바닥에 세운 상자
    private Rect PlayerRect()
    {
        var p = GameObject.FindWithTag("Player");
        var col = p != null ? p.GetComponent<Collider2D>() : null;
        Vector2 size = col != null ? (Vector2)col.bounds.size : new Vector2(1f, 2f);
        if (size.x < 0.05f || size.y < 0.05f) size = new Vector2(1f, 2f);
        float x = CurrentPlayerX();
        float y = col != null && useScenePlayer ? col.bounds.min.y : boss.GroundY;
        return new Rect(x - size.x * 0.5f, y, size.x, size.y);
    }

    // ───────────────────────── 원래대로 돌리기 ─────────────────────────

    private class HandSnapshot
    {
        public KyodaiHand hand;
        public Vector3 rootPos, visualPos, visualScale, shadowPos, shadowScale;
        public Quaternion visualRot, shadowRot;
        public Sprite sprite;
        public Color color, shadowColor;
        public Vector2 boxSize, boxOffset;
    }

    private class TelegraphSnapshot
    {
        public SpriteRenderer renderer;
        public bool enabled;
        public Sprite sprite;
        public Color color;
        public Vector3 pos, scale;
    }

    private void StartPreview()
    {
        if (previewing || boss == null) return;
        if (playerX == 0f && !useScenePlayer) playerX = CurrentPlayerXDefault();
        snapshots.Clear();
        foreach (var h in boss.GetComponentsInChildren<KyodaiHand>(true))
        {
            var s = new HandSnapshot { hand = h, rootPos = h.transform.position };
            if (h.Visual != null) { s.visualPos = h.Visual.localPosition; s.visualRot = h.Visual.localRotation; s.visualScale = h.Visual.localScale; }
            if (h.Renderer != null) { s.sprite = h.Renderer.sprite; s.color = h.Renderer.color; }
            if (h.Shadow != null)
            {
                s.shadowPos = h.Shadow.transform.position; s.shadowRot = h.Shadow.transform.rotation;
                s.shadowScale = h.Shadow.transform.localScale; s.shadowColor = h.Shadow.color;
            }
            if (h.HurtZone != null) { s.boxSize = h.HurtZone.Box.size; s.boxOffset = h.HurtZone.Box.offset; }
            snapshots.Add(s);
        }
        var telegraph = boss.GetComponentInChildren<GroundTelegraph>(true);
        var tr = telegraph != null ? telegraph.GetComponent<SpriteRenderer>() : null;
        telegraphSnapshot = tr == null ? null : new TelegraphSnapshot
        {
            renderer = tr, enabled = tr.enabled, sprite = tr.sprite, color = tr.color,
            pos = tr.transform.position, scale = tr.transform.localScale
        };
        previewing = true;
        lastTime = EditorApplication.timeSinceStartup;
    }

    private float CurrentPlayerXDefault()
    {
        var p = GameObject.FindWithTag("Player");
        return p != null ? Mathf.Clamp(p.transform.position.x, boss.ArenaMinX, boss.ArenaMaxX) : boss.ArenaCenterX;
    }

    private void StopPreview()
    {
        if (!previewing) return;
        previewing = playing = false;
        frameAccum = 0f;
        foreach (var s in snapshots)
        {
            if (s.hand == null) continue;
            s.hand.Stop();
            s.hand.transform.position = s.rootPos;
            if (s.hand.Visual != null) { s.hand.Visual.localPosition = s.visualPos; s.hand.Visual.localRotation = s.visualRot; s.hand.Visual.localScale = s.visualScale; }
            if (s.hand.Renderer != null) { s.hand.Renderer.sprite = s.sprite; s.hand.Renderer.color = s.color; }
            if (s.hand.Shadow != null)
            {
                s.hand.Shadow.transform.position = s.shadowPos; s.hand.Shadow.transform.rotation = s.shadowRot;
                s.hand.Shadow.transform.localScale = s.shadowScale; s.hand.Shadow.color = s.shadowColor;
            }
            if (s.hand.HurtZone != null) { s.hand.HurtZone.Box.size = s.boxSize; s.hand.HurtZone.Box.offset = s.boxOffset; }
        }
        snapshots.Clear();
        if (telegraphSnapshot != null && telegraphSnapshot.renderer != null)
        {
            var t = telegraphSnapshot;
            t.renderer.enabled = t.enabled; t.renderer.sprite = t.sprite; t.renderer.color = t.color;
            t.renderer.transform.position = t.pos; t.renderer.transform.localScale = t.scale;
        }
        telegraphSnapshot = null;
        if (boss != null) boss.PlayerXOverride = null;
        SceneView.RepaintAll();
        Repaint();
    }

    // ───────────────────────── Scene 뷰 표시 ─────────────────────────

    private void OnSceneGUI(SceneView view)
    {
        if (!previewing || boss == null) return;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        // 경기장 바닥·끝
        Handles.color = new Color(1f, 0.9f, 0.2f, 0.8f);
        Handles.DrawLine(new Vector3(boss.ArenaMinX, boss.GroundY), new Vector3(boss.ArenaMaxX, boss.GroundY));
        Handles.DrawLine(new Vector3(boss.ArenaMinX, boss.GroundY), new Vector3(boss.ArenaMinX, boss.GroundY + 2f));
        Handles.DrawLine(new Vector3(boss.ArenaMaxX, boss.GroundY), new Vector3(boss.ArenaMaxX, boss.GroundY + 2f));

        // 가상 플레이어 — 닿는 중이면 빨강
        Color pc = hitNow ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 1f, 0.5f);
        Handles.DrawSolidRectangleWithOutline(ToVerts(playerRect), new Color(pc.r, pc.g, pc.b, 0.15f), pc);
        Handles.Label(new Vector3(playerRect.center.x, playerRect.yMax + 0.3f), "플레이어");

        // 판정 상자
        if (hasHitBox) Handles.DrawSolidRectangleWithOutline(ToVerts(hitBox), new Color(1f, 0f, 0f, 0.2f), Color.red);

        // 손 목표 자리
        var hand = FindHand();
        if (hand != null && hand.PlayingMotion != null)
        {
            Handles.color = Color.cyan;
            Vector3 t = hand.Target;
            Handles.DrawLine(t + Vector3.left * 0.3f, t + Vector3.right * 0.3f);
            Handles.DrawLine(t + Vector3.down * 0.3f, t + Vector3.up * 0.3f);
        }
    }

    private static Vector3[] ToVerts(Rect r) => new[]
    {
        new Vector3(r.xMin, r.yMin), new Vector3(r.xMax, r.yMin), new Vector3(r.xMax, r.yMax), new Vector3(r.xMin, r.yMax)
    };
}
