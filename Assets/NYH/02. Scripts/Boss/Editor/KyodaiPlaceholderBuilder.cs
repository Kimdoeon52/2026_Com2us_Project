using System.IO;
using RealSteel.Boss.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 메뉴 한 번으로 지금 열린 씬에 "임시 쿄다이"를 세운다 (2026-10-28). 3D 에셋을 구하기 전까지 상자 로봇으로 패턴을 맞춰보는 용도.
///
/// 만드는 것:
///   · 데이터: 04. SO/Boss/Kyodai/ 에 BossMasterData·손바닥 압착 BossPatternData (이미 있으면 그대로 씀)
///   · 스튜디오(맵 밖 y=-500): 원근 카메라 + 상자 로봇(몸·머리·양팔). PixelStudio
///   · 경기장 쪽: KyodaiBossController, 양팔 KyodaiLimb + BossHurtZone, 손바닥 압착 패턴, 바닥 그림자
/// 경기장 범위는 메인 카메라가 경기장 평면(z=0)에서 보는 영역 × 1.5, 바닥은 씬의 첫 로봇 발밑으로 잡는다 — 인스펙터에서 고치면 된다.
/// 에셋이 정해지면 스튜디오의 "모델"만 갈아 끼우고 KyodaiLimb의 effector를 새 손·발(IK 타깃)로 바꾼다.
/// </summary>
public static class KyodaiPlaceholderBuilder
{
    private const string DataFolder = "Assets/NYH/04. SO/Boss/Kyodai";
    private const float BackDepth = 60f;       // 배경 부위가 스튜디오 카메라에서 떨어진 거리
    private const float ForegroundDepth = 25f; // 전경으로 나온 부위 거리 (가까울수록 크게)

