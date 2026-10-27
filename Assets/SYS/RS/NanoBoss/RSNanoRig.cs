// RE:AL STEEL - 나노봇 리그 (애니메이션으로 만드는 공격 모션)
//
// 보스 아래에 두는 게임오브젝트. 자식에 부위(RSNanoLimb) · 발사기(RSNanoEmitter)를 두고,
// 유니티 애니메이션 창에서 부위의 위치 · 회전 · 크기와 값(양 · 결속 · 자라기 · 판정 …)에 키를 잡는다.
// 리그를 선택하면 씬 뷰에서 봇이 따라온다. 애니메이션 창에서 미리보기를 켜면 어느 프레임으로 가든
// 클립 처음부터 다시 계산해 항상 같은 모습을 보여 준다 (앞뒤로 끌어도, 한 프레임씩 넘겨도).
// 메뉴: 창 → RE_AL STEEL → 나노봇 동작 — 버튼 한 번에 키 넣기 · 행동 블록 · 포즈 · 점검
//
// 만드는 규칙
//   · 리그 원점 = 보스 몸 중심. 플레이 중엔 패턴 시작 때 보스 몸 중심으로 옮긴다
//   · +x = 플레이어 쪽. 플레이어가 왼쪽이면 리그를 통째로 좌우 반전한다 → 오른쪽을 보는 것으로만 만들면 된다
//   · '플레이어 기준점' 자식은 매 프레임 플레이어 발밑으로 옮겨진다 (그 아래 부위 = 플레이어 기준).
//     '플레이어 고정' 을 켜는 순간 그 자리에 멈춘다 → 가시가 솟을 자리 · 내리찍을 자리
//   · 리그 원점 · 플레이어 기준점 자체에는 키를 잡지 않는다 (잡으면 편집기가 지운다)
//
// 봇 흐름: 부위의 '양' 이 바뀌면 끝 자리부터 풀고, 모자란 부위는 가까운 봇부터 가져간다 → 부위 사이로 흘러간다.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;

