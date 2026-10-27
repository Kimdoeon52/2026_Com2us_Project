// RE:AL STEEL - 나노봇 리그 에디터 미리보기
//
// 리그(또는 그 자식)를 선택하면 에디터에서 봇을 돌린다. 선택을 풀면 봇을 치운다.
//   · 애니메이션 창 미리보기가 켜져 있으면 '재시뮬' — 창의 시각이 뒤로 가거나 건너뛰면 클립 처음부터 1/60초씩 다시 계산한다.
//     → 앞뒤로 끌어도, 한 프레임씩 넘겨도(, .) 그 프레임의 모습이 늘 같다. 키 · 값을 고치면 바로 다시 계산한다
//   · 애니메이션 창이 없으면 '실시간' — 지금 모양을 계속 따라간다 (부위 값을 바꿔 보며 느낌 잡기)
//   · 어니언 스킨 — 앞 · 뒤 프레임의 부위 윤곽을 겹쳐 그린다 (앞 = 주황, 뒤 = 초록)
//   · 리그 원점 · 플레이어 기준점에 키가 잡히면 지운다 (리그의 '원점 키 허용' 을 켜면 두기)
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RealSteel.NanoBoss.EditorTools
{
    [InitializeOnLoad]
    public static class RSNanoRigPreview
    {
        static RSNanoRig current;
        static double last;
        static float liveClock;

        static AnimationClip simClip;
        static int simFrame = -1;
        static bool dirty = true, pending;
        static double lastResim, playLast;
        static float lastWindowTime = -1f;

        /// <summary>동작 창의 재생 속도 (0 = 멈춤)</summary>
        public static float PlaybackSpeed;
        public static bool Loop = true;

        // 어니언 스킨
        public static int OnionCount = 1;
        public static int OnionStep = 6;
        public static bool OnionKeys;
        static readonly List<(Color color, Vector3[] pts)> onion = new List<(Color, Vector3[])>();
        static int onionFrame = -1;

        public static RSNanoRig Current => current;
        public static bool Scrubbing { get; private set; }
        public static int SimFrame => simFrame;
        public static float LastResimMs { get; private set; }

        static RSNanoRigPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingEditMode) Stop(); };
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            ObjectChangeEvents.changesPublished += OnChanges;
            Undo.undoRedoPerformed += () => dirty = true;
            AnimationUtility.onCurveWasModified += OnCurveModified;
            SceneView.duringSceneGui += DrawOnion;
        }

        public static void MarkDirty() { dirty = true; onionFrame = -1; }
        public static void RefreshOnion() { onionFrame = -1; }

        /// <summary>리그 안의 값 · 구조가 바뀌면 다시 계산 (플레이어 기준점이 움직이는 것은 무시)</summary>
        static void OnChanges(ref ObjectChangeEventStream stream)
        {
            if (!current) return;
            for (int i = 0; i < stream.length; i++)
            {
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var e);
                        Check(e.instanceId);
                        break;
                    case ObjectChangeKind.ChangeGameObjectStructure:
                        stream.GetChangeGameObjectStructureEvent(i, out var s);
                        Check(s.instanceId);
                        break;
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                        stream.GetChangeGameObjectStructureHierarchyEvent(i, out var h);
                        Check(h.instanceId);
                        break;
                    case ObjectChangeKind.ChangeGameObjectParent:
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                        dirty = true;
                        break;
                }
            }
        }

        static void Check(int id)
        {
            var o = EditorUtility.InstanceIDToObject(id);
            Transform t = o is Component c ? c.transform : o is GameObject g ? g.transform : null;
            if (!t || !current) return;
            if (current.playerAnchor && t == current.playerAnchor) return;
            if (t == current.transform || t.IsChildOf(current.transform) || (current.swarm && t == current.swarm.transform)) { dirty = true; onionFrame = -1; }
        }

        static void Stop()
        {
            if (current) current.StopPreview();
            current = null;
            simClip = null;
            simFrame = -1;
            onion.Clear();
            PlaybackSpeed = 0f;
            SceneView.RepaintAll();
        }

        public static void Restart()
        {
            if (!current) return;
            var r = current;
            Stop();
            current = r;
            r.BeginPreview();
            dirty = true;
        }

        static void Update()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var go = Selection.activeGameObject;
            var want = go ? go.GetComponentInParent<RSNanoRig>() : null;
            if (!want && EditorWindow.focusedWindow is RSNanoActionWindow && current) want = current;   // 동작 창을 만지는 동안은 유지
            if (want && !want.preview) want = null;
            if (want != current)
            {
                Stop();
                current = want;
                if (current) { last = EditorApplication.timeSinceStartup; liveClock = 0f; dirty = true; }
            }
            if (!current) return;

            var aw = RSNanoKeys.AnimWindow();
            AnimationClip clip = aw ? aw.animationClip : null;
            bool scrub = aw && aw.previewing && AnimationMode.InAnimationMode() && clip && RSNanoKeys.ClipBelongs(current, clip);
            Scrubbing = scrub;
            double now = EditorApplication.timeSinceStartup;

            if (scrub)
            {
                if (PlaybackSpeed > 0f)
                {
                    float t = aw.time + (float)(now - playLast) * PlaybackSpeed;
                    if (t > clip.length) t = Loop ? 0f : clip.length;
                    aw.time = t;
                    aw.Repaint();
                }
                playLast = now;
                float wt = aw.time;
                int target = Mathf.RoundToInt(wt * RSNanoKeys.Fps);
                bool need = dirty || pending || clip != simClip || !current.Active || target < simFrame || target - simFrame > 24;
                if (need) Resim(clip, target, wt);
                else if (target > simFrame || Mathf.Abs(wt - lastWindowTime) > 1e-4f) StepTo(clip, target, wt);
                if (OnionCount > 0 && (onionFrame != simFrame)) BuildOnion(clip, wt);
                if (OnionCount == 0) onion.Clear();
            }
            else
            {
                onion.Clear();
                simClip = null;
                simFrame = -1;
                PlaybackSpeed = 0f;
                if (!current.Active) current.BeginPreview();
                if (!current.swarm) return;
                float dt = Mathf.Clamp((float)(now - last), 0f, 0.05f);
                last = now;
                if (dt <= 0f) return;
                liveClock += dt;
                current.Tick(dt, liveClock);
                current.swarm.Simulate(dt, liveClock);
            }
            SceneView.RepaintAll();
        }

        static void Sample(AnimationClip clip, float t)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(current.gameObject, clip, Mathf.Clamp(t, 0f, clip.length));
            AnimationMode.EndSampling();
        }

        /// <summary>클립 처음부터 다시 계산</summary>
        static void Resim(AnimationClip clip, int target, float windowTime)
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - lastResim < 0.06 && simClip == clip && current.Active) { pending = true; return; }   // 끄는 중엔 1초 15번까지
            pending = false;
            dirty = false;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            simClip = clip;
            Sample(clip, 0f);
            current.BeginPreview();   // 봇을 새로 만들고 첫 프레임 모양으로 격자 자리를 깐다 (난수 씨앗 고정)
            if (!current.Active || !current.swarm) return;
            simFrame = 0;
            StepTo(clip, target, windowTime);
            onionFrame = -1;
            lastResim = EditorApplication.timeSinceStartup;
            LastResimMs = (float)sw.Elapsed.TotalMilliseconds;
        }

        static void StepTo(AnimationClip clip, int target, float windowTime)
        {
            const float dt = 1f / RSNanoKeys.Fps;
            for (int f = simFrame + 1; f <= target; f++)
            {
                float t = f * dt;
                Sample(clip, t);
                current.Tick(dt, t);
                current.swarm.Simulate(dt, t);
            }
            simFrame = Mathf.Max(simFrame, target);
            Sample(clip, windowTime);   // 창 시각으로 되돌린다
            lastWindowTime = windowTime;
        }

        // ─────────────────────────────────────────────────────────────
        // 어니언 스킨
        // ─────────────────────────────────────────────────────────────
        static void BuildOnion(AnimationClip clip, float windowTime)
        {
            onion.Clear();
            onionFrame = simFrame;
            var times = new List<(float t, int k)>();
            if (OnionKeys)
            {
                var keys = KeyTimes(clip);
                int before = 0, after = 0;
                for (int i = keys.Count - 1; i >= 0 && before < OnionCount; i--) if (keys[i] < windowTime - 1e-3f) times.Add((keys[i], -(++before)));
                for (int i = 0; i < keys.Count && after < OnionCount; i++) if (keys[i] > windowTime + 1e-3f) times.Add((keys[i], ++after));
            }
            else
            {
                for (int k = 1; k <= OnionCount; k++)
                {
                    float a = windowTime - k * OnionStep / RSNanoKeys.Fps, b = windowTime + k * OnionStep / RSNanoKeys.Fps;
                    if (a >= 0f) times.Add((a, -k));
                    if (b <= clip.length) times.Add((b, k));
                }
            }
            float floorY = FloorY(current);
            var limbs = current.GetComponentsInChildren<RSNanoLimb>(false);
            foreach (var (t, k) in times)
            {
                Sample(clip, t);
                float alpha = 0.55f / Mathf.Abs(k);
                Color c = k < 0 ? new Color(1f, 0.55f, 0.25f, alpha) : new Color(0.4f, 1f, 0.5f, alpha);
                foreach (var l in limbs)
                {
                    if (l.amount < 0.01f) continue;
                    var pts = new List<Vector3>();
                    RSNanoLimbShape.Outline(l, floorY, pts);
                    if (pts.Count > 1) { pts.Add(pts[0]); onion.Add((c, pts.ToArray())); }
                }
            }
            Sample(clip, windowTime);
            foreach (var l in limbs) { l.RefreshJoints(); l.CachePose(); }
        }

        static List<float> KeyTimes(AnimationClip clip)
        {
            var set = new SortedSet<float>();
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (b.propertyName == "danger" || b.propertyName == "shake") continue;
                var c = AnimationUtility.GetEditorCurve(clip, b);
                if (c == null) continue;
                foreach (var k in c.keys) set.Add(Mathf.Round(k.time * RSNanoKeys.Fps) / RSNanoKeys.Fps);
            }
            return new List<float>(set);
        }

        static void DrawOnion(SceneView sv)
        {
            if (onion.Count == 0 || !current || !Scrubbing) return;
            foreach (var (c, pts) in onion)
            {
                Handles.color = c;
                Handles.DrawAAPolyLine(2.5f, pts);
            }
        }

        public static float FloorY(RSNanoRig rig)
        {
            var sw = rig ? (rig.swarm ? rig.swarm : rig.GetComponentInParent<RSNanoSwarm>()) : null;
            if (!sw) return rig ? rig.transform.position.y - 2.1f : 0f;
            return sw.floorFromTransform ? sw.transform.position.y : sw.floorY;
        }

        // ─────────────────────────────────────────────────────────────
        // 리그 원점 · 플레이어 기준점 키 막기
        // ─────────────────────────────────────────────────────────────
        static void OnCurveModified(AnimationClip clip, EditorCurveBinding binding, AnimationUtility.CurveModifiedType type)
        {
            dirty = true;
            onionFrame = -1;
            if (type == AnimationUtility.CurveModifiedType.CurveDeleted || type == AnimationUtility.CurveModifiedType.ClipModified) return;
            var rig = current;
            if (!rig || rig.allowRootKeys || binding.type != typeof(Transform)) return;
            bool root = string.IsNullOrEmpty(binding.path);
            bool anchor = rig.playerAnchor && binding.path == RSNanoKeys.PathOf(rig.transform, rig.playerAnchor);
            if (!root && !anchor) return;
            if (!RSNanoKeys.ClipBelongs(rig, clip)) return;
            EditorApplication.delayCall += () =>
            {
                if (!clip) return;
                AnimationUtility.SetEditorCurve(clip, binding, null);
                Debug.LogWarning("[나노봇 리그] " + (root ? "리그 원점" : "플레이어 기준점") + " 에 잡힌 키(" + binding.propertyName + ")를 지웠습니다. " +
                                 "원점은 플레이 중 보스 몸 중심으로 옮겨지고, 플레이어 기준점은 플레이어 발밑을 따라가서 키가 덮어써집니다. " +
                                 "부위를 움직이려면 부위(자식)에 키를 잡으세요. (꼭 필요하면 리그의 '원점 키 허용')", rig);
            };
        }
    }

    /// <summary>부위 윤곽 — 씬 뷰 실루엣 · 어니언 스킨이 함께 쓴다</summary>
    public static class RSNanoLimbShape
    {
        const int N = 24;

        /// <summary>닫힌 윤곽 (마지막 점 → 첫 점으로 닫힌다고 본다)</summary>
        public static void Outline(RSNanoLimb l, float floorY, List<Vector3> pts)
        {
            pts.Clear();
            l.RefreshJoints();
            l.CachePose();
            switch (l.type)
            {
                case LimbType.Chain:
                {
                    var left = new Vector3[N + 1];
                    var right = new Vector3[N + 1];
                    Strip(l, floorY, left, right);
                    for (int i = 0; i <= N; i++) pts.Add(left[i]);
                    for (int i = N; i >= 0; i--) pts.Add(right[i]);
                    break;
                }
                case LimbType.Blob:
                    for (int i = 0; i < 40; i++)
                    {
                        float a = i / 40f * Mathf.PI * 2f;
                        pts.Add(l.transform.TransformPoint(new Vector3(Mathf.Cos(a) * l.size.x, Mathf.Sin(a) * l.size.y, 0f)));
                    }
                    break;
                default:
                {
                    Vector3 c = l.transform.position;
                    float y = floorY + l.layer + 0.08f;
                    float hx = l.extent.x * 0.5f, hz = l.extent.y * 0.5f;
                    pts.Add(new Vector3(c.x - hx, y, c.z - hz));
                    pts.Add(new Vector3(c.x + hx, y, c.z - hz));
                    pts.Add(new Vector3(c.x + hx, y, c.z + hz));
                    pts.Add(new Vector3(c.x - hx, y, c.z + hz));
                    break;
                }
            }
        }

        /// <summary>줄기의 양쪽 가장자리 (바닥 채움이면 위 = 물마루, 아래 = 바닥)</summary>
        public static void Strip(RSNanoLimb l, float floorY, Vector3[] left, Vector3[] right)
        {
            int n = left.Length - 1;
            float g = Mathf.Clamp01(l.grow);
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n * g;
                l.Sample(u, out Vector3 p, out float r);
                l.Sample(Mathf.Min(1f, u + 0.01f), out Vector3 p2, out _);
                if (u >= 0.99f) { l.Sample(u - 0.01f, out Vector3 p0, out _); p2 = p + (p - p0); }
                if (l.fillToFloor)
                {
                    left[i] = new Vector3(p.x, p.y + r, p.z);
                    right[i] = new Vector3(p.x, floorY + 0.1f, p.z);
                }
                else
                {
                    Vector3 tan = p2 - p;
                    if (tan.sqrMagnitude < 1e-8f) tan = Vector3.up;
                    Vector3 nrm = Vector3.Cross(tan.normalized, Vector3.forward).normalized;
                    left[i] = p + nrm * r;
                    right[i] = p - nrm * r;
                }
            }
        }

        /// <summary>씬 뷰 채우기</summary>
        public static void Fill(RSNanoLimb l, float floorY, Color fill, Color line)
        {
            var pts = new List<Vector3>();
            if (l.type == LimbType.Chain)
            {
                var left = new Vector3[N + 1];
                var right = new Vector3[N + 1];
                l.RefreshJoints();
                l.CachePose();
                Strip(l, floorY, left, right);
                Handles.color = fill;
                for (int i = 0; i < N; i++) Handles.DrawAAConvexPolygon(left[i], left[i + 1], right[i + 1], right[i]);
                Handles.color = line;
                Handles.DrawAAPolyLine(2f, left);
                Handles.DrawAAPolyLine(2f, right);
                Handles.DrawAAPolyLine(2f, left[0], right[0]);
                Handles.DrawAAPolyLine(2f, left[N], right[N]);
                return;
            }
            Outline(l, floorY, pts);
            if (pts.Count < 3) return;
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(pts.ToArray());
            pts.Add(pts[0]);
            Handles.color = line;
            Handles.DrawAAPolyLine(2f, pts.ToArray());
        }
    }
}
