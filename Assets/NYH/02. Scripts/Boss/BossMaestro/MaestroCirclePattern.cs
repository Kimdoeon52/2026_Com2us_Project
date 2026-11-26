using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using RealSteel.NanoBoss;
using UnityEngine;

/// <summary>
/// 마에스트로 「원 음표탄」 (2026-11-26 사용자 — 패턴 제조기 모션 '원그리기' + 메모 기반).
///   ① 모션 재생 — 지휘봉 끝으로 크게 원을 그린다. 모션의 점(생성) 이벤트 시간마다 그 순간 지휘봉 끝 자리에 작은 음표가 생긴다
///      → 원을 따라 음표가 차례로 맺힌다 (몸의 봇이 흘러나와 음표 모양으로)
///   ② 모션의 선(탄 궤적) 이벤트 시간에 생긴 순서대로 하나씩 빠르게(다다다닥) 플레이어 쪽으로 발사
///   ③ 날아가는 처음 잠깐만 약하게 유도(최대 각속도 제한) → 그 뒤 직선
///   ④ 플레이어·바닥·벽(화면 끝 — 바깥으로 나갈 때만)에 닿으면 고리로 터져 모래 → 몸으로 재흡수
/// 로직(생성·비행·판정)은 전투 틱(CombatClock), 그림은 LateUpdate. 수치는 MaestroCircleData(TODO 임시값).
/// </summary>
public class MaestroCirclePattern : BossPatternGimmickBase, IMaestroMovementGate
{
    [SerializeField] private MaestroCircleData circleData;
    [Tooltip("이 악장부터 나온다 (기획서에 아직 없음 — 시험용 1)")]
    [Min(1)] [SerializeField] private int minMovement = 1;

    public int MinMovement => minMovement;

    private enum NoteState { Waiting, Hanging, Flying, Done }

    private class Note
    {
        public NoteState state;
        public int spawnFrame, fireFrame, flightFrames;
        public MaestroMotion.Anchor anchor;
        public Vector2 offset;
        public Vector3 pos, prevPos, vel;
        public readonly List<int> bots = new List<int>(128);
        public readonly List<Vector3> offsets = new List<Vector3>(128);
        public SpriteRenderer halo;
        public float flash;
    }

    private static readonly List<Collider2D> overlap = new List<Collider2D>(8);
    private static readonly ContactFilter2D overlapFilter = new ContactFilter2D { useTriggers = true };

    private MaestroBossController maestro;
    private MaestroCircleData.DifficultyRow row;
    private MaestroFx fx;
    private AudioSource audioSource;
    private readonly List<Note> notes = new List<Note>(24);
    private readonly List<int> tmp = new List<int>(256);
    private readonly List<Vector3> shape = new List<Vector3>(256);
    private bool running, subscribed;
    private int frame, perNote;
    private float scale = 0.5f, hitR = 0.1f, playerH = 1.5f, motionLength;

    protected override bool UseExternalTellPhase => true;
    private RSNanoSwarm Swarm => maestro != null ? maestro.Swarm : null;
    private static int Frames(float s) => Mathf.Max(0, Mathf.RoundToInt(s / CombatClock.TICK));

