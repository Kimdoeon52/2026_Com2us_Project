using System;
using System.IO;
using RealSteel.Boss.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 메뉴 한 번으로 지금 열린 씬에 "2D 임시 쿄다이"를 세운다 (2026-11-04 2D 개편 1단계).
/// 진짜 그림이 나오기 전까지 코드로 그린 도트 임시 그림으로 구조·움직임부터 맞춘다.
///
/// 만드는 것 (이미 있으면 그대로 씀 — 손으로 고친 값·그림을 덮어쓰지 않는다):
///   · 그림: 03. Image/Boss/Kyodai2D/ 임시_주먹·임시_손바닥·임시_본체·임시_그림자 (진짜 그림이 나오면 같은 이름으로 덮어쓰면 된다)
///   · 데이터: 04. SO/Boss/Kyodai2D/ 마스터·손 느낌·주먹 내려찍기(패턴 데이터 + 손 동작)
///   · 씬: 쿄다이 (2D) — 본체(경기장 뒤), 왼손·오른손(KyodaiHand + 맞는 자리 + 그림자), 바닥 예고, 주먹 내려찍기 패턴
/// 경기장 범위는 메인 카메라가 경기장 평면(z=0)에서 보는 영역, 바닥은 씬의 첫 로봇 발밑 — 인스펙터에서 고치면 된다.
/// </summary>
public static class Kyodai2DBuilder
{
    private const string DataFolder = "Assets/NYH/04. SO/Boss/Kyodai2D";
    private const string ImageFolder = "Assets/NYH/03. Image/Boss/Kyodai2D";
    private const float BodyBackZ = 6f; // 본체가 경기장 평면 뒤로 떨어진 거리(월드)