namespace RealSteel.NanoBoss
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(150)]
    [AddComponentMenu("RE_AL STEEL/나노봇 리그")]
    public class RSNanoRig : MonoBehaviour
    {
        [Tooltip("봇을 빌려 올 군집. 비우면 부모에서 찾는다")]
        public RSNanoSwarm swarm;

        [Header("플레이어")]
        [Tooltip("매 프레임 플레이어 발밑으로 옮겨지는 자식")]
        public Transform playerAnchor;
        [Tooltip("켜는 순간 플레이어 기준점을 그 자리에 고정 (키를 잡는다)")]
        public bool lockPlayer;

        [Header("연출")]
        [Tooltip("화면 흔들림 세기 (m, 키를 잡는다). 플레이 중에만")]
        [Min(0f)] public float shake;

        [Header("편집")]
        [Tooltip("리그 원점 · 플레이어 기준점에 키를 잡아도 지우지 않는다 (보통 끔 — 잡아도 덮어써진다)")]
        public bool allowRootKeys;
        [Tooltip("포즈 모음 (나노봇 동작 창 → 포즈)")]
        public RSNanoPoseLibrary poses;

        [Header("에디터 미리보기")]
        [Tooltip("리그를 고르면 에디터에서 봇을 돌려 애니메이션을 미리 본다")]
        public bool preview = true;
        [Tooltip("미리보기용 플레이어. 비우면 아래 위치")]
        public Transform previewTarget;
        [Tooltip("미리보기용 플레이어 발밑 (리그 원점 기준, +x = 앞)")]
        public Vector3 previewOffset = new Vector3(7f, -2.1f, 0f);
        [Tooltip("3페(검은 기사) 상태로 미리본다 — 봇 일부가 기사 몸, 나머지가 후광")]
        public bool previewAsKnight;

        public const int S = NanoSimJob.S, H = NanoSimJob.H;

        public bool Active { get; private set; }
        public float Clock => clock;
        public float Facing => facing;
        public IReadOnlyList<RSNanoLimb> Limbs => limbs;
        /// <summary>리그 한 프레임 (배분 · 자리 정리 · 관절선 기록) 시간 ms — HUD</summary>
        public float TickMs { get; private set; }

        // ── 잡으로 넘기는 값 ──
        public int SimLimbCount => Active ? limbs.Count : 0;
        public LimbSim[] SimLimbs => limbSim;
        public float4[] SimSlots => slotTable;
        public float4[] SimSpine => spine;
        public float[] SimSpineTime => spineTime;

        readonly List<RSNanoLimb> limbs = new List<RSNanoLimb>();
        readonly List<RSNanoEmitter> emitters = new List<RSNanoEmitter>();
        List<int>[] limbBots = Array.Empty<List<int>>();
        List<int>[] fresh = Array.Empty<List<int>>();
        NanoSlotSet[] sets = Array.Empty<NanoSlotSet>();
        Vector4[][] slotsOf = Array.Empty<Vector4[]>();
        int[] slotOffset = Array.Empty<int>(), layoutKey = Array.Empty<int>(), histHead = Array.Empty<int>(), histCount = Array.Empty<int>(), desired = Array.Empty<int>();
        float[] layoutSpace = Array.Empty<float>(), layoutScale = Array.Empty<float>(), lockStart = Array.Empty<float>(), prevBind = Array.Empty<float>();
        LimbSim[] limbSim = new LimbSim[1];
        float4[] slotTable = new float4[1];
        float4[] spine = new float4[S * H];
        float[] spineTime = new float[H];
        int[] orig = Array.Empty<int>();
        readonly List<int> transfer = new List<int>();
        readonly List<int> pool = new List<int>();
        readonly List<int> picked = new List<int>();
        float[] keyBuf = Array.Empty<float>();
        int[] idxBuf = Array.Empty<int>();
        bool[] mark = Array.Empty<bool>();
        int[] claimStamp = Array.Empty<int>();
        int stamp;

        int home = RSNanoSwarm.G_Blob;
        Func<Vector3> playerFeet;
        float facing = 1f, clock;
        bool lockedPrev;
        Vector3 lockedPos;
        Vector3 savedScale;

        public RSNanoLimb LimbAt(int i) => i >= 0 && i < limbs.Count ? limbs[i] : null;
        public Vector3 AimPoint => (playerFeet != null ? playerFeet() : transform.position) + Vector3.up * 0.9f;
        public Vector3 PreviewFeet => previewTarget ? previewTarget.position : transform.position + previewOffset;
        public int SlotCountOf(int li) => li >= 0 && li < slotsOf.Length && slotsOf[li] != null ? slotsOf[li].Length : 0;
        public float SpaceOf(int li) => li >= 0 && li < layoutSpace.Length ? layoutSpace[li] : 0f;

        void Awake()
        {
            if (!Application.isPlaying) return;
            // 애니메이터 컨트롤러는 편집용 — 플레이 중엔 두뇌가 클립을 직접 재생한다
            var an = GetComponent<Animator>();
            if (an) an.runtimeAnimatorController = null;
            if (!swarm) swarm = GetComponentInParent<RSNanoSwarm>();
        }

        public void Collect()
        {
            limbs.Clear();
            GetComponentsInChildren(false, limbs);
            emitters.Clear();
            GetComponentsInChildren(false, emitters);
            for (int i = 0; i < limbs.Count; i++) { limbs[i].index = i; limbs[i].Migrate(); limbs[i].RefreshJoints(); }
        }

        // ─────────────────────────────────────────────────────────────
        // 시작 · 끝
        // ─────────────────────────────────────────────────────────────
        /// <summary>
        /// 패턴 시작. homeGuide = 쓰고 남은 봇이 돌아갈 곳 (1 · 2페 덩어리, 3페 기사).
        /// facing = +1 플레이어가 오른쪽. rootPos = 보스 몸 중심
        /// </summary>
        public void Begin(int homeGuide, Func<Vector3> playerFeetPos, float face, Vector3 rootPos)
        {
            if (!swarm) swarm = GetComponentInParent<RSNanoSwarm>();
            if (!swarm) { UnityEngine.Debug.LogWarning("[나노봇 리그] 군집이 없습니다", this); return; }
            home = homeGuide;
            playerFeet = playerFeetPos;
            facing = face >= 0f ? 1f : -1f;
            transform.position = rootPos;
            savedScale = transform.localScale;
            var s = savedScale;
            s.x = Mathf.Abs(s.x) * facing;
            transform.localScale = s;

            clock = 0f;
            lockedPrev = false;
            swarm.Rig.rig = this;
            Rebind();
            for (int li = 0; li < limbs.Count; li++)
            {
                histHead[li] = -1;
                histCount[li] = 0;
                lockStart[li] = -1f;
                prevBind[li] = limbs[li] ? limbs[li].bind : 1f;
                limbs[li].assigned = 0;
            }
            foreach (var e in emitters) e.ResetState();
            Active = true;
        }

        /// <summary>패턴 끝. 리그가 쓰던 봇을 원래 자리로 돌려보낸다</summary>
        public void End()
        {
            if (!Active) return;
            Active = false;
            if (swarm)
            {
                var bots = swarm.Bots;
                for (int i = 0; i < bots.Length; i++)
                {
                    if (bots[i].mode == BotMode.Bound && bots[i].guide == RSNanoSwarm.G_Rig) swarm.SendHome(i, HomeOf(i));
                    else if (bots[i].mode == BotMode.Free && bots[i].home == RSNanoSwarm.G_Rig) bots[i].home = HomeOf(i);
                }
                swarm.freeGlow = 0f;
            }
            foreach (var l in limbs) if (l) l.assigned = 0;
            for (int li = 0; li < limbBots.Length; li++) limbBots[li].Clear();
            if (Application.isPlaying && savedScale != Vector3.zero) transform.localScale = savedScale;
        }

        int HomeOf(int i)
        {
            int o = i < orig.Length ? orig[i] : -1;
            if (i < orig.Length) orig[i] = -1;
            return o == RSNanoSwarm.G_Blob || o == RSNanoSwarm.G_Halo || o == RSNanoSwarm.G_Knight ? o : home;
        }

        // ─────────────────────────────────────────────────────────────
        // 진행
        // ─────────────────────────────────────────────────────────────
        void LateUpdate()
        {
            if (!Application.isPlaying || !Active) return;
            Tick(Time.deltaTime, Time.time);
        }

        public void Tick(float dt, float time)
        {
            if (!Active || !swarm) return;
            long t0 = Stopwatch.GetTimestamp();
            clock += dt;

            if (playerAnchor && playerFeet != null)
            {
                if (lockPlayer && !lockedPrev) lockedPos = playerFeet();
                playerAnchor.position = lockPlayer ? lockedPos : playerFeet();
            }
            lockedPrev = lockPlayer;

            // 부위가 늘거나 줄었으면 (에디터에서 고치는 중) 다시 모은다
            int liveLimbs = 0;
            foreach (var l in limbs) if (l && l.isActiveAndEnabled) liveLimbs++;
            if (liveLimbs != limbs.Count || limbs.Count != limbBots.Length ||
                (!Application.isPlaying && (GetComponentsInChildren<RSNanoLimb>(false).Length != limbs.Count || GetComponentsInChildren<RSNanoEmitter>(false).Length != emitters.Count)))
                Rebind();

            bool relayout = false;
            for (int li = 0; li < limbs.Count; li++)
            {
                var l = limbs[li];
                l.RefreshJoints();
                if (l.LayoutKey != layoutKey[li]) { Layout(li); relayout = true; }
                l.CachePose();
            }
            if (relayout) ConcatSlots();

            RecordSpines();
            Allocate();
            MaintainSlots();
            for (int li = 0; li < limbs.Count; li++)
            {
                float b = limbs[li].bind;
                if (prevBind[li] < 0.6f && b >= 0.6f) lockStart[li] = clock;   // 재결속 연출
                prevBind[li] = b;
            }
            FillLimbSim();

            foreach (var e in emitters) if (e) e.Tick(swarm, () => AimPoint, facing, clock, dt);
            if (Application.isPlaying && shake > 0.001f) RSNanoCameraShake.Shake(shake, 0.12f);

            float ms = (float)((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
            TickMs = TickMs <= 0f ? ms : Mathf.Lerp(TickMs, ms, 0.1f);
        }

        void Rebind()
        {
            var bots = swarm.Bots;
            for (int i = 0; i < bots.Length; i++)
                if (bots[i].mode == BotMode.Bound && bots[i].guide == RSNanoSwarm.G_Rig) swarm.SendHome(i, HomeOf(i));
            Collect();
            int L = limbs.Count;
            limbBots = new List<int>[L];
            fresh = new List<int>[L];
            sets = new NanoSlotSet[L];
            slotsOf = new Vector4[L][];
            slotOffset = new int[L];
            layoutKey = new int[L];
            layoutSpace = new float[L];
            layoutScale = new float[L];
            histHead = new int[L];
            histCount = new int[L];
            lockStart = new float[L];
            prevBind = new float[L];
            desired = new int[L];
            for (int li = 0; li < L; li++)
            {
                limbBots[li] = new List<int>();
                fresh[li] = new List<int>();
                sets[li] = new NanoSlotSet();
                histHead[li] = -1;
                lockStart[li] = -1f;
                prevBind[li] = limbs[li].bind;
                Layout(li);
            }
            ConcatSlots();
            limbSim = new LimbSim[Mathf.Max(1, L)];
            spine = new float4[Mathf.Max(1, L) * H * S];
            spineTime = new float[Mathf.Max(1, L) * H];
            if (orig.Length != swarm.Count) { orig = new int[swarm.Count]; for (int i = 0; i < orig.Length; i++) orig[i] = -1; }
        }

        void Layout(int li)
        {
            var l = limbs[li];
            l.RefreshJoints();
            var slots = l.BuildSlots(swarm.floorY, out float sp);
            slotsOf[li] = slots;
            layoutSpace[li] = sp;
            layoutScale[li] = Mathf.Max(1e-4f, l.CurScale);
            layoutKey[li] = l.LayoutKey;
            sets[li].Resize(slots.Length);
            var bots = swarm.Bots;
            foreach (int i in limbBots[li]) bots[i].cell = -1;
        }

        void ConcatSlots()
        {
            int total = 0;
            for (int li = 0; li < slotsOf.Length; li++) { slotOffset[li] = total; total += slotsOf[li].Length; }
            if (slotTable.Length < Mathf.Max(1, total)) slotTable = new float4[Mathf.Max(1, total)];
            for (int li = 0; li < slotsOf.Length; li++)
            {
                var s = slotsOf[li];
                for (int k = 0; k < s.Length; k++) slotTable[slotOffset[li] + k] = s[k];
            }
        }

        void RecordSpines()
        {
            for (int li = 0; li < limbs.Count; li++)
            {
                int h = (histHead[li] + 1) % H;
                histHead[li] = h;
                histCount[li] = Mathf.Min(H, histCount[li] + 1);
                spineTime[li * H + h] = clock;
                limbs[li].WriteSpine(spine, (li * H + h) * S);
            }
        }

        static uint Pack(Color32 c) => (uint)c.r | ((uint)c.g << 8) | ((uint)c.b << 16) | ((uint)c.a << 24);

        void FillLimbSim()
        {
            for (int li = 0; li < limbs.Count; li++)
            {
                var l = limbs[li];
                l.GetFeel(out float om, out float ze, out float gom, out float lagS);
                Vector3 ls = l.transform.lossyScale;
                float age = lockStart[li] >= 0f ? clock - lockStart[li] : -1f;
                if (age > 0.6f) age = -1f;
                limbSim[li] = new LimbSim
                {
                    type = (int)l.type,
                    slotStart = slotOffset[li],
                    slotCount = slotsOf[li].Length,
                    histBase = li * H,
                    histHead = Mathf.Max(0, histHead[li]),
                    histCount = histCount[li],
                    local = l.transform.localToWorldMatrix * Matrix4x4.Scale(l.size),
                    origin = l.transform.position,
                    grow = l.grow,
                    depth = l.depth,
                    wobble = l.wobble,
                    fillToFloor = l.fillToFloor ? 1 : 0,
                    extent = l.extent,
                    layer = l.layer,
                    ripple = l.ripple,
                    rippleSpeed = l.rippleSpeed,
                    sx = ls.x < 0f ? -1f : 1f,
                    bumpPos = l.bumpAt ? l.bumpAt.position : l.transform.position,
                    bump = l.bump,
                    bumpRadius = l.bumpRadius,
                    bumpShake = l.bumpShake,
                    hasBump = l.bumpAt ? 1 : 0,
                    omega = om, zeta = ze, gatherOmega = gom,
                    maxSpeed = 150f,
                    flow = l.flow,
                    lag = lagS,
                    bind = Mathf.Clamp01(l.bind),
                    space = layoutSpace[li] * (l.CurScale / layoutScale[li]),
                    lockAge = age,
                    glow = l.glow,
                    tint = Pack(l.tint),
                    tinted = l.tint != Color.white ? 1 : 0,
                    danger = l.danger ? 1 : 0,
                };
            }
        }

        bool InPool(ref NanoBot b) =>
            b.mode == BotMode.Bound && b.wait <= 0f &&
            (b.guide == RSNanoSwarm.G_Blob || b.guide == RSNanoSwarm.G_Halo || (b.guide == RSNanoSwarm.G_Knight && b.seg >= 0));

        /// <summary>list 에서 at 에 가까운 take 개를 골라 picked 에 (list 안 위치). penalty = 기사 몸이면 더하는 거리²</summary>
        void PickNearest(List<int> list, Vector3 at, int take, bool knightPenalty)
        {
            picked.Clear();
            int n = list.Count;
            if (take <= 0 || n == 0) return;
            if (keyBuf.Length < n) { keyBuf = new float[Mathf.Max(n, swarm.Count)]; idxBuf = new int[keyBuf.Length]; mark = new bool[keyBuf.Length]; }
            var bots = swarm.Bots;
            for (int k = 0; k < n; k++)
            {
                int i = list[k];
                float d = (bots[i].pos - at).sqrMagnitude;
                if (knightPenalty && bots[i].guide == RSNanoSwarm.G_Knight && bots[i].mode == BotMode.Bound) d += 400f;   // 3페: 기사 몸보다 후광을 먼저
                keyBuf[k] = d;
                idxBuf[k] = k;
            }
            take = Mathf.Min(take, n);
            if (take <= 16 && n > 64)
            {
                for (int k = 0; k < n; k++) mark[k] = false;
                for (int t = 0; t < take; t++)
                {
                    int best = -1;
                    float bd = float.MaxValue;
                    for (int k = 0; k < n; k++) if (!mark[k] && keyBuf[k] < bd) { bd = keyBuf[k]; best = k; }
                    if (best < 0) break;
                    mark[best] = true;
                    picked.Add(best);
                }
            }
            else
            {
                Array.Sort(keyBuf, idxBuf, 0, n);
                for (int t = 0; t < take; t++) picked.Add(idxBuf[t]);
            }
        }

        /// <summary>list 에서 picked 위치들을 빼고 남은 것만</summary>
        void RemovePicked(List<int> list)
        {
            int n = list.Count;
            for (int k = 0; k < n; k++) mark[k] = false;
            foreach (int k in picked) mark[k] = true;
            int w = 0;
            for (int k = 0; k < n; k++) if (!mark[k]) list[w++] = list[k];
            list.RemoveRange(w, n - w);
        }

        /// <summary>부위마다 원하는 봇 수를 맞춘다. 끝 자리 봇부터 풀고, 모자란 부위가 가까운 봇부터 가져간다</summary>
        void Allocate()
        {
            var bots = swarm.Bots;
            int L = limbs.Count;

            // 맞았거나 쏴서 리그를 떠난 봇은 목록에서 뺀다
            for (int li = 0; li < L; li++)
            {
                var list = limbBots[li];
                int w = 0;
                for (int k = 0; k < list.Count; k++)
                {
                    int i = list[k];
                    if (bots[i].mode == BotMode.Bound && bots[i].guide == RSNanoSwarm.G_Rig && bots[i].slot == li) list[w++] = i;
                }
                list.RemoveRange(w, list.Count - w);
            }

            int have = 0, poolCount = 0;
            for (int li = 0; li < L; li++) have += limbBots[li].Count;
            for (int i = 0; i < bots.Length; i++) if (InPool(ref bots[i])) poolCount++;
            int available = have + poolCount;

            for (int li = 0; li < L; li++) desired[li] = limbs[li] ? Mathf.Min(limbs[li].Desired, sets[li].Count) : 0;
            // 다른 부위에서 받아 쓰는 부위가 있으면, 준 쪽은 그만큼 덜 원한다 (되찾아 가지 않게)
            for (int li = 0; li < L; li++)
            {
                var f = limbs[li] ? limbs[li].feedFrom : null;
                if (f && f.index >= 0 && f.index < L && f.index != li) desired[f.index] = Mathf.Max(0, desired[f.index] - desired[li]);
            }
            int want = 0;
            for (int li = 0; li < L; li++) want += desired[li];
            if (want > available && want > 0)
            {
                float k = available / (float)want;
                for (int li = 0; li < L; li++) desired[li] = Mathf.FloorToInt(desired[li] * k);
            }

            // 받아 쓰기 — feedFrom 부위에서 가까운 봇부터 바로 옮긴다
            for (int li = 0; li < L; li++)
            {
                var limb = limbs[li];
                var f = limb ? limb.feedFrom : null;
                if (!f || f.index < 0 || f.index >= L || f.index == li) continue;
                int need = desired[li] - limbBots[li].Count;
                var src = limbBots[f.index];
                if (need <= 0 || src.Count == 0) continue;
                PickNearest(src, limb.transform.position, need, false);
                foreach (int k in picked)
                {
                    int i = src[k];
                    bots[i].slot = li;
                    bots[i].cell = -1;
                    limbBots[li].Add(i);
                }
                RemovePicked(src);
            }

            // 풀기 — 끝 자리(채움 순서 뒤)부터
            transfer.Clear();
            for (int li = 0; li < L; li++)
            {
                var list = limbBots[li];
                int excess = list.Count - desired[li];
                if (excess <= 0) continue;
                int n = list.Count;
                if (keyBuf.Length < n) { keyBuf = new float[Mathf.Max(n, swarm.Count)]; idxBuf = new int[keyBuf.Length]; mark = new bool[keyBuf.Length]; }
                for (int k = 0; k < n; k++) { int c = bots[list[k]].cell; keyBuf[k] = c < 0 ? 1e9f : c; idxBuf[k] = list[k]; }
                Array.Sort(keyBuf, idxBuf, 0, n);
                list.Clear();
                for (int k = 0; k < n - excess; k++) list.Add(idxBuf[k]);
                for (int k = n - excess; k < n; k++) transfer.Add(idxBuf[k]);
            }

            // 가져오기 — 방금 풀린 봇 + 남는 봇 중 가까운 것부터
            bool anyNeed = false;
            for (int li = 0; li < L; li++) if (desired[li] > limbBots[li].Count) { anyNeed = true; break; }
            stamp++;
            if (claimStamp.Length != bots.Length) claimStamp = new int[bots.Length];
            if (anyNeed)
            {
                pool.Clear();
                pool.AddRange(transfer);
                for (int i = 0; i < bots.Length; i++) if (InPool(ref bots[i])) pool.Add(i);
                for (int li = 0; li < L; li++)
                {
                    int need = desired[li] - limbBots[li].Count;
                    if (need <= 0 || pool.Count == 0) continue;
                    var limb = limbs[li];
                    Vector3 at = limb.type == LimbType.Puddle && limb.bumpAt ? limb.bumpAt.position : limb.transform.position;
                    PickNearest(pool, at, need, true);
                    foreach (int k in picked)
                    {
                        int i = pool[k];
                        if (orig[i] < 0 && bots[i].mode == BotMode.Bound && bots[i].guide != RSNanoSwarm.G_Rig) orig[i] = bots[i].guide;
                        swarm.Bind(i, RSNanoSwarm.G_Rig);
                        bots[i].slot = li;
                        bots[i].cell = -1;
                        limbBots[li].Add(i);
                        fresh[li].Add(i);
                        claimStamp[i] = stamp;
                    }
                    RemovePicked(pool);
                }
                // 아무도 안 가져간 풀린 봇은 집으로
                foreach (int i in transfer)
                    if (claimStamp[i] != stamp) swarm.SendHome(i, HomeOf(i));
            }
            else
            {
                foreach (int i in transfer) swarm.SendHome(i, HomeOf(i));
            }
            for (int li = 0; li < L; li++) if (limbs[li]) limbs[li].assigned = limbBots[li].Count;
        }

        /// <summary>격자 자리 정리 + 새로 온 봇의 출발 순서</summary>
        void MaintainSlots()
        {
            var bots = swarm.Bots;
            for (int li = 0; li < limbs.Count; li++)
            {
                var set = sets[li];
                set.Maintain(bots, limbBots[li], RSNanoSwarm.G_Rig, li, clock, swarm.refillDelay, Mathf.Max(2, set.Count / 30), null, slotsOf[li]);
                var fl = fresh[li];
                if (fl.Count == 0) continue;
                var l = limbs[li];
                float gsec = l.gather / 60f;
                foreach (int i in fl)
                {
                    if (bots[i].cell < 0) continue;
                    float frac = bots[i].cell / (float)Mathf.Max(1, set.Count);
                    bots[i].wait = l.gatherOrder == GatherOrder.Root ? frac * gsec * 0.6f
                                 : l.gatherOrder == GatherOrder.Random ? bots[i].hash * gsec * 0.6f : 0f;
                }
                fl.Clear();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 에디터 미리보기 (RSNanoRigPreview 가 부른다)
        // ─────────────────────────────────────────────────────────────
        public void BeginPreview()
        {
            if (Active) End();
            if (!swarm) swarm = GetComponentInParent<RSNanoSwarm>();
            if (!swarm) swarm = FindAnyObjectByType<RSNanoSwarm>();
            if (!swarm) return;
            swarm.ResetRandom(1234);
            var brain = swarm.GetComponent<RSNanoBossBrain>();
            Vector3 shape = brain ? brain.blobRadius : new Vector3(3.4f, 2.7f, 0.9f);
            swarm.Blob.aspect = shape.x / Mathf.Max(0.01f, shape.y);
            if (swarm.floorFromTransform) swarm.floorY = swarm.transform.position.y;
            swarm.Blob.radius = swarm.BlobSize(swarm.botCount, shape);
            swarm.Build(transform.position);
            swarm.highlightDanger = true;
            swarm.dark = 0f;
            int homeG = RSNanoSwarm.G_Blob;
            if (previewAsKnight) { SetupKnightPreview(); homeG = RSNanoSwarm.G_Knight; }
            swarm.Settle(0.25f);
            savedScale = Vector3.zero;   // 에디터에선 스케일을 건드리지 않는다
            Begin(homeG, () => PreviewFeet, 1f, transform.position);
        }

        void SetupKnightPreview()
        {
            var k = swarm.Knight;
            k.origin = new Vector3(transform.position.x, swarm.floorY + 0.6f, transform.position.z);
            k.facing = 1f;
            k.lean = 0f; k.armAngle = 0f; k.bladeAngle = 0f;
            k.stiffness = 900f; k.damping = 55f; k.maxSpeed = 95f; k.noise = 0.004f;
            k.UpdatePose();
            swarm.Halo.center = k.ChestWorld;
            var bots = swarm.Bots;
            var rng = new System.Random(99);
            float R() => (float)rng.NextDouble();
            int knightN = RSNanoBossBrain.KnightBotCount(swarm);
            for (int i = 0; i < bots.Length; i++)
            {
                if (i < knightN)
                {
                    KnightGuide.Sample(ref bots[i], NanoLattice.Halton(i, 2), NanoLattice.Halton(i, 3), NanoLattice.Halton(i, 5), NanoLattice.Halton(i, 7));
                    swarm.Bind(i, RSNanoSwarm.G_Knight);
                }
                else { bots[i].v = new Vector3(R(), R(), R()); swarm.Bind(i, RSNanoSwarm.G_Halo); }
                bots[i].glow = 0f;
            }
            swarm.dark = 1f;
        }

        public void StopPreview()
        {
            End();
            if (swarm && !Application.isPlaying)
            {
                swarm.highlightDanger = false;
                swarm.dark = 0f;
                swarm.ClearBots();
            }
        }
    }
}
