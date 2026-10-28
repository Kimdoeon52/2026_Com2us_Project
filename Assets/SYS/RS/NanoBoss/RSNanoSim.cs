// RE:AL STEEL - 나노봇 시뮬레이션 (Burst 잡)
//
// 봇 5,000 개를 GTX 1060 3GB 급 PC 에서 60fps 로 돌리려고 봇 계산을 전부 Burst 잡으로 옮겼다.
//   NanoGridJob    봇 위치를 화면 평면 격자에 나눠 담는다 (가장자리 판정 · 끈적임에 쓴다). 한 스레드
//   NanoSimJob     봇마다: 목표 자리 계산 → 흐름 · 끈적임 → 감쇠 스프링(정확해) → 모습(각도 · 늘이기 · 판). 여러 스레드
//   NanoUploadJob  GPU 로 보낼 값 (판 그림 고르기 · 색 · 발광) + 경계 상자. 한 스레드
//
// 목표 자리는 가이드 · 부위가 매 프레임 미리 채워 둔 값(GuideFeel · LimbSim · 격자 자리표 · 관절선 기록)으로만 계산한다.
// 관리 코드(가이드 클래스 · 부위 컴포넌트)는 잡 안에서 부르지 않는다.
//
// 스프링: 감쇠 진동의 정확해를 쓴다 → 서브스텝 없이도 안정, 같은 입력이면 같은 결과 (편집기 되감기 재시뮬)
//
// 깊이(z): 기계(결속 1)일수록 화면 안쪽 두께를 얇게 눌러 판이 한 장의 면처럼 맞물린다 (두꺼우면 원근 때문에
//          판이 위아래로 어긋나 '물에 뜬 시리얼' 처럼 보인다). 유체일수록 원래 두께.
// 결속(bind): 봇은 늘 자기 격자 자리를 갖는다. 기계(1)면 자리에 딱 맞물린 판, 유체(0)면 자리 위에 '흐름'(공간적으로
// 이어진 물결 변위)이 얹히고 끈적임으로 이웃과 함께 움직이며 둥근 방울로 그려진다 → 흩어진 먼지가 아니라 흐르는 덩어리.
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
    public struct KnightSim { public float3 origin, lookAt, head; public float facing; }
    public struct HaloSim { public float3 center; public float radius, spin; }

    /// <summary>리그 부위 하나 (잡으로 넘기는 값)</summary>
    public struct LimbSim
    {
        public int type;                      // 0 줄기 · 1 덩어리 · 2 웅덩이 · 3 그림
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
        public float glow;
        public uint tint;
        public int tinted, danger;
    }

    public struct GridInfo { public float minX, minY, cell; public int w, h; }

    [StructLayout(LayoutKind.Sequential)]
    public struct NanoGpuBot
    {
        public float3 pos;
        public float roll, scale, flash, glow, rim, stretch;
        public uint tile, color;
        public float pad;
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

        public GridInfo grid;
        public BlobSim blob;
        public TubeSim tubeA, tubeB;
        public RingSim ring;
        public KnightSim knight;
        public HaloSim halo;
        public int limbCount, blobSlotCount, frame;
        public float dt, time, clock, floorY, gravity, bounce, friction, flashDecay, half, viscosity, edgeRate, dark, spacing;

        struct Feel
        {
            public float omega, zeta, vmax, noise, nspeed, bind, space, gather, lockAge, frac, flow;
            public bool lattice;
        }

        /// <summary>공간적으로 이어진 흐름 변위 (이웃끼리 비슷하게 움직인다)</summary>
        /// <summary>깊이 배율 — 기계일수록 얇게</summary>
        static float ZK(float bind) => math.lerp(1f, 0.06f, math.saturate(bind));

        static float3 Flow(float3 p, float t)
        {
            float x = math.sin(p.y * 1.6f + t * 1.7f) + 0.6f * math.sin(p.x * 2.9f - t * 2.3f + p.y * 1.1f);
            float y = math.sin(p.x * 1.4f - t * 1.3f) + 0.6f * math.sin(p.y * 3.1f + t * 2.1f + p.x * 0.9f);
            float z = 0.5f * math.sin((p.x - p.y) * 1.2f + t * 1.1f);
            return new float3(x, y, z) * 0.55f;
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
            float sp = b->space > 0f ? b->space : spacing;
            if (((i + frame) & 1) == 0) b->edge = Toward(b->edge, EdgeOf(i, pos.xy, sp), 2f * dt * edgeRate);   // 두 프레임에 한 번씩 번갈아

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

            // 모습 — 기계: 각도 0 · 판 · 늘이지 않음 / 유체: 둥근 방울 · 속도 방향으로 늘이기
            float speed = math.sqrt(math.lengthsq(vel));
            float machine = f.lattice ? math.saturate((f.bind - 0.35f) / 0.3f) : 0f;
            float kk = 1f - math.exp(-12f * dt);
            float rollT;
            if (machine > 0.5f) rollT = 0f;
            else if (speed > 2f) rollT = math.atan2(vel.y, vel.x);
            else rollT = f.lattice ? 0f : (b->hash - 0.5f) * 0.6f;
            b->roll += WrapPi(rollT - b->roll) * kk;
            float stretchT = (1f - machine) * math.saturate((speed - 3f) / 18f) * (f.lattice ? 1.4f * fluid : 0.8f);
            b->stretch = math.lerp(b->stretch, stretchT, kk);
            float scaleT = f.lattice ? math.lerp(1.2f, 1f, machine) : 0.8f + b->hash2 * 0.35f;
            b->scale = Toward(b->scale, scaleT, dt * 3f);
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

        /// <summary>주변 8 방향 중 3 방향 이상이 비었으면 실루엣 가장자리</summary>
        float EdgeOf(int self, float2 p, float space)
        {
            if (grid.w <= 0) return 0f;
            float r = math.clamp(space * 1.9f, 0.04f, grid.cell);
            float r2 = r * r;
            int cx = math.clamp((int)((p.x - grid.minX) / grid.cell), 0, grid.w - 1);
            int cy = math.clamp((int)((p.y - grid.minY) / grid.cell), 0, grid.h - 1);
            int mask = 0;
            // 제 칸부터 → 8 방향이 다 차면 바로 끝 (안쪽 봇은 금방 끝난다)
            for (int o = 0; o < 9; o++)
            {
                int dx = o == 0 ? 0 : o == 1 ? 1 : o == 2 ? -1 : o == 3 ? 0 : o == 4 ? 0 : o == 5 ? 1 : o == 6 ? -1 : o == 7 ? 1 : -1;
                int dy = o == 0 ? 0 : o == 1 ? 0 : o == 2 ? 0 : o == 3 ? 1 : o == 4 ? -1 : o == 5 ? 1 : o == 6 ? -1 : o == 7 ? -1 : 1;
                int x = cx + dx, y = cy + dy;
                if (x < 0 || x >= grid.w || y < 0 || y >= grid.h) continue;
                int c = y * grid.w + x, s0 = cellStart[c], n = cellCount[c];
                for (int k = s0; k < s0 + n; k++)
                {
                    if (sorted[k] == self) continue;
                    float2 d = sortedPos[k] - p;
                    float d2 = math.dot(d, d);
                    if (d2 > r2 || d2 < 1e-10f) continue;
                    mask |= 1 << Octant(d);
                }
                if (mask == 0xFF) return 0f;
            }
            return 8 - math.countbits(mask) >= 3 ? 1f : 0f;
        }

        static int Octant(float2 d)
        {
            int o = 0;
            if (d.y < 0f) { d = -d; o = 4; }
            if (d.x <= 0f) { d = new float2(d.y, -d.x); o += 2; }
            if (d.y > d.x) o += 1;
            return o;
        }

        // ── 목표 자리 ────────────────────────────────────────────────
        float3 Target(NanoBot* b, out Feel f)
        {
            int g = b->guide;
            GuideFeel gf = feel[g];
            f.omega = gf.omega; f.zeta = gf.zeta; f.vmax = gf.maxSpeed; f.noise = gf.noise; f.nspeed = gf.noiseSpeed;
            f.bind = gf.bind; f.space = gf.space; f.gather = gf.gather; f.lockAge = -1f; f.frac = 0f; f.lattice = gf.lattice != 0; f.flow = gf.flow;
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

        float3 RingTarget(NanoBot* b)
        {
            float a = b->v.x * 6.2832f + ring.angle;
            float along = (b->v.y - 0.5f) * ring.length;
            float3 perp = new float3(-ring.axis.y, ring.axis.x, 0f);
            float rr = ring.radius * (0.8f + 0.2f * b->v.z);
            return ring.center + ring.axis * along + (perp * math.cos(a) + new float3(0f, 0f, 1f) * math.sin(a)) * rr;
        }

        float3 KnightTarget(NanoBot* b, ref Feel f)
        {
            if (b->seg < 0)
            {
                // 눈 — 머리 앞쪽, 보는 방향으로 살짝 쏠린다
                f.lattice = false;
                float3 hw = knight.head + new float3(0.12f * knight.facing, 0.02f, -0.35f);
                float3 look = knight.lookAt - hw;
                look.z = 0f;
                if (math.lengthsq(look) > 1e-4f) hw += math.normalize(look) * 0.06f;
                return hw + new float3(b->segV.x * 0.09f, b->segV.y * 0.03f, 0f);
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
            float lw = has ? 1f : 0f;   // 자리가 없으면 (아직 배정 전) 부위 안 무작위 자리
            switch (L->type)
            {
                case 0: return ChainTarget(b, L, slot, lw, ZK(f.bind));
                case 1: return LimbBlobTarget(b, L, slot, lw, ZK(f.bind));
                case 3: return SpriteTarget(b, L, slot, has);
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

        float3 PuddleTarget(NanoBot* b, LimbSim* L, float4 slot, float bind)
        {
            float nx = math.lerp(b->hash - 0.5f, slot.x, bind);
            float ny = math.lerp(b->hash2, slot.y, bind);
            float nz = math.lerp(b->hash3 - 0.5f, slot.z, bind);
            float3 c = L->origin;
            float x = c.x + nx * L->extent.x * L->sx;
            float z = c.z + nz * L->extent.y;
            float y = floorY + 0.08f + L->layer * ny;
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
            b->pos = pos; b->vel = vel;
        }

        void Debris(NanoBot* b)
        {
            float3 pos = b->pos, vel = b->vel;
            vel.y -= gravity * dt;
            pos += vel * dt;
            b->roll += b->spin * dt;
            float fl = floorY + half * 0.6f;
            if (pos.y <= fl)
            {
                pos.y = fl;
                vel.y = vel.y < -1.5f ? -vel.y * bounce : 0f;
                float fr = math.exp(-friction * dt);
                vel.x *= fr; vel.z *= fr;
                b->spin *= fr;
            }
            b->stretch = 0f;
            b->machine = 0f;
            b->edge = math.max(0f, b->edge - dt * 4f);
            b->pos = pos; b->vel = vel;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // GPU 로 보낼 값
    // ─────────────────────────────────────────────────────────────
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct NanoUploadJob : IJob
    {
        [NativeDisableUnsafePtrRestriction] public NanoBot* bots;
        [NativeDisableUnsafePtrRestriction] public NanoGpuBot* gpu;
        [NativeDisableUnsafePtrRestriction] public GuideFeel* feel;
        [NativeDisableUnsafePtrRestriction] public LimbSim* limbs;
        [NativeDisableUnsafePtrRestriction] public float3* bounds;
        public int count, limbCount, highlightDanger, plates;
        public float freeGlow;
        public uint freeTint;

        static uint Mul(uint c, uint t)
        {
            uint r = ((c & 255u) * (t & 255u)) / 255u;
            uint g = (((c >> 8) & 255u) * ((t >> 8) & 255u)) / 255u;
            uint b = (((c >> 16) & 255u) * ((t >> 16) & 255u)) / 255u;
            return r | (g << 8) | (b << 16) | (255u << 24);
        }

        static uint Mix(uint a, uint b)
        {
            uint r = (((a & 255u) + (b & 255u)) >> 1);
            uint g = ((((a >> 8) & 255u) + ((b >> 8) & 255u)) >> 1);
            uint bl = ((((a >> 16) & 255u) + ((b >> 16) & 255u)) >> 1);
            return r | (g << 8) | (bl << 16) | (255u << 24);
        }

        public void Execute()
        {
            float3 mn = new float3(float.MaxValue), mx = new float3(float.MinValue);
            for (int i = 0; i < count; i++)
            {
                NanoBot* b = bots + i;
                float scale = b->mode == BotMode.Dead ? 0f : b->scale;
                float glow = b->glow;
                uint c = (uint)b->tint.r | ((uint)b->tint.g << 8) | ((uint)b->tint.b << 16) | ((uint)b->tint.a << 24);
                uint tile = b->tileLoose;
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
                    if (plates != 0 && b->space > 0f)
                    {
                        // 격자 봇: 기계 = 몸 판 (가장자리는 테두리 판), 유체 = 방울. 그림은 봇 종류 (RSNanoBotKinds)
                        bool machine = b->machine > 0.5f;
                        tile = machine ? (b->edge > 0.5f ? b->tileEdge : b->tileBody) : b->tileDrop;
                        if (machine && b->glow > 0.5f)
                        {
                            // 반짝이 봇은 판 위에선 은은하게 (밝은 판이 점점이 박히면 덩어리가 아니라 낱알로 읽힌다)
                            glow -= b->glow * 0.8f;
                            c = Mix(c, 0xFFC8C0B8u);
                        }
                    }
                }
                else if (b->mode == BotMode.Free)
                {
                    glow += freeGlow;
                    c = Mul(c, freeTint);
                }
                gpu[i] = new NanoGpuBot
                {
                    pos = b->pos,
                    roll = b->roll,
                    scale = scale,
                    flash = b->flash,
                    glow = glow,
                    rim = b->rim,
                    stretch = b->stretch,
                    tile = tile,
                    color = c,
                };
                if (scale > 0f)
                {
                    float3 p = b->pos;
                    mn = math.min(mn, p);
                    mx = math.max(mx, p);
                }
            }
            bounds[0] = mn;
            bounds[1] = mx;
        }
    }
}
