using System.Collections.Generic;
using RealSteel.NanoBoss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 보스3 마에스트로 외형 (2026-11-24, 에디터 전용).
/// 그림: 사용자 참고 시트(`03. Image/Boss/Maestro/마에스트로_캐릭터시트_원본.png`, 원본 고해상도)의 왼쪽 큰 그림을 원래 픽셀 칸(약 5.2px)마다
/// 칸 가운데 색 그대로 뽑은 93×150px(`마에스트로_전체.png`)을 부위 6장으로 나눈 것 — 몸·머리·달(초승달+별)·지휘팔(지휘봉+소매)·반대팔·반짝이.
/// 부위마다 관절 픽셀을 원점으로 구워(MaestroSpriteBaker) 리그 그림 부위로 붙이고, MaestroLook이 관절을 돌려 몸짓을 만든다.
///
/// BuildLook — 군집 밑에 리그·부위·MaestroLook을 세운다 (보스 빌더도 이걸 부른다).
/// 메뉴 NYH/보스3/외형 시안: 달빛 지휘자 (현재 씬) — 전투와 무관한 보기용 [마에스트로 외형 시안]. 플레이하면 형체가 되고 몸짓을 돈다.
/// 1px = 군집 칸(1/32m) → 키 약 4.7m. 엔진(RSNano*)은 KKH 폴더 것 참조만.
/// </summary>
public static class MaestroLookBuilder
{
    private const string RootName = "[마에스트로 외형 시안]";
    private const string ImageFolder = "Assets/NYH/03. Image/Boss/Maestro/";
    private const int ArtWidth = 93, ArtHeight = 150;

    // 부위: 이름, 관절(원본 그림 픽셀 — 왼쪽 위 기준), 느낌 프리셋, 끝 늦음, 결속
    private struct PartDef
    {
        public string name;
        public Vector2Int jointTopDown;
        public int preset;
        public float tipLag, bind;
        public PartDef(string n, int x, int y, int p, float lag, float b) { name = n; jointTopDown = new Vector2Int(x, y); preset = p; tipLag = lag; bind = b; }
    }

    private static readonly PartDef Body = new PartDef("몸", 46, ArtHeight, 1, 0f, 0.85f);
    private static readonly PartDef[] Children =
    {
        new PartDef("머리", 52, 41, 2, 0f, 1f),
        new PartDef("달", 53, 31, 1, 2f, 0.9f),
        new PartDef("지휘팔", 41, 47, 1, 3f, 0.9f),
        new PartDef("반대팔", 63, 57, 1, 4f, 0.85f),
        new PartDef("반짝이", 46, 75, 0, 6f, 0.6f),
    };

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

