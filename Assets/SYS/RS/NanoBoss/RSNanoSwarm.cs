// RE:AL STEEL - 나노봇 군집 (런타임 핵심)
//
// 작은 봇 수만 개를 한 덩어리로 다룬다. 봇마다 GameObject 가 없고,
// 위치 · 상태는 배열에, 계산은 Burst 잡(RSNanoSim)에, 그리기는 Graphics.RenderPrimitives 한 번이다.
//
// 2D 모래 (11-10) — 배경은 3D, 나노봇은 2D 픽셀 판 위의 알갱이
//   · 판 2장: 앞판(이 오브젝트 z) · 뒷판(z + 뒷판 깊이). 봇은 판 사이를 3D 로 날아 오간다
//   · 판 위의 봇은 칸(알갱이 크기 × 1/32 m)에 딱 맞춰 그린다. 회전 · 원근 · 겹침 없음 → 촘촘한 덩어리
//   · 맞아서 떨어진 봇 = 모래: 칸 규칙으로 떨어지고 · 쌓이고 · 흘러내린다 (유체에서 떨어진 봇은 옆으로 퍼짐)
//   · 실루엣 가장자리(위 · 아래 · 왼 · 오른 칸이 빔)를 매 프레임 찾아 조명 · 3페 흰 테두리에 쓴다
//
// 봇의 상태
//   Bound   가이드에 붙어 있다 (덩어리 · 줄기 · 포신 · 기사 · 후광 · 리그). 자리를 감쇠 스프링으로 쫓는다
//   Free    투사체. hold 자리에서 wait 동안 대기(대열 예고) → vel 로 날아감 → life 가 끝나면 home 가이드로 복귀
//   Debris  맞아서 떨어졌다. 날다가 바닥 · 모래 더미에 닿으면 모래 칸이 된다 (질량에서 빠짐)
//   Dead    숨김
//
// 보스 '체력' = 질량 = Bound + Free 봇 수. 패턴 · 페이즈는 RSNanoBossBrain 이 정한다.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealSteel.NanoBoss
{
    public enum BotMode : byte { Bound, Free, Debris, Dead }

    public struct NanoBot
    {
        public Vector3 pos, vel;
        public BotMode mode;
        public int guide;          // Bound: 붙은 가이드
        public int home;           // Free 가 끝나면 돌아갈 가이드
        public float wait;         // Bound: 출발 대기 / Free: 대열에서 기다리는 시간
        public Vector3 hold;       // Free: 대기 자리
        public Vector3 accel;      // Free: 가속
        public Vector3 launch;     // Free: 대기가 끝나면 받을 속도
        public float life;         // Free: 남은 비행 시간

        public int slot;           // 리그: 부위 번호
        public float u;            // 줄기 · 포신 좌표
        public Vector3 v;
        public Vector3 blobV;      // 유체일 때의 자리 (단위 구 안, 영구)
        public int seg;            // 기사 조각 (-1 = 눈)
        public float segU;
        public Vector2 segV;

        public float roll, spin, scale, flash;
        public float glow;         // 개별 발광 (반짝이 봇)
        public float rim;          // 3페 흰 테두리
        public Color32 tint;
        public byte plane;         // 0 앞판 · 1 뒷판 (목표 판)
        public byte liquid;        // 모래가 돼도 옆으로 퍼진다 (유체에서 떨어짐)
        public byte open;          // 비어 있는 이웃 칸 (1 위 · 2 아래 · 4 왼 · 8 오른) — 그리기 잡이 매 프레임 채움
        public int sand;           // 모래 칸 번호 (-1 = 아직 날고 있음)
        public float hash, hash2, hash3;

        public int cell;           // 격자 자리 (덩어리 · 리그 부위). -1 = 없음
        public float edge;         // 실루엣 가장자리 정도 0~1
        public float stretch;      // 속도 방향 늘이기
        public float machine;      // 기계(결속) 정도 0~1
        public float space;        // 자리 간격 (m)
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    [AddComponentMenu("RE_AL STEEL/나노봇 군집")]
    public partial class RSNanoSwarm : MonoBehaviour
    {
        public const string ShaderName = "RE_AL STEEL/NanoBot Pixel Lit";
        public const int G_Blob = 0, G_TubeA = 1, G_TubeB = 2, G_Ring = 3, G_Knight = 4, G_Halo = 5, G_Rig = 6;
        public const int GuideCount = 7;
        const int MaxCells = 128 * 128;

        [Header("봇")]
        [Tooltip("봇 수. 1px 알갱이면 봇 하나 = 픽셀 하나라 덩어리가 꽤 작다 (16,000 ≈ 지름 4m)")]
        [Range(500, 60000)] public int botCount = 16000;

        [Tooltip("알갱이 크기 (px). 1 = 진짜 모래, 2 · 3 = 알갱이가 보임. 픽셀 밀도는 32px/m (게임 기준)")]
        [Range(1, 3)] public int grain = 1;

        [Tooltip("픽셀 밀도: 8px 의 크기 (m). 8 ÷ 32px/m = 0.25 (게임 기준)")]
        public float botWorldSize = 0.25f;

        [Tooltip("줄기 · 덩어리 · 기사처럼 계산으로 만드는 모양을 채우는 촘촘함. 1 = 칸마다 봇 하나 (곡선 · 기울기에서 구멍이 보일 수 있음), 클수록 빈틈이 없고 봇이 더 든다")]
        [Range(1f, 2f)] public float fill = 1.3f;

        [Tooltip("탄 하나를 이루는 봇 수. 탄을 쏘면 같은 곳의 봇이 이만큼 뭉쳐 한 덩어리로 날아간다 (1 = 봇 하나씩 · 듬성듬성)")]
        [Range(1, 64)] public int bulletClump = 24;
        [Tooltip("날아가는 봇 하나를 그리는 크기 (칸). 탄이 뭉치므로 보통 1")]
        [Range(1, 4)] public int freeSize = 1;

        [Header("봇 색")]
        [Tooltip("한 줄 = 색 + 비율. 봇마다 색은 끝까지 같다. 비우면 기본 금속 색 3단.\n그림 부위의 '봇 색 = 그림 색' 이 켜져 있으면 그림 칸에 도착한 봇은 그 칸 색이 된다")]
        public List<NanoBotColor> botColors = new List<NanoBotColor>();
        [Tooltip("알갱이마다 밝기 차이 (모래 · 금속 가루 결)")]
        [Range(0f, 0.3f)] public float grainNoise = 0.08f;

        [Tooltip("반짝이는 봇 비율")]
        [Range(0f, 0.2f)] public float sparkleRatio = 0.04f;

        public Color32 sparkleTint = new Color32(110, 225, 255, 255);

        [Header("판 (2D)")]
        [Tooltip("뒷판이 앞판보다 화면 안쪽으로 얼마나 (m). 이 깊이의 절반보다 안쪽을 노리는 모양은 뒷판에 그린다")]
        public float backDepth = 9f;
        [Tooltip("뒷판 봇 밝기 (멀리 있어 어둡게)")]
        [Range(0.2f, 1f)] public float backDim = 0.6f;
        [Tooltip("모래가 쌓이는 범위 — 보스 시작 위치 중심 너비 · 바닥부터 높이 (m). 밖으로 나간 모래는 그냥 바닥에 멈춘다")]
        public Vector2 sandField = new Vector2(64f, 18f);

        [Header("모래")]
        [Tooltip("맞아서 빈 자리를 다시 채우기 전까지 (초). 이동안 구멍이 보인다")]
        [Range(0f, 2f)] public float refillDelay = 0.35f;
        [Tooltip("유체일 때 주변 봇과 속도를 맞추는 세기 (끈적임)")]
        [Range(0f, 30f)] public float viscosity = 8f;
        [Tooltip("모래가 비탈을 미끄러지는 확률 (프레임마다). 낮을수록 가파르게 쌓인다")]
        [Range(0f, 1f)] public float slide = 0.6f;

        [Header("바닥")]
        [Tooltip("시작할 때 이 오브젝트 높이를 바닥으로 쓴다")]
        public bool floorFromTransform = true;
        public float floorY;
        public float gravity = 28f;

        [Header("3페 색")]
        [Tooltip("0 = 금속 봇 / 1 = 검은 실루엣")]
        [Range(0f, 1f)] public float dark;
        [Tooltip("검게 될 때 남는 밝기")]
        [Range(0f, 0.3f)] public float darkTint = 0.05f;
        [ColorUsage(false, true)] public Color rimColor = new Color(1.6f, 1.6f, 1.7f);

        [Header("그리기")]
        public Shader shader;
        [Tooltip("비우면 셰이더로 자동 생성")]
        public Material material;
        public bool castShadows = true;
        public bool receiveShadows = true;
        public Color flashColor = Color.white;
        public float flashDecay = 5f;

        [Header("날아가는 봇")]
        [Tooltip("투사체 봇의 추가 발광 (빔 등). 패턴이 바꾼다")]
        public float freeGlow;
        [NonSerialized] public bool highlightDanger;   // 편집기 미리보기: 위험한 봇을 주황으로
        public Color32 freeTint = new Color32(255, 255, 255, 255);

        // ── 가이드 ───────────────────────────────────────────────────
        public readonly BlobGuide Blob = new BlobGuide();
        public readonly TubeGuide TubeA = new TubeGuide("줄기 A");
        public readonly TubeGuide TubeB = new TubeGuide("줄기 B");
        public readonly RingGuide Ring = new RingGuide();
        public readonly KnightGuide Knight = new KnightGuide();
        public readonly HaloGuide Halo = new HaloGuide();
        public readonly RigGuide Rig = new RigGuide();
        SwarmGuide[] guides;
        SwarmGuide[] Guides => guides ??= new SwarmGuide[] { Blob, TubeA, TubeB, Ring, Knight, Halo, Rig };
        public SwarmGuide GuideAt(int g) => Guides[g];

        // ── 상태 ─────────────────────────────────────────────────────
        NanoBot[] bots = Array.Empty<NanoBot>();
        public NanoBot[] Bots => bots;
        public int Count => bots.Length;
        public readonly int[] guideCount = new int[GuideCount];
        public int FreeCount { get; private set; }
        public int DebrisCount { get; private set; }
        public int Alive { get; private set; }

        /// <summary>시뮬 시간 (ms, 부드럽게) — HUD</summary>
        public float SimMs { get; private set; }
        public float UploadMs { get; private set; }
        /// <summary>덩어리 격자 간격 (m) — 지금 덩어리 크기에서</summary>
        public float BlobSpace { get; private set; }

        /// <summary>칸 한 변 (m) = 알갱이 크기 × 픽셀 크기</summary>
        public float Cell => Mathf.Max(1, grain) * botWorldSize / 8f;
        /// <summary>계산으로 만드는 모양의 자리 간격 (m) — 칸보다 조금 촘촘하게 ('촘촘함')</summary>
        public float spacing => Cell / Mathf.Sqrt(Mathf.Max(1f, fill));
        public float FrontZ => transform.position.z;
        public float BackZ => transform.position.z + backDepth;

        // 덩어리 격자
        Vector4[] blobTable = Array.Empty<Vector4>();
        float4[] blobSlots = new float4[1];
        float blobAspectBuilt = -1f;
        readonly NanoSlotSet blobSet = new NanoSlotSet();
        float blobExt;

        // 잡 데이터 (관리 배열을 고정해 포인터로 넘긴다)
        readonly GuideFeel[] feel = new GuideFeel[GuideCount];
        readonly float3[] tubeNodes = new float3[TubeGuide.K * 2];
        readonly float4[] knightSegs = new float4[NanoSimJob.KnightSegs];
        readonly float2[] knightRad = new float2[NanoSimJob.KnightSegs];
        int[] cellStart = new int[MaxCells], cellCount = new int[MaxCells], cellFill = new int[MaxCells];
        float4[] cellVel = new float4[MaxCells];
        int[] sortedIdx = new int[1];
        float2[] sortedPos = new float2[1];
        readonly GridInfo[] gridInfo = new GridInfo[1];
        static readonly LimbSim[] NoLimbs = new LimbSim[1];
        static readonly float4[] NoSlots = new float4[1];
        static readonly float4[] NoSpine = new float4[NanoSimJob.S * NanoSimJob.H];
        static readonly float[] NoSpineTime = new float[NanoSimJob.H];
        static readonly uint[] NoColors = new uint[1];

        NanoGpuBot[] gpu = Array.Empty<NanoGpuBot>();
        readonly float3[] boundsOut = new float3[2];
        GraphicsBuffer buffer;
        MaterialPropertyBlock mpb;
        Material runtimeMaterial;
        // 모래 · 판 칸 (앞판 · 뒷판, 고정 범위)
        byte[] sandOcc = new byte[1];
        uint[] occBits = new uint[1];
        float fieldMinX, fieldMinY;
        int fieldW, fieldH;
        int colorSig;
        Bounds bounds;
        int uploadedFrame = -1;
        System.Random rng = new System.Random(7);
        float simClock;
        int simFrame;

        static class Ids
        {
            public static readonly int Bots = Shader.PropertyToID("_Bots");
            public static readonly int BossFlash = Shader.PropertyToID("_BossFlashColor");
            public static readonly int Dark = Shader.PropertyToID("_Dark");
            public static readonly int DarkTint = Shader.PropertyToID("_DarkTint");
            public static readonly int RimColor = Shader.PropertyToID("_RimColor");
        }

        // ─────────────────────────────────────────────────────────────
        // 수명
        // ─────────────────────────────────────────────────────────────
        void Reset() { shader = Shader.Find(ShaderName); }

        void Awake()
        {
            if (!Application.isPlaying) return;   // 에디터에서는 리그 미리보기가 켤 때만 봇을 만든다
            if (floorFromTransform) floorY = transform.position.y;
            Build(transform.position);
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            if (glitchAura != null && !glitchAura.shader) glitchAura.shader = Shader.Find(AuraShaderName);   // 빌드에도 들어가게 씬에 기록
#endif
            if (bots.Length > 0 && RSNanoBotColorsSig() != colorSig) RecolorAll();
        }

        int RSNanoBotColorsSig()
        {
            unchecked
            {
                int h = 17 + grainNoise.GetHashCode() * 7 + sparkleRatio.GetHashCode() * 13;
                if (botColors != null) foreach (var c in botColors) if (c != null) h = h * 31 + c.color.GetHashCode() * 3 + c.ratio.GetHashCode();
                return h;
            }
        }

        /// <summary>봇 색 목록을 다시 칠한다 (목록을 고쳤을 때)</summary>
        public void RecolorAll()
        {
            colorSig = RSNanoBotColorsSig();
            for (int i = 0; i < bots.Length; i++)
            {
                if (bots[i].seg == -1) continue;   // 기사 눈
                bool sparkle = bots[i].hash3 < sparkleRatio;
                bots[i].glow = sparkle ? 1.2f + bots[i].hash2 : 0f;
                bots[i].tint = sparkle ? sparkleTint : RSNanoBotColors.Pick(botColors, i, grainNoise);
            }
            dirty = true;
        }

        /// <summary>모래 · 판 칸 범위를 정한다 (Build 때)</summary>
        void SetupField(float centerX)
        {
            float c = Cell;
            fieldW = Mathf.Clamp(Mathf.CeilToInt(sandField.x / c), 16, 8192);
            fieldH = Mathf.Clamp(Mathf.CeilToInt(sandField.y / c), 16, 4096);
            fieldMinX = Mathf.Floor((centerX - fieldW * c * 0.5f) / c) * c;
            fieldMinY = floorY;
            int cells = fieldW * fieldH * 2;
            if (sandOcc.Length != cells) sandOcc = new byte[cells]; else Array.Clear(sandOcc, 0, cells);
            int words = (cells + 31) / 32;
            if (occBits.Length != words) occBits = new uint[words];
        }

        /// <summary>봇을 모두 없앤다 (에디터 미리보기 끝)</summary>
        public void ClearBots()
        {
            bots = Array.Empty<NanoBot>();
            gpu = Array.Empty<NanoGpuBot>();
            buffer?.Release();
            buffer = null;
            Recount();
        }

        /// <summary>에디터 미리보기용 — 난수 씨앗을 고정해 매번 같은 결과</summary>
        public void ResetRandom(int seed) { rng = new System.Random(seed); }

        void OnEnable() { RenderPipelineManager.beginCameraRendering += OnBeginCamera; }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            buffer?.Release();
            buffer = null;
        }

        void OnDestroy()
        {
            if (runtimeMaterial) { if (Application.isPlaying) Destroy(runtimeMaterial); else DestroyImmediate(runtimeMaterial); }
            DestroyAura();
        }

        float R() => (float)rng.NextDouble();

        /// <summary>봇을 새로 만든다. 전부 덩어리에 붙은 상태</summary>
        public void Build(Vector3 blobCenter)
        {
            int n = Mathf.Max(1, botCount);
            if (floorFromTransform && !Application.isPlaying) floorY = transform.position.y;
            SetupField(blobCenter.x);
            bots = new NanoBot[n];
            gpu = new NanoGpuBot[n];
            sortedIdx = new int[n];
            sortedPos = new float2[n];
            Blob.floorY = floorY;
            Blob.center = blobCenter;
            for (int i = 0; i < n; i++)
            {
                Vector3 v;
                do v = new Vector3(R() * 2f - 1f, R() * 2f - 1f, 0f); while (v.sqrMagnitude > 1f);
                bots[i] = new NanoBot
                {
                    mode = BotMode.Bound,
                    guide = G_Blob,
                    home = G_Blob,
                    blobV = v,
                    seg = 0,
                    cell = -1,
                    scale = 1f,
                    hash = R(), hash2 = R(), hash3 = R(),
                    space = spacing,
                    sand = -1,
                };
                bots[i].pos = blobCenter + Vector3.Scale(v, Blob.radius);
                bots[i].pos.z = FrontZ;
            }
            RecolorAll();
            // 덩어리 자리표는 봇 수 · 비율이 같으면 그대로 쓰고 주인만 비운다 (편집기 되감기 재시뮬이 자주 부른다)
            if (blobTable.Length == n && Mathf.Abs(blobAspectBuilt - Blob.aspect) <= 0.01f) blobSet.Resize(n);
            else blobAspectBuilt = -1f;
            buffer?.Release();
            buffer = null;
            Recount();
        }

        // ─────────────────────────────────────────────────────────────
        // 시뮬레이션
        // ─────────────────────────────────────────────────────────────
        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            Simulate(Time.deltaTime, Time.time);
        }

        /// <summary>시간을 흘려 봇을 자리로 보낸다 (미리보기 준비)</summary>
        public void Settle(float seconds)
        {
            for (float t = 0f; t < seconds; t += 1f / 60f) Simulate(1f / 60f, simClock + 1f / 60f);
        }

        /// <summary>한 프레임 시뮬레이션. 플레이 중엔 LateUpdate 가, 에디터 미리보기에선 편집기가 부른다</summary>
        public unsafe void Simulate(float dt, float time)
        {
            if (dt <= 0f || bots.Length == 0) return;
            long t0 = Stopwatch.GetTimestamp();
            dt = Mathf.Min(dt, 0.05f);
            simClock = time;
            Blob.floorY = floorY;
            Knight.UpdatePose();
            MaintainBlob(time);
            FillKnight(dt);
            UpdateAura(dt, time);
            FitTube(TubeA, G_TubeA);
            FitTube(TubeB, G_TubeB);
            FillGuides();

            var rig = Rig.rig;
            bool rigOn = rig && rig.Active && rig.SimLimbCount > 0;
            LimbSim[] lsim = rigOn ? rig.SimLimbs : NoLimbs;
            float4[] lslots = rigOn ? rig.SimSlots : NoSlots;
            float4[] lspine = rigOn ? rig.SimSpine : NoSpine;
            float[] lspineT = rigOn ? rig.SimSpineTime : NoSpineTime;
            uint[] lcolors = rigOn ? rig.SimSlotColors : NoColors;
            if (sortedIdx.Length < bots.Length) { sortedIdx = new int[bots.Length]; sortedPos = new float2[bots.Length]; }

            var pins = new Pins();
            var pBots = (NanoBot*)pins.Pin(bots);
            var pCellStart = (int*)pins.Pin(cellStart);
            var pCellCount = (int*)pins.Pin(cellCount);
            var pCellFill = (int*)pins.Pin(cellFill);
            var pSorted = (int*)pins.Pin(sortedIdx);
            var pSortedPos = (float2*)pins.Pin(sortedPos);
            var pCellVel = (float4*)pins.Pin(cellVel);
            var pGrid = (GridInfo*)pins.Pin(gridInfo);
            try
            {
                // 끈적임용 격자 — 유체(결속 < 1)가 하나도 없으면 건너뛴다
                bool fluidAny = false;
                for (int g = 0; g < GuideCount && !fluidAny; g++) if (guideCount[g] > 0 && g != G_Rig && feel[g].bind < 0.99f) fluidAny = true;
                for (int li = 0; rigOn && li < rig.SimLimbCount && !fluidAny; li++) if (lsim[li].bind < 0.99f) fluidAny = true;
                if (!fluidAny || viscosity <= 0f) gridInfo[0] = default;
                else
                new NanoGridJob
                {
                    bots = pBots, cellStart = pCellStart, cellCount = pCellCount, cellFill = pCellFill,
                    sorted = pSorted, sortedPos = pSortedPos, cellVel = pCellVel, info = pGrid,
                    count = bots.Length, maxCells = MaxCells, baseCell = Mathf.Max(0.12f, Cell * 3f),
                }.Run();

                var job = new NanoSimJob
                {
                    bots = pBots,
                    feel = (GuideFeel*)pins.Pin(feel),
                    blobSlots = (float4*)pins.Pin(blobSlots),
                    tubeNodes = (float3*)pins.Pin(tubeNodes),
                    knightSegs = (float4*)pins.Pin(knightSegs),
                    knightRad = (float2*)pins.Pin(knightRad),
                    limbs = (LimbSim*)pins.Pin(lsim),
                    limbSlots = (float4*)pins.Pin(lslots),
                    spine = (float4*)pins.Pin(lspine),
                    spineTime = (float*)pins.Pin(lspineT),
                    cellStart = pCellStart, cellCount = pCellCount, sorted = pSorted, sortedPos = pSortedPos, cellVel = pCellVel,
                    grid = gridInfo[0],
                    blob = new BlobSim
                    {
                        center = Blob.center, radius = Blob.radius, bulgeDir = Blob.bulgeDir,
                        wobble = Blob.wobble, wobbleSpeed = Blob.wobbleSpeed, flow = Blob.flow, flowSpeed = Blob.flowSpeed,
                        bulge = Blob.bulge, floorY = Blob.floorY, ext = blobExt,
                    },
                    tubeA = new TubeSim { r0 = TubeA.er0, r1 = TubeA.er1 },
                    tubeB = new TubeSim { r0 = TubeB.er0, r1 = TubeB.er1 },
                    ring = new RingSim { center = Ring.center, axis = Ring.axis, radius = Ring.radius, length = Ring.length, angle = Ring.angle },
                    knight = new KnightSim
                    {
                        origin = Knight.origin, lookAt = Knight.lookAt, head = Knight.HeadWorld, facing = Knight.facing,
                        sprite = knightSprite ? 1 : 0, slotCount = knightHigh + 1,
                        art = knightSprite && !Knight.moves.darkSilhouette ? 1 : 0,
                        bladeGlow = knightSprite ? Knight.moves.bladeGlow : 0f, fxGlow = knightSprite ? Knight.moves.fxGlow : 0f,
                        eyeSpread = knightSprite ? Cell * Knight.Scale * 1.2f : 0f,
                    },
                    knightSlots = (float4*)pins.Pin(kSlots),
                    knightColors = (uint*)pins.Pin(kColors),
                    halo = new HaloSim { center = Halo.center, radius = Halo.radius, spin = Halo.spin },
                    limbCount = rigOn ? rig.SimLimbCount : 0,
                    blobSlotCount = blobTable.Length,
                    frame = ++simFrame,
                    dt = dt, time = time, clock = rigOn ? rig.Clock : 0f, floorY = floorY,
                    gravity = gravity, flashDecay = flashDecay,
                    viscosity = viscosity, dark = dark, spacing = spacing, cell = Cell,
                    frontZ = FrontZ, backZ = BackZ,
                    limbSlotColor = (uint*)pins.Pin(lcolors),
                };
                job.Schedule(bots.Length, 64).Complete();

                new NanoSandJob
                {
                    bots = pBots,
                    occ = (byte*)pins.Pin(sandOcc),
                    count = bots.Length,
                    w = fieldW, h = fieldH, minX = fieldMinX, minY = fieldMinY, cell = Cell,
                    frontZ = FrontZ, backZ = BackZ,
                    dt = dt, frame = simFrame, slide = slide,
                }.Run();
            }
            finally { pins.Release(); }

            Recount();
            dirty = true;
            float ms = (float)((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
            SimMs = SimMs <= 0f ? ms : Mathf.Lerp(SimMs, ms, 0.1f);
        }
        bool dirty;

        /// <summary>관리 배열을 고정해 포인터로 넘긴다 (GC 가 옮기지 못하게). 잡이 끝나면 푼다</summary>
        /// <summary>관리 배열을 잡 동안 고정한다 (최대 32개)</summary>
        unsafe struct Pins
        {
            fixed ulong h[32];
            int n;

            public void* Pin(Array a)
            {
                if (n >= 32) throw new InvalidOperationException("Pins: 너무 많다");
                void* p = UnsafeUtility.PinGCArrayAndGetDataAddress(a, out ulong hh);
                h[n++] = hh;
                return p;
            }

            public void Release()
            {
                for (int i = 0; i < n; i++) UnsafeUtility.ReleaseGCObject(h[i]);
                n = 0;
            }
        }

        /// <summary>덩어리 격자 자리 정리 — 빈자리 채우기 · 다지기. 덩어리 크기는 붙은 봇 수에 맞춘다</summary>
        void MaintainBlob(float time)
        {
            int n = bots.Length;
            if (blobTable.Length != n || Mathf.Abs(blobAspectBuilt - Blob.aspect) > 0.01f)
            {
                blobTable = NanoLattice.EllipseHex(n, Blob.aspect);   // 정사각 격자 (2D 칸)
                blobAspectBuilt = Blob.aspect;
                if (blobSlots.Length != n) blobSlots = new float4[n];
                for (int k = 0; k < n; k++) blobSlots[k] = blobTable[k];
                blobSet.Resize(n);
            }
            int occ = blobSet.Maintain(bots, null, G_Blob, -1, time, refillDelay, Mathf.Max(4, n / 60), null, blobTable);
            blobExt = occ > 0 ? blobTable[Mathf.Min(occ, n) - 1].w + 0.5f : 0f;
            BlobSpace = blobExt > 0f ? Blob.radius.y / blobExt : spacing;
        }

        // ── 그림 기사 (3페) ──────────────────────────────────────────
        float4[] kSlots = new float4[1];
        uint[] kColors = new uint[1];
        int[] kOwner = Array.Empty<int>();
        readonly List<int> kNeed = new List<int>();
        readonly List<int> kTmp = new List<int>();
        bool knightSprite;
        int knightHigh = -1;
        /// <summary>그림 기사가 한 프레임에 후광에서 데려오는 최대 봇 수</summary>
        public int knightRefillPerFrame = 3000;

        /// <summary>그림 기사 봇의 지금 자리 종류 (0 몸 · 1 칼날 · 2 효과, -1 없음)</summary>
        public int KnightKind(int i)
        {
            int c = bots[i].cell;
            return knightSprite && c >= 0 && c <= knightHigh && c < kSlots.Length ? Mathf.RoundToInt(kSlots[c].w) : -1;
        }

        /// <summary>
        /// 그림 기사 — 봇마다 고정 번호(cell). 프레임이 바뀌면 봇이 가장 덜 움직이게 새 칸을 맡긴다 (NanoKnightFill):
        /// 그대로 있는 칸은 그 봇이 그대로, 바뀐 곳만 가까운 봇이 옮겨 간다 → 동작 중에도 몸이 안 깨진다.
        /// 맞아서 떨어진 만큼은 후광에서 데려오고, 남으면 돌려보낸다. 봇이 모자라면 몸 칸을 고르게 솎는다 (점묘)
        /// </summary>
        float kHold;
        readonly NanoKnightFill kFill = new NanoKnightFill();
        int[] kList = new int[64], kHintX = new int[64], kHintY = new int[64];
        int kLastMove = -1, kLastFrame = -1, kLastCount = -1, kLastS = -1;
        float kLastFacing, kLastOx, kLastOy;
        int kMaxBody, kMaxBodyS = -1;
        RSNanoKnightMoves kMaxBodyMoves;
        /// <summary>그림 기사 짝짓기 결과 (점검용) — 칸 · 그대로 · 가까이 · 곡선 · 겹침</summary>
        public string KnightFillInfo => $"칸 {kFill.lastTargets:N0} · 그대로 {kFill.lastKept:N0} · 가까이 {kFill.lastNear:N0} · 곡선 {kFill.lastCurve:N0} · 겹침 {kFill.lastStacked:N0}";

        void FillKnight(float dt)
        {
            var k = Knight;
            k.cell = Cell;
            knightSprite = k.SpriteMode;
            knightHigh = -1;
            if (!knightSprite) { if (kLastMove >= 0) kFill.Clear(); kLastMove = -1; return; }
            var clip = k.Clip;
            if (clip == null || !clip.Ready) { knightSprite = false; return; }
            var mv = k.moves;
            int f = Mathf.Clamp(k.frame, 0, clip.frames - 1);
            int nb = clip.Body(f), nl = clip.Blade(f), nf = clip.Fx(f);
            int S = k.Scale, S2 = S * S;
            int nBB = nb + nl;
            int n = bots.Length;
            if (kOwner.Length != n) { kOwner = new int[n]; kSlots = new float4[Mathf.Max(1, n)]; kColors = new uint[Mathf.Max(1, n)]; kFill.Ensure(n); kFill.Clear(); }
            for (int c = 0; c < n; c++) kOwner[c] = -1;
            kNeed.Clear();
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                ref NanoBot b = ref bots[i];
                if (b.mode != BotMode.Bound || b.guide != G_Knight || b.seg < 0) continue;
                count++;
                int c = b.cell;
                if (c >= 0 && c < n && kOwner[c] < 0) { kOwner[c] = i; if (c > knightHigh) knightHigh = c; }
                else kNeed.Add(i);
            }
            bool joined = kNeed.Count > 0;
            int cur = 0;
            foreach (int i in kNeed)
            {
                while (cur < n && kOwner[cur] >= 0) cur++;
                if (cur >= n) break;
                kOwner[cur] = i; bots[i].cell = cur;
                kFill.Forget(cur);                   // 새로 온 봇 — 지금 위치에서 가까운 칸을 찾는다
                if (cur > knightHigh) knightHigh = cur;
            }
            // 모자라면 후광에서 데려온다 — 몸 · 칼날은 꽉, 검기 · 효과는 1/3 (가루처럼 성기게).
            // 가장 큰 프레임의 몸만큼은 늘 쥐고 있는다 (큰 동작이 시작될 때 데려오느라 몸이 숭숭 비지 않게)
            if (kMaxBodyS != S || kMaxBodyMoves != mv) { kMaxBody = mv.MaxBodyBots(S); kMaxBodyS = S; kMaxBodyMoves = mv; }
            int want = Mathf.Max(nBB * S2 + nf * Mathf.Max(1, S2 / 3), kMaxBody);
            kHold = Mathf.Max(want, kHold - dt * 6000f);   // 베기가 끝난 뒤 잠시 그대로 (바로 흩어지면 깜빡인다)
            if (k.reinforce && count < want && guideCount[G_Halo] > 0)
            {
                Collect(G_Halo, k.ChestWorld, Mathf.Min(want - count, knightRefillPerFrame), kTmp);
                foreach (int i in kTmp)
                {
                    bots[i].seg = 0;
                    bots[i].cell = -1;
                    bots[i].rim = 0f;
                    Bind(i, G_Knight);
                }
            }
            // 남는 봇은 후광(탄약)으로 돌려보낸다 — 번호가 큰 것부터
            if (k.reinforce && count > kHold + 64)
            {
                int give = Mathf.Min(count - Mathf.CeilToInt(kHold), knightRefillPerFrame / 2);
                for (int c = knightHigh; c >= 0 && give > 0; c--)
                {
                    int i = kOwner[c];
                    if (i < 0) continue;
                    kOwner[c] = -1;
                    kFill.Forget(c);
                    SendHome(i, G_Halo);
                    give--; joined = true;
                }
                while (knightHigh >= 0 && kOwner[knightHigh] < 0) knightHigh--;
            }

            // 칸 눈금 (모래 · 그리기 눈금과 같게 — 칸 경계에 걸리면 봇이 두 칸 사이를 오가며 구멍이 난다)
            float cs = Cell, fc = k.facing >= 0f ? 1f : -1f;
            Vector3 o = k.origin;
            float ox = fieldMinX + Mathf.Round((o.x - fieldMinX) / cs) * cs;
            float oy = fieldMinY + Mathf.Round((o.y - fieldMinY) / cs) * cs;

            // 몸이 움직인 만큼 봇도 같이 옮긴다 (스프링이 따라가며 뒤처지면 달릴 때 몸이 뒤로 쏠리고 끝이 흩어진다)
            if (kLastMove >= 0)
            {
                Vector2 d = new Vector2(ox - kLastOx, oy - kLastOy);
                if (d.sqrMagnitude > 0f && d.sqrMagnitude < 4f)
                    for (int c = 0; c <= knightHigh; c++)
                    {
                        int i = kOwner[c];
                        if (i >= 0) { bots[i].pos.x += d.x; bots[i].pos.y += d.y; }
                    }
            }
            kLastOx = ox; kLastOy = oy;

            // 짝짓기 — 프레임 · 봇 수 · 배율 · 방향이 바뀌었을 때만
            int live = 0;
            for (int c = 0; c <= knightHigh; c++) if (kOwner[c] >= 0) live++;
            bool flip = kLastMove >= 0 && fc != kLastFacing;
            if (flip) kFill.Mirror();
            if (joined || flip || (int)k.move != kLastMove || f != kLastFrame || live != kLastCount || S != kLastS)
            {
                if (kList.Length < live) { kList = new int[live]; kHintX = new int[live]; kHintY = new int[live]; }
                int j = 0;
                for (int c = 0; c <= knightHigh; c++)
                {
                    int i = kOwner[c];
                    if (i < 0) { kFill.Forget(c); continue; }
                    kList[j] = c;
                    Vector3 p = bots[i].pos;
                    kHintX[j] = Mathf.RoundToInt((p.x - ox) * fc / cs - 0.5f);
                    kHintY[j] = Mathf.RoundToInt((p.y - oy) / cs - 0.5f);
                    j++;
                }
                kFill.Match(clip, f, S, mv.cellSize.x, mv.cellSize.y, mv.root.x, mv.root.y, kList, live, kHintX, kHintY);
                kLastMove = (int)k.move; kLastFrame = f; kLastCount = live; kLastS = S;
            }
            kLastFacing = fc;

            // 칸 → 월드 (매 프레임 — 기사가 움직이면 몸 전체가 같이)
            Vector3 chest = k.ChestWorld;
            for (int c = 0; c <= knightHigh; c++)
            {
                if (kOwner[c] < 0) continue;
                if (kFill.has[c] == 0) { kSlots[c] = new float4(chest.x, chest.y, o.z, 0f); kColors[c] = 0xFF202020u; continue; }
                kSlots[c] = new float4(ox + fc * (kFill.locX[c] + 0.5f) * cs, oy + (kFill.locY[c] + 0.5f) * cs, o.z, kFill.kind[c]);
                kColors[c] = kFill.color[c];
            }
            NoteAuraFrame(clip, f, S, ox, oy, fc, o.z, cs);
        }

        readonly List<int> tubeTmp = new List<int>();
        float[] tubeKeys = Array.Empty<float>();
        int[] tubeIdx = Array.Empty<int>();

        /// <summary>
        /// 줄기를 칸 간격으로 빈틈 없이 — 봇이 모자라면 두께를 줄이고(듬성듬성해지지 않게),
        /// 길이가 15% 넘게 바뀌거나 봇 수가 바뀌면 지금 길이로 자리표를 다시 깐다 (뿌리부터 순서는 그대로라 크게 안 움직인다)
        /// </summary>
        void FitTube(TubeGuide t, int g)
        {
            int n = guideCount[g];
            float sp = spacing;
            float len = t.Length();
            float need = len * (t.r0 + t.r1) / (sp * sp);
            float k = need > n && need > 0f ? n / need : 1f;
            t.er0 = Mathf.Max(sp * 1.5f, t.r0 * k);
            t.er1 = Mathf.Max(sp * 0.5f, t.r1 * k);
            if (n < 8 || len < 0.3f) return;
            if (t.builtN == n && t.builtLen > 0f && Mathf.Abs(len - t.builtLen) < 0.15f * t.builtLen) return;
            tubeTmp.Clear();
            for (int i = 0; i < bots.Length; i++) if (bots[i].mode == BotMode.Bound && bots[i].guide == g) tubeTmp.Add(i);
            int m = tubeTmp.Count;
            if (m < 8) return;
            if (tubeKeys.Length < m) { tubeKeys = new float[m]; tubeIdx = new int[m]; }
            for (int q = 0; q < m; q++) { tubeKeys[q] = bots[tubeTmp[q]].u + Mathf.Abs(bots[tubeTmp[q]].v.x) * 1e-3f; tubeIdx[q] = tubeTmp[q]; }
            Array.Sort(tubeKeys, tubeIdx, 0, m);
            var lat = NanoLattice.Taper(m, t.er0, t.er1, len);
            for (int q = 0; q < m; q++)
            {
                ref NanoBot b = ref bots[tubeIdx[q]];
                b.u = lat[q].x;
                b.v = new Vector3(lat[q].y, 0f, 0f);
            }
            t.builtLen = len;
            t.builtN = n;
        }

        static uint Pack(Color32 c) => (uint)c.r | ((uint)c.g << 8) | ((uint)c.b << 16) | ((uint)c.a << 24);

        void FillGuides()
        {
            for (int g = 0; g < GuideCount; g++)
            {
                var gd = Guides[g];
                NanoSpring.FromStiffness(gd.stiffness, gd.damping, out float om, out float ze);
                feel[g] = new GuideFeel
                {
                    omega = om, zeta = ze, maxSpeed = gd.maxSpeed, noise = gd.noise, noiseSpeed = gd.noiseSpeed,
                    bind = Mathf.Clamp01(gd.bind), space = spacing, glow = gd.glow, tint = Pack(gd.tint),
                    flow = g == G_Blob ? Blob.flow * Mathf.Min(Blob.radius.x, Blob.radius.y) * 0.5f : 0f,
                    gather = g == G_Blob ? om * 3f : 0f,   // 덩어리: 멀리 있는 봇(돌아오는 봇 · 다지기)은 빨리 자리로
                    danger = gd.danger ? 1 : 0,
                    lattice = g == G_Blob || g == G_TubeA || g == G_TubeB || g == G_Knight || g == G_Rig ? 1 : 0,
                };
            }
            for (int k = 0; k < TubeGuide.K; k++)
            {
                tubeNodes[k] = TubeA.nodes[k];
                tubeNodes[TubeGuide.K + k] = TubeB.nodes[k];
            }
            for (int s = 0; s < NanoSimJob.KnightSegs && s < Knight.SegCount; s++)
            {
                knightSegs[s] = Knight.SegWorld(s);
                knightRad[s] = KnightGuide.SegRadius(s);
            }
        }

        void Recount()
        {
            Array.Clear(guideCount, 0, guideCount.Length);
            int free = 0, debris = 0;
            for (int i = 0; i < bots.Length; i++)
            {
                switch (bots[i].mode)
                {
                    case BotMode.Bound: guideCount[bots[i].guide]++; break;
                    case BotMode.Free: free++; break;
                    case BotMode.Debris: debris++; break;
                }
            }
            FreeCount = free;
            DebrisCount = debris;
            int a = free;
            for (int g = 0; g < guideCount.Length; g++) a += guideCount[g];
            Alive = a;
        }

        // ─────────────────────────────────────────────────────────────
        // 공개 API — 패턴이 쓰는 것
        // ─────────────────────────────────────────────────────────────

        /// <summary>봇 n 개가 격자 간격으로 맞물린 덩어리의 반지름 (shape = 가로 : 세로 : 깊이 비율)</summary>
        public Vector3 BlobSize(int n, Vector3 shape)
        {
            float A = shape.x / Mathf.Max(0.01f, shape.y);
            float area = Mathf.Max(1, n) * spacing * spacing;
            float ry = Mathf.Sqrt(area / (Mathf.PI * A));
            return new Vector3(A * ry, ry, ry * shape.z / Mathf.Max(0.01f, shape.y));
        }

        /// <summary>가이드에 붙인다</summary>
        public void Bind(int i, int guide)
        {
            ref NanoBot b = ref bots[i];
            if (b.mode == BotMode.Dead) b.scale = 0f;
            if (b.guide != guide || b.mode != BotMode.Bound) b.cell = -1;
            b.mode = BotMode.Bound;
            b.guide = guide;
            b.wait = 0f;
        }

        /// <summary>
        /// 탄으로 쏜다. hold 자리에서 wait 초 대기(예고) → vel 로 life 초 비행 → home 으로 복귀.
        /// clump = 탄 하나를 이루는 봇 수 (-1 = 군집 '탄 뭉치'). 나머지 봇은 같은 가이드(없으면 후광 → 덩어리)에서 데려와
        /// hold 주위 칸에 촘촘히 붙인다 → 탄이 듬성듬성한 점이 아니라 뭉친 덩어리로 날아간다
        /// </summary>
        public void Fire(int i, Vector3 hold, float wait, Vector3 vel, float life, int home, Vector3 accel = default, float rim = 0f, bool snap = false, int clump = -1)
        {
            int src = bots[i].mode == BotMode.Bound ? bots[i].guide : home;
            FireOne(i, hold, wait, vel, life, home, accel, rim, snap);
            int c = clump < 0 ? bulletClump : clump;
            if (c <= 1) return;
            float cs = Cell;
            for (int k = 1; k < c && k < ClumpOffsets.Length; k++)
            {
                int j = NextIdle(src);
                if (j < 0) break;
                Vector2 o = ClumpOffsets[k];
                // 대기 없이 바로 쏘면 그 자리에 붙여서 (멀리서 따로 날아오면 덩어리가 안 된다)
                FireOne(j, hold + new Vector3(o.x * cs, o.y * cs, 0f), wait, vel, life, home, accel, rim, snap || wait <= 0f);
            }
        }

        /// <summary>탄 뭉치 칸 — 가운데부터 둥글게 (정수 칸)</summary>
        static readonly Vector2[] ClumpOffsets = BuildClump(96);
        static Vector2[] BuildClump(int n)
        {
            var list = new List<Vector2>();
            for (int y = -6; y <= 6; y++) for (int x = -6; x <= 6; x++) list.Add(new Vector2(x, y));
            list.Sort((a, b) => (a.sqrMagnitude + a.x * 0.01f + a.y * 0.001f).CompareTo(b.sqrMagnitude + b.x * 0.01f + b.y * 0.001f));
            return list.GetRange(0, Mathf.Min(n, list.Count)).ToArray();
        }

        int idleCursor;
        /// <summary>탄 뭉치에 데려올 봇 — guide 에 붙어 쉬는 봇 (리그 · 기사 몸이면 후광 → 덩어리에서)</summary>
        int NextIdle(int guide)
        {
            int n = bots.Length;
            if (n == 0) return -1;
            for (int pass = 0; pass < 3; pass++)
            {
                int g = pass == 0 ? guide : pass == 1 ? G_Halo : G_Blob;
                if (pass == 0 && (g == G_Rig || g == G_Knight)) continue;
                if (guideCount[g] <= 0) continue;
                for (int t = 0; t < n; t++)
                {
                    int i = idleCursor;
                    idleCursor = (idleCursor + 1) % n;
                    ref NanoBot b = ref bots[i];
                    if (b.mode == BotMode.Bound && b.guide == g && b.wait <= 0f && b.seg >= 0) { guideCount[g]--; return i; }
                }
            }
            return -1;
        }

        void FireOne(int i, Vector3 hold, float wait, Vector3 vel, float life, int home, Vector3 accel, float rim, bool snap)
        {
            ref NanoBot b = ref bots[i];
            if (snap) b.pos = hold;
            b.mode = BotMode.Free;
            b.hold = hold;
            b.wait = wait;
            b.launch = vel;
            if (wait <= 0f) b.vel = vel;
            b.accel = accel;
            b.life = life;
            b.home = home;
            b.rim = rim;
            b.cell = -1;
            b.spin = (b.hash - 0.5f) * 20f;
        }

        /// <summary>떨어뜨린다 (질량에서 빠짐)</summary>
        public void Drop(int i, Vector3 vel)
        {
            ref NanoBot b = ref bots[i];
            if (b.mode == BotMode.Bound && b.guide == G_Rig && Rig.rig) b.liquid = (byte)(Rig.rig.LimbBind(b.slot) < 0.5f ? 1 : 0);
            else b.liquid = 0;
            b.mode = BotMode.Debris;
            b.vel = new Vector3(vel.x, vel.y, 0f);
            b.flash = 1f;
            b.rim = 0f;
        }

        /// <summary>
        /// 지점을 때린다. 반경 안의 붙은 봇이 떨어져 나간다 (화면 평면 거리, 깊이 무시).
        /// chance = 맞은 봇이 실제로 떨어질 확률 (3페 기사는 단단해서 낮게). guideMask = 맞을 수 있는 가이드 비트
        /// </summary>
        public int Hit(Vector3 point, float radius, Vector3 impulse, float chance = 1f, int guideMask = ~0)
        {
            float r2 = radius * radius;
            int n = 0;
            for (int i = 0; i < bots.Length; i++)
            {
                ref NanoBot b = ref bots[i];
                if (b.mode != BotMode.Bound || (guideMask & (1 << b.guide)) == 0) continue;
                if (b.guide == G_Knight && b.seg < 0) continue;   // 눈은 맞지 않는다
                if (Mathf.Abs(b.pos.z - point.z) > 1.5f) continue;   // 뒷판 · 판 사이를 나는 봇은 맞지 않는다
                float dx = b.pos.x - point.x, dy = b.pos.y - point.y;
                if (dx * dx + dy * dy > r2) continue;
                b.flash = 1f;
                if (chance < 1f && R() > chance) continue;
                var away = new Vector2(dx, dy);
                away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up;
                Drop(i, impulse + new Vector3(away.x, away.y + 0.6f, 0f) * (3f + 3f * b.hash));
                n++;
            }
            if (n > 0) Recount();
            return n;
        }

        /// <summary>플레이어를 해칠 수 있는 봇이 지점 근처에 있나 (날아가는 탄, 위험 가이드, 기사 칼날)</summary>
        /// <summary>그림 기사 봇의 판정 — 효과 칸은 늘, 칼날 칸은 검기가 있는 프레임에만</summary>
        bool KnightDanger(int i)
        {
            int kind = KnightKind(i);
            // 효과 · 칼날은 공격 프레임에만 (몸에 붙은 작은 반짝임 · 먼지는 안 아프다). 돌진 중엔 칼날도
            return (kind == 2 && Knight.Active) || (kind == 1 && (Knight.Active || Knight.bladeDanger));
        }

        public int DangerNear(Vector3 point, float radius)
        {
            float r2 = radius * radius;
            int c = 0;
            for (int i = 0; i < bots.Length; i++)
            {
                ref NanoBot b = ref bots[i];
                bool d = (b.mode == BotMode.Free && b.wait <= 0f)
                      || (b.mode == BotMode.Bound && (Guides[b.guide].danger
                                                      || (b.guide == G_Knight && !knightSprite && Knight.bladeDanger && b.seg == KnightGuide.BladeSeg)
                                                      || (b.guide == G_Knight && knightSprite && b.seg >= 0 && KnightDanger(i))
                                                      || (b.guide == G_Rig && Rig.Danger(b.slot))));
                if (!d) continue;
                float dx = b.pos.x - point.x, dy = b.pos.y - point.y, dz = b.pos.z - point.z;
                if (dx * dx + dy * dy <= r2 && Mathf.Abs(dz) < 1.2f) c++;
            }
            return c;
        }

        float[] sortKeys = Array.Empty<float>();
        int[] sortIdx = Array.Empty<int>();

        /// <summary>가이드에 붙은 봇 중 지점에 가까운 순으로 max 개</summary>
        public List<int> Collect(int guide, Vector3 near, int max, List<int> into = null)
        {
            into ??= new List<int>();
            into.Clear();
            if (sortKeys.Length < bots.Length) { sortKeys = new float[bots.Length]; sortIdx = new int[bots.Length]; }
            int m = 0;
            for (int i = 0; i < bots.Length; i++)
            {
                if (bots[i].mode != BotMode.Bound || bots[i].guide != guide) continue;
                sortKeys[m] = (bots[i].pos - near).sqrMagnitude;
                sortIdx[m] = i;
                m++;
            }
            Array.Sort(sortKeys, sortIdx, 0, m);
            for (int k = 0; k < m && k < max; k++) into.Add(sortIdx[k]);
            return into;
        }

        /// <summary>어떤 모드의 봇 전부</summary>
        public List<int> AllIn(BotMode mode, List<int> into = null, int guide = -1)
        {
            into ??= new List<int>();
            into.Clear();
            for (int i = 0; i < bots.Length; i++)
                if (bots[i].mode == mode && (guide < 0 || bots[i].guide == guide)) into.Add(i);
            return into;
        }

        public void SetFlash(int i, float f) { bots[i].flash = Mathf.Max(bots[i].flash, f); }
        public void SetRim(int i, float r) { bots[i].rim = r; }
        public void SetTint(int i, Color32 c) { bots[i].tint = c; }
        public void SetGlow(int i, float g) { bots[i].glow = g; }

        /// <summary>전부 바닥에 흩어 놓는다 (등장 전 · 가짜 엔딩) — 모래가 돼 바닥에 얇게 깔린다</summary>
        public void ScatterOnFloor(float xMin, float xMax, float zMin = -1f, float zMax = 2f)
        {
            for (int i = 0; i < bots.Length; i++)
            {
                ref NanoBot b = ref bots[i];
                b.mode = BotMode.Debris;
                b.liquid = 0;
                b.plane = 0;
                b.pos = new Vector3(Mathf.Lerp(xMin, xMax, R()), floorY + Cell * (0.5f + R() * 6f), FrontZ);
                b.vel = Vector3.zero;
                b.cell = -1;
            }
            Recount();
        }

        public void ResetBotLooks()
        {
            for (int i = 0; i < bots.Length; i++)
            {
                ref NanoBot b = ref bots[i];
                b.rim = 0f;
                if (b.seg == -1) b.seg = 0;
            }
            RecolorAll();
        }

        // ─────────────────────────────────────────────────────────────
        // 그리기
        // ─────────────────────────────────────────────────────────────
        void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (!this || !isActiveAndEnabled || bots.Length == 0) return;
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            if (buffer == null || dirty || uploadedFrame != Time.frameCount) Upload();
            dirty = false;

            var mat = GetMaterial();
            if (!mat) return;

            // 판은 월드 XY 평면 (횡스크롤 카메라가 +z 를 본다). 봇 사각형 = 칸 하나
            mpb ??= new MaterialPropertyBlock();
            mpb.SetBuffer(Ids.Bots, buffer);
            mpb.SetColor(Ids.BossFlash, flashColor);
            mpb.SetFloat(Ids.Dark, dark);
            mpb.SetFloat(Ids.DarkTint, darkTint);
            mpb.SetColor(Ids.RimColor, rimColor);

            var rp = new RenderParams(mat)
            {
                camera = cam,
                layer = gameObject.layer,
                worldBounds = bounds,
                shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = receiveShadows,
                matProps = mpb,
            };
            Graphics.RenderPrimitives(rp, MeshTopology.Triangles, 6, bots.Length);
            DrawAura(cam);
        }

        unsafe void Upload()
        {
            long t0 = Stopwatch.GetTimestamp();
            int n = bots.Length;
            if (gpu.Length != n) gpu = new NanoGpuBot[n];
            if (buffer == null || buffer.count != n)
            {
                buffer?.Release();
                buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, UnsafeUtility.SizeOf<NanoGpuBot>());
            }
            if (feel[0].omega <= 0f) FillGuides();
            var rig = Rig.rig;
            bool rigOn = rig && rig.Active && rig.SimLimbCount > 0;
            LimbSim[] lsim = rigOn ? rig.SimLimbs : NoLimbs;

            var pins = new Pins();
            try
            {
                new NanoUploadJob
                {
                    bots = (NanoBot*)pins.Pin(bots),
                    gpu = (NanoGpuBot*)pins.Pin(gpu),
                    feel = (GuideFeel*)pins.Pin(feel),
                    limbs = (LimbSim*)pins.Pin(lsim),
                    bounds = (float3*)pins.Pin(boundsOut),
                    count = n,
                    limbCount = rigOn ? rig.SimLimbCount : 0,
                    highlightDanger = highlightDanger ? 1 : 0,
                    freeGlow = freeGlow,
                    freeTint = Pack(freeTint),
                    occ = (uint*)pins.Pin(occBits),
                    w = fieldW, h = fieldH, minX = fieldMinX, minY = fieldMinY, cell = Cell,
                    frontZ = FrontZ, backZ = BackZ, backDim = backDim, freeSize = freeSize,
                    eyeSize = Knight.SpriteMode ? Knight.Scale : 1,
                }.Run();
            }
            finally { pins.Release(); }

            buffer.SetData(gpu);
            Vector3 min = boundsOut[0], max = boundsOut[1];
            float pad = Cell * 8f;
            bounds = min.x <= max.x
                ? new Bounds((min + max) * 0.5f, max - min + new Vector3(pad, pad, pad))
                : new Bounds(transform.position, Vector3.one);
            uploadedFrame = Time.frameCount;
            float ms = (float)((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
            UploadMs = UploadMs <= 0f ? ms : Mathf.Lerp(UploadMs, ms, 0.1f);
        }

        Material GetMaterial()
        {
            if (material) return material;
            if (!runtimeMaterial)
            {
                var sh = shader ? shader : Shader.Find(ShaderName);
                if (!sh) return null;
                runtimeMaterial = new Material(sh) { name = "NanoBot (자동)", hideFlags = HideFlags.DontSave };
            }
            return runtimeMaterial;
        }
    }
}
