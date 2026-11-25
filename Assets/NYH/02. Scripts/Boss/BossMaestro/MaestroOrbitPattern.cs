using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using RealSteel.NanoBoss;
using UnityEngine;

/// <summary>
/// 마에스트로 「원형 음탄」 (2026-11-24, 사용자 시트 '음표 폭풍 / 원형 음탄' — 2악장 후보, 기획서에 아직 없음).
///   ① 두 팔을 활짝 펼치면 몸의 봇이 흘러나와 보스 둘레 고리 위에 음표(8분음표)가 생긴다
///   ② 고리가 돈다 (예고) — 음표가 은은하게 맥동
///   ③ 박자마다 고리 순서대로 하나씩 바깥(보스 → 음표 방향)으로 발사 — 고리가 계속 돌아서 발사 방향이 나선으로 퍼진다
///   ④ 마지막 박에 남은 음표가 한꺼번에 터지듯 사방으로
///   ⑤ 플레이어·벽·바닥에 닿으면 고리로 터져 모래 → 재흡수 (음표 공격과 같다)
/// 회피: 발사 박자를 읽고 탄 사이로. 회전 방향은 매번 무작위. 타격 부위는 패턴 데이터(임시 왼팔).
/// 로직은 전투 틱(CombatClock), 그림은 LateUpdate. 수치는 MaestroOrbitData(전부 TODO 임시값).
/// </summary>
public class MaestroOrbitPattern : BossPatternGimmickBase, IMaestroMovementGate
{
    [SerializeField] private MaestroOrbitData orbitData;
    [Tooltip("선율·녹음 음원 공용 (음표 공격 수치). 비우면 소리 없음")]
    [SerializeField] private MaestroNoteData noteData;
    [Tooltip("이 악장부터 나온다 (2 = 2악장 후보). 1로 바꾸면 1악장에서도 시험할 수 있다")]
    [Min(1)] [SerializeField] private int minMovement = 2;

    public int MinMovement => minMovement;

    private enum OrbState { Orbit, Flying, Done }

    private class Orb
    {
        public OrbState state;
        public int fireFrame, flightFrames;
        public float slot;                    // 고리 위 자리 (라디안)
        public Vector3 pos, prevPos, vel;
        public readonly List<int> bots = new List<int>(256);
        public readonly List<Vector3> offsets = new List<Vector3>(256);
        public SpriteRenderer halo;
        public float flash;
    }

    private static readonly List<Collider2D> overlap = new List<Collider2D>(8);
    private static readonly ContactFilter2D overlapFilter = new ContactFilter2D { useTriggers = true };

    private MaestroBossController maestro;
    private MaestroOrbitData.DifficultyRow row;
    private readonly List<Orb> orbs = new List<Orb>(16);
    private readonly List<int> tmp = new List<int>(512);
    private readonly List<Vector3> shape = new List<Vector3>(512);
    private MaestroFx fx;
    private AudioSource audioSource;
    private bool running, subscribed;
    private int frame, gatherEnd, lastFire, fired;
    private float ringAngle, spinDir, ringRadius;

    protected override bool UseExternalTellPhase => true;
    private RSNanoSwarm Swarm => maestro != null ? maestro.Swarm : null;
    private static int Frames(float s) => Mathf.Max(0, Mathf.RoundToInt(s / CombatClock.TICK));

