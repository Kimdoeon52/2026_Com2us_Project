using System.Collections.Generic;
using System.IO;
using RealSteel.NanoBoss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 보스3 마에스트로 외형 = 나노봇 퍼펫 (2026-11-24 개편, 에디터 전용).
/// 그림 `03. Image/Boss/Maestro/마에스트로_전체.png` — 사용자 참고 시트(원본 고해상도)의 왼쪽 큰 그림을 원래 픽셀 칸(약 5.2px)마다 칸 가운데 색 그대로 뽑은 93×150px.
/// 픽셀마다 붙을 뼈·천·종류를 아래 규칙(그림 좌표, 위쪽 0)으로 나눠 `Maestro_PuppetData`에 넣고, 남는 봇을 숨길 하체 실루엣을 굽는다.
/// 뼈·천 위치와 느낌 값은 처음 만들 때만 넣는다 — 에셋에서 고친 값은 다시 빌드해도 유지(뼈 수가 바뀌었을 때만 새로).
///
/// BuildLook — 군집과 MaestroLook을 세운다 (보스 빌더도 이걸 부른다).
/// 메뉴 NYH/보스3/외형 시안: 달빛 지휘자 (현재 씬) — 전투와 무관한 보기용 [마에스트로 외형 시안].
/// </summary>
public static class MaestroLookBuilder
{
    private const string RootName = "[마에스트로 외형 시안]";
    private const string ArtPng = "Assets/NYH/03. Image/Boss/Maestro/마에스트로_전체.png";
    private const string DataPath = MaestroSpriteBaker.OutFolder + "/Maestro_PuppetData.asset";
    private const string ReservePath = MaestroSpriteBaker.OutFolder + "/마에스트로_하체_나노봇그림.asset";
    private const float FeetX = 46f;

    [MenuItem("NYH/보스3/외형 시안: 달빛 지휘자 (현재 씬)", false, 210)]
    private static void BuildPreview()
    {
        var old = GameObject.Find(RootName);
        if (old) Undo.DestroyObjectImmediate(old);

        Vector3 feet = Vector3.zero;
        var player = GameObject.FindWithTag("Player");
        if (player) feet = new Vector3(player.transform.position.x - 4f, player.transform.position.y, 0f);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "마에스트로 외형 시안");
        root.transform.position = feet;
        var swarmGo = new GameObject("군집");
        swarmGo.transform.SetParent(root.transform, false);
        var swarm = swarmGo.AddComponent<RSNanoSwarm>();

