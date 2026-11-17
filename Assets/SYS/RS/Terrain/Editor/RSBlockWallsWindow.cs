// RE:AL STEEL - 이동 막기 (자동 경계) 창
//
// 플레이어가 서 있는 자리에서 "걸어갈 수 있는 곳"을 격자로 퍼뜨려 보고, 아래 경계에 보이지 않는 벽(RSBlockWalls)을 세운다.
//   · 맵 밖으로 떨어지는 곳 (발밑에 아무 바닥도 없음 / 지형 범위 밖 / 아주 깊은 곳)
//   · 들어가면 못 돌아오는 곳 (내려가는 건 제한이 없지만 오르는 건 Step Offset · Slope Limit 까지라 갇히는 구덩이 · 아랫단)
//   · (선택) 시작 높이보다 일정 이상 높은 곳 — 윗단 막기
// 이동 규칙은 RSSimpleCharacter 와 같다: 오르막은 CharacterController 의 Step Offset · Slope Limit 까지, 내리막은 제한 없음, 점프 없음.
//
// 지형이 맵 프리팹(MAP_...) 안에 있으면 벽도 그 프리팹 안(BLOCK_이동 막기 (자동))에 저장한다.
// 걸어갈 수 있는 곳에 걸쳐 있는데 콜라이더가 없는(통과되는) 소품도 같이 찾아 준다.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Terrain.EditorTools
{
    public class RSBlockWallsWindow : EditorWindow
    {
        const string BlockName = "BLOCK_이동 막기 (자동)";
        const int MaxL = 6;          // 한 칸에 겹친 바닥 층 수 (다리 위 · 아래 등)
        const int VOID = -1, RISE = -2, NONE = -3;

        // 기본값 — '기본값으로' 버튼이 이 값으로 되돌린다
        const float DefGridCell = 0.15f, DefWallHeight = 2f, DefMaxRise = 0f, DefKillDepth = 15f,
                    DefGapFill = 1.5f, DefFlatAngle = 15f, DefSlopeBand = 3f;
        const bool DefLimitToTerrain = true;

        [SerializeField] float gridCell = DefGridCell;
        [SerializeField] float wallHeight = DefWallHeight;
        [SerializeField] bool limitToTerrain = DefLimitToTerrain;
        [SerializeField] float maxRise = DefMaxRise;
        [SerializeField] float killDepth = DefKillDepth;
        [SerializeField] float gapFill = DefGapFill;
        [SerializeField] float flatAngle = DefFlatAngle;
        [SerializeField] float slopeBand = DefSlopeBand;

        void ResetSettings()
        {
            gridCell = DefGridCell; wallHeight = DefWallHeight; limitToTerrain = DefLimitToTerrain;
            maxRise = DefMaxRise; killDepth = DefKillDepth; gapFill = DefGapFill;
            flatAngle = DefFlatAngle; slopeBand = DefSlopeBand;
            GUI.FocusControl(null);
        }
        [SerializeField] bool showPreview = true;

        readonly List<Bounds> boxes = new List<Bounds>();
        readonly List<Vector3> trapPts = new List<Vector3>();
        readonly List<Vector3> slopePts = new List<Vector3>();
        readonly List<GameObject> noCollider = new List<GameObject>();
        string report = "";
        bool analyzed;
        float usedCs = 0.25f;
        Vector2 scroll;

        [MenuItem("Tools/RE_AL STEEL/Stage/이동 막기 (자동 경계)", false, 30)]
        static void Open() { GetWindow<RSBlockWallsWindow>("이동 막기"); }

        void OnEnable() { SceneView.duringSceneGui += OnScene; }
        void OnDisable() { SceneView.duringSceneGui -= OnScene; }

        // ─────────────────────────────────────────────────────────────
        // UI
        // ─────────────────────────────────────────────────────────────

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(
                "플레이어(CharacterController) 자리에서 걸어갈 수 있는 곳을 퍼뜨려 보고, 맵 밖으로 떨어지는 곳 · 들어가면 못 돌아오는 곳 앞에 보이지 않는 벽을 세웁니다.\n" +
                "1) 분석 → 씬 뷰에 빨간 상자(세울 벽) · 주황 X(갇히는 곳) · 노란 +(잘라 낸 비탈)\n2) 벽 세우기 → 맵 프리팹 안 'BLOCK_이동 막기 (자동)' 에 저장",
                MessageType.Info);

            gridCell = EditorGUILayout.Slider(new GUIContent("격자 칸 (m)", "작을수록 경사 · 모서리를 촘촘히 따라간다. 0.15 권장 (40 m 지형이면 1 ~ 2초)"), gridCell, 0.08f, 1f);
            wallHeight = EditorGUILayout.Slider(new GUIContent("벽 높이 (m)", "발 높이 위로 이만큼. 점프가 없어서 2 m 면 충분"), wallHeight, 0.5f, 6f);
            limitToTerrain = EditorGUILayout.Toggle(new GUIContent("지형 범위 밖은 막기", "가장 큰 RS 지형의 가로 · 세로 밖(외곽 언덕 포함)으로는 못 나가게"), limitToTerrain);
            maxRise = EditorGUILayout.FloatField(new GUIContent("윗단 막기 (m, 0 = 끔)", "시작 자리보다 이만큼 이상 높은 곳은 못 올라가게 막는다"), maxRise);
            killDepth = EditorGUILayout.FloatField(new GUIContent("떨어짐 깊이 (m)", "시작 자리보다 이만큼 아래로 떨어지는 곳은 맵 밖으로 본다"), killDepth);
            flatAngle = EditorGUILayout.Slider(new GUIContent("평지 기준 (도)", "이보다 완만한 바닥만 '평지'. 위험한 곳으로 이어지는 비탈은 평지가 끝나는 자리에 벽을 세운다"), flatAngle, 3f, 40f);
            slopeBand = EditorGUILayout.Slider(new GUIContent("비탈 잘라내기 (m)", "위험한 곳에 붙은 비탈을 이 거리까지 걸을 수 없는 곳으로 본다 → 벽이 비탈 중간이 아니라 위쪽 평지 가장자리에 붙는다. 0 = 끔. 긴 경사로가 끊기면 줄이세요"), slopeBand, 0f, 8f);
            gapFill = EditorGUILayout.Slider(new GUIContent("틈 메우기 (m)", "위험한 곳(낭떠러지 · 갇히는 곳)에서 이 거리 안에 있는 돌 · 폐자재 · 소품 띠도 위험으로 보고 그 앞까지 벽을 잇는다. 둑 위 폐자재 사이로 새는 틈을 막는다"), gapFill, 0f, 4f);
            if (GUILayout.Button(new GUIContent("기본값으로", "격자 0.15 · 벽 높이 2 · 지형 밖 막기 켬 · 윗단 0 · 떨어짐 15 · 틈 1.5 · 평지 15° · 비탈 3"), GUILayout.Width(90))) ResetSettings();
            EditorGUI.BeginChangeCheck();
            showPreview = EditorGUILayout.Toggle("씬 뷰 미리보기", showPreview);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("분석", GUILayout.Height(28))) { Analyze(); SceneView.RepaintAll(); }
                using (new EditorGUI.DisabledScope(!analyzed))
                    if (GUILayout.Button("벽 세우기 (" + boxes.Count + "개)", GUILayout.Height(28))) Commit(false);
            }
            if (GUILayout.Button("자동 벽 전부 지우기 (수동 벽은 남김)")) Commit(true);

            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, MessageType.None);

            if (noCollider.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("콜라이더가 없어 통과되는 소품 (걸어갈 수 있는 곳에 걸침)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("조립품은 인스펙터의 '콜라이더' 를 켜고, 모델은 Box / Mesh Collider 를 붙이세요. 통과돼도 되는 장식이면 그대로 둬도 됩니다.", MessageType.None);
                if (GUILayout.Button("모두 선택")) Selection.objects = noCollider.ToArray();
                foreach (var go in noCollider)
                {
                    if (go == null) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(go, typeof(GameObject), true);
                        if (GUILayout.Button("선택", GUILayout.Width(44))) { Selection.activeGameObject = go; EditorGUIUtility.PingObject(go); SceneView.FrameLastActiveSceneView(); }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void OnScene(SceneView sv)
        {
            if (!showPreview || !analyzed) return;
            var z = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            Handles.color = new Color(1f, 0.25f, 0.2f, 0.9f);
            foreach (var b in boxes) Handles.DrawWireCube(b.center, b.size);
            Handles.color = new Color(1f, 0.6f, 0.1f, 0.9f);
            float s = usedCs * 0.3f;
            foreach (var p in trapPts)
            {
                Handles.DrawLine(p + new Vector3(-s, 0.03f, -s), p + new Vector3(s, 0.03f, s));
                Handles.DrawLine(p + new Vector3(-s, 0.03f, s), p + new Vector3(s, 0.03f, -s));
            }
            Handles.color = new Color(1f, 0.95f, 0.2f, 0.9f);
            foreach (var p in slopePts)
            {
                Handles.DrawLine(p + new Vector3(-s, 0.03f, 0f), p + new Vector3(s, 0.03f, 0f));
                Handles.DrawLine(p + new Vector3(0f, 0.03f, -s), p + new Vector3(0f, 0.03f, s));
            }
            Handles.zTest = z;
        }

        // ─────────────────────────────────────────────────────────────
        // 분석
        // ─────────────────────────────────────────────────────────────

        HashSet<Collider> ignore;
        readonly Collider[] buf = new Collider[32];
        float pr, ph, pstep, pclimb, cosLim;

        static bool Alive(Component c)
        {
            return c != null && c.gameObject.scene.IsValid() && !EditorSceneManager.IsPreviewScene(c.gameObject.scene)
                && !EditorUtility.IsPersistent(c) && c.gameObject.activeInHierarchy;
        }

        /// <summary>플레이어 컴포넌트(RSSimpleCharacter)의 maxStepHeight — 다른 어셈블리라 이름으로 읽는다</summary>
        static float StepClimbOf(CharacterController cc)
        {
            foreach (var mb in cc.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                var f = mb.GetType().GetField("maxStepHeight");
                if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(mb);
            }
            return 0f;
        }

        static RSTerrain MainTerrain()
        {
            RSTerrain best = null; float area = -1f;
            foreach (var t in Object.FindObjectsByType<RSTerrain>(FindObjectsSortMode.None))
            {
                float a = t.size.x * t.size.y;
                if (a > area) { area = a; best = t; }
            }
            return best;
        }

        static CharacterController FindPlayer()
        {
            CharacterController first = null;
            foreach (var c in Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
            {
                if (c.CompareTag("Player")) return c;
                if (first == null) first = c;
            }
            return first;
        }

        bool Standable(Vector3 p)
        {
            Vector3 a = p + Vector3.up * (pr + pstep + 0.03f);
            Vector3 b = p + Vector3.up * Mathf.Max(pr + pstep + 0.04f, ph - pr);
            int n = Physics.OverlapCapsuleNonAlloc(a, b, pr * 0.9f, buf, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!ignore.Contains(buf[i])) return false;
            return true;
        }

        bool WallBetween(Vector3 from, Vector3 to, float y)
        {
            Vector3 o = new Vector3(from.x, y, from.z), d = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float len = d.magnitude; if (len < 1e-4f) return false;
            d /= len;
            var hits = Physics.RaycastAll(o, d, len, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
                if (!ignore.Contains(h.collider) && h.normal.y < cosLim - 0.01f) return true;
            return false;
        }

        void Analyze()
        {
            boxes.Clear(); trapPts.Clear(); slopePts.Clear(); noCollider.Clear(); analyzed = false; report = "";

            var cc = FindPlayer();
            if (cc == null) { report = "플레이어(CharacterController)를 열린 씬에서 못 찾았습니다."; return; }
            var terrain = MainTerrain();
            if (limitToTerrain && terrain == null) { report = "RS 지형이 없어 '지형 범위 밖은 막기' 를 끄고 분석합니다."; }

            Physics.SyncTransforms();
            var ls = cc.transform.lossyScale;
            pr = cc.radius * Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z));
            ph = cc.height * Mathf.Abs(ls.y);
            pstep = cc.stepOffset;
            pclimb = Mathf.Max(pstep, StepClimbOf(cc));   // RSSimpleCharacter 의 '최대 턱 높이' (계단)
            cosLim = Mathf.Cos(Mathf.Clamp(cc.slopeLimit, 1f, 89f) * Mathf.Deg2Rad);

            // 무시할 콜라이더: 플레이어 · 이미 세운 벽
            ignore = new HashSet<Collider>(cc.GetComponentsInChildren<Collider>(true));
            var all = new List<Collider>();
            foreach (var c in Resources.FindObjectsOfTypeAll<Collider>())
            {
                if (!Alive(c) || !c.enabled || c.isTrigger) continue;
                if (c.GetComponentInParent<RSBlockWalls>(true) != null) { ignore.Add(c); continue; }
                if (ignore.Contains(c)) continue;
                all.Add(c);
            }
            if (all.Count == 0) { report = "씬에 콜라이더가 없습니다 (지형의 '콜라이더 만들기' 확인)."; return; }

            Bounds world = all[0].bounds;
            foreach (var c in all) world.Encapsulate(c.bounds);

            // 영역 (XZ)
            bool useArea = limitToTerrain && terrain != null;
            float axMin, axMax, azMin, azMax;
            if (useArea)
            {
                var t = terrain.transform;
                Vector3 c0 = t.TransformPoint(new Vector3(-terrain.size.x * 0.5f, 0f, -terrain.size.y * 0.5f));
                Vector3 c1 = t.TransformPoint(new Vector3(terrain.size.x * 0.5f, 0f, terrain.size.y * 0.5f));
                axMin = Mathf.Min(c0.x, c1.x); axMax = Mathf.Max(c0.x, c1.x);
                azMin = Mathf.Min(c0.z, c1.z); azMax = Mathf.Max(c0.z, c1.z);
            }
            else { axMin = world.min.x; axMax = world.max.x; azMin = world.min.z; azMax = world.max.z; }

            float cs = Mathf.Clamp(gridCell, 0.08f, 1f);
            int nx, nz;
            while (true)
            {
                nx = Mathf.CeilToInt((axMax - axMin) / cs) + 2;
                nz = Mathf.CeilToInt((azMax - azMin) / cs) + 2;
                if ((long)nx * nz <= 300000) break;
                cs *= 1.25f;
            }
            usedCs = cs;
            float x0 = axMin - cs, z0 = azMin - cs;
            float topY = world.max.y + 2f, botY = world.min.y - 2f;
            float maxUp = pclimb + cs * Mathf.Tan(Mathf.Clamp(cc.slopeLimit, 1f, 80f) * Mathf.Deg2Rad) * 1.05f;

            int nc = nx * nz;
            var fl = new float[nc * MaxL];       // 층 높이 (위 → 아래)
            var steep = new bool[nc * MaxL];
            var nrmY = new float[nc * MaxL];
            var stand = new bool[nc * MaxL];
            var cnt = new byte[nc];
            var inside = new bool[nc];

            // ── 칸마다 위에서 아래로 쏘아 겹친 층을 모두 찾는다 ──
            RaycastHit hit;
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int c = j * nx + i;
                float x = x0 + (i + 0.5f) * cs, z = z0 + (j + 0.5f) * cs;
                inside[c] = i > 0 && j > 0 && i < nx - 1 && j < nz - 1 && (!useArea || (x >= axMin && x <= axMax && z >= azMin && z <= azMax));
                if (!inside[c]) continue;
                Vector3 o = new Vector3(x, topY, z);
                int k = 0;
                for (int it = 0; it < 32 && k < MaxL; it++)
                {
                    float remain = o.y - botY;
                    if (remain <= 0f || !Physics.Raycast(o, Vector3.down, out hit, remain, ~0, QueryTriggerInteraction.Ignore)) break;
                    o.y = hit.point.y - 0.01f;
                    if (ignore.Contains(hit.collider) || hit.normal.y < 0.05f) continue;
                    // 바로 위 층과 거의 같은 높이(겹친 면)는 하나로
                    if (k > 0 && fl[c * MaxL + k - 1] - hit.point.y < 0.05f) continue;
                    int id = c * MaxL + k;
                    fl[id] = hit.point.y;
                    steep[id] = hit.normal.y < cosLim;
                    nrmY[id] = hit.normal.y;
                    stand[id] = Standable(hit.point);
                    k++;
                }
                cnt[c] = (byte)k;
            }

            // ── 시작 자리 ──
            Vector3 foot = cc.transform.TransformPoint(cc.center) - Vector3.up * (ph * 0.5f);
            int si = Mathf.FloorToInt((foot.x - x0) / cs), sj = Mathf.FloorToInt((foot.z - z0) / cs);
            int start = -1; float bestD = float.MaxValue;
            for (int dj = -3; dj <= 3; dj++)
            for (int di = -3; di <= 3; di++)
            {
                int i = si + di, j = sj + dj;
                if (i < 0 || j < 0 || i >= nx || j >= nz) continue;
                int c = j * nx + i;
                for (int k = 0; k < cnt[c]; k++)
                {
                    int id = c * MaxL + k;
                    if (!stand[id] || steep[id]) continue;
                    float d = Mathf.Abs(fl[id] - foot.y) + 0.3f * (Mathf.Abs(di) + Mathf.Abs(dj));
                    if (d < bestD) { bestD = d; start = id; }
                }
            }
            if (start < 0) { report = "플레이어 발밑에서 설 수 있는 바닥을 못 찾았습니다. 플레이어를 바닥 위에 두고 다시 분석하세요."; return; }
            float startH = fl[start];

            // ── 앞으로 퍼뜨리기 (갈 수 있는 곳) ──
            int N = nc * MaxL;
            var fwd = new int[N * 4];
            for (int e = 0; e < fwd.Length; e++) fwd[e] = NONE;
            var inR = new bool[N];
            var order = new List<int>();
            var q = new Queue<int>();
            inR[start] = true; q.Enqueue(start);
            int[] di4 = { 1, -1, 0, 0 }, dj4 = { 0, 0, 1, -1 };

            while (q.Count > 0)
            {
                int a = q.Dequeue(); order.Add(a);
                int ca = a / MaxL, ia = ca % nx, ja = ca / nx;
                float ha = fl[a];
                bool aSteep = steep[a];
                Vector3 pa = new Vector3(x0 + (ia + 0.5f) * cs, ha, z0 + (ja + 0.5f) * cs);
                for (int d = 0; d < 4; d++)
                {
                    int ib = ia + di4[d], jb = ja + dj4[d];
                    int target;
                    if (ib < 0 || jb < 0 || ib >= nx || jb >= nz || !inside[jb * nx + ib]) target = VOID;
                    else
                    {
                        int cb = jb * nx + ib;
                        target = VOID;
                        float lim = aSteep ? ha + pstep : ha + maxUp;    // 가파른 비탈 위에서는 높이를 얻을 수 없다
                        for (int k = 0; k < cnt[cb]; k++)
                        {
                            int id = cb * MaxL + k;
                            float hb = fl[id];
                            if (hb > (steep[id] ? ha + pstep : lim)) continue;   // 너무 높다 (위층 · 벽)
                            target = id; break;                              // 위에서부터 → 내려가 닿는 가장 높은 층
                        }
                        if (target >= 0)
                        {
                            float hb = fl[target];
                            if (hb < startH - killDepth) target = VOID;
                            else if (maxRise > 0f && hb > startH + maxRise) target = RISE;
                            else if (!stand[target]) target = NONE;          // 소품 · 벽이 차지함
                            else
                            {
                                Vector3 pb = new Vector3(x0 + (ib + 0.5f) * cs, hb, z0 + (jb + 0.5f) * cs);
                                if (WallBetween(pa, pb, ha + pclimb + 0.05f) || WallBetween(pa, pb, ha + ph * 0.6f)) target = NONE;
                            }
                        }
                        else
                        {
                            // 닿을 층이 없다: 아무것도 없으면 낭떠러지. 전부 너무 높으면 — 머리 위로 떠 있는 것(그 아래는 빔)이면 낭떠러지, 아니면 벽 · 오르막
                            if (cnt[cb] == 0) target = VOID;
                            else target = fl[cb * MaxL + cnt[cb] - 1] > ha + ph ? VOID : NONE;
                        }
                    }
                    fwd[a * 4 + d] = target;
                    if (target >= 0 && !inR[target]) { inR[target] = true; q.Enqueue(target); }
                }
            }

            // ── 거꾸로 퍼뜨리기 (시작 자리로 돌아올 수 있는 곳) ──
            var rev = new Dictionary<int, List<int>>();
            foreach (int a in order)
                for (int d = 0; d < 4; d++)
                {
                    int b = fwd[a * 4 + d];
                    if (b < 0) continue;
                    List<int> l; if (!rev.TryGetValue(b, out l)) rev[b] = l = new List<int>(4);
                    l.Add(a);
                }
            var back = new bool[N];
            back[start] = true; q.Enqueue(start);
            while (q.Count > 0)
            {
                int b = q.Dequeue();
                List<int> l; if (!rev.TryGetValue(b, out l)) continue;
                foreach (int a in l) if (!back[a]) { back[a] = true; q.Enqueue(a); }
            }

            // ── 틈 메우기: 위험한 칸에서 '못 서는 칸'(돌 · 폐자재 · 소품 · 비탈)을 따라 gapFill 만큼 위험을 번지게 한다 ──
            // 둑 위에 폐자재가 띠처럼 있으면 그 칸들은 '막힘'으로 보여 벽이 안 생기지만, 실제로는 틈 사이로 빠질 수 있다
            var backCell = new bool[nc];
            var trapCell = new bool[nc];
            foreach (int a in order) { if (back[a]) backCell[a / MaxL] = true; else trapCell[a / MaxL] = true; }
            var near = SpreadDanger(backCell, trapCell, inside, cnt, nx, nz, Mathf.RoundToInt(gapFill / cs));

            // ── 비탈 잘라내기: 위험한 곳으로 이어지는 비탈은 평지가 끝나는 자리까지 걸을 수 없는 곳으로 ──
            // 경계가 비탈 중간에 생기면 벽과 비탈 사이 좁은 틈에 플레이어가 끼거나 미끄러진다
            System.Func<int, int, bool> dangerEdge = (a, d) =>
            {
                int b = fwd[a * 4 + d];
                if (b == VOID || b == RISE || (b >= 0 && !back[b])) return true;
                if (b != NONE) return false;
                int c = a / MaxL, ib = c % nx + di4[d], jb = c / nx + dj4[d];
                return ib >= 0 && jb >= 0 && ib < nx && jb < nz && near[jb * nx + ib];
            };
            float cosFlat = Mathf.Cos(Mathf.Clamp(flatAngle, 1f, 89f) * Mathf.Deg2Rad);
            var cut = new bool[N];
            if (slopeBand > 0f)
            {
                int maxS = Mathf.Max(1, Mathf.RoundToInt(slopeBand / cs));
                var sd = new Dictionary<int, int>();
                foreach (int a in order)
                {
                    if (!back[a] || nrmY[a] >= cosFlat) continue;
                    for (int d = 0; d < 4; d++) if (dangerEdge(a, d)) { sd[a] = 0; cut[a] = true; q.Enqueue(a); break; }
                }
                while (q.Count > 0)
                {
                    int a = q.Dequeue();
                    int da = sd[a];
                    if (da >= maxS) continue;
                    for (int d = 0; d < 4; d++)
                    {
                        int b = fwd[a * 4 + d];
                        if (b >= 0 && back[b] && !cut[b] && nrmY[b] < cosFlat) { cut[b] = true; sd[b] = da + 1; q.Enqueue(b); }
                    }
                    List<int> l;
                    if (rev.TryGetValue(a, out l))
                        foreach (int b in l)
                            if (back[b] && !cut[b] && nrmY[b] < cosFlat) { cut[b] = true; sd[b] = da + 1; q.Enqueue(b); }
                }
                cut[start] = false;
            }

            // ── 최종 '다닐 곳' = 잘라 낸 비탈을 빼고, 시작 자리에서 갈 수 있고 돌아올 수도 있는 곳 ──
            var keep = new bool[N];
            {
                var fr = new bool[N];
                fr[start] = true; q.Enqueue(start);
                while (q.Count > 0)
                {
                    int a = q.Dequeue();
                    for (int d = 0; d < 4; d++)
                    {
                        int b = fwd[a * 4 + d];
                        if (b >= 0 && back[b] && !cut[b] && !fr[b]) { fr[b] = true; q.Enqueue(b); }
                    }
                }
                keep[start] = true; q.Enqueue(start);
                while (q.Count > 0)
                {
                    int b = q.Dequeue();
                    List<int> l; if (!rev.TryGetValue(b, out l)) continue;
                    foreach (int a in l) if (fr[a] && !keep[a]) { keep[a] = true; q.Enqueue(a); }
                }
            }

            // ── 틈 메우기를 최종 '다닐 곳' 기준으로 다시 — 잘라 낸 비탈 앞의 돌 · 폐자재 사이 틈도 막는다 ──
            var keepCell = new bool[nc];
            var badCell = new bool[nc];
            foreach (int a in order) { if (keep[a]) keepCell[a / MaxL] = true; else badCell[a / MaxL] = true; }
            var near2 = SpreadDanger(keepCell, badCell, inside, cnt, nx, nz, Mathf.RoundToInt(gapFill / cs));

            // ── 막을 경계 ──
            // key: 축(0 = x 고정선, 1 = z 고정선), 선 번호, 높이 층(다리 위 · 아래 구분용) → 칸 번호 목록
            var segs = new Dictionary<long, List<Vector2>>();
            int nBack = 0, nTrap = 0, nVoid = 0, nTrapEdge = 0, nRise = 0, nGap = 0, nCut = 0, nSlopeEdge = 0;
            foreach (int a in order)
            {
                if (!keep[a])
                {
                    int c = a / MaxL;
                    var pt = new Vector3(x0 + (c % nx + 0.5f) * cs, fl[a], z0 + (c / nx + 0.5f) * cs);
                    if (back[a]) { nCut++; if (slopePts.Count < 12000) slopePts.Add(pt); }
                    else { nTrap++; if (trapPts.Count < 12000) trapPts.Add(pt); }
                    continue;
                }
                nBack++;
                int ca = a / MaxL, ia = ca % nx, ja = ca / nx;
                for (int d = 0; d < 4; d++)
                {
                    int b = fwd[a * 4 + d];
                    bool block = b == VOID || b == RISE || (b >= 0 && !keep[b]);
                    bool gap = false;
                    if (!block && b == NONE)
                    {
                        int ib = ia + di4[d], jb = ja + dj4[d];
                        gap = ib >= 0 && jb >= 0 && ib < nx && jb < nz && near2[jb * nx + ib];
                    }
                    if (!block && !gap) continue;
                    if (gap) nGap++; else if (b == VOID) nVoid++; else if (b == RISE) nRise++; else if (back[b]) nSlopeEdge++; else nTrapEdge++;
                    int axis = d < 2 ? 0 : 1;
                    int line = axis == 0 ? ia + (d == 0 ? 1 : 0) : ja + (d == 2 ? 1 : 0);
                    int along = axis == 0 ? ja : ia;
                    int hb = Mathf.FloorToInt(fl[a] / 2.5f);   // 다리 위 · 아래처럼 크게 다른 층만 나눈다
                    long key = ((long)axis << 50) | ((long)(line & 0xFFFFF) << 28) | (long)(hb + 100000);
                    List<Vector2> l; if (!segs.TryGetValue(key, out l)) segs[key] = l = new List<Vector2>();
                    l.Add(new Vector2(along, fl[a]));
                }
            }

            // ── 붙은 칸끼리 한 상자로 ──
            const float thick = 0.1f;
            foreach (var kv in segs)
            {
                int axis = (int)(kv.Key >> 50);
                int line = (int)((kv.Key >> 28) & 0xFFFFF);
                var l = kv.Value;
                l.Sort((u, v) => u.x.CompareTo(v.x));
                int s = 0;
                while (s < l.Count)
                {
                    int e = s; float lo = l[s].y, hi = l[s].y;
                    while (e + 1 < l.Count && (int)l[e + 1].x <= (int)l[e].x + 1)
                    {
                        float y = l[e + 1].y;
                        if (Mathf.Max(hi, y) - Mathf.Min(lo, y) > 2f) break;
                        e++; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
                    }
                    float k0 = l[s].x, k1 = l[e].x + 1f;
                    float bottom = lo - 0.3f, top = hi + wallHeight;
                    float along0 = (axis == 0 ? z0 : x0) + k0 * cs, along1 = (axis == 0 ? z0 : x0) + k1 * cs;
                    float lp = (axis == 0 ? x0 : z0) + line * cs;
                    Vector3 center = axis == 0
                        ? new Vector3(lp, (bottom + top) * 0.5f, (along0 + along1) * 0.5f)
                        : new Vector3((along0 + along1) * 0.5f, (bottom + top) * 0.5f, lp);
                    Vector3 size = axis == 0
                        ? new Vector3(thick, top - bottom, along1 - along0 + thick)
                        : new Vector3(along1 - along0 + thick, top - bottom, thick);
                    boxes.Add(new Bounds(center, size));
                    s = e + 1;
                }
            }

            FindNoCollider(fl, cnt, keep, nx, nz, x0, z0, cs, cc);

            float a2 = cs * cs;
            report = string.Format(
                "격자 {0:0.##} m · 걸어갈 수 있는 곳 {1:0} m²\n" +
                "맵 밖으로 떨어지는 경계 {2}칸 · 들어가면 못 돌아오는 곳 {3:0.#} m² (경계 {4}칸) · 틈 메움 {8}칸{5}\n" +
                "잘라 낸 비탈 {9:0.#} m² (평지 가장자리 벽 {10}칸)\n" +
                "→ 세울 벽 {6}개 · 통과되는 소품 {7}개",
                cs, nBack * a2, nVoid, nTrap * a2, nTrapEdge,
                maxRise > 0f ? " · 윗단 경계 " + nRise + "칸" : "",
                boxes.Count, noCollider.Count, nGap, nCut * a2, nSlopeEdge);
            analyzed = true;
        }

        /// <summary>
        /// 위험한 칸(지형 밖 · 바닥 없음 · bad 노드가 있는 칸 — 단 good 노드가 있는 칸은 제외)에서 시작해
        /// good 노드가 없는 칸(돌 · 폐자재 · 소품 · 비탈)을 따라 maxStep 칸까지 위험을 번지게 한다
        /// </summary>
        static bool[] SpreadDanger(bool[] goodCell, bool[] badCell, bool[] inside, byte[] cnt, int nx, int nz, int maxStep)
        {
            int nc = nx * nz;
            var near = new bool[nc];
            var dist = new int[nc];
            var fq = new Queue<int>();
            int[] di = { 1, -1, 0, 0 }, dj = { 0, 0, 1, -1 };
            for (int c = 0; c < nc; c++)
            {
                dist[c] = -1;
                if (goodCell[c]) continue;
                if (!inside[c] || cnt[c] == 0 || badCell[c]) { dist[c] = 0; near[c] = true; fq.Enqueue(c); }
            }
            while (fq.Count > 0)
            {
                int c = fq.Dequeue();
                if (dist[c] >= maxStep) continue;
                int ic = c % nx, jc = c / nx;
                for (int d = 0; d < 4; d++)
                {
                    int i2 = ic + di[d], j2 = jc + dj[d];
                    if (i2 < 0 || j2 < 0 || i2 >= nx || j2 >= nz) continue;
                    int c2 = j2 * nx + i2;
                    if (dist[c2] >= 0 || goodCell[c2]) continue;
                    dist[c2] = dist[c] + 1; near[c2] = true; fq.Enqueue(c2);
                }
            }
            return near;
        }

        static readonly string[] SkipTypes =
        {
            "RSFogVolume", "RSGodRays", "RSLightShafts", "RSBackdrop", "RSWetSurface", "RSWaterReflection",
            "RSPlant", "RSPlantBatch", "RSIndirectLight", "RSCharacterGlow", "RSBlobShadow", "RSTerrain",
            "RSCloudShadow", "RSSkyTimeOfDay",
        };

        void FindNoCollider(float[] fl, byte[] cnt, bool[] back, int nx, int nz, float x0, float z0, float cs, CharacterController cc)
        {
            var seen = new HashSet<GameObject>();
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!Alive(r) || !r.enabled) continue;
                if (r is SpriteRenderer || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (r.transform.IsChildOf(cc.transform)) continue;
                if (r.GetComponent<Collider>() != null || r.GetComponentInParent<Collider>() != null) continue;
                var sm = r.sharedMaterial;
                if (sm != null && sm.renderQueue >= 2500) continue;           // 반투명 (안개 · 빛줄기 · 유리)

                GameObject owner = r.gameObject;
                bool skip = false;
                foreach (var mb in r.GetComponentsInParent<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    string tn = mb.GetType().Name;
                    if (System.Array.IndexOf(SkipTypes, tn) >= 0) { skip = true; break; }
                    if (tn == "RSAssembly" || tn == "RSEditMesh") { owner = mb.gameObject; break; }
                }
                if (skip) continue;
                if ((owner.hideFlags & HideFlags.DontSave) != 0) continue;
                // RSAssembly 의 콜라이더는 숨은 자식(조립 메시)에 붙는다
                if (owner != r.gameObject && owner.GetComponentInChildren<Collider>(true) != null) continue;

                var b = r.bounds;
                if (b.size.y < 0.2f) continue;                                // 납작한 것 (바닥 무늬 · 웅덩이)
                int i0 = Mathf.Max(0, Mathf.FloorToInt((b.min.x - x0) / cs)), i1 = Mathf.Min(nx - 1, Mathf.FloorToInt((b.max.x - x0) / cs));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((b.min.z - z0) / cs)), j1 = Mathf.Min(nz - 1, Mathf.FloorToInt((b.max.z - z0) / cs));
                bool hitWalk = false;
                for (int j = j0; j <= j1 && !hitWalk; j++)
                for (int i = i0; i <= i1 && !hitWalk; i++)
                {
                    int c = j * nx + i;
                    for (int k = 0; k < cnt[c]; k++)
                    {
                        int id = c * MaxL + k;
                        if (!back[id]) continue;
                        float h = fl[id];
                        if (b.min.y < h + ph * 0.9f && b.max.y > h + pstep + 0.05f) { hitWalk = true; break; }
                    }
                }
                if (hitWalk && seen.Add(owner)) noCollider.Add(owner);
            }
            noCollider.Sort((p, q) => string.CompareOrdinal(p.name, q.name));
        }

        // ─────────────────────────────────────────────────────────────
        // 벽 세우기 / 지우기
        // ─────────────────────────────────────────────────────────────

        void Commit(bool clear)
        {
            var scene = SceneManager.GetActiveScene();
            var terrain = MainTerrain();
            GameObject root = terrain != null ? PrefabUtility.GetOutermostPrefabInstanceRoot(terrain.gameObject) : null;
            string path = root != null ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) : null;

            GameObject blk = null;
            if (root != null) { var t = root.transform.Find(BlockName); if (t != null) blk = t.gameObject; }
            else foreach (var g in scene.GetRootGameObjects()) if (g.name == BlockName) { blk = g; break; }

            if (blk == null)
            {
                if (clear) { report = "세운 벽이 없습니다."; return; }
                blk = new GameObject(BlockName);
                Undo.RegisterCreatedObjectUndo(blk, "이동 막기 벽");
                if (root != null) blk.transform.SetParent(root.transform, false);
                else SceneManager.MoveGameObjectToScene(blk, scene);
            }
            var comp = blk.GetComponent<RSBlockWalls>();
            if (comp == null) comp = Undo.AddComponent<RSBlockWalls>(blk);

            Undo.RecordObject(comp, "이동 막기 벽");
            comp.boxes = new List<RSBlockWalls.Box>();
            string warn = "";
            if (!clear)
            {
                var tr = blk.transform;
                if (Quaternion.Angle(tr.rotation, Quaternion.identity) > 0.1f) warn = "\n※ 맵 루트가 회전돼 있어 벽이 비스듬할 수 있습니다 (루트 회전 0 권장)";
                var sc = tr.lossyScale;
                foreach (var b in boxes)
                {
                    Vector3 lc = tr.InverseTransformPoint(b.center);
                    Vector3 lsz = new Vector3(b.size.x / Mathf.Abs(sc.x), b.size.y / Mathf.Abs(sc.y), b.size.z / Mathf.Abs(sc.z));
                    // 손으로 옮기거나 지운 자동 벽 자리는 잘라 낸다
                    foreach (var piece in RSBlockWallsUtil.Clip(new RSBlockWalls.Box(lc, lsz), comp.suppressed)) comp.boxes.Add(piece);
                }
            }
            EditorUtility.SetDirty(comp);

            string where = RSBlockWallsUtil.ApplyToPrefab(comp);
            comp.Build();
            EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
            int nm = comp.manual != null ? comp.manual.Count : 0;
            report = (clear ? "자동 벽을 모두 지웠습니다" : "자동 벽 " + comp.boxes.Count + "개를 세웠습니다") +
                     (nm > 0 ? " (수동 벽 " + nm + "개는 그대로)" : "") + " → " + where + warn +
                     "\n씬을 저장(Ctrl+S)하세요. 다시 분석하면 세운 벽은 무시하고 처음부터 계산합니다.";
            if (clear) { boxes.Clear(); trapPts.Clear(); slopePts.Clear(); analyzed = false; }
        }
    }
}
