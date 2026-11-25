using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using RealSteel.NanoBoss;
using UnityEngine;

/// <summary>
/// 마에스트로 1악장 「음표 공격」 (보스3_월광_기획 1.4, 2026-11-19 2단계). 탄막형, 타격 부위 왼팔(패턴 데이터).
///   ① 흐름  지휘봉을 크게 한 번 휘두른다 — 궤적(호)을 따라 몸의 봇이 흘러나온다
///   ② 형태  휘두르는 동안 궤적 위 공중에 음표 N개가 차례로 생긴다 (N·구성·간격 = 난이도별, MaestroNoteData)
///   ③ 예고  생긴 순서대로 하나씩 그 음표의 음이 울리고 같은 프레임에 반짝인다
///   ④ 공격  음이 울린 순간의 플레이어 위치를 향해 직선 비행 (모양 = 속도)
///   ⑤ 붕괴  플레이어·벽·바닥에 닿으면 모래로 흩어졌다가 몸으로 재흡수
/// 휘두르기 1번이 끝나면(음표가 전부 날아가 흩어지면) 패턴 끝.
///
/// 로직(진행·이동·판정)은 전투 틱(CombatClock), 그림은 LateUpdate에서 군집 봇을 논리 위치로 옮긴다
/// (군집 엔진은 Time.deltaTime으로 돌아서 탄을 엔진에 맡기면 판정과 그림이 어긋난다).
/// KKH 전조 단계(Time.deltaTime 기반)는 쓰지 않는다 — 예고가 패턴 안(③)에 있다.
/// 아직 없는 것: 지휘봉 그림(거대 지휘봉 단계에서 줄기 가이드로), 자체 녹음 음원(지금은 임시 합성음).
/// </summary>
public class MaestroNotePattern : BossPatternGimmickBase
{
    [Tooltip("음표 수치 (속도·판정·난이도별 수/간격·휘두르기·소리)")]
    [SerializeField] private MaestroNoteData noteData;

    private enum NoteState { Waiting, Forming, Sounded, Flying, Done }

    private class Note
    {
        public MaestroNoteKind kind;
        public NoteState state;
        public int spawnFrame, soundFrame, launchFrame, flightFrames;
        public Vector3 pos, prevPos, vel;
        public readonly List<int> bots = new List<int>(256);
        public readonly List<Vector3> offsets = new List<Vector3>(256);
        public SpriteRenderer halo; // 시트: 발광하는 음표
        public float flash;
    }

    private MaestroFx fx;

    private static readonly List<Collider2D> overlap = new List<Collider2D>(8);
    private static readonly ContactFilter2D overlapFilter = new ContactFilter2D { useTriggers = true };

    private MaestroBossController maestro;
    private readonly List<Note> notes = new List<Note>(8);
    private readonly List<int> tmp = new List<int>(512);
    private readonly List<Vector3> shape = new List<Vector3>(512);
    private static readonly Dictionary<int, AudioClip> synth = new Dictionary<int, AudioClip>(); // 패턴끼리 공용 (원형 음탄도 씀)
    private AudioSource audioSource;
    private bool running, subscribed;
    private int frame, side;
    private float swingCenterX;
    private float noteScale = 1f;   // 음표 크기 배율 (플레이어 키 기준 — 그림·판정 같이)

    protected override bool UseExternalTellPhase => true;

    private RSNanoSwarm Swarm => maestro != null ? maestro.Swarm : null;
    private static int Frames(float seconds) => Mathf.Max(0, Mathf.RoundToInt(seconds / CombatClock.TICK));

    // ───────────────────────── KKH 패턴 골격 ─────────────────────────