    [MenuItem("NYH/쿄다이/2D 임시 보스 만들기 (현재 씬)")]
    private static void Build()
    {
        if (Object.FindAnyObjectByType<KyodaiBossController>() != null &&
            !EditorUtility.DisplayDialog("쿄다이", "씬에 이미 2D 쿄다이가 있습니다. 하나 더 만들까요?", "만들기", "취소"))
            return;

        // ── 1. 임시 그림 ──
        PaintTempImages(false, out Sprite fist, out Sprite palm, out Sprite bodySprite, out Sprite shadowSprite);

        // ── 2. 데이터 ──
        var feel = LoadOrCreate<KyodaiHandFeel>($"{DataFolder}/Kyodai2D_손느낌.asset", _ => { });
        var slamMotion = LoadOrCreate<KyodaiHandMotion>($"{DataFolder}/Kyodai2D_주먹내려찍기_모션.asset", FillFistSlam);
        var slamData = LoadOrCreate<BossPatternData>($"{DataFolder}/Kyodai2D_주먹내려찍기.asset", p =>
        {
            p.patternID = "KYODAI2D_FIST_SLAM";
            p.patternName = "주먹 내려찍기";
            p.description = "2D 개편: 손이 플레이어 위로 떠서 따라오다(그림자 예고) 끌어올린 뒤 내려찍고 잠깐 박혀 있다가 돌아간다. 한 손씩 번갈아";
            p.tellDuration = 66f / 60f; // 동작의 예고 키(조준 50f + 끌어올림 16f)와 맞춤
            p.postDelay = 1f;           // KKH 공통 딜타임
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.Core;
            p.partDurabilityDamage = 0;
            // baseDamage는 기획 수치 없음 — KKH 기본값 그대로(TODO)
        });
        var master = LoadOrCreate<BossMasterData>($"{DataFolder}/Kyodai2D_Master.asset", m =>
        {
            m.bossID = "BOSS_REGION_02_KYODAI";
            m.bossName = "쿄다이";
            m.patterns.Clear();
            m.patterns.Add(slamData);
            m.gimmickParts.Clear();
            m.phases.Clear();
            m.phases.Add(new BossPhaseData { phaseIndex = 1, hpThresholdRatio = 1f });   // 패턴 목록 비움 = 전부
            m.phases.Add(new BossPhaseData { phaseIndex = 2, hpThresholdRatio = 0.5f }); // 50%는 임시값(2026-11-04 사용자: HP 비율 2페이즈)
            // maxHp·방어력은 기획 수치 없음 — KKH 기본값 그대로(TODO)
        });

        // ── 3. 경기장 ──
        Rect arena = GuessArena(out float groundY);

        var root = new GameObject("쿄다이 (2D)");
        root.tag = "Boss";
        root.transform.position = new Vector3(arena.center.x, groundY, 0f);
        var boss = root.AddComponent<KyodaiBossController>();

        // ── 4. 본체 (경기장 뒤, 원근 카메라면 뒤로 간 만큼 키우고 화면상 같은 자리에 보이게 옮긴다) ──
        var bodyGo = new GameObject("본체", typeof(SpriteRenderer));
        bodyGo.transform.SetParent(root.transform, false);
        var bodyRenderer = bodyGo.GetComponent<SpriteRenderer>();
        bodyRenderer.sprite = bodySprite;
        bodyRenderer.sortingOrder = -20;
        bodyRenderer.color = new Color(0.62f, 0.64f, 0.7f); // 배경이라 살짝 어둡게
        PlaceBehind(bodyGo.transform, new Vector2(arena.center.x, groundY + 6.5f), BodyBackZ);

        // ── 5. 손 ──
        var telegraphGo = new GameObject("바닥 예고", typeof(SpriteRenderer), typeof(GroundTelegraph));
        telegraphGo.transform.SetParent(root.transform, false);
        telegraphGo.GetComponent<SpriteRenderer>().sortingOrder = -10;

        Hand("왼손", "왼팔", KyodaiHandSide.Left, new Vector2(-4.5f, 5f), root.transform, feel, fist, palm, shadowSprite);
        Hand("오른손", "오른팔", KyodaiHandSide.Right, new Vector2(4.5f, 5f), root.transform, feel, fist, palm, shadowSprite);

        SetFields(boss, so =>
        {
            so.FindProperty("masterData").objectReferenceValue = master;
            so.FindProperty("arenaMinX").floatValue = arena.xMin + 1f;
            so.FindProperty("arenaMaxX").floatValue = arena.xMax - 1f;
            so.FindProperty("groundY").floatValue = groundY;
            so.FindProperty("body").objectReferenceValue = bodyGo.transform;
        });

        // ── 6. 패턴 ──
        var slamGo = new GameObject("주먹 내려찍기");
        slamGo.transform.SetParent(root.transform, false);
        var slam = slamGo.AddComponent<KyodaiHandPattern>();
        SetFields(slam, so =>
        {
            so.FindProperty("patternData").objectReferenceValue = slamData;
            so.FindProperty("motion").objectReferenceValue = slamMotion;
            so.FindProperty("handChoice").enumValueIndex = (int)KyodaiHandChoice.Alternate;
            so.FindProperty("telegraph").objectReferenceValue = telegraphGo.GetComponent<GroundTelegraph>();
        });

        Undo.RegisterCreatedObjectUndo(root, "2D 쿄다이 만들기");
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log($"[Kyodai] 2D 임시 보스 생성 — 경기장 x {arena.xMin + 1f:0.#} ~ {arena.xMax - 1f:0.#}, 바닥 y={groundY:0.##}. " +
                  "씬에 3D 쿄다이가 있으면 꺼둘 것. Player에 HurtboxColliderSync('Player' 태그), 씬에 DummyBattleBootstrap·HitDetection이 있어야 서로 때린다.");
    }