        var look = BuildLook(swarm, root.transform);
        if (look == null) return;
        look.standalone = true;

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[Maestro] 외형 시안 배치 — 군집 봇 {swarm.botCount}. 플레이하면 형체가 되고 몸짓을 돈다. 같은 자리의 보스는 잠시 꺼 두고 볼 것");
    }

    /// <summary>
    /// swarm 밑에 리그와 부위를 세우고 lookHost에 MaestroLook을 붙인다 (이미 있으면 지우고 다시).
    /// 군집 봇 수 = 그림 봇 + 발밑 웅덩이 poolBots (패턴이 빌려 쓰는 봇)
    /// </summary>
    public static MaestroLook BuildLook(RSNanoSwarm swarm, Transform lookHost, int poolBots = 600)
    {
        // 굽기 — 부위마다 관절이 원점
        var shapes = new Dictionary<string, RSNanoSpriteShape>();
        foreach (var def in All())
        {
            var pivot = new Vector2Int(def.jointTopDown.x, ArtHeight - def.jointTopDown.y); // 텍스처는 아래가 0
            var shape = MaestroSpriteBaker.BakeToAsset($"{ImageFolder}마에스트로_{def.name}.png",
                                                      $"{MaestroSpriteBaker.OutFolder}/마에스트로_{def.name}_나노봇그림.asset", pivot);
            if (shape == null) return null;
            shapes[def.name] = shape;
        }

        swarm.shader = Shader.Find(RSNanoSwarm.ShaderName);
        swarm.floorFromTransform = false;
        swarm.botColors.Clear();
        swarm.botColors.Add(new NanoBotColor { color = new Color32(60, 54, 68, 255), ratio = 3f });   // 웅덩이 모래 — 그림의 어두운 남색
        swarm.botColors.Add(new NanoBotColor { color = new Color32(110, 104, 125, 255), ratio = 1f });
        swarm.sparkleRatio = 0.02f;
        swarm.sparkleTint = new Color32(194, 200, 248, 255);
        swarm.sandDrip = 0.3f; // TODO 시안값: 표면 알갱이가 아래로 흘러내림 (그림 아랫단이 흩어지는 느낌)

        var oldRig = swarm.transform.Find("리그_마에스트로");
        if (oldRig) Undo.DestroyObjectImmediate(oldRig.gameObject);
        var rigGo = new GameObject("리그_마에스트로");
        Undo.RegisterCreatedObjectUndo(rigGo, "마에스트로 리그");
        rigGo.transform.SetParent(swarm.transform, false);
        var rig = rigGo.AddComponent<RSNanoRig>();
        rig.swarm = swarm;
        var anchor = new GameObject("플레이어").transform;
        anchor.SetParent(rigGo.transform, false);
        anchor.localPosition = rig.previewOffset;
        rig.playerAnchor = anchor;

        float cell = swarm.Cell;
        var limbs = new List<RSNanoLimb>();
        var bodyLimb = Part(rigGo.transform, Body, shapes[Body.name], Vector3.zero);
        limbs.Add(bodyLimb);
        var made = new Dictionary<string, Transform>();
        foreach (var def in Children)
        {
            // 몸 원점(발밑 가운데) 기준 관절 자리 (m)
            var local = new Vector3((def.jointTopDown.x - Body.jointTopDown.x) * cell, (ArtHeight - def.jointTopDown.y) * cell, 0f);
            var l = Part(bodyLimb.transform, def, shapes[def.name], local);
            limbs.Add(l);
            made[def.name] = l.transform;
        }

        int artBots = 0;
        foreach (var s in shapes.Values) artBots += s.botCount;
        swarm.botCount = artBots + poolBots;

        var look = lookHost.GetComponent<MaestroLook>();
        if (look == null) look = Undo.AddComponent<MaestroLook>(lookHost.gameObject);
        look.swarm = swarm;
        look.rig = rig;
        look.body = bodyLimb.transform;
        look.head = made["머리"];
        look.moon = made["달"];
        look.batonArm = made["지휘팔"];
        look.otherArm = made["반대팔"];
        look.sparkle = made["반짝이"];
        look.limbs = limbs.ToArray();
        EditorUtility.SetDirty(look);
        return look;
    }

    private static IEnumerable<PartDef> All()
    {
        yield return Body;
        foreach (var c in Children) yield return c;
    }

    private static RSNanoLimb Part(Transform parent, PartDef def, RSNanoSpriteShape shape, Vector3 local)
    {
        var go = new GameObject(def.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        var l = go.AddComponent<RSNanoLimb>();
        l.type = LimbType.Sprite;
        l.picture = shape;
        l.artColor = true;
        l.fps = 0f;
        l.frame = 0f;
        l.ApplyPreset(RSNanoLimb.Presets[def.preset]);
        l.tipLag = def.tipLag;
        l.bind = def.bind;
        l.flow = def.bind < 0.8f ? 0.1f : 0.04f;
        if (def.name == "반짝이") { l.glow = 2.5f; l.tint = new Color(0.76f, 0.78f, 0.97f); }
        if (def.name == "달") l.glow = 0.8f;
        return l;
    }
}
