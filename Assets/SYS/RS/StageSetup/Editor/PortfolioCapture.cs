// 포트폴리오 캡처 — CAM_Stage 를 1920×1080 PNG 로 저장 (프로젝트 폴더/Captures, Assets 밖이라 임포트 안 됨)
// 메뉴: Tools → RE_AL STEEL → Capture
//  · 지금 화면 한 장          : 현재 씬 상태 그대로
//  · 룩 레이어 쌓기 (10장)    : RS 연출 컴포넌트를 하나씩 켜며 같은 프레임으로 촬영, 끝나면 원래대로 되돌림
//  · 시간대 4장               : 아침 · 한낮 · 노을 · 밤, 끝나면 원래 시각으로
//  · 인스펙터 창 캡처         : 2초 뒤 인스펙터 창 영역만 저장
// 씬은 저장하지 않는다. 켜고 끈 컴포넌트 · 시각은 끝나면 원래 값으로 돌려놓는다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealSteel.EditorTools
{
    public static class PortfolioCapture
    {
        const int W = 1920, H = 1080;
        const string Menu = "Tools/RE_AL STEEL/Capture/";

        static string Dir
        {
            get
            {
                string d = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));
                Directory.CreateDirectory(d);
                return d;
            }
        }

        static Camera FindCam()
        {
            var go = GameObject.Find("CAM_Stage");
            var c = go != null ? go.GetComponent<Camera>() : null;
            if (c == null) c = Camera.main;
            return c;
        }

        // ---------- 렌더 한 장
        public static string Shot(string fileName)
        {
            var cam = FindCam();
            if (cam == null) { Debug.LogError("[Capture] 카메라(CAM_Stage / MainCamera)를 못 찾음"); return null; }

            var desc = new RenderTextureDescriptor(W, H, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 };
            var rt = new RenderTexture(desc) { name = "RS_Capture" };
            rt.Create();
            try
            {
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req))
                    RenderPipeline.SubmitRenderRequest(cam, req);
                else
                {
                    var prev = cam.targetTexture;
                    cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
                }

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;

                string path = Path.Combine(Dir, fileName + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                Debug.Log("[Capture] 저장: " + path);
                return path;
            }
            finally
            {
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        [MenuItem(Menu + "지금 화면 한 장 (1920×1080)", false, 1)]
        static void ShotNow()
        {
            string f = "Shot_" + DateTime.Now.ToString("MMdd_HHmmss");
            Run(new[] { new Step { apply = null, file = f, wait = 1.5f } }, null);
        }

        // ---------- 촬영 시점: 씬 뷰를 따라가기 (켜면 CAM_Stage 를 잠깐 씬 뷰 위치로 옮겨 찍고 되돌린다)
        const string PrefKey = "RS_Capture_UseSceneView";
        static bool UseSceneView { get { return EditorPrefs.GetBool(PrefKey, false); } set { EditorPrefs.SetBool(PrefKey, value); } }

        [MenuItem(Menu + "촬영 시점: 씬 뷰 따라가기", false, 30)]
        static void ToggleSceneView()
        {
            UseSceneView = !UseSceneView;
            var sv = SceneView.lastActiveSceneView;
            var cam = FindCam();
            if (UseSceneView && sv != null && cam != null)
            {
                // 씬 뷰를 게임 카메라와 같은 화각 · 위치에서 시작 → 씬 뷰에서 구도를 잡으면 그대로 찍힌다
                sv.cameraSettings.fieldOfView = cam.fieldOfView;
                sv.cameraSettings.nearClip = cam.nearClipPlane;
                sv.cameraSettings.farClip = cam.farClipPlane;
                sv.orthographic = false;
                sv.AlignViewToObject(cam.transform);
                sv.Repaint();
            }
            Debug.Log("[Capture] 촬영 시점: " + (UseSceneView ? "씬 뷰 따라가기" : "게임 카메라 그대로"));
        }
        [MenuItem(Menu + "촬영 시점: 씬 뷰 따라가기", true)]
        static bool ToggleSceneViewCheck() { UnityEditor.Menu.SetChecked(Menu + "촬영 시점: 씬 뷰 따라가기", UseSceneView); return true; }

        [MenuItem(Menu + "선택한 오브젝트로 구도 잡기 (게임 카메라 각도)", false, 31)]
        static void FrameSelection()
        {
            var sv = SceneView.lastActiveSceneView; var cam = FindCam();
            if (sv == null || Selection.activeGameObject == null) return;
            var rs = Selection.activeGameObject.GetComponentsInChildren<Renderer>(false);
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var rot = cam != null ? cam.transform.rotation : sv.rotation;
            sv.LookAt(b.center, rot, b.extents.magnitude);
            sv.Repaint();
            Debug.Log("[Capture] 구도: " + Selection.activeGameObject.name + " 중심 " + b.center + " 크기 " + b.size);
        }

        static Camera poseCam; static Vector3 posePos; static Quaternion poseRot;
        static bool PoseBegin()
        {
            var sv = SceneView.lastActiveSceneView;
            poseCam = FindCam();
            if (sv == null || sv.camera == null || poseCam == null) return false;
            posePos = poseCam.transform.position; poseRot = poseCam.transform.rotation;
            poseCam.transform.SetPositionAndRotation(sv.camera.transform.position, sv.camera.transform.rotation);
            return true;
        }
        static void PoseEnd()
        {
            if (poseCam != null) poseCam.transform.SetPositionAndRotation(posePos, poseRot);
            poseCam = null;
        }

        [MenuItem(Menu + "캡처 폴더 열기", false, 50)]
        static void OpenDir() { EditorUtility.RevealInFinder(Dir + Path.DirectorySeparatorChar); }

        // ---------- 순서 실행 (에디터 프레임을 몇 번 돌려 연출이 갱신된 뒤 찍는다)
        class Step { public Action apply; public string file; public float wait; }
        static readonly Queue<Step> queue = new Queue<Step>();
        static double nextTime;
        static Step pending;
        static Action onDone;

        static void Run(IEnumerable<Step> steps, Action done)
        {
            if (queue.Count > 0 || pending != null) { Debug.LogWarning("[Capture] 이미 촬영 중"); return; }
            bool pose = UseSceneView && PoseBegin();
            foreach (var s in steps) queue.Enqueue(s);
            onDone = () => { try { done?.Invoke(); } finally { if (pose) PoseEnd(); } };
            nextTime = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Kick()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            InternalEditorUtility.RepaintAllViews();
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextTime) { Kick(); return; }

            if (pending != null)
            {
                if (!string.IsNullOrEmpty(pending.file)) Shot(pending.file);
                pending = null;
            }

            if (queue.Count == 0)
            {
                EditorApplication.update -= Tick;
                var d = onDone; onDone = null;
                try { d?.Invoke(); } finally { Kick(); }
                Debug.Log("[Capture] 끝 — " + Dir);
                return;
            }

            pending = queue.Dequeue();
            try { pending.apply?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            Kick();
            nextTime = now + pending.wait;
        }

        // ---------- 컴포넌트 묶음 (타입 이름으로 찾는다 — 어셈블리 참조 없이)
        static readonly string[][] Groups =
        {
            new[] { "RSTimeOfDay", "RSCharacterGlow" },          // 02 시간대 조명
            new[] { "RSCloudShadow" },                            // 03 구름 그림자
            new[] { "RSIndirectLight", "RSBounceSource" },        // 04 간접광
            new[] { "RSWetness", "RSWetSurface" },                // 05 젖은 바닥
            new[] { "RSWaterReflection", "#Water" },              // 06 물 (#Water = 물 셰이더 렌더러)
            new[] { "RSFogVolume" },                              // 07 안개
            new[] { "RSLightShafts" },                            // 08 햇살 빛줄기
            new[] { "RSGodRays" },                                // 09 갓레이
            new[] { "RSColorGrade", "RSAutoFocus", "#StageVolume" }, // 10 색감 · DOF
        };
        static readonly string[] StepNames =
        {
            "01_기본", "02_시간대조명", "03_구름그림자", "04_간접광", "05_젖은바닥",
            "06_물", "07_안개", "08_햇살빛줄기", "09_갓레이", "10_색감DOF"
        };

        static bool Visible(Component c)
        {
            return c != null && c.gameObject.scene.IsValid() && (c.gameObject.hideFlags & HideFlags.HideInHierarchy) == 0;
        }

        static List<(UnityEngine.Object obj, bool on)> Collect(string key)
        {
            var list = new List<(UnityEngine.Object, bool)>();
            if (key == "#Water")
            {
                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!Visible(r) || r.sharedMaterial == null || r.sharedMaterial.shader == null) continue;
                    if (r.sharedMaterial.shader.name.Contains("Water")) list.Add((r, r.enabled));
                }
                return list;
            }
            if (key == "#StageVolume")
            {
                foreach (var b in UnityEngine.Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!Visible(b) || b.GetType().Name != "Volume") continue;
                    list.Add((b, b.enabled));
                }
                return list;
            }
            foreach (var b in UnityEngine.Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (Visible(b) && b.GetType().Name == key) list.Add((b, b.enabled));
            return list;
        }

        static void SetOn(UnityEngine.Object o, bool on)
        {
            if (o is Behaviour b) { if (b) b.enabled = on; }
            else if (o is Renderer r) { if (r) r.enabled = on; }
        }

        [MenuItem(Menu + "룩 레이어 쌓기 (10장)", false, 10)]
        static void LayerStack()
        {
            // 묶음별로 원래 켜져 있던 것만 다룬다 (원래 꺼진 건 끝까지 그대로)
            var groups = Groups.Select(g => g.SelectMany(Collect).Where(x => x.on).Select(x => x.obj).ToList()).ToList();
            var all = Groups.SelectMany(g => g.SelectMany(Collect)).ToList();
            string stamp = DateTime.Now.ToString("MMdd_HHmm");

            var steps = new List<Step>();
            for (int i = 0; i <= Groups.Length; i++)
            {
                int n = i;
                steps.Add(new Step
                {
                    apply = () =>
                    {
                        for (int g = 0; g < groups.Count; g++)
                            foreach (var o in groups[g]) SetOn(o, g < n);
                    },
                    file = "Layer_" + StepNames[n] + "_" + stamp,
                    wait = n == 3 ? 4f : 2f   // 간접광은 계산 · 블렌드 시간
                });
            }
            Debug.Log("[Capture] 레이어 쌓기 시작 — 묶음: " + string.Join(" / ", groups.Select((g, i) => StepNames[i + 1] + " " + g.Count)));
            Run(steps, () => { foreach (var (obj, on) in all) SetOn(obj, on); });
        }

        // ---------- 시간대
        static Component TimeOfDay()
        {
            return UnityEngine.Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => Visible(b) && b.GetType().Name == "RSTimeOfDay");
        }

        [MenuItem(Menu + "시간대 4장 (아침 · 한낮 · 노을 · 밤)", false, 11)]
        static void TimeShots()
        {
            var tod = TimeOfDay();
            if (tod == null) { Debug.LogError("[Capture] RSTimeOfDay 없음"); return; }
            var t = tod.GetType();
            var setTime = t.GetMethod("SetTime", BindingFlags.Public | BindingFlags.Instance);
            var timeField = t.GetField("time", BindingFlags.Public | BindingFlags.Instance);
            float original = (float)timeField.GetValue(tod);
            string stamp = DateTime.Now.ToString("MMdd_HHmm");
            var hours = new (float h, string name)[] { (7.5f, "1_아침"), (12f, "2_한낮"), (18.3f, "3_노을"), (22f, "4_밤") };
            var steps = hours.Select(x => new Step
            {
                apply = () => setTime.Invoke(tod, new object[] { x.h }),
                file = "Time_" + x.name + "_" + stamp,
                wait = 3f
            });
            Run(steps, () => setTime.Invoke(tod, new object[] { original }));
        }

        // ---------- 인스펙터 창
        [MenuItem(Menu + "인스펙터 창 캡처 (2초 뒤)", false, 20)]
        static void InspectorShot()
        {
            var win = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.GetType().Name == "InspectorWindow");
            if (win == null) { Debug.LogError("[Capture] 인스펙터 창 없음"); return; }
            string stamp = DateTime.Now.ToString("MMdd_HHmmss");
            Run(new[]
            {
                new Step { apply = () => { win.Focus(); win.Repaint(); }, file = null, wait = 2f },
                new Step { apply = () => GrabWindow(win, "Inspector_" + stamp), file = null, wait = 0.1f },
            }, null);
        }

        static void GrabWindow(EditorWindow win, string fileName)
        {
            float ppp = EditorGUIUtility.pixelsPerPoint;
            Rect r = win.position;
            var px = new Vector2(r.x * ppp, r.y * ppp);
            int w = Mathf.RoundToInt(r.width * ppp), h = Mathf.RoundToInt(r.height * ppp);
            Color[] cols = InternalEditorUtility.ReadScreenPixel(px, w, h);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.SetPixels(cols);
            tex.Apply();
            string path = Path.Combine(Dir, fileName + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("[Capture] 저장: " + path);
        }
    }
}
