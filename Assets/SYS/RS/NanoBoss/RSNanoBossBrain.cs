// RE:AL STEEL - 나노봇 보스 두뇌 (난이도 · 페이즈 · 패턴)
//
// 기획 (10-27 정리)
//   · 형태 없는 나노봇 덩어리. 흐르면서 그때그때 가시 · 채찍 · 빔으로 변해 공격하고, 맞을수록 질량이 깎인다
//   · 모든 동작은 절차적 — 가이드(RSNanoSwarm.Guides)를 코드로 움직이고 봇을 옮겨 담는다
//   · 난이도 5단계. 단계마다 예고 시간 · 회복 시간 · 속도 · 연계 · 재흡수 · 패턴 풀이 다르다
//   · 5단계(헬)만 3페: 질량을 다 깎으면 무너져 끝난 듯 보이다가 → 눈이 뜨이고 → 흩어진 봇을 전부 빨아들여
//     압축된 검은 기사로 선다. 1 · 2페 = 본능만 있는 군집, 3페 = 지성이 생긴 악의 (정돈된 탄막, 노리는 공격)
//
// 페이즈: 등장 → 1페 → (질량 60%↓) 2페 → (질량 12%↓) 격파  /  헬: → 가짜 격파 → 각성 → 3페 → 격파
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealSteel.NanoBoss
{
    public enum BossPhase { Intro, Phase1, Phase2, FakeDeath, Awakening, Phase3, Defeated }

    [RequireComponent(typeof(RSNanoSwarm))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("RE_AL STEEL/나노봇 보스 두뇌")]
    public partial class RSNanoBossBrain : MonoBehaviour
    {
        [Serializable]
        public struct Difficulty
        {
            public string name;
            [Tooltip("예고 시간 배율 (클수록 여유)")] public float telegraph;
            [Tooltip("공격 뒤 쉬는 시간 배율")] public float recover;
            [Tooltip("공격 속도 배율")] public float speed;
            [Tooltip("공격 직후 바로 다음 공격을 이을 확률")] [Range(0, 1)] public float chain;
            [Tooltip("떨어진 봇을 다시 빨아들인다")] public bool absorb;
            [Tooltip("바닥 가시 · 화면 뒤 가시")] public bool depthAttacks;
            [Tooltip("빔")] public bool beam;
            [Tooltip("덮치기 (2페)")] public bool slam;
            [Tooltip("3페 (각성한 기사)")] public bool phase3;
        }

        public static readonly Difficulty[] Difficulties =
        {
            new Difficulty { name = "1단계", telegraph = 1.5f,  recover = 1.4f,  speed = 0.85f, chain = 0f,    absorb = false, depthAttacks = false, beam = false, slam = false, phase3 = false },
            new Difficulty { name = "2단계", telegraph = 1.25f, recover = 1.2f,  speed = 0.95f, chain = 0.15f, absorb = false, depthAttacks = false, beam = true,  slam = false, phase3 = false },
            new Difficulty { name = "3단계", telegraph = 1.0f,  recover = 1.0f,  speed = 1.0f,  chain = 0.3f,  absorb = false, depthAttacks = true,  beam = true,  slam = true,  phase3 = false },
            new Difficulty { name = "4단계", telegraph = 0.82f, recover = 0.85f, speed = 1.1f,  chain = 0.45f, absorb = true,  depthAttacks = true,  beam = true,  slam = true,  phase3 = false },
            new Difficulty { name = "헬",    telegraph = 0.66f, recover = 0.7f,  speed = 1.2f,  chain = 0.6f,  absorb = true,  depthAttacks = true,  beam = true,  slam = true,  phase3 = true  },
        };

        [Serializable]
        public class RigPattern
        {
            [Tooltip("보스 아래의 나노봇 리그")] public RSNanoRig rig;
            [Tooltip("그 리그에 만든 애니메이션 클립")] public AnimationClip clip;
            [Tooltip("이 페이즈에서 쓴다")] public bool phase1 = true, phase2 = true, phase3;
            [Tooltip("이 난이도부터 나온다")] [Range(1, 5)] public int fromDifficulty = 1;
            [Tooltip("뽑힐 비중")] [Min(0f)] public float weight = 1f;
            public string Name => clip ? clip.name : "(클립 없음)";
        }

        [Header("난이도")]
        [Range(1, 5)] public int difficulty = 5;

        [Header("리그 패턴 (애니메이션으로 만든 공격)")]
        [Tooltip("나노봇 리그 + 클립. 페이즈 · 난이도에 맞는 것 중에서 뽑는다")]
        public List<RigPattern> rigPatterns = new List<RigPattern>();
        [Tooltip("코드로 만든 예제 패턴도 섞어 쓴다 (리그 패턴이 없으면 늘 예제)")]
        public bool mixBuiltIn = true;

        [Header("연결")]
        [Tooltip("공격 대상. 비우면 (-6, 0, 0) 을 노린다")]
        public Transform player;

        [Header("경기장")]
        public float arenaMinX = -12f;
        public float arenaMaxX = 12f;
        [Tooltip("플레이어와 유지하려는 거리 (m)")]
        public float preferredDistance = 7.5f;

        [Header("페이즈")]
        [Range(0f, 1f)] public float phase2At = 0.6f;
        [Range(0f, 1f)] public float deathAt = 0.12f;
        [Tooltip("3페 기사: 붙은 봇이 처음의 이 비율 아래가 되면 격파")]
        [Range(0f, 1f)] public float knightDeathAt = 0.3f;
        [Tooltip("3페 기사는 단단하다 — 맞은 봇이 실제로 떨어질 확률")]
        [Range(0f, 1f)] public float knightHitChance = 0.35f;

        [Header("덩어리 모양")]
        [Tooltip("가로 : 세로 : 깊이 비율. 실제 크기는 덩어리에 붙은 봇 수 × 격자 간격(군집)으로 정해진다 — 봇이 빠지면 줄어든다")]
        public Vector3 blobRadius = new Vector3(3.4f, 2.7f, 0.9f);

        public BossPhase Phase { get; private set; }
        public string CurrentPattern { get; private set; } = "";
        public bool LooksDefeated => Phase == BossPhase.FakeDeath || Phase == BossPhase.Defeated;
        public event Action<BossPhase> PhaseChanged;

        public Difficulty D => Difficulties[Mathf.Clamp(difficulty, 1, 5) - 1];

        RSNanoSwarm sw;
        string queued;
        RigPattern queuedRig;
        RSNanoRig activeRig;
        UnityEngine.Playables.PlayableGraph activeGraph;
        public RSNanoRig ActiveRig => activeRig;
        int knightInitial;
        float absorbTimer;
        bool lockFacing;
        Vector3 blobSquash = Vector3.one;
        float blobLift;
        bool drifting;
        Vector3 blobBase;
        readonly List<int> tmp = new List<int>();

        // ─────────────────────────────────────────────────────────────
        // 시작 · 조회
        // ─────────────────────────────────────────────────────────────
        void Start()
        {
            sw = GetComponent<RSNanoSwarm>();
            Restart();
        }

        public void Restart() => Restart(difficulty);

        public void Restart(int diff)
        {
            difficulty = Mathf.Clamp(diff, 1, 5);
            StopAllCoroutines();
            if (!sw) sw = GetComponent<RSNanoSwarm>();
            queued = null;
            lockFacing = false;
            blobSquash = Vector3.one;
            blobLift = 0f;
            sw.dark = 0f;
            sw.freeGlow = 0f;
            ResetGuides();
            sw.Blob.aspect = blobRadius.x / Mathf.Max(0.01f, blobRadius.y);
            blobBase = sw.BlobSize(sw.botCount, blobRadius);
            sw.Blob.radius = blobBase;
            sw.Blob.center = new Vector3(Mathf.Clamp(transform.position.x, arenaMinX + 3f, arenaMaxX - 3f), sw.floorY + blobBase.y, 0f);
            sw.Build(sw.Blob.center);
            sw.ResetBotLooks();
            ApplyPhaseLook(BossPhase.Phase1);
            StartCoroutine(Run());
        }

        Vector3 P => player ? player.position + Vector3.up * 0.9f : new Vector3(-6f, sw.floorY + 0.9f, 0f);

        /// <summary>몸 중심 (덩어리 중심 또는 기사 가슴)</summary>
        public Vector3 BodyCenter => Phase == BossPhase.Phase3 || Phase == BossPhase.Awakening ? sw.Knight.ChestWorld : sw.Blob.center;

        float Side => Mathf.Sign(P.x - BodyCenter.x) == 0f ? -1f : Mathf.Sign(P.x - BodyCenter.x);

        /// <summary>남은 질량 0~1 (3페는 기사 기준)</summary>
        public float Mass01
        {
            get
            {
                if (!sw || sw.Count == 0) return 0f;
                if (Phase == BossPhase.Phase3) return knightInitial > 0 ? KnightBodyCount / (float)knightInitial : 0f;
                if (Phase == BossPhase.Awakening) return 1f;
                return sw.Alive / (float)sw.Count;
            }
        }

        // 3페에서 작성 패턴이 기사 봇을 형태로 가져가도 체력이 줄어든 걸로 치지 않는다
        int KnightBodyCount => sw.guideCount[RSNanoSwarm.G_Knight] + (activeRig ? sw.guideCount[RSNanoSwarm.G_Rig] : 0);

        public bool CanBeHit => Phase == BossPhase.Phase1 || Phase == BossPhase.Phase2 || Phase == BossPhase.Phase3;

        int HitMask => Phase == BossPhase.Phase3
            ? (1 << RSNanoSwarm.G_Knight) | (1 << RSNanoSwarm.G_Rig)
            : (1 << RSNanoSwarm.G_Blob) | (1 << RSNanoSwarm.G_TubeA) | (1 << RSNanoSwarm.G_TubeB) | (1 << RSNanoSwarm.G_Ring) | (1 << RSNanoSwarm.G_Rig) | (1 << RSNanoSwarm.G_Halo);

        /// <summary>플레이어 공격이 맞았다. 떨어진 봇 수를 돌려준다</summary>
        public int Hit(Vector3 point, float radius, Vector3 impulse)
        {
            if (!CanBeHit) return 0;
            return sw.Hit(point, radius, impulse, Phase == BossPhase.Phase3 ? knightHitChance : 1f, HitMask);
        }

        /// <summary>from 에서 가장 가까운 맞출 수 있는 봇 위치 (자동 공격 조준용, 표본 추출)</summary>
        public bool TryAimPoint(Vector3 from, out Vector3 point)
        {
            point = default;
            if (!CanBeHit || sw.Count == 0) return false;
            float best = float.MaxValue;
            var bots = sw.Bots;
            int mask = HitMask;
            for (int k = 0; k < 96; k++)
            {
                int i = Random.Range(0, bots.Length);
                ref NanoBot b = ref bots[i];
                if (b.mode != BotMode.Bound || (mask & (1 << b.guide)) == 0 || (b.guide == RSNanoSwarm.G_Knight && b.seg < 0)) continue;
                float d = (b.pos - from).sqrMagnitude;
                if (d < best) { best = d; point = b.pos; }
            }
            return best < float.MaxValue;
        }

        public void ForcePattern(string patternName) => queued = patternName;

        /// <summary>리그 패턴을 다음 차례에 실행 (HUD · 에디터에서 부른다)</summary>
        public void PlayRigPattern(RigPattern p) => queuedRig = p;

        bool PendingAny => queued != null || queuedRig != null;

        RigPattern PickRig()
        {
            float total = 0f;
            foreach (var a in rigPatterns) if (Fits(a)) total += a.weight;
            if (total <= 0f) return null;
            if (mixBuiltIn && Random.value < 0.5f) return null;
            float r = Random.value * total;
            foreach (var a in rigPatterns)
            {
                if (!Fits(a)) continue;
                r -= a.weight;
                if (r <= 0f) return a;
            }
            return null;
        }

        bool Fits(RigPattern a) =>
            a != null && a.rig && a.clip && difficulty >= a.fromDifficulty &&
            ((Phase == BossPhase.Phase1 && a.phase1) || (Phase == BossPhase.Phase2 && a.phase2) || (Phase == BossPhase.Phase3 && a.phase3));

        /// <summary>리그 패턴 실행 — 리그의 애니메이터에 클립을 직접 재생한다 (컨트롤러 없이)</summary>
        IEnumerator RunRig(RigPattern p)
        {
            CurrentPattern = p.Name;
            drifting = false;
            var rig = p.rig;
            var animator = rig.GetComponent<Animator>();
            if (!animator) animator = rig.gameObject.AddComponent<Animator>();
            animator.enabled = true;
            int home = Phase == BossPhase.Phase3 ? RSNanoSwarm.G_Knight : RSNanoSwarm.G_Blob;
            float face = P.x >= BodyCenter.x ? 1f : -1f;
            float speed = Phase == BossPhase.Phase3 ? D.speed : Sp;
            var playable = UnityEngine.Playables.AnimationPlayableUtilities.PlayClip(animator, p.clip, out activeGraph);
            UnityEngine.Playables.PlayableExtensions.SetSpeed(playable, speed);
            activeGraph.Evaluate(0f);   // 첫 프레임 모양으로 격자 자리를 만든다 (편집기 미리보기와 같게)
            rig.Begin(home, () => player ? player.position : new Vector3(-6f, sw.floorY, 0f), face, BodyCenter);
            activeRig = rig;
            float end = p.clip.length / Mathf.Max(0.05f, speed);
            for (float t = 0f; t < end; t += Time.deltaTime) yield return null;
            StopRig();
            CurrentPattern = "";
        }

        void StopRig()
        {
            if (activeGraph.IsValid()) activeGraph.Destroy();
            if (activeRig) activeRig.End();
            activeRig = null;
        }

        /// <summary>질량을 깎아 2페로</summary>
        public void ForcePhase2()
        {
            if (Phase != BossPhase.Phase1) return;
            int want = Mathf.CeilToInt(sw.Count * (1f - phase2At + 0.04f)) - (sw.Count - sw.Alive);
            var ids = sw.AllIn(BotMode.Bound, tmp, RSNanoSwarm.G_Blob);
            for (int k = 0; k < ids.Count && want > 0; k++, want--)
                sw.Drop(ids[k], new Vector3(Random.Range(-3f, 3f), Random.Range(1f, 5f), Random.Range(-1f, 1f)));
        }

        /// <summary>바로 3페 (난이도와 무관하게 미리보기)</summary>
        public void ForcePhase3()
        {
            StopAllCoroutines();
            ResetGuides();
            StartCoroutine(FakeDeathAndAwaken());
        }

        void SetPhase(BossPhase p)
        {
            if (Phase == p) return;
            Phase = p;
            ApplyPhaseLook(p);
            PhaseChanged?.Invoke(p);
        }

        void ApplyPhaseLook(BossPhase p)
        {
            var blob = sw.Blob;
            bool p2 = p == BossPhase.Phase2;
            blob.wobble = p2 ? 0.17f : 0.08f;
            blob.wobbleSpeed = p2 ? 2.6f : 1.6f;
            blob.flow = p2 ? 0.38f : 0.22f;
            blob.flowSpeed = p2 ? 1.4f : 0.8f;
            blob.noise = p2 ? 0.1f : 0.05f;
            blob.stiffness = p2 ? 34f : 26f;
            blob.bind = p2 ? 0.7f : 1f;   // 2페: 결속이 풀려 흐르기 시작한다
        }

        void ResetGuides()
        {
            if (!sw) return;
            foreach (var g in new SwarmGuide[] { sw.TubeA, sw.TubeB, sw.Ring, sw.Blob })
            {
                g.danger = false;
                g.glow = 0f;
                g.tint = new Color32(255, 255, 255, 255);
            }
            sw.Blob.bulge = 0f;
            sw.freeGlow = 0f;
            sw.Knight.bladeDanger = false;
            StopRig();
            CurrentPattern = "";
        }

        // ─────────────────────────────────────────────────────────────
        // 매 프레임 — 덩어리 크기 · 재흡수 · 페이즈 판정 · 기사 시선
        // ─────────────────────────────────────────────────────────────
        void Update()
        {
            if (!sw) return;
            float dt = Time.deltaTime;
            var blob = sw.Blob;

            if (Phase == BossPhase.Intro || Phase == BossPhase.Phase1 || Phase == BossPhase.Phase2)
            {
                // 덩어리 크기 = 붙은 봇 수가 격자 간격으로 맞물린 크기 → 봇이 가시 · 리그로 빠지거나 맞아 떨어지면 줄어든다
                Vector3 size = sw.BlobSize(Mathf.Max(40, sw.guideCount[RSNanoSwarm.G_Blob]), blobRadius);
                blobBase = Vector3.Lerp(blobBase, size, 1f - Mathf.Exp(-5f * dt));
                blob.radius = Vector3.Scale(blobBase, blobSquash);
                {
                    blob.center.y = sw.floorY + blob.radius.y * 0.78f + blobLift;
                    blob.center.z = Mathf.MoveTowards(blob.center.z, 0f, dt * 6f);   // 화면 안쪽으로 갔던 덩어리는 싸우는 면으로 돌아온다
                }
                blob.bulge = Mathf.MoveTowards(blob.bulge, 0f, dt * 1.5f);

                if (drifting)
                {
                    float want = Mathf.Clamp(P.x - Side * preferredDistance, arenaMinX + blob.radius.x * 0.6f, arenaMaxX - blob.radius.x * 0.6f);
                    float v = Mathf.Clamp(want - blob.center.x, -1f, 1f) * 1.6f * D.speed;
                    blob.center.x += v * dt;
                    blob.bulgeDir = new Vector3(Mathf.Sign(v), 0f, 0f);
                    blob.bulge = Mathf.Max(blob.bulge, Mathf.Abs(v) * 0.18f);   // 흐르는 방향으로 앞이 부푼다
                }

                if (D.absorb && Phase != BossPhase.Intro) Absorb(dt);

                if (Phase == BossPhase.Phase1 && Mass01 < phase2At) SetPhase(BossPhase.Phase2);
                if ((Phase == BossPhase.Phase1 || Phase == BossPhase.Phase2) && Mass01 < deathAt)
                {
                    StopAllCoroutines();
                    ResetGuides();
                    ReleaseAllTo(RSNanoSwarm.G_Blob);
                    StartCoroutine(D.phase3 ? FakeDeathAndAwaken() : Defeat());
                }
            }
            else if (Phase == BossPhase.Phase3 || Phase == BossPhase.Awakening)
            {
                UpdateKnight(dt);
                if (Phase == BossPhase.Phase3 && Mass01 < knightDeathAt)
                {
                    StopAllCoroutines();
                    ResetGuides();
                    StartCoroutine(Defeat());
                }
            }
        }

        void Absorb(float dt)
        {
            absorbTimer += dt;
            if (absorbTimer < 0.08f) return;
            absorbTimer = 0f;
            var bots = sw.Bots;
            Vector3 c = sw.Blob.center;
            int got = 0;
            for (int k = 0; k < 64 && got < 2; k++)
            {
                int i = Random.Range(0, bots.Length);
                if (bots[i].mode != BotMode.Debris) continue;
                if (Mathf.Abs(bots[i].pos.x - c.x) > sw.Blob.radius.x + 4f) continue;
                sw.Bind(i, RSNanoSwarm.G_Blob);
                got++;
            }
        }

        void ReleaseAllTo(int guide)
        {
            var bots = sw.Bots;
            for (int i = 0; i < bots.Length; i++)
            {
                if (bots[i].mode == BotMode.Bound && bots[i].guide != guide) sw.Bind(i, guide);
                else if (bots[i].mode == BotMode.Free) bots[i].home = guide;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 흐름
        // ─────────────────────────────────────────────────────────────
        IEnumerator Run()
        {
            // 등장 — 바닥에 흩어진 봇이 가운데부터 솟아올라 덩어리가 된다
            SetPhase(BossPhase.Intro);
            CurrentPattern = "등장";
            float cx = sw.Blob.center.x;
            sw.ScatterOnFloor(cx - 6f, cx + 6f);
            yield return new WaitForSeconds(0.6f);
            var all = sw.AllIn(BotMode.Debris, new List<int>());
            all.Sort((a, b) => Mathf.Abs(sw.Bots[a].pos.x - cx).CompareTo(Mathf.Abs(sw.Bots[b].pos.x - cx)));
            float dur = 1.6f, t = 0f;
            int next = 0;
            while (next < all.Count)
            {
                t += Time.deltaTime;
                int upto = Mathf.Min(all.Count, Mathf.CeilToInt(all.Count * Mathf.Clamp01(t / dur)));
                for (; next < upto; next++) sw.Bind(all[next], RSNanoSwarm.G_Blob);
                yield return null;
            }
            yield return new WaitForSeconds(1.2f);

            SetPhase(BossPhase.Phase1);
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
                    string pick = queued ?? Pick(last);
                    queued = null;
                    last = pick;
                    yield return RunPattern(pick);
                }

                if (Random.value < D.chain && !PendingAny) continue;   // 연계: 쉬지 않고 바로 다음
                yield return Idle(Rc(1.1f));
            }
        }

        public static readonly string[] SwarmPatterns = { "가시 찌르기", "바닥 가시", "화면 뒤 가시", "휘두르기", "몸통 휘두르기", "빔", "덮치기" };

        string Pick(string last)
        {
            var pool = new List<(string, float)> { ("가시 찌르기", 3f), ("휘두르기", 2f) };
            if (D.beam) pool.Add(("빔", 2f));
            if (D.depthAttacks) { pool.Add(("바닥 가시", 2f)); pool.Add(("화면 뒤 가시", 1.5f)); }
            if (Phase == BossPhase.Phase2)
            {
                pool.Add(("몸통 휘두르기", 2.5f));
                if (D.slam) pool.Add(("덮치기", 2.5f));
            }
            float total = 0f;
            foreach (var p in pool) if (p.Item1 != last) total += p.Item2;
            float r = Random.value * total;
            foreach (var p in pool)
            {
                if (p.Item1 == last) continue;
                r -= p.Item2;
                if (r <= 0f) return p.Item1;
            }
            return pool[0].Item1;
        }

        IEnumerator RunPattern(string name)
        {
            CurrentPattern = name;
            drifting = false;
            switch (name)
            {
                case "가시 찌르기": yield return Spike(); break;
                case "바닥 가시": yield return GroundSpike(); break;
                case "화면 뒤 가시": yield return BackSpike(); break;
                case "휘두르기": yield return Whip(false); break;
                case "몸통 휘두르기": yield return Whip(true); break;
                case "빔": yield return Beam(); break;
                case "덮치기": yield return Slam(); break;
                default: yield return Spike(); break;
            }
            CurrentPattern = "";
        }

        IEnumerator Idle(float dur)
        {
            drifting = true;
            float t = 0f;
            while (t < dur && !PendingAny) { t += Time.deltaTime; yield return null; }
            drifting = false;
        }

        // ─────────────────────────────────────────────────────────────
        // 도우미
        // ─────────────────────────────────────────────────────────────
        bool P2 => Phase == BossPhase.Phase2;
        float Tg(float s) => s * D.telegraph * (P2 ? 0.85f : 1f);
        float Rc(float s) => s * D.recover * (P2 ? 0.8f : 1f);
        float Sp => D.speed * (P2 ? 1.15f : 1f);

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
        static float EaseIn(float x) => x * x * x;
        static float Smooth(float x) => x * x * (3f - 2f * x);

        /// <summary>
        /// 덩어리에서 지점에 가까운 봇을 frac 비율만큼 떼어 줄기에 붙인다 (가까운 봇 = 뿌리 쪽).
        /// 자리는 격자 — 뿌리부터 줄 × 두께 칸으로 맞물린다 (줄기의 r0 · r1 을 먼저 정해 둔다)
        /// </summary>
        List<int> TakeToTube(float frac, Vector3 near, int guide, int min = 30)
        {
            int n = Mathf.Max(min, Mathf.RoundToInt(sw.guideCount[RSNanoSwarm.G_Blob] * frac));
            var ids = sw.Collect(RSNanoSwarm.G_Blob, near, n, new List<int>());
            var tube = guide == RSNanoSwarm.G_TubeB ? sw.TubeB : sw.TubeA;
            var lat = NanoLattice.Taper(Mathf.Max(1, ids.Count), tube.r0, tube.r1, 7f);
            for (int k = 0; k < ids.Count; k++)
            {
                ref NanoBot b = ref sw.Bots[ids[k]];
                b.u = lat[k].x;
                b.v = new Vector3(lat[k].y, lat[k].z, 0f);
                sw.Bind(ids[k], guide);
            }
            return ids;
        }

        void BindRandomV(int i, int guide)
        {
            sw.Bots[i].v = new Vector3(Random.value, Random.value, Random.value);
            sw.Bind(i, guide);
        }

        void Release(List<int> ids, int fromGuide)
        {
            var bots = sw.Bots;
            foreach (int i in ids)
            {
                if (bots[i].mode == BotMode.Bound && bots[i].guide == fromGuide) sw.Bind(i, RSNanoSwarm.G_Blob);
                else if (bots[i].mode == BotMode.Free && bots[i].home == fromGuide) bots[i].home = RSNanoSwarm.G_Blob;
            }
        }

        static readonly Color32 Warn = new Color32(255, 150, 140, 255);
        static readonly Color32 White = new Color32(255, 255, 255, 255);

        void ClampNodesToFloor(TubeGuide tube)
        {
            float fl = sw.floorY + 0.15f;
            for (int i = 0; i < TubeGuide.K; i++) if (tube.nodes[i].y < fl) tube.nodes[i].y = fl;
        }

        // ─────────────────────────────────────────────────────────────
        // 1 · 2페 패턴
        // ─────────────────────────────────────────────────────────────

        /// <summary>가시 찌르기 — 플레이어 쪽이 부풀며 봇이 한 점에 뭉친다(예고) → 조준을 고정하고 한 번에 뻗는다</summary>
        IEnumerator Spike()
        {
            var blob = sw.Blob;
            var tube = sw.TubeA;
            Vector3 dir = P - blob.center; dir.z = 0f; dir.Normalize();
            Vector3 Root() => blob.center + new Vector3(dir.x * blob.radius.x * 0.75f, dir.y * blob.radius.y * 0.75f, -0.2f);

            tube.r0 = 0.55f; tube.r1 = 0.03f; tube.stiffness = 480f; tube.damping = 30f; tube.maxSpeed = 70f;
            tube.tint = Warn; tube.glow = 0f; tube.danger = false;
            tube.SetLine(Root(), Root(), 0f);
            var ids = TakeToTube(P2 ? 0.16f : 0.12f, Root(), RSNanoSwarm.G_TubeA, 40);

            Vector3 aim = P;
            float tg = Tg(0.75f);
            for (float t = 0f; t < tg; t += Time.deltaTime)
            {
                aim = Vector3.Lerp(aim, P, Time.deltaTime * 6f);
                Vector3 r = Root();
                blob.bulgeDir = dir;
                blob.bulge = Mathf.Lerp(0f, 0.9f, t / tg);
                tube.SetLine(r, r + (aim - r).normalized * 0.9f, Smooth(t / tg));   // 뭉친 봇이 플레이어 쪽으로 고개를 든다
                tube.glow = Mathf.Lerp(0f, 0.8f, t / tg);
                yield return null;
            }

            Vector3 root = Root();
            Vector3 ad = (aim - root); ad.z = 0f; ad.Normalize();
            float len = Mathf.Clamp(Vector3.Distance(root, aim) + 1.5f, 4f, 12f);
            tube.danger = true;
            float st = 0.12f / Sp;
            for (float t = 0f; t < st; t += Time.deltaTime) { tube.SetLine(root, root + ad * len, EaseOutBack(Mathf.Clamp01(t / st))); yield return null; }
            tube.SetLine(root, root + ad * len, 1f);
            yield return new WaitForSeconds(Rc(0.3f));
            tube.danger = false;
            tube.glow = 0f;
            for (float t = 0f; t < 0.35f; t += Time.deltaTime) { tube.SetLine(root, root + ad * len, 1f - Smooth(t / 0.35f)); yield return null; }
            tube.tint = White;
            Release(ids, RSNanoSwarm.G_TubeA);
        }

        /// <summary>바닥 가시 — 봇이 바닥을 기어 플레이어 발밑으로 모인다(예고) → 그 자리에서 솟는다</summary>
        IEnumerator GroundSpike()
        {
            var blob = sw.Blob;
            var tube = sw.TubeA;
            float fl = sw.floorY;
            float s = Side;
            Vector3 a = new Vector3(blob.center.x + s * blob.radius.x * 0.6f, fl + 0.15f, 0f);
            tube.r0 = 0.3f; tube.r1 = 0.3f; tube.stiffness = 300f; tube.damping = 24f; tube.maxSpeed = 40f;
            tube.tint = Warn; tube.danger = false;
            tube.SetLine(a, a, 0f);
            var ids = TakeToTube(0.12f, a, RSNanoSwarm.G_TubeA, 40);

            float px = P.x;
            float tg = Tg(0.95f);
            for (float t = 0f; t < tg; t += Time.deltaTime)
            {
                px = Mathf.Lerp(px, P.x, Time.deltaTime * 5f);
                a = new Vector3(blob.center.x + s * blob.radius.x * 0.6f, fl + 0.15f, 0f);
                tube.SetLine(a, new Vector3(px, fl + 0.15f, 0f), Smooth(t / tg));
                tube.r1 = Mathf.Lerp(0.3f, 0.75f, t / tg);   // 발밑에 고이며 넓어진다
                tube.glow = Mathf.Lerp(0f, 1f, t / tg);
                yield return null;
            }

            Vector3 bottom = new Vector3(px, fl - 0.4f, 0f), top = new Vector3(px, fl + 4.4f, 0f);
            tube.r0 = 0.8f; tube.r1 = 0.03f; tube.stiffness = 520f; tube.damping = 30f; tube.maxSpeed = 90f;
            tube.danger = true;
            float st = 0.1f / Sp;
            for (float t = 0f; t < st; t += Time.deltaTime) { tube.SetLine(bottom, top, EaseOutBack(Mathf.Clamp01(t / st))); yield return null; }
            tube.SetLine(bottom, top, 1f);
            yield return new WaitForSeconds(Rc(0.35f));
            tube.danger = false;
            tube.glow = 0f;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime) { tube.SetLine(bottom, top, 1f - Smooth(t / 0.3f)); yield return null; }
            tube.tint = White;
            Release(ids, RSNanoSwarm.G_TubeA);
        }

        /// <summary>화면 뒤 가시 — 줄기가 화면 안쪽 높은 곳으로 흘러가 뭉친다(예고) → 거기서 플레이어에게 내리꽂힌다</summary>
        IEnumerator BackSpike()
        {
            var blob = sw.Blob;
            var tube = sw.TubeA;
            float s = Side;
            Vector3 From() => blob.center + new Vector3(0f, blob.radius.y * 0.4f, 0.5f);
            Vector3 bg = new Vector3(Mathf.Clamp(P.x - s * 2f, arenaMinX, arenaMaxX), P.y + 4f, 9f);
            tube.r0 = 0.45f; tube.r1 = 0.2f; tube.stiffness = 260f; tube.damping = 22f; tube.maxSpeed = 45f;
            tube.tint = Warn; tube.danger = false;
            tube.SetLine(From(), From(), 0f);
            var ids = TakeToTube(0.14f, From(), RSNanoSwarm.G_TubeA, 50);

            float tg = Tg(1.0f);
            for (float t = 0f; t < tg; t += Time.deltaTime) { tube.SetLine(From(), bg, Smooth(t / tg)); yield return null; }
            // 뒤에서 한 덩어리로 뭉친다
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                tube.SetLine(Vector3.Lerp(From(), bg, Smooth(t / 0.3f)), bg, 1f);
                tube.glow = Mathf.Lerp(0f, 1.2f, t / 0.3f);
                yield return null;
            }
            Vector3 aim = P;
            Vector3 dir = (aim - bg).normalized;
            float len = Vector3.Distance(bg, aim) + 1.5f;
            tube.r0 = 0.6f; tube.r1 = 0.03f; tube.stiffness = 520f; tube.maxSpeed = 95f;
            tube.danger = true;
            float st = 0.16f / Sp;
            for (float t = 0f; t < st; t += Time.deltaTime) { tube.SetLine(bg, bg + dir * len, EaseOutBack(Mathf.Clamp01(t / st))); yield return null; }
            tube.SetLine(bg, bg + dir * len, 1f);
            yield return new WaitForSeconds(Rc(0.3f));
            tube.danger = false;
            tube.glow = 0f;
            for (float t = 0f; t < 0.35f; t += Time.deltaTime) { tube.SetLine(bg, bg + dir * len, 1f - Smooth(t / 0.35f)); yield return null; }
            tube.tint = White;
            Release(ids, RSNanoSwarm.G_TubeA);
        }

        /// <summary>
        /// 휘두르기 — 덩어리 일부가 위로 솟아 뒤로 젖혔다가(예고) 앞으로 내리친다. 사슬 물리라 끝이 늦게 휘감긴다.
        /// big = 몸통 휘두르기 (2페): 질량의 절반 가까이를 굵게 휘두른다
        /// </summary>
        IEnumerator Whip(bool big)
        {
            var blob = sw.Blob;
            var tube = sw.TubeB;
            float s = Side;
            float length = big ? 10f : 7f;
            Vector3 Root() => blob.center + new Vector3(0f, blob.radius.y * 0.35f, -0.1f);

            tube.r0 = big ? 1.05f : 0.5f; tube.r1 = big ? 0.25f : 0.07f;
            tube.stiffness = big ? 220f : 320f; tube.damping = big ? 18f : 24f; tube.maxSpeed = 80f;
            tube.tint = big ? Warn : White; tube.danger = false;
            tube.ResetChain(90f);
            tube.ChainStep(Root(), 90f, 0.5f, 0.016f);
            var ids = TakeToTube(big ? 0.42f : 0.18f, Root() + Vector3.up * 2f, RSNanoSwarm.G_TubeB, 40);

            float windup = 90f + (big ? 45f : 32f) * s;
            float end = 90f - 112f * s;
            float L = 0.5f, ang = 90f;
            float chainK = big ? 170f : 240f;

            void StepChain(float dt)
            {
                tube.ChainStep(Root(), ang, L, dt * 0.5f, chainK);
                tube.ChainStep(Root(), ang, L, dt * 0.5f, chainK);
                ClampNodesToFloor(tube);
            }

            float tg = Tg(big ? 1.0f : 0.8f);
            for (float t = 0f; t < tg; t += Time.deltaTime)
            {
                float k = Smooth(t / tg);
                L = Mathf.Lerp(0.5f, length * 0.8f, k);
                ang = Mathf.Lerp(90f, windup, k);
                blob.bulgeDir = Vector3.up;
                blob.bulge = 0.6f * k;
                StepChain(Time.deltaTime);
                yield return null;
            }

            tube.danger = true;
            float st = (big ? 0.42f : 0.3f) / Sp;
            for (float t = 0f; t < st; t += Time.deltaTime)
            {
                float k = EaseIn(Mathf.Clamp01(t / st));
                ang = Mathf.Lerp(windup, end, k);
                L = Mathf.Lerp(length * 0.8f, length, k);
                StepChain(Time.deltaTime);
                yield return null;
            }
            for (float t = 0f; t < Rc(0.4f); t += Time.deltaTime)
            {
                if (t > 0.18f) tube.danger = false;
                StepChain(Time.deltaTime);
                yield return null;
            }
            tube.danger = false;
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                float k = Smooth(t / 0.45f);
                L = Mathf.Lerp(length, 0.5f, k);
                ang = Mathf.Lerp(end, 90f, k);
                StepChain(Time.deltaTime);
                yield return null;
            }
            tube.tint = White;
            Release(ids, RSNanoSwarm.G_TubeB);
        }

        /// <summary>빔 — 봇이 고리로 모여 소용돌이치며 압축 · 발광(예고, 마지막에 조준 고정) → 봇 줄기를 쏟아 낸다</summary>
        IEnumerator Beam()
        {
            var blob = sw.Blob;
            var ring = sw.Ring;
            Vector3 dir = P - blob.center; dir.z = 0f; dir.Normalize();
            Vector3 Mouth() => blob.center + dir * (blob.radius.x + 1.0f) + Vector3.up * 0.3f;

            ring.center = Mouth(); ring.axis = dir; ring.radius = 1.7f; ring.length = 1.0f;
            ring.tint = new Color32(190, 240, 255, 255); ring.glow = 0f; ring.danger = false;
            var ids = sw.Collect(RSNanoSwarm.G_Blob, ring.center, Mathf.Max(60, Mathf.RoundToInt(sw.guideCount[RSNanoSwarm.G_Blob] * 0.2f)), new List<int>());
            foreach (int i in ids) BindRandomV(i, RSNanoSwarm.G_Ring);

            float tg = Tg(1.25f), spin = 2f;
            for (float t = 0f; t < tg; t += Time.deltaTime)
            {
                float k = t / tg;
                if (t < tg - 0.3f * D.telegraph)   // 마지막 순간에는 조준을 고정한다 — 피할 틈
                {
                    Vector3 want = P - blob.center; want.z = 0f; want.Normalize();
                    dir = Vector3.Slerp(dir, want, Time.deltaTime * 2.5f);
                }
                spin = Mathf.Lerp(2f, 16f, k);
                ring.angle += spin * Time.deltaTime;
                ring.center = Mouth(); ring.axis = dir;
                ring.radius = Mathf.Lerp(1.7f, 0.55f, Smooth(k));
                ring.glow = Mathf.Lerp(0f, 2.6f, k);
                blob.bulgeDir = dir; blob.bulge = 0.5f * k;
                yield return null;
            }

            sw.freeGlow = 3.2f;
            sw.freeTint = new Color32(200, 245, 255, 255);
            float fire = 1.0f, carry = 0f;
            int rr = 0;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
            for (float t = 0f; t < fire; t += Time.deltaTime)
            {
                ring.angle += spin * Time.deltaTime;
                ring.center = Mouth();
                blob.center -= dir * (0.6f * Time.deltaTime);   // 반동
                carry += Time.deltaTime * 260f * Sp;
                int shots = (int)carry;
                carry -= shots;
                for (int k = 0; k < shots && ids.Count > 0; k++)
                {
                    for (int tries = 0; tries < ids.Count; tries++)
                    {
                        int i = ids[rr++ % ids.Count];
                        if (sw.Bots[i].mode != BotMode.Bound || sw.Bots[i].guide != RSNanoSwarm.G_Ring) continue;
                        Vector3 jitter = perp * Random.Range(-0.25f, 0.25f) + Vector3.forward * Random.Range(-0.2f, 0.2f);
                        sw.Fire(i, ring.center + jitter, 0f, dir * 44f * Sp, 0.42f, RSNanoSwarm.G_Ring, default, 0f, true);
                        break;
                    }
                }
                yield return null;
            }
            ring.glow = 0f;
            yield return new WaitForSeconds(0.2f);
            sw.freeGlow = 0f;
            sw.freeTint = White;
            Release(ids, RSNanoSwarm.G_Ring);
            yield return new WaitForSeconds(Rc(0.3f));
        }

        /// <summary>덮치기 (2페) — 납작하게 웅크렸다가(예고) 플레이어 위로 뛰어올라 내려찍는다. 착지하면 바닥으로 봇이 퍼진다</summary>
        IEnumerator Slam()
        {
            var blob = sw.Blob;
            float tg = Tg(0.75f);
            for (float t = 0f; t < tg; t += Time.deltaTime)
            {
                float k = Smooth(t / tg);
                blobSquash = new Vector3(Mathf.Lerp(1f, 1.2f, k), Mathf.Lerp(1f, 0.7f, k), 1f);
                yield return null;
            }
            float x0 = blob.center.x;
            float x1 = Mathf.Clamp(P.x, arenaMinX + 2f, arenaMaxX - 2f);
            float jt = 0.8f / Sp;
            for (float t = 0f; t < jt; t += Time.deltaTime)
            {
                float k = t / jt;
                blob.center.x = Mathf.Lerp(x0, x1, Smooth(k));
                blobLift = Mathf.Sin(k * Mathf.PI) * 6.5f;
                blobSquash = new Vector3(0.85f, 1.25f, 1f);
                blob.danger = k > 0.55f;
                yield return null;
            }
            blobLift = 0f;
            blobSquash = new Vector3(1.4f, 0.6f, 1f);
            // 착지 충격파 — 바닥을 따라 양쪽으로 봇이 퍼진다
            var ids = sw.Collect(RSNanoSwarm.G_Blob, new Vector3(blob.center.x, sw.floorY, 0f), 36, new List<int>());
            for (int k = 0; k < ids.Count; k++)
            {
                float side = k % 2 == 0 ? 1f : -1f;
                Vector3 hold = new Vector3(blob.center.x + side * blob.radius.x * 0.8f, sw.floorY + 0.25f + (k / 2 % 3) * 0.22f, 0f);
                sw.Fire(ids[k], hold, 0f, new Vector3(side * 10f * Sp, 0f, 0f), 0.9f, RSNanoSwarm.G_Blob, default, 0f, true);
            }
            yield return new WaitForSeconds(0.15f);
            blob.danger = false;
            for (float t = 0f; t < Rc(0.6f); t += Time.deltaTime)
            {
                blobSquash = Vector3.Lerp(blobSquash, Vector3.one, Time.deltaTime * 6f);
                yield return null;
            }
            blobSquash = Vector3.one;
        }

        // ─────────────────────────────────────────────────────────────
        // 격파
        // ─────────────────────────────────────────────────────────────
        IEnumerator Defeat()
        {
            SetPhase(BossPhase.Defeated);
            CurrentPattern = "격파";
            Vector3 c = BodyCenter;
            var bots = sw.Bots;
            for (int i = 0; i < bots.Length; i++)
            {
                if (bots[i].mode != BotMode.Bound && bots[i].mode != BotMode.Free) continue;
                Vector3 d = bots[i].pos - c;
                sw.Drop(i, d.normalized * Random.Range(2f, 9f) + Vector3.up * Random.Range(1f, 6f));
            }
            for (float t = 0f; t < 1.5f; t += Time.deltaTime) { sw.dark = Mathf.MoveTowards(sw.dark, 0f, Time.deltaTime); yield return null; }
        }
    }
}
