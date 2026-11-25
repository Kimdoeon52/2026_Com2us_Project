#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using RealSteel.Boss.Core;
using RealSteel.NanoBoss;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 마에스트로 디버그 패널 (2026-11-25 사용자 요청 — 실행 중 패턴을 골라 쓰고, 상태를 보고, 나노봇·외형·패턴 값을 실시간으로 세세하게 조절).
/// 게임 화면 안 IMGUI 창. F8로 열고 닫기. 에디터·개발 빌드에서만 들어간다.
///   상태    악장·HP·FSM 상태·마지막 패턴·무적·남는 봇·몸짓
///   패턴    패턴마다 '지금 실행'(진행 중이던 패턴을 끊고 바로), 자동 패턴 멈춤, 악장 연출 1/2/3, 이번 악장 끝내기
///   몸짓    몸짓 재생(길이 조절), 튕기기·피격·지휘봉 생성·무너졌다 다시 쌓기
///   외형    MaestroLook 값 / 뼈·천 = MaestroPuppetData / 패턴 수치 = 패턴의 데이터 에셋 / 나노봇 = 군집 + 재질
///   시간    느리게 보기, 보스 판정 상자 보기
/// 저장: 에셋(패턴 수치·뼈·천·재질) 값은 플레이를 꺼도 남는다. 컴포넌트(외형·군집) 값은 플레이를 끄면 돌아가므로 '복사' →
///       플레이 끈 뒤 메뉴 NYH/보스3/디버그: 복사한 외형·군집 값 붙여넣기.
/// </summary>
public class MaestroDebugPanel : MonoBehaviour
{
    [Tooltip("시작할 때 열어 둘지")]
    public bool openOnStart;

    private static readonly string[] Tabs = { "상태·패턴", "몸짓", "외형", "뼈·천", "패턴 수치", "나노봇", "시간" };

    private MaestroBossController boss;
    private bool open;
    private int tab;
    private Rect window = new Rect(12, 12, 470, 640);
    private Vector2 scroll;
    private float gestureSeconds = 1.2f;
    private float timeScale = 1f;
    private bool showHurt;
    private int patternDataIndex;
    private string message = "";
    private GUIStyle small, title;