    [MenuItem("NYH/쿄다이/임시 보스 만들기 (현재 씬)")]
    private static void Build()
    {
        if (Object.FindAnyObjectByType<KyodaiBossController>() != null &&
            !EditorUtility.DisplayDialog("쿄다이", "씬에 이미 쿄다이가 있습니다. 하나 더 만들까요?", "만들기", "취소"))
            return;

        // ── 1. 데이터 ──
        var pattern = LoadOrCreate<BossPatternData>($"{DataFolder}/Kyodai_손바닥압착.asset", p =>
        {
            p.patternID = "KYODAI_PALM_PRESS";
            p.patternName = "손바닥 압착";
            p.description = "기획서10_28 2페이즈: 오른팔 / 경기장 1/3 그림자 1.2초 / 손바닥이 짓누름 / 잔류 3초(손등 위 타격) / 파츠 피해 —";
            p.tellDuration = 1.2f;   // 기획서: 그림자 1.2초
            p.postDelay = 1f;        // KKH 공통 딜타임
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.Core; // 기획서: 파츠 피해 —
            p.partDurabilityDamage = 0;
            // baseDamage는 기획 수치 없음 — KKH 기본값 그대로(TODO)
        });
        var master = LoadOrCreate<BossMasterData>($"{DataFolder}/Kyodai_Master.asset", m =>
        {
            m.bossID = "BOSS_REGION_02_KYODAI";
            m.bossName = "쿄다이";
            m.patterns.Clear();
            m.patterns.Add(pattern);
            m.gimmickParts.Clear();
            m.phases.Clear();
            m.phases.Add(new BossPhaseData { phaseIndex = 1, hpThresholdRatio = 1f }); // 패턴 목록 비움 = 전부
            // maxHp·방어력은 기획 수치 없음 — KKH 기본값 그대로(TODO)
        });

        // ── 2. 경기장 범위 ──
        Rect arena = GuessArena(out float groundY);

        // ── 3. 스튜디오 ──
        var studioRoot = new GameObject("쿄다이 스튜디오");
        studioRoot.transform.position = new Vector3(0f, -500f, 0f);
        var cam = new GameObject("스튜디오 카메라").AddComponent<Camera>();
        cam.transform.SetParent(studioRoot.transform, false);
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 200f;
        cam.aspect = arena.width / arena.height;
        cam.depth = -100f; // 플레이 전(렌더텍스처 연결 전)에 게임 화면을 덮지 않게 메인 카메라보다 먼저 그린다
        cam.cullingMask = 1 << 30;

        var studio = studioRoot.AddComponent<PixelStudio>();
        SetFields(studio, so =>
        {
            so.FindProperty("studioCamera").objectReferenceValue = cam;
            so.FindProperty("arenaRect").rectValue = arena;
            so.FindProperty("planeZ").floatValue = 0f;
        });
        int backLayer = 30;

        float wpsBack = WorldPerStudio(cam, arena, BackDepth);
        float wpsFront = WorldPerStudio(cam, arena, ForegroundDepth);
        Vector3 At(float u, float v, float depth) => cam.ViewportToWorldPoint(new Vector3(u, v, depth));

        var model = new GameObject("모델 (임시 상자 로봇)");
        model.transform.SetParent(studioRoot.transform, false);

        float bodyH = arena.height * 0.45f / wpsBack;
        var body = Box("몸", model.transform, At(0.5f, 0.62f, BackDepth), new Vector3(bodyH * 0.8f, bodyH, bodyH * 0.4f), new Color(0.35f, 0.38f, 0.42f));
        var head = Box("머리", model.transform, body.position + Vector3.up * bodyH * 0.65f, Vector3.one * bodyH * 0.35f, new Color(0.3f, 0.3f, 0.34f));
        Box("메인 렌즈", model.transform, head.position + Vector3.back * bodyH * 0.18f, Vector3.one * bodyH * 0.12f, new Color(1f, 0.2f, 0.15f));

        // 손 크기: 전경에서 경기장 1/3 너비(손바닥 압착)로 보이게
        float palmW = arena.width / 3f / wpsFront;
        float palmT = 1.5f / wpsFront;
        Vector3 palmSize = new Vector3(palmW, palmT, palmW * 0.5f);

        var root = new GameObject("쿄다이");
        root.tag = "Boss";
        var boss = root.AddComponent<KyodaiBossController>();

        KyodaiLimb rightArm = Arm("오른팔", root.transform, model.transform, body, bodyH, +1f, At(0.72f, 0.42f, BackDepth), palmSize, boss);
        Arm("왼팔", root.transform, model.transform, body, bodyH, -1f, At(0.28f, 0.42f, BackDepth), palmSize, boss);

        SetLayerRecursive(model.transform, backLayer);

        SetFields(boss, so =>
        {
            so.FindProperty("masterData").objectReferenceValue = master;
            so.FindProperty("studio").objectReferenceValue = studio;
            so.FindProperty("arenaMinX").floatValue = arena.xMin + arena.width / 6f; // 판 가장자리보다 안쪽
            so.FindProperty("arenaMaxX").floatValue = arena.xMax - arena.width / 6f;
            so.FindProperty("groundY").floatValue = groundY;
        });

        // ── 4. 패턴 ──
        var shadowGo = new GameObject("바닥 그림자", typeof(SpriteRenderer), typeof(GroundTelegraph));
        shadowGo.transform.SetParent(root.transform, false);
        shadowGo.GetComponent<SpriteRenderer>().sortingOrder = -10;

        var palmGo = new GameObject("손바닥 압착");
        palmGo.transform.SetParent(root.transform, false);
        var palm = palmGo.AddComponent<KyodaiPalmPressPattern>();
        SetFields(palm, so =>
        {
            so.FindProperty("patternData").objectReferenceValue = pattern;
            so.FindProperty("limb").objectReferenceValue = rightArm;
            so.FindProperty("telegraph").objectReferenceValue = shadowGo.GetComponent<GroundTelegraph>();
        });

        Undo.RegisterCreatedObjectUndo(studioRoot, "쿄다이 만들기");
        Undo.RegisterCreatedObjectUndo(root, "쿄다이 만들기");
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log($"[Kyodai] 임시 보스 생성 — 경기장 {arena}, 바닥 y={groundY:0.##}. 경기장 범위·바닥은 '쿄다이' 인스펙터에서 확인할 것. " +
                  "Player에 HurtboxColliderSync, 씬에 DummyBattleBootstrap·HitDetection이 있어야 서로 때린다.");
    }

