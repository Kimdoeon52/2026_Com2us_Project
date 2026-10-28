// RE:AL STEEL - 나노봇 보스 두뇌: 헬 3페 (각성한 기사)
//
// 1 · 2페 = 본능만 있는 군집. 3페 = 지성이 생긴 악의.
// 차이는 '행동에 의도가 생기는 것'으로 보여 준다
//   형태   무너지는 덩어리 → 압축된 검은 실루엣 (흰 테두리)
//   시선   없음 → 눈이 생겨 플레이어를 계속 따라봄
//   움직임 출렁임 → 멈춰서 지켜보다가 끊기듯 순간이동
//   공격   덩어리째 들이받기 → 기하학적으로 정돈된 탄막 (호 · 격자 · 고리 · 나선), 화면을 덮는 밀도
//   리듬   쉬지 않고 몰아침 → 정적 → 폭발
//
// 탄은 전부 봇이다. 기사 뒤를 도는 '후광' 봇이 탄약이고, 쏜 탄은 수명이 끝나면 후광으로 돌아온다.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealSteel.NanoBoss
{
    public partial class RSNanoBossBrain
    {
        public static readonly string[] KnightPatterns = { "발도", "순간이동 베기", "검비", "고리 탄막", "나선" };

        readonly List<int> eyeIds = new List<int>();
        readonly List<int> haloPool = new List<int>();
        int haloCursor;

        static readonly Color32 EyeRed = new Color32(255, 40, 36, 255);

        // ─────────────────────────────────────────────────────────────
        // 가짜 격파 → 각성
        // ─────────────────────────────────────────────────────────────
        IEnumerator FakeDeathAndAwaken()
        {
            SetPhase(BossPhase.FakeDeath);
            CurrentPattern = "";
            drifting = false;
            blobSquash = Vector3.one;
            blobLift = 0f;

            // 1) 무너진다 — 격파한 것처럼 보인다
            Vector3 c = sw.Blob.center;
            var bots = sw.Bots;
            for (int i = 0; i < bots.Length; i++)
            {
                if (bots[i].mode != BotMode.Bound && bots[i].mode != BotMode.Free) continue;
                sw.Drop(i, new Vector3((bots[i].pos.x - c.x) * Random.Range(0.4f, 1.2f), Random.Range(0f, 2.5f), Random.Range(-0.6f, 0.6f)));
                bots[i].flash = 0f;
            }
            yield return new WaitForSeconds(2.4f);   // 정적

            // 2) 한 점에서 눈이 뜨인다
            SetPhase(BossPhase.Awakening);
            CurrentPattern = "각성";
            var k = sw.Knight;
            float kx = Mathf.Clamp(c.x, arenaMinX + 3f, arenaMaxX - 3f);
            k.origin = new Vector3(kx, sw.floorY + 0.6f, 0f);
            k.facing = Mathf.Sign(P.x - kx) == 0f ? -1f : Mathf.Sign(P.x - kx);
            k.lean = 0f; k.armAngle = 0f; k.bladeAngle = 0f; k.bladeDanger = false;
            k.UpdatePose();
            k.stiffness = 6f; k.damping = 3.5f; k.maxSpeed = 3.5f; k.noise = 0f;

            var debris = sw.AllIn(BotMode.Debris, new List<int>());
            Vector3 under = new Vector3(kx, sw.floorY, 0f);
            debris.Sort((a, b) => (bots[a].pos - under).sqrMagnitude.CompareTo((bots[b].pos - under).sqrMagnitude));
            eyeIds.Clear();
            for (int j = 0; j < 3 && j < debris.Count; j++) MakeEye(debris[j], j);
            yield return new WaitForSeconds(1.8f);   // 눈이 떠올라 플레이어를 본다

            // 3) 흩어진 봇을 전부 빨아들여 압축된 검은 기사로 선다
            var rest = new List<int>();
            for (int i = 0; i < bots.Length; i++)
                if ((bots[i].mode == BotMode.Debris || bots[i].mode == BotMode.Dead) && !eyeIds.Contains(i)) rest.Add(i);
            Vector3 o = k.ChestWorld;
            rest.Sort((a, b) => (bots[a].pos - o).sqrMagnitude.CompareTo((bots[b].pos - o).sqrMagnitude));
            int knightN = KnightBotCount(sw);
            sw.Halo.center = k.ChestWorld;

            float dur = 1.5f, t = 0f;
            int next = 0;
            while (t < dur + 0.6f)
            {
                t += Time.deltaTime;
                float q = Mathf.Clamp01(t / dur);
                int upto = Mathf.Min(rest.Count, Mathf.CeilToInt(rest.Count * q));
                for (; next < upto; next++) AwakenBot(rest[next], next < knightN, kx, next);
                sw.dark = Mathf.Clamp01(t / (dur + 0.3f));
                k.stiffness = Mathf.Lerp(6f, 900f, q * q);
                k.damping = Mathf.Lerp(3.5f, 55f, q);
                k.maxSpeed = Mathf.Lerp(3.5f, 95f, q);
                yield return null;
            }
            k.stiffness = 900f; k.damping = 55f; k.maxSpeed = 95f; k.noise = 0.004f;
            sw.dark = 1f;

            // 4) 한 번 내려다본다
            for (float u = 0f; u < 0.35f; u += Time.deltaTime) { k.lean = Mathf.Lerp(0f, -12f, Smooth(u / 0.35f)); yield return null; }
            yield return new WaitForSeconds(0.8f);
            for (float u = 0f; u < 0.25f; u += Time.deltaTime) { k.lean = Mathf.Lerp(-12f, 0f, Smooth(u / 0.25f)); yield return null; }

            knightInitial = Mathf.Max(1, KnightBodyCount);
            SetPhase(BossPhase.Phase3);
            yield return KnightLoop();
        }

        void MakeEye(int i, int j)
        {
            ref NanoBot b = ref sw.Bots[i];
            b.seg = -1;
            b.segV = new Vector2(j - 1, 0f);
            b.tint = EyeRed;
            b.glow = 7f;
            b.rim = 0f;
            sw.Bind(i, RSNanoSwarm.G_Knight);
            eyeIds.Add(i);
        }

        /// <summary>기사 몸을 격자 간격으로 빈틈 없이 채우는 봇 수 (나머지는 후광 = 탄약)</summary>
        public static int KnightBotCount(RSNanoSwarm s)
        {
            int n = Mathf.RoundToInt(KnightGuide.BodyArea / (0.866f * s.spacing * s.spacing));
            return Mathf.Clamp(n, 200, Mathf.Max(200, Mathf.RoundToInt(s.Count * 0.6f)));
        }

        /// <summary>흩어진 봇 하나를 기사 몸(테두리 판정 포함) 또는 후광으로 빨아들인다. ordinal = 기사 몸을 고르게 채우는 순번</summary>
        void AwakenBot(int i, bool toKnight, float kx, int ordinal)
        {
            ref NanoBot b = ref sw.Bots[i];
            b.glow = 0f;
            b.tint = White;
            if (b.mode == BotMode.Dead) { b.pos = new Vector3(kx + Random.Range(-6f, 6f), sw.floorY, Random.Range(-1f, 2f)); b.scale = 0f; }
            if (toKnight)
            {
                KnightGuide.Sample(ref b, NanoLattice.Halton(ordinal, 2), NanoLattice.Halton(ordinal, 3), NanoLattice.Halton(ordinal, 5), NanoLattice.Halton(ordinal, 7));
                b.rim = 0f;   // 테두리는 군집이 실루엣 가장자리를 찾아 매 프레임 정한다 (RSNanoSwarm.UpdateRim)
                sw.Bind(i, RSNanoSwarm.G_Knight);
            }
            else
            {
                b.v = new Vector3(Random.value, Random.value, Random.value);
                b.rim = 0.35f;
                sw.Bind(i, RSNanoSwarm.G_Halo);
            }
        }

        void UpdateKnight(float dt)
        {
            var k = sw.Knight;
            if (!lockFacing)
            {
                float f = Mathf.Sign(P.x - k.origin.x);
                if (f != 0f) k.facing = f;
            }
            k.lookAt = P;
            k.origin.y = sw.floorY + 0.6f + Mathf.Sin(Time.time * 1.3f) * 0.12f;
            sw.Halo.center = k.ChestWorld + new Vector3(-k.facing * 0.5f, 0.4f, 0f);
        }

        void SetEyeGlow(float g)
        {
            foreach (int i in eyeIds) if (sw.Bots[i].mode == BotMode.Bound) sw.SetGlow(i, g);
        }

        // ─────────────────────────────────────────────────────────────
        // 3페 흐름
        // ─────────────────────────────────────────────────────────────
        IEnumerator KnightLoop()
        {
            string last = null;
            while (true)
            {
                var rp = queuedRig ?? (queued == null ? PickRig() : null);
                queuedRig = null;
                if (rp != null && rp.rig && rp.clip)
                {
                    yield return RunRig(rp);
                }
                else
                {
                    string pick = queued ?? PickKnight(last);
                    queued = null;
                    last = pick;
                    CurrentPattern = pick;
                    switch (pick)
                    {
                        case "발도": yield return Iai(); break;
                        case "순간이동 베기": yield return BlinkSlash(); break;
                        case "검비": yield return SwordRain(); break;
                        case "고리 탄막": yield return RingBurst(); break;
                        case "나선": yield return Spiral(); break;
                    }
                    CurrentPattern = "";
                }
                if (Random.value < 0.5f && !PendingAny) continue;
                yield return KnightIdle(Rc(0.9f));
            }
        }

        string PickKnight(string last)
        {
            string p;
            do p = KnightPatterns[Random.Range(0, KnightPatterns.Length)]; while (p == last);
            return p;
        }

        /// <summary>대기 — 흐르지 않는다. 멈춰 지켜보다가 거리를 맞추려 끊기듯 한 칸씩 순간이동</summary>
        IEnumerator KnightIdle(float dur)
        {
            var k = sw.Knight;
            float t = 0f, step = 0f;
            while (t < dur && !PendingAny)
            {
                t += Time.deltaTime;
                step += Time.deltaTime;
                if (step > 0.38f)
                {
                    step = 0f;
                    float want = Mathf.Clamp(P.x - k.facing * 6.5f, arenaMinX + 1.5f, arenaMaxX - 1.5f);
                    k.origin.x += Mathf.Clamp(want - k.origin.x, -1.6f, 1.6f);
                }
                yield return null;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 탄약 (후광 봇)
        // ─────────────────────────────────────────────────────────────
        void RefreshHalo()
        {
            sw.AllIn(BotMode.Bound, haloPool, RSNanoSwarm.G_Halo);
            haloCursor = 0;
        }

        /// <summary>후광에 붙어 있는 봇 하나 (돌아가며). 없으면 -1</summary>
        int NextHalo()
        {
            if (haloCursor >= haloPool.Count) RefreshHalo();
            while (haloCursor < haloPool.Count)
            {
                int i = haloPool[haloCursor++];
                if (sw.Bots[i].mode == BotMode.Bound && sw.Bots[i].guide == RSNanoSwarm.G_Halo) return i;
            }
            return -1;
        }

        float FacingAngle => sw.Knight.facing > 0f ? 0f : 180f;

        static Vector3 Dir(float deg) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad), 0f);

        /// <summary>칼을 휘두른 자리에서 호 모양으로 검기가 퍼진다</summary>
        void Crescent(int n, float speed, float spread = 55f)
        {
            var k = sw.Knight;
            Vector3 c = k.ChestWorld + Vector3.down * 0.3f;
            for (int j = 0; j < n; j++)
            {
                int i = NextHalo();
                if (i < 0) return;
                float a = FacingAngle + (n > 1 ? Mathf.Lerp(-spread, spread, j / (float)(n - 1)) : 0f) * k.facing;
                Vector3 d = Dir(a);
                sw.Fire(i, c + d * 2.2f, 0f, d * speed, 1.3f, RSNanoSwarm.G_Halo, default, 1f, true);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 3페 패턴
        // ─────────────────────────────────────────────────────────────

        /// <summary>발도 — 칼을 뒤로 들어 올리고(예고) 완전히 멈춘다(정적) → 한 번에 베고 호 모양 검기</summary>
        IEnumerator Iai()
        {
            var k = sw.Knight;
            lockFacing = true;
            float tg = Tg(0.6f);
            for (float t = 0f; t < tg; t += Time.deltaTime)
            {
                float q = Smooth(t / tg);
                k.armAngle = Mathf.Lerp(0f, 200f, q);      // 칼을 머리 위 뒤쪽으로
                k.lean = Mathf.Lerp(0f, 9f, q);
                yield return null;
            }
            SetEyeGlow(13f);
            yield return new WaitForSeconds(0.35f * D.telegraph);   // 정적 → 폭발

            k.bladeDanger = true;
            for (float t = 0f; t < 0.07f; t += Time.deltaTime)
            {
                float q = t / 0.07f;
                k.armAngle = Mathf.Lerp(200f, 40f, q);     // 앞으로 내리베기
                k.lean = Mathf.Lerp(9f, -12f, q);
                yield return null;
            }
            k.armAngle = 40f; k.lean = -12f;
            Crescent(21, 19f);
            SetEyeGlow(7f);
            yield return new WaitForSeconds(0.12f);
            k.bladeDanger = false;
            for (float t = 0f; t < Rc(0.5f); t += Time.deltaTime)
            {
                float q = Smooth(Mathf.Clamp01(t / Rc(0.5f)));
                k.armAngle = Mathf.Lerp(40f, 0f, q);
                k.lean = Mathf.Lerp(-12f, 0f, q);
                yield return null;
            }
            lockFacing = false;
        }

        /// <summary>순간이동 베기 ×3 — 흩어져 검은 줄로 화면을 가르고 플레이어 앞 · 뒤에서 번갈아 재조립하며 벤다</summary>
        IEnumerator BlinkSlash()
        {
            var k = sw.Knight;
            lockFacing = true;
            for (int r = 0; r < 3; r++)
            {
                float s = Mathf.Sign(P.x - k.origin.x);
                if (s == 0f) s = 1f;
                float nx = Mathf.Clamp(P.x + (r % 2 == 0 ? s : -s) * 2.8f, arenaMinX + 1.5f, arenaMaxX - 1.5f);
                k.stiffness = 650f; k.damping = 42f; k.maxSpeed = 150f;
                k.armAngle = 20f;
                k.origin.x = nx;
                yield return new WaitForSeconds(0.17f);
                k.stiffness = 900f; k.damping = 55f; k.maxSpeed = 95f;
                k.facing = Mathf.Sign(P.x - nx) == 0f ? 1f : Mathf.Sign(P.x - nx);

                float tg = Tg(0.26f);
                for (float t = 0f; t < tg; t += Time.deltaTime) { k.armAngle = Mathf.Lerp(20f, 190f, Smooth(t / tg)); yield return null; }
                k.bladeDanger = true;
                for (float t = 0f; t < 0.06f; t += Time.deltaTime) { k.armAngle = Mathf.Lerp(190f, 40f, t / 0.06f); yield return null; }
                k.armAngle = 40f;
                Crescent(9, 16f, 40f);
                yield return new WaitForSeconds(0.1f);
                k.bladeDanger = false;
                yield return new WaitForSeconds(Rc(0.22f));
            }
            for (float t = 0f; t < 0.3f; t += Time.deltaTime) { k.armAngle = Mathf.Lerp(40f, 0f, Smooth(t / 0.3f)); yield return null; }
            lockFacing = false;
        }

        /// <summary>검비 — 화면 안쪽 높은 곳에 칼날이 격자로 줄지어 걸린다(예고, 빈 줄 하나) → 순서대로 쏟아진다. 3번</summary>
        IEnumerator SwordRain()
        {
            var k = sw.Knight;
            lockFacing = true;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime) { k.armAngle = Mathf.Lerp(0f, 165f, Smooth(t / 0.3f)); yield return null; }
            SetEyeGlow(11f);

            float fallWait = Tg(0.95f);
            for (int w = 0; w < 3; w++)
            {
                float safe = P.x + Random.Range(-1f, 1f);
                var cols = new List<float>();
                for (float x = arenaMinX + 0.8f; x <= arenaMaxX - 0.8f; x += 1.3f)
                    if (Mathf.Abs(x - safe) > 1.1f) cols.Add(x);
                float slant = w % 2 == 0 ? 0.7f : -0.7f;
                float zBack = 7f + (w % 2) * 1.2f;
                for (int c = 0; c < cols.Count; c++)
                {
                    float x = cols[c];
                    Vector3 top = new Vector3(x, 8.6f, zBack);
                    Vector3 vel = (new Vector3(x + slant, sw.floorY, 0f) - top).normalized * 21f;
                    for (int j = 0; j < 4; j++)
                    {
                        int i = NextHalo();
                        if (i < 0) break;
                        sw.Fire(i, top + new Vector3(0f, j * 0.27f, 0f), fallWait + c * 0.03f, vel, 1.15f, RSNanoSwarm.G_Halo, default, 1f);
                    }
                }
                yield return new WaitForSeconds(0.6f);
            }
            yield return new WaitForSeconds(fallWait);
            SetEyeGlow(7f);
            for (float t = 0f; t < Rc(0.6f); t += Time.deltaTime) { k.armAngle = Mathf.Lerp(165f, 0f, Smooth(Mathf.Clamp01(t / Rc(0.6f)))); yield return null; }
            lockFacing = false;
        }

        /// <summary>고리 탄막 — 가슴 앞에 탄이 완벽한 고리로 정렬(예고, 틈 하나) → 동시에 퍼진다. 3겹, 겹마다 틈이 돈다</summary>
        IEnumerator RingBurst()
        {
            var k = sw.Knight;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime) { k.armAngle = Mathf.Lerp(0f, 60f, Smooth(t / 0.25f)); yield return null; }
            for (int r = 0; r < 3; r++)
            {
                Vector3 c = k.ChestWorld;
                Vector3 toP = P - c;
                float gap = Mathf.Atan2(toP.y, toP.x) * Mathf.Rad2Deg + Random.Range(-30f, 30f);
                const int n = 48;
                float off = r * 3.75f;
                for (int j = 0; j < n; j++)
                {
                    float a = off + j * 360f / n;
                    if (Mathf.Abs(Mathf.DeltaAngle(a, gap)) < 17f) continue;
                    int i = NextHalo();
                    if (i < 0) break;
                    Vector3 d = Dir(a);
                    sw.Fire(i, c + d * 1.5f, Tg(0.55f), d * 7.5f, 2.6f, RSNanoSwarm.G_Halo, default, 1f);
                }
                yield return new WaitForSeconds(0.5f);
            }
            yield return new WaitForSeconds(Tg(0.55f));
            for (float t = 0f; t < Rc(0.5f); t += Time.deltaTime) { k.armAngle = Mathf.Lerp(60f, 0f, Smooth(Mathf.Clamp01(t / Rc(0.5f)))); yield return null; }
        }

        /// <summary>나선 — 후광이 빨라지고 눈이 번뜩인다(예고) → 세 갈래 나선이 2.6초 동안 화면을 채운다</summary>
        IEnumerator Spiral()
        {
            var k = sw.Knight;
            var halo = sw.Halo;
            float baseSpin = halo.spin;
            float tg = Tg(0.55f);
            for (float t = 0f; t < tg; t += Time.deltaTime)
            {
                halo.spin = Mathf.Lerp(baseSpin, 3f, t / tg);
                k.armAngle = Mathf.Lerp(0f, 35f, Smooth(t / tg));
                yield return null;
            }
            SetEyeGlow(12f);
            float theta = Random.value * 360f, dur = 2.6f, emit = 0f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                emit += Time.deltaTime;
                while (emit >= 0.045f)
                {
                    emit -= 0.045f;
                    Vector3 c = k.ChestWorld;
                    for (int a = 0; a < 3; a++)
                    {
                        int i = NextHalo();
                        if (i < 0) break;
                        Vector3 d = Dir(theta + a * 120f);
                        sw.Fire(i, c + d * 0.6f, 0f, d * 7.2f, 2.8f, RSNanoSwarm.G_Halo, default, 1f, true);
                    }
                    theta += 11f * k.facing;
                }
                yield return null;
            }
            halo.spin = baseSpin;
            SetEyeGlow(7f);
            for (float t = 0f; t < Rc(0.6f); t += Time.deltaTime) { k.armAngle = Mathf.Lerp(35f, 0f, Smooth(Mathf.Clamp01(t / Rc(0.6f)))); yield return null; }
        }
    }
}