    private void Start() => open = openOnStart;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f8Key.wasPressedThisFrame) open = !open;
        if (boss == null) boss = FindAnyObjectByType<MaestroBossController>();
    }

    private void OnGUI()
    {
        if (showHurt) DrawHurtBox();
        if (!open) { GUI.Label(new Rect(8, Screen.height - 24, 300, 22), "F8: 마에스트로 디버그"); return; }
        if (small == null)
        {
            small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            title = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        }
        window.height = Mathf.Min(window.height, Screen.height - 24);
        window = GUI.Window(GetInstanceID(), window, DrawWindow, "마에스트로 디버그 (F8)");
    }

    private void DrawWindow(int id)
    {
        tab = GUILayout.SelectionGrid(tab, Tabs, 4);
        if (boss == null) { GUILayout.Label("씬에 MaestroBossController가 없음"); GUI.DragWindow(); return; }
        scroll = GUILayout.BeginScrollView(scroll);
        switch (tab)
        {
            case 0: StatusTab(); break;
            case 1: GestureTab(); break;
            case 2: LookTab(); break;
            case 3: PuppetTab(); break;
            case 4: PatternDataTab(); break;
            case 5: SwarmTab(); break;
            case 6: TimeTab(); break;
        }
        GUILayout.EndScrollView();
        if (!string.IsNullOrEmpty(GUI.tooltip)) GUILayout.Label(GUI.tooltip, small);
        if (!string.IsNullOrEmpty(message)) GUILayout.Label(message, small);
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    // ───────────────────────── 탭 ─────────────────────────

    private void StatusTab()
    {
        var s = boss.Snapshot;
        var look = boss.Look;
        GUILayout.Label("상태", title);
        GUILayout.Label($"난이도 {boss.SelectedDifficulty} · 악장 {boss.Movement}/{boss.MovementCount}{(boss.InTransition ? " (전환 중)" : "")}");
        if (s != null) GUILayout.Label($"HP {s.currentHp}/{s.maxHp} · 무적 {s.isInvincible} · 그로기 {s.isGroggy}");
        GUILayout.Label($"FSM: {boss.StateMachine?.CurrentState?.StateName ?? "-"}");
        GUILayout.Label($"마지막 패턴: {(boss.LastPattern != null ? boss.LastPattern.GimmickName : "-")}");
        if (look != null && look.Ready) GUILayout.Label($"몸짓 {look.CurrentGesture} · 연출 악장 {look.CurrentMovement} · 남는 봇 {boss.BodyBotCount}/{look.ReserveCount} · 키 {look.Height:0.00}m");
        if (boss.Swarm != null) GUILayout.Label($"군집 봇 {boss.Swarm.Count} · 시뮬 {boss.Swarm.SimMs:0.00}ms");

        GUILayout.Space(6);
        GUILayout.Label("패턴 — 누르면 진행 중이던 패턴을 끊고 바로 실행", title);
        foreach (var p in boss.DebugPatterns)
        {
            if (p == null) continue;
            string gate = p is IMaestroMovementGate g ? $" (악장 {g.MinMovement}~)" : "";
            if (GUILayout.Button($"▶ {p.GimmickName}{gate}")) { boss.DebugRunPattern(p); message = $"{p.GimmickName} 실행"; }
        }
        GUILayout.Label("꺼 둔 패턴(오브젝트 비활성)은 목록에 없음 — 켜고 다시 플레이", small);
        boss.debugPauseAuto = GUILayout.Toggle(boss.debugPauseAuto, " 자동 패턴 멈춤 (몸짓·연출만 볼 때)");

        GUILayout.Space(6);
        GUILayout.Label("악장", title);
        GUILayout.BeginHorizontal();
        if (look != null)
            for (int m = 1; m <= 3; m++) if (GUILayout.Button($"연출만 {m}악장")) look.SetMovement(m);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("이번 악장 HP 0으로 (전환·격파 확인)")) boss.DebugFinishMovement();
    }

    private void GestureTab()
    {
        var look = boss.Look;
        if (look == null || !look.Ready) { GUILayout.Label("외형(MaestroLook)이 없음"); return; }
        GUILayout.Label("자동 패턴이 끼어들면 몸짓이 덮이니 '자동 패턴 멈춤'을 켜고 보세요", small);
        boss.debugPauseAuto = GUILayout.Toggle(boss.debugPauseAuto, " 자동 패턴 멈춤");
        GUILayout.BeginHorizontal();
        GUILayout.Label($"몸짓 길이 {gestureSeconds:0.00}초", GUILayout.Width(130));
        gestureSeconds = GUILayout.HorizontalSlider(gestureSeconds, 0.2f, 4f);
        GUILayout.EndHorizontal();
        foreach (MaestroGesture g in Enum.GetValues(typeof(MaestroGesture)))
        {
            if (g == MaestroGesture.None) continue;
            if (!GUILayout.Button($"▶ {g}")) continue;
            if (g == MaestroGesture.SlashLeft || g == MaestroGesture.SlashRight)
                look.PlayTimed(g, gestureSeconds * 0.45f, gestureSeconds * 0.2f, gestureSeconds * 0.35f);
            else look.Play(g, gestureSeconds);
        }
        if (GUILayout.Button("■ 몸짓 멈춤 (평소 지휘로)")) look.Stop();
        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("튕기기")) look.Flick();
        if (GUILayout.Button("피격")) look.Hit(1f);
        if (GUILayout.Button("지휘봉 생성")) look.FormBaton(0.6f);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("무너졌다 다시 쌓기")) look.DropAll(0.8f, 0.9f, 1.2f);

        // 키 포즈 (11-25 B-2 — 모션 시트 포즈로 봇이 흘러감)
        GUILayout.Space(6);
        GUILayout.Label($"키 포즈: {(look.HasKeyPoses ? look.CurrentKeyPose : "없음 (빌더 다시 실행)")}", small);
        if (!look.HasKeyPoses) return;
        poseMirror = GUILayout.Toggle(poseMirror, " 좌우 반전");
        for (int p = 0; p < look.KeyPoseCount; p++)
        {
            if (p % 3 == 0) GUILayout.BeginHorizontal();
            if (GUILayout.Button(look.KeyPoseName(p))) look.DebugPose(p, poseMirror);
            if (p % 3 == 2 || p == look.KeyPoseCount - 1) GUILayout.EndHorizontal();
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("잔상")) look.PoseAfterimage(0.5f, new Color(0.76f, 0.74f, 0.98f, 0.4f));
        if (GUILayout.Button("■ 포즈 풀기")) look.DebugPose(-1, false);
        GUILayout.EndHorizontal();
    }

    private bool poseMirror;

    private void LookTab()
    {
        var look = boss.Look;
        if (look == null) { GUILayout.Label("외형(MaestroLook)이 없음"); return; }
        CopyButtons();
        GUILayout.Label("MaestroLook — 컴포넌트라 플레이를 끄면 돌아감 (위 '복사' 후 메뉴로 붙여넣기)", small);
        MaestroFieldEditor.Draw(look, "look", 0, (int)boss.SelectedDifficulty);
    }

    private void PuppetTab()
    {
        var look = boss.Look;
        if (look == null || look.data == null) { GUILayout.Label("퍼펫 데이터가 없음"); return; }
        GUILayout.Label("Maestro_PuppetData — 에셋이라 플레이를 꺼도 남음. 뼈: 진동수(빠르기)·감쇠(<1 출렁임)·반응·팔로스루 / 천: 복원력·중력·바람·저항", small);
        if (MaestroFieldEditor.Draw(look.data, "puppet", 0, 0)) SetDirty(look.data);
    }

    private void PatternDataTab()
    {
        var list = new List<(string, ScriptableObject)>();
        foreach (var p in boss.DebugPatterns)
        {
            if (p == null) continue;
            foreach (var f in p.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (typeof(ScriptableObject).IsAssignableFrom(f.FieldType) && f.GetValue(p) is ScriptableObject so && so != null)
                    list.Add(($"{p.GimmickName} · {so.name}", so));
        }
        if (list.Count == 0) { GUILayout.Label("패턴 데이터가 없음"); return; }
        var names = new string[list.Count];
        for (int i = 0; i < list.Count; i++) names[i] = list[i].Item1;
        patternDataIndex = Mathf.Clamp(patternDataIndex, 0, list.Count - 1);
        patternDataIndex = GUILayout.SelectionGrid(patternDataIndex, names, 1);
        GUILayout.Label("에셋이라 플레이를 꺼도 남음. 난이도별 줄은 지금 난이도가 먼저 펼쳐짐. 바뀐 값은 다음 패턴 실행부터", small);
        var target = list[patternDataIndex].Item2;
        if (MaestroFieldEditor.Draw(target, "pd" + patternDataIndex, 0, (int)boss.SelectedDifficulty)) SetDirty(target);
    }

    private void SwarmTab()
    {
        var swarm = boss.Swarm;
        if (swarm == null) return;
        CopyButtons();
        var mat = swarm.material;
        if (mat != null)
        {
            GUILayout.Label($"재질 {mat.name} — 에셋이라 남음", title);
            MatSlider(mat, "_LightStrength", "조명 반영 (0 = 그림 색 그대로)", 0f, 1f);
            MatSlider(mat, "_EdgeTilt", "가장자리 기울기", 0f, 1f);
            MatSlider(mat, "_TopLight", "윗면 밝게", 0f, 1f);
            MatSlider(mat, "_BottomDark", "아랫면 어둡게", 0f, 1f);
            MatSlider(mat, "_GlowStrength", "발광 세기", 0f, 8f);
            MatSlider(mat, "_FlashStrength", "번쩍임 세기", 0f, 1f);
        }
        GUILayout.Label("RSNanoSwarm (KKH 엔진) — 컴포넌트라 플레이를 끄면 돌아감. 봇 수·칸 크기는 다시 빌드해야 반영", small);
        MaestroFieldEditor.Draw(swarm, "swarm", 0, 0);
    }

    private void TimeTab()
    {
        GUILayout.Label($"시간 배율 {timeScale:0.00} (느리게 보기)");
        float t = GUILayout.HorizontalSlider(timeScale, 0.05f, 1f);
        if (!Mathf.Approximately(t, timeScale)) { timeScale = t; Time.timeScale = t; }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("0.1배")) { timeScale = 0.1f; Time.timeScale = 0.1f; }
        if (GUILayout.Button("0.25배")) { timeScale = 0.25f; Time.timeScale = 0.25f; }
        if (GUILayout.Button("0.5배")) { timeScale = 0.5f; Time.timeScale = 0.5f; }
        if (GUILayout.Button("1배")) { timeScale = 1f; Time.timeScale = 1f; }
        GUILayout.EndHorizontal();
        showHurt = GUILayout.Toggle(showHurt, " 보스 판정 상자 보기 (초록)");
    }

    private void OnDisable()
    {
        if (!Mathf.Approximately(Time.timeScale, 1f) && !Mathf.Approximately(timeScale, 1f)) Time.timeScale = 1f;
    }

    // ───────────────────────── 도우미 ─────────────────────────

    private void CopyButtons()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("외형·군집 값 복사 (클립보드)"))
        {
            var pack = new MaestroDebugValues { look = boss.Look != null ? JsonUtility.ToJson(boss.Look) : "", swarm = boss.Swarm != null ? JsonUtility.ToJson(boss.Swarm) : "" };
            GUIUtility.systemCopyBuffer = MaestroDebugValues.Tag + JsonUtility.ToJson(pack);
            message = "복사함 — 플레이를 끈 뒤 메뉴 NYH/보스3/디버그: 복사한 외형·군집 값 붙여넣기";
        }
        GUILayout.EndHorizontal();
    }

    private static void MatSlider(Material m, string prop, string label, float min, float max)
    {
        if (!m.HasProperty(prop)) return;
        float v = m.GetFloat(prop);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{label} {v:0.00}", GUILayout.Width(190));
        float n = GUILayout.HorizontalSlider(v, min, max);
        GUILayout.EndHorizontal();
        if (!Mathf.Approximately(n, v)) { m.SetFloat(prop, n); SetDirty(m); }
    }

    private static void SetDirty(UnityEngine.Object o)
    {
#if UNITY_EDITOR
        if (o != null) UnityEditor.EditorUtility.SetDirty(o);
#endif
    }

    // 보스 판정 상자 (BoxCollider2D) 테두리
    private void DrawHurtBox()
    {
        if (boss == null) return;
        var cam = Camera.main;
        var box = boss.GetComponent<BoxCollider2D>();
        if (cam == null || box == null || !box.enabled) return;
        var b = box.bounds;
        Vector3 a = cam.WorldToScreenPoint(new Vector3(b.min.x, b.min.y, b.center.z));
        Vector3 c = cam.WorldToScreenPoint(new Vector3(b.max.x, b.max.y, b.center.z));
        var r = Rect.MinMaxRect(Mathf.Min(a.x, c.x), Screen.height - Mathf.Max(a.y, c.y), Mathf.Max(a.x, c.x), Screen.height - Mathf.Min(a.y, c.y));
        var old = GUI.color;
        GUI.color = new Color(0.2f, 1f, 0.3f, 0.9f);
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMin, r.yMax - 2, r.width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, 2, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - 2, r.yMin, 2, r.height), Texture2D.whiteTexture);
        GUI.color = old;
    }
}

