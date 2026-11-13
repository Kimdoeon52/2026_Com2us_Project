// RE:AL STEEL - 고물상 꾸미기 (Map_Test 에 소품 층을 얹는다)
//
// 메뉴: Tools → RE_AL STEEL → Stage → 고물상 꾸미기 (열린 씬에)
//  · 'JUNKYARD_꾸미기' 하나를 만든다. 지형 · 기존 오브젝트 · 조명은 건드리지 않는다 (지우면 원래대로)
//  · 다시 누르면 지우고 새로 만든다 → 손으로 옮긴 위치가 사라지니, 손본 뒤에는 다시 누르지 말 것
//  · 소품은 지형 높이를 읽어 놓는다. 위치는 지형 기능(길 · 배수로 · 더미 · 작업장)의 좌표를 보고 정한 것이라
//    실제 화면에서 겹치거나 뜬 곳이 있으면 그 소품만 골라 옮기면 된다 (콘솔에 경사가 심한 자리를 알려준다)
//  · 화면 구성 원칙: 카메라는 늘 북쪽을 본다 → 길의 북쪽에 키 큰 것, 길 남쪽(카메라 쪽)에는 낮은 것만
//  · 밤 조명(RSNightLamp)은 시간대가 17.8시 ~ 6.5시일 때 켜진다. 점광은 모두 8개
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RealSteel.Build;
using RealSteel.Common;
using RealSteel.Common.EditorTools;
using RealSteel.Lighting;
using RealSteel.Terrain;

public static partial class RSJunkyard
{
    public const string RootName = "JUNKYARD_꾸미기";

    // 지형 기능 좌표 (Map_Test 의 예제_폐철장 에서 읽은 값) — 잡동사니를 뿌릴 때 피한다
    static readonly Vector2[][] Paths =
    {
        new[] { V(-5.5f, -21), V(-4.5f, -16), V(-2, -11), V(-0.4f, -7), V(0, -4.8f), V(0, -3.2f) },
        new[] { V(-2, -11), V(3, -13.2f), V(8.5f, -13.6f) },
        new[] { V(0, 0.6f), V(0.6f, 2.4f), V(2.8f, 3.8f), V(5, 5), V(5, 10.4f), V(1, 11.8f), V(-6, 11.4f), V(-9, 11.4f), V(-9, 16.2f), V(-7, 18.5f), V(-5, 21) },
    };
    static readonly Vector2[][] Trenches =
    {
        new[] { V(-21, 0.5f), V(-18, -0.8f), V(-15, -1.4f), V(-12, -1.7f), V(-9, -2.5f), V(-6, -1.5f), V(-2.85f, -0.85f), V(3.4f, -1f),
                V(5.8f, 0f), V(8.8f, -1.6f), V(11.8f, -2.1f), V(16.3f, -1.9f), V(19.3f, -1.6f), V(21, -0.7f) },
    };
    // 더미 · 웅덩이: x, z, 반지름
    static readonly Vector3[] Blobs =
    {
        new Vector3(-15, 12.2f, 2.6f), new Vector3(13, 5.6f, 2.6f), new Vector3(17.2f, -18.2f, 1.8f), new Vector3(-7, -5.8f, 1.5f),
        new Vector3(6, 18.2f, 3f), new Vector3(-15, -7.6f, 2.4f), new Vector3(3, -17.5f, 2.2f), new Vector3(-11, -19, 1.8f),
        new Vector3(7.4f, -4.6f, 1.4f), new Vector3(13, 12.2f, 2.2f), new Vector3(-3.8f, 6.4f, 1.6f), new Vector3(-16, 18.5f, 2f),
        new Vector3(1, 7.2f, 1.2f), new Vector3(9, -17, 1.5f), new Vector3(5, -8, 1.7f), new Vector3(-6.5f, -11, 1.3f),
        new Vector3(2, 14, 1.2f), new Vector3(-1, 18, 1.5f),
    };