    private static KyodaiLimb Arm(string name, Transform arenaRoot, Transform model, Transform body, float bodyH, float side,
                                  Vector3 handRest, Vector3 palmSize, KyodaiBossController boss)
    {
        var shoulder = new GameObject($"{name} 어깨").transform;
        shoulder.SetParent(model, false);
        shoulder.position = body.position + new Vector3(side * bodyH * 0.45f, bodyH * 0.35f, 0f);

        var hand = Box($"{name} 손", model, handRest, palmSize, new Color(0.45f, 0.47f, 0.5f));
        var segment = Box($"{name} 팔", model, shoulder.position, new Vector3(bodyH * 0.12f, 1f, bodyH * 0.12f), new Color(0.4f, 0.42f, 0.46f));

        var limbGo = new GameObject(name);
        limbGo.transform.SetParent(arenaRoot, false);
        var limb = limbGo.AddComponent<KyodaiLimb>();

        var zoneGo = new GameObject($"{name} 피격", typeof(BoxCollider2D), typeof(BossHurtZone));
        zoneGo.transform.SetParent(limbGo.transform, false);
        zoneGo.GetComponent<BoxCollider2D>().isTrigger = true;
        zoneGo.GetComponent<BoxCollider2D>().enabled = false; // 배경에서 시작 — 잔류일 때만 켜짐
        var zone = zoneGo.GetComponent<BossHurtZone>();
        SetFields(zone, so =>
        {
            so.FindProperty("partId").stringValue = name;
            so.FindProperty("damageScale").floatValue = 1f; // 기획서10_28: 팔 1.0
        });

        SetFields(limb, so =>
        {
            so.FindProperty("partId").stringValue = name;
            so.FindProperty("boss").objectReferenceValue = boss;
            so.FindProperty("effector").objectReferenceValue = hand;
            so.FindProperty("segment").objectReferenceValue = segment;
            so.FindProperty("shoulder").objectReferenceValue = shoulder;
            so.FindProperty("hurtZone").objectReferenceValue = zone;
            so.FindProperty("foregroundDepth").floatValue = ForegroundDepth;
        });
        return limb;
    }

    private static Transform Box(string name, Transform parent, Vector3 position, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // 스튜디오 모델은 그림 전용
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = size;
        var renderer = go.GetComponent<MeshRenderer>();
        var mat = new Material(renderer.sharedMaterial) { color = color };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        renderer.sharedMaterial = mat;
        return go.transform;
    }

    private static Rect GuessArena(out float groundY)
    {
        var executor = Object.FindAnyObjectByType<ActionExecutor>();
        groundY = executor != null ? executor.transform.position.y : 0f;

        var main = Camera.main;
        if (main == null || main.orthographic)
        {
            float h = main != null ? main.orthographicSize * 2f : 12f;
            float w = h * (main != null ? main.aspect : 16f / 9f);
            Vector2 c = main != null ? (Vector2)main.transform.position : Vector2.zero;
            return MakeRect(c, w * 1.5f, h * 1.5f);
        }

        float dist = Mathf.Abs(main.transform.position.z);
        float height = 2f * dist * Mathf.Tan(main.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return MakeRect(main.transform.position, height * main.aspect * 1.5f, height * 1.5f);
    }

    private static Rect MakeRect(Vector2 center, float w, float h) => new Rect(center.x - w * 0.5f, center.y - h * 0.5f, w, h);

    private static float WorldPerStudio(Camera cam, Rect arena, float depth) =>
        arena.height / (2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));

    private static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayerRecursive(c, layer);
    }

    private static void SetFields(Object target, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T LoadOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        string folder = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(folder);
        var asset = ScriptableObject.CreateInstance<T>();
        init(asset);
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