    private static void Hand(string name, string partId, KyodaiHandSide side, Vector2 homeLocal, Transform root,
                             KyodaiHandFeel feel, Sprite fist, Sprite palm, Sprite shadowSprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = homeLocal;

        var visual = new GameObject("Visual", typeof(SpriteRenderer));
        visual.transform.SetParent(go.transform, false);
        var renderer = visual.GetComponent<SpriteRenderer>();
        renderer.sprite = fist;
        renderer.sortingOrder = 5;
        renderer.flipX = side == KyodaiHandSide.Left; // 임시 그림은 오른손 기준 — 왼손은 거울로

        var hurt = new GameObject("맞는 자리", typeof(BoxCollider2D), typeof(BossHurtZone));
        hurt.transform.SetParent(go.transform, false);
        var zone = hurt.GetComponent<BossHurtZone>();
        SetFields(zone, so =>
        {
            so.FindProperty("partId").stringValue = partId;
            so.FindProperty("damageScale").floatValue = 1f; // 기획서10_28: 팔 1.0
        });

        var shadow = new GameObject("그림자", typeof(SpriteRenderer));
        shadow.transform.SetParent(go.transform, false);
        var shadowRenderer = shadow.GetComponent<SpriteRenderer>();
        shadowRenderer.sprite = shadowSprite;
        shadowRenderer.sortingOrder = -5;
        shadowRenderer.color = new Color(0f, 0f, 0f, 0.4f);

        var hand = go.AddComponent<KyodaiHand>();
        SetFields(hand, so =>
        {
            so.FindProperty("side").enumValueIndex = (int)side;
            so.FindProperty("homeLocal").vector2Value = homeLocal;
            so.FindProperty("feel").objectReferenceValue = feel;
            so.FindProperty("fistSprite").objectReferenceValue = fist;
            so.FindProperty("palmSprite").objectReferenceValue = palm;
            so.FindProperty("visual").objectReferenceValue = visual.transform;
            so.FindProperty("spriteRenderer").objectReferenceValue = renderer;
            so.FindProperty("shadow").objectReferenceValue = shadowRenderer;
            so.FindProperty("hurtZone").objectReferenceValue = zone;
        });
    }

