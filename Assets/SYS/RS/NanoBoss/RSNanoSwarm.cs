// RE:AL STEEL - 나노봇 군집 (런타임 핵심)
//
// 8x8 이내의 작은 봇 수천 개를 한 덩어리로 다룬다. 봇마다 GameObject 가 없고,
// 위치 · 상태는 배열에, 계산은 Burst 잡(RSNanoSim)에, 그리기는 Graphics.RenderPrimitives 한 번이다.
//
// 봇의 상태
//   Bound   가이드에 붙어 있다 (덩어리 · 줄기 · 포신 · 기사 · 후광 · 리그). 자리를 감쇠 스프링으로 쫓는다
//   Free    투사체. hold 자리에서 wait 동안 대기(대열 예고) → vel 로 날아감 → life 가 끝나면 home 가이드로 복귀
//   Debris  맞아서 떨어졌다. 바닥에 굴러다닌다 (질량에서 빠짐)
//   Dead    숨김
//
// 기계 군체 (10-28)
//   · 덩어리 · 줄기 · 기사 · 리그 부위는 '격자 자리'(RSNanoLattice)에 맞물린다. 자리 잡은 봇은 각도 0 의 판(3~6px)으로 그린다
//   · 결속(bind) 1 = 기계 격자, 0 = 유체 (무작위 채움 + 끈적임 + 표면 떨림 + 속도 방향 늘이기)
//   · 실루엣 가장자리 봇(주변 8 방향 중 3 방향 이상 빔)은 테두리 판. 3페 흰 테두리도 이것으로 정한다
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
        public ushort tileBody, tileEdge, tileDrop, tileLoose;   // 아틀라스 칸: 몸 판 · 테두리 판 · 방울 · 낱개 (봇 종류)
        public float hash, hash2, hash3;

        public int cell;           // 격자 자리 (덩어리 · 리그 부위). -1 = 없음
        public float edge;         // 실루엣 가장자리 정도 0~1
        public float stretch;      // 속도 방향 늘이기
        public float machine;      // 기계(판) 정도 0~1 — 그리기용
        public float space;        // 자리 간격 (m) — 판 크기 고르기
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
        [Tooltip("봇 수. 덩어리 크기가 이 수 × 격자 간격으로 정해진다")]
        [Range(200, 8000)] public int botCount = 5000;

        [Tooltip("봇 칸 크기 (m) = 픽셀 밀도. 8px ÷ 32px/m = 0.25 (게임 기준)")]
        public float botWorldSize = 0.25f;

        [Tooltip("격자 간격 (m). 봇이 이 간격으로 맞물린다. 작을수록 촘촘하고 같은 봇 수에서 덩어리가 작다.\n'간격을 그림에 맞춤' 이 켜져 있으면 몸 그림 크기로 자동")]
        [Range(0.03f, 0.6f)] public float spacing = 0.1f;

        [Tooltip("반짝이는 봇 비율")]
        [Range(0f, 0.2f)] public float sparkleRatio = 0.05f;

        public Color32 sparkleTint = new Color32(110, 225, 255, 255);

        [Header("봇 그림")]
        [Tooltip("봇 종류 — 한 줄에 PNG 하나 + 쓰는 곳 + 비율.\n" +
                 "몸 종류만 넣으면 테두리 · 유체 · 낱개 그림은 몸 그림으로 자동으로 만든다. 비우면 코드에 그린 시험 그림.\n" +
                 "PNG 를 고쳐 저장하면 바로 바뀐다 (가져오기 설정은 자동으로 맞춘다)")]
        public List<NanoBotKind> botKinds = new List<NanoBotKind>();
        [Tooltip("격자 간격을 몸 그림 크기에 맞춘다 (비율이 가장 큰 몸 그림의 칠한 크기 × 픽셀 크기). 끄면 아래 '격자 간격' 값을 쓴다")]
        public bool spacingFromPicture = true;

        [Header("기계 군체")]
        [Tooltip("격자에 자리 잡은 봇을 몸 · 테두리 · 방울 그림으로 그린다. 끄면 모두 낱개 그림")]
        public bool plates = true;
        [Tooltip("맞아서 빈 자리를 다시 채우기 전까지 (초). 이동안 구멍이 보인다")]
        [Range(0f, 2f)] public float refillDelay = 0.35f;
        [Tooltip("유체일 때 주변 봇과 속도를 맞추는 세기 (끈적임)")]
        [Range(0f, 30f)] public float viscosity = 8f;

        [Header("바닥")]
        [Tooltip("시작할 때 이 오브젝트 높이를 바닥으로 쓴다")]
        public bool floorFromTransform = true;
        public float floorY;
        public float gravity = 28f;
        [Range(0f, 1f)] public float bounce = 0.25f;
        public float friction = 4f;

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
        [Range(0f, 1f)] public float pitchFollow = 0.35f;
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

        NanoGpuBot[] gpu = Array.Empty<NanoGpuBot>();
        readonly float3[] boundsOut = new float3[2];
        GraphicsBuffer buffer;
        MaterialPropertyBlock mpb;
        Material runtimeMaterial;
        NanoBotAtlas atlas;
        int atlasSig;
        Bounds bounds;
        int uploadedFrame = -1;
        System.Random rng = new System.Random(7);
        float simClock;
        int simFrame;

        static class Ids
        {
            public static readonly int Bots = Shader.PropertyToID("_Bots");
            public static readonly int MainTex = Shader.PropertyToID("_MainTex");
            public static readonly int PlaneRight = Shader.PropertyToID("_PlaneRight");
            public static readonly int PlaneUp = Shader.PropertyToID("_PlaneUp");
            public static readonly int PlaneNormal = Shader.PropertyToID("_PlaneNormal");
            public static readonly int AtlasInfo = Shader.PropertyToID("_AtlasInfo");
            public static readonly int AtlasSize = Shader.PropertyToID("_AtlasSize");
            public static readonly int TileWorld = Shader.PropertyToID("_TileWorld");
            public static readonly int BossFlip = Shader.PropertyToID("_BossFlip");
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

        /// <summary>지금 봇 그림 아틀라스 (인스펙터 미리보기)</summary>
        public NanoBotAtlas Atlas { get { EnsureAtlas(); return atlas; } }

        void EnsureAtlas()
        {
            int sig = RSNanoBotSprites.Signature(botKinds);
            if (atlas == null || !atlas.texture || sig != atlasSig)
            {
                if (atlas != null && atlas.texture) { if (Application.isPlaying) Destroy(atlas.texture); else DestroyImmediate(atlas.texture); }
                atlas = RSNanoBotSprites.Build(botKinds);
                atlasSig = sig;
                for (int i = 0; i < bots.Length; i++) atlas.Assign(i, ref bots[i]);
                dirty = true;
            }
            SyncSpacing();
        }

        /// <summary>'간격을 그림에 맞춤' — 격자 간격 = 몸 그림 칠한 크기 × 픽셀 크기</summary>
        void SyncSpacing()
        {
            if (!spacingFromPicture || atlas == null) return;
            spacing = Mathf.Clamp(atlas.MainSize * botWorldSize / RSNanoBotSprites.Tile, 0.03f, 0.6f);
        }

        /// <summary>봇 그림 PNG 를 다시 읽는다 (편집기가 PNG 가 다시 가져와질 때 부른다)</summary>
        public void RebuildAtlas()
        {
            atlasSig = 0;
            if (atlas != null && atlas.texture) { if (Application.isPlaying) Destroy(atlas.texture); else DestroyImmediate(atlas.texture); }
            atlas = null;
            EnsureAtlas();
        }

        /// <summary>편집기가 채운다: 봇 그림 PNG 가져오기 설정을 맞춘다 (읽기 허용 · 점 필터 · 무압축)</summary>
        public static System.Action<Texture2D> fixSheetImport;

        void OnValidate()
        {
            if (botKinds != null && fixSheetImport != null)
                foreach (var k in botKinds)
                    if (k != null && k.picture) fixSheetImport(k.picture);
            if (atlas != null) SyncSpacing();
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
            if (atlas != null && atlas.texture) { if (Application.isPlaying) Destroy(atlas.texture); else DestroyImmediate(atlas.texture); }
            atlas = null;
        }

        float R() => (float)rng.NextDouble();

        /// <summary>봇을 새로 만든다. 전부 덩어리에 붙은 상태</summary>
        public void Build(Vector3 blobCenter)
        {
            int n = Mathf.Max(1, botCount);
            if (floorFromTransform && !Application.isPlaying) floorY = transform.position.y;
            EnsureAtlas();
            bots = new NanoBot[n];
            gpu = new NanoGpuBot[n];
            sortedIdx = new int[n];
            sortedPos = new float2[n];
            Blob.floorY = floorY;
            Blob.center = blobCenter;
            for (int i = 0; i < n; i++)
            {
                Vector3 v;
                do v = new Vector3(R() * 2f - 1f, R() * 2f - 1f, R() * 2f - 1f); while (v.sqrMagnitude > 1f);
                bool sparkle = R() < sparkleRatio;
                byte shade = (byte)(205 + R() * 50f);
                bots[i] = new NanoBot
                {
                    mode = BotMode.Bound,
                    guide = G_Blob,
                    home = G_Blob,
                    blobV = v,
                    seg = 0,
                    cell = -1,
                    roll = (R() - 0.5f) * 0.6f,
                    scale = 0.8f + R() * 0.35f,
                    glow = sparkle ? 1.2f + R() : 0f,
                    tint = sparkle ? sparkleTint : new Color32(shade, shade, (byte)Mathf.Min(255, shade + 12), 255),
                    hash = R(), hash2 = R(), hash3 = R(),
                    space = spacing,
                };
                bots[i].pos = blobCenter + Vector3.Scale(v, Blob.radius);
                atlas.Assign(i, ref bots[i]);
            }
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
            FillGuides();

            var rig = Rig.rig;
            bool rigOn = rig && rig.Active && rig.SimLimbCount > 0;
            LimbSim[] lsim = rigOn ? rig.SimLimbs : NoLimbs;
            float4[] lslots = rigOn ? rig.SimSlots : NoSlots;
            float4[] lspine = rigOn ? rig.SimSpine : NoSpine;
            float[] lspineT = rigOn ? rig.SimSpineTime : NoSpineTime;
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
                new NanoGridJob
                {
                    bots = pBots, cellStart = pCellStart, cellCount = pCellCount, cellFill = pCellFill,
                    sorted = pSorted, sortedPos = pSortedPos, cellVel = pCellVel, info = pGrid,
                    count = bots.Length, maxCells = MaxCells, baseCell = Mathf.Max(0.16f, spacing * 2f),
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
                    tubeA = new TubeSim { r0 = TubeA.r0, r1 = TubeA.r1 },
                    tubeB = new TubeSim { r0 = TubeB.r0, r1 = TubeB.r1 },
                    ring = new RingSim { center = Ring.center, axis = Ring.axis, radius = Ring.radius, length = Ring.length, angle = Ring.angle },
                    knight = new KnightSim { origin = Knight.origin, lookAt = Knight.lookAt, head = Knight.HeadWorld, facing = Knight.facing },
                    halo = new HaloSim { center = Halo.center, radius = Halo.radius, spin = Halo.spin },
                    limbCount = rigOn ? rig.SimLimbCount : 0,
                    blobSlotCount = blobTable.Length,
                    frame = ++simFrame,
                    dt = dt, time = time, clock = rigOn ? rig.Clock : 0f, floorY = floorY,
                    gravity = gravity, bounce = bounce, friction = friction, flashDecay = flashDecay,
                    half = botWorldSize * 0.5f, viscosity = viscosity, edgeRate = 12f, dark = dark, spacing = spacing,
                };
                job.Schedule(bots.Length, 64).Complete();
            }
            finally { pins.Release(); }

            Recount();
            dirty = true;
            float ms = (float)((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
            SimMs = SimMs <= 0f ? ms : Mathf.Lerp(SimMs, ms, 0.1f);
        }
        bool dirty;

        /// <summary>관리 배열을 고정해 포인터로 넘긴다 (GC 가 옮기지 못하게). 잡이 끝나면 푼다</summary>
        unsafe struct Pins
        {
            ulong h0, h1, h2, h3, h4, h5, h6, h7, h8, h9, h10, h11, h12, h13, h14, h15, h16, h17;
            int n;

            public void* Pin(Array a)
            {
                void* p = UnsafeUtility.PinGCArrayAndGetDataAddress(a, out ulong h);
                switch (n++)
                {
                    case 0: h0 = h; break; case 1: h1 = h; break; case 2: h2 = h; break; case 3: h3 = h; break;
                    case 4: h4 = h; break; case 5: h5 = h; break; case 6: h6 = h; break; case 7: h7 = h; break;
                    case 8: h8 = h; break; case 9: h9 = h; break; case 10: h10 = h; break; case 11: h11 = h; break;
                    case 12: h12 = h; break; case 13: h13 = h; break; case 14: h14 = h; break; case 15: h15 = h; break;
                    case 16: h16 = h; break; case 17: h17 = h; break;
                    default: throw new InvalidOperationException("Pins: 너무 많다");
                }
                return p;
            }

            public void Release()
            {
                for (int i = 0; i < n; i++)
                {
                    ulong h;
                    switch (i)
                    {
                        case 0: h = h0; break; case 1: h = h1; break; case 2: h = h2; break; case 3: h = h3; break;
                        case 4: h = h4; break; case 5: h = h5; break; case 6: h = h6; break; case 7: h = h7; break;
                        case 8: h = h8; break; case 9: h = h9; break; case 10: h = h10; break; case 11: h = h11; break;
                        case 12: h = h12; break; case 13: h = h13; break; case 14: h = h14; break; case 15: h = h15; break;
                        case 16: h = h16; break; default: h = h17; break;
                    }
                    UnsafeUtility.ReleaseGCObject(h);
                }
                n = 0;
            }
        }

        /// <summary>덩어리 격자 자리 정리 — 빈자리 채우기 · 다지기. 덩어리 크기는 붙은 봇 수에 맞춘다</summary>
        void MaintainBlob(float time)
        {
            int n = bots.Length;
            if (blobTable.Length != n || Mathf.Abs(blobAspectBuilt - Blob.aspect) > 0.01f)
            {
                blobTable = NanoLattice.EllipseHex(n, Blob.aspect);
                blobAspectBuilt = Blob.aspect;
                if (blobSlots.Length != n) blobSlots = new float4[n];
                for (int k = 0; k < n; k++) blobSlots[k] = blobTable[k];
                blobSet.Resize(n);
            }
            int occ = blobSet.Maintain(bots, null, G_Blob, -1, time, refillDelay, Mathf.Max(4, n / 60), null, blobTable);
            blobExt = occ > 0 ? blobTable[Mathf.Min(occ, n) - 1].w + 0.5f : 0f;
            BlobSpace = blobExt > 0f ? Blob.radius.y / blobExt : spacing;
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
            float area = Mathf.Max(1, n) * 0.866f * spacing * spacing;
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

        /// <summary>투사체로 쏜다. hold 자리에서 wait 초 대기(예고) → vel 로 life 초 비행 → home 으로 복귀</summary>
        public void Fire(int i, Vector3 hold, float wait, Vector3 vel, float life, int home, Vector3 accel = default, float rim = 0f, bool snap = false)
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
            b.mode = BotMode.Debris;
            b.vel = vel;
            b.spin = (b.hash - 0.5f) * 30f;
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
                float dx = b.pos.x - point.x, dy = b.pos.y - point.y;
                if (dx * dx + dy * dy > r2) continue;
                b.flash = 1f;
                if (chance < 1f && R() > chance) continue;
                var away = new Vector2(dx, dy);
                away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up;
                Drop(i, impulse + new Vector3(away.x, away.y + 0.6f, (b.hash2 - 0.5f) * 1.2f) * (3f + 3f * b.hash));
                n++;
            }
            if (n > 0) Recount();
            return n;
        }

        /// <summary>플레이어를 해칠 수 있는 봇이 지점 근처에 있나 (날아가는 탄, 위험 가이드, 기사 칼날)</summary>
        public int DangerNear(Vector3 point, float radius)
        {
            float r2 = radius * radius;
            int c = 0;
            for (int i = 0; i < bots.Length; i++)
            {
                ref NanoBot b = ref bots[i];
                bool d = (b.mode == BotMode.Free && b.wait <= 0f)
                      || (b.mode == BotMode.Bound && (Guides[b.guide].danger
                                                      || (b.guide == G_Knight && Knight.bladeDanger && b.seg == KnightGuide.BladeSeg)
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

        /// <summary>전부 바닥에 흩어 놓는다 (등장 전 · 가짜 엔딩)</summary>
        public void ScatterOnFloor(float xMin, float xMax, float zMin = -1f, float zMax = 2f)
        {
            for (int i = 0; i < bots.Length; i++)
            {
                ref NanoBot b = ref bots[i];
                b.mode = BotMode.Debris;
                b.pos = new Vector3(Mathf.Lerp(xMin, xMax, R()), floorY + botWorldSize * 0.3f, Mathf.Lerp(zMin, zMax, R()));
                b.vel = Vector3.zero;
                b.spin = 0f;
                b.cell = -1;
                b.roll = Mathf.Round(R() * 4f) * Mathf.PI * 0.5f;
            }
            Recount();
        }

        public void ResetBotLooks()
        {
            for (int i = 0; i < bots.Length; i++)
            {
                ref NanoBot b = ref bots[i];
                bool sparkle = b.glow > 0.5f && b.seg != -1;
                b.rim = 0f;
                if (b.seg == -1) { b.seg = 0; b.glow = 0f; }
                byte shade = (byte)(205 + b.hash * 50f);
                b.tint = sparkle ? sparkleTint : new Color32(shade, shade, (byte)Mathf.Min(255, shade + 12), 255);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 그리기
        // ─────────────────────────────────────────────────────────────
        void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (!this || !isActiveAndEnabled || bots.Length == 0) return;
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            EnsureAtlas();
            if (buffer == null || dirty || uploadedFrame != Time.frameCount) Upload();
            dirty = false;

            var mat = GetMaterial();
            if (!mat) return;

            // 카메라를 보는 평행 빌보드 (카메라 오른쪽은 수평으로, 위는 캐릭터 셰이더처럼 일부만 기울임)
            Vector3 right = cam.transform.right; right.y = 0f;
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Lerp(Vector3.up, cam.transform.up, pitchFollow).normalized;
            Vector3 n = Vector3.Cross(right, up).normalized;   // 카메라 쪽

            mpb ??= new MaterialPropertyBlock();
            mpb.SetBuffer(Ids.Bots, buffer);
            mpb.SetTexture(Ids.MainTex, atlas.texture);
            mpb.SetVector(Ids.PlaneRight, right);
            mpb.SetVector(Ids.PlaneUp, up);
            mpb.SetVector(Ids.PlaneNormal, n);
            mpb.SetVector(Ids.AtlasInfo, new Vector4(atlas.cols, atlas.rows, atlas.tile + RSNanoBotSprites.Pad * 2, RSNanoBotSprites.Pad));
            mpb.SetVector(Ids.AtlasSize, new Vector4(atlas.texture.width, atlas.texture.height, atlas.tile, 0f));
            mpb.SetFloat(Ids.TileWorld, botWorldSize / RSNanoBotSprites.Tile * atlas.tile);   // 칸이 8px 보다 크면 그만큼 크게 → 픽셀 밀도 그대로
            mpb.SetFloat(Ids.BossFlip, 0f);
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
                    plates = plates ? 1 : 0,
                    freeGlow = freeGlow,
                    freeTint = Pack(freeTint),
                }.Run();
            }
            finally { pins.Release(); }

            buffer.SetData(gpu);
            Vector3 min = boundsOut[0], max = boundsOut[1];
            float pad = botWorldSize * 2f;
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
