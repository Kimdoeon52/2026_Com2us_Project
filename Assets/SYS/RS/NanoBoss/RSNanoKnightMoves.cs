// RE:AL STEEL - 나노봇 3페 기사 동작 (스프라이트 시트 → 기사 몸)
//
// 3페 기사의 몸을 픽셀아트 동작 그림으로 만든다. 동작마다 가로로 이어 붙인 시트 PNG 하나 (칸 크기 = cellSize).
//   · 그림 픽셀 하나 = 칸 '배율 × 배율' (봇이 그만큼 모여 픽셀 하나가 된다) → 45px 캐릭터 × 3 = 키 약 4.2m
//   · 픽셀은 세 종류로 나눠 굽는다
//       몸     — 이동 동작(달리기 · 대시 · 백대시)에도 나오는 색
//       칼날   — 몸 색 중 밝고 푸른 색 (b ≥ r + 15, 밝기 0.6 이상)
//       효과   — 이동 동작에 없는 색 (검기 · 불꽃 · 먼지). 공격 동작에서만 나온다
//   · 봇은 몸 → 칼날 → 효과 순으로 채운다. 봇이 모자라면 효과가 성기게 (가루처럼) 그려지고 몸은 늘 꽉 찬다
//   · 3페: 몸은 검은 실루엣 + 흰 테두리, 칼날 · 효과는 흰 빛. 공격 프레임(효과 픽셀 120개 이상)의 효과 · 칼날이 공격 판정
// 굽기는 편집기 (Tools → RE_AL STEEL → 나노봇 → 3페 기사 동작 굽기, 또는 이 에셋 인스펙터).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.NanoBoss
{
    public enum KnightMove
    {
        RunIn, Running, RunOut, Dash, BackDash,
        Light1, Light2, Light3, Light4,
        FrontHeavy, DashAtk, DashHeavy, Rush,
        Jump, AirIn, AirLoop, AirLand,
        Guard1, Guard2, Guard3,
    }

    [Serializable]
    public class NanoKnightClip
    {
        [Tooltip("가로로 이어 붙인 동작 시트 PNG (칸 크기는 위 '칸 크기')")]
        public Texture2D sheet;
        [Tooltip("초당 프레임 — 아래 '프레임 시간' 과 GIF 기준 타이밍 표가 둘 다 없을 때만 쓴다")]
        [Range(4f, 30f)] public float fps = 15f;
        [Tooltip("프레임마다 시간 (초). 비우면 GIF 기준 타이밍 표(11-11, 사용자 GIF 에서 잰 값)를 쓴다. 길이 = 프레임 수")]
        public float[] frameTime = Array.Empty<float>();
        [NonSerialized] public float[] defaultTime;

        // ── 구운 값 ──
        [HideInInspector] public int frames;
        [HideInInspector] public int[] start = Array.Empty<int>();     // 프레임마다 px 시작 (frames + 1)
        [HideInInspector] public int[] counts = Array.Empty<int>();    // 프레임마다 몸 · 칼날 · 효과 수 (frames × 3)
        [HideInInspector] public int[] px = Array.Empty<int>();        // (x+2048) | (y+2048)<<12 | 종류<<24 — 몸 · 칼날 · 효과 순, 각 힐베르트 순
        [HideInInspector] public int[] color = Array.Empty<int>();     // px 와 같은 순서 (r | g<<8 | b<<16)
        [HideInInspector] public string bakedKey;

        public bool Ready => frames > 0 && start != null && start.Length == frames + 1 && counts.Length == frames * 3;
        public int Body(int f) => counts[f * 3];
        public int Blade(int f) => counts[f * 3 + 1];
        public int Fx(int f) => counts[f * 3 + 2];
        /// <summary>효과 픽셀이 있는 프레임 (공격이 닿는 순간)</summary>
        public bool Active(int f) => Ready && Fx(Mathf.Clamp(f, 0, frames - 1)) >= ActiveFx;
        /// <summary>효과 픽셀이 이만큼 넘으면 공격 프레임 (그보다 적으면 먼지 · 반짝임)</summary>
        public const int ActiveFx = 120;
        /// <summary>첫 공격 프레임 (없으면 -1)</summary>
        public int FirstActive()
        {
            for (int f = 0; f < frames; f++) if (Active(f)) return f;
            return -1;
        }
        /// <summary>프레임 f 의 시간 (초) — 프레임 시간 → GIF 기준 표 → fps 순</summary>
        public float Dur(int f)
        {
            if (frames <= 0) return 1f / Mathf.Max(1f, fps);
            f = Mathf.Clamp(f, 0, frames - 1);
            if (frameTime != null && frameTime.Length == frames && frameTime[f] > 0f) return frameTime[f];
            if (defaultTime != null && defaultTime.Length == frames && defaultTime[f] > 0f) return defaultTime[f];
            return 1f / Mathf.Max(1f, fps);
        }
        /// <summary>from ~ to(제외) 프레임의 시간 합 (초)</summary>
        public float TimeOf(int from, int to)
        {
            float t = 0f;
            for (int f = Mathf.Max(0, from); f < Mathf.Min(to, frames); f++) t += Dur(f);
            return t;
        }
        public float Length => Ready ? TimeOf(0, frames) : 0f;
        /// <summary>반복 재생에서 t 초 뒤의 프레임</summary>
        public int LoopFrame(float t)
        {
            float len = Length;
            if (len <= 0f) return 0;
            t %= len;
            for (int f = 0; f < frames; f++) { t -= Dur(f); if (t < 0f) return f; }
            return frames - 1;
        }
    }

    [CreateAssetMenu(menuName = "RE_AL STEEL/나노봇 3페 기사 동작", fileName = "기사_동작")]
    public class RSNanoKnightMoves : ScriptableObject
    {
        [Header("굽기")]
        [Tooltip("시트 한 칸의 크기 (px)")]
        public Vector2Int cellSize = new Vector2Int(300, 128);
        [Tooltip("칸 안에서 발밑 가운데 (px, 왼쪽 아래 0). 모든 동작이 같은 자리를 기준으로 그려져 있어야 한다")]
        public Vector2Int root = new Vector2Int(150, 33);
        [Tooltip("알파가 이보다 낮은 픽셀은 무시")]
        [Range(0f, 1f)] public float alphaCut = 0.5f;

        [Header("기사")]
        [Tooltip("그림 픽셀 하나 = 칸 배율 × 배율 (봇이 그만큼). 3 = 45px 캐릭터가 키 약 4.2m. 군집 봇 수가 모자라면 각성 때 자동으로 낮춘다 (몸이 비지 않게)")]
        [Range(1, 4)] public int pixelScale = 3;
        [Tooltip("검은 실루엣 + 흰 테두리 (끄면 그림 색 그대로)")]
        public bool darkSilhouette = true;
        [Tooltip("칼날 발광")]
        [Range(0f, 4f)] public float bladeGlow = 0.8f;
        [Tooltip("검기 · 효과 발광")]
        [Range(0f, 6f)] public float fxGlow = 2f;
        [Tooltip("봇이 자리에 붙는 세기. 높을수록 프레임이 바뀔 때 몸이 바로 또렷해지고 (그림처럼 착착), 낮을수록 프레임 사이에 봇이 날아다녀 몸이 흐트러져 보인다")]
        [Range(2000f, 80000f)] public float bodySnap = 40000f;
        [Tooltip("봇 최고 속도 (m/s) — 검기처럼 멀리 뻗는 칸까지 한두 프레임에 닿게")]
        [Range(50f, 600f)] public float snapSpeed = 400f;
        [Tooltip("대기 자세 — 이 동작의 첫 프레임")]
        public KnightMove idle = KnightMove.Guard1;

        [Header("동작 (시트 PNG)")]
        public NanoKnightClip runIn = new NanoKnightClip();
        public NanoKnightClip running = new NanoKnightClip { fps = 14f };
        public NanoKnightClip runOut = new NanoKnightClip();
        public NanoKnightClip dash = new NanoKnightClip { fps = 22f };
        public NanoKnightClip backDash = new NanoKnightClip { fps = 18f };
        public NanoKnightClip light1 = new NanoKnightClip { fps = 20f };
        public NanoKnightClip light2 = new NanoKnightClip { fps = 20f };
        public NanoKnightClip light3 = new NanoKnightClip { fps = 20f };
        public NanoKnightClip light4 = new NanoKnightClip { fps = 20f };
        public NanoKnightClip frontHeavy = new NanoKnightClip { fps = 16f };
        public NanoKnightClip dashAtk = new NanoKnightClip { fps = 18f };
        public NanoKnightClip dashHeavy = new NanoKnightClip { fps = 18f };
        public NanoKnightClip rush = new NanoKnightClip { fps = 18f };
        public NanoKnightClip jump = new NanoKnightClip { fps = 16f };
        public NanoKnightClip airIn = new NanoKnightClip { fps = 16f };
        public NanoKnightClip airLoop = new NanoKnightClip { fps = 12f };
        public NanoKnightClip airLand = new NanoKnightClip { fps = 18f };
        public NanoKnightClip guard1 = new NanoKnightClip { fps = 18f };
        public NanoKnightClip guard2 = new NanoKnightClip { fps = 18f };
        public NanoKnightClip guard3 = new NanoKnightClip { fps = 18f };

        public NanoKnightClip Get(KnightMove m)
        {
            var c = GetRaw(m);
            if (c.defaultTime == null || c.defaultTime.Length != c.frames) c.defaultTime = GifTimingFor(m, c.frames);
            return c;
        }

        NanoKnightClip GetRaw(KnightMove m)
        {
            switch (m)
            {
                case KnightMove.RunIn: return runIn;
                case KnightMove.Running: return running;
                case KnightMove.RunOut: return runOut;
                case KnightMove.Dash: return dash;
                case KnightMove.BackDash: return backDash;
                case KnightMove.Light1: return light1;
                case KnightMove.Light2: return light2;
                case KnightMove.Light3: return light3;
                case KnightMove.Light4: return light4;
                case KnightMove.FrontHeavy: return frontHeavy;
                case KnightMove.DashAtk: return dashAtk;
                case KnightMove.DashHeavy: return dashHeavy;
                case KnightMove.Rush: return rush;
                case KnightMove.Jump: return jump;
                case KnightMove.AirIn: return airIn;
                case KnightMove.AirLoop: return airLoop;
                case KnightMove.AirLand: return airLand;
                case KnightMove.Guard1: return guard1;
                case KnightMove.Guard2: return guard2;
                default: return guard3;
            }
        }

        public static readonly KnightMove[] All = (KnightMove[])Enum.GetValues(typeof(KnightMove));

        // ── GIF 기준 타이밍 (11-11) ──────────────────────────────────
        // 사용자가 보낸 GIF 3개(약공 4연타 · 대시 → 대시공격 → 점프공격 · 대기)의 프레임을 동작 시트와 맞춰 잰 프레임 시간 (ms).
        // 앞에서부터 프레임 순서. -1 = GIF 에 안 나온 프레임 → 'fill' 값. 표에 없는 동작은 fill 만.
        // 대기 계열 120ms · 이동 100ms · 공격 70~100ms · 대시 순간 30~50ms
        static readonly Dictionary<KnightMove, (int fill, int[] ms)> GifTiming = new Dictionary<KnightMove, (int, int[])>
        {
            { KnightMove.Light1, (100, new[] { 100, 100, 100, 100, 100, 80, 80, 80, 100 }) },
            { KnightMove.Light2, (100, new[] { 100, 100, 100, 80, 80, 80, 80, 80, 80, 80, 100, 100, 100, 100, 100 }) },
            { KnightMove.Light3, (100, new[] { 80, 80, 70, 80, 80, 80, 100, 100, 100, 100 }) },
            { KnightMove.Light4, (100, new[] { 100, 100, 80, 80, 80 }) },
            { KnightMove.DashAtk, (100, new[] { 70, 100, 100, 70, 70, 70, 70, 70, 70, 70, 100, 100, 100 }) },
            { KnightMove.Rush, (80, new[] { 70, 100, 100, 100, 100, 30, 50, 50, 50, 70, 70, 80, 80, 80, 80, 80, 70, 70, 70, 70, 70, 70, 70, 70, 100, 100, 100, 100, 100 }) },
            { KnightMove.Jump, (100, new[] { 50, 100, 100, 100, 100, 50, 100, 100, 100, 70, 70, 70, 70, 70, 70 }) },
            { KnightMove.Running, (100, null) }, { KnightMove.RunIn, (100, null) }, { KnightMove.RunOut, (100, null) },
            { KnightMove.Dash, (70, null) }, { KnightMove.BackDash, (70, null) },
            { KnightMove.FrontHeavy, (100, null) }, { KnightMove.DashHeavy, (80, null) },
            { KnightMove.AirIn, (100, null) }, { KnightMove.AirLoop, (100, null) }, { KnightMove.AirLand, (70, null) },
            { KnightMove.Guard1, (120, null) }, { KnightMove.Guard2, (100, null) }, { KnightMove.Guard3, (100, null) },
        };

        public static float[] GifTimingFor(KnightMove m, int frames)
        {
            var t = new float[Mathf.Max(0, frames)];
            if (!GifTiming.TryGetValue(m, out var g)) g = (100, null);
            for (int f = 0; f < t.Length; f++)
            {
                int ms = g.ms != null && f < g.ms.Length && g.ms[f] > 0 ? g.ms[f] : g.fill;
                t[f] = ms / 1000f;
            }
            return t;
        }

        /// <summary>이동 동작 — 이 동작들의 색이 '몸' 색이다</summary>
        public static readonly KnightMove[] BaseMoves = { KnightMove.RunIn, KnightMove.Running, KnightMove.RunOut, KnightMove.Dash, KnightMove.BackDash };

        /// <summary>시트 파일 이름 (편집기 굽기가 폴더에서 찾는다)</summary>
        public static string FileName(KnightMove m) => m.ToString();

        public bool Ready => Get(idle).Ready;
        public bool Has(KnightMove m) => Get(m).Ready;

        /// <summary>가장 큰 프레임의 봇 수 (배율 반영) — 몸 + 칼날만</summary>
        public int MaxBodyBots() => MaxBodyBots(pixelScale);

        /// <summary>배율 s 일 때 가장 큰 프레임의 몸 + 칼날 봇 수</summary>
        public int MaxBodyBots(int s)
        {
            int n = 0, s2 = s * s;
            foreach (var m in All)
            {
                var c = Get(m);
                if (!c.Ready) continue;
                for (int f = 0; f < c.frames; f++) n = Mathf.Max(n, (c.Body(f) + c.Blade(f)) * s2);
            }
            return n;
        }

        public static int Pack(int x, int y, int kind) => ((x + 2048) & 0xFFF) | (((y + 2048) & 0xFFF) << 12) | (kind << 24);
        public static int X(int v) => (v & 0xFFF) - 2048;
        public static int Y(int v) => ((v >> 12) & 0xFFF) - 2048;
        public static int Kind(int v) => (v >> 24) & 0xFF;
    }
}