/// <summary>디버그 패널 '복사' 값 (플레이 끈 뒤 에디터 메뉴로 붙여넣기)</summary>
[Serializable]
public class MaestroDebugValues
{
    public const string Tag = "MAESTRO_DEBUG:";
    public string look, swarm;
}

/// <summary>
/// 리플렉션으로 필드를 IMGUI로 그리고 고친다 — 인스펙터에 보이는 필드(public / [SerializeField], [HideInInspector]·[NonSerialized] 제외).
/// float·int(Range가 있으면 그 범위 슬라이더, 없으면 값 주변 슬라이더 + 입력칸), bool, enum, Vector2/3, Color, 배열·리스트(직렬화 클래스는 한 줄씩 펼침).
/// 고쳤으면 true.
/// </summary>
public static class MaestroFieldEditor
{
    private static readonly Dictionary<string, bool> fold = new Dictionary<string, bool>();
    private static readonly Dictionary<string, int> pick = new Dictionary<string, int>();
    private static readonly Dictionary<string, string> text = new Dictionary<string, string>();

    public static bool Draw(object obj, string path, int depth, int preferIndex)
    {
        if (obj == null || depth > 3) return false;
        bool changed = false;
        foreach (var f in obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (f.IsDefined(typeof(HideInInspector), true) || f.IsDefined(typeof(NonSerializedAttribute), true)) continue;
            if (!f.IsPublic && !f.IsDefined(typeof(SerializeField), true)) continue;
            if (f.IsInitOnly || f.IsLiteral) continue;
            var tip = f.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? "";
            var header = f.GetCustomAttribute<HeaderAttribute>();
            if (header != null) GUILayout.Label("— " + header.header, EditorLikeHeader);
            changed |= DrawField(obj, f, new GUIContent(f.Name, tip), path + "." + f.Name, depth, preferIndex);
        }
        return changed;
    }

