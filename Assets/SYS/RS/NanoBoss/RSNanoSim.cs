// RE:AL STEEL - 나노봇 시뮬레이션 (Burst 잡)
//
// 봇 수만 개를 GTX 1060 3GB 급 PC 에서 60fps 로 돌리려고 봇 계산을 전부 Burst 잡으로 옮겼다.
//   NanoGridJob    봇 위치를 화면 평면 격자에 나눠 담는다 (유체 끈적임에 쓴다). 한 스레드
//   NanoSimJob     봇마다: 목표 자리 계산 → 판(앞 · 뒤) 정하기 → 흐름 · 끈적임 → 감쇠 스프링(정확해). 여러 스레드
//   NanoSandJob    떨어진 봇(모래): 날다가 바닥 · 더미에 닿으면 칸에 앉고, 아래 → 대각선 아래 (유체는 옆) 로 흘러내림. 한 스레드
//   NanoUploadJob  칸에 맞춘 위치 · 비어 있는 이웃(가장자리) · 색 · 발광 + 경계 상자. 한 스레드
//
// 목표 자리는 가이드 · 부위가 매 프레임 미리 채워 둔 값(GuideFeel · LimbSim · 격자 자리표 · 관절선 기록)으로만 계산한다.
// 관리 코드(가이드 클래스 · 부위 컴포넌트)는 잡 안에서 부르지 않는다.
//
// 스프링: 감쇠 진동의 정확해를 쓴다 → 서브스텝 없이도 안정, 같은 입력이면 같은 결과 (편집기 되감기 재시뮬)
//
// 2D 판 (11-10): 목표 자리의 깊이(z)는 판 두 장(앞 · 뒤) 중 하나로 눌린다. 리그 부위는 '뒷판' 값으로, 가이드는
//   목표가 두 판 사이 절반보다 안쪽이면 뒷판. 봇은 판 사이를 3D 로 날아 오가고, 판 위에서는 칸에 맞춰 그린다.
// 결속(bind): 봇은 늘 자기 격자 자리를 갖는다. 기계(1)면 자리에 딱, 유체(0)면 자리 위에 '흐름'(공간적으로
// 이어진 물결 변위)이 얹히고 끈적임으로 이웃과 함께 움직인다 → 흩어진 먼지가 아니라 흐르는 덩어리.
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace RealSteel.NanoBoss
{
    /// <summary>가이드 하나의 느낌 · 모습 (잡으로 넘기는 값)</summary>
    public struct GuideFeel
    {
        public float omega, zeta, maxSpeed, noise, noiseSpeed, bind, space, glow, flow, gather;
        public uint tint;
        public int danger;
        public int lattice;     // 격자 판으로 그리는 가이드인가
    }

    public struct BlobSim { public float3 center, radius, bulgeDir; public float wobble, wobbleSpeed, flow, flowSpeed, bulge, floorY, ext; }
    public struct TubeSim { public float r0, r1; }
    public struct RingSim { public float3 center, axis; public float radius, length, angle; }
    public struct KnightSim
    {
        public float3 origin, lookAt, head;
        public float facing;
        public int sprite, slotCount, art;      // 그림 기사: 봇마다 knightSlots[cell] 자리
        public float bladeGlow, fxGlow, eyeSpread;
    }
    public struct HaloSim { public float3 center; public float radius, spin; }

    /// <summary>리그 부위 하나 (잡으로 넘기는 값)</summary>
    public struct LimbSim
    {
        public int type;                      // 0 줄기 · 1 덩어리 · 2 웅덩이 · 3 그림 · 4 무리
        public int slotStart, slotCount;      // 격자 자리표 안 위치
        public int histBase, histHead, histCount;   // 관절선 기록 (부위 번호 × 기록 수)
        public float4x4 local;                // 덩어리: 단위 타원 → 월드 (크기 곱함)
        public float3 origin;
        public float grow, depth, wobble;
        public int fillToFloor;
        public float2 extent;
        public float layer, ripple, rippleSpeed, sx;
        public float3 bumpPos;
        public float bump, bumpRadius, bumpShake;
        public int hasBump;
        public float omega, zeta, gatherOmega, maxSpeed, flow, lag, bind, space;
        public float lockAge;                 // 재결속 연출 경과 (초). < 0 = 없음
        public int plane;                     // 0 앞판 · 1 뒷판
        public int artColor;                  // 그림: 도착한 봇이 그림 칸 색이 된다
        public float2 roam;                   // 무리: 돌아다니는 반경 (m)
        public float roamSpeed;
        public float glow;
        public uint tint;
        public int tinted, danger;
    }

    public struct GridInfo { public float minX, minY, cell; public int w, h; }

    /// <summary>GPU 로 보내는 봇 하나 (48 바이트). 셰이더 NanoBot 과 같은 순서</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NanoGpuBot
    {
        public float3 pos;      // 칸 중심 (판 위면 칸에 맞춤)
        public float size;      // 사각형 한 변 (m). 0 = 숨김
        public float flash, glow, rim;
        public uint open;       // 비어 있는 이웃 칸 (1 위 · 2 아래 · 4 왼 · 8 오른) — 가장자리 조명 · 테두리
        public uint color;
        public float3 pad;
    }

    public static class NanoSpring
    {
        public const float SettleK = 4.7f;   // 반응 시간 T 일 때 고유진동수 = SettleK / T

        /// <summary>감쇠 스프링 정확해 (목표는 dt 동안 고정)</summary>
        public static void Step(ref float3 pos, ref float3 vel, float3 target, float omega, float zeta, float dt)
        {
            omega = math.max(0.05f, omega);
            zeta = math.clamp(zeta, 0.02f, 0.999f);
            float3 x0 = pos - target;
            float3 v0 = vel;
            float zw = zeta * omega;
            float e = math.exp(-zw * dt);
            float wd = omega * math.sqrt(1f - zeta * zeta);
            if (wd < 1e-3f)
            {
                float3 c1 = v0 + omega * x0;
                pos = target + e * (x0 + c1 * dt);
                vel = e * (v0 - omega * c1 * dt);
                return;
            }
            math.sincos(wd * dt, out float s, out float c);
            pos = target + e * (x0 * c + (v0 + zw * x0) / wd * s);
            vel = e * (v0 * c - ((omega * omega) * x0 + zw * v0) / wd * s);
        }

        /// <summary>예전 값(세기 k · 감쇠 c) → 고유진동수 · 감쇠비</summary>
        public static void FromStiffness(float k, float c, out float omega, out float zeta)
        {
            omega = math.sqrt(math.max(0.01f, k));
            zeta = c / (2f * omega);
        }

        /// <summary>반응(프레임) · 출렁임(0~1) → 고유진동수 · 감쇠비</summary>
        public static void FromFrames(float frames, float bounce, out float omega, out float zeta)
        {
            omega = SettleK / math.max(1f / 60f, frames / 60f);
            zeta = 1f - 0.8f * math.saturate(bounce);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 격자 만들기
    // ─────────────────────────────────────────────────────────────
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct NanoGridJob : IJob
    {
        [NativeDisableUnsafePtrRestriction] public NanoBot* bots;
        [NativeDisableUnsafePtrRestriction] public int* cellStart;
        [NativeDisableUnsafePtrRestriction] public int* cellCount;
        [NativeDisableUnsafePtrRestriction] public int* cellFill;
        [NativeDisableUnsafePtrRestriction] public int* sorted;
        [NativeDisableUnsafePtrRestriction] public float2* sortedPos;
        [NativeDisableUnsafePtrRestriction] public float4* cellVel;
        [NativeDisableUnsafePtrRestriction] public GridInfo* info;
        public int count, maxCells;
        public float baseCell;

        public void Execute()
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            int any = 0;
            for (int i = 0; i < count; i++)
            {
                if (bots[i].mode != BotMode.Bound) continue;
                float3 p = bots[i].pos;
                if (!math.all(math.isfinite(p))) continue;
                minX = math.min(minX, p.x); maxX = math.max(maxX, p.x);
                minY = math.min(minY, p.y); maxY = math.max(maxY, p.y);
                any++;
            }
            if (any == 0) { *info = default; return; }

            float cell = baseCell;
            int W, H;
            while (true)
            {
                W = (int)((maxX - minX) / cell) + 1;
                H = (int)((maxY - minY) / cell) + 1;
                if (W * H <= maxCells) break;
                cell *= 1.4f;
            }
            int cells = W * H;
            for (int c = 0; c < cells; c++) { cellCount[c] = 0; cellVel[c] = float4.zero; }
            for (int i = 0; i < count; i++)
            {
                if (bots[i].mode != BotMode.Bound) continue;
                float3 p = bots[i].pos;
                if (!math.all(math.isfinite(p))) continue;
                int c = Index(p.x, p.y, minX, minY, cell, W, H);
                cellCount[c]++;
                float3 v = bots[i].vel;
                cellVel[c] += new float4(v.x, v.y, 1f, 0f);
            }
            int run = 0;
            for (int c = 0; c < cells; c++) { cellStart[c] = run; cellFill[c] = run; run += cellCount[c]; }
            for (int i = 0; i < count; i++)
            {
                if (bots[i].mode != BotMode.Bound) continue;
                float3 p = bots[i].pos;
                if (!math.all(math.isfinite(p))) continue;
                int c = Index(p.x, p.y, minX, minY, cell, W, H);
                int k = cellFill[c]++;
                sorted[k] = i;
                sortedPos[k] = p.xy;
            }
            *info = new GridInfo { minX = minX, minY = minY, cell = cell, w = W, h = H };
        }

        public static int Index(float x, float y, float minX, float minY, float cell, int W, int H)
        {
            int cx = math.clamp((int)((x - minX) / cell), 0, W - 1);
            int cy = math.clamp((int)((y - minY) / cell), 0, H - 1);
            return cy * W + cx;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 봇 한 프레임
    // ─────────────────────────────────────────────────────────────
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct NanoSimJob : IJobParallelFor
    {
        public const int K = 16, S = 32, H = 48, KnightSegs = 12;

        [NativeDisableUnsafePtrRestriction] public NanoBot* bots;
        [NativeDisableUnsafePtrRestriction] public GuideFeel* feel;
        [NativeDisableUnsafePtrRestriction] public float4* blobSlots;
        [NativeDisableUnsafePtrRestriction] public float3* tubeNodes;
        [NativeDisableUnsafePtrRestriction] public float4* knightSegs;
        [NativeDisableUnsafePtrRestriction] public float2* knightRad;
        [NativeDisableUnsafePtrRestriction] public LimbSim* limbs;
        [NativeDisableUnsafePtrRestriction] public float4* limbSlots;
        [NativeDisableUnsafePtrRestriction] public float4* spine;
        [NativeDisableUnsafePtrRestriction] public float* spineTime;
        [NativeDisableUnsafePtrRestriction] public int* cellStart;
        [NativeDisableUnsafePtrRestriction] public int* cellCount;
        [NativeDisableUnsafePtrRestriction] public int* sorted;
        [NativeDisableUnsafePtrRestriction] public float2* sortedPos;
        [NativeDisableUnsafePtrRestriction] public float4* cellVel;
        [NativeDisableUnsafePtrRestriction] public uint* limbSlotColor;
        [NativeDisableUnsafePtrRestriction] public float4* knightSlots;     // 그림 기사: 자리 (월드 xyz, w = 0 몸 · 1 칼날 · 2 효과)
        [NativeDisableUnsafePtrRestriction] public uint* knightColors;

        public GridInfo grid;
        public BlobSim blob;
        public TubeSim tubeA, tubeB;
        public RingSim ring;
        public KnightSim knight;
        public HaloSim halo;
        public int limbCount, blobSlotCount, frame;
        public float dt, time, clock, floorY, gravity, flashDecay, viscosity, dark, spacing, cell, frontZ, backZ;

        struct Feel
        {
            public float omega, zeta, vmax, noise, nspeed, bind, space, gather, lockAge, frac, flow;
            public bool lattice;
            public int plane;       // -1 = 목표 깊이로 정함
            public uint slotColor;  // 그림 칸 색 (0 = 없음)
        }

        /// <summary>깊이 배율 — 2D 판이라 모양의 두께는 쓰지 않는다</summary>
        static float ZK(float bind) => 0f;

        /// <summary>공간적으로 이어진 흐름 변위 (이웃끼리 비슷하게 움직인다). 판 위라 z 는 없다</summary>
        static float3 Flow(float3 p, float t)
        {
            float x = math.sin(p.y * 1.6f + t * 1.7f) + 0.6f * math.sin(p.x * 2.9f - t * 2.3f + p.y * 1.1f);
            float y = math.sin(p.x * 1.4f - t * 1.3f) + 0.6f * math.sin(p.y * 3.1f + t * 2.1f + p.x * 0.9f);
            return new float3(x, y, 0f) * 0.55f;
        }

        public void Execute(int i)
        {
            NanoBot* b = bots + i;
            b->flash = math.max(0f, b->flash - flashDecay * dt);
            switch (b->mode)
            {
                case BotMode.Bound: Bound(i, b); break;
                case BotMode.Free: Free(b); break;
                case BotMode.Debris: Debris(b); break;
                default: b->scale = 0f; break;
            }
        }

        static float Toward(float a, float b, float step) => a < b ? math.min(b, a + step) : math.max(b, a - step);

        static float WrapPi(float a)
        {
            a = math.fmod(a + math.PI, 2f * math.PI);
            if (a < 0f) a += 2f * math.PI;
            return a - math.PI;
        }

        // ── 붙은 봇 ─────────────────────────────────────────────────
        void Bound(int i, NanoBot* b)
        {
            float3 pos = b->pos, vel = b->vel;
            b->edge = b->open != 0 ? 1f : 0f;   // 비어 있는 이웃 칸 (지난 프레임 그리기 잡)

            if (b->wait > 0f)
            {
                // 출발 대기 (차례로 모일 때) — 제자리에서 미끄러지듯 멈춘다
                b->wait -= dt;
                vel *= math.exp(-4f * dt);
                pos += vel * dt;
                b->pos = pos; b->vel = vel;
                return;
            }

            int g = b->guide;
            float3 tgt = Target(b, out Feel f);
            float fluid = 1f - f.bind;

            // 판 — 리그 부위는 '뒷판' 값, 나머지는 목표가 두 판 사이 절반보다 안쪽이면 뒷판
            int plane = f.plane >= 0 ? f.plane : (tgt.z > (frontZ + backZ) * 0.5f ? 1 : 0);
            b->plane = (byte)plane;
            tgt.z = plane == 1 ? backZ : frontZ;

            // 그림 색 — 그림 칸 가까이 도착하면 그 칸 색이 된다
            if (f.slotColor != 0u && math.lengthsq(tgt.xy - pos.xy) < cell * cell * 4f)
            {
                uint sc = f.slotColor;
                b->tint = new UnityEngine.Color32((byte)(sc & 255u), (byte)((sc >> 8) & 255u), (byte)((sc >> 16) & 255u), 255);
            }

            // 끈적임 — 유체일수록 주변 봇과 속도를 맞춘다 → 통째로 흐른다
            if (fluid > 0.01f && viscosity > 0f && grid.w > 0)
            {
                float3 av = CellVelocity(pos.xy, vel);
                float k = 1f - math.exp(-viscosity * fluid * dt);
                vel.x += (av.x - vel.x) * k;
                vel.y += (av.y - vel.y) * k;
            }

            // 흐름 — 유체일수록 덩어리째 일렁인다 (격자 자리 위에 얹는 이어진 물결)
            if (f.lattice && f.flow > 0f && fluid > 0.01f) tgt += Flow(tgt, time) * (f.flow * fluid);

            // 떨림 — 격자 봇은 유체일 때 표면만 살짝, 격자가 없는 봇(포신 · 후광 · 눈)은 그대로
            if (f.noise > 0f)
            {
                float amp = f.lattice ? f.noise * fluid * (0.2f + 0.8f * b->edge) : f.noise;
                if (amp > 1e-5f)
                {
                    float an = time * f.nspeed + b->hash * 40f;
                    tgt += new float3(math.sin(an), math.sin(an * 1.31f + 1.7f), math.sin(an * 0.77f + 3.1f) * 0.5f) * amp;
                }
            }

            // 모이는 중(멀리 있음)이면 '모이는 시간' 으로, 가까우면 부위 느낌으로
            float omega = f.omega, zeta = f.zeta;
            if (f.gather > 0f)
            {
                float dist = math.length(tgt - pos);
                float gk = math.saturate((dist - 0.8f) / 2f);
                omega = math.lerp(omega, f.gather, gk);
                zeta = math.lerp(zeta, 0.9f, gk);
            }
            NanoSpring.Step(ref pos, ref vel, tgt, omega, zeta, dt);
            float s2 = math.lengthsq(vel);
            if (s2 > f.vmax * f.vmax) vel *= f.vmax / math.sqrt(s2);

            // 재결속 — 뿌리부터 끝까지 '착착' 잠기는 빛 물결
            if (f.lockAge >= 0f)
            {
                float d = f.lockAge - f.frac * 0.18f;
                if (d >= 0f && d < 0.08f) b->flash = math.max(b->flash, 0.9f * (1f - d / 0.08f));
            }

            // 모습 — 2D 칸: 회전 · 늘이기 없음
            float machine = f.lattice ? math.saturate((f.bind - 0.35f) / 0.3f) : 0f;
            b->roll = 0f;
            b->stretch = 0f;
            b->scale = Toward(b->scale, 1f, dt * 3f);
            b->machine = machine;
            b->space = f.lattice ? f.space : 0f;
            if (dark > 0.01f && ((g == 4 && b->seg >= 0) || g == 6))
                b->rim = Toward(b->rim, b->edge > 0.5f ? 1f : 0f, dt * 10f);

            b->pos = pos; b->vel = vel;
        }

        float3 CellVelocity(float2 p, float3 fallback)
        {
            int cx = math.clamp((int)((p.x - grid.minX) / grid.cell), 0, grid.w - 1);
            int cy = math.clamp((int)((p.y - grid.minY) / grid.cell), 0, grid.h - 1);
            float2 sum = float2.zero;
            float n = 0f;
            for (int dy = -1; dy <= 1; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= grid.h) continue;
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx;
                    if (x < 0 || x >= grid.w) continue;
                    float4 cv = cellVel[y * grid.w + x];
                    sum += cv.xy;
                    n += cv.z;
                }
            }
            return n > 0f ? new float3(sum / n, fallback.z) : fallback;
        }

        // ── 목표 자리 ────────────────────────────────────────────────
        float3 Target(NanoBot* b, out Feel f)
        {
            int g = b->guide;
            GuideFeel gf = feel[g];
            f.omega = gf.omega; f.zeta = gf.zeta; f.vmax = gf.maxSpeed; f.noise = gf.noise; f.nspeed = gf.noiseSpeed;
            f.bind = gf.bind; f.space = gf.space; f.gather = gf.gather; f.lockAge = -1f; f.frac = 0f; f.lattice = gf.lattice != 0; f.flow = gf.flow;
            f.plane = -1; f.slotColor = 0u;
            switch (g)
            {
                case 0: return BlobTarget(b, ref f);
                case 1: return TubeTarget(b, 0, tubeA, ZK(f.bind));
                case 2: return TubeTarget(b, 1, tubeB, ZK(f.bind));
                case 3: return RingTarget(b);
                case 4: return KnightTarget(b, ref f);
                case 5: return HaloTarget(b);
                default: return RigTarget(b, ref f);
            }
        }

        float3 BlobTarget(NanoBot* b, ref Feel f)
        {
            float3 vf = b->blobV;
            float ph = b->hash * 6.2832f, fs = time * blob.flowSpeed;
            vf += new float3(math.sin(fs * 0.9f + ph), math.sin(fs * 1.1f + ph * 1.7f), math.sin(fs * 0.6f + ph * 2.3f)) * blob.flow;
            float m = math.length(vf);
            if (m > 1f) vf /= m;
            float3 v = vf;
            int c = b->cell;
            if (c >= 0 && c < blobSlotCount && blob.ext > 0f)
            {
                float4 s = blobSlots[c];
                v = new float3(s.x / blob.ext, s.y / blob.ext, s.z * 0.6f);
            }
            else f.lattice = false;   // 아직 자리가 없다 (돌아오는 중) — 안쪽을 떠돈다
            f.space = blob.ext > 0f ? blob.radius.y / blob.ext : spacing;

            float ang = math.atan2(v.y, v.x), ws = time * blob.wobbleSpeed;
            float w = 1f + blob.wobble * (math.sin(3f * ang + ws) * 0.5f + math.sin(5f * ang - ws * 1.4f) * 0.3f + math.sin(2f * ang + ws * 0.6f) * 0.4f);
            float3 p = new float3(v.x * blob.radius.x * w, v.y * blob.radius.y * w, v.z * blob.radius.z * (f.lattice ? ZK(f.bind) : 1f));
            if (blob.bulge > 0f)
            {
                float dm = math.length(v.xy);
                if (dm > 1e-4f)
                {
                    float k = math.max(0f, (v.x * blob.bulgeDir.x + v.y * blob.bulgeDir.y) / dm);
                    p += blob.bulgeDir * (blob.bulge * k * k * dm);
                }
            }
            float3 pos = blob.center + p;
            float fl = blob.floorY + 0.12f;
            if (pos.y < fl)
            {
                pos.x += (pos.x - blob.center.x) * (fl - pos.y) * 0.3f;   // 바닥에 눌리면 옆으로 퍼진다
                pos.y = fl;
            }
            return pos;
        }

        float3 TubeTarget(NanoBot* b, int which, TubeSim t, float zk)
        {
            float3* nodes = tubeNodes + which * K;
            float fu = math.saturate(b->u) * (K - 1);
            int i = math.min((int)fu, K - 2);
            float a = fu - i;
            float3 p = math.lerp(nodes[i], nodes[i + 1], a);
            float3 tan = nodes[i + 1] - nodes[i];
            tan = math.lengthsq(tan) < 1e-8f ? new float3(0f, 1f, 0f) : math.normalize(tan);
            float3 n = math.cross(tan, new float3(0f, 0f, 1f));
            n = math.lengthsq(n) < 1e-6f ? new float3(1f, 0f, 0f) : math.normalize(n);
            float3 bz = math.cross(tan, n);
            float r = math.lerp(t.r0, t.r1, b->u);
            return p + n * (b->v.x * r) + bz * (b->v.y * r * zk);
        }

        /// <summary>포신 — 2D 판이라 고리 대신 꽉 찬 소용돌이 원반 (가운데로 갈수록 빨리 돈다)</summary>
        float3 RingTarget(NanoBot* b)
        {
            float rk = math.sqrt(b->v.z);
            float a = b->v.x * 6.2832f + ring.angle * (1.6f - 0.8f * rk);
            float along = (b->v.y - 0.5f) * ring.length * 0.4f;
            float3 perp = new float3(-ring.axis.y, ring.axis.x, 0f);
            float rr = ring.radius * rk;
            return ring.center + ring.axis * along + (perp * math.cos(a) + ring.axis * (math.sin(a) * 0.55f)) * rr;
        }

        float3 KnightTarget(NanoBot* b, ref Feel f)
        {
            if (b->seg < 0)
            {
                // 눈 — 머리 앞쪽, 보는 방향으로 살짝 쏠린다
                f.lattice = false;
                float3 hw = knight.head + new float3(0.12f * knight.facing, 0.02f, knight.sprite != 0 ? 0f : -0.35f);
                float3 look = knight.lookAt - hw;
                look.z = 0f;
                if (math.lengthsq(look) > 1e-4f) hw += math.normalize(look) * 0.06f;
                float sp = knight.eyeSpread > 0f ? knight.eyeSpread : 0.09f;
                return hw + new float3(b->segV.x * sp, b->segV.y * 0.03f, 0f);
            }
            if (knight.sprite != 0)
            {
                // 그림 기사 — 군집이 매 프레임 봇마다 자리를 정해 둔다 (몸 → 칼날 → 효과 순)
                int c = b->cell;
                if (c < 0 || c >= knight.slotCount)
                {
                    f.lattice = false;
                    return knight.origin + new float3(0f, 1.5f, 0f);   // 아직 자리가 없다 — 가슴 쪽으로
                }
                float4 ks = knightSlots[c];
                b->glow = ks.w > 1.5f ? knight.fxGlow : ks.w > 0.5f ? knight.bladeGlow : 0f;
                if (knight.art != 0)
                {
                    uint kc = knightColors[c];
                    b->tint = new UnityEngine.Color32((byte)(kc & 255u), (byte)((kc >> 8) & 255u), (byte)((kc >> 16) & 255u), 255);
                }
                f.space = cell;
                return ks.xyz;
            }
            int s = math.clamp(b->seg, 0, KnightSegs - 1);
            float4 sg = knightSegs[s];
            float2 A = sg.xy, B = sg.zw;
            float2 p = math.lerp(A, B, b->segU);
            float2 d = B - A;
            float2 n = math.lengthsq(d) > 1e-6f ? math.normalize(new float2(-d.y, d.x)) : new float2(1f, 0f);
            float2 rr = knightRad[s];
            float r = math.lerp(rr.x, rr.y, b->segU);
            float2 q = p + n * (b->segV.x * r);
            return knight.origin + new float3(q.x * knight.facing, q.y, b->segV.y * r * 0.45f * ZK(f.bind));
        }

        float3 HaloTarget(NanoBot* b)
        {
            float a = b->v.x * 6.2832f + time * halo.spin * (b->v.z > 0.5f ? 1f : -0.7f);
            float r = halo.radius * (0.7f + 0.6f * b->v.y);
            return halo.center + new float3(math.cos(a) * r, math.sin(a) * r * 0.75f, 1.4f + b->v.z * 1.2f);
        }

        // ── 리그 부위 ────────────────────────────────────────────────
        float3 RigTarget(NanoBot* b, ref Feel f)
        {
            int li = b->slot;
            if (li < 0 || li >= limbCount) { f.lattice = false; return b->pos; }
            LimbSim* L = limbs + li;
            f.omega = L->omega; f.zeta = L->zeta; f.vmax = L->maxSpeed; f.noise = L->flow * 0.15f; f.nspeed = 2.2f;
            f.space = L->space; f.gather = L->gatherOmega; f.lockAge = L->lockAge; f.flow = L->flow;
            bool has = b->cell >= 0 && b->cell < L->slotCount;
            float4 slot = has ? limbSlots[L->slotStart + b->cell] : float4.zero;
            f.lattice = has;
            f.frac = slot.w;
            f.bind = has ? math.saturate(L->bind) : 0f;
            f.plane = L->plane;
            if (has && L->artColor != 0) f.slotColor = limbSlotColor[L->slotStart + b->cell];
            float lw = has ? 1f : 0f;   // 자리가 없으면 (아직 배정 전) 부위 안 무작위 자리
            switch (L->type)
            {
                case 0: return ChainTarget(b, L, slot, lw, ZK(f.bind));
                case 1: return LimbBlobTarget(b, L, slot, lw, ZK(f.bind));
                case 3: return SpriteTarget(b, L, slot, has);
                case 4: f.lattice = false; return RoamTarget(b, L, slot);
                default: return PuddleTarget(b, L, slot, lw);
            }
        }

        float3 ChainTarget(NanoBot* b, LimbSim* L, float4 slot, float bind, float zk)
        {
            bool floor = L->fillToFloor != 0;
            float fu = b->hash, fa = floor ? b->hash2 : b->hash2 * 2f - 1f, fz = b->hash3 * 2f - 1f;
            float u = math.lerp(fu, slot.x, bind), a = math.lerp(fa, slot.y, bind), z = math.lerp(fz, slot.z, bind);
            float lagT = L->lag > 0f ? L->lag * u * (0.4f + 0.6f * b->hash2) : 0f;
            SampleSpine(L, u * math.saturate(L->grow), clock - lagT, out float3 p, out float3 tan, out float r);
            if (floor)
            {
                // 관절선(물마루)에서 바닥까지 기둥처럼 채운다
                float top = p.y + r;
                float y = math.lerp(floorY + 0.1f, top, math.saturate(a));
                return new float3(p.x, y, p.z + z * L->depth * 1.5f * zk);
            }
            float3 nrm = math.cross(tan, new float3(0f, 0f, 1f));
            nrm = math.lengthsq(nrm) < 1e-6f ? new float3(1f, 0f, 0f) : math.normalize(nrm);
            return p + nrm * (a * r) + new float3(0f, 0f, z * r * L->depth * zk);
        }

        float3 LimbBlobTarget(NanoBot* b, LimbSim* L, float4 slot, float bind, float zk)
        {
            float3 v = math.lerp((float3)b->blobV, slot.xyz, bind);
            v.z *= zk;
            float ang = math.atan2(v.y, v.x);
            float w = 1f + L->wobble * (math.sin(3f * ang + time * 2f + b->hash * 2f * (1f - bind)) * 0.6f + math.sin(5f * ang - time * 2.7f) * 0.4f);
            float3 p = math.transform(L->local, v * w);
            if (L->lag > 0f)
            {
                float lagT = L->lag * (0.4f + 0.6f * b->hash2);
                SampleSpine(L, 0f, clock - lagT, out float3 then, out _, out _);
                SampleSpine(L, 0f, clock, out float3 now, out _, out _);
                p += then - now;
            }
            return p;
        }

        /// <summary>그림 (스프라이트 시트) — 자리표가 지금 프레임의 칠한 픽셀 위치 (부위 좌표, m)</summary>
        float3 SpriteTarget(NanoBot* b, LimbSim* L, float4 slot, bool has)
        {
            float3 p = math.transform(L->local, has ? slot.xyz : float3.zero);
            if (L->lag > 0f)
            {
                float lagT = L->lag * (0.4f + 0.6f * b->hash2);
                SampleSpine(L, 0f, clock - lagT, out float3 then, out _, out _);
                SampleSpine(L, 0f, clock, out float3 now, out _, out _);
                p += then - now;
            }
            return p;
        }

        /// <summary>무리 — 봇마다 제 궤도로 부위 주위를 날아다닌다 (개별 움직임). 자리 = (단위 원 x, y, 위상)</summary>
        float3 RoamTarget(NanoBot* b, LimbSim* L, float4 slot)
        {
            float sp = L->roamSpeed * (0.6f + 0.8f * b->hash2);
            float a = time * sp + slot.z * 6.2832f;
            float wob = 0.35f * math.sin(time * sp * 1.7f + b->hash * 13f);
            float2 orbit = new float2(math.cos(a), math.sin(a * (1f + 0.37f * b->hash3)));
            float2 home = new float2(slot.x, slot.y) * 0.65f;
            float2 q = home + orbit * (0.35f + wob * 0.3f);
            float3 c = L->origin;
            if (L->lag > 0f)
            {
                float lagT = L->lag * (0.4f + 0.6f * b->hash2);
                SampleSpine(L, 0f, clock - lagT, out float3 then, out _, out _);
                c = then;
            }
            return c + new float3(q.x * L->roam.x * L->sx, q.y * L->roam.y, 0f);
        }

        float3 PuddleTarget(NanoBot* b, LimbSim* L, float4 slot, float bind)
        {
            float nx = math.lerp(b->hash - 0.5f, slot.x, bind);
            float ny = math.lerp(b->hash2, slot.y, bind);
            float3 c = L->origin;
            float x = c.x + nx * L->extent.x * L->sx;
            float z = c.z;
            float y = floorY + cell * 0.5f + L->layer * ny;
            float dist = math.abs(x - c.x);
            y += L->ripple * (0.5f + 0.5f * math.sin(dist * 2.6f - time * L->rippleSpeed + b->hash * 1.5f * (1f - 0.7f * bind)));
            if (L->hasBump != 0 && L->bump > 0f)
            {
                float3 bp = L->bumpPos;
                float dx = x - bp.x, dz = z - bp.z;
                float d2 = (dx * dx + dz * dz) / math.max(0.01f, L->bumpRadius * L->bumpRadius);
                float k = math.exp(-d2);
                x = math.lerp(x, bp.x, k * 0.6f);   // 모여들며 솟고 요동친다
                z = math.lerp(z, bp.z, k * 0.6f);
                float shake = 1f + L->bumpShake * math.sin(time * 26f + b->hash * 13f);
                y += L->bump * k * shake * (0.5f + ny);
            }
            return new float3(x, y, z);
        }

        /// <summary>관절선 기록에서 시점 when 의 u 자리 (위치 · 접선 · 반지름)</summary>
        void SampleSpine(LimbSim* L, float u, float when, out float3 p, out float3 tan, out float r)
        {
            int cnt = L->histCount;
            if (cnt <= 0) { p = L->origin; tan = new float3(0f, 1f, 0f); r = 0f; return; }
            int head = L->histHead;
            int fa = head, fb = head;
            float t = 0f;
            if (when < spineTime[L->histBase + head] - 1e-5f)
            {
                int newer = head;
                bool found = false;
                for (int k = 1; k < cnt; k++)
                {
                    int idx = (head - k + H) % H;
                    float ti = spineTime[L->histBase + idx];
                    if (ti <= when)
                    {
                        float span = spineTime[L->histBase + newer] - ti;
                        fa = idx; fb = newer;
                        t = span > 1e-5f ? (when - ti) / span : 0f;
                        found = true;
                        break;
                    }
                    newer = idx;
                }
                if (!found) { fa = fb = (head - cnt + 1 + H) % H; t = 0f; }
            }
            float fs = math.saturate(u) * (S - 1);
            int i0 = math.min((int)fs, S - 2);
            float a = fs - i0;
            float4* A = spine + (L->histBase + fa) * S;
            float4* B = spine + (L->histBase + fb) * S;
            float4 s0 = math.lerp(A[i0], B[i0], t);
            float4 s1 = math.lerp(A[i0 + 1], B[i0 + 1], t);
            float4 sp = math.lerp(s0, s1, a);
            p = sp.xyz;
            r = sp.w;
            float3 d = s1.xyz - s0.xyz;
            tan = math.lengthsq(d) < 1e-10f ? new float3(0f, 1f, 0f) : math.normalize(d);
        }

        // ── 날아가는 봇 · 떨어진 봇 ───────────────────────────────────
        void Free(NanoBot* b)
        {
            float3 pos = b->pos, vel = b->vel;
            if (b->wait > 0f)
            {
                // 대열에서 대기 (예고)
                b->wait -= dt;
                NanoSpring.Step(ref pos, ref vel, b->hold, 19.5f, 0.667f, dt);
                float s2 = math.lengthsq(vel);
                if (s2 > 3600f) vel *= 60f / math.sqrt(s2);
                if (b->wait <= 0f) vel = b->launch;
                b->stretch = 0f;
            }
            else
            {
                vel += (float3)b->accel * dt;
                pos += vel * dt;
                float speed = math.length(vel);
                if (speed > 10f)
                {
                    b->roll = math.atan2(vel.y, vel.x);
                    b->stretch = math.saturate((speed - 10f) / 30f) * 1.2f;   // 빠른 탄은 궤적 방향으로 늘어난다
                }
                else { b->roll += b->spin * dt; b->stretch = 0f; }
                b->life -= dt;
                if (b->life <= 0f || pos.y < floorY - 0.5f)
                {
                    b->mode = BotMode.Bound;
                    b->guide = b->home;
                    vel *= 0.2f;
                    b->rim = 0f;
                    b->cell = -1;
                }
            }
            b->edge = math.max(0f, b->edge - dt * 4f);
            b->machine = 0f;
            b->roll = 0f; b->stretch = 0f;
            // 판 — 탄은 가까운 판 위를 난다
            float pz = pos.z > (frontZ + backZ) * 0.5f ? backZ : frontZ;
            b->plane = (byte)(pz == backZ ? 1 : 0);
            pos.z += (pz - pos.z) * (1f - math.exp(-10f * dt));
            b->pos = pos; b->vel = vel;
        }

        /// <summary>떨어진 봇 — 날고 있는 동안만 (칸에 앉은 모래는 NanoSandJob 이 움직인다)</summary>
        void Debris(NanoBot* b)
        {
            b->roll = 0f; b->stretch = 0f; b->machine = 0f; b->edge = 0f; b->scale = 1f;
            if (b->sand >= 0) return;
            float3 pos = b->pos, vel = b->vel;
            vel.y -= gravity * dt;
            vel.x *= math.exp(-0.6f * dt);
            pos += vel * dt;
            float pz = b->plane == 1 ? backZ : frontZ;
            pos.z += (pz - pos.z) * (1f - math.exp(-12f * dt));
            b->pos = pos; b->vel = vel;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 모래
    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// 떨어진 봇(Debris)을 모래로. 판마다 고정 범위 칸(occ, 0 빔 · 1 모래)에 앉힌다.
    ///   날고 있음 (sand &lt; 0): 지난 위치 → 지금 위치 사이 세로 칸을 훑어 바닥 · 더미에 닿으면 그 위 빈 칸에 앉음
    ///   앉음: 아래가 비면 다시 날기 시작, 아니면 대각선 아래(좌우 무작위), 유체에서 떨어진 봇은 옆으로도 퍼짐
    ///   떨어진 봇이 아니게 되면 (재흡수 · 새로 시작) 칸을 비운다
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct NanoSandJob : IJob
    {
        [NativeDisableUnsafePtrRestriction] public NanoBot* bots;
        [NativeDisableUnsafePtrRestriction] public byte* occ;
        public int count, w, h, frame;
        public float minX, minY, cell, frontZ, backZ, dt, slide;

        static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
            return x;
        }

        public void Execute()
        {
            int plane = w * h;
            for (int i = 0; i < count; i++)
            {
                NanoBot* b = bots + i;
                if (b->sand >= 0 && b->mode != BotMode.Debris) { occ[b->sand] = 0; b->sand = -1; continue; }
                if (b->mode != BotMode.Debris) continue;

                int pl = b->plane == 1 ? 1 : 0;
                byte* g = occ + pl * plane;
                float pz = pl == 1 ? backZ : frontZ;
                uint rnd = Hash((uint)i * 9781u + (uint)frame * 6271u);

                if (b->sand < 0)
                {
                    // ── 날고 있음 ──
                    float3 p = b->pos;
                    int cx = (int)math.floor((p.x - minX) / cell);
                    if (cx < 0 || cx >= w)
                    {
                        // 범위 밖 — 바닥에 멈춘다
                        if (p.y < minY + cell * 0.5f) { p.y = minY + cell * 0.5f; b->pos = p; b->vel = float3.zero; }
                        continue;
                    }
                    if (b->vel.y > 0f) continue;   // 올라가는 중
                    int cy = (int)math.floor((p.y - minY) / cell);
                    if (cy >= h) continue;
                    float prevY = p.y - b->vel.y * dt;
                    int top = math.clamp((int)math.floor((prevY - minY) / cell), 0, h - 1);
                    int land = int.MinValue;
                    for (int r = top; r >= cy - 1; r--)
                    {
                        if (r < 0 || g[r * w + cx] != 0) { land = r + 1; break; }
                    }
                    if (land == int.MinValue) continue;
                    while (land < h && g[land * w + cx] != 0) land++;
                    if (land >= h) continue;
                    int c = land * w + cx;
                    g[c] = 1;
                    b->sand = pl * plane + c;
                    b->pos = new float3(minX + (cx + 0.5f) * cell, minY + (land + 0.5f) * cell, pz);
                    b->vel = float3.zero;
                    continue;
                }

                // ── 앉은 모래 ──
                int idx = b->sand - pl * plane;
                int x = idx % w, y = idx / w;
                if (y > 0 && g[(y - 1) * w + x] == 0)
                {
                    // 아래가 비었다 — 다시 떨어진다
                    g[idx] = 0;
                    b->sand = -1;
                    b->vel = new float3(0f, -1.5f, 0f);
                    continue;
                }
                if (y == 0 && b->liquid == 0) continue;
                if ((rnd & 1023u) / 1024f > slide) continue;
                int dir = (rnd & 1024u) != 0 ? 1 : -1;
                int moved = -1;
                if (y > 0)
                {
                    for (int t = 0; t < 2 && moved < 0; t++)
                    {
                        int nx = x + (t == 0 ? dir : -dir);
                        if (nx < 0 || nx >= w) continue;
                        if (g[(y - 1) * w + nx] == 0 && g[y * w + nx] == 0) moved = (y - 1) * w + nx;
                    }
                }
                if (moved < 0 && b->liquid != 0 && (rnd & 2048u) != 0)
                {
                    // 유체 — 옆으로 퍼진다
                    for (int t = 0; t < 2 && moved < 0; t++)
                    {
                        int nx = x + (t == 0 ? dir : -dir);
                        if (nx < 0 || nx >= w) continue;
                        if (g[y * w + nx] == 0) moved = y * w + nx;
                    }
                }
                if (moved < 0) continue;
                g[idx] = 0;
                g[moved] = 1;
                b->sand = pl * plane + moved;
                b->pos = new float3(minX + (moved % w + 0.5f) * cell, minY + (moved / w + 0.5f) * cell, pz);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // GPU 로 보낼 값
    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// 봇마다 칸에 맞춘 위치 · 색 · 발광. 판 위 봇은 칸 비트맵(앞 · 뒤 판, 모래 범위와 같은 칸)에 찍어
    /// 비어 있는 이웃(위 · 아래 · 왼 · 오른)을 찾는다 → 셰이더가 가장자리 조명 · 3페 흰 테두리에 쓴다.
    /// 범위 밖의 봇은 이웃을 모른다 (비어 있는 것으로 본다).
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct NanoUploadJob : IJob
    {
        [NativeDisableUnsafePtrRestriction] public NanoBot* bots;
        [NativeDisableUnsafePtrRestriction] public NanoGpuBot* gpu;
        [NativeDisableUnsafePtrRestriction] public GuideFeel* feel;
        [NativeDisableUnsafePtrRestriction] public LimbSim* limbs;
        [NativeDisableUnsafePtrRestriction] public float3* bounds;
        [NativeDisableUnsafePtrRestriction] public uint* occ;
        public int count, limbCount, highlightDanger, w, h, freeSize, eyeSize;
        public float freeGlow, minX, minY, cell, frontZ, backZ, backDim;
        public uint freeTint;

        static uint Mul(uint c, uint t)
        {
            uint r = ((c & 255u) * (t & 255u)) / 255u;
            uint g = (((c >> 8) & 255u) * ((t >> 8) & 255u)) / 255u;
            uint b = (((c >> 16) & 255u) * ((t >> 16) & 255u)) / 255u;
            return r | (g << 8) | (b << 16) | (255u << 24);
        }

        static uint Scale(uint c, float k)
        {
            uint r = (uint)math.min(255f, (c & 255u) * k);
            uint g = (uint)math.min(255f, ((c >> 8) & 255u) * k);
            uint b = (uint)math.min(255f, ((c >> 16) & 255u) * k);
            return r | (g << 8) | (b << 16) | (255u << 24);
        }

        /// <summary>판 위인가 (0 앞 · 1 뒤 · -1 판 사이를 나는 중)</summary>
        int PlaneOf(float z)
        {
            float tol = math.max(0.15f, math.abs(backZ - frontZ) * 0.04f);
            if (math.abs(z - frontZ) < tol) return 0;
            if (math.abs(z - backZ) < tol) return 1;
            return -1;
        }

        bool Get(int pl, int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return false;
            int k = pl * w * h + y * w + x;
            return (occ[k >> 5] & (1u << (k & 31))) != 0u;
        }

        public void Execute()
        {
            int words = (w * h * 2 + 31) >> 5;
            for (int k = 0; k < words; k++) occ[k] = 0u;

            // 1. 판 위 봇을 칸 비트맵에 찍는다
            for (int i = 0; i < count; i++)
            {
                NanoBot* b = bots + i;
                if (b->mode == BotMode.Dead || b->scale < 0.5f) continue;
                if (b->mode == BotMode.Bound && b->guide == 5) continue;   // 후광(3페 탄약)도 — 기사 몸 뒤에 따로 그린다
                if (b->mode == BotMode.Bound && b->guide == 4 && b->seg < 0) continue;   // 기사 눈 — 몸 위에 따로
                int pl = PlaneOf(b->pos.z);
                if (pl < 0) continue;
                int x = (int)math.floor((b->pos.x - minX) / cell), y = (int)math.floor((b->pos.y - minY) / cell);
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                int k = pl * w * h + y * w + x;
                occ[k >> 5] |= 1u << (k & 31);
            }

            // 2. 봇마다 칸 위치 · 비어 있는 이웃 · 색
            float3 mn = new float3(float.MaxValue), mx = new float3(float.MinValue);
            for (int i = 0; i < count; i++)
            {
                NanoBot* b = bots + i;
                bool show = b->mode != BotMode.Dead && b->scale >= 0.5f;
                float glow = b->glow;
                uint c = (uint)b->tint.r | ((uint)b->tint.g << 8) | ((uint)b->tint.b << 16) | (255u << 24);
                if (b->mode == BotMode.Bound)
                {
                    int g = b->guide;
                    if (g == 6)
                    {
                        if (b->wait <= 0f && b->slot >= 0 && b->slot < limbCount)
                        {
                            LimbSim* L = limbs + b->slot;
                            if (L->glow > 0f) { glow += L->glow; c = L->tint; }
                            else if (L->tinted != 0) c = Mul(c, L->tint);
                            if (highlightDanger != 0 && L->danger != 0) c = 0xFF5078FFu;   // 주황 (ABGR)
                        }
                    }
                    else
                    {
                        glow += feel[g].glow;
                        if (g != 0 && b->seg != -1) c = Mul(c, feel[g].tint);
                    }
                }
                else if (b->mode == BotMode.Free)
                {
                    glow += freeGlow;
                    c = Mul(c, freeTint);
                }

                float3 p = b->pos;
                int pl = PlaneOf(p.z);
                int gx = (int)math.floor((p.x - minX) / cell), gy = (int)math.floor((p.y - minY) / cell);
                // 칸에 맞춘다 (모래 범위 밖도 같은 칸 눈금)
                p.x = minX + (gx + 0.5f) * cell;
                p.y = minY + (gy + 0.5f) * cell;
                uint open = 15u;
                if (pl >= 0)
                {
                    p.z = pl == 1 ? backZ : frontZ;
                    bool halo = b->mode == BotMode.Bound && b->guide == 5;
                    bool eye = b->mode == BotMode.Bound && b->guide == 4 && b->seg < 0;
                    if (halo) p.z += 0.25f;   // 기사 몸 바로 뒤 (같은 판이면 겹쳐 깜빡인다)
                    if (eye) { p.z -= 0.08f; halo = true; }   // 기사 눈 — 몸 바로 앞
                    if (!halo && gx >= 0 && gy >= 0 && gx < w && gy < h)
                    {
                        open = 0u;
                        if (!Get(pl, gx, gy + 1)) open |= 1u;
                        if (!Get(pl, gx, gy - 1) && gy > 0) open |= 2u;
                        if (!Get(pl, gx - 1, gy)) open |= 4u;
                        if (!Get(pl, gx + 1, gy)) open |= 8u;
                    }
                    if (pl == 1) c = Scale(c, backDim);
                }
                b->open = (byte)open;
                float size = show ? cell * (b->mode == BotMode.Free ? math.max(1, freeSize)
                                            : b->mode == BotMode.Bound && b->guide == 4 && b->seg < 0 ? math.max(1, eyeSize) : 1) : 0f;
                gpu[i] = new NanoGpuBot
                {
                    pos = p,
                    size = size,
                    flash = b->flash,
                    glow = glow,
                    rim = b->rim,
                    open = open,
                    color = c,
                };
                if (show)
                {
                    mn = math.min(mn, p);
                    mx = math.max(mx, p);
                }
            }
            bounds[0] = mn;
            bounds[1] = mx;
        }
    }
}
