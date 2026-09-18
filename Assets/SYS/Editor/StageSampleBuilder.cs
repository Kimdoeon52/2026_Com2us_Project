// RE:AL STEEL - 2.5D 디오라마 스테이지 샘플 생성기 (ProBuilder 6.x)
//
// 옥토패스 트래블러식 HD-2D 환경의 "덩어리"를 ProBuilder 지오메트리로 짜준다.
// 텍스처는 직접 입히는 전제이므로 여기서는 다음 셋만 책임진다.
//   1) 형태(그레이박스)
//   2) 파츠 분리 (오브젝트마다 머티리얼 슬롯이 따로)
//   3) 월드 스페이스 UV (모든 면의 텍셀 밀도 통일)
//
// 메뉴: Tools > RE_AL STEEL > Stage
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace RealSteel.EditorTools
{
    public static class StageSampleBuilder
    {
        // ─────────────────────────────────────────────────────────────
        // 규격 - 프로젝트 전체에서 이 값을 지킨다
        // ─────────────────────────────────────────────────────────────

        /// <summary>픽셀 / 유닛. 로봇 높이 2.4유닛 = 76.8px</summary>
        public const float Ppu = 32f;

        /// <summary>타일 텍스처 한 변의 픽셀 수. 64px 타일 = 2유닛을 덮는다.</summary>
        public const float TilePixels = 64f;

        /// <summary>씬 편집 스냅 그리드</summary>
        public const float Grid = 0.25f;

        /// <summary>로봇 기준 높이 (스케일 확인용 더미)</summary>
        public const float RobotHeight = 2.4f;

        /// <summary>
        /// Z-fighting 방지용 "파묻기" 깊이.
        /// 두 면을 같은 높이에 딱 맞추면 깊이 버퍼가 어느 쪽이 앞인지 판정을 못 해서 깜빡인다.
        /// 맞붙는 면은 항상 이 값만큼 상대 안쪽으로 밀어 넣어, 겹치는 면이 아예 안 보이게 한다.
        /// </summary>
        public const float Bite = 0.02f;

        /// <summary>월드 UV 스케일. 64px 타일 + 32PPU → 0.5</summary>
        static float UvScale { get { return Ppu / TilePixels; } }

        // ─────────────────────────────────────────────────────────────
        // 출력 폴더 - 프로젝트마다 다르므로 EditorPrefs 로 들고 있는다
        // ─────────────────────────────────────────────────────────────

        const string ArtDirKey = "RSPixelStageTools.StageSampleBuilder.ArtDir";
        const string DefaultArtDir = "Assets/StageSamples";

        /// <summary>
        /// 생성한 머티리얼·텍스처가 저장될 프로젝트 내 폴더.
        /// 메뉴 `0. 출력 폴더 지정` 으로 바꾸거나 코드에서 직접 대입해도 된다.
        /// </summary>
        public static string ArtDir
        {
            get
            {
                string v = EditorPrefs.GetString(ArtDirKey, DefaultArtDir);
                if (string.IsNullOrEmpty(v) || !v.StartsWith("Assets")) v = DefaultArtDir;
                return v.TrimEnd('/');
            }
            set
            {
                string v = string.IsNullOrEmpty(value) ? DefaultArtDir : value.Replace('\\', '/').TrimEnd('/');
                if (!v.StartsWith("Assets")) v = DefaultArtDir;
                EditorPrefs.SetString(ArtDirKey, v);
            }
        }

        static string MatDir { get { return ArtDir + "/Materials"; } }
        static string TexDir { get { return ArtDir + "/Textures"; } }
        static string CheckerTexPath { get { return TexDir + "/TEX_Checker_8px.png"; } }

        const string RootName = "STAGE_Sample";

        // ─────────────────────────────────────────────────────────────
        // 머티리얼 정의
        // ─────────────────────────────────────────────────────────────

        struct MatDef
        {
            public string key;
            public Color color;
            public Color emission;

            public MatDef(string k, float r, float g, float b)
            {
                key = k; color = new Color(r, g, b, 1f); emission = Color.black;
            }

            public MatDef(string k, float r, float g, float b, Color emis)
            {
                key = k; color = new Color(r, g, b, 1f); emission = emis;
            }
        }

        static readonly MatDef[] Mats =
        {
            new MatDef("Ground",   0.30f, 0.29f, 0.28f),   // 아스팔트 바닥
            new MatDef("Concrete", 0.46f, 0.45f, 0.43f),   // 콘크리트 옹벽/계단
            new MatDef("Stone",    0.38f, 0.37f, 0.35f),   // 돌 - 다리/기단
            new MatDef("Metal",    0.34f, 0.36f, 0.39f),   // 철판 - 배관/난간
            new MatDef("Rust",     0.42f, 0.30f, 0.22f),   // 녹슨 금속
            new MatDef("Wall",     0.55f, 0.50f, 0.44f),   // 1층 벽
            new MatDef("Wall2",    0.48f, 0.45f, 0.42f),   // 2층 벽
            new MatDef("Roof",     0.33f, 0.30f, 0.31f),   // 지붕
            new MatDef("Trim",     0.25f, 0.24f, 0.24f),   // 몰딩/창틀/문틀
            new MatDef("Wood",     0.44f, 0.33f, 0.23f),   // 목재
            new MatDef("Water",    0.18f, 0.26f, 0.30f),   // 수로 바닥
            new MatDef("Accent",   0.75f, 0.28f, 0.22f, new Color(1.4f, 0.45f, 0.25f)), // 간판 - 발광
        };

        // ─────────────────────────────────────────────────────────────
        // 메뉴
        // ─────────────────────────────────────────────────────────────

        [MenuItem("Tools/RE_AL STEEL/Stage/0. 출력 폴더 지정 (머티리얼 · 텍스처)", false, 9)]
        public static void MenuPickArtDir()
        {
            string start = AssetDatabase.IsValidFolder(ArtDir) ? ArtDir : "Assets";
            string abs = EditorUtility.OpenFolderPanel("생성 에셋을 저장할 폴더 (프로젝트 안)", start, "");
            if (string.IsNullOrEmpty(abs)) return;

            string proj = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/');
            abs = abs.Replace('\\', '/').TrimEnd('/');

            if (abs.Length <= proj.Length || !abs.StartsWith(proj))
            {
                Debug.LogWarning("[Stage] 프로젝트 폴더 안(Assets 아래)이어야 합니다: " + abs);
                return;
            }

            string rel = abs.Substring(proj.Length + 1);
            if (!rel.StartsWith("Assets"))
            {
                Debug.LogWarning("[Stage] Assets 아래 폴더를 골라 주세요: " + rel);
                return;
            }

            ArtDir = rel;
            Debug.Log("[Stage] 출력 폴더 → " + ArtDir + "  (Materials / Textures 하위 폴더는 자동 생성)");
        }

        [MenuItem("Tools/RE_AL STEEL/Stage/1. 머티리얼 + 체커 텍스처 생성", false, 10)]
        public static void MenuCreateAssets()
        {
            EnsureFolders();
            CreateCheckerTexture();
            foreach (var d in Mats) EnsureMaterial(d);
            EnsureCheckerMaterial();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Stage] 머티리얼 " + (Mats.Length + 1) + "종 + 체커 텍스처 생성 완료 → " + ArtDir);
        }

        [MenuItem("Tools/RE_AL STEEL/Stage/2. 지형 샘플 생성", false, 11)]
        public static void MenuBuildTerrain()
        {
            MenuCreateAssets();
            var root = GetOrCreateRoot();
            var t = NewGroup("TERRAIN_Sample", root);
            BuildTerrain(t);
            FinalizeGroup(t);
            Selection.activeTransform = t;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        [MenuItem("Tools/RE_AL STEEL/Stage/3. 건물 샘플 생성 (파츠 분리)", false, 12)]
        public static void MenuBuildBuilding()
        {
            MenuCreateAssets();
            var root = GetOrCreateRoot();
            var b = NewGroup("BLD_Workshop_01", root);
            b.localPosition = new Vector3(6f, 0f, -1.5f);
            BuildWorkshop(b);
            FinalizeGroup(b);
            Selection.activeTransform = b;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        [MenuItem("Tools/RE_AL STEEL/Stage/4. 전체 생성 (지형 + 건물 + 스케일 더미)", false, 13)]
        public static void MenuBuildAll()
        {
            MenuCreateAssets();
            var root = GetOrCreateRoot();

            var t = NewGroup("TERRAIN_Sample", root);
            BuildTerrain(t);

            var b = NewGroup("BLD_Workshop_01", root);
            b.localPosition = new Vector3(6f, 0f, -1.5f);
            BuildWorkshop(b);

            var r = NewGroup("REF_Scale", root);
            Box(r, "ROBOT_ScaleRef_2.4m", new Vector3(0.9f, RobotHeight, 0.7f),
                new Vector3(-1.5f, 0f, -2.5f), "Rust");
            Box(r, "ROBOT_ScaleRef_Head", new Vector3(0.55f, 0.45f, 0.5f),
                new Vector3(-1.5f, RobotHeight, -2.5f), "Metal");

            FinalizeGroup(root);
            Selection.activeTransform = root;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log("[Stage] 전체 생성 완료. 다음 메뉴 '5. 카메라 + 조명 + 포스트'를 실행하면 HD-2D 룩 세팅이 붙습니다.");
        }

        [MenuItem("Tools/RE_AL STEEL/Stage/UV 다시 계산 (월드 스페이스)", false, 40)]
        public static void MenuReapplyUv()
        {
            var root = GameObject.Find(RootName);
            if (root == null) { Debug.LogWarning("[Stage] " + RootName + " 이 씬에 없습니다."); return; }
            FinalizeGroup(root.transform);
            Debug.Log("[Stage] 월드 UV 재적용 완료 (scale " + UvScale + ")");
        }

        [MenuItem("Tools/RE_AL STEEL/Stage/텍셀 검사 - 체커로 전환 (Ctrl+Z 로 복원)", false, 41)]
        public static void MenuSwapToChecker()
        {
            var root = GameObject.Find(RootName);
            if (root == null) { Debug.LogWarning("[Stage] " + RootName + " 이 씬에 없습니다."); return; }

            var checker = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/MAT_Stage_Checker.mat");
            if (checker == null) { Debug.LogWarning("[Stage] 체커 머티리얼이 없습니다. 메뉴 1번을 먼저 실행하세요."); return; }

            var rends = root.GetComponentsInChildren<MeshRenderer>(true);
            Undo.RecordObjects(rends, "Stage Checker Swap");
            foreach (var r in rends) r.sharedMaterial = checker;
            Debug.Log("[Stage] " + rends.Length + "개 렌더러를 체커로 전환. 모든 면에서 체커 칸 크기가 같은지 확인하세요. 복원은 Ctrl+Z.");
        }

        // ─────────────────────────────────────────────────────────────
        // 지형
        // ─────────────────────────────────────────────────────────────
        //
        //  Z-  (카메라 쪽)                                        Z+
        //  ├── 광장 y=0 ──┤ 수로 y=-2 ├─ 중단 y=0 ─┤ 계단 ├─ 상단 y=2 ─┤
        //     -7 ······ 2      2 ··· 5     5 ··· 7    7··10    10 ··· 14
        //
        static void BuildTerrain(Transform g)
        {
            // ── 바닥 / 단차 ──────────────────────────────────────────
            // 지면 슬래브들은 서로 "딱 맞춤"이 아니라, 옆에 오는 석벽이 슬래브의
            // 끝면을 덮도록 살짝 물러나 있다. 그래야 같은 평면에 두 면이 안 생긴다.
            Box(g, "TER_Ground_Plaza",  new Vector3(22f, 1.0f, 8.8f), new Vector3(0f, -1.0f, -2.6f), "Ground"); // Z -7 ~ 1.8
            Box(g, "TER_Ground_Mid",    new Vector3(22f, 1.0f, 4.8f), new Vector3(0f, -1.0f,  7.6f), "Ground"); // Z 5.2 ~ 10
            Box(g, "TER_Ground_Upper",  new Vector3(22f, 3.0f, 4.2f), new Vector3(0f, -1.0f, 11.9f), "Ground"); // Z 9.8 ~ 14, 윗면 y=2

            // 상단 단차를 받치는 옹벽. 윗면을 Bite 만큼 낮춰서 상단 지면이 덮게 한다.
            Box(g, "TER_Retain_Wall_L", new Vector3(9f, 2.0f, 0.6f), new Vector3(-6.5f, -Bite, 10f), "Concrete");
            Box(g, "TER_Retain_Wall_R", new Vector3(9f, 2.0f, 0.6f), new Vector3( 6.5f, -Bite, 10f), "Concrete");

            // ── 수로 ────────────────────────────────────────────────
            Box(g, "TER_Canal_Floor",   new Vector3(22f, 0.5f, 3f), new Vector3(0f, -2.5f, 3.5f), "Water"); // 윗면 y=-2
            // 석벽: 아래는 수로 바닥에 파묻고, 위는 지면보다 Bite 만큼 낮춰 지면이 덮게 한다.
            Box(g, "TER_Canal_Wall_S",  new Vector3(22f, 2.0f, 0.6f), new Vector3(0f, -2f - Bite, 2.0f), "Stone");
            Box(g, "TER_Canal_Wall_N",  new Vector3(22f, 2.0f, 0.6f), new Vector3(0f, -2f - Bite, 5.0f), "Stone");

            // ── 계단 ────────────────────────────────────────────────
            // 맨 윗단을 상단 지면보다 Bite 만큼 낮춰서, 겹치는 구간이 지면 아래로 숨는다.
            Stairs(g, "TER_Stairs_Main", new Vector3(4f, 2f, 3f), 6, new Vector3(0f, -Bite, 8.5f), "Concrete");

            // ── 경사로 (큐브 정점 변형으로 만든 쐐기) ────────────────
            var ramp = Box(g, "TER_Ramp_East", new Vector3(3f, 2f, 3f), new Vector3(6.5f, -Bite, 8.5f), "Concrete");
            Deform(ramp, delegate (Vector3 v)
            {
                if (v.y <= 0f) return v;                       // 아랫면은 그대로
                float t = Mathf.InverseLerp(-1.5f, 1.5f, v.z); // 안쪽(Z-)에서 바깥(Z+)으로 상승
                return new Vector3(v.x, Mathf.Lerp(-0.9f, 1.0f, t), v.z);
            });

            // ── 아치 다리 ───────────────────────────────────────────
            var br = NewGroup("TER_Bridge", g);

            // GenerateArch: XY 평면에 아치, depth 가 Z. Y축 90도 회전시켜 수로를 가로지르게 놓는다.
            // 스프링잉을 수로 바닥에 파묻어 밑면이 바닥 윗면과 같은 높이가 되지 않게 한다.
            Arch(br, "BR_Arch", 180f, 1.5f, 0.45f, 5.0f, 9,
                 new Vector3(0f, -2.05f, 3.5f), new Vector3(0f, 90f, 0f), "Stone");   // 아치 마루 y=-0.55

            // 상판: 아치 마루가 0.05 파고들게 바닥을 -0.60 에 두고, 윗면은 지면보다 Bite 낮춘다.
            Box(br, "BR_Deck",      new Vector3(5.0f,  0.58f, 4.4f), new Vector3( 0f,   -0.60f, 3.5f), "Stone");
            Box(br, "BR_Parapet_W", new Vector3(0.4f,  0.90f, 4.4f), new Vector3(-2.3f, -0.10f, 3.5f), "Stone");
            Box(br, "BR_Parapet_E", new Vector3(0.4f,  0.90f, 4.4f), new Vector3( 2.3f, -0.10f, 3.5f), "Stone");
            Stack(br, "BR_Cap_W",   new Vector3(0.55f, 0.18f, 4.6f), new Vector3(-2.3f,  0.80f, 3.5f), "Concrete");
            Stack(br, "BR_Cap_E",   new Vector3(0.55f, 0.18f, 4.6f), new Vector3( 2.3f,  0.80f, 3.5f), "Concrete");

            // ── 전경 기둥 (DOF 로 흐려질 근경 - 옥토패스가 즐겨 쓰는 깊이 장치) ──
            Cylinder(g, "TER_Pillar_Fore_A", 0.45f, 3.5f, 8, new Vector3(-8.5f, -Bite, -5.5f), "Stone");
            Cylinder(g, "TER_Pillar_Fore_B", 0.45f, 3.0f, 8, new Vector3( 9.0f, -Bite, -5.0f), "Stone");

            // ── 잡동사니 ────────────────────────────────────────────
            Stack(g, "PROP_Crate_A", new Vector3(0.9f, 0.9f, 0.9f), new Vector3(-4.5f, 0f, 0.5f), "Wood");
            Stack(g, "PROP_Crate_B", new Vector3(0.8f, 1.4f, 0.8f), new Vector3(-5.3f, 0f, 1.2f), "Wood");
            Cylinder(g, "PROP_Drum", 0.38f, 1.1f, 10, new Vector3(-3.4f, -Bite, 1.1f), "Rust");
        }

        // ─────────────────────────────────────────────────────────────
        // 건물 - 파츠 분리
        // ─────────────────────────────────────────────────────────────
        //
        //  파츠를 나누는 기준은 "텍스처가 다른가"이다.
        //  같은 텍스처를 쓸 면은 한 오브젝트로 묶고, 다른 텍스처를 쓸 면은 쪼갠다.
        //
        static void BuildWorkshop(Transform g)
        {
            const float wallT  = 0.25f;  // 벽 두께
            const float foundH = 0.40f;  // 기단 높이
            const float trimH  = 0.15f;  // 기단 몰딩
            const float f1H    = 3.00f;  // 1층 높이
            const float bandH  = 0.20f;  // 층 구분 띠
            const float f2H    = 2.20f;  // 2층 높이
            const float roofRise = 1.50f;
            const float eave     = 0.50f; // 처마 내밀기

            const float hw1 = 2.5f, hd1 = 2.0f;   // 1층 반폭 / 반깊이
            const float hw2 = 2.3f, hd2 = 1.8f;   // 2층 (0.2 들여쌓기)

            float yFound = 0f;
            float yTrim  = yFound + foundH;              // 0.40
            float yF1    = yTrim + trimH;                // 0.55
            float yBand  = yF1 + f1H;                    // 3.55
            float yF2    = yBand + bandH;                // 3.75
            float yGable = yF2 + f2H;                    // 5.95
            float yRidge = yGable + roofRise;            // 7.45

            // ── 00 기단 ────────────────────────────────────────────
            // Stack() = 윗면은 그대로 두고 아랫면만 아래 솔리드에 파묻는다 (Z-fighting 방지).
            Stack(g, "00_Foundation",  new Vector3(hw1 * 2 + 0.4f, foundH, hd1 * 2 + 0.4f), new Vector3(0, yFound, 0), "Stone");
            Stack(g, "01_Plinth_Trim", new Vector3(hw1 * 2 + 0.6f, trimH,  hd1 * 2 + 0.6f), new Vector3(0, yTrim,  0), "Trim");

            // ── 10 1층 벽 (면마다 따로) ─────────────────────────────
            Stack(g, "10_Wall_Front", new Vector3(hw1 * 2, f1H, wallT), new Vector3(0, yF1, -hd1 + wallT * 0.5f), "Wall");
            Stack(g, "11_Wall_Back",  new Vector3(hw1 * 2, f1H, wallT), new Vector3(0, yF1,  hd1 - wallT * 0.5f), "Wall");
            Stack(g, "12_Wall_Left",  new Vector3(wallT, f1H, hd1 * 2 - wallT * 2), new Vector3(-hw1 + wallT * 0.5f, yF1, 0), "Wall");
            Stack(g, "13_Wall_Right", new Vector3(wallT, f1H, hd1 * 2 - wallT * 2), new Vector3( hw1 - wallT * 0.5f, yF1, 0), "Wall");

            // ── 20 층 구분 띠 + 2층 벽 ──────────────────────────────
            Stack(g, "20_Band_Floor2", new Vector3(hw1 * 2 + 0.3f, bandH, hd1 * 2 + 0.3f), new Vector3(0, yBand, 0), "Trim");

            Stack(g, "21_Wall2_Front", new Vector3(hw2 * 2, f2H, wallT), new Vector3(0, yF2, -hd2 + wallT * 0.5f), "Wall2");
            Stack(g, "22_Wall2_Back",  new Vector3(hw2 * 2, f2H, wallT), new Vector3(0, yF2,  hd2 - wallT * 0.5f), "Wall2");
            Stack(g, "23_Wall2_Left",  new Vector3(wallT, f2H, hd2 * 2 - wallT * 2), new Vector3(-hw2 + wallT * 0.5f, yF2, 0), "Wall2");
            Stack(g, "24_Wall2_Right", new Vector3(wallT, f2H, hd2 * 2 - wallT * 2), new Vector3( hw2 - wallT * 0.5f, yF2, 0), "Wall2");

            // ── 30 박공 + 지붕 ──────────────────────────────────────
            // 박공(삼각 벽)은 ProBuilder Prism. 삼각 단면이 XY, 두께가 Z 이므로
            // 용마루는 Z 축을 따라 달린다 → 앞/뒤가 박공, 좌/우가 경사면.
            Prism(g, "30_Gable_Front", new Vector3(hw2 * 2, roofRise + Bite, wallT), new Vector3(0, yGable - Bite, -hd2 + wallT * 0.5f), "Wall2");
            Prism(g, "31_Gable_Back",  new Vector3(hw2 * 2, roofRise + Bite, wallT), new Vector3(0, yGable - Bite,  hd2 - wallT * 0.5f), "Wall2");

            // 지붕 슬래브: 처마까지 연장한 경사면을 계산해서 회전 배치
            float run   = hw2 + eave;                       // 2.8
            float rise  = roofRise * (run / hw2);           // 처마 끝까지 이어진 낙차
            float eaveY = yRidge - rise;
            float slope = Mathf.Sqrt(run * run + rise * rise);
            float ang   = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            const float roofT = 0.22f;
            float roofD = hd2 * 2 + 0.9f;                   // 박공 쪽 처마

            Vector3 nR = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), Mathf.Cos(ang * Mathf.Deg2Rad), 0f);
            Vector3 midR = new Vector3(run * 0.5f, (yRidge + eaveY) * 0.5f, 0f) + nR * (roofT * 0.5f);
            Vector3 midL = new Vector3(-midR.x, midR.y, 0f);

            BoxCentered(g, "32_Roof_Plane_R", new Vector3(slope + 0.15f, roofT, roofD), midR, new Vector3(0, 0, -ang), "Roof");
            BoxCentered(g, "33_Roof_Plane_L", new Vector3(slope + 0.15f, roofT, roofD), midL, new Vector3(0, 0,  ang), "Roof");
            Box(g, "34_Roof_Ridge", new Vector3(0.45f, 0.30f, roofD + 0.1f), new Vector3(0, yRidge - 0.05f, 0), "Trim");

            // ── 40 개구부 (벽 앞으로 살짝 빼서 Z-fighting 회피) ───────
            float fz = -hd1;                 // 1층 정면 바깥면
            float fz2 = -hd2;                // 2층 정면 바깥면

            // 문틀/창틀은 "벽에 딱 붙이지" 말고 벽 안쪽으로 파고들게 둔다.
            // 뒷면이 벽 속에 묻히므로 겹치는 면이 애초에 화면에 안 나온다.
            Stack(g, "40_Door_Frame", new Vector3(1.50f, 2.50f, 0.20f), new Vector3(0f, yF1, fz - 0.05f), "Trim");
            Stack(g, "41_Door_Panel", new Vector3(1.15f, 2.15f, 0.12f), new Vector3(0f, yF1, fz - 0.14f), "Wood");
            Box(g, "42_Window_L",  new Vector3(1.00f, 1.10f, 0.18f), new Vector3(-1.70f, 1.90f, fz - 0.04f), "Trim");
            Box(g, "43_Window_R",  new Vector3(1.00f, 1.10f, 0.18f), new Vector3( 1.70f, 1.90f, fz - 0.04f), "Trim");
            Box(g, "44_Window2_C", new Vector3(1.20f, 1.00f, 0.18f), new Vector3( 0f,    4.40f, fz2 - 0.04f), "Trim");

            // ── 50 차양 ────────────────────────────────────────────
            Cylinder(g, "50_Pillar_L", 0.14f, 2.95f, 8, new Vector3(-2.0f, yF1 - Bite, -3.10f), "Metal");
            Cylinder(g, "51_Pillar_R", 0.14f, 2.95f, 8, new Vector3( 2.0f, yF1 - Bite, -3.10f), "Metal");
            // 차양은 벽 속으로 0.2 파고들게 깊이를 잡았다 (딱 맞추면 경계면이 깜빡인다)
            BoxCentered(g, "52_Canopy", new Vector3(5.4f, 0.2f, 1.9f),
                        new Vector3(0f, 3.45f, -2.75f), new Vector3(-8f, 0f, 0f), "Metal");

            // ── 60 설비 / 간판 ─────────────────────────────────────
            //
            // 여기가 Z-fighting 이 나던 자리였다. 원인 두 가지:
            //   (1) 간판 우측면 x=-2.70 과 차양 좌측면 x=-2.70 이 정확히 같은 평면
            //   (2) 간판/팔의 앞면 z=-2.01 과 정면 벽 앞면 z=-2.00 이 1cm 차이 (사실상 동일 깊이)
            //
            // 고친 방법: 간판을 차양 바깥으로 완전히 빼서 맞닿는 면 자체를 없애고,
            // 팔은 벽 표면에 붙이는 대신 벽 두께 "속"을 지나가게 했다.
            //
            //   차양       x ∈ [-2.70,  2.70]
            //   2층 정면벽 x ∈ [-2.30,  2.30]   z ∈ [-1.80, -1.55]
            //   간판 팔    x ∈ [-4.15, -2.25]   z ∈ [-1.88, -1.70]  ← 벽을 10cm 뚫고 들어감
            //   간판       x ∈ [-4.08, -3.13]   z ∈ [-1.84, -1.74]  ← 팔 두께 안쪽
            //
            Box(g, "61_Sign_Arm",   new Vector3(1.90f, 0.14f, 0.18f), new Vector3(-3.20f, 5.00f, -1.79f), "Metal");
            Box(g, "60_Sign_Board", new Vector3(0.95f, 1.90f, 0.10f), new Vector3(-3.60f, 3.15f, -1.79f), "Accent");
            Cylinder(g, "62_Pipe_Corner", 0.11f, 5.60f, 8, new Vector3(2.32f, yF1 - Bite, -1.82f), "Rust");
            Box(g,   "63_Vent_Bracket", new Vector3(1.00f, 0.12f, 0.80f), new Vector3(2.55f, 4.08f, 0f), "Metal");
            Stack(g, "64_Vent_Box",     new Vector3(0.90f, 0.80f, 0.70f), new Vector3(2.55f, 4.20f, 0f), "Metal");
        }

        // ─────────────────────────────────────────────────────────────
        // 도형 헬퍼
        // ─────────────────────────────────────────────────────────────

        static ProBuilderMesh Box(Transform parent, string name, Vector3 size, Vector3 bottomCenter, string matKey)
        {
            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            return Finish(pb, parent, name, Vector3.zero, bottomCenter, false, matKey);
        }

        /// <summary>
        /// 다른 솔리드 위에 얹히는 파츠. 윗면은 지정한 높이 그대로 두고,
        /// 아랫면만 Bite 만큼 아래로 늘려서 받침 속에 파묻는다.
        ///
        /// 두 면을 같은 높이에 딱 맞추면 깊이 값이 동일해져서 깜빡인다(Z-fighting).
        /// "맞붙이기"가 아니라 "파묻기"가 정답이고, 이 게임 스케일에서 2cm 는 보이지 않는다.
        /// </summary>
        static ProBuilderMesh Stack(Transform parent, string name, Vector3 size, Vector3 bottomCenter, string matKey)
        {
            size.y += Bite;
            bottomCenter.y -= Bite;
            return Box(parent, name, size, bottomCenter, matKey);
        }

        static ProBuilderMesh BoxCentered(Transform parent, string name, Vector3 size, Vector3 center, Vector3 euler, string matKey)
        {
            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            return Finish(pb, parent, name, euler, center, true, matKey);
        }

        static ProBuilderMesh Prism(Transform parent, string name, Vector3 size, Vector3 bottomCenter, string matKey)
        {
            var pb = ShapeGenerator.GeneratePrism(PivotLocation.Center, size);
            return Finish(pb, parent, name, Vector3.zero, bottomCenter, false, matKey);
        }

        static ProBuilderMesh Stairs(Transform parent, string name, Vector3 size, int steps, Vector3 bottomCenter, string matKey)
        {
            var pb = ShapeGenerator.GenerateStair(PivotLocation.Center, size, steps, true);
            return Finish(pb, parent, name, Vector3.zero, bottomCenter, false, matKey);
        }

        static ProBuilderMesh Cylinder(Transform parent, string name, float radius, float height, int sides, Vector3 bottomCenter, string matKey)
        {
            // smoothing -1 → 각진 면 유지. 픽셀아트에서는 부드러운 셰이딩이 오히려 방해된다.
            var pb = ShapeGenerator.GenerateCylinder(PivotLocation.Center, sides, radius, height, 0, -1);
            return Finish(pb, parent, name, Vector3.zero, bottomCenter, false, matKey);
        }

        static ProBuilderMesh Arch(Transform parent, string name, float angle, float radius, float width,
                                   float depth, int radialCuts, Vector3 bottomCenter, Vector3 euler, string matKey)
        {
            var pb = ShapeGenerator.GenerateArch(PivotLocation.Center, angle, radius, width, depth, radialCuts,
                                                 true, true, true, true, true);
            return Finish(pb, parent, name, euler, bottomCenter, false, matKey);
        }

        static ProBuilderMesh Finish(ProBuilderMesh pb, Transform parent, string name,
                                     Vector3 euler, Vector3 anchor, bool byCenter, string matKey)
        {
            pb.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.localRotation = Quaternion.Euler(euler);

            pb.ToMesh();
            pb.Refresh();

            Place(pb, anchor, byCenter);

            var mr = pb.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = LoadMat(matKey);

            Undo.RegisterCreatedObjectUndo(pb.gameObject, "Build Stage");
            return pb;
        }

        /// <summary>
        /// 메시의 로컬 바운즈를 부모 기준 AABB 로 변환해서, 원하는 지점에 정확히 앉힌다.
        /// 도형마다 피벗 규칙이 달라도 이 방식이면 배치가 어긋나지 않는다.
        /// </summary>
        static void Place(ProBuilderMesh pb, Vector3 anchor, bool byCenter)
        {
            var mf = pb.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            Bounds lb = mf.sharedMesh.bounds;
            Matrix4x4 m = Matrix4x4.TRS(pb.transform.localPosition, pb.transform.localRotation, pb.transform.localScale);

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < 8; i++)
            {
                Vector3 c = new Vector3(
                    (i & 1) == 0 ? lb.min.x : lb.max.x,
                    (i & 2) == 0 ? lb.min.y : lb.max.y,
                    (i & 4) == 0 ? lb.min.z : lb.max.z);
                Vector3 w = m.MultiplyPoint3x4(c);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }

            Vector3 cur = byCenter
                ? (min + max) * 0.5f
                : new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);

            pb.transform.localPosition += anchor - cur;
        }

        /// <summary>
        /// 정점을 좌표의 함수로 변형한다. 같은 위치의 정점은 같은 결과를 받으므로
        /// 용접이 풀리지 않는다. 쐐기/경사/테이퍼를 만들 때 쓴다.
        /// </summary>
        static void Deform(ProBuilderMesh pb, System.Func<Vector3, Vector3> f)
        {
            var pos = new List<Vector3>(pb.positions);
            for (int i = 0; i < pos.Count; i++) pos[i] = f(pos[i]);
            pb.positions = pos;
            pb.ToMesh();
            pb.Refresh();
        }

        // ─────────────────────────────────────────────────────────────
        // 그룹 / UV 마무리
        // ─────────────────────────────────────────────────────────────

        static Transform GetOrCreateRoot()
        {
            var go = GameObject.Find(RootName);
            if (go == null)
            {
                go = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(go, "Create Stage Root");
            }
            return go.transform;
        }

        static Transform NewGroup(string name, Transform parent)
        {
            var old = parent.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create Stage Group");
            return go.transform;
        }

        /// <summary>
        /// 월드 스페이스 UV 적용. 반드시 최종 배치가 끝난 뒤에 호출해야 한다
        /// (ProBuilder 의 월드 투영은 트랜스폼을 참조한다).
        /// </summary>
        static void FinalizeGroup(Transform root)
        {
            float s = UvScale;
            var meshes = root.GetComponentsInChildren<ProBuilderMesh>(true);

            foreach (var pb in meshes)
            {
                var faces = pb.faces;
                for (int i = 0; i < faces.Count; i++)
                {
                    var f = faces[i];
                    f.manualUV = false;

                    var uv = f.uv;
                    uv.useWorldSpace = true;
                    uv.scale = new Vector2(s, s);
                    f.uv = uv;
                }

                pb.ToMesh();
                pb.Refresh(RefreshMask.All);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 에셋 생성
        // ─────────────────────────────────────────────────────────────

        static void EnsureFolders()
        {
            EnsureFolderPath(ArtDir);
            EnsureFolderPath(MatDir);
            EnsureFolderPath(TexDir);
        }

        /// <summary>"Assets/a/b/c" 처럼 몇 단계든 한 번에 만든다.</summary>
        internal static void EnsureFolderPath(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        static Shader LitShader()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            return sh;
        }

        static string MatPath(string key) { return MatDir + "/MAT_Stage_" + key + ".mat"; }

        static Material LoadMat(string key)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath(key));
            if (m == null) m = AssetDatabase.LoadAssetAtPath<Material>(MatPath("Concrete"));
            return m;
        }

        static void EnsureMaterial(MatDef d)
        {
            string path = MatPath(d.key);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(LitShader());

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", d.color);
            if (m.HasProperty("_Color"))     m.SetColor("_Color", d.color);
            if (m.HasProperty("_Metallic"))  m.SetFloat("_Metallic", 0f);
            // 픽셀아트는 스페큘러가 튀면 즉시 망가진다. Smoothness 0 고정.
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);

            if (d.emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", d.emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
        }

        static void EnsureCheckerMaterial()
        {
            string path = MatDir + "/MAT_Stage_Checker.mat";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(CheckerTexPath);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(LitShader());

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Metallic"))  m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            }

            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
        }

        /// <summary>
        /// 텍셀 밀도 검증용 체커. 64px 안에 8px 칸 8×8.
        /// 이 텍스처를 전부에 발랐을 때 모든 면에서 칸 크기가 같으면 UV 가 올바른 것.
        /// </summary>
        static void CreateCheckerTexture()
        {
            if (File.Exists(CheckerTexPath)) return;

            const int size = (int)TilePixels;
            const int cell = 8;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var a = new Color32(210, 210, 210, 255);
            var b = new Color32(120, 120, 120, 255);
            var mark = new Color32(200, 90, 70, 255);   // 원점 표시용 칸

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool odd = ((x / cell) + (y / cell)) % 2 == 1;
                    Color32 c = odd ? b : a;
                    if (x < cell && y < cell) c = mark;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();

            File.WriteAllBytes(CheckerTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(CheckerTexPath, ImportAssetOptions.ForceUpdate);

            var ti = AssetImporter.GetAtPath(CheckerTexPath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Default;
                ti.filterMode = FilterMode.Point;              // 픽셀아트 필수
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.SaveAndReimport();
            }
        }
    }
}