    protected override void OnAttached()
    {
        maestro = OwnerBoss as MaestroBossController;
        if (maestro == null) Debug.LogError($"[MaestroCirclePattern] {name}: 마에스트로 밑에 있어야 함");
        if (circleData == null) Debug.LogError($"[MaestroCirclePattern] {name}: 원 음표탄 수치(MaestroCircleData)가 비어 있음");
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    protected override void OnPreExecute()
    {
        Clear(drop: true);
        var swarm = Swarm;
        if (maestro == null || circleData == null || swarm == null) return;
        row = circleData.Row(maestro.SelectedDifficulty);
        if (row == null) return;
        fx = MaestroFx.Get(maestro);
        var look = maestro.Look;
        var motion = circleData.motion;
        playerH = look != null && look.Ready ? look.PlayerHeight : 1.5f;
        scale = playerH * circleData.sizePerPlayer;
        hitR = Mathf.Max(0.05f, circleData.hitRadius * scale); // 크기 배율에 비례 (보이는 곳 = 맞는 곳)

        // 생성 점 (모션의 점 이벤트 중 역할 '생성') — 시간순. 난이도 비율만큼 고르게 고른다
        var points = new List<MaestroMotion.MotionEvent>();
        float fireTime = circleData.fallbackFireTime;
        bool fireFound = false;
        if (motion != null && motion.events != null)
        {
            foreach (var e in motion.events)
            {
                if (e == null) continue;
                if (e.type == MaestroMotion.EventType.Point && e.pointRole == MaestroMotion.PointRole.Spawn) points.Add(e);
                else if (e.type == MaestroMotion.EventType.Line && e.lineRole == MaestroMotion.LineRole.Projectile && (!fireFound || e.time < fireTime)) { fireTime = e.time; fireFound = true; }
            }
            points.Sort((a, b) => a.time.CompareTo(b.time));
        }
        if (points.Count == 0)
        {
            // 모션이 없으면 1초 동안 지휘봉 끝에서 12개
            for (int k = 0; k < 12; k++) points.Add(new MaestroMotion.MotionEvent { time = 0.2f + k * 0.07f, anchor = MaestroMotion.Anchor.BatonTip });
        }
        int want = Mathf.Max(1, Mathf.RoundToInt(points.Count * row.pointRatio));
        int fireFrame = Frames(fireTime);
        int interval = Mathf.Max(1, Frames(row.fireInterval));
        for (int k = 0; k < want; k++)
        {
            var e = points[Mathf.Min(points.Count - 1, Mathf.FloorToInt(k * points.Count / (float)want))];
            var n = new Note
            {
                state = NoteState.Waiting,
                spawnFrame = Frames(e.time),
                anchor = e.anchor,
                offset = e.offset,
                fireFrame = fireFrame + k * interval,
            };
            n.fireFrame = Mathf.Max(n.fireFrame, n.spawnFrame + 6); // 생기자마자 쏘지는 않게
            notes.Add(n);
        }

        // 음표 하나에 쓸 봇 — 몸을 너무 깎지 않게 나눠 쓴다
        MaestroNotePattern.BuildShape(MaestroNoteKind.Eighth, scale, Mathf.Max(swarm.spacing, 0.02f), shape);
        int spare = Mathf.Max(0, maestro.SpareBots(0.5f));
        perNote = Mathf.Clamp(spare / Mathf.Max(1, notes.Count), 6, Mathf.Max(6, shape.Count));

        float length = motion != null ? motion.length : 2f;
        if (look != null)
        {
            if (motion != null)
            {
                // 모션 시간은 이 패턴이 전투 틱으로 직접 맞춘다 (11-26 사용자: 원의 반쪽에만 음표가 생김 —
                // 발사 끝까지의 길이로 늘려 재생해서 모션이 느려지고, 점 시간에 팔이 원을 반밖에 못 돌았다)
                look.PlayMotion(motion, motion.length);
                look.MotionTimeScale = 0f;
                look.SetMotionTime(0f);
                motionLength = motion.length;
            }
            else look.Play(MaestroGesture.Swing, length);
        }
        frame = 0;
        running = true;
        Debug.Log($"[Maestro] 원 음표탄 — {notes.Count}개, 발사 {fireTime:0.00}초부터 {row.fireInterval:0.000}초 간격, 속도 {row.speed}, 유도 {row.turnDegPerSec}°/s ({maestro.SelectedDifficulty}{(motion == null ? ", 모션 없음" : "")})");
    }

    protected override async UniTask ExecuteActionAsync(CancellationToken token)
    {
        if (!running) return;
        await UniTask.WaitUntil(() => !running, cancellationToken: token);
    }

    protected override void CleanupEffects()
    {
        running = false;
        Clear(drop: true);
        if (maestro != null && maestro.Look != null) { maestro.Look.MotionTimeScale = 1f; maestro.Look.Stop(); }
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
        // 모션 = 전투 틱 시간 그대로 (끝나면 마지막 자세로 버팀 — 길이에 딱 닿으면 몸짓이 끝나므로 살짝 앞)
        if (motionLength > 0f && maestro.Look != null) maestro.Look.SetMotionTime(Mathf.Min(frame * CombatClock.TICK, motionLength - 0.001f));
        bool anyLeft = false;
        int order = 0;
        foreach (var n in notes)
        {
            n.prevPos = n.pos;
            switch (n.state)
            {
                case NoteState.Waiting:
                    if (frame >= n.spawnFrame) Spawn(n, order);
                    break;
                case NoteState.Hanging:
                    if (frame >= n.fireFrame) Launch(n, order);
                    break;
                case NoteState.Flying:
                    Fly(n);
                    break;
            }
            if (n.state != NoteState.Done) anyLeft = true;
            order++;
        }
        if (!anyLeft) running = false;
    }

    // ① 지금 지휘봉 끝(모션 점의 기준) 자리에 음표가 맺힌다 — 몸의 봇이 흘러와 모양을 잡는다
    private void Spawn(Note n, int order)
    {
        var swarm = Swarm;
        var look = maestro.Look;
        Vector3 p = look != null && look.Ready
            ? look.MotionAnchorWorld(n.anchor) + look.MotionToWorldDelta(n.offset)
            : maestro.HandPoint(1);
        p.z = swarm.FrontZ;
        n.pos = n.prevPos = p;
        swarm.Collect(RSNanoSwarm.G_Blob, maestro.BodyCenter, Mathf.Min(perNote, shape.Count), tmp);
        n.bots.Clear();
        n.offsets.Clear();
        int step = Mathf.Max(1, shape.Count / Mathf.Max(1, tmp.Count));
        for (int k = 0; k < tmp.Count; k++)
        {
            int i = tmp[k];
            var off = shape[Mathf.Min(shape.Count - 1, k * step)];
            swarm.Fire(i, p + off, 9999f, Vector3.zero, 9999f, RSNanoSwarm.G_Blob, clump: 1);
            swarm.SetGlow(i, circleData.glow);
            swarm.SetTint(i, k % 3 == 0 ? MaestroFx.Lavender : MaestroFx.MoonWhite);
            n.bots.Add(i);
            n.offsets.Add(off);
        }
        n.state = NoteState.Hanging;
        if (fx != null) { n.halo = fx.Acquire(); n.flash = 0.8f; }
    }

    // ② 하나씩 — 이 순간 플레이어 가슴 쪽으로
    private void Launch(Note n, int order)
    {
        n.vel = AimDir(n.pos) * row.speed;
        n.state = NoteState.Flying;
        n.flash = 1f;
        foreach (int i in n.bots) Swarm.SetFlash(i, 1f);
        PlayTone(order);
        if (maestro.Look != null && (order & 1) == 0) maestro.Look.Flick();
    }

    private Vector3 AimDir(Vector3 from)
    {
        Transform player = maestro.PlayerTransform;
        Vector3 d = player != null ? new Vector3(player.position.x, player.position.y + playerH * circleData.aimHeightPerPlayer, from.z) - from : Vector3.down;
        d.z = 0f;
        return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.down;
    }

    // ③ 처음 homingSeconds 동안만 약하게 플레이어 쪽으로 돌고 그 뒤 직선 · ④ 닿으면 터짐
    private void Fly(Note n)
    {
        n.flightFrames++;
        if (row.turnDegPerSec > 0f && n.flightFrames * CombatClock.TICK <= circleData.homingSeconds)
        {
            Vector3 want = AimDir(n.pos);
            float speed = n.vel.magnitude;
            Vector3 cur = n.vel / Mathf.Max(1e-4f, speed);
            float maxRad = row.turnDegPerSec * Mathf.Deg2Rad * CombatClock.TICK;
            n.vel = Vector3.RotateTowards(cur, want, maxRad, 0f) * speed;
        }
        n.pos += n.vel * CombatClock.TICK;

        int count = Physics2D.OverlapCircle(n.pos, hitR, overlapFilter, overlap);
        for (int k = 0; k < count; k++)
        {
            if (!overlap[k].CompareTag("Player")) continue;
            if (ApplyDamageToPlayer(overlap[k].gameObject)) { Collapse(n); return; }
            break;
        }
        bool floor = n.pos.y - hitR <= maestro.GroundY;
        bool wall = false;
        // 화면 끝은 바깥으로 나가는 쪽일 때만 (밖에서 생긴 음표가 들어오다 부서지지 않게)
        if (CameraBounds.TryGetVisibleX(Camera.main, Swarm.FrontZ, n.pos.y, out float minX, out float maxX))
            wall = (n.pos.x - hitR <= minX && n.vel.x < 0f) || (n.pos.x + hitR >= maxX && n.vel.x > 0f);
        if (floor || wall || n.flightFrames >= Frames(circleData.maxFlightSeconds)) Collapse(n);
    }

    private void Collapse(Note n)
    {
        if (fx != null) fx.Ring(n.pos, hitR * 0.5f, hitR * 3f, 0.35f, MaestroFx.MoonWhite);
        DropBots(n);
        n.state = NoteState.Done;
    }

    private void DropBots(Note n)
    {
        var swarm = Swarm;
        if (swarm != null && maestro != null)
        {
            Vector3 v = n.vel * 0.3f;
            int due = Frames(circleData != null ? circleData.reabsorbDelay : 1f);
            foreach (int i in n.bots)
            {
                if (i >= swarm.Count || swarm.Bots[i].mode != BotMode.Free) continue;
                maestro.ReleaseToSand(i, v + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.5f, 2.5f), 0f), due);
            }
        }
        n.bots.Clear();
        n.offsets.Clear();
        if (fx != null && n.halo != null) { fx.Release(n.halo); n.halo = null; }
    }

    private void Clear(bool drop)
    {
        foreach (var n in notes)
            if (drop && n.state != NoteState.Done) DropBots(n);
        notes.Clear();
    }

    private void PlayTone(int order)
    {
        var nd = circleData.noteData;
        if (audioSource == null || nd == null) return;
        AudioClip clip = null;
        if (nd.noteClips != null && nd.noteClips.Length > 0) clip = nd.noteClips[order % nd.noteClips.Length];
        if (clip == null && nd.melody != null && nd.melody.Length > 0) clip = MaestroNotePattern.Synth(nd.melody[order % nd.melody.Length] + 12); // 한 옥타브 위 — 작은 음표
        if (clip != null) audioSource.PlayOneShot(clip, nd.volume * 0.6f);
    }

    // ───────────────────────── 그림 ─────────────────────────

    private void LateUpdate()
    {
        var swarm = Swarm;
        if (swarm == null || notes.Count == 0) return;
        float a = CombatClock.InterpolationAlpha;
        var bots = swarm.Bots;
        float t = Time.time;
        int idx = 0;
        foreach (var n in notes)
        {
            idx++;
            if (n.state == NoteState.Waiting || n.state == NoteState.Done) continue;
            Vector3 p = Vector3.Lerp(n.prevPos, n.pos, a);
            bool flying = n.state == NoteState.Flying;
            // 맺혀 있는 동안 두근두근 (판정과 무관)
            float pulse = flying ? 1f : 1f + circleData.bob * Mathf.Sin(t * 9f + idx * 0.7f);
            if (n.halo != null && fx != null)
            {
                n.flash = Mathf.MoveTowards(n.flash, 0f, Time.deltaTime * 3f);
                Color c = MaestroFx.Lavender;
                c.a = 0.35f + 0.5f * n.flash;
                fx.Place(n.halo, p, Vector2.one * hitR * (5f + 3f * n.flash) * pulse, c);
            }
            for (int k = 0; k < n.bots.Count; k++)
            {
                int i = n.bots[k];
                if (i >= bots.Length || bots[i].mode != BotMode.Free) continue;
                Vector3 target = p + n.offsets[k] * pulse;
                bots[i].hold = target;
                bots[i].wait = 9999f;
                if (flying) { bots[i].pos = target; bots[i].vel = Vector3.zero; }
            }
        }
    }
}
