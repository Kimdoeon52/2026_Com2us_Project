using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace RealSteel.EditorTools
{
    /// <summary>
    /// 균일한 ProBuilder 지오메트리를 "손으로 쌓은 것처럼" 흐트러뜨린다.
    ///
    /// ProBuilder 기본 도형은 완벽하게 균일해서 밋밋하다. 참고작(옥토패스)의 계단·지형이
    /// 풍부해 보이는 이유는 면이 휘어서가 아니라, <b>평평한 면들이 제각각 놓여 있어서</b>다.
    /// 면마다 각도가 조금씩 다르면 빛을 다르게 받아 변화가 생기는데,
    /// 각 면은 여전히 평평하므로 픽셀 텍스처가 뭉개지지 않는다.
    ///
    /// 두 가지를 제공한다.
    ///   · <b>돌계단 생성</b> — 단마다 돌을 따로 쌓아 폭·깊이·각도를 흩어 놓는다
    ///   · <b>울퉁불퉁 바닥 생성</b> — 격자 칸마다 돌을 따로 놓고 윗면 높이를 계단식으로 흩어 놓는다
    ///   · <b>정점 흔들기</b> — 기존 메시의 정점을 격자에 스냅해서 밀어 놓는다
    ///
    /// 흔들림은 항상 <b>격자에 스냅</b>된다. 연속적으로 흔들면 표면이 매끈한 곡면이 되어
    /// 부드러운 그라데이션이 생기고, 그게 픽셀 텍스처와 싸운다 — 각져 있어야 한다.
    /// </summary>
    public class StageRoughenTool : EditorWindow
    {
        // ── 돌계단 ──
        int steps = 6;
        float totalRise = 2f;
        float totalRun = 3f;
        float stairWidth = 4f;
        int stonesPerStep = 2;
        float sizeJitter = 0.18f;
        float rotJitter = 2.5f;

        // ── 울퉁불퉁 바닥 ──
        float groundWidth = 8f;
        float groundDepth = 6f;
        float groundCell = 0.75f;
        float groundThickness = 0.6f;
        float heightRange = 0.3f;
        float heightStep = 0.1f;
        bool groundStatic = true;

        // ── 정점 흔들기 ──
        float jitterAmount = 0.12f;
        float jitterSnap = 0.05f;
        bool jitterX = true, jitterY = true, jitterZ = true;
        bool keepBottomFlat = true;
        bool keepOutline = false;

        int seed = 12345;

        /// <summary>생성한 돌에 붙일 머티리얼. 비워 두면 ProBuilder 기본값이 붙는다.</summary>
        Material stoneMaterial;

        Vector2 scroll;
        string report = "";

        [MenuItem("Tools/RE_AL STEEL/Stage/지형 흐트러뜨리기 (돌계단 · 울퉁불퉁 바닥 · 정점 흔들기)", false, 45)]
        static void Open()
        {
            var w = GetWindow<StageRoughenTool>("지형 흐트러뜨리기");
            w.minSize = new Vector2(460f, 620f);
        }

        // ─────────────────────────────────────────────────────────

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "균일한 도형을 손으로 쌓은 것처럼 흐트러뜨린다.\n" +
                "면은 평평하게 두고 배치만 흩어 놓으므로 픽셀 텍스처가 안 뭉개진다.",
                MessageType.None);

            EditorGUILayout.Space(4f);
            seed = EditorGUILayout.IntField(
                new GUIContent("시드", "같은 시드 = 같은 결과. 마음에 안 들면 숫자만 바꿔 다시 뽑는다."), seed);
            stoneMaterial = (Material)EditorGUILayout.ObjectField(
                new GUIContent("머티리얼", "생성한 돌에 붙일 머티리얼. 비워 두면 기본값."),
                stoneMaterial, typeof(Material), false);

            // ── 돌계단 ─────────────────────────────────────────
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("돌계단 생성", EditorStyles.boldLabel);

            steps = Mathf.Max(2, EditorGUILayout.IntField("단 수", steps));
            totalRise = EditorGUILayout.FloatField(
                new GUIContent("총 높이", "맨 아래에서 맨 위까지"), totalRise);
            totalRun = EditorGUILayout.FloatField(
                new GUIContent("총 깊이", "앞에서 뒤까지"), totalRun);
            stairWidth = EditorGUILayout.FloatField("폭", stairWidth);

            stonesPerStep = Mathf.Clamp(EditorGUILayout.IntField(
                new GUIContent("단당 돌 개수",
                    "1 = 통짜 한 덩이. 2~3 = 폭을 나눠 돌을 여러 개 쌓는다 (석축 느낌)."),
                stonesPerStep), 1, 4);

            sizeJitter = EditorGUILayout.Slider(
                new GUIContent("크기 흔들기", "돌마다 폭·깊이·높이를 이 비율만큼 다르게"),
                sizeJitter, 0f, 0.5f);
            rotJitter = EditorGUILayout.Slider(
                new GUIContent("각도 흔들기(도)", "돌을 살짝 비틀어 놓는다. 3도만 넘어도 꽤 거칠어진다."),
                rotJitter, 0f, 10f);

            if (GUILayout.Button("돌계단 만들기", GUILayout.Height(28f))) BuildStoneStairs();

            // ── 울퉁불퉁 바닥 ──────────────────────────────────
            EditorGUILayout.Space(14f);
            EditorGUILayout.LabelField("울퉁불퉁 바닥 생성", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "정점 흔들기는 정점이 많은 메시에만 먹는다 — 육면체 하나짜리 바닥은 흔들어도\n" +
                "그냥 기울어질 뿐이다. 울퉁불퉁한 바닥은 칸마다 돌을 따로 놓아서 만든다.",
                MessageType.None);

            groundWidth = EditorGUILayout.FloatField("폭 (X)", groundWidth);
            groundDepth = EditorGUILayout.FloatField("깊이 (Z)", groundDepth);
            groundCell = Mathf.Max(0.1f, EditorGUILayout.FloatField(
                new GUIContent("칸 크기", "돌 하나의 크기. 작을수록 조밀하지만 오브젝트가 많아진다."), groundCell));
            groundThickness = Mathf.Max(0.05f, EditorGUILayout.FloatField(
                new GUIContent("두께", "아래로 파묻는 깊이. 넉넉해야 비틀어도 아래가 안 뚫린다."), groundThickness));

            heightRange = EditorGUILayout.Slider(
                new GUIContent("높이 편차", "칸마다 윗면 높이가 이 범위 안에서 달라진다. 0 = 평평한 돌바닥."),
                heightRange, 0f, 1.5f);
            heightStep = EditorGUILayout.Slider(
                new GUIContent("높이 단위",
                    "높이를 이 단위로 끊는다. 0 에 가까우면 매끈한 언덕이 되어 부드러운\n" +
                    "그라데이션이 생긴다 — 픽셀아트에는 층이 져 있는 편이 낫다."),
                heightStep, 0f, 0.4f);

            groundStatic = EditorGUILayout.Toggle(
                new GUIContent("Static 으로 표시", "돌이 오브젝트마다 하나라 개수가 많다. 정적 배칭이 먹게 켜 둔다."),
                groundStatic);

            int nxPreview = Mathf.Max(1, Mathf.CeilToInt(groundWidth / groundCell));
            int nzPreview = Mathf.Max(1, Mathf.CeilToInt(groundDepth / groundCell));
            EditorGUILayout.LabelField(" ", nxPreview + " × " + nzPreview + " = 돌 " + (nxPreview * nzPreview) + "개",
                EditorStyles.miniLabel);

            if (GUILayout.Button("울퉁불퉁 바닥 만들기", GUILayout.Height(28f))) BuildRockGround();

            // ── 정점 흔들기 ────────────────────────────────────
            EditorGUILayout.Space(14f);
            EditorGUILayout.LabelField("정점 흔들기 (선택한 오브젝트)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "이미 만든 언덕·바닥·옹벽을 거칠게 만든다.\n" +
                "같은 자리에 있는 정점은 같은 값으로 밀리므로 메시가 찢어지지 않는다.",
                MessageType.None);

            jitterAmount = EditorGUILayout.Slider(
                new GUIContent("흔들기 세기(유닛)", "정점이 최대 이만큼 밀린다"), jitterAmount, 0f, 1f);
            jitterSnap = EditorGUILayout.Slider(
                new GUIContent("격자 스냅",
                    "밀린 값을 이 단위로 반올림한다. 0 에 가까우면 매끈한 곡면이 되어\n" +
                    "부드러운 그라데이션이 생긴다 — 픽셀아트에는 각져 있는 편이 낫다."),
                jitterSnap, 0f, 0.25f);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("축", GUILayout.Width(30f));
                jitterX = EditorGUILayout.ToggleLeft("X", jitterX, GUILayout.Width(45f));
                jitterY = EditorGUILayout.ToggleLeft("Y", jitterY, GUILayout.Width(45f));
                jitterZ = EditorGUILayout.ToggleLeft("Z", jitterZ, GUILayout.Width(45f));
            }

            keepBottomFlat = EditorGUILayout.Toggle(
                new GUIContent("아랫면 고정", "바닥이 떠서 틈이 생기는 걸 막는다"), keepBottomFlat);
            keepOutline = EditorGUILayout.Toggle(
                new GUIContent("바깥 실루엣 고정",
                    "가장자리 정점을 안 건드린다. 옆 오브젝트와 맞물린 면이 벌어지는 걸 막는다."),
                keepOutline);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("흔들기", GUILayout.Height(26f))) Jitter(false);
                if (GUILayout.Button("한 번 더", GUILayout.Height(26f))) Jitter(true);
            }

            EditorGUILayout.Space(8f);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────────────────
        // 돌계단
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// 단마다 돌을 따로 놓아 계단을 쌓는다.
        ///
        /// ProBuilder 의 Stairs 도형은 한 덩어리라 모든 단이 똑같다. 여기서는 돌을 하나씩
        /// 놓으면서 폭·깊이·높이·각도를 흩어 놓는다 — 각 돌은 여전히 평평한 육면체다.
        ///
        /// 돌은 아래로 넉넉히 겹쳐 놓는다. 딱 맞추면 각도를 비튼 순간 틈이 벌어진다.
        /// </summary>
        void BuildStoneStairs()
        {
            var rng = new System.Random(seed);
            var mat = stoneMaterial;

            var root = new GameObject($"TER_StoneStairs_{seed}");
            Undo.RegisterCreatedObjectUndo(root, "돌계단 만들기");

            float rise = totalRise / steps;
            float run = totalRun / steps;
            int made = 0;

            for (int i = 0; i < steps; i++)
            {
                float topY = rise * (i + 1);
                float centerZ = run * (i + 0.5f);

                // 돌을 폭 방향으로 나눈다. 이음매가 보이지 않게 서로 겹쳐 놓는다.
                float slice = stairWidth / stonesPerStep;
                for (int s = 0; s < stonesPerStep; s++)
                {
                    float w = slice * (1f + Rand(rng, sizeJitter)) + 0.08f;
                    float d = run * (1f + Rand(rng, sizeJitter)) + 0.06f;

                    // 높이는 아래로 길게 뺀다 — 비틀어도 아래가 안 뚫린다
                    float h = rise * (1.6f + Mathf.Abs(Rand(rng, sizeJitter)));

                    float x = -stairWidth * 0.5f + slice * (s + 0.5f) + Rand(rng, 0.04f);
                    float z = centerZ + Rand(rng, run * 0.08f);

                    var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(w, h, d));
                    pb.name = $"Stone_{i:00}_{s}";
                    pb.transform.SetParent(root.transform, false);
                    pb.transform.localRotation = Quaternion.Euler(
                        Rand(rng, rotJitter * 0.4f),
                        Rand(rng, rotJitter),
                        Rand(rng, rotJitter * 0.4f));

                    pb.ToMesh();
                    pb.Refresh();

                    // 윗면이 정확히 topY 에 오도록 앉힌다 (회전 후 바운즈 기준)
                    PlaceTop(pb, new Vector3(x, topY, z));

                    var mr = pb.GetComponent<MeshRenderer>();
                    if (mr != null && mat != null) mr.sharedMaterial = mat;

                    made++;
                }
            }

            Selection.activeGameObject = root;
            report = $"돌 {made}개로 {steps}단 계단 생성 (시드 {seed})\n\n" +
                     $"  단 높이 {rise:0.###} · 단 깊이 {run:0.###} · 폭 {stairWidth:0.##}\n" +
                     $"  마음에 안 들면 시드만 바꿔 다시 뽑을 것.\n\n" +
                     "다음: UV 밀도 맞추기 → 픽셀 텍스처 설정 순서로 돌리면 된다.";
        }

        /// <summary>회전까지 반영한 바운즈 기준으로 '윗면 중심'을 맞춘다.</summary>
        static void PlaceTop(ProBuilderMesh pb, Vector3 target)
        {
            var mf = pb.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            Bounds lb = mf.sharedMesh.bounds;
            Matrix4x4 m = Matrix4x4.TRS(Vector3.zero, pb.transform.localRotation, Vector3.one);

            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3(
                    (i & 1) == 0 ? lb.min.x : lb.max.x,
                    (i & 2) == 0 ? lb.min.y : lb.max.y,
                    (i & 4) == 0 ? lb.min.z : lb.max.z);
                var w = m.MultiplyPoint3x4(c);
                min = Vector3.Min(min, w); max = Vector3.Max(max, w);
            }

            var cur = new Vector3((min.x + max.x) * 0.5f, max.y, (min.z + max.z) * 0.5f);
            pb.transform.localPosition += target - cur;
        }

        static float Rand(System.Random r, float amp) => ((float)r.NextDouble() * 2f - 1f) * amp;

        // ─────────────────────────────────────────────────────────
        // 울퉁불퉁 바닥
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// 격자 칸마다 돌을 따로 놓아 울퉁불퉁한 바닥을 만든다.
        ///
        /// 정점 흔들기로는 이걸 만들 수 없다. 육면체 하나는 모서리 정점 8개뿐이라
        /// 흔들면 바닥 전체가 기울어질 뿐이고, 가운데가 솟거나 꺼지지 않는다.
        /// 옥토패스의 지면이 풍부한 이유도 면이 휘어서가 아니라 <b>덩어리가 여러 개라서</b>다.
        ///
        /// 높이는 넓은 기복(coarse) 7 : 칸별 잔변화(fine) 3 으로 섞는다.
        /// 칸마다 순수 난수를 주면 TV 노이즈처럼 보이고 지형으로 안 읽힌다.
        /// 섞은 값은 <c>높이 단위</c>로 끊어 층이 지게 만든다 — 연속적이면 매끈한 곡면이 되어
        /// 부드러운 그라데이션이 생기고, 그게 픽셀 텍스처와 싸운다.
        /// </summary>
        void BuildRockGround()
        {
            int nx = Mathf.Max(1, Mathf.CeilToInt(groundWidth / groundCell));
            int nz = Mathf.Max(1, Mathf.CeilToInt(groundDepth / groundCell));
            int cells = nx * nz;

            if (cells > 1200)
            {
                report = "돌 " + cells + "개는 너무 많다. 칸 크기를 키우거나 범위를 줄일 것.";
                return;
            }
            if (cells > 300 && !EditorUtility.DisplayDialog("울퉁불퉁 바닥",
                    "돌 " + cells + "개를 만든다. 씬이 무거워질 수 있다.", "계속", "취소"))
                return;

            var rng = new System.Random(seed);
            var mat = stoneMaterial;

            var root = new GameObject("TER_RockGround_" + seed);
            Undo.RegisterCreatedObjectUndo(root, "울퉁불퉁 바닥 만들기");

            // 칸끼리 넉넉히 겹쳐 놓는다 — 비틀면 모서리에서 틈이 벌어진다
            float overlap = groundCell * 0.14f;
            float h = groundThickness + heightRange;

            float loY = float.MaxValue, hiY = float.MinValue;

            for (int ix = 0; ix < nx; ix++)
            {
                for (int iz = 0; iz < nz; iz++)
                {
                    float topY = CellHeight(ix, iz);
                    loY = Mathf.Min(loY, topY); hiY = Mathf.Max(hiY, topY);

                    float w = groundCell * (1f + Rand(rng, sizeJitter)) + overlap;
                    float d = groundCell * (1f + Rand(rng, sizeJitter)) + overlap;

                    float x = -groundWidth * 0.5f + groundCell * (ix + 0.5f) + Rand(rng, groundCell * 0.05f);
                    float z = -groundDepth * 0.5f + groundCell * (iz + 0.5f) + Rand(rng, groundCell * 0.05f);

                    var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(w, h, d));
                    pb.name = "Rock_" + ix.ToString("00") + "_" + iz.ToString("00");
                    pb.transform.SetParent(root.transform, false);
                    pb.transform.localRotation = Quaternion.Euler(
                        Rand(rng, rotJitter * 0.6f),
                        Rand(rng, rotJitter * 2f),      // 요우는 바닥에서 티가 안 나므로 넉넉히
                        Rand(rng, rotJitter * 0.6f));

                    pb.ToMesh();
                    pb.Refresh();

                    PlaceTop(pb, new Vector3(x, topY, z));

                    var mr = pb.GetComponent<MeshRenderer>();
                    if (mr != null && mat != null) mr.sharedMaterial = mat;
                    if (groundStatic) pb.gameObject.isStatic = true;
                }
            }

            if (groundStatic) root.isStatic = true;
            Selection.activeGameObject = root;

            report = "돌 " + cells + "개로 " + nx + " × " + nz + " 바닥 생성 (시드 " + seed + ")\n\n" +
                     "  범위 " + groundWidth.ToString("0.##") + " × " + groundDepth.ToString("0.##") +
                     " · 칸 " + groundCell.ToString("0.###") + "\n" +
                     "  윗면 높이 " + loY.ToString("0.###") + " ~ " + hiY.ToString("0.###") +
                     " (단위 " + heightStep.ToString("0.###") + ")\n" +
                     "  마음에 안 들면 시드만 바꿔 다시 뽑을 것.\n\n" +
                     "캐릭터가 걸어다닐 바닥이면 높이 편차 0.1~0.2 이 적당하다. 그 이상은 발이 뜬다.\n" +
                     "다음: UV 밀도 맞추기 → 픽셀 텍스처 선명하게 순서로 돌리면 된다.";
        }

        /// <summary>넓은 기복 7 : 칸별 잔변화 3 을 섞고 높이 단위로 끊는다.</summary>
        float CellHeight(int ix, int iz)
        {
            float coarse = Hash(new Vector3(Mathf.Floor(ix / 3f), 0f, Mathf.Floor(iz / 3f)), seed, 7);
            float fine = Hash(new Vector3(ix, 0f, iz), seed, 8);
            float v = coarse * 0.7f + fine * 0.3f;

            float y = (v * 2f - 1f) * heightRange * 0.5f;
            if (heightStep <= 0.0001f) return y;
            return Mathf.Round(y / heightStep) * heightStep;
        }

        // ─────────────────────────────────────────────────────────
        // 정점 흔들기
        // ─────────────────────────────────────────────────────────

        void Jitter(bool again)
        {
            var meshes = new List<ProBuilderMesh>();
            foreach (var go in Selection.gameObjects)
            {
                if (go == null) continue;
                foreach (var pb in go.GetComponentsInChildren<ProBuilderMesh>(true))
                    if (!meshes.Contains(pb)) meshes.Add(pb);
            }
            if (meshes.Count == 0) { report = "ProBuilder 오브젝트를 선택할 것."; return; }

            if (again) seed++;
            var log = new StringBuilder();

            foreach (var pb in meshes)
            {
                Undo.RecordObject(pb, "정점 흔들기");

                var pos = new List<Vector3>(pb.positions);
                if (pos.Count == 0) continue;

                // 아랫면·바깥 실루엣을 지키려면 어디가 끝인지 알아야 한다
                float minY = float.MaxValue;
                var bMin = Vector3.one * float.MaxValue;
                var bMax = Vector3.one * float.MinValue;
                foreach (var p in pos)
                {
                    minY = Mathf.Min(minY, p.y);
                    bMin = Vector3.Min(bMin, p);
                    bMax = Vector3.Max(bMax, p);
                }

                const float eps = 0.001f;
                int moved = 0;

                for (int i = 0; i < pos.Count; i++)
                {
                    var p = pos[i];

                    if (keepBottomFlat && Mathf.Abs(p.y - minY) < eps) continue;
                    if (keepOutline && OnOutline(p, bMin, bMax, eps)) continue;

                    // 같은 자리의 정점은 같은 오프셋을 받아야 메시가 찢어지지 않는다
                    var off = new Vector3(
                        jitterX ? Snap((Hash(p, seed, 0) * 2f - 1f) * jitterAmount) : 0f,
                        jitterY ? Snap((Hash(p, seed, 1) * 2f - 1f) * jitterAmount) : 0f,
                        jitterZ ? Snap((Hash(p, seed, 2) * 2f - 1f) * jitterAmount) : 0f);

                    if (off.sqrMagnitude > 0f) { pos[i] = p + off; moved++; }
                }

                pb.positions = pos;
                pb.ToMesh();
                pb.Refresh();
                EditorUtility.SetDirty(pb);

                log.AppendLine($"  {pb.name,-26} 정점 {pos.Count,4}개 중 {moved,4}개 이동");
            }

            log.Insert(0, $"흔들기 세기 {jitterAmount:0.###} · 스냅 {jitterSnap:0.###} · 시드 {seed}\n\n");
            log.AppendLine();
            log.AppendLine("UV 는 월드 투영이라 자동으로 따라간다. 밀도가 어긋나 보이면 UV 도구를 한 번 돌릴 것.");
            report = log.ToString();
        }

        static bool OnOutline(Vector3 p, Vector3 min, Vector3 max, float eps) =>
            Mathf.Abs(p.x - min.x) < eps || Mathf.Abs(p.x - max.x) < eps ||
            Mathf.Abs(p.z - min.z) < eps || Mathf.Abs(p.z - max.z) < eps;

        float Snap(float v)
        {
            if (jitterSnap <= 0.0001f) return v;
            return Mathf.Round(v / jitterSnap) * jitterSnap;
        }

        /// <summary>
        /// 좌표에서 바로 유도하는 해시. 같은 위치 → 같은 값이라 용접이 풀리지 않는다.
        /// ProBuilder 정점은 면마다 분리돼 있어서(육면체 하나에 24개) 이 성질이 반드시 필요하다.
        /// </summary>
        static float Hash(Vector3 p, int seed, int channel)
        {
            unchecked
            {
                int x = Mathf.RoundToInt(p.x * 1000f);
                int y = Mathf.RoundToInt(p.y * 1000f);
                int z = Mathf.RoundToInt(p.z * 1000f);

                int h = 17;
                h = h * 31 + x;
                h = h * 31 + y;
                h = h * 31 + z;
                h = h * 31 + seed;
                h = h * 31 + channel;

                h ^= h >> 15; h *= (int)0x2c1b3c6d;
                h ^= h >> 12; h *= (int)0x297a2d39;
                h ^= h >> 15;
                return (h & 0x7fffffff) / 2147483647f;
            }
        }
    }
}