    protected override void OnAttached()
    {
        maestro = OwnerBoss as MaestroBossController;
        if (maestro == null) Debug.LogError($"[MaestroOrbitPattern] {name}: 마에스트로 밑에 있어야 함");
        if (orbitData == null) Debug.LogError($"[MaestroOrbitPattern] {name}: 원형 음탄 수치(MaestroOrbitData)가 비어 있음");
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    protected override void OnPreExecute()
    {
        Clear();
        var swarm = Swarm;
        if (maestro == null || orbitData == null || swarm == null) return;
        row = orbitData.Row(maestro.SelectedDifficulty);
        if (row == null) return;
        fx = MaestroFx.Get(maestro);

        // 지휘봉 패턴처럼 배경(뒤판)으로 물러난다 — 그동안 무적(본체 판정도 꺼짐), 끝나면 제자리 (11-25 사용자)
        maestro.SetBodyPose(orbitData.backOffset, orbitData.backGrow, true);
        if (OwnerBoss.Snapshot != null) OwnerBoss.Snapshot.isInvincible = true;

        float bossH = maestro.Look != null && maestro.Look.Ready ? maestro.Look.Height : 3f;
        ringRadius = bossH * orbitData.ringRadiusRatio;
        ringAngle = Random.value * Mathf.PI * 2f;
        spinDir = Random.value < 0.5f ? -1f : 1f;

        gatherEnd = Frames(orbitData.gatherSeconds);
        int fireStart = gatherEnd + Frames(orbitData.spinSeconds);
        int interval = Mathf.Max(1, Frames(row.interval));
        int n = Mathf.Max(3, row.notes);
        int single = Mathf.Max(0, n - Mathf.Clamp(row.finalBurst, 0, n));
        int burstFrame = fireStart + single * interval + Frames(orbitData.burstPause);
        for (int k = 0; k < n; k++)
        {
            var o = new Orb { state = OrbState.Orbit, slot = k * Mathf.PI * 2f / n };
            o.fireFrame = k < single ? fireStart + k * interval : burstFrame;
            o.pos = o.prevPos = RingPoint(o.slot);
            orbs.Add(o);
        }
        lastFire = burstFrame;
        fired = 0;

        // 몸에서 봇을 빌려 음표 모양으로 (흘러나와 고리 자리로)
        MaestroNotePattern.BuildShape(MaestroNoteKind.Eighth, orbitData.visualScale, Mathf.Max(swarm.spacing, 0.025f), shape);
        int spare = maestro.SpareBots(0.5f);
        int per = Mathf.Min(shape.Count, Mathf.Max(8, spare / n));
        foreach (var o in orbs)
        {
            swarm.Collect(RSNanoSwarm.G_Blob, maestro.BodyCenter, per, tmp);
            for (int k = 0; k < tmp.Count; k++)
            {
                int i = tmp[k];
                swarm.Fire(i, o.pos + shape[k], 9999f, Vector3.zero, 9999f, RSNanoSwarm.G_Blob, clump: 1);
                swarm.SetGlow(i, orbitData.glow);
                swarm.SetTint(i, k % 3 == 0 ? MaestroFx.Lavender : MaestroFx.MoonWhite);
                o.bots.Add(i);
                o.offsets.Add(shape[k]);
            }
            if (fx != null) { o.halo = fx.Acquire(); o.flash = 0.5f; }
        }

        frame = 0;
        running = true;
        if (maestro.Look != null) maestro.Look.Play(MaestroGesture.Spread, (lastFire + 20) * CombatClock.TICK);
        if (fx != null) fx.Ring(maestro.BodyCenter, ringRadius * 0.3f, ringRadius * 1.2f, 0.8f, MaestroFx.Lavender);
        Debug.Log($"[Maestro] 원형 음탄 — 음표 {n}개 (마지막 {n - single}개 한꺼번에), 간격 {row.interval}초, 회전 {(spinDir > 0 ? "반시계" : "시계")} ({maestro.SelectedDifficulty})");
    }

    protected override async UniTask ExecuteActionAsync(CancellationToken token)
    {
        if (!running) return;
        await UniTask.WaitUntil(() => !running, cancellationToken: token);
    }

    protected override void CleanupEffects()
    {
        running = false;
        Clear();
        // 배경에서 제자리로 — 악장 전환 중이면 무적은 전환이 맡는다
        if (maestro != null)
        {
            maestro.ResetBodyPose();
            if (maestro.Look != null) maestro.Look.Stop();
            if (!maestro.InTransition && OwnerBoss.Snapshot != null) OwnerBoss.Snapshot.isInvincible = false;
        }
    }

    // ───────────────────────── 전투 틱 ─────────────────────────

    private void OnEnable() => Subscribe();
    private void Start() => Subscribe();

    private void OnDisable()
    {
        var clock = CombatClock.Existing;
        if (clock != null && subscribed) clock.OnCombatTick -= CombatTick;
        subscribed = false;
    }

    private void Subscribe()
    {
        if (subscribed || !Application.isPlaying) return;
        CombatClock.Instance.OnCombatTick += CombatTick;
        subscribed = true;
    }

    private void CombatTick()
    {
        if (!running || maestro == null || Swarm == null) return;
        frame++;
        // 고리 회전 — 모이는 동안은 천천히, 그 뒤엔 난이도 속도
        float spin = row.spinDegPerSec * Mathf.Deg2Rad * (frame < gatherEnd ? 0.4f : 1f);
        ringAngle += spinDir * spin * CombatClock.TICK;

        bool anyLeft = false;
        bool burstSound = false;
        foreach (var o in orbs)
        {
            o.prevPos = o.pos;
            if (o.state == OrbState.Orbit)
            {
                o.pos = RingPoint(o.slot);
                if (frame >= o.fireFrame)
                {
                    Launch(o, playTone: o.fireFrame != lastFire || !burstSound);
                    if (o.fireFrame == lastFire) burstSound = true;
                }
            }
            else if (o.state == OrbState.Flying) Fly(o);
            if (o.state != OrbState.Done) anyLeft = true;
        }
        if (!anyLeft) running = false;
    }

    // 고리 — 배경(뒤판)으로 물러난 보스 둘레
    private Vector3 RingPoint(float slot)
    {
        float a = ringAngle + slot;
        Vector3 c = maestro.BodyCenter;
        return new Vector3(c.x + Mathf.Cos(a) * ringRadius, c.y + Mathf.Sin(a) * ringRadius * orbitData.ringFlatten, Swarm.BackZ);
    }

    private Vector3 AimPoint()
    {
        Transform player = maestro.PlayerTransform;
        float aim = noteData != null ? noteData.aimHeight : 0.6f;
        return player != null ? player.position + Vector3.up * aim : maestro.BodyCenter + Vector3.right * 4f;
    }

    // ③④ 발사 — 플레이어 쪽으로 출발 (배경에서 앞으로 튀어나오며 점점 빨라짐). 음이 울리고 번쩍
    private void Launch(Orb o, bool playTone)
    {
        Vector3 dir = AimPoint() - o.pos;
        dir.z = 0f;
        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
        o.vel = dir * row.speed;
        o.flightFrames = 0;
        o.state = OrbState.Flying;
        o.flash = 1f;
        foreach (int i in o.bots) Swarm.SetFlash(i, 1f);
        if (fx != null) fx.Ring(o.pos, orbitData.hitRadius, orbitData.hitRadius * 3f, 0.3f, MaestroFx.Lavender);
        if (playTone) { PlayTone(fired); if (maestro.Look != null) maestro.Look.Flick(); }
        fired++;
    }

    // 비행: ① 튀어나옴(배경 → 앞판, 느리게 출발, 판정 없음) ② 유도(플레이어 쪽으로 꺾음, 최대 각속도) ③ 직선
    private void Fly(Orb o)
    {
        o.flightFrames++;
        int emerge = Mathf.Max(1, Frames(orbitData.emergeSeconds));
        float u = Mathf.Clamp01(o.flightFrames / (float)emerge);
        float speed = row.speed * Mathf.Lerp(0.25f, 1f, u * u);

        if (o.flightFrames <= emerge + Frames(orbitData.homingSeconds))
        {
            Vector3 want = AimPoint() - o.pos;
            want.z = 0f;
            if (want.sqrMagnitude > 1e-4f)
            {
                float cur = Mathf.Atan2(o.vel.y, o.vel.x) * Mathf.Rad2Deg;
                float tgt = Mathf.Atan2(want.y, want.x) * Mathf.Rad2Deg;
                float turn = orbitData.Homing(maestro.SelectedDifficulty, row) * CombatClock.TICK * (u < 1f ? 2f : 1f); // 튀어나오는 동안은 더 크게 꺾어 방향을 잡는다
                float a = Mathf.MoveTowardsAngle(cur, tgt, turn) * Mathf.Deg2Rad;
                o.vel = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            }
        }
        Vector3 dir = o.vel.sqrMagnitude > 1e-6f ? o.vel.normalized : Vector3.right;
        o.vel = dir * speed;
        Vector3 next = o.pos + o.vel * CombatClock.TICK;
        next.z = Mathf.Lerp(Swarm.BackZ, Swarm.FrontZ, Mathf.SmoothStep(0f, 1f, u)); // 배경에서 앞판으로
        o.pos = next;
        if (u < 1f) return; // 튀어나오는 중엔 판정·벽·바닥 없음

        int count = Physics2D.OverlapCircle(o.pos, orbitData.hitRadius, overlapFilter, overlap);
        for (int k = 0; k < count; k++)
        {
            if (!overlap[k].CompareTag("Player")) continue;
            if (ApplyDamageToPlayer(overlap[k].gameObject)) { Collapse(o); return; }
            break;
        }
        bool floor = o.pos.y - orbitData.hitRadius <= maestro.GroundY;
        bool wall = CameraBounds.TryGetVisibleX(Camera.main, Swarm.FrontZ, maestro.GroundY, out float minX, out float maxX)
            ? o.pos.x <= minX || o.pos.x >= maxX
            : Mathf.Abs(o.pos.x - maestro.BodyCenter.x) > 30f;
        bool tooHigh = o.pos.y > maestro.GroundY + 40f;
        if (floor || wall || tooHigh || o.flightFrames >= Frames(orbitData.maxFlightSeconds)) Collapse(o);
    }

    private void Collapse(Orb o)
    {
        if (fx != null) fx.Ring(o.pos, orbitData.hitRadius * 0.5f, orbitData.hitRadius * 3f, 0.4f, MaestroFx.MoonWhite);
        Drop(o);
        o.state = OrbState.Done;
    }

    private void Drop(Orb o)
    {
        var swarm = Swarm;
        if (swarm != null && maestro != null)
        {
            int due = Frames(orbitData != null ? orbitData.reabsorbDelay : 0.8f);
            Vector3 v = o.vel * 0.3f;
            foreach (int i in o.bots)
                if (i < swarm.Count && swarm.Bots[i].mode == BotMode.Free)
                    maestro.ReleaseToSand(i, v + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.5f, 2.5f), 0f), due);
        }
        o.bots.Clear();
        o.offsets.Clear();
        if (fx != null && o.halo != null) { fx.Release(o.halo); o.halo = null; }
    }

    private void Clear()
    {
        foreach (var o in orbs) if (o.state != OrbState.Done) Drop(o);
        orbs.Clear();
    }

    private void PlayTone(int order)
    {
        if (audioSource == null || noteData == null || orbitData == null) return;
        AudioClip clip = null;
        if (noteData.noteClips != null && noteData.noteClips.Length > 0) clip = noteData.noteClips[order % noteData.noteClips.Length];
        if (clip == null && noteData.melody != null && noteData.melody.Length > 0)
            clip = MaestroNotePattern.Synth(noteData.melody[order % noteData.melody.Length] + 12); // 한 옥타브 위 — 경쾌하게
        if (clip != null) audioSource.PlayOneShot(clip, orbitData.volume);
    }

    // ───────────────────────── 그림 ─────────────────────────

    private void LateUpdate()
    {
        var swarm = Swarm;
        if (swarm == null || orbs.Count == 0 || !running) return;
        float alpha = CombatClock.InterpolationAlpha;
        var arr = swarm.Bots;
        foreach (var o in orbs)
        {
            if (o.state == OrbState.Done) continue;
            Vector3 p = Vector3.Lerp(o.prevPos, o.pos, alpha);
            // 모이는 동안은 몸에서 흘러오게(자리만), 다 모이면 바로 붙임
            bool snap = o.state == OrbState.Flying || frame >= gatherEnd;
            for (int k = 0; k < o.bots.Count; k++)
            {
                int i = o.bots[k];
                if (i >= arr.Length || arr[i].mode != BotMode.Free) continue;
                Vector3 target = p + o.offsets[k];
                arr[i].hold = target;
                arr[i].wait = 9999f;
                if (snap) { arr[i].pos = target; arr[i].vel = Vector3.zero; }
            }
            if (o.halo != null && fx != null)
            {
                o.flash = Mathf.MoveTowards(o.flash, 0f, Time.deltaTime * 2.5f);
                float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 7f + o.slot * 3f);
                Color c = MaestroFx.Lavender;
                c.a = (0.3f + 0.6f * o.flash) * pulse;
                fx.Place(o.halo, p + Vector3.up * orbitData.hitRadius * 0.6f, Vector2.one * orbitData.hitRadius * (5f + 3f * o.flash), c);
            }
        }
    }
}
