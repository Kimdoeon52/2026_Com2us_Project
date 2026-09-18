// RE:AL STEEL - 옥토패스풍 "울퉁불퉁" 지형 생성기 (ProBuilder 6.x)
//
// StageSampleBuilder 의 박스 지형은 단차가 자로 잰 듯 각져 있다.
// 이 스크립트는 그 대신 높이맵을 ProBuilder 메시로 구워서
//   · 언덕(플라토) 가장자리가 구불구불하고 살짝 부풀어 오른 잔디 둔덕
//   · 잔디 윗면 / 바위 절벽면 자동 분리 (경사로 머티리얼 슬롯이 갈린다)
//   · 굽이치는 개울 + 돌다리
//   · 길·광장·집터는 평평하게 눌러서 걷기 좋게
//   · 언덕으로 올라가는 계단
// 을 한 번에 뽑는다. 결과물은 전부 ProBuilderMesh 라 이후 손으로 정점을 더 밀어 다듬을 수 있다.
//
// 메뉴: Tools > RE_AL STEEL > Stage > 2b. 울퉁불퉁 지형 생성 (옥토패스풍)
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace RealSteel.EditorTools
{
    public static class StageTerrainBuilder
    {
        // ─────────────────────────────────────────────────────────────
        // 규격
        // ─────────────────────────────────────────────────────────────

        const string RootName  = "STAGE_Sample";
        const string GroupName = "TERRAIN_Organic";

        /// <summary>
        /// 머티리얼 폴더는 StageSampleBuilder 의 출력 폴더 설정(public ArtDir)을 따른다.
        /// public 멤버만 참조한다 — 어셈블리가 갈려도 깨지지 않게.
        /// </summary>
        static string MatDir { get { return StageSampleBuilder.ArtDir + "/Materials"; } }

        /// <summary>지형 범위 (X 좌우, Z 앞뒤 - 카메라는 Z- 에서 바라본다)</summary>
        const float MinX = -12f, MaxX = 12f;
        const float MinZ = -7f,  MaxZ = 15f;

        /// <summary>격자 한 칸. 0.5 = 64px 타일의 1/4. 더 잘게 하면 부드럽지만 정점이 4배씩 는다.</summary>
        const float Cell = 0.5f;

        /// <summary>지형 바깥 테두리(스커트)를 어디까지 내려 덮을지</summary>
        const float SkirtBottom = -4f;

        /// <summary>면 법선의 y 가 이 값보다 작으면 "절벽" → 바위 머티리얼</summary>
        const float CliffNormalY = 0.62f;

        /// <summary>StageSampleBuilder.Bite 와 같은 값. 맞닿는 면을 파묻는 깊이.</summary>
        const float Bite = 0.02f;

        /// <summary>월드 UV 스케일 (StageSampleBuilder 와 동일: 32ppu / 64px)</summary>
        const float UvScale = 0.5f;

        // 잔디 윗면을 부드럽게 셰이딩할지. 절벽은 항상 각진 면(하드 엣지)으로 둔다.
        const bool SmoothGrassTop = true;

        // ─────────────────────────────────────────────────────────────
        // 레이아웃 - 여기 숫자를 만지면 지형이 바뀐다
        // ─────────────────────────────────────────────────────────────
        //
        //  Z+ (안쪽)
        //  ┌──────────── 뒷산 y=2.4 ───────────┐   z ≥ 12.5
        //  │ 왼쪽 언덕 y=1.6 │ 계단 │ 오른쪽 언덕 y=2.0 │
        //  │  (집터)         │ 길  │  (집터)          │   z 5.5 ~ 11.5
        //  ├───────── 개울 (굽이침) ───────────┤   z ≈ 1.6 ~ 4.4, 돌다리 x=0
        //  │              광장 y=0             │   z < 1.5
        //  └───────────────────────────────────┘
        //  Z- (카메라)

        struct Plateau
        {
            public Vector2 center, halfSize;
            public float round;     // 모서리 둥글기
            public float height;    // 윗면 높이
            public float edge;      // 절벽 전이 폭 (반폭). 작을수록 가파르다
            public Plateau(float cx, float cz, float hx, float hz, float r, float h, float e)
            { center = new Vector2(cx, cz); halfSize = new Vector2(hx, hz); round = r; height = h; edge = e; }
        }

        static readonly Plateau[] Plateaus =
        {
            new Plateau(-7.0f,  9.0f, 2.6f, 2.0f, 2.0f, 1.6f, 0.75f),  // 왼쪽 언덕
            new Plateau( 7.0f,  9.0f, 2.4f, 2.4f, 2.2f, 2.0f, 0.70f),  // 오른쪽 언덕 (집터)
            new Plateau( 0.0f, 22.0f, 40f,  9.5f, 1.0f, 2.4f, 0.80f),  // 뒷산 (z ≥ 12.5)
        };

        // 개울 중심선 z = StreamZ(x)
        const float StreamDepth = 1.5f;
        const float StreamHalfWidth = 1.1f;   // 바닥 반폭
        const float StreamBank = 1.0f;        // 둑 경사 폭
        const float WaterY = -0.95f;

        // 계단: 길(x=0) 끝에서 뒷산으로
        const float StairZ0 = 10.2f, StairZ1 = 12.8f, StairHalfW = 1.7f, StairTop = 2.4f;

        // 평탄화 구역 (길·광장·집터). 안에서는 노이즈를 거의 죽인다.
        struct FlatZone { public Vector2 a, b; public float r; public FlatZone(Vector2 a, Vector2 b, float r) { this.a = a; this.b = b; this.r = r; } }
        static readonly FlatZone[] FlatZones =
        {
            new FlatZone(new Vector2(0f, -7f), new Vector2(0f, 15f), 1.4f),   // 남북 길
            new FlatZone(new Vector2(-6f, -3f), new Vector2(6f, -3f), 2.4f),  // 광장
            new FlatZone(new Vector2(-7f, 9f), new Vector2(-7f, 9f), 1.8f),   // 왼쪽 집터
            new FlatZone(new Vector2(7f, 9f),  new Vector2(7f, 9f),  2.0f),   // 오른쪽 집터
        };

        // 노이즈
        const float RollAmp = 0.34f, RollFreq = 0.16f;   // 완만한 굴곡
        const float BumpAmp = 0.11f, BumpFreq = 0.80f;   // 잔 울퉁불퉁
        const float EdgeWobble = 1.3f, EdgeWobbleFreq = 0.42f;  // 절벽선 구불거림
        const float RimHeight = 0.28f;                    // 절벽 가장자리 잔디 둔덕 부풀기

        static int s_seed = 7;
        static float S { get { return s_seed * 13.37f; } }

        // ─────────────────────────────────────────────────────────────
        // 메뉴
        // ─────────────────────────────────────────────────────────────

        [MenuItem("Tools/RE_AL STEEL/Stage/2b. 울퉁불퉁 지형 생성 (옥토패스풍)", false, 12)]
        public static void MenuBuild()
        {
            Build();
        }

        [MenuItem("Tools/RE_AL STEEL/Stage/2c. 시드 바꿔서 다시 생성", false, 13)]
        public static void MenuReseed()
        {
            s_seed = Random.Range(1, 9999);
            Build();
        }

        static void Build()
        {
            StageSampleBuilder.MenuCreateAssets();

            var root = GetOrCreateRoot();

            // 박스 지형 샘플이 있으면 겹치므로 꺼 둔다 (삭제는 안 함)
            var boxy = root.Find("TERRAIN_Sample");
            if (boxy != null && boxy.gameObject.activeSelf)
            {
                Undo.RecordObject(boxy.gameObject, "Hide Box Terrain");
                boxy.gameObject.SetActive(false);
                Debug.Log("[Terrain] 기존 TERRAIN_Sample 은 비활성화했습니다. 필요하면 다시 켜세요.");
            }

            var g = NewGroup(GroupName, root);

            BuildGroundMesh(g);
            BuildWater(g);
            BuildBridge(g);
            BuildStairs(g);
            BuildRocks(g);

            ApplyWorldUv(g);

            Selection.activeTransform = g;
            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log("[Terrain] 울퉁불퉁 지형 생성 완료 (seed " + s_seed + ").\n" +
                      "· 잔디 윗면 = MAT_Stage_Ground, 절벽면 = MAT_Stage_Stone (면 기울기로 자동 분리)\n" +
                      "· 형태를 바꾸려면 StageTerrainBuilder.cs 의 Plateaus / FlatZones / 노이즈 상수를 조정하세요.\n" +
                      "· ProBuilder 정점 편집으로 국소 수정도 가능합니다.");
        }

        // ─────────────────────────────────────────────────────────────
        // 높이 함수
        // ─────────────────────────────────────────────────────────────

        /// <summary>월드 XZ 에서 지면 높이. 다른 스크립트에서 프롭을 얹을 때도 쓸 수 있다.</summary>
        public static float SampleHeight(float x, float z)
        {
            // 1) 플라토 (언덕). 가장 높은 것을 취한다.
            float wob = (Mathf.PerlinNoise(x * EdgeWobbleFreq + S, z * EdgeWobbleFreq + S * 0.7f) - 0.5f) * EdgeWobble;
            float plateau = 0f;
            float rim = 0f;
            foreach (var p in Plateaus)
            {
                float d = RoundedRectDist(new Vector2(x, z), p.center, p.halfSize, p.round) + wob;

                // 절벽 프로파일: d < -edge → 윗면, d > edge → 바닥
                float t = Mathf.Clamp01((d + p.edge) / (2f * p.edge));
                float k = 1f - SmoothStep(t);           // 1 = 위, 0 = 아래
                k = k * k * (3f - 2f * k);              // 한 번 더 → 중간이 더 가파르고 위아래는 완만
                float h = p.height * k;

                // 절벽 가장자리 잔디 둔덕 - 옥토패스 언덕 특유의 "볼록한 테두리"
                float rimT = Mathf.Clamp01(1f - Mathf.Abs(d + p.edge * 0.9f) / (p.edge * 1.4f));
                float r = RimHeight * rimT * rimT * Mathf.Clamp01(p.height / 2f);

                if (h > plateau) { plateau = h; rim = r; }
            }

            // 2) 평탄화 마스크 (길·광장·집터)
            float flat = 0f;
            foreach (var fz in FlatZones)
            {
                float dist = SegmentDist(new Vector2(x, z), fz.a, fz.b) - fz.r;
                flat = Mathf.Max(flat, 1f - SmoothStep(Mathf.Clamp01(dist / 1.4f)));
            }

            // 3) 노이즈
            float roll = (Mathf.PerlinNoise(x * RollFreq + S, z * RollFreq - S) - 0.5f) * 2f * RollAmp;
            float bump = (Mathf.PerlinNoise(x * BumpFreq - S * 0.3f, z * BumpFreq + S * 1.9f) - 0.5f) * 2f * BumpAmp;
            float noise = (roll + bump) * (1f - 0.88f * flat);
            rim *= (1f - flat);

            float h0 = plateau + rim + noise;

            // 4) 개울 - 언덕 위에는 못 흐르게 플라토 높이로 감쇠
            float dz = Mathf.Abs(z - StreamZ(x));
            float sT = Mathf.Clamp01((dz - StreamHalfWidth) / StreamBank);
            float stream = -StreamDepth * (1f - SmoothStep(sT));
            stream *= Mathf.Clamp01(1f - plateau / 0.6f);
            h0 += stream;

            // 5) 계단 자리: 계단 밑에 묻히도록 직선 램프로 덮어쓴다
            if (Mathf.Abs(x) < StairHalfW + 0.3f && z >= StairZ0 - 0.4f && z <= StairZ1 + 0.2f)
            {
                float rt = Mathf.Clamp01((z - StairZ0) / (StairZ1 - StairZ0));
                float ramp = Mathf.Lerp(0f, StairTop, rt) - 0.30f;
                float m = 1f - SmoothStep(Mathf.Clamp01((Mathf.Abs(x) - StairHalfW) / 0.3f));
                h0 = Mathf.Lerp(h0, ramp, m);
            }

            return h0;
        }

        static float StreamZ(float x)
        {
            return 3.0f + 0.9f * Mathf.Sin(x * 0.45f + S * 0.01f) + 0.35f * Mathf.Sin(x * 1.3f + 2f);
        }

        static float SmoothStep(float t) { return t * t * (3f - 2f * t); }

        /// <summary>둥근 사각형 부호 거리. 안쪽이 음수.</summary>
        static float RoundedRectDist(Vector2 p, Vector2 c, Vector2 half, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half;
            Vector2 qp = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return qp.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static float SegmentDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-5f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return (p - (a + ab * t)).magnitude;
        }

        // ─────────────────────────────────────────────────────────────
        // 지면 메시
        // ─────────────────────────────────────────────────────────────

        static void BuildGroundMesh(Transform parent)
        {
            int nx = Mathf.RoundToInt((MaxX - MinX) / Cell) + 1;
            int nz = Mathf.RoundToInt((MaxZ - MinZ) / Cell) + 1;

            var H = new float[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    H[i, j] = SampleHeight(MinX + i * Cell, MinZ + j * Cell);

            var positions = new List<Vector3>((nx - 1) * (nz - 1) * 4 + (nx + nz) * 8);
            var faces = new List<Face>((nx - 1) * (nz - 1) + (nx + nz) * 2);

            // 면마다 정점 4개를 따로 둔다 (하드 엣지). ProBuilderMesh.Create 가 같은 좌표를 공유 정점으로 묶어 준다.
            for (int i = 0; i < nx - 1; i++)
            {
                for (int j = 0; j < nz - 1; j++)
                {
                    Vector3 a = P(i,     j,     H);
                    Vector3 b = P(i + 1, j,     H);
                    Vector3 c = P(i + 1, j + 1, H);
                    Vector3 d = P(i,     j + 1, H);

                    int i0 = positions.Count;
                    positions.Add(a); positions.Add(b); positions.Add(c); positions.Add(d);

                    // 대각선은 높이차가 작은 쪽으로 - 능선/골이 지형을 따라 접힌다
                    bool diagAC = Mathf.Abs(a.y - c.y) <= Mathf.Abs(b.y - d.y);
                    int[] idx = diagAC
                        ? new[] { i0, i0 + 3, i0 + 2,  i0, i0 + 2, i0 + 1 }
                        : new[] { i0, i0 + 3, i0 + 1,  i0 + 1, i0 + 3, i0 + 2 };

                    var f = new Face(idx);
                    Vector3 n = Vector3.Cross(c - a, b - d).normalized;   // 위쪽 법선 (대각선 두 개의 외적)
                    if (n.y < 0f) n = -n;
                    bool cliff = n.y < CliffNormalY;
                    f.submeshIndex = cliff ? 1 : 0;
                    f.smoothingGroup = (!cliff && SmoothGrassTop) ? 1 : 0;
                    faces.Add(f);
                }
            }

            // 스커트: 네 변 테두리를 아래로 내려 옆구리를 막는다
            Vector3 center = new Vector3((MinX + MaxX) * 0.5f, 0f, (MinZ + MaxZ) * 0.5f);
            for (int i = 0; i < nx - 1; i++)
            {
                AddSkirt(positions, faces, P(i, 0, H),      P(i + 1, 0, H),      center);
                AddSkirt(positions, faces, P(i, nz - 1, H), P(i + 1, nz - 1, H), center);
            }
            for (int j = 0; j < nz - 1; j++)
            {
                AddSkirt(positions, faces, P(0, j, H),      P(0, j + 1, H),      center);
                AddSkirt(positions, faces, P(nx - 1, j, H), P(nx - 1, j + 1, H), center);
            }

            var pb = ProBuilderMesh.Create(positions, faces);
            pb.name = "TER_Ground_Organic";
            pb.transform.SetParent(parent, false);

            var mr = pb.GetComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { LoadMat("Ground"), LoadMat("Stone") };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            pb.gameObject.AddComponent<MeshCollider>();
            pb.ToMesh();
            pb.Refresh();

            Undo.RegisterCreatedObjectUndo(pb.gameObject, "Build Organic Terrain");
        }

        static Vector3 P(int i, int j, float[,] H)
        {
            return new Vector3(MinX + i * Cell, H[i, j], MinZ + j * Cell);
        }

        static void AddSkirt(List<Vector3> pos, List<Face> faces, Vector3 p0, Vector3 p1, Vector3 center)
        {
            Vector3 q0 = new Vector3(p0.x, SkirtBottom, p0.z);
            Vector3 q1 = new Vector3(p1.x, SkirtBottom, p1.z);

            int i0 = pos.Count;
            pos.Add(p0); pos.Add(p1); pos.Add(q1); pos.Add(q0);

            // 바깥을 향하도록 감기 방향 결정
            Vector3 n = Vector3.Cross(p1 - p0, q1 - p0);
            Vector3 mid = (p0 + p1) * 0.5f;
            bool flip = Vector3.Dot(n, mid - center) < 0f;

            int[] idx = flip
                ? new[] { i0, i0 + 2, i0 + 1,  i0, i0 + 3, i0 + 2 }
                : new[] { i0, i0 + 1, i0 + 2,  i0, i0 + 2, i0 + 3 };

            var f = new Face(idx) { submeshIndex = 1, smoothingGroup = 0 };
            faces.Add(f);
        }

        // ─────────────────────────────────────────────────────────────
        // 물 / 다리 / 계단 / 바위
        // ─────────────────────────────────────────────────────────────

        static void BuildWater(Transform g)
        {
            float w = MaxX - MinX;
            var pb = ShapeGenerator.GeneratePlane(PivotLocation.Center, w, 7f, 1, 1, Axis.Up);
            pb.name = "TER_Stream_Water";
            pb.transform.SetParent(g, false);
            pb.transform.localPosition = new Vector3((MinX + MaxX) * 0.5f, WaterY, 3.0f);
            pb.ToMesh(); pb.Refresh();
            var mr = pb.GetComponent<MeshRenderer>();
            mr.sharedMaterial = LoadMat("Water");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Undo.RegisterCreatedObjectUndo(pb.gameObject, "Build Organic Terrain");
        }

        static void BuildBridge(Transform g)
        {
            float zc = StreamZ(0f);
            float span = (StreamHalfWidth + StreamBank) * 2f + 1.2f;   // 둑 위까지 걸치게
            var br = NewGroup("TER_Bridge", g);

            // 아치: XY 평면 아치를 Y축 90도 돌려 개울을 가로지른다
            var arch = ShapeGenerator.GenerateArch(PivotLocation.Center, 180f, 1.35f, 0.4f, 4.4f, 9, true, true, true, true, true);
            Finish(arch, br, "BR_Arch", new Vector3(0f, 90f, 0f), new Vector3(0f, WaterY - 0.9f, zc), false, "Stone");

            Box(br, "BR_Deck",      new Vector3(4.4f, 0.45f, span),        new Vector3(0f, -0.40f, zc), "Stone");
            Box(br, "BR_Parapet_W", new Vector3(0.35f, 0.75f, span + 0.2f), new Vector3(-2.05f, 0.05f - Bite, zc), "Stone");
            Box(br, "BR_Parapet_E", new Vector3(0.35f, 0.75f, span + 0.2f), new Vector3( 2.05f, 0.05f - Bite, zc), "Stone");
        }

        static void BuildStairs(Transform g)
        {
            float depth = StairZ1 - StairZ0;
            var pb = ShapeGenerator.GenerateStair(PivotLocation.Center, new Vector3(StairHalfW * 2f, StairTop, depth), 8, true);
            Finish(pb, g, "TER_Stairs_Hill", Vector3.zero, new Vector3(0f, -Bite, StairZ0 + depth * 0.5f), false, "Concrete");

            // 계단 양옆 옹벽 - 절벽 단면이 계단과 만나는 자리를 덮는다
            Box(g, "TER_Stair_Cheek_W", new Vector3(0.4f, StairTop + 0.3f, depth + 0.4f), new Vector3(-StairHalfW - 0.15f, -0.3f, StairZ0 + depth * 0.5f), "Stone");
            Box(g, "TER_Stair_Cheek_E", new Vector3(0.4f, StairTop + 0.3f, depth + 0.4f), new Vector3( StairHalfW + 0.15f, -0.3f, StairZ0 + depth * 0.5f), "Stone");
        }

        static void BuildRocks(Transform g)
        {
            var rocks = NewGroup("TER_Rocks", g);
            var rng = new System.Random(s_seed * 31 + 5);

            int made = 0, tries = 0;
            while (made < 14 && tries++ < 300)
            {
                float x = Mathf.Lerp(MinX + 1f, MaxX - 1f, (float)rng.NextDouble());
                float z = Mathf.Lerp(MinZ + 1f, MaxZ - 1f, (float)rng.NextDouble());

                // 길·광장·계단·개울 위는 피한다
                if (Mathf.Abs(x) < 2.2f) continue;
                if (Mathf.Abs(z - StreamZ(x)) < StreamHalfWidth + StreamBank + 0.5f) continue;
                if (z < 0f && Mathf.Abs(x) < 8f) continue;

                float h = SampleHeight(x, z);
                float s = Mathf.Lerp(0.45f, 1.3f, (float)rng.NextDouble());
                var size = new Vector3(s * Mathf.Lerp(0.8f, 1.4f, (float)rng.NextDouble()),
                                       s * Mathf.Lerp(0.5f, 0.9f, (float)rng.NextDouble()),
                                       s * Mathf.Lerp(0.8f, 1.3f, (float)rng.NextDouble()));

                var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
                float yaw = (float)rng.NextDouble() * 360f;
                Finish(pb, rocks, "ROCK_" + made.ToString("00"), new Vector3(0f, yaw, 0f),
                       new Vector3(x, h - size.y * 0.35f, z), false, "Stone");

                // 정점을 무작위로 밀어 바위 느낌 (같은 좌표는 같은 결과 → 용접 유지)
                int salt = rng.Next(1, 1000);
                Deform(pb, v =>
                {
                    float nx = Mathf.PerlinNoise(v.x * 3.1f + salt, v.z * 3.1f + salt * 0.5f) - 0.5f;
                    float nz = Mathf.PerlinNoise(v.y * 2.7f + salt * 0.3f, v.x * 2.7f - salt) - 0.5f;
                    float ny = Mathf.PerlinNoise(v.z * 2.9f - salt, v.y * 2.9f + salt * 0.8f) - 0.5f;
                    return v + new Vector3(nx, ny, nz) * s * 0.35f;
                });
                made++;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 헬퍼 (StageSampleBuilder 와 같은 규칙)
        // ─────────────────────────────────────────────────────────────

        static ProBuilderMesh Box(Transform parent, string name, Vector3 size, Vector3 bottomCenter, string matKey)
        {
            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            return Finish(pb, parent, name, Vector3.zero, bottomCenter, false, matKey);
        }

        static ProBuilderMesh Finish(ProBuilderMesh pb, Transform parent, string name,
                                     Vector3 euler, Vector3 anchor, bool byCenter, string matKey)
        {
            pb.name = name;
            pb.transform.SetParent(parent, false);
            pb.transform.localRotation = Quaternion.Euler(euler);
            pb.ToMesh(); pb.Refresh();
            Place(pb, anchor, byCenter);

            var mr = pb.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = LoadMat(matKey);

            Undo.RegisterCreatedObjectUndo(pb.gameObject, "Build Organic Terrain");
            return pb;
        }

        static void Place(ProBuilderMesh pb, Vector3 anchor, bool byCenter)
        {
            var mf = pb.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            Bounds lb = mf.sharedMesh.bounds;
            Matrix4x4 m = Matrix4x4.TRS(pb.transform.localPosition, pb.transform.localRotation, pb.transform.localScale);
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x,
                                        (i & 2) == 0 ? lb.min.y : lb.max.y,
                                        (i & 4) == 0 ? lb.min.z : lb.max.z);
                Vector3 w = m.MultiplyPoint3x4(c);
                min = Vector3.Min(min, w); max = Vector3.Max(max, w);
            }
            Vector3 cur = byCenter ? (min + max) * 0.5f
                                   : new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
            pb.transform.localPosition += anchor - cur;
        }

        static void Deform(ProBuilderMesh pb, System.Func<Vector3, Vector3> f)
        {
            var pos = new List<Vector3>(pb.positions);
            for (int i = 0; i < pos.Count; i++) pos[i] = f(pos[i]);
            pb.positions = pos;
            pb.ToMesh(); pb.Refresh();
        }

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

        static Material LoadMat(string key)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/MAT_Stage_" + key + ".mat");
            if (m == null) m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/MAT_Stage_Concrete.mat");
            return m;
        }

        /// <summary>월드 스페이스 UV. 최종 배치 후에 호출.</summary>
        static void ApplyWorldUv(Transform root)
        {
            foreach (var pb in root.GetComponentsInChildren<ProBuilderMesh>(true))
            {
                var faces = pb.faces;
                for (int i = 0; i < faces.Count; i++)
                {
                    var f = faces[i];
                    f.manualUV = false;
                    var uv = f.uv;
                    uv.useWorldSpace = true;
                    uv.scale = new Vector2(UvScale, UvScale);
                    f.uv = uv;
                }
                pb.ToMesh();
                pb.Refresh(RefreshMask.All);
            }
        }
    }
}