    // 주먹 내려찍기: 떠서 따라오기 → 끌어올리기(예비 동작) → 내려찍기 → 박힘 → 뽑기. 프레임·높이는 전부 임시값(기획 수치 없음)
    private static void FillFistSlam(KyodaiHandMotion m)
    {
        m.keys.Clear();
        m.keys.Add(new KyodaiHandKey
        {
            label = "따라오며 조준", frames = 50, anchor = KyodaiHandAnchor.Player, offset = new Vector2(0f, 5.5f), trackPlayer = true,
            move = KyodaiHandMove.Spring, springFrequency = 1.6f, springDamping = 0.75f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.6f, telegraphWidth = 2.6f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "끌어올리기", frames = 16, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 6.8f),
            move = KyodaiHandMove.Spring, springFrequency = 3f, springDamping = 0.5f,
            pose = KyodaiHandPose.Fist, scale = 1.12f, floatWeight = 0.1f, telegraphWidth = 2.6f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "내려찍기", frames = 7, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 1f),
            move = KyodaiHandMove.Ease, easeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.6f, 0f)),
            pose = KyodaiHandPose.Fist, floatWeight = 0f, telegraphWidth = 2.6f,
            hitActive = true, hitSize = new Vector2(2f, 1.8f),
            impactOnEnd = true, impactShake = 0.35f, impactFreezeFrames = 5
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "박힘", frames = 60, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 1f),
            move = KyodaiHandMove.Spring, springFrequency = 6f, springDamping = 0.9f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.05f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "뽑기", frames = 14, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 2.4f),
            move = KyodaiHandMove.Spring, springFrequency = 2.5f, springDamping = 0.6f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.3f
        });
    }

    // ───────────────────────── 경기장·배치 ─────────────────────────

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
            return new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
        }
        float dist = Mathf.Abs(main.transform.position.z);
        float height = 2f * dist * Mathf.Tan(main.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * main.aspect;
        Vector2 center = main.transform.position;
        return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
    }

    // 원근 카메라: 뒤로 back만큼 간 물체가 경기장 평면의 planePoint 자리에 같은 크기로 보이도록 위치·크기를 맞춘다
    private static void PlaceBehind(Transform t, Vector2 planePoint, float back)
    {
        var main = Camera.main;
        if (main == null || main.orthographic)
        {
            t.position = new Vector3(planePoint.x, planePoint.y, back);
            return;
        }
        float dist = Mathf.Max(Mathf.Abs(main.transform.position.z), 0.1f);
        float k = (dist + back) / dist;
        Vector2 cam = main.transform.position;
        t.position = new Vector3(cam.x + (planePoint.x - cam.x) * k, cam.y + (planePoint.y - cam.y) * k, back);
        t.localScale = Vector3.one * k;
    }

    // ───────────────────────── 임시 도트 그림 ─────────────────────────

    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    private static readonly Color32 Outline = new Color32(28, 30, 38, 255);
    private static readonly Color32 Dark = new Color32(78, 84, 98, 255);
    private static readonly Color32 Mid = new Color32(118, 126, 142, 255);
    private static readonly Color32 Bright = new Color32(170, 178, 192, 255);
    private static readonly Color32 Shine = new Color32(214, 220, 230, 255);
    private static readonly Color32 Red = new Color32(226, 64, 52, 255);
    private static readonly Color32 RedGlow = new Color32(255, 150, 120, 255);

    private static bool RoundRect(float x, float y, float cx, float cy, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(x - cx) - (hx - r), qy = Mathf.Abs(y - cy) - (hy - r);
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f);
        return outside <= r;
    }

    private static bool Circle(float x, float y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

    // inside로 모양을, 위아래 명암으로 입체를 낸다. detail이 색을 돌려주면 그 색으로 덮는다
    private static Func<int, int, Color32> Shade(Func<float, float, bool> inside, int h, Func<int, int, Color32?> detail = null,
                                                 float lightAbove = 0.62f, float darkBelow = 0.28f)
    {
        return (x, y) =>
        {
            float px = x + 0.5f, py = y + 0.5f;
            if (!inside(px, py)) return Clear;
            if (!inside(px + 1, py) || !inside(px - 1, py) || !inside(px, py + 1) || !inside(px, py - 1)) return Outline;
            Color32? d = detail?.Invoke(x, y);
            if (d.HasValue) return d.Value;
            float v = y / (float)h;
            if (!inside(px - 2, py + 2)) return Shine;   // 왼쪽 위 테두리 빛
            if (!inside(px + 2, py - 2)) return Dark;    // 오른쪽 아래 그늘
            return v > lightAbove ? Bright : v < darkBelow ? Dark : Mid;
        };
    }

    // 주먹: 소지가 아래로 가는 망치 주먹 (2026-11-04 사용자 요청) — 엄지가 위에서 감싸고, 말아쥔 손가락 4개가 위아래로 쌓여
    // 소지가 맨 아래, 손가락 끝 마디가 오른쪽(바깥), 손목 띠가 왼쪽(몸 쪽). 오른손 기준이고 왼손은 flipX로 거울
    private const int FistW = 34, FistH = 40;
    private static Func<int, int, Color32> PaintFist => Shade((x, y) =>
            RoundRect(x, y, 18f, 18f, 12f, 14f, 5f)                                                                       // 말아쥔 손가락 덩어리
            || Circle(x, y, 28f, 28.5f, 4.6f) || Circle(x, y, 29f, 21.5f, 4.6f) || Circle(x, y, 29f, 14.5f, 4.6f) || Circle(x, y, 28f, 8f, 4f) // 손가락 끝 마디 (검지 → 소지)
            || RoundRect(x, y, 17f, 32.5f, 11f, 4.5f, 3.5f)                                                               // 위로 감싼 엄지
            || RoundRect(x, y, 4.5f, 18f, 4.5f, 9f, 2f),                                                                  // 손목
        FistH,
        (x, y) =>
        {
            if ((y == 25 || y == 18 || y == 11) && x >= 13 && x <= 33) return Dark; // 손가락 사이
            if (y == 28 && x >= 7 && x <= 25) return Dark;                          // 엄지 아래 선
            if (x == 13 && y >= 4 && y <= 27) return Dark;                          // 손가락 접힌 선
            if (x >= 2 && x <= 5 && y >= 10 && y <= 26) return Red;                // 손목 띠 (쿄다이 색)
            return null;
        },
        0.7f, 0.15f);

    private static Func<int, int, Color32> PaintPalm => Shade((x, y) =>
            RoundRect(x, y, 23f, 15f, 15f, 13f, 6f)              // 손바닥
            || RoundRect(x, y, 12f, 34f, 3.5f, 12f, 3f) || RoundRect(x, y, 19.5f, 37f, 3.5f, 14f, 3f)
            || RoundRect(x, y, 27f, 36f, 3.5f, 13f, 3f) || RoundRect(x, y, 34f, 32f, 3.5f, 10f, 3f) // 손가락 4개
            || RoundRect(x, y, 6f, 20f, 4f, 9f, 3.5f),             // 엄지
        52,
        (x, y) =>
        {
            if ((x == 12 || x == 19 || x == 27 || x == 34) && (y == 30 || y == 40)) return Dark; // 손가락 마디
            if (Circle(x + 0.5f, y + 0.5f, 23f, 15f, 5f) && !Circle(x + 0.5f, y + 0.5f, 23f, 15f, 3.5f)) return Dark; // 손바닥 가운데 장치
            if (Circle(x + 0.5f, y + 0.5f, 23f, 15f, 2f)) return Red;
            if (x >= 10 && x <= 36 && y >= 3 && y <= 5) return Red; // 손목 띠
            return null;
        });

    private static Func<int, int, Color32> PaintBody => Shade((x, y) =>
            RoundRect(x, y, 60f, 38f, 42f, 34f, 12f)             // 몸통
            || RoundRect(x, y, 60f, 84f, 24f, 20f, 9f)           // 머리
            || RoundRect(x, y, 14f, 58f, 12f, 10f, 5f) || RoundRect(x, y, 106f, 58f, 12f, 10f, 5f), // 어깨
        110,
        (x, y) =>
        {
            float px = x + 0.5f, py = y + 0.5f;
            if (Circle(px, py, 60f, 84f, 6f)) return Circle(px, py, 58f, 86f, 2.5f) ? RedGlow : Red; // 메인 렌즈
            if (Circle(px, py, 60f, 84f, 8f)) return Outline;
            if (y >= 44 && y <= 46 && x >= 30 && x <= 90) return Dark;   // 가슴 판 선
            if ((x == 45 || x == 75) && y >= 12 && y <= 44) return Dark;
            if (Circle(px, py, 60f, 28f, 6f)) return Circle(px, py, 60f, 28f, 3f) ? RedGlow : Red; // 가슴 코어
            return null;
        });

    private static Func<int, int, Color32> PaintShadow => (x, y) =>
    {
        float nx = (x + 0.5f - 16f) / 16f, ny = (y + 0.5f - 5f) / 5f;
        float d = nx * nx + ny * ny;
        return d <= 0.55f ? new Color32(255, 255, 255, 255) : d <= 1f ? new Color32(255, 255, 255, 128) : Clear;
    };

    // 임시 그림을 코드 기준으로 다시 그린다 (모양을 고친 뒤). 같은 파일이라 씬·에셋 연결은 그대로 유지된다
    [MenuItem("NYH/쿄다이/2D 임시 그림 다시 그리기")]
    private static void RepaintTempImages()
    {
        if (!EditorUtility.DisplayDialog("쿄다이", $"{ImageFolder}의 임시_ 그림 4장을 코드 기준으로 덮어씁니다. 진짜 그림을 같은 이름으로 넣어뒀다면 사라집니다.", "다시 그리기", "취소"))
            return;
        PaintTempImages(true, out _, out _, out _, out _);
        Debug.Log("[Kyodai] 2D 임시 그림 다시 그림");
    }

    private static void PaintTempImages(bool force, out Sprite fist, out Sprite palm, out Sprite body, out Sprite shadow)
    {
        fist = LoadOrPaint($"{ImageFolder}/임시_주먹.png", FistW, FistH, 18f, PaintFist, force);
        palm = LoadOrPaint($"{ImageFolder}/임시_손바닥.png", 44, 52, 18f, PaintPalm, force);
        body = LoadOrPaint($"{ImageFolder}/임시_본체.png", 120, 110, 10f, PaintBody, force);
        shadow = LoadOrPaint($"{ImageFolder}/임시_그림자.png", 32, 10, 32f, PaintShadow, force);
    }

    // 이미 있으면 그대로 쓴다 — 진짜 그림으로 덮어쓴 파일을 지우지 않게 (force = 다시 그리기 메뉴)
    private static Sprite LoadOrPaint(string path, int w, int h, float ppu, Func<int, int, Color32> paint, bool force = false)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null && !force) return existing;

        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = paint(x, y);
        tex.SetPixels32(pixels);
        File.WriteAllBytes(Path.Combine(Application.dataPath, path.Substring("Assets/".Length)), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ───────────────────────── 공용 ─────────────────────────

    private static void SetFields(Object target, Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T LoadOrCreate<T>(string path, Action<T> init) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
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