    static Vector2 V(float x, float z) { return new Vector2(x, z); }

    [MenuItem("Tools/RE_AL STEEL/Stage/고물상 꾸미기 (열린 씬에)", false, 8)]
    static void Menu()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "Map_Test" &&
            !EditorUtility.DisplayDialog("고물상 꾸미기", "지금 열린 씬은 '" + scene.name + "' 입니다. 소품 위치는 Map_Test 지형에 맞춰져 있습니다. 그래도 만들까요?", "만들기", "취소"))
            return;
        var old = GameObject.Find(RootName);
        if (old != null)
        {
            if (!EditorUtility.DisplayDialog("고물상 꾸미기", "'" + RootName + "' 가 이미 있습니다. 지우고 새로 만들면 손으로 옮긴 위치가 사라집니다.", "지우고 새로", "취소"))
                return;
            Undo.DestroyObjectImmediate(old);
        }
        var go = Build();
        if (go == null) return;
        Undo.RegisterCreatedObjectUndo(go, "고물상 꾸미기");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = go;
    }

    static bool LoadSurfaces()
    {
        surf = new Dictionary<string, RSSurface>();
        AssetDatabase.Refresh();
        string texDir = RSPaths.Textures + "/Build/고물상";
        if (!AssetDatabase.IsValidFolder(texDir))
        {
            EditorUtility.DisplayDialog("고물상 꾸미기", "텍스처 폴더가 없습니다: " + texDir, "확인");
            return false;
        }
        string dataDir = RSSurfaceTools.SurfacesRoot + "/고물상";
        RSPaths.Ensure(dataDir);
        string setPath = RSSurfaceTools.SurfacesRoot + "/고물상.asset";
        var set = AssetDatabase.LoadAssetAtPath<RSSurfaceSet>(setPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<RSSurfaceSet>();
            set.note = "고물상 꾸미기 소품 재질 (Textures/Build/고물상)";
            AssetDatabase.CreateAsset(set, setPath);
        }
        foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }))
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g));
            if (tex == null) continue;
            var s = RSSurfaceTools.CreateFromTexture(tex, dataDir);
            if (s == null) continue;
            surf[tex.name] = s;
            if (!set.surfaces.Contains(s)) set.surfaces.Add(s);
        }
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        return surf.Count > 0;
    }

    // ─────────────────────────────────────────────────────────────
    // 빈 자리 찾기 (잡동사니 뿌리기)
    // ─────────────────────────────────────────────────────────────

    static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    static float DistPoly(Vector2 p, Vector2[] poly)
    {
        float d = float.MaxValue;
        for (int i = 0; i + 1 < poly.Length; i++) d = Mathf.Min(d, DistSeg(p, poly[i], poly[i + 1]));
        return d;
    }

    static bool Free(float x, float z, float r)
    {
        var p = new Vector2(x, z);
        foreach (var path in Paths) if (DistPoly(p, path) < 1.9f + r) return false;
        foreach (var t in Trenches) if (DistPoly(p, t) < 2.9f + r) return false;
        foreach (var b in Blobs) if (Vector2.Distance(p, new Vector2(b.x, b.y)) < b.z * 0.9f + r) return false;
        if (x > -15.4f && x < -8.2f && z > 1.2f && z < 8.4f) return false;      // 주인공 집 컨테이너
        if (Mathf.Abs(x) < 3.2f && z > -4.8f && z < 2.4f) return false;        // 다리
        if (x > 7.6f && x < 15.4f && z > -16.5f && z < -11f) return false;     // 앞 작업장
        if (Vector2.Distance(p, new Vector2(0f, -6.2f)) < 2.2f) return false;  // 시작 위치
        foreach (var u in used) if (Vector2.Distance(p, new Vector2(u.x, u.y)) < u.z + r + 0.25f) return false;
        float h = H(x, z);
        const float e = 0.45f;
        if (Mathf.Abs(H(x + e, z) - h) > 0.13f || Mathf.Abs(H(x - e, z) - h) > 0.13f ||
            Mathf.Abs(H(x, z + e) - h) > 0.13f || Mathf.Abs(H(x, z - e) - h) > 0.13f) return false;   // 비탈 · 절벽
        return true;
    }

    static int Clutter(int count)
    {
        int placed = 0, tries = 0;
        while (placed < count && tries < count * 60)
        {
            tries++;
            float x = Rand(-18.4f, 18.4f), z = Rand(-18.4f, 18.4f);
            if (!Free(x, z, 0.6f)) continue;
            Asm a;
            float r = 0.5f;
            switch (rng.Next(11))
            {
                case 0: case 1: a = Drums(1 + rng.Next(2), rng.Next(3) == 0); r = 0.6f; break;
                case 2: case 3: a = Tires(1 + rng.Next(3)); break;
                case 4: a = CinderBlocks(); r = 0.7f; break;
                case 5: a = Cartons(); r = 0.8f; break;
                case 6: a = LeanSheet(); r = 0.8f; break;
                case 7: a = Pallets(1 + rng.Next(2), rng.Next(3)); r = 0.8f; break;
                case 8: case 9: a = RobotScrap(rng.Next(3)); r = 0.7f; break;
                default: a = Cones(1 + rng.Next(2)); break;
            }
            Put(a, x, z, Rand(0f, 360f), r);
            placed++;
        }
        return placed;
    }

    // ─────────────────────────────────────────────────────────────
    // 배치
    // ─────────────────────────────────────────────────────────────

    /// <summary>지금 열린(활성) 씬에 고물상 소품 층을 만든다</summary>
    public static GameObject Build()
    {
        missing.Clear(); warn.Clear(); used.Clear();
        rng = new System.Random(20261112);
        if (!LoadSurfaces()) return null;
        terrain = RSTerrain.All.Count > 0 ? RSTerrain.All[0] : Object.FindFirstObjectByType<RSTerrain>();
        if (terrain == null) warn.Add("지형(RSTerrain)을 못 찾음 — 모든 소품을 높이 0 에 놓았습니다");
        glowWarm = GlowMat("MAT_Glow_전구색", new Color(3.4f, 2.3f, 1.1f, 1f));
        glowCool = GlowMat("MAT_Glow_화면색", new Color(1.6f, 2.2f, 2.6f, 1f));
        glowAmber = GlowMat("MAT_Glow_표시등", new Color(2.6f, 1.4f, 0.3f, 1f));

        var rootGo = new GameObject(RootName);
        root = rootGo.transform;

        // ── 둘레: 남쪽은 철망(카메라 앞이라 비쳐 보이게), 나머지는 골함석 ──
        Fence("울타리_남_왼", V(-19.5f, -19.5f), V(-7.6f, -19.5f), true, 56);
        Fence("울타리_남_오른", V(-2.8f, -19.5f), V(19.5f, -19.5f), true, 56);
        Fence("울타리_서_아래", V(-19.5f, -19.5f), V(-19.5f, -3.4f), false, 72);
        Fence("울타리_서_집옆", V(-19.5f, 1.8f), V(-19.5f, 8.4f), false, 72);
        Fence("울타리_서_위", V(-19.5f, 10f), V(-19.5f, 19.5f), false, 72);
        Fence("울타리_동_위", V(19.5f, 19.5f), V(19.5f, 10f), false, 72);
        Fence("울타리_동_가운데", V(19.5f, 8.4f), V(19.5f, 1.8f), false, 72);
        Fence("울타리_동_아래", V(19.5f, -3.6f), V(19.5f, -19.5f), false, 72);
        Fence("울타리_북_왼", V(-19.5f, 19.5f), V(-8.2f, 19.5f), false, 76);
        Fence("울타리_북_오른", V(-4.4f, 19.5f), V(19.5f, 19.5f), false, 76);

        // ── 입구 (남쪽 길) ──
        Put(Gate(140), -5.2f, -19.5f, 0, 1f);
        Raise(PutGo(SignSlot("고물상 간판 (3 × 1 m)", "간판_고물상", 96, 32, "고물상 간판. 진짜 간판을 자식으로 넣으면 임시 그림이 꺼진다"), -10.6f, -19.62f, 0, 0.3f, 0f), 0.5f);
        Raise(PutGo(SignSlot("매입 안내판", "간판_매입", 80, 16, "매입 안내 문구"), -14.4f, -19.62f, 0, 0.3f, 0f), 0.75f);
        Raise(PutGo(SignSlot("리그 포스터", "포스터_리그", 24, 32, "로봇 리그 포스터"), 5.6f, -19.62f, 0, 0.3f, 0f), 0.45f);
        Put(Vending(), -1.6f, -15.4f, 0, 0.7f);
        Put(Bench(), 0.3f, -15.3f, -6, 0.8f);
        Put(StreetLamp(), -6.6f, -13.4f, 0, 0.3f);
        Put(Car("차체_청"), -9.8f, -14.8f, 18, 2.2f);
        Put(Tires(3), -7.1f, -16.3f, 0, 0.5f);
        Put(Drums(2), -8.2f, -12.7f, 30, 0.6f);
        Put(Containers("컨테이너_적", "컨테이너_황", 16, 6), -15f, -13.6f, 0, 3.2f);
        Put(ArrowBoard(), 1.1f, -9.5f, 0, 0.5f);

        // ── 다리 앞 (아래 마당) ──
        Put(Pallets(3, 3), -5.4f, -8.2f, 12, 0.9f);
        Put(Cones(2), -3.4f, -6.2f, 20, 0.4f);
        Put(TarpPile("방수포_청"), -10.6f, -8.4f, 0, 1.4f);
        Put(Dumpster(), -10.9f, -5.7f, 4, 1.7f);
        Put(Drums(3, true), 2.6f, -6.2f, 0, 0.9f);
        Put(Spool(), 3.4f, -10.2f, 15, 0.7f);
        Put(Pipes(), 6.8f, -10.9f, 8, 1.6f);
        Put(StreetLamp(), -1.9f, -4.7f, 0, 0.3f);
        Put(SignPost("표지_경고"), 1.9f, -4.5f, 0, 0.2f);
        Put(Cubes(3, 2), 9.7f, -8.6f, 0, 2f);
        Put(Containers("컨테이너_청"), 16f, -7.3f, -4, 3.1f);
        Put(Tires(4), 12.6f, -6.3f, 0, 0.5f);

        // ── 앞 작업장 ──
        Put(Shed(150, 96, 92, "골함석_청", 2f), 12.9f, -12.6f, 0, 0.3f);
        Put(Car("차체_적"), 12.9f, -12.5f, 0, 0.3f);
        Put(Workbench(true), 8.9f, -11.1f, 0, 1.1f);
        Put(Compactor(), 17.4f, -12.9f, -8, 1.8f);
        Put(Barricade(), 15.9f, -15.5f, -12, 0.8f);
        Put(GasCylinders(), 10.3f, -10.9f, 0, 0.4f);

        // ── 다리 건너 · 동쪽 마당 ──
        Put(Cubes(3, 2, 1), 8.6f, 7.4f, 0, 2f);
        Put(Appliances(), 16.4f, 3.4f, -10, 1.6f);
        Put(Beams(), 16.6f, 7f, 6, 1.6f);
        Put(Tires(2), 7.1f, 3.3f, 0, 0.5f);
        Put(Cones(2), 2.3f, 2.6f, -30, 0.4f);

        // ── 주인공 집 마당 (컨테이너 동쪽) ──
        var canopy = Shed(110, 100, 84, "방수포_올리브", 1.8f);
        canopy.go.name = "집 마당 차양";
        Put(canopy, -6.6f, 5.9f, 0, 0.3f);
        PutGo(RobotStand(), -6.2f, 6.9f, -12, 0.9f, 0.02f);
        Put(Workbench(false), -7.2f, 5f, 0, 1f);
        Put(GasCylinders(), -5.5f, 5.1f, 0, 0.4f);
        Put(Deck(), -6.5f, 2.7f, 0, 1.3f);
        Put(Mailbox(), -8f, 1.9f, 0, 0.2f);
        var pole = WoodPole(84);
        Put(pole, -4.8f, 2.1f, 0, 0.2f);
        StringLights("줄전구_마당", At(canopy, -55, 86, -50), At(pole, 0, 82, 0), 6, 0.3f, true);
        StringLights("줄전구_차양", At(pole, 0, 82, 0), At(canopy, 55, 96, 50), 7, 0.35f, false);
        // 컨테이너 서쪽
        Put(Solar(2), -17.3f, 6.9f, 0, 1.4f);
        Put(WaterTank(), -16.3f, 2.7f, 0, 0.8f);
        Put(Laundry(), -17.1f, 4.7f, 4, 0.5f);

        // ── 가운데 단 (높이 2.4 m) ──
        Put(Cubes(4, 3, 2, 1), -3.6f, 13.6f, 0, 2.4f);
        Put(StreetLamp(), 2.4f, 13.2f, 180, 0.3f);
        Put(Pallets(2, 2), 0.3f, 13.6f, -8, 0.8f);
        Put(Crane(180), 13f, 12.2f, 0, 0.3f);
        Put(Drums(3), 7.6f, 13.5f, 0, 0.8f);
        Put(Barricade(), 7f, 10.9f, 20, 0.8f);
        Put(TarpPile("방수포_올리브"), -12f, 13.4f, 0, 1.3f);

        // ── 위 단 (높이 4.2 m) — 전봇대 · 광고판 · 뒷담 ──
        var p1 = Pole(true); Put(p1, -12.6f, 18.8f, 0, 0.3f);
        var p2 = Pole(false); Put(p2, -2.4f, 18.9f, 0, 0.3f);
        var p3 = Pole(true); Put(p3, 16.8f, 18.95f, 0, 0.3f);
        var pm = Pole(false); Put(pm, -7.3f, 13.7f, 35, 0.3f);
        var wires = WireHost("전선");
        for (int s = -1; s <= 1; s += 2)
        {
            Wire(wires, At(p1, s * 26, 231, 0), At(p2, s * 26, 231, 0), 0.7f);
            Wire(wires, At(p2, s * 26, 231, 0), At(p3, s * 26, 231, 0), 1.2f, 8);
        }
        Wire(wires, At(pm, 0, 231, 0), At(p1, 0, 231, 0), 0.6f);
        Put(Billboard(), 10.6f, 19f, 0, 0.3f);
        Put(Containers("컨테이너_황"), 15.4f, 17.4f, 0, 3.1f);
        Put(Cubes(3, 2), -11.6f, 17.6f, 0, 2f);
        Put(Appliances(), -3.6f, 17f, 14, 1.5f);
        Put(Drums(2), 1.5f, 17.2f, 0, 0.6f);

        // ── 빈 자리에 잡동사니 ──
        int n = Clutter(34);

        string msg = "[고물상 꾸미기] 소품 " + root.childCount + "개 (잡동사니 " + n + "개 포함) · 재질 " + surf.Count + "개";
        if (missing.Count > 0) msg += "\n없는 재질 (회색으로 보임): " + string.Join(", ", new List<string>(missing).ToArray());
        if (warn.Count > 0) msg += "\n손봐야 할 수 있는 자리:\n  · " + string.Join("\n  · ", warn.ToArray());
        Debug.Log(msg, rootGo);
        return rootGo;
    }
}