        var look = BuildLook(swarm, root.transform, 1500);
        if (look == null) return;
        look.standalone = true;

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[Maestro] 외형 시안 배치 — 군집 봇 {swarm.botCount}. 플레이하면 형체가 되고 움직인다. 같은 자리의 보스는 잠시 꺼 두고 볼 것");
    }

    /// <summary>swarm을 퍼펫용으로 맞추고 lookHost에 MaestroLook을 붙인다. 군집 봇 수 = 그림 봇 + 남는 봇 reserveBots</summary>
    public static MaestroLook BuildLook(RSNanoSwarm swarm, Transform lookHost, int reserveBots = 600)
    {
        var data = BuildData(out var reserve);
        if (data == null) return null;

        // 원본 그림 색이 그대로 보이게 — 조명·음영 끈 재질, 봇마다 밝기 흔들림·무작위 반짝이·흘러내림 끔
        swarm.shader = Shader.Find(RSNanoSwarm.ShaderName);
        swarm.material = EnsureMaterial(swarm.shader);
        swarm.floorFromTransform = false;
        swarm.botColors.Clear();
        swarm.botColors.Add(new NanoBotColor { color = new Color32(60, 54, 68, 255), ratio = 1f });
        swarm.grainNoise = 0f;
        swarm.sparkleRatio = 0f;
        swarm.sandDrip = 0f;
        swarm.castShadows = false;
        swarm.receiveShadows = false;
        swarm.botCount = data.PixelCount + reserveBots;

        // 예전 리그 방식 외형이 남아 있으면 지운다
        var oldRig = swarm.transform.Find("리그_마에스트로");
        if (oldRig) Undo.DestroyObjectImmediate(oldRig.gameObject);

        var look = lookHost.GetComponent<MaestroLook>();
        if (look == null) look = Undo.AddComponent<MaestroLook>(lookHost.gameObject);
        look.swarm = swarm;
        look.data = data;
        look.reserveShape = reserve;
        EditorUtility.SetDirty(look);
        EditorUtility.SetDirty(swarm);
        return look;
    }

    // ───────────────────────── 퍼펫 데이터 ─────────────────────────

    private static MaestroPuppetData BuildData(out RSNanoSpriteShape reserve)
    {
        reserve = null;
        if (!File.Exists(ArtPng)) { Debug.LogError($"[Maestro] 그림이 없음: {ArtPng}"); return null; }
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(ArtPng));
        int W = tex.width, H = tex.height;
        Color32[] px = tex.GetPixels32();
        Object.DestroyImmediate(tex);

        var data = AssetDatabase.LoadAssetAtPath<MaestroPuppetData>(DataPath);
        bool created = data == null;
        if (created) data = ScriptableObject.CreateInstance<MaestroPuppetData>();
        var defaultBones = DefaultBones();
        if (created || data.bones == null || data.bones.Length != defaultBones.Length) { data.bones = defaultBones; data.cloths = DefaultCloths(); }
        data.artWidthPx = W;
        data.artHeightPx = H;
        data.feetXPx = FeetX;

        var pos = new List<Vector2>();
        var col = new List<Color32>();
        var owner = new List<short>();
        var kind = new List<byte>();
        var resPts = new List<Vector2Int>();
        var resCols = new List<Color32>();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Color32 c = px[(H - 1 - y) * W + x]; // 텍스처는 아래가 0 → 그림 좌표(위가 0)
                if (c.a < 128) continue;
                Classify(data, x, y, c, out short o, out byte k);
                pos.Add(new Vector2(x, y));
                col.Add(c);
                owner.Add(o);
                kind.Add(k);
                if (o == -1 && k == (byte)MaestroPuppetData.PixelKind.Normal)
                {
                    resPts.Add(new Vector2Int(x - (int)FeetX, H - 1 - y)); // 발밑 가운데 기준, 위가 +
                    resCols.Add(c);
                }
            }
        data.pixPos = pos.ToArray();
        data.pixColor = col.ToArray();
        data.pixOwner = owner.ToArray();
        data.pixKind = kind.ToArray();

        EnsureFolder(MaestroSpriteBaker.OutFolder);
        if (created) AssetDatabase.CreateAsset(data, DataPath);
        EditorUtility.SetDirty(data);

        reserve = AssetDatabase.LoadAssetAtPath<RSNanoSpriteShape>(ReservePath);
        bool newReserve = reserve == null;
        if (newReserve) reserve = ScriptableObject.CreateInstance<RSNanoSpriteShape>();
        MaestroSpriteBaker.BakePixels(reserve, resPts, resCols, $"maestro_lower_{resPts.Count}_{File.GetLastWriteTimeUtc(ArtPng).Ticks}");
        if (newReserve) AssetDatabase.CreateAsset(reserve, ReservePath);
        EditorUtility.SetDirty(reserve);
        AssetDatabase.SaveAssets();

        int[] counts = new int[data.bones.Length + data.cloths.Length + 1];
        foreach (short o in data.pixOwner) counts[o >= 0 ? o : o == -1 ? data.bones.Length : data.bones.Length + 1 + (-2 - o)]++;
        var sb = new System.Text.StringBuilder($"[Maestro] 퍼펫 데이터 — 픽셀 {data.PixelCount} (하체 {counts[data.bones.Length]}");
        for (int b = 0; b < data.bones.Length; b++) sb.Append($", {data.bones[b].name} {counts[b]}");
        for (int c = 0; c < data.cloths.Length; c++) sb.Append($", {data.cloths[c].name} {counts[data.bones.Length + 1 + c]}");
        Debug.Log(sb.Append(")").ToString());
        return data;
    }

    // 픽셀 → 붙을 뼈/천/종류 (그림 좌표, 위쪽 0). 앞의 규칙이 먼저
    private static void Classify(MaestroPuppetData d, int x, int y, Color32 c, out short owner, out byte kind)
    {
        kind = (byte)MaestroPuppetData.PixelKind.Normal;
        float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
        bool bluish = c.b - c.r > 20 && c.b > 150;
        bool head = In(x, y, 45, 61, 21, 41);
        bool moonArea = In(x, y, 38, 70, 0, 35);
        bool torso = In(x, y, 44, 64, 38, 62);

        if (head) { owner = B(d, "머리"); return; }
        if (moonArea) { owner = B(d, "달"); if (lum > 0.7f) kind = (byte)MaestroPuppetData.PixelKind.Glow; return; }
        if (bluish && !torso) { owner = -1; kind = (byte)MaestroPuppetData.PixelKind.Sparkle; return; }
        if (y <= 25 && DistToSeg(x, y, 21, 25, 31, 3) <= 1.6f) { owner = B(d, "지휘봉"); return; }
        if (In(x, y, 14, 24, 23, 34)) { owner = x <= 18 && y >= 26 ? B(d, "지휘_손가락") : B(d, "지휘_손"); return; }
        if (In(x, y, 12, 18, 35, 60) && lum < 0.45f) { owner = Cloth(0); return; }   // 왼소매
        if (In(x, y, 13, 28, 34, 60)) { owner = B(d, "지휘_아래팔"); return; }
        if (In(x, y, 26, 42, 43, 57)) { owner = B(d, "지휘_위팔"); return; }
        if ((x <= 11 && y >= 55 && y <= 86) || (x <= 36 && y >= 55 && y <= 77)) { owner = Cloth(1); return; } // 왼끈
        if (In(x, y, 12, 39, 78, 106)) { owner = Cloth(2); return; }                  // 왼아래끈
        if (x >= 84 && y >= 68 && y <= 79) { owner = y <= 72 ? B(d, "손가락1") : y <= 75 ? B(d, "손가락2") : B(d, "손가락3"); return; }
        if (In(x, y, 79, 83, 67, 79)) { owner = B(d, "반대_손"); return; }
        if (x >= 66 && y >= 79 && y <= 106) { owner = Cloth(3); return; }             // 반대소매
        if (In(x, y, 69, 80, 62, 79)) { owner = B(d, "반대_아래팔"); return; }
        if (x >= 59 && y >= 52 && y <= 70) { owner = B(d, "반대_위팔"); return; }
        if (y < 82) { owner = B(d, "상체"); return; }
        owner = -1;
        if (y >= 118) kind = (byte)MaestroPuppetData.PixelKind.Drip;
    }

    private static bool In(int x, int y, int x0, int x1, int y0, int y1) => x >= x0 && x <= x1 && y >= y0 && y <= y1;
    private static short B(MaestroPuppetData d, string name) => (short)Mathf.Max(-1, d.BoneIndex(name));
    private static short Cloth(int c) => (short)(-2 - c);

    private static float DistToSeg(float x, float y, float ax, float ay, float bx, float by)
    {
        var p = new Vector2(x, y); var a = new Vector2(ax, ay); var b = new Vector2(bx, by);
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    // 뼈 (그림 좌표 관절) — 부모가 앞 번호. 스프링: 진동수·감쇠·반응·팔로스루. 전부 시안값(TODO) — 에셋에서 조절
    private static MaestroPuppetData.Bone[] DefaultBones() => new[]
    {
        Bone("상체",        -1, 52, 82, 1.2f, 0.7f, 0f, 0f),
        Bone("머리",         0, 52, 41, 1.6f, 0.5f, 0.3f, 0.02f),
        Bone("달",           0, 53, 31, 0.9f, 0.4f, 0f, 0.04f),
        Bone("지휘_위팔",    0, 40, 48, 2.2f, 0.5f, 0.6f, 0f),
        Bone("지휘_아래팔",  3, 25, 53, 1.9f, 0.42f, 0.2f, 0.06f),
        Bone("지휘_손",      4, 20, 33, 2.4f, 0.38f, 0.2f, 0.05f),
        Bone("지휘봉",       5, 21, 26, 3.0f, 0.32f, 0.3f, 0.04f),
        Bone("지휘_손가락",  5, 19, 30, 3.2f, 0.5f, 0.5f, 0f),
        Bone("반대_위팔",    0, 61, 56, 1.8f, 0.5f, 0.4f, 0f),
        Bone("반대_아래팔",  8, 71, 68, 1.7f, 0.42f, 0.2f, 0.06f),
        Bone("반대_손",      9, 81, 74, 2.2f, 0.4f, 0.3f, 0.05f),
        Bone("손가락1",     10, 84, 72, 3.4f, 0.45f, 0.5f, 0.03f),
        Bone("손가락2",     10, 84, 74, 3.0f, 0.45f, 0.5f, 0.035f),
        Bone("손가락3",     10, 84, 76, 2.7f, 0.45f, 0.5f, 0.04f),
    };

    private static MaestroPuppetData.Bone Bone(string n, int parent, float x, float y, float f, float z, float r, float ft) =>
        new MaestroPuppetData.Bone { name = n, parent = parent, pivotPx = new Vector2(x, y), frequency = f, damping = z, response = r, followThrough = ft };

    // 천 — 뼈대 선(뿌리 → 끝, 그림 좌표). 시안값(TODO)
    private static MaestroPuppetData.Cloth[] DefaultCloths() => new[]
    {
        Cloth("왼소매", 4, 120f, 25f, 1f, 1.5f, new Vector2(19, 36), new Vector2(17, 44), new Vector2(16, 51), new Vector2(17, 59)),
        Cloth("왼끈", 0, 80f, 6f, 1.2f, 3f, new Vector2(37, 55), new Vector2(32, 60), new Vector2(26, 65), new Vector2(19, 63), new Vector2(11, 60),
              new Vector2(5, 63), new Vector2(3, 70), new Vector2(5, 77), new Vector2(10, 83)),
        Cloth("왼아래끈", -1, 80f, 6f, 1.2f, 3f, new Vector2(39, 78), new Vector2(36, 86), new Vector2(35, 93), new Vector2(31, 98), new Vector2(23, 99), new Vector2(14, 103)),
        Cloth("반대소매", 9, 90f, 10f, 1.5f, 2.5f, new Vector2(75, 78), new Vector2(75, 85), new Vector2(77, 92), new Vector2(79, 98), new Vector2(81, 103)),
    };

    private static MaestroPuppetData.Cloth Cloth(string n, int parent, float kRoot, float kTip, float gravity, float wind, params Vector2[] spine) =>
        new MaestroPuppetData.Cloth { name = n, parentBone = parent, spinePx = spine, stiffnessRoot = kRoot, stiffnessTip = kTip, gravity = gravity, wind = wind };

    // ───────────────────────── 재질 · 폴더 ─────────────────────────

    // 나노봇 재질 — 조명 반영·가장자리 기울기·윗면/아랫면 음영을 꺼서 그림 픽셀 색 그대로. 처음 만들 때만 값을 넣는다
    private static Material EnsureMaterial(Shader shader)
    {
        string path = MaestroSpriteBaker.OutFolder + "/Maestro_NanoBot.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        EnsureFolder(MaestroSpriteBaker.OutFolder);
        mat = new Material(shader) { name = "Maestro_NanoBot" };
        if (mat.HasProperty("_LightStrength")) mat.SetFloat("_LightStrength", 0f);
        if (mat.HasProperty("_EdgeTilt")) mat.SetFloat("_EdgeTilt", 0f);
        if (mat.HasProperty("_TopLight")) mat.SetFloat("_TopLight", 0f);
        if (mat.HasProperty("_BottomDark")) mat.SetFloat("_BottomDark", 0f);
        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Maestro] 나노봇 재질 생성 (조명·음영 끔): {path}");
        return mat;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