    protected override void OnAttached()
    {
        maestro = OwnerBoss as MaestroBossController;
        if (maestro == null) Debug.LogError($"[MaestroNotePattern] {name}: 마에스트로(MaestroBossController) 밑에 있어야 함");
        if (noteData == null) Debug.LogError($"[MaestroNotePattern] {name}: 음표 수치(MaestroNoteData)가 비어 있음");
        if (patternData == null) Debug.LogWarning($"[MaestroNotePattern] {name}: 패턴 데이터가 비어 있음 — 데미지·부위가 KKH 기본값");
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    protected override void OnPreExecute()
    {
        ClearNotes(drop: true);
        if (maestro == null || noteData == null || Swarm == null) return;
        var row = noteData.Row(maestro.SelectedDifficulty);
        if (row == null || row.notes == null || row.notes.Length == 0) return;

        Transform player = maestro.PlayerTransform;
        side = player != null && player.position.x < maestro.BodyCenter.x ? -1 : 1;
        swingCenterX = maestro.BodyCenter.x;
        // 11-25 사용자: 음표가 너무 커서 화면 밖으로 → 플레이어 키에 맞춰 줄인다 (늘리지는 않음)
        float ph = maestro.Look != null && maestro.Look.Ready ? maestro.Look.PlayerHeight : 1.5f;
        noteScale = noteData.sizePerPlayer > 0f ? Mathf.Min(1f, ph * noteData.sizePerPlayer) : 1f;

        int n = row.notes.Length;
        int swing = Mathf.Max(1, Frames(noteData.swingSeconds));
        int firstSound = swing + Frames(noteData.firstSoundDelay);
        int interval = Mathf.Max(1, Frames(row.interval));
        int windup = Frames(noteData.launchDelay);
        for (int k = 0; k < n; k++)
        {
            var note = new Note
            {
                kind = row.notes[k],
                state = NoteState.Waiting,
                spawnFrame = Mathf.FloorToInt(swing * (k + 0.5f) / n),
                soundFrame = firstSound + k * interval,
            };
            note.launchFrame = note.soundFrame + windup;
            notes.Add(note);
        }
        frame = 0;
        running = true;
        if (maestro.Look != null) maestro.Look.Play(MaestroGesture.Swing, noteData.swingSeconds); // ① 크게 한 번 휘두르기
        Debug.Log($"[Maestro] 음표 공격 — {n}개, 간격 {row.interval}초 ({maestro.SelectedDifficulty})");
    }

    protected override async UniTask ExecuteActionAsync(CancellationToken token)
    {
        if (!running) return;
        await UniTask.WaitUntil(() => !running, cancellationToken: token);
    }

    // 끝났든 끊겼든(악장 전환·전투 종료) 남은 음표는 모래로 — 재흡수는 계속 돈다 (전환 중이면 전환이 맡는다)
    protected override void CleanupEffects()
    {
        running = false;
        ClearNotes(drop: true);
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

        bool anyLeft = false;
        foreach (var note in notes)
        {
            note.prevPos = note.pos;
            switch (note.state)
            {
                case NoteState.Waiting:
                    if (frame >= note.spawnFrame) Form(note);
                    break;
                case NoteState.Forming:
                    if (frame >= note.soundFrame) Sound(note);
                    break;
                case NoteState.Sounded:
                    if (frame >= note.launchFrame) note.state = NoteState.Flying;
                    break;
                case NoteState.Flying:
                    Fly(note);
                    break;
            }
            if (note.state != NoteState.Done) anyLeft = true;
        }
        if (!anyLeft) running = false;
    }

    // ② 음표 하나가 궤적 위 자리에 생긴다 — 몸에서 가까운 봇을 데려와 실루엣 자리로 흘려보낸다(그림은 LateUpdate)
    private void Form(Note note)
    {
        note.pos = note.prevPos = ArcPoint(note.spawnFrame);
        var spec = noteData.Spec(note.kind);
        BuildShape(note.kind, spec.visualScale * noteScale, Mathf.Max(Swarm.spacing, 0.025f), shape); // 외형 크기를 줄여 칸이 작아져도 음표 하나에 봇이 너무 많이 들지 않게
        Swarm.Collect(RSNanoSwarm.G_Blob, note.pos, shape.Count, tmp);
        note.bots.Clear();
        note.offsets.Clear();
        for (int k = 0; k < tmp.Count; k++)
        {
            int i = tmp[k];
            // 대기(wait)를 길게 걸어 엔진이 스스로 날리지 않게 — 자리(hold)는 매 프레임 이 스크립트가 정한다
            Swarm.Fire(i, note.pos + shape[k], 9999f, Vector3.zero, 9999f, RSNanoSwarm.G_Blob, clump: 1);
            Swarm.SetGlow(i, noteData.glow);
            Swarm.SetTint(i, MaestroFx.MoonWhite); // 몸에서 빌린 봇(어두운 색)을 달빛 흰색으로 — 시트의 빛나는 음표
            note.bots.Add(i);
            note.offsets.Add(shape[k]);
        }
        note.state = NoteState.Forming;
        if (fx == null) fx = MaestroFx.Get(maestro);
        if (fx != null) { note.halo = fx.Acquire(); note.flash = 0.6f; }
    }

    // ③ 음이 울리는 프레임에 반짝이고, 그 순간 플레이어 위치로 조준
    private void Sound(Note note)
    {
        int order = notes.IndexOf(note);
        PlayTone(order);
        foreach (int i in note.bots) Swarm.SetFlash(i, 1f);
        note.flash = 1f;
        if (fx != null) fx.Ring(note.pos, HitR(note), HitR(note) * 2.5f, 0.35f, MaestroFx.Lavender); // 음이 울리는 순간 퍼지는 고리
        if (maestro.Look != null) maestro.Look.Flick(); // 음이 울릴 때마다 지휘봉을 튕긴다

        Vector3 aim = note.pos + Vector3.right * side;
        Transform player = maestro.PlayerTransform;
        if (player != null) aim = player.position + Vector3.up * noteData.aimHeight;
        Vector3 dir = aim - note.pos;
        dir.z = 0f;
        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.right * side;
        note.vel = dir * noteData.Spec(note.kind).speed;
        note.state = noteData.launchDelay > 0f ? NoteState.Sounded : NoteState.Flying;
    }

    // ④ 직선 비행 — 플레이어(회피 무적이면 통과)·벽·바닥에 닿으면 ⑤
    private void Fly(Note note)
    {
        note.pos += note.vel * CombatClock.TICK;
        note.flightFrames++;
        var spec = noteData.Spec(note.kind);

        float hitR = spec.hitRadius * noteScale;
        int count = Physics2D.OverlapCircle(note.pos, hitR, overlapFilter, overlap);
        for (int k = 0; k < count; k++)
        {
            if (!overlap[k].CompareTag("Player")) continue;
            if (ApplyDamageToPlayer(overlap[k].gameObject)) { Collapse(note); return; }
            break;
        }

        bool floor = note.pos.y - hitR <= maestro.GroundY;
        bool wall = false;
        // 화면 끝은 바깥으로 나가는 쪽일 때만 벽 (11-25 사용자: 밖에서 날아오던 음표가 화면 끝에 부딪혀 밖에서 사라짐)
        if (CameraBounds.TryGetVisibleX(Camera.main, Swarm.FrontZ, note.pos.y, out float minX, out float maxX))
            wall = (note.pos.x - hitR <= minX && note.vel.x < 0f) || (note.pos.x + hitR >= maxX && note.vel.x > 0f);
        else
            wall = Mathf.Abs(note.pos.x - swingCenterX) > 30f;
        if (floor || wall || note.flightFrames >= Frames(noteData.maxFlightSeconds)) Collapse(note);
    }

    // ⑤ 흩어져 모래가 됐다가 잠시 뒤 몸으로 재흡수
    private void Collapse(Note note)
    {
        // 시트 탄환 연출: 음표 → 고리로 터짐
        if (fx != null) fx.Ring(note.pos, HitR(note) * 0.5f, HitR(note) * 3f, 0.4f, MaestroFx.MoonWhite);
        DropBots(note);
        note.state = NoteState.Done;
    }

    // 재흡수는 보스 컨트롤러 공용 (전환 중엔 전환이 맡는다)
    private void DropBots(Note note)
    {
        var swarm = Swarm;
        if (swarm != null)
        {
            Vector3 v = note.vel * 0.3f;
            int due = Frames(noteData != null ? noteData.reabsorbDelay : 1f);
            foreach (int i in note.bots)
            {
                if (i >= swarm.Count || swarm.Bots[i].mode != BotMode.Free) continue;
                maestro.ReleaseToSand(i, v + new Vector3(Random.Range(-2f, 2f), Random.Range(0.5f, 3f), 0f), due);
            }
        }
        note.bots.Clear();
        note.offsets.Clear();
        if (fx != null && note.halo != null) { fx.Release(note.halo); note.halo = null; }
    }

    private void ClearNotes(bool drop)
    {
        foreach (var note in notes)
            if (drop && note.state != NoteState.Done) DropBots(note);
        notes.Clear();
    }

    // 휘두르기 궤적 — 몸 중심 위 호. 0 = 플레이어 쪽 수평, 90 = 바로 위
    private Vector3 ArcPoint(int atFrame)
    {
        int swing = Mathf.Max(1, Frames(noteData.swingSeconds));
        float t = Mathf.Clamp01(atFrame / (float)swing);
        float a = Mathf.Lerp(noteData.arcAngles.x, noteData.arcAngles.y, t) * Mathf.Deg2Rad;
        Vector3 c = maestro.BodyCenter + Vector3.up * noteData.arcLift;
        var p = new Vector3(c.x + Mathf.Cos(a) * noteData.arcRadius * side, c.y + Mathf.Sin(a) * noteData.arcRadius, Swarm.FrontZ);
        p.y = Mathf.Max(p.y, maestro.GroundY + noteData.minSpawnHeight);
        return ClampToScreen(p);
    }

    private float HitR(Note note) => noteData.Spec(note.kind).hitRadius * noteScale;

    // 생기는 자리를 화면 안으로 (11-25 사용자: 화면 밖에 생겨서 안 보이거나 밖에서 사라짐). 음표 크기 ≈ 키 1m × 배율
    private Vector3 ClampToScreen(Vector3 p)
    {
        var cam = Camera.main;
        if (cam == null) return p;
        float size = noteScale * 1f;              // 음표 실루엣 높이 ≈ 1 × 배율 (머리 아래 0.2 · 기둥 위 0.95)
        float m = size * noteData.screenMargin * 0.5f;
        if (TopY(cam, p.z, out float top)) p.y = Mathf.Min(p.y, top - size - m);
        p.y = Mathf.Max(p.y, maestro.GroundY + noteData.minSpawnHeight);
        if (CameraBounds.TryGetVisibleX(cam, p.z, p.y, out float minX, out float maxX) && maxX - minX > m * 2f)
            p.x = Mathf.Clamp(p.x, minX + m, maxX - m);
        return p;
    }

    // 화면 위쪽 끝이 평면 z에 닿는 높이
    private static bool TopY(Camera cam, float z, out float y)
    {
        y = 0f;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 1f, 0f));
        if (Mathf.Abs(ray.direction.z) < 1e-6f) return false;
        float t = (z - ray.origin.z) / ray.direction.z;
        if (t <= 0f) return false;
        y = ray.origin.y + ray.direction.y * t;
        return true;
    }

    // ───────────────────────── 그림 ─────────────────────────

    // 군집(실행 순서 200)이 시뮬레이션하기 전에 음표 봇의 자리를 논리 위치로 옮긴다.
    // 생기는 중(Forming)엔 자리만 정해 봇이 몸에서 흘러오게, 날기 시작하면 봇을 그 자리에 바로 붙인다(판정과 그림 일치)
    private void LateUpdate()
    {
        var swarm = Swarm;
        if (swarm == null || notes.Count == 0) return;
        float a = CombatClock.InterpolationAlpha;
        var bots = swarm.Bots;
        foreach (var note in notes)
        {
            if (note.state == NoteState.Waiting || note.state == NoteState.Done) continue;
            Vector3 p = Vector3.Lerp(note.prevPos, note.pos, a);
            bool snap = note.state == NoteState.Flying;
            if (note.halo != null)
            {
                // 발광 번짐 — 음이 울리면 크게 번쩍했다 가라앉고, 은은하게 맥동
                note.flash = Mathf.MoveTowards(note.flash, 0f, Time.deltaTime * 2.5f);
                float r = HitR(note);
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f + note.spawnFrame);
                Color c = MaestroFx.Lavender;
                c.a = (0.35f + 0.55f * note.flash) * pulse;
                fx.Place(note.halo, p + Vector3.up * r * 0.6f, Vector2.one * r * (4f + 3f * note.flash), c);
            }
            for (int k = 0; k < note.bots.Count; k++)
            {
                int i = note.bots[k];
                if (i >= bots.Length || bots[i].mode != BotMode.Free) continue;
                Vector3 target = p + note.offsets[k];
                bots[i].hold = target;
                bots[i].wait = 9999f;
                if (snap) { bots[i].pos = target; bots[i].vel = Vector3.zero; }
            }
        }
    }

    // 음표 실루엣 (머리 중심 = 원점, m). 단순한 음표만 — 기획서 1.4 구현 메모
    public static void BuildShape(MaestroNoteKind kind, float scale, float pitch, List<Vector3> into) // 원형 음탄도 씀
    {
        into.Clear();
        pitch = Mathf.Max(0.005f, pitch);
        float tilt = -20f * Mathf.Deg2Rad;
        switch (kind)
        {
            case MaestroNoteKind.Whole:
                // 속 빈 타원 (안쪽 구멍은 비스듬히)
                Fill(into, pitch, scale, -0.4f, 0.4f, -0.3f, 0.3f, (x, y) =>
                    Ellipse(x, y, 0.35f, 0.25f, 0f) <= 1f && Ellipse(x, y, 0.19f, 0.12f, 35f * Mathf.Deg2Rad) > 1f);
                break;
            case MaestroNoteKind.Quarter:
                // 꽉 찬 머리 + 기둥
                Fill(into, pitch, scale, -0.25f, 0.25f, -0.2f, 0.95f, (x, y) =>
                    Ellipse(x, y, 0.22f, 0.16f, tilt) <= 1f || (x >= 0.14f && x <= 0.19f && y >= 0f && y <= 0.9f));
                break;
            default:
                // 작은 머리 + 기둥 + 꼬리
                Fill(into, pitch, scale, -0.2f, 0.4f, -0.15f, 0.75f, (x, y) =>
                {
                    if (Ellipse(x, y, 0.16f, 0.12f, tilt) <= 1f) return true;
                    if (x >= 0.10f && x <= 0.14f && y >= 0f && y <= 0.72f) return true;
                    float dx = x - 0.14f;
                    if (dx < 0f || dx > 0.22f) return false;
                    float yc = 0.72f - 1.8f * dx + 2.2f * dx * dx;
                    return y <= yc && y >= yc - 0.08f;
                });
                break;
        }
    }

    private static void Fill(List<Vector3> into, float pitch, float scale, float x0, float x1, float y0, float y1, System.Func<float, float, bool> inside)
    {
        float step = pitch / Mathf.Max(0.2f, scale);
        for (float y = y0; y <= y1; y += step)
            for (float x = x0; x <= x1; x += step)
                if (inside(x, y)) into.Add(new Vector3(x * scale, y * scale, 0f));
    }

    private static float Ellipse(float x, float y, float rx, float ry, float rot)
    {
        float c = Mathf.Cos(rot), s = Mathf.Sin(rot);
        float u = x * c + y * s, v = -x * s + y * c;
        return u * u / (rx * rx) + v * v / (ry * ry);
    }

    // ───────────────────────── 소리 ─────────────────────────

    // 음표 순서 = 선율 순서. 녹음 음원이 있으면 그것, 없으면 임시 합성음
    private void PlayTone(int order)
    {
        if (audioSource == null || noteData == null) return;
        AudioClip clip = null;
        if (noteData.noteClips != null && noteData.noteClips.Length > 0)
            clip = noteData.noteClips[order % noteData.noteClips.Length];
        if (clip == null && noteData.melody != null && noteData.melody.Length > 0)
            clip = Synth(noteData.melody[order % noteData.melody.Length]);
        if (clip != null) audioSource.PlayOneShot(clip, noteData.volume);
    }

    // TODO: 임시 합성음 — 자체 편곡 음원이 나오면 noteClips로 교체
    public static AudioClip Synth(int midi) // 원형 음탄도 씀
    {
        if (synth.TryGetValue(midi, out var cached)) return cached;
        const int rate = 44100;
        const float length = 0.8f;
        float freq = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        int samples = Mathf.RoundToInt(rate * length);
        var data = new float[samples];
        for (int s = 0; s < samples; s++)
        {
            float t = s / (float)rate;
            float env = Mathf.Min(1f, t * 200f) * Mathf.Exp(-t * 4f);
            data[s] = env * (0.7f * Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t));
        }
        var clip = AudioClip.Create($"월광 음 {midi}", samples, 1, rate, false);
        clip.SetData(data, 0);
        synth[midi] = clip;
        return clip;
    }
}
