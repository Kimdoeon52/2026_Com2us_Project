using System;
using System.IO;
using RealSteel.Boss.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 메뉴 한 번(NYH/쿄다이/2D 쿄다이 만들기·갱신)으로 지금 열린 씬에 "2D 임시 쿄다이"를 세우거나 빠진 것을 채운다 (2026-11-04 2D 개편).
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
    private const float DefaultSize = 0.4f; // 새로 만들 때 보스 크기(루트 스케일) — 2026-11-04 사용자: 플레이어에 맞춰 0.4
    private const float BodyHeight = 6.5f;  // 본체 중심의 바닥 위 높이(보스 단위)

    // 메뉴 순서: 만들기·갱신(0) → 미리보기(1, Kyodai2DPreviewWindow) │ 임시 그림 다시 그리기(20)
    public const int MenuPriority = 0;

    /// <summary>
    /// 2D 쿄다이 만들기·갱신 하나로 (2026-11-04 메뉴 통합). 씬에 2D 쿄다이가 없으면 새로 세우고,
    /// 있으면 빠진 것(본체·바닥 예고·손·마스터 연결·패턴·눈)만 채운다. 이미 있는 것·손으로 고친 값은 건드리지 않는다.
    /// 새 패턴을 Patterns에 추가했을 때도 이 메뉴 한 번이면 된다.
    /// </summary>
    [MenuItem("NYH/쿄다이/2D 쿄다이 만들기·갱신 (현재 씬)", false, MenuPriority)]
    internal static void BuildOrUpdate()
    {
        var added = new System.Collections.Generic.List<string>();

        // ── 1. 임시 그림·데이터 (없을 때만 만든다) ──
        PaintTempImages(false, out Sprite fist, out Sprite palm, out Sprite bodySprite, out Sprite shadowSprite);
        var feel = LoadOrCreate<KyodaiHandFeel>($"{DataFolder}/Kyodai2D_손느낌.asset", _ => { });
        var master = LoadOrCreate<BossMasterData>($"{DataFolder}/Kyodai2D_Master.asset", m =>
        {
            m.bossID = "BOSS_REGION_02_KYODAI";
            m.bossName = "쿄다이";
            m.patterns.Clear(); // 패턴은 EnsurePattern이 채운다
            m.gimmickParts.Clear();
            m.phases.Clear();
            m.phases.Add(new BossPhaseData { phaseIndex = 1, hpThresholdRatio = 1f });   // 패턴 목록 비움 = 전부
            m.phases.Add(new BossPhaseData { phaseIndex = 2, hpThresholdRatio = 0.5f }); // 50%는 임시값(2026-11-04 사용자: HP 비율 2페이즈)
            // maxHp·방어력은 기획 수치 없음 — KKH 기본값 그대로(TODO)
        });

        // ── 2. 보스 루트 (없으면 경기장을 카메라로 짐작해 새로) ──
        var boss = Object.FindAnyObjectByType<KyodaiBossController>();
        bool created = boss == null;
        if (created)
        {
            Rect arena = GuessArena(out float groundY);
            var rootGo = new GameObject("쿄다이 (2D)");
            Undo.RegisterCreatedObjectUndo(rootGo, "2D 쿄다이 만들기");
            rootGo.tag = "Boss";
            rootGo.transform.position = new Vector3(arena.center.x, groundY, 0f);
            rootGo.transform.localScale = Vector3.one * DefaultSize;
            boss = rootGo.AddComponent<KyodaiBossController>();
            SetFields(boss, so =>
            {
                so.FindProperty("arenaMinX").floatValue = arena.xMin + 1f;
                so.FindProperty("arenaMaxX").floatValue = arena.xMax - 1f;
                so.FindProperty("groundY").floatValue = groundY;
            });
            added.Add("보스");
        }
        Transform root = boss.transform;
        var bossSo = new SerializedObject(boss);

        if (bossSo.FindProperty("masterData").objectReferenceValue == null)
        {
            SetFields(boss, so => so.FindProperty("masterData").objectReferenceValue = master);
            added.Add("마스터 연결");
        }
        else master = bossSo.FindProperty("masterData").objectReferenceValue as BossMasterData;

        // ── 3. 본체 (경기장 뒤, 원근 카메라면 뒤로 간 만큼 키우고 화면상 같은 자리에 보이게 옮긴다) ──
        if (bossSo.FindProperty("body").objectReferenceValue == null)
        {
            var bodyGo = new GameObject("본체", typeof(SpriteRenderer));
            Undo.RegisterCreatedObjectUndo(bodyGo, "2D 쿄다이 본체");
            bodyGo.transform.SetParent(root, false);
            var bodyRenderer = bodyGo.GetComponent<SpriteRenderer>();
            bodyRenderer.sprite = bodySprite;
            bodyRenderer.sortingOrder = -20;
            bodyRenderer.color = new Color(0.62f, 0.64f, 0.7f); // 배경이라 살짝 어둡게
            PlaceBehind(bodyGo.transform, new Vector2(root.position.x, boss.GroundY + BodyHeight * boss.Size), BodyBackZ);
            SetFields(boss, so => so.FindProperty("body").objectReferenceValue = bodyGo.transform);
            added.Add("본체");
        }
        else
        {
            // 루트 스케일을 바꾸면 본체의 깊이(z)도 같이 줄어 원근이 어긋난다 → 깊이가 BodyBackZ에서 벗어났으면 다시 놓는다
            var body = (Transform)bossSo.FindProperty("body").objectReferenceValue;
            if (Mathf.Abs(body.position.z - BodyBackZ) > 0.01f)
            {
                Undo.RecordObject(body, "2D 쿄다이 본체 다시 놓기");
                PlaceBehind(body, new Vector2(root.position.x, boss.GroundY + BodyHeight * boss.Size), BodyBackZ);
                added.Add("본체 자리 다시 맞춤(보스 크기 변경)");
            }
        }

        // ── 4. 바닥 예고·손 ──
        var telegraph = EnsureTelegraph(boss, MainTelegraphName, added);

        if (!HasHand(boss, KyodaiHandSide.Left))
        {
            Hand("왼손", "왼팔", KyodaiHandSide.Left, new Vector2(-4.5f, 5f), root, feel, fist, palm, shadowSprite);
            added.Add("왼손");
        }
        if (!HasHand(boss, KyodaiHandSide.Right))
        {
            Hand("오른손", "오른팔", KyodaiHandSide.Right, new Vector2(4.5f, 5f), root, feel, fist, palm, shadowSprite);
            added.Add("오른손");
        }

        // ── 5. 패턴·눈 ──
        foreach (var spec in Patterns)
            if (EnsurePattern(boss, telegraph, master, spec)) added.Add(spec.name);
        if (EnsureLaser(boss, master)) added.Add("레이저");

        // ── 6. 내려찍는 기물(1페이즈부터) · 2페이즈: 양손 패턴·전환 동작·페이즈별 패턴 목록 ──
        var secondTelegraph = EnsureTelegraph(boss, SecondTelegraphName, added);
        foreach (var spec in DualPatterns)
            if (EnsureDualPattern(boss, telegraph, secondTelegraph, master, spec)) added.Add(spec.name);
        if (EnsureCrusher(boss, added)) added.Add("내려찍는 기물");
        if (EnsureTentacles(boss)) added.Add("촉수 생성기");
        if (EnsureShockwave(boss)) added.Add("충격파");
        if (EnsureLaserSweep(boss, master)) added.Add("레이저 쓸기");
        EnsureTransitionMotion(boss, added);
        SyncPhases(master, added);

        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        Selection.activeGameObject = boss.gameObject;
        string what = added.Count > 0 ? string.Join(", ", added) : "없음 (이미 다 있음)";
        Debug.Log(created
            ? $"[Kyodai] 2D 쿄다이 생성 — 경기장 x {boss.ArenaMinX:0.#} ~ {boss.ArenaMaxX:0.#}, 바닥 y={boss.GroundY:0.##}. 경기장 범위·바닥은 인스펙터에서 확인할 것. " +
              "씬에 3D 쿄다이가 있으면 꺼둘 것. Player에 HurtboxColliderSync('Player' 태그), 씬에 DummyBattleBootstrap·HitDetection이 있어야 서로 때린다."
            : $"[Kyodai] 2D 쿄다이 갱신 — 새로 붙인 것: {what}");
    }

    private static bool HasHand(KyodaiBossController boss, KyodaiHandSide side)
    {
        foreach (var hand in boss.GetComponentsInChildren<KyodaiHand>(true))
            if (hand.Side == side) return true;
        return false;
    }

    // ───────────────────────── 레이저 ─────────────────────────

    // 본체 렌즈가 화면에서 겹쳐 보이는 경기장 평면 자리 = 보스 루트 기준 (0, 9.4)
    // (본체 중심 바닥 위 6.5 + 임시 그림 렌즈가 중심에서 29px 위 / 10ppu = 2.9)
    private static readonly Vector2 EyeLocal = new Vector2(0f, 9.4f);

    // 눈(KyodaiLaser)과 레이저 패턴이 없으면 만든다. 새로 붙였으면 true
    private static bool EnsureLaser(KyodaiBossController boss, BossMasterData master)
    {
        var laser = boss.GetComponentInChildren<KyodaiLaser>(true);
        if (laser == null)
        {
            var eye = new GameObject("눈 (레이저)");
            Undo.RegisterCreatedObjectUndo(eye, "2D 쿄다이 눈");
            eye.transform.SetParent(boss.transform, false);
            eye.transform.localPosition = EyeLocal;
            laser = eye.AddComponent<KyodaiLaser>();
        }

        var laserData = LoadOrCreate<KyodaiLaserData>($"{DataFolder}/Kyodai2D_레이저_설정.asset", _ => { });
        var data = LoadOrCreate<BossPatternData>($"{DataFolder}/Kyodai2D_레이저.asset", p =>
        {
            p.patternID = "KYODAI2D_LASER";
            p.patternName = "레이저";
            p.description = "2D 개편: 본체 눈이 플레이어를 노린다 — 조준선이 늦게 따라오며 2초간 좁아지고, 고정·깜빡임 뒤 발사. " +
                            "손에 맞으면 그 손이 4초 정지(손에서 막힘). 플레이어가 레이저를 손 쪽으로 유도한다";
            p.tellDuration = (laserData.aimFrames + laserData.lockFrames) / 60f;
            p.postDelay = 1f;
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.Core;
            p.partDurabilityDamage = 0;
            // baseDamage는 기획 수치 없음 — KKH 기본값 그대로(TODO)
        });
        if (master != null && !master.patterns.Contains(data))
        {
            master.patterns.Add(data);
            EditorUtility.SetDirty(master);
            AssetDatabase.SaveAssets();
        }

        var companion = LoadOrCreate<KyodaiHandMotion>($"{DataFolder}/Kyodai2D_레이저용주먹_모션.asset", FillFistSlamHold);

        foreach (var existing in boss.GetComponentsInChildren<KyodaiLaserPattern>(true))
        {
            if (existing.PatternData != data) continue;
            if (existing.CompanionMotion != null) return false; // 손으로 바꿔둔 동반 동작은 건드리지 않는다
            SetCompanion(existing, companion);
            return true;
        }

        var go = new GameObject("레이저");
        Undo.RegisterCreatedObjectUndo(go, "2D 쿄다이 패턴 추가");
        go.transform.SetParent(boss.transform, false);
        var pattern = go.AddComponent<KyodaiLaserPattern>();
        SetFields(pattern, so =>
        {
            so.FindProperty("patternData").objectReferenceValue = data;
            so.FindProperty("laser").objectReferenceValue = laser;
            so.FindProperty("laserData").objectReferenceValue = laserData;
            so.FindProperty("telegraph").objectReferenceValue = FindTelegraph(boss, MainTelegraphName);
        });
        SetCompanion(pattern, companion);
        return true;
    }

    // 동반 손 동작 = 레이저용 주먹, 플레이어에게 가까운 손 (2026-11-04 사용자: 주먹 동작 붙이기)
    private static void SetCompanion(KyodaiLaserPattern pattern, KyodaiHandMotion companion)
    {
        SetFields(pattern, so =>
        {
            so.FindProperty("companionMotion").objectReferenceValue = companion;
            so.FindProperty("companionHand").enumValueIndex = (int)KyodaiHandChoice.NearestToPlayer;
            if (so.FindProperty("telegraph").objectReferenceValue == null)
                so.FindProperty("telegraph").objectReferenceValue = FindTelegraph(pattern.GetComponentInParent<KyodaiBossController>(), MainTelegraphName);
        });
    }

    // ───────────────────────── 패턴 목록 ─────────────────────────

    private const string MainTelegraphName = "바닥 예고";
    private const string SecondTelegraphName = "바닥 예고 2";   // 양손 패턴의 두 번째 손
    private const string CrusherTelegraphName = "기물 예고";

    private static GroundTelegraph FindTelegraph(KyodaiBossController boss, string name)
    {
        if (boss == null) return null;
        Transform t = boss.transform.Find(name);
        return t != null ? t.GetComponent<GroundTelegraph>() : null;
    }

    // 이름으로 찾고 없으면 만든다 (예고가 여러 개라 이름으로 구분)
    private static GroundTelegraph EnsureTelegraph(KyodaiBossController boss, string name, System.Collections.Generic.List<string> added)
    {
        var found = FindTelegraph(boss, name);
        if (found != null) return found;
        var go = new GameObject(name, typeof(SpriteRenderer), typeof(GroundTelegraph));
        Undo.RegisterCreatedObjectUndo(go, "2D 쿄다이 바닥 예고");
        go.transform.SetParent(boss.transform, false);
        go.GetComponent<SpriteRenderer>().sortingOrder = -10;
        added.Add(name);
        return go.GetComponent<GroundTelegraph>();
    }

    private class PatternSpec
    {
        public int minPhase = 1;            // 이 페이즈부터 나온다
        public string name, file, id, description;
        public int tellFrames;              // 패턴 데이터 tellDuration = 동작의 예고 키 프레임 합
        public KyodaiHandChoice hand;
        public Action<KyodaiHandMotion, KyodaiBossController> fill; // 보스: 경기장 너비·크기가 필요한 동작(손바닥 압착)
    }

    // 새 패턴은 여기에 한 줄 추가 → '2D 쿄다이 만들기·갱신' 메뉴. 프레임·높이는 전부 임시값(기획 수치 없음)
    private static readonly PatternSpec[] Patterns =
    {
        new PatternSpec
        {
            name = "주먹 내려찍기", file = "주먹내려찍기", id = "KYODAI2D_FIST_SLAM", tellFrames = 66, hand = KyodaiHandChoice.Alternate, fill = (m, b) => FillFistSlam(m),
            description = "2D 개편: 손이 플레이어 위로 떠서 따라오다(그림자 예고) 끌어올린 뒤 내려찍고 잠깐 박혀 있다가 돌아간다. 한 손씩 번갈아"
        },
        new PatternSpec
        {
            name = "손바닥 밀기", file = "손바닥밀기", id = "KYODAI2D_PALM_PUSH", tellFrames = 56, hand = KyodaiHandChoice.FarthestFromPlayer, fill = (m, b) => FillPalmPush(m),
            description = "2D 개편: 플레이어에게서 먼 손이 경기장 반대쪽 끝으로 내려가 손바닥을 펴고(바닥 전체 예고) 뒤로 젖혔다가 " +
                          "바닥을 쓸며 반대편 끝까지 민다. 점프로는 못 넘는 높이 — 회피 무적으로 통과"
        },
        // ── 2026-11-04 원본 기획서 패턴 되살리기 ──
        new PatternSpec
        {
            name = "주먹 5연타", file = "주먹5연타", id = "KYODAI2D_FIST_COMBO", tellFrames = 34, hand = KyodaiHandChoice.NearestToPlayer, minPhase = 1,
            fill = (m, b) => FillFistCombo(m),
            description = "원본 1페이즈 '손가락 5연타': 점점 빨라지는 리듬으로 플레이어를 쫓아 5번 내려찍고, 마지막 주먹은 3초 박혀 있다(딜 타임)"
        },
        new PatternSpec
        {
            name = "손바닥 압착", file = "손바닥압착", id = "KYODAI2D_PALM_PRESS", tellFrames = 72, hand = KyodaiHandChoice.Alternate, minPhase = 2,
            fill = FillPalmPress,
            description = "원본 2페이즈: 경기장 1/3을 덮는 그림자 1.2초 → 크게 펼친 손바닥이 그 구역을 짓누름 → 3초 잔류. 그 구역에서 빠져나가야 한다"
        },
        new PatternSpec
        {
            name = "충격파 내려치기", file = "충격파내려치기", id = "KYODAI2D_SHOCK_SLAM", tellFrames = 48, hand = KyodaiHandChoice.NearestToPlayer, minPhase = 2,
            fill = (m, b) => FillShockSlam(m),
            description = "원본 2페이즈: 주먹 그림자 0.8초 → 내려찍으면 충격파가 바닥을 따라 좌우로 퍼짐(점프로 넘음) → 주먹 2초 잔류"
        },
    };

    // 없으면 데이터·동작 에셋과 패턴 오브젝트를 만들고 마스터 목록에 넣는다. 새로 붙였으면 true
    private static bool EnsurePattern(KyodaiBossController boss, GroundTelegraph telegraph, BossMasterData master, PatternSpec spec)
    {
        var motion = LoadOrCreate<KyodaiHandMotion>($"{DataFolder}/Kyodai2D_{spec.file}_모션.asset", m => spec.fill(m, boss));
        var data = LoadOrCreate<BossPatternData>($"{DataFolder}/Kyodai2D_{spec.file}.asset", p =>
        {
            p.patternID = spec.id;
            p.patternName = spec.name;
            p.description = spec.description;
            p.tellDuration = spec.tellFrames / 60f;
            p.postDelay = 1f; // KKH 공통 딜타임
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.Core;
            p.partDurabilityDamage = 0;
            // baseDamage는 기획 수치 없음 — KKH 기본값 그대로(TODO)
        });
        if (master != null && !master.patterns.Contains(data))
        {
            master.patterns.Add(data);
            EditorUtility.SetDirty(master);
            AssetDatabase.SaveAssets();
        }

        foreach (var existing in boss.GetComponentsInChildren<KyodaiHandPattern>(true))
            if (existing.PatternData == data) return false;

        var go = new GameObject(spec.name);
        Undo.RegisterCreatedObjectUndo(go, "2D 쿄다이 패턴 추가");
        go.transform.SetParent(boss.transform, false);
        var pattern = go.AddComponent<KyodaiHandPattern>();
        SetFields(pattern, so =>
        {
            so.FindProperty("patternData").objectReferenceValue = data;
            so.FindProperty("motion").objectReferenceValue = motion;
            so.FindProperty("handChoice").enumValueIndex = (int)spec.hand;
            so.FindProperty("telegraph").objectReferenceValue = telegraph;
        });
        return true;
    }

    private static void Hand(string name, string partId, KyodaiHandSide side, Vector2 homeLocal, Transform root,
                             KyodaiHandFeel feel, Sprite fist, Sprite palm, Sprite shadowSprite)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "2D 쿄다이 손");
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
    private static void FillFistSlam(KyodaiHandMotion m) => FillFistSlam(m, 60, 1f);

    // 레이저용 주먹 (2026-11-04): 같은 내려찍기인데 레이저 발사가 끝날 때까지 박혀서 버틴다.
    // 레이저 발사 140~164f(조준 120 + 고정 20 + 발사 24) ↔ 찍기 끝 약 78f(66 + 7 + 충격 멈춤 5) + 박힘 100f = 178f.
    // 플레이어가 박힌 주먹의 바깥쪽 옆에 서면 눈 → 플레이어 선을 주먹이 막는다 → 주먹 4초 정지(때릴 기회).
    // 높이 1.1 = 망치 주먹 그림(세로 40px / 18ppu ≈ 2.2)의 아래 끝이 바닥에 닿는 자리
    private static void FillFistSlamHold(KyodaiHandMotion m) => FillFistSlam(m, 100, 1.1f);

    private static void FillFistSlam(KyodaiHandMotion m, int embedFrames, float groundHeight)
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
            label = "내려찍기", frames = 7, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, groundHeight),
            move = KyodaiHandMove.Ease, easeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.6f, 0f)),
            pose = KyodaiHandPose.Fist, floatWeight = 0f, telegraphWidth = 2.6f,
            hitActive = true, hitSize = new Vector2(2f, 1.8f),
            impactOnEnd = true, impactShake = 0.35f, impactFreezeFrames = 5
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "박힘", frames = embedFrames, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, groundHeight),
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

    // 손바닥 밀기: 반대쪽 끝으로 내려가기 → 뒤로 젖히기(예비 동작) → 바닥을 쓸며 밀기 → 멈춤 → 올라가기.
    // 방향 키(mirrorByDirection)라 플레이어가 어느 쪽에 있든 반대쪽 끝에서 플레이어 쪽으로 민다.
    // 기본 점프 높이(약 0.9)로는 손바닥(키 약 2.4)을 못 넘는다 → 회피 무적(0.3초)으로 통과하는 패턴. 밀기 속도 약 30/초 → 겹치는 시간 약 0.09초
    private static void FillPalmPush(KyodaiHandMotion m)
    {
        const float palmY = 1.45f; // 손바닥 중심 높이 — 아래 끝이 바닥에 닿게 (임시 그림 52px / 18ppu ≈ 2.9)
        m.keys.Clear();
        m.keys.Add(new KyodaiHandKey
        {
            label = "반대쪽 끝으로", frames = 40, anchor = KyodaiHandAnchor.ArenaFar, offset = new Vector2(-1.2f, palmY),
            mirrorByDirection = true, clampToArena = false,
            move = KyodaiHandMove.Spring, springFrequency = 1.5f, springDamping = 0.8f,
            pose = KyodaiHandPose.Palm, floatWeight = 0.5f, telegraphFullArena = true
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "뒤로 젖히기", frames = 16, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(-0.9f, palmY + 0.25f),
            mirrorByDirection = true, clampToArena = false,
            move = KyodaiHandMove.Spring, springFrequency = 3.2f, springDamping = 0.5f,
            pose = KyodaiHandPose.Palm, rotation = 10f, scale = 1.1f, floatWeight = 0.1f, telegraphFullArena = true
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "밀기", frames = 36, anchor = KyodaiHandAnchor.ArenaNear, offset = new Vector2(2.5f, palmY),
            mirrorByDirection = true, clampToArena = false,
            move = KyodaiHandMove.Ease,
            easeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.3f, 0.25f, 1.6f, 1.6f), new Keyframe(1f, 1f, 1.1f, 0f)),
            pose = KyodaiHandPose.Palm, rotation = -6f, scale = 1.05f, floatWeight = 0f,
            hitActive = true, hitSize = new Vector2(1.8f, 2.4f),
            impactShake = 0f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "멈춤", frames = 20, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, palmY),
            mirrorByDirection = true, clampToArena = false,
            move = KyodaiHandMove.Spring, springFrequency = 4f, springDamping = 0.6f,
            pose = KyodaiHandPose.Palm, floatWeight = 0.3f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "올라가기", frames = 20, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 5f),
            mirrorByDirection = true, clampToArena = false,
            move = KyodaiHandMove.Spring, springFrequency = 2f, springDamping = 0.7f,
            pose = KyodaiHandPose.Palm, floatWeight = 0.6f
        });
    }

    // ───────────────────────── 2페이즈 ─────────────────────────

    private class DualSpec
    {
        public string name, file, id, description;
        public int tellFrames, leftDelay, rightDelay;
        public bool shareHit;
        public Action<KyodaiHandMotion> fillLeft, fillRight;
    }

    // 2페이즈 전용 양손 패턴 (2026-11-04 사용자 결정). 프레임·높이는 전부 임시값
    private static readonly DualSpec[] DualPatterns =
    {
        new DualSpec
        {
            name = "양손 협공", file = "양손협공", id = "KYODAI2D_DUAL_CLAP", tellFrames = 56, shareHit = true,
            fillLeft = m => FillClap(m, -1), fillRight = m => FillClap(m, +1),
            description = "2페이즈: 양손이 경기장 양 끝으로 내려가 손바닥을 펴고(바닥 전체 예고) 젖혔다가 바닥을 쓸며 플레이어 자리로 모여 손뼉. " +
                          "회피로 통과. 맞붙은 채 잠깐 잔류. 두 손에 같이 닿아도 한 번만 맞음"
        },
        new DualSpec
        {
            name = "양손 번갈아 연타", file = "양손연타", id = "KYODAI2D_DUAL_BARRAGE", tellFrames = 40, leftDelay = 0, rightDelay = 55, shareHit = false,
            fillLeft = FillBarrageLeft, fillRight = FillBarrageRight,
            description = "2페이즈: 왼손 → 오른손 → 왼손 순으로 플레이어를 쫓아 빠르게 내려찍기 3번. 한 번마다 따로 맞을 수 있음"
        },
        new DualSpec
        {
            name = "양팔 동시 내려찍기", file = "양팔동시", id = "KYODAI2D_DUAL_SLAM", tellFrames = 48, shareHit = true,
            fillLeft = m => FillBothSlam(m, -1), fillRight = m => FillBothSlam(m, +1),
            description = "원본 3페이즈(2페이즈로): 예고 0.8초 → 양팔이 플레이어 양옆을 동시에 내려찍고 3초 잔류(큰 딜 타임). " +
                          "한쪽 팔이 무력화돼 누워 있으면 다른 팔만 찍는다"
        },
    };

    private static bool EnsureDualPattern(KyodaiBossController boss, GroundTelegraph leftTelegraph, GroundTelegraph rightTelegraph,
                                          BossMasterData master, DualSpec spec)
    {
        var left = LoadOrCreate<KyodaiHandMotion>($"{DataFolder}/Kyodai2D_{spec.file}_왼손_모션.asset", spec.fillLeft);
        var right = LoadOrCreate<KyodaiHandMotion>($"{DataFolder}/Kyodai2D_{spec.file}_오른손_모션.asset", spec.fillRight);
        var data = LoadOrCreate<BossPatternData>($"{DataFolder}/Kyodai2D_{spec.file}.asset", p =>
        {
            p.patternID = spec.id;
            p.patternName = spec.name;
            p.description = spec.description;
            p.tellDuration = spec.tellFrames / 60f;
            p.postDelay = 1f;
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.Core;
            p.partDurabilityDamage = 0;
            // baseDamage는 기획 수치 없음 — KKH 기본값 그대로(TODO)
        });
        if (master != null && !master.patterns.Contains(data))
        {
            master.patterns.Add(data);
            EditorUtility.SetDirty(master);
        }

        foreach (var existing in boss.GetComponentsInChildren<KyodaiDualHandPattern>(true))
            if (existing.PatternData == data) return false;

        var go = new GameObject(spec.name);
        Undo.RegisterCreatedObjectUndo(go, "2D 쿄다이 패턴 추가");
        go.transform.SetParent(boss.transform, false);
        var pattern = go.AddComponent<KyodaiDualHandPattern>();
        SetFields(pattern, so =>
        {
            so.FindProperty("patternData").objectReferenceValue = data;
            so.FindProperty("leftMotion").objectReferenceValue = left;
            so.FindProperty("rightMotion").objectReferenceValue = right;
            so.FindProperty("leftDelayFrames").intValue = spec.leftDelay;
            so.FindProperty("rightDelayFrames").intValue = spec.rightDelay;
            so.FindProperty("leftTelegraph").objectReferenceValue = leftTelegraph;
            so.FindProperty("rightTelegraph").objectReferenceValue = rightTelegraph;
            so.FindProperty("shareHit").boolValue = spec.shareHit;
        });
        return true;
    }

    // 양손 협공 한 손 (side -1 = 왼손, +1 = 오른손): 끝으로 → 젖히기 → 플레이어 자리로 모으기(판정·손뼉 충격) → 맞붙음 → 올라가기.
    // 두 손이 같은 틱에 '모으기'를 시작해 같은 플레이어 x로 모인다(시작 때 고정). 예고 그림자는 왼손만
    private static void FillClap(KyodaiHandMotion m, int side)
    {
        const float palmY = 1.45f;
        bool left = side < 0;
        m.keys.Clear();
        m.keys.Add(new KyodaiHandKey
        {
            label = "끝으로", frames = 40, anchor = left ? KyodaiHandAnchor.ArenaLeft : KyodaiHandAnchor.ArenaRight,
            offset = new Vector2(1.2f * side, palmY), clampToArena = false,
            move = KyodaiHandMove.Spring, springFrequency = 1.5f, springDamping = 0.8f,
            pose = KyodaiHandPose.Palm, floatWeight = 0.5f, telegraphFullArena = left
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "젖히기", frames = 16, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0.9f * side, palmY + 0.25f), clampToArena = false,
            move = KyodaiHandMove.Spring, springFrequency = 3.2f, springDamping = 0.5f,
            pose = KyodaiHandPose.Palm, rotation = 10f * -side, scale = 1.1f, floatWeight = 0.1f, telegraphFullArena = left
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "모으기", frames = 30, anchor = KyodaiHandAnchor.Player, offset = new Vector2(0.75f * side, palmY),
            move = KyodaiHandMove.Ease, easeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.2f, 0f)),
            pose = KyodaiHandPose.Palm, rotation = 6f * side, scale = 1.05f, floatWeight = 0f,
            hitActive = true, hitSize = new Vector2(1.8f, 2.4f),
            impactOnEnd = true, impactShake = 0.4f, impactFreezeFrames = 6
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "맞붙음", frames = 40, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, palmY),
            move = KyodaiHandMove.Spring, springFrequency = 6f, springDamping = 0.9f,
            pose = KyodaiHandPose.Palm, floatWeight = 0.05f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "올라가기", frames = 20, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 5f),
            move = KyodaiHandMove.Spring, springFrequency = 2f, springDamping = 0.7f,
            pose = KyodaiHandPose.Palm, floatWeight = 0.6f
        });
    }

    // 빠른 내려찍기 한 번 (70f): 조준 30 → 끌어올림 10 → 찍기 6(판정·충격) → 박힘 14 → 뽑기 10
    private static void AddQuickSlam(KyodaiHandMotion m)
    {
        m.keys.Add(new KyodaiHandKey
        {
            label = "조준", frames = 30, anchor = KyodaiHandAnchor.Player, offset = new Vector2(0f, 4.5f), trackPlayer = true,
            move = KyodaiHandMove.Spring, springFrequency = 2.2f, springDamping = 0.75f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.5f, telegraphWidth = 2.4f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "끌어올림", frames = 10, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 5.6f),
            move = KyodaiHandMove.Spring, springFrequency = 3.5f, springDamping = 0.5f,
            pose = KyodaiHandPose.Fist, scale = 1.1f, floatWeight = 0.1f, telegraphWidth = 2.4f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "찍기", frames = 6, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 1.1f),
            move = KyodaiHandMove.Ease, easeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.6f, 0f)),
            pose = KyodaiHandPose.Fist, floatWeight = 0f, telegraphWidth = 2.4f,
            hitActive = true, hitSize = new Vector2(2f, 1.8f),
            impactOnEnd = true, impactShake = 0.3f, impactFreezeFrames = 3
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "박힘", frames = 14, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 1.1f),
            move = KyodaiHandMove.Spring, springFrequency = 6f, springDamping = 0.9f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.05f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "뽑기", frames = 10, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 2.6f),
            move = KyodaiHandMove.Spring, springFrequency = 2.6f, springDamping = 0.6f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.3f
        });
    }

    // 연타 왼손: 찍기 → (오른손 차례 동안 위에서 따라다님) → 찍기. 오른손: 55f 늦게 찍기 한 번 → 찍는 간격 약 55f
    private static void FillBarrageLeft(KyodaiHandMotion m)
    {
        m.keys.Clear();
        AddQuickSlam(m);
        m.keys.Add(new KyodaiHandKey
        {
            label = "오른손 차례 기다림", frames = 40, anchor = KyodaiHandAnchor.Player, offset = new Vector2(0f, 5f), trackPlayer = true,
            move = KyodaiHandMove.Spring, springFrequency = 1.6f, springDamping = 0.8f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.6f
        });
        AddQuickSlam(m);
    }

    private static void FillBarrageRight(KyodaiHandMotion m)
    {
        m.keys.Clear();
        AddQuickSlam(m);
    }

    // 2페이즈 전환 연출의 양손 내려침 (데미지 없음 — 판정 키가 없다): 들어올리기 → 내려침(충격) → 박힘 → 뽑기
    private static void FillTransitionSlam(KyodaiHandMotion m)
    {
        m.keys.Clear();
        m.keys.Add(new KyodaiHandKey
        {
            label = "들어올리기", frames = 24, anchor = KyodaiHandAnchor.Home, offset = new Vector2(0f, 1.5f),
            move = KyodaiHandMove.Spring, springFrequency = 2f, springDamping = 0.6f,
            pose = KyodaiHandPose.Fist, scale = 1.15f, floatWeight = 0.2f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "내려침", frames = 7, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 1.1f),
            move = KyodaiHandMove.Ease, easeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.6f, 0f)),
            pose = KyodaiHandPose.Fist, floatWeight = 0f,
            impactOnEnd = true, impactShake = 0.5f, impactFreezeFrames = 6
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "박힘", frames = 30, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 1.1f),
            move = KyodaiHandMove.Spring, springFrequency = 6f, springDamping = 0.9f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.05f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "뽑기", frames = 16, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 2.6f),
            move = KyodaiHandMove.Spring, springFrequency = 2.5f, springDamping = 0.6f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.3f
        });
    }

    private static void EnsureTransitionMotion(KyodaiBossController boss, System.Collections.Generic.List<string> added)
    {
        var so = new SerializedObject(boss);
        if (so.FindProperty("phaseTransitionMotion").objectReferenceValue != null) return;
        var motion = LoadOrCreate<KyodaiHandMotion>($"{DataFolder}/Kyodai2D_전환내려침_모션.asset", FillTransitionSlam);
        SetFields(boss, s => s.FindProperty("phaseTransitionMotion").objectReferenceValue = motion);
        added.Add("2페이즈 전환 동작");
    }

    // 내려찍는 기물: 전투 내내(1페이즈부터) 패턴과 따로 주기적으로 (KyodaiCrusher). 데미지는 기물 전용 패턴 데이터 — 패턴 목록·마스터 목록엔 안 넣는다
    private static bool EnsureCrusher(KyodaiBossController boss, System.Collections.Generic.List<string> added)
    {
        if (boss.GetComponentInChildren<KyodaiCrusher>(true) != null) return false;
        PaintCrusherImages(false, out Sprite pressSprite, out Sprite rodSprite);
        var crusherData = LoadOrCreate<KyodaiCrusherData>($"{DataFolder}/Kyodai2D_기물_설정.asset", _ => { });
        var data = LoadOrCreate<BossPatternData>($"{DataFolder}/Kyodai2D_기물.asset", p =>
        {
            p.patternID = "KYODAI2D_CRUSHER";
            p.patternName = "내려찍는 기물";
            p.description = "1페이즈부터: 패턴과 따로 주기적으로 — 화면 위에서 플레이어를 따라 내려와(그림자 예고) 멈추고 떨린 뒤 찍고 올라감. 파괴 불가(미정)";
            p.tellDuration = 0f;
            p.postDelay = 0f;
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.Core;
            p.partDurabilityDamage = 0;
            // baseDamage는 기획 수치 없음 — KKH 기본값 그대로(TODO)
        });
        var telegraph = EnsureTelegraph(boss, CrusherTelegraphName, added);

        var go = new GameObject("내려찍는 기물");
        Undo.RegisterCreatedObjectUndo(go, "2D 쿄다이 기물");
        go.transform.SetParent(boss.transform, false);

        var press = new GameObject("기물 그림", typeof(SpriteRenderer));
        press.transform.SetParent(go.transform, false);
        var pressRenderer = press.GetComponent<SpriteRenderer>();
        pressRenderer.sprite = pressSprite;
        pressRenderer.sortingOrder = 6;
        pressRenderer.enabled = false;

        var rod = new GameObject("막대", typeof(SpriteRenderer));
        rod.transform.SetParent(go.transform, false);
        var rodRenderer = rod.GetComponent<SpriteRenderer>();
        rodRenderer.sprite = rodSprite;
        rodRenderer.sortingOrder = 5;
        rodRenderer.enabled = false;

        var crusher = go.AddComponent<KyodaiCrusher>();
        SetFields(crusher, so =>
        {
            so.FindProperty("patternData").objectReferenceValue = data;
            so.FindProperty("data").objectReferenceValue = crusherData;
            so.FindProperty("pressRenderer").objectReferenceValue = pressRenderer;
            so.FindProperty("rodRenderer").objectReferenceValue = rodRenderer;
            so.FindProperty("telegraph").objectReferenceValue = telegraph;
        });
        return true;
    }

    // ───────────────────────── 원본 기획서 패턴 (2026-11-04) ─────────────────────────

    // 주먹 5연타: 조준 → 끌어올림 → 찍기 → 짧게 박힘을 5번, 갈수록 빨라짐. 마지막은 3초 박힘(원본 기획 수치) → 뽑기
    private static void FillFistCombo(KyodaiHandMotion m)
    {
        int[] aim = { 26, 20, 15, 11, 8 };
        int[] raise = { 8, 6, 5, 4, 4 };
        int[] embed = { 6, 5, 4, 3, 180 };
        m.keys.Clear();
        for (int i = 0; i < 5; i++)
        {
            m.keys.Add(new KyodaiHandKey
            {
                label = $"{i + 1}타 조준", frames = aim[i], anchor = KyodaiHandAnchor.Player, offset = new Vector2(0f, 3.8f), trackPlayer = true,
                move = KyodaiHandMove.Spring, springFrequency = 2.6f, springDamping = 0.75f,
                pose = KyodaiHandPose.Fist, floatWeight = 0.4f, telegraphWidth = 2.4f
            });
            m.keys.Add(new KyodaiHandKey
            {
                label = $"{i + 1}타 끌어올림", frames = raise[i], anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 4.8f),
                move = KyodaiHandMove.Spring, springFrequency = 4f, springDamping = 0.5f,
                pose = KyodaiHandPose.Fist, scale = 1.08f, floatWeight = 0.1f, telegraphWidth = 2.4f
            });
            m.keys.Add(new KyodaiHandKey
            {
                label = $"{i + 1}타 찍기", frames = 5, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 1.1f),
                move = KyodaiHandMove.Ease, easeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.6f, 0f)),
                pose = KyodaiHandPose.Fist, floatWeight = 0f, telegraphWidth = 2.4f,
                hitActive = true, hitSize = new Vector2(2f, 1.8f),
                impactOnEnd = true, impactShake = i == 4 ? 0.4f : 0.22f, impactFreezeFrames = i == 4 ? 5 : 2
            });
            m.keys.Add(new KyodaiHandKey
            {
                label = i == 4 ? "마지막 박힘 (딜 타임)" : $"{i + 1}타 박힘", frames = embed[i], anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 1.1f),
                move = KyodaiHandMove.Spring, springFrequency = 6f, springDamping = 0.9f,
                pose = KyodaiHandPose.Fist, floatWeight = 0.05f
            });
        }
        m.keys.Add(new KyodaiHandKey
        {
            label = "뽑기", frames = 14, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 2.6f),
            move = KyodaiHandMove.Spring, springFrequency = 2.5f, springDamping = 0.6f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.3f
        });
    }

    // 손바닥 압착: 경기장 1/3 너비를 덮게 손바닥을 키우고 가로로 늘린다(widthScale). 그림자 1.2초 → 짓누름 → 3초 잔류 (원본 기획 수치)
    private const float PalmW = 44f / 18f, PalmH = 52f / 18f; // 임시 손바닥 그림 크기(보스 단위)
    private static void FillPalmPress(KyodaiHandMotion m, KyodaiBossController boss)
    {
        const float scale = 2f;
        float third = boss != null ? boss.ArenaWidth / 3f / boss.Size : 8f; // 경기장 1/3(보스 단위)
        float width = Mathf.Max(1f, third / (PalmW * scale));
        float contactY = PalmH * scale * 0.5f;
        m.keys.Clear();
        m.keys.Add(new KyodaiHandKey
        {
            label = "위에서 따라옴", frames = 40, anchor = KyodaiHandAnchor.Player, offset = new Vector2(0f, 9f), trackPlayer = true,
            move = KyodaiHandMove.Spring, springFrequency = 1.4f, springDamping = 0.8f,
            pose = KyodaiHandPose.Palm, scale = scale, widthScale = width, floatWeight = 0.3f, telegraphWidth = third
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "멈춤", frames = 32, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 9.6f),
            move = KyodaiHandMove.Spring, springFrequency = 3f, springDamping = 0.5f,
            pose = KyodaiHandPose.Palm, scale = scale, widthScale = width, floatWeight = 0.05f, telegraphWidth = third
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "짓누름", frames = 8, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, contactY),
            move = KyodaiHandMove.Ease, easeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.6f, 0f)),
            pose = KyodaiHandPose.Palm, scale = scale, widthScale = width, floatWeight = 0f, telegraphWidth = third,
            hitActive = true, hitSize = new Vector2(third, PalmH * scale * 0.9f),
            impactOnEnd = true, impactShake = 0.55f, impactFreezeFrames = 7
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "잔류 (딜 타임)", frames = 180, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, contactY),
            move = KyodaiHandMove.Spring, springFrequency = 6f, springDamping = 0.9f,
            pose = KyodaiHandPose.Palm, scale = scale, widthScale = width, floatWeight = 0.03f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "들기", frames = 22, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 6f),
            move = KyodaiHandMove.Spring, springFrequency = 2f, springDamping = 0.7f,
            pose = KyodaiHandPose.Palm, scale = scale, widthScale = width, floatWeight = 0.3f
        });
    }

    // 충격파 내려치기: 주먹 그림자 0.8초 → 찍기(충격파) → 2초 잔류 (원본 기획 수치)
    private static void FillShockSlam(KyodaiHandMotion m)
    {
        m.keys.Clear();
        m.keys.Add(new KyodaiHandKey
        {
            label = "따라오며 조준", frames = 32, anchor = KyodaiHandAnchor.Player, offset = new Vector2(0f, 5.5f), trackPlayer = true,
            move = KyodaiHandMove.Spring, springFrequency = 1.8f, springDamping = 0.75f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.5f, telegraphWidth = 2.6f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "끌어올리기", frames = 16, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 6.8f),
            move = KyodaiHandMove.Spring, springFrequency = 3f, springDamping = 0.5f,
            pose = KyodaiHandPose.Fist, scale = 1.15f, floatWeight = 0.1f, telegraphWidth = 2.6f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "내려찍기 (충격파)", frames = 7, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 1.1f),
            move = KyodaiHandMove.Ease, easeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.6f, 0f)),
            pose = KyodaiHandPose.Fist, scale = 1.15f, floatWeight = 0f, telegraphWidth = 2.6f,
            hitActive = true, hitSize = new Vector2(2f, 1.8f),
            impactOnEnd = true, impactShake = 0.45f, impactFreezeFrames = 5, spawnShockwave = true
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "잔류 (딜 타임)", frames = 120, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 1.1f),
            move = KyodaiHandMove.Spring, springFrequency = 6f, springDamping = 0.9f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.05f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "뽑기", frames = 14, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 2.6f),
            move = KyodaiHandMove.Spring, springFrequency = 2.5f, springDamping = 0.6f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.3f
        });
    }

    // 양팔 동시 내려찍기 한 손 (side -1 = 왼손, +1 = 오른손): 플레이어 양옆을 따라오다 예고 0.8초 → 동시에 찍기 → 3초 잔류 (원본 기획 수치)
    private static void FillBothSlam(KyodaiHandMotion m, int side)
    {
        m.keys.Clear();
        m.keys.Add(new KyodaiHandKey
        {
            label = "양옆으로 따라옴", frames = 34, anchor = KyodaiHandAnchor.Player, offset = new Vector2(1f * side, 5.5f), trackPlayer = true,
            move = KyodaiHandMove.Spring, springFrequency = 1.6f, springDamping = 0.75f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.5f, telegraphWidth = 2.4f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "끌어올리기", frames = 14, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 6.8f),
            move = KyodaiHandMove.Spring, springFrequency = 3f, springDamping = 0.5f,
            pose = KyodaiHandPose.Fist, scale = 1.12f, floatWeight = 0.1f, telegraphWidth = 2.4f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "동시에 찍기", frames = 7, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 1.1f),
            move = KyodaiHandMove.Ease, easeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.6f, 0f)),
            pose = KyodaiHandPose.Fist, floatWeight = 0f, telegraphWidth = 2.4f,
            hitActive = true, hitSize = new Vector2(2f, 1.8f),
            impactOnEnd = true, impactShake = 0.45f, impactFreezeFrames = 6
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "잔류 (딜 타임)", frames = 180, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 1.1f),
            move = KyodaiHandMove.Spring, springFrequency = 6f, springDamping = 0.9f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.05f
        });
        m.keys.Add(new KyodaiHandKey
        {
            label = "뽑기", frames = 16, anchor = KyodaiHandAnchor.Keep, offset = new Vector2(0f, 2.6f),
            move = KyodaiHandMove.Spring, springFrequency = 2.5f, springDamping = 0.6f,
            pose = KyodaiHandPose.Fist, floatWeight = 0.3f
        });
    }

    // 충격파 오브젝트 (KyodaiShockwave) — 충격파 키가 있는 동작이 찍을 때 쓴다
    private static bool EnsureShockwave(KyodaiBossController boss)
    {
        if (boss.GetComponentInChildren<KyodaiShockwave>(true) != null) return false;
        var sprite = PaintShockwaveImage(false);
        var go = new GameObject("충격파");
        Undo.RegisterCreatedObjectUndo(go, "2D 쿄다이 충격파");
        go.transform.SetParent(boss.transform, false);
        var wave = go.AddComponent<KyodaiShockwave>();
        SetFields(wave, so => so.FindProperty("waveSprite").objectReferenceValue = sprite);
        return true;
    }

    private static Sprite PaintShockwaveImage(bool force) =>
        LoadOrPaint($"{ImageFolder}/임시_충격파.png", 16, 12, 18f, PaintShockwave, force, SpriteAlignment.BottomCenter);

    // 충격파: 앞(오른쪽)으로 갈수록 솟았다가 뚝 떨어지는 물결 (흰색 — 색은 코드가 입힌다)
    private static Func<int, int, Color32> PaintShockwave => (x, y) =>
    {
        float px = x + 0.5f, py = y + 0.5f;
        float crest = px < 12.5f ? 10f * Mathf.Pow(px / 12.5f, 1.6f) : 10f * (1f - (px - 12.5f) / 3.5f);
        if (py > crest + 0.5f) return Clear;
        bool edge = py > crest - 1f || px < 1f || px > 15f;
        if (edge) return new Color32(255, 255, 255, 255);
        return py > crest - 2.5f ? new Color32(255, 240, 210, 230) : new Color32(255, 200, 150, 150);
    };

    // 레이저 쓸기 (새 패턴): 쓸기 모드 레이저 + 쓸고 난 뒤 바닥 폭발 (KyodaiLaserData.sweepLength > 0)
    private static bool EnsureLaserSweep(KyodaiBossController boss, BossMasterData master)
    {
        var laser = boss.GetComponentInChildren<KyodaiLaser>(true);
        if (laser == null) return false; // 눈은 EnsureLaser가 먼저 만든다

        var sweepData = LoadOrCreate<KyodaiLaserData>($"{DataFolder}/Kyodai2D_레이저쓸기_설정.asset", d =>
        {
            d.sweepLength = boss.ArenaWidth * 0.6f; // 경기장 너비의 60% (임시)
            d.aimFrames = 70;                      // 쓸기는 조준이 짧다 (임시)
            d.lockFrames = 15;
            d.beamWidth = 0.8f;
            d.explosionSpacing = 0.8f;
        });
        var data = LoadOrCreate<BossPatternData>($"{DataFolder}/Kyodai2D_레이저쓸기.asset", p =>
        {
            p.patternID = "KYODAI2D_LASER_SWEEP";
            p.patternName = "레이저 쓸기";
            p.description = "새 패턴: 조준선이 플레이어 뒤쪽 바닥을 따라오다 고정 → 레이저가 바닥을 가로로 쓸고 지나감(그을음) → " +
                            "약 1.2초 뒤 쓸고 간 방향으로 바닥이 펑 펑 펑 터짐. 쓸고 가는 길에 손이 있으면 그 손이 떨어진다";
            p.tellDuration = (sweepData.aimFrames + sweepData.lockFrames) / 60f;
            p.postDelay = 1f;
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.Core;
            p.partDurabilityDamage = 0;
            // baseDamage는 기획 수치 없음 — KKH 기본값 그대로(TODO)
        });
        if (master != null && !master.patterns.Contains(data))
        {
            master.patterns.Add(data);
            EditorUtility.SetDirty(master);
        }
        foreach (var existing in boss.GetComponentsInChildren<KyodaiLaserPattern>(true))
            if (existing.PatternData == data) return false;

        var go = new GameObject("레이저 쓸기");
        Undo.RegisterCreatedObjectUndo(go, "2D 쿄다이 패턴 추가");
        go.transform.SetParent(boss.transform, false);
        var pattern = go.AddComponent<KyodaiLaserPattern>();
        SetFields(pattern, so =>
        {
            so.FindProperty("patternData").objectReferenceValue = data;
            so.FindProperty("laser").objectReferenceValue = laser;
            so.FindProperty("laserData").objectReferenceValue = sweepData;
        });
        return true;
    }

    // ───────────────────────── 촉수 ─────────────────────────

    private const int TentacleBones = 7;
    private const float TentacleSegment = 0.55f; // 뼈 하나 길이(보스 단위) — 마디 그림(12px / 18ppu ≈ 0.67)과 살짝 겹치게

    // 촉수 생성기 + 꺼둔 촉수 원본(뼈 사슬). 데미지는 생성기 전용 패턴 데이터 — 패턴 목록·마스터 목록엔 안 넣는다
    private static bool EnsureTentacles(KyodaiBossController boss)
    {
        if (boss.GetComponentInChildren<KyodaiTentacleSpawner>(true) != null) return false;
        PaintTentacleImages(false, out Sprite segmentSprite, out Sprite tipSprite);
        var tentacleData = LoadOrCreate<KyodaiTentacleData>($"{DataFolder}/Kyodai2D_촉수_설정.asset", _ => { });
        var data = LoadOrCreate<BossPatternData>($"{DataFolder}/Kyodai2D_촉수.asset", p =>
        {
            p.patternID = "KYODAI2D_TENTACLE";
            p.patternName = "촉수";
            p.description = "해킹 대체: 촉수가 점점 늘어남(최대 10). 3개부터 플레이어 이동속도 감소(방어력 감소는 KKH 협의 전). " +
                            "가까이 오면 젖혔다가 휘두름. 부수면 줄어듦";
            p.tellDuration = 0f;
            p.postDelay = 0f;
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.Core;
            p.partDurabilityDamage = 0;
            // baseDamage는 기획 수치 없음 — KKH 기본값 그대로(TODO)
        });

        var spawnerGo = new GameObject("촉수 생성기");
        Undo.RegisterCreatedObjectUndo(spawnerGo, "2D 쿄다이 촉수");
        spawnerGo.transform.SetParent(boss.transform, false);

        // 원본: 뿌리 오브젝트(맞는 자리) → 뼈0 → 뼈1 → … (각 뼈가 앞 뼈의 자식, 마디 그림은 뼈의 자식)
        var templateGo = new GameObject("촉수 원본 (복제용, 꺼둠)", typeof(BoxCollider2D));
        templateGo.transform.SetParent(spawnerGo.transform, false);
        var bones = new Transform[TentacleBones];
        Transform parent = templateGo.transform;
        for (int i = 0; i < TentacleBones; i++)
        {
            var bone = new GameObject($"뼈{i}").transform;
            bone.SetParent(parent, false);
            bone.localPosition = i == 0 ? Vector3.zero : new Vector3(0f, TentacleSegment, 0f);
            bones[i] = bone;

            bool tip = i == TentacleBones - 1;
            var seg = new GameObject(tip ? "끝" : "마디", typeof(SpriteRenderer));
            seg.transform.SetParent(bone, false);
            float taper = Mathf.Lerp(1f, 0.55f, i / (TentacleBones - 1f)); // 끝으로 갈수록 가늘게 (가로만 — 세로를 줄이면 마디 사이가 벌어진다)
            seg.transform.localScale = new Vector3(taper, 1f, 1f);
            var r = seg.GetComponent<SpriteRenderer>();
            r.sprite = tip ? tipSprite : segmentSprite;
            r.sortingOrder = 3; // 본체(-20) 앞, 손(5) 뒤
            parent = bone;
        }

        var tentacle = templateGo.AddComponent<KyodaiTentacle>();
        SetFields(tentacle, so =>
        {
            var arr = so.FindProperty("bones");
            arr.arraySize = bones.Length;
            for (int i = 0; i < bones.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = bones[i];
            so.FindProperty("hurtBox").objectReferenceValue = templateGo.GetComponent<BoxCollider2D>();
        });
        templateGo.SetActive(false);

        var spawner = spawnerGo.AddComponent<KyodaiTentacleSpawner>();
        SetFields(spawner, so =>
        {
            so.FindProperty("patternData").objectReferenceValue = data;
            so.FindProperty("data").objectReferenceValue = tentacleData;
            so.FindProperty("template").objectReferenceValue = tentacle;
        });
        return true;
    }

    private static void PaintTentacleImages(bool force, out Sprite segment, out Sprite tip)
    {
        segment = LoadOrPaint($"{ImageFolder}/임시_촉수마디.png", 12, 12, 18f, PaintTentacleSegment, force, SpriteAlignment.BottomCenter);
        tip = LoadOrPaint($"{ImageFolder}/임시_촉수끝.png", 10, 12, 18f, PaintTentacleTip, force, SpriteAlignment.BottomCenter);
    }

    // 촉수 마디: 둥근 쇠 마디 + 아래 이음매 + 가운데 붉은 줄
    private static Func<int, int, Color32> PaintTentacleSegment => Shade((x, y) => RoundRect(x, y, 6f, 6f, 5f, 6f, 4f), 12,
        (x, y) =>
        {
            if (y == 2 && x >= 2 && x <= 9) return Dark;   // 이음매
            if (y == 7 && x >= 3 && x <= 8) return Red;    // 붉은 줄 (쿄다이 색)
            return null;
        });

    // 촉수 끝: 뾰족한 끝 + 붉은 렌즈
    private static Func<int, int, Color32> PaintTentacleTip => Shade((x, y) =>
            RoundRect(x, y, 5f, 4f, 4.5f, 4f, 3f) || (y >= 4f && y <= 11.5f && Mathf.Abs(x - 5f) <= (11.5f - y) * 0.55f),
        12,
        (x, y) => Circle(x + 0.5f, y + 0.5f, 5f, 5f, 1.6f) ? Red : (Color32?)null);

    // 페이즈별 패턴 목록: 1페이즈 = minPhase 1 패턴 + 레이저, 2페이즈 = 전부. 빠진 ID만 더한다(손으로 고친 목록은 그대로).
    // 1페이즈 목록이 비어 있으면(= 전부) 2페이즈 패턴이 1페이즈에 섞이니 반드시 채운다
    private static void SyncPhases(BossMasterData master, System.Collections.Generic.List<string> added)
    {
        if (master == null) return;
        var p1 = master.phases.Find(p => p.phaseIndex == 1);
        if (p1 == null) master.phases.Add(p1 = new BossPhaseData { phaseIndex = 1, hpThresholdRatio = 1f });
        var p2 = master.phases.Find(p => p.phaseIndex == 2);
        if (p2 == null) master.phases.Add(p2 = new BossPhaseData { phaseIndex = 2, hpThresholdRatio = 0.5f }); // 50% 임시값
        if (p1.availableSkillIDs == null) p1.availableSkillIDs = new System.Collections.Generic.List<string>();
        if (p2.availableSkillIDs == null) p2.availableSkillIDs = new System.Collections.Generic.List<string>();

        var phase1 = new System.Collections.Generic.List<string>();
        foreach (var spec in Patterns) if (spec.minPhase <= 1) phase1.Add(spec.id);
        phase1.Add("KYODAI2D_LASER");
        phase1.Add("KYODAI2D_LASER_SWEEP");
        var phase2 = new System.Collections.Generic.List<string>(phase1);
        foreach (var spec in Patterns) if (spec.minPhase >= 2) phase2.Add(spec.id);
        foreach (var spec in DualPatterns) phase2.Add(spec.id);

        bool changed = false;
        foreach (string id in phase1) if (!p1.availableSkillIDs.Contains(id)) { p1.availableSkillIDs.Add(id); changed = true; }
        foreach (string id in phase2) if (!p2.availableSkillIDs.Contains(id)) { p2.availableSkillIDs.Add(id); changed = true; }
        if (!changed) return;
        EditorUtility.SetDirty(master);
        AssetDatabase.SaveAssets();
        added.Add("페이즈별 패턴 목록");
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
    // 부모(보스 루트)가 줄어 있어도 된다: 위치는 월드로, 크기는 로컬 k(= 월드 k × 보스 크기)로 넣는다
    private static void PlaceBehind(Transform t, Vector2 planePoint, float back)
    {
        var main = Camera.main;
        if (main == null || main.orthographic)
        {
            t.position = new Vector3(planePoint.x, planePoint.y, back);
            t.localScale = Vector3.one;
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
    [MenuItem("NYH/쿄다이/2D 임시 그림 다시 그리기", false, MenuPriority + 20)]
    private static void RepaintTempImages()
    {
        if (!EditorUtility.DisplayDialog("쿄다이", $"{ImageFolder}의 임시_ 그림 4장을 코드 기준으로 덮어씁니다. 진짜 그림을 같은 이름으로 넣어뒀다면 사라집니다.", "다시 그리기", "취소"))
            return;
        PaintTempImages(true, out _, out _, out _, out _);
        PaintCrusherImages(true, out _, out _);
        PaintTentacleImages(true, out _, out _);
        PaintShockwaveImage(true);
        Debug.Log("[Kyodai] 2D 임시 그림 다시 그림");
    }

    private static void PaintTempImages(bool force, out Sprite fist, out Sprite palm, out Sprite body, out Sprite shadow)
    {
        fist = LoadOrPaint($"{ImageFolder}/임시_주먹.png", FistW, FistH, 18f, PaintFist, force);
        palm = LoadOrPaint($"{ImageFolder}/임시_손바닥.png", 44, 52, 18f, PaintPalm, force);
        body = LoadOrPaint($"{ImageFolder}/임시_본체.png", 120, 110, 10f, PaintBody, force);
        shadow = LoadOrPaint($"{ImageFolder}/임시_그림자.png", 32, 10, 32f, PaintShadow, force);
    }

    private static void PaintCrusherImages(bool force, out Sprite press, out Sprite rod)
    {
        press = LoadOrPaint($"{ImageFolder}/임시_기물.png", 40, 36, 18f, PaintPress, force);
        rod = LoadOrPaint($"{ImageFolder}/임시_막대.png", 6, 4, 18f, PaintRod, force, SpriteAlignment.BottomCenter); // 아래 끝 기준 — 위로 늘린다
    }

    private static readonly Color32 Hazard = new Color32(232, 184, 48, 255);

    // 내려찍는 기물: 두꺼운 쇳덩이 + 아래 노랑·검정 경고 줄무늬 + 위 연결 고리
    private static Func<int, int, Color32> PaintPress => Shade((x, y) =>
            RoundRect(x, y, 20f, 15f, 19f, 14f, 3f)          // 쇳덩이
            || RoundRect(x, y, 20f, 32f, 6f, 4f, 2f),         // 연결 고리
        36,
        (x, y) =>
        {
            if (y >= 3 && y <= 7 && x >= 2 && x <= 37) return ((x + y) / 3) % 2 == 0 ? Hazard : Outline; // 경고 줄무늬
            if (y == 9 && x >= 2 && x <= 37) return Dark;
            if ((x == 8 || x == 31) && (y == 22 || y == 23)) return Bright;                              // 볼트
            return null;
        });

    // 막대: 가로 6픽셀 무늬를 세로로 늘려 쓴다
    private static Func<int, int, Color32> PaintRod => (x, y) => x == 0 || x == 5 ? Outline : x == 1 ? Bright : x == 4 ? Dark : Mid;

    // 이미 있으면 그대로 쓴다 — 진짜 그림으로 덮어쓴 파일을 지우지 않게 (force = 다시 그리기 메뉴)
    private static Sprite LoadOrPaint(string path, int w, int h, float ppu, Func<int, int, Color32> paint, bool force = false,
                                      SpriteAlignment pivot = SpriteAlignment.Center)
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
        if (pivot != SpriteAlignment.Center)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)pivot;
            importer.SetTextureSettings(settings);
        }
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