    private static GUIStyle headerStyle;
    private static GUIStyle EditorLikeHeader => headerStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 11 };

    private static bool DrawField(object obj, FieldInfo f, GUIContent label, string path, int depth, int preferIndex)
    {
        var t = f.FieldType;
        object v = f.GetValue(obj);
        object n = DrawValue(t, v, label, path, depth, preferIndex, f, out bool changed);
        if (changed && !t.IsClass) f.SetValue(obj, n);
        else if (changed && n != null && !ReferenceEquals(n, v)) f.SetValue(obj, n);
        return changed;
    }

    private static object DrawValue(Type t, object v, GUIContent label, string path, int depth, int preferIndex, FieldInfo f, out bool changed)
    {
        changed = false;
        if (t == typeof(float))
        {
            float x = (float)v;
            float y = FloatRow(label, x, path, f);
            changed = !Mathf.Approximately(x, y);
            return y;
        }
        if (t == typeof(int))
        {
            int x = (int)v;
            int y = Mathf.RoundToInt(FloatRow(label, x, path, f));
            changed = x != y;
            return y;
        }
        if (t == typeof(bool))
        {
            bool x = (bool)v;
            bool y = GUILayout.Toggle(x, label);
            changed = x != y;
            return y;
        }
        if (t.IsEnum)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(170));
            var vals = Enum.GetValues(t);
            int idx = Array.IndexOf(vals, v);
            if (GUILayout.Button(v.ToString())) { idx = (idx + 1) % vals.Length; changed = true; }
            GUILayout.EndHorizontal();
            return changed ? vals.GetValue(idx) : v;
        }
        if (t == typeof(Vector2))
        {
            var x = (Vector2)v;
            GUILayout.Label(label);
            float a = FloatRow(new GUIContent("   x", label.tooltip), x.x, path + ".x", null);
            float b = FloatRow(new GUIContent("   y", label.tooltip), x.y, path + ".y", null);
            changed = !Mathf.Approximately(a, x.x) || !Mathf.Approximately(b, x.y);
            return new Vector2(a, b);
        }
        if (t == typeof(Vector3))
        {
            var x = (Vector3)v;
            GUILayout.Label(label);
            float a = FloatRow(new GUIContent("   x"), x.x, path + ".x", null);
            float b = FloatRow(new GUIContent("   y"), x.y, path + ".y", null);
            float c = FloatRow(new GUIContent("   z"), x.z, path + ".z", null);
            changed = !Mathf.Approximately(a, x.x) || !Mathf.Approximately(b, x.y) || !Mathf.Approximately(c, x.z);
            return new Vector3(a, b, c);
        }
        if (t == typeof(Vector4))
        {
            var x = (Vector4)v;
            GUILayout.Label(label);
            float a = FloatRow(new GUIContent("   x"), x.x, path + ".x", null);
            float b = FloatRow(new GUIContent("   y"), x.y, path + ".y", null);
            float c = FloatRow(new GUIContent("   z"), x.z, path + ".z", null);
            float d = FloatRow(new GUIContent("   w"), x.w, path + ".w", null);
            changed = a != x.x || b != x.y || c != x.z || d != x.w;
            return new Vector4(a, b, c, d);
        }
        if (t == typeof(Color) || t == typeof(Color32))
        {
            Color x = t == typeof(Color) ? (Color)v : (Color)(Color32)v;
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110));
            var old = GUI.color; GUI.color = new Color(x.r, x.g, x.b, 1f); GUILayout.Box("", GUILayout.Width(20), GUILayout.Height(16)); GUI.color = old;
            float r = GUILayout.HorizontalSlider(x.r, 0f, 1f), g = GUILayout.HorizontalSlider(x.g, 0f, 1f), b = GUILayout.HorizontalSlider(x.b, 0f, 1f), a = GUILayout.HorizontalSlider(x.a, 0f, 1f);
            GUILayout.EndHorizontal();
            var y = new Color(r, g, b, a);
            changed = y != x;
            return t == typeof(Color) ? (object)y : (Color32)y;
        }
        if (typeof(UnityEngine.Object).IsAssignableFrom(t))
        {
            GUILayout.Label($"{label.text}: {(v as UnityEngine.Object != null ? ((UnityEngine.Object)v).name : "(없음)")}");
            return v;
        }
        if (t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)))
        {
            var list = v as IList;
            if (list == null) return v;
            Type et = t.IsArray ? t.GetElementType() : t.GetGenericArguments()[0];
            if (!Fold(path, $"{label.text} [{list.Count}]", label.tooltip)) return v;
            if (et.IsPrimitive || et == typeof(Vector2) || et == typeof(Color))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    object e = DrawValue(et, list[i], new GUIContent($"   [{i}]"), path + "[" + i + "]", depth + 1, preferIndex, null, out bool ec);
                    if (ec) { list[i] = e; changed = true; }
                }
                return v;
            }
            if (list.Count == 0) return v;
            // 직렬화 클래스: 한 줄씩 (◀ 번호 ▶). 처음엔 지금 난이도 줄
            if (!pick.TryGetValue(path, out int idx)) idx = Mathf.Clamp(preferIndex, 0, list.Count - 1);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(28))) idx = (idx - 1 + list.Count) % list.Count;
            object cur = list[Mathf.Clamp(idx, 0, list.Count - 1)];
            string nameField = cur?.GetType().GetField("name")?.GetValue(cur) as string;
            GUILayout.Label($"  {idx}{(string.IsNullOrEmpty(nameField) ? "" : " · " + nameField)}", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("▶", GUILayout.Width(28))) idx = (idx + 1) % list.Count;
            GUILayout.EndHorizontal();
            pick[path] = idx = Mathf.Clamp(idx, 0, list.Count - 1);
            GUILayout.BeginVertical(GUI.skin.box);
            changed |= Draw(list[idx], path + "[" + idx + "]", depth + 1, preferIndex);
            GUILayout.EndVertical();
            return v;
        }
        if (t.IsClass && t.IsDefined(typeof(SerializableAttribute), false) && t != typeof(string))
        {
            if (v == null) return v;
            if (!Fold(path, label.text, label.tooltip)) return v;
            GUILayout.BeginVertical(GUI.skin.box);
            changed |= Draw(v, path, depth + 1, preferIndex);
            GUILayout.EndVertical();
            return v;
        }
        if (t == typeof(string)) { GUILayout.Label($"{label.text}: {v}"); return v; }
        return v;
    }

    private static bool Fold(string path, string label, string tip)
    {
        fold.TryGetValue(path, out bool o);
        o = GUILayout.Toggle(o, new GUIContent((o ? "▼ " : "▶ ") + label, tip), GUI.skin.button);
        fold[path] = o;
        return o;
    }

    // 이름 · 슬라이더 · 입력칸. Range가 있으면 그 범위, Min이 있으면 [min, 값 근처], 없으면 값 주변
    private static float FloatRow(GUIContent label, float x, string path, FieldInfo f)
    {
        float lo, hi;
        var range = f?.GetCustomAttribute<RangeAttribute>();
        var min = f?.GetCustomAttribute<MinAttribute>();
        float span = Mathf.Max(1f, Mathf.Abs(x) * 2f);
        if (range != null) { lo = range.min; hi = range.max; }
        else if (min != null) { lo = min.min; hi = Mathf.Max(min.min + 1f, x + span); }
        else { lo = x - span; hi = x + span; }

        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(170));
        float y = GUILayout.HorizontalSlider(x, lo, hi);
        if (!text.TryGetValue(path, out string s) || !GUI.GetNameOfFocusedControl().Equals(path)) s = x.ToString("0.###");
        GUI.SetNextControlName(path);
        string s2 = GUILayout.TextField(s, GUILayout.Width(64));
        text[path] = s2;
        GUILayout.EndHorizontal();
        if (s2 != s && float.TryParse(s2, out float typed)) return typed;
        return Mathf.Approximately(y, x) ? x : y;
    }
}
#endif
