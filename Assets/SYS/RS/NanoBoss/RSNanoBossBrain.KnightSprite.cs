// RE:AL STEEL - 나노봇 보스 두뇌: 헬 3페 — 그림 기사 (동작 시트)
//
// '3페 기사 동작'(RSNanoKnightMoves)이 있으면 3페 기사의 몸을 픽셀아트 동작 그림으로 만든다.
// 그림 픽셀 하나 = 봇 '배율 × 배율'. 검은 실루엣 + 흰 테두리, 칼날 · 검기는 흰 빛, 붉은 눈.
// 동작은 그림 그대로 넘기고, 이동(달리기 · 대시 · 도약)은 코드가 몸 위치를 옮긴다 → 봇이 늦게 따라와 잔상이 된다.
//
// 패턴
//   연속 베기      대시로 붙어 4연타 (첫 타 예고, 마지막 타에 검기)
//   강베기         칼을 든 채 정적(예고) → 내리베기 + 호 모양 검기
//   찌르기         번쩍임을 모으다(예고) 앞으로 뛰어들며 찌르기, 이어서 한 번 더 찌르기
//   돌진 베기      웅크렸다가(예고) 플레이어를 꿰뚫고 지나가 돌아서며 큰 베기 + 검기
//   도약 내려찍기   뛰어올라 머리 위에서 따라오다(예고) 내려찍기 + 바닥 충격파
//   (11-11 탄막 패턴 — 검비 · 고리 탄막 · 나선 — 은 뺐다. 베기에 붙은 검기 탄 · 충격파는 남음)
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealSteel.NanoBoss
{
    public partial class RSNanoBossBrain
    {
        [Header("3페 기사 동작 (그림)")]
        [Tooltip("3페 기사를 픽셀아트 동작 그림으로 만든다. 비우면 예전 조각 기사.\nTools → RE_AL STEEL → 나노봇 → 3페 기사 동작 굽기 가 만들어 여기에 넣는다")]
        public RSNanoKnightMoves knightMoves;

        public static readonly string[] SpriteKnightPatterns = { "연속 베기", "강베기", "찌르기", "돌진 베기", "도약 내려찍기" };

        bool SpriteKnight => knightMoves && knightMoves.Ready;
        /// <summary>3페 패턴 이름 (HUD) — 그림 기사면 그림 기사 패턴</summary>
        public string[] Phase3Patterns => SpriteKnight ? SpriteKnightPatterns : KnightPatterns;
        RSNanoKnightMoves KM => knightMoves;

        // ─────────────────────────────────────────────────────────────
        // 동작 재생
        // ─────────────────────────────────────────────────────────────
        static int FirstBig(NanoKnightClip c, int min = 150)
        {
            if (c == null || !c.Ready) return -1;
            for (int f = 0; f < c.frames; f++) if (c.Fx(f) >= min) return f;
            return c.FirstActive();
        }

        void Pose(KnightMove m, int f) => sw.Knight.SetPose(m, f);

        void FacePlayer()
        {
            float f = Mathf.Sign(P.x - sw.Knight.origin.x);
            if (f != 0f) sw.Knight.facing = f;
        }

        /// <summary>동작을 from ~ to(제외) 프레임까지 재생. frame(f) 는 새 프레임마다, step(f, dt) 는 매 프레임</summary>
        IEnumerator Play(KnightMove m, int from = 0, int to = -1, float speed = 1f, Action<int> frame = null, Action<int, float> step = null)
        {
            var c = KM.Get(m);
            if (!c.Ready) yield break;
            int end = to < 0 ? c.frames : Mathf.Min(to, c.frames);
            float k = 1f / Mathf.Max(0.05f, D.speed * speed);   // 프레임 시간(GIF 기준) × 난이도 · 패턴 배속
            int f = Mathf.Max(0, from);
            float t = 0f, next = c.Dur(f) * k;
            int last = -1;
            while (f < end)
            {
                Pose(m, f);
                if (f != last) { frame?.Invoke(f); last = f; }
                step?.Invoke(f, Time.deltaTime);
                yield return null;
                t += Time.deltaTime;
                while (f < end && t >= next) { f++; if (f < end) next += c.Dur(f) * k; }
            }
        }

        /// <summary>한 프레임에 멈춰 있는다 (예고 · 정적)</summary>
        IEnumerator Hold(KnightMove m, int f, float time, Action<float> step = null)
        {
            Pose(m, f);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                step?.Invoke(Time.deltaTime);
                yield return null;
            }
        }

        float ArenaClamp(float x) => Mathf.Clamp(x, arenaMinX + 1.2f, arenaMaxX - 1.2f);

        void MoveX(float target, float speed, float dt)
        {
            var k = sw.Knight;
            k.origin.x = Mathf.MoveTowards(k.origin.x, ArenaClamp(target), speed * dt);
        }

        void IdlePose() => Pose(KM.idle, 0);

        // ─────────────────────────────────────────────────────────────
        // 흐름
        // ─────────────────────────────────────────────────────────────
        IEnumerator SpriteKnightLoop()
        {
            string last = null;
            sw.Knight.reinforce = true;
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
                    string pick = queued ?? PickSpriteKnight(last);
                    queued = null;
                    last = pick;
                    CurrentPattern = pick;
                    lockFacing = true;
                    switch (pick)
                    {
                        case "연속 베기": yield return Combo(); break;
                        case "강베기": yield return HeavySlash(); break;
                        case "찌르기": yield return Thrust(); break;
                        case "돌진 베기": yield return RushSlash(); break;
                        case "도약 내려찍기": yield return Plunge(); break;
                    }
                    lockFacing = false;
                    sw.Knight.origin.y = sw.floorY;
                    IdlePose();
                    CurrentPattern = "";
                }
                if (Random.value < 0.4f && !PendingAny) continue;
                yield return SpriteKnightIdle(Rc(0.9f));
            }
        }

        string PickSpriteKnight(string last)
        {
            var ok = new List<string>();
            foreach (var p in SpriteKnightPatterns)
            {
                if (p == last) continue;
                if (p == "연속 베기" && !KM.Has(KnightMove.Light1)) continue;
                if (p == "강베기" && !KM.Has(KnightMove.FrontHeavy)) continue;
                if (p == "찌르기" && !KM.Has(KnightMove.DashHeavy)) continue;
                if (p == "돌진 베기" && !KM.Has(KnightMove.Rush)) continue;
                if (p == "도약 내려찍기" && !(KM.Has(KnightMove.AirIn) && KM.Has(KnightMove.AirLoop) && KM.Has(KnightMove.AirLand))) continue;
                ok.Add(p);
            }
            if (ok.Count > 0) return ok[Random.Range(0, ok.Count)];
            return last != null && System.Array.IndexOf(SpriteKnightPatterns, last) >= 0 ? last : "";   // 쓸 수 있는 게 하나뿐이면 그것 (없으면 대기만)
        }

        /// <summary>대기 — 칼을 겨눈 채 지켜본다. 너무 멀면 달려가고, 너무 가까우면 백대시</summary>
        IEnumerator SpriteKnightIdle(float dur)
        {
            var k = sw.Knight;
            float t = 0f;
            while (t < dur && !PendingAny)
            {
                FacePlayer();
                float want = ArenaClamp(P.x - k.facing * 5.5f);
                float d = Mathf.Abs(P.x - k.origin.x);
                if (d < 2.2f && KM.Has(KnightMove.BackDash))
                {
                    float away = ArenaClamp(k.origin.x - k.facing * 3.5f);
                    float sp = 3.5f / Mathf.Max(0.1f, KM.backDash.Length / D.speed);
                    lockFacing = true;
                    yield return Play(KnightMove.BackDash, step: (f, dt) => MoveX(away, sp * 1.4f, dt));
                    lockFacing = false;
                    t += 0.4f;
                }
                else if (Mathf.Abs(want - k.origin.x) > 2.5f && KM.Has(KnightMove.Running))
                {
                    float run = 7.5f * D.speed;
                    if (KM.Has(KnightMove.RunIn)) yield return Play(KnightMove.RunIn, step: (f, dt) => MoveX(want, run * 0.5f, dt));
                    for (float r = 0f; r < 2f && Mathf.Abs(want - k.origin.x) > 0.6f && !PendingAny; r += Time.deltaTime)
                    {
                        var c = KM.running;
                        Pose(KnightMove.Running, c.LoopFrame(r * D.speed));
                        want = ArenaClamp(P.x - k.facing * 5.5f);
                        MoveX(want, run, Time.deltaTime);
                        yield return null;
                    }
                    if (KM.Has(KnightMove.RunOut)) yield return Play(KnightMove.RunOut, step: (f, dt) => MoveX(want, run * 0.3f, dt));
                    t += 0.5f;
                }
                else
                {
                    IdlePose();
                    yield return null;
                    t += Time.deltaTime;
                }
            }
            IdlePose();
        }

        /// <summary>대시로 플레이어 앞 gap(m) 까지 붙는다</summary>
        IEnumerator DashTo(float gap)
        {
            var k = sw.Knight;
            FacePlayer();
            float target = ArenaClamp(P.x - k.facing * gap);
            if (Mathf.Abs(target - k.origin.x) < 1f || !KM.Has(KnightMove.Dash)) yield break;
            float len = Mathf.Max(0.1f, KM.dash.Length / D.speed);
            float sp = Mathf.Abs(target - k.origin.x) / (len * 0.6f);
            yield return Play(KnightMove.Dash, step: (f, dt) => { if (f >= 2) MoveX(target, sp, dt); });
        }

        // ─────────────────────────────────────────────────────────────
        // 패턴
        // ─────────────────────────────────────────────────────────────

        /// <summary>연속 베기 — 대시로 붙어 4연타. 첫 타만 예고가 길고, 마지막 타에 검기</summary>
        IEnumerator Combo()
        {
            var k = sw.Knight;
            yield return DashTo(1.8f);
            KnightMove[] hits = { KnightMove.Light1, KnightMove.Light2, KnightMove.Light3, KnightMove.Light4 };
            for (int h = 0; h < hits.Length; h++)
            {
                var m = hits[h];
                var c = KM.Get(m);
                if (!c.Ready) continue;
                FacePlayer();
                int a = Mathf.Max(1, FirstBig(c));
                yield return Play(m, 0, a);
                yield return Hold(m, a - 1, h == 0 ? Tg(0.35f) : Tg(0.08f));
                if (h == 0) SetEyeGlow(12f);
                float step = 0.35f;
                bool last = h == hits.Length - 1;
                yield return Play(m, a, -1, 1f, f => { if (f == a && last) Crescent(11, 15f, 35f); },
                                  (f, dt) => { if (f <= a + 1) k.origin.x = ArenaClamp(k.origin.x + k.facing * step * dt * 8f); });
            }
            SetEyeGlow(7f);
        }

        /// <summary>강베기 — 칼을 든 채 정적(예고) → 내리베기 + 호 모양 검기</summary>
        IEnumerator HeavySlash()
        {
            var c = KM.frontHeavy;
            FacePlayer();
            int a = Mathf.Max(1, FirstBig(c));
            yield return Play(KnightMove.FrontHeavy, 0, a);
            SetEyeGlow(13f);
            yield return Hold(KnightMove.FrontHeavy, a - 1, Tg(0.55f));
            yield return Play(KnightMove.FrontHeavy, a, -1, 1f, f => { if (f == a) Crescent(21, 19f); });
            SetEyeGlow(7f);
        }

        /// <summary>찌르기 — 번쩍임을 모으다(예고) 앞으로 뛰어들며 찌르기 → 반쯤 확률로 한 번 더</summary>
        IEnumerator Thrust()
        {
            var k = sw.Knight;
            var c = KM.dashHeavy;
            FacePlayer();
            int a = Mathf.Max(1, FirstBig(c));
            yield return Play(KnightMove.DashHeavy, 0, a);
            SetEyeGlow(12f);
            yield return Hold(KnightMove.DashHeavy, a - 1, Tg(0.45f));
            float target = ArenaClamp(P.x - k.facing * 1.2f);
            yield return Play(KnightMove.DashHeavy, a, -1, 1f, null, (f, dt) => { if (f <= a + 2) MoveX(target, 40f * D.speed, dt); });
            SetEyeGlow(7f);
            if (Random.value < 0.5f)
            {
                var g = Random.value < 0.5f ? KnightMove.Guard2 : KnightMove.Guard3;
                var gc = KM.Get(g);
                if (gc.Ready)
                {
                    FacePlayer();
                    int ga = Mathf.Max(1, FirstBig(gc));
                    yield return Play(g, 0, ga);
                    yield return Hold(g, ga - 1, Tg(0.15f));
                    float t2 = ArenaClamp(P.x - k.facing * 1.4f);
                    yield return Play(g, ga, -1, 1f, null, (f, dt) => { if (f <= ga + 1) MoveX(t2, 30f * D.speed, dt); });
                }
            }
        }

        /// <summary>돌진 베기 — 웅크렸다가(예고) 플레이어를 꿰뚫고 지나가 돌아서며 큰 베기 + 검기</summary>
        IEnumerator RushSlash()
        {
            var k = sw.Knight;
            var c = KM.rush;
            FacePlayer();
            const int crouch = 4, dashEnd = 10;
            yield return Play(KnightMove.Rush, 0, crouch + 1);
            SetEyeGlow(12f);
            yield return Hold(KnightMove.Rush, crouch, Tg(0.5f));
            float through = ArenaClamp(P.x + k.facing * 3.5f);
            float len = Mathf.Max(0.05f, c.TimeOf(crouch + 1, dashEnd) / D.speed);
            float sp = Mathf.Abs(through - k.origin.x) / len;
            sw.Knight.bladeDanger = true;
            yield return Play(KnightMove.Rush, crouch + 1, dashEnd, 1f, null, (f, dt) => MoveX(through, sp, dt));
            k.origin.x = through;
            FacePlayer();   // 돌아선다
            int a = FirstBig(c);
            yield return Play(KnightMove.Rush, dashEnd, -1, 1f, f => { if (f == a) Crescent(15, 17f, 45f); });
            sw.Knight.bladeDanger = false;
            SetEyeGlow(7f);
        }

        /// <summary>도약 내려찍기 — 뛰어올라 머리 위에서 따라오다(예고) 내려찍기 + 바닥 충격파</summary>
        IEnumerator Plunge()
        {
            var k = sw.Knight;
            FacePlayer();
            float top = sw.floorY + 3.4f;
            float y0 = k.origin.y;
            float inLen = Mathf.Max(0.05f, KM.airIn.Length / D.speed);
            float t = 0f;
            yield return Play(KnightMove.AirIn, step: (f, dt) =>
            {
                t += dt;
                k.origin.y = Mathf.Lerp(y0, top, Smooth(Mathf.Clamp01(t / inLen)));
                MoveX(P.x, 6f * D.speed, dt);
            });
            SetEyeGlow(13f);
            var loop = KM.airLoop;
            float hold = Tg(0.6f);
            for (float u = 0f; u < hold; u += Time.deltaTime)
            {
                Pose(KnightMove.AirLoop, loop.LoopFrame(u));
                k.origin.y = top + Mathf.Sin(u * 9f) * 0.05f;
                if (u < hold - 0.15f * D.telegraph) MoveX(P.x, 7f * D.speed, Time.deltaTime);   // 마지막엔 조준 고정
                yield return null;
            }
            // 내려찍기
            Pose(KnightMove.AirLand, 0);
            for (float u = 0f; u < 0.06f; u += Time.deltaTime) { k.origin.y = Mathf.Lerp(top, sw.floorY, u / 0.06f); yield return null; }
            k.origin.y = sw.floorY;
            Shockwave(k.origin.x);
            yield return Play(KnightMove.AirLand);
            SetEyeGlow(7f);
        }

        /// <summary>바닥 충격파 — 양쪽으로 봇 뭉치가 바닥을 따라 퍼진다</summary>
        void Shockwave(float x)
        {
            for (int k = 0; k < 12; k++)
            {
                int i = NextHalo();
                if (i < 0) break;
                float side = k % 2 == 0 ? 1f : -1f;
                Vector3 hold = new Vector3(x + side * 1.2f, sw.floorY + 0.2f + (k / 2 % 3) * 0.2f, 0f);
                sw.Fire(i, hold, 0f, new Vector3(side * 11f * D.speed, 0f, 0f), 0.9f, RSNanoSwarm.G_Halo, default, 1f, true);
            }
        }
    }
}
