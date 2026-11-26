using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using UnityEngine;

/// <summary>
/// 마에스트로 「빛기둥」 (2026-11-26 사용자 — 패턴 제조기 모션 '오픈모션' 기반. 사용자 그림: 흰 칸 = 빛기둥, 검은 칸 = 피하는 곳).
///   ① 모션 재생 — 모션의 '보스 이동'(뒤판으로 들어가기)이 시작되는 순간 순간이동: 떠난 자리 번쩍 → 봇이 흩어져 새 자리에 모임.
///      깊이가 hideDepth 이상이면 본체 판정 꺼짐 + 무적
///   ② 두 팔을 활짝 든 자세(holdTimes.x)에서 모션을 멈추고, 화면 전체를 세로 띠로 나눠 빛기둥 칸에 예고(옅게 → 진하게 → 깜박) →
///      하늘에서 빛기둥이 떨어져 바닥에 꽂힘(판정) → 사라짐. 이것을 1~4번(난이도) 반복, 매번 기둥 자리를 뒤바꿈(예전 안전한 곳에 떨어짐)
///   ③ 모션을 holdTimes.y부터 이어서 — '앞으로 나오기' 이동이 시작되는 순간 다시 순간이동해 돌아와 끝
/// 로직(진행·판정)은 전투 틱(CombatClock), 그림은 LateUpdate. 모션 시간도 이 패턴이 전투 틱으로 몬다(Look.SetMotionTime).
/// 수치는 MaestroOpenData(TODO 임시값).
/// </summary>
public class MaestroOpenPattern : BossPatternGimmickBase, IMaestroMovementGate
{
    [SerializeField] private MaestroOpenData openData;
    [Tooltip("이 악장부터 나온다 (기획서에 아직 없음 — 시험용 1)")]
    [Min(1)] [SerializeField] private int minMovement = 1;

    public int MinMovement => minMovement;

    private enum Phase { Intro, Waves, Outro }

    private class Pillar
    {
        public float x;
        public SpriteRenderer warn, glow, core, mark;
    }

    private class Wave
    {
        public int warnFrame, dropFrame, landFrame, activeEnd, fadeEnd;
        public float phase;             // 띠 시작 자리 (m)
        public bool started, landed, hit, done;
        public readonly List<Pillar> pillars = new List<Pillar>(16);
    }

    private static readonly List<Collider2D> overlap = new List<Collider2D>(8);
    private static readonly ContactFilter2D overlapFilter = new ContactFilter2D { useTriggers = true };

    private MaestroBossController maestro;
    private MaestroOpenData.DifficultyRow row;
    private MaestroMotion motion;
    private MaestroFx fx;
    private readonly List<Wave> waves = new List<Wave>(4);
    private Phase phase;
    private bool running, subscribed;
    private int frame, wavesEndFrame;
    private float mt, playerH = 1.5f, pillarW, safeW, minX, maxX, topY;
    private int arriveFlashFrame = -1;

    protected override bool UseExternalTellPhase => true;
    private static int Frames(float s) => Mathf.Max(0, Mathf.RoundToInt(s / CombatClock.TICK));

    protected override void OnAttached()
    {
        maestro = OwnerBoss as MaestroBossController;
        if (maestro == null) Debug.LogError($"[MaestroOpenPattern] {name}: 마에스트로 밑에 있어야 함");
        if (openData == null) Debug.LogError($"[MaestroOpenPattern] {name}: 빛기둥 수치(MaestroOpenData)가 비어 있음");
    }

    protected override void OnPreExecute()
    {
        Clear();
        if (maestro == null || openData == null || maestro.Swarm == null) return;
        row = openData.Row(maestro.SelectedDifficulty);
        motion = openData.motion;
        if (row == null) return;
        fx = MaestroFx.Get(maestro);
        var look = maestro.Look;
        playerH = look != null && look.Ready ? look.PlayerHeight : 1.5f;
        pillarW = playerH * openData.pillarWidthPerPlayer;
        safeW = playerH * openData.safeWidthPerPlayer;

        float frontZ = maestro.Swarm.FrontZ;
        if (!CameraBounds.TryGetVisibleX(Camera.main, frontZ, maestro.GroundY, out minX, out maxX)) { minX = maestro.transform.position.x - 12f; maxX = maestro.transform.position.x + 12f; }
        topY = TopY(frontZ, out float ty) ? ty : maestro.GroundY + 10f;

        if (look != null)
        {
            if (motion != null) { look.PlayMotion(motion, motion.length); look.MotionTimeScale = 0f; look.SetMotionTime(0f); }
            else look.Play(MaestroGesture.RaiseBoth, 6f);
        }
        mt = 0f;
        frame = 0;
        phase = Phase.Intro;
        running = true;
        if (!openData.teleport) ApplyMove(0f);
        Debug.Log($"[Maestro] 빛기둥 — 기둥 폭 {pillarW:0.0}m · 피하는 곳 {safeW:0.0}m, {row.waves.x}~{row.waves.y}번, 예고 {row.warn:0.00}초 ({maestro.SelectedDifficulty}{(motion == null ? ", 모션 없음" : "")})");
    }

    protected override async UniTask ExecuteActionAsync(CancellationToken token)
    {
        if (!running) return;
        await UniTask.WaitUntil(() => !running, cancellationToken: token);
    }

    // 끝났든 끊겼든: 기둥 정리, 제자리·앞판으로 (악장 전환 중이면 무적은 전환이 맡는다)
    protected override void CleanupEffects()
    {
        bool moved = false;
        if (maestro != null) { maestro.GetBodyPose(out var po, out float pg, out bool pb); moved = pb || po.sqrMagnitude > 0.0025f || Mathf.Abs(pg - 1f) > 0.02f; }
        running = false;
        Clear();
        if (maestro == null) return;
        maestro.ResetBodyPose();
        maestro.SetBodyDepth(-1f);
        if (maestro.Look != null)
        {
            maestro.Look.MotionTimeScale = 1f;
            maestro.Look.Stop();
            if (moved) maestro.Look.Teleport(openData != null ? openData.teleportSeconds : 0.4f); // 제자리가 아니면 순간이동으로 돌아옴 (끊겼을 때 · 모션 이동 합이 0이 아닐 때)
        }
        if (!maestro.InTransition && OwnerBoss.Snapshot != null) OwnerBoss.Snapshot.isInvincible = false;
    }

    private void Clear()
    {
        foreach (var w in waves) ReleaseFx(w);
        waves.Clear();
    }

    private void ReleaseFx(Wave w)
    {
        if (fx == null) return;
        foreach (var p in w.pillars)
        {
            if (p.warn != null) fx.Release(p.warn);
            if (p.glow != null) fx.Release(p.glow);
            if (p.core != null) fx.Release(p.core);
            if (p.mark != null) fx.Release(p.mark);
            p.warn = p.glow = p.core = p.mark = null;
        }
        w.pillars.Clear();
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
        if (!running || maestro == null) return;
        frame++;
        if (frame == arriveFlashFrame) ArriveFlash();

        switch (phase)
        {
            case Phase.Intro:
            {
                float hold = openData.holdTimes.x;
                AdvanceMotion(Mathf.Min(hold, mt + CombatClock.TICK));
                if (mt >= hold - 1e-4f) BeginWaves();
                break;
            }
            case Phase.Waves:
                TickWaves();
                if (frame >= wavesEndFrame)
                {
                    phase = Phase.Outro;
                    AdvanceMotion(Mathf.Max(mt, openData.holdTimes.y)); // 멈춰 둔 구간을 건너뛰고 이어서 (사이 이동도 처리)
                }
                break;
            case Phase.Outro:
            {
                float end = motion != null ? motion.length : mt;
                AdvanceMotion(mt + CombatClock.TICK);
                if (mt >= end) running = false;
                break;
            }
        }
    }

    // 모션 시간을 next로 — 사이에 시작되는 '보스 이동'은 순간이동(또는 경로대로)
    private void AdvanceMotion(float next)
    {
        float prev = mt;
        mt = next;
        if (maestro.Look != null && motion != null) maestro.Look.SetMotionTime(mt);
        if (motion == null || !motion.HasMove) return;
        if (!openData.teleport) { ApplyMove(mt); return; }
        foreach (var e in motion.events)
        {
            if (e == null || e.type != MaestroMotion.EventType.Line || e.lineRole != MaestroMotion.LineRole.BossMove) continue;
            bool startsNow = e.time > prev || (prev <= 0f && e.time <= 0f && frame == 1);
            if (startsNow && e.time <= mt) TeleportTo(e.time + e.duration);
        }
    }

    // 경로대로 따라가기 (순간이동 끔)
    private void ApplyMove(float t)
    {
        if (motion == null || !motion.HasMove || maestro.Look == null) return;
        motion.EvaluateMove(t, out var off, out float grow, out float depth);
        SetBody(off, grow, depth);
    }

    private void SetBody(Vector2 off, float grow, float depth)
    {
        bool back = depth >= motion.hideDepth;
        Vector3 w = maestro.Look.MotionToWorldDelta(off, false);
        maestro.SetBodyPose(new Vector2(w.x, w.y), grow, back);
        maestro.SetBodyDepth(depth);
        if (OwnerBoss.Snapshot != null && !maestro.InTransition) OwnerBoss.Snapshot.isInvincible = back;
    }

    // 순간이동 (11-26 사용자) — 떠나는 자리 번쩍 → 도착 자리(이동이 끝난 상태)로 바로, 봇은 흩어졌다 다시 모임
    private void TeleportTo(float arriveTime)
    {
        if (maestro.Look == null) return;
        Vector3 from = maestro.BodyCenter;
        motion.EvaluateMove(arriveTime, out var off, out float grow, out float depth);
        SetBody(off, grow, depth);
        maestro.Look.Teleport(openData.teleportSeconds);
        if (fx != null)
        {
            fx.Flash(MaestroFx.Disc, from, Vector2.one * playerH * 1.4f, 0f, MaestroFx.MoonWhite, 0.3f);
            fx.Ring(from, playerH * 0.2f, playerH * 1.2f, 0.35f, MaestroFx.Lavender);
        }
        arriveFlashFrame = frame + Mathf.Max(2, Frames(openData.teleportSeconds * 0.5f));
    }

    private void ArriveFlash()
    {
        if (fx == null) return;
        var c = maestro.BodyCenter;
        fx.Ring(c, playerH * 0.6f, playerH * 0.15f, 0.3f, MaestroFx.MoonWhite); // 안으로 모이는 고리
    }

    // ───────────────────────── 빛기둥 ─────────────────────────

    private void BeginWaves()
    {
        phase = Phase.Waves;
        int n = Random.Range(Mathf.Max(1, row.waves.x), Mathf.Max(row.waves.x, row.waves.y) + 1);
        float period = pillarW + safeW;
        float phase0 = Random.value * period;
        int at = frame + 1;
        for (int k = 0; k < n; k++)
        {
            var w = new Wave
            {
                warnFrame = at,
                // 기둥 자리 뒤바꾸기: 반 주기씩 밀면 기둥이 예전 피하는 곳 가운데로 온다
                phase = phase0 + (openData.alternate ? (k & 1) * period * 0.5f : 0f),
            };
            w.dropFrame = w.warnFrame + Mathf.Max(1, Frames(row.warn));
            w.landFrame = w.dropFrame + Mathf.Max(1, Frames(openData.dropSeconds));
            w.activeEnd = w.landFrame + Mathf.Max(1, Frames(openData.activeSeconds));
            w.fadeEnd = w.activeEnd + Mathf.Max(1, Frames(openData.fadeSeconds));
            waves.Add(w);
            at = w.activeEnd + Frames(row.gap);
        }
        wavesEndFrame = waves[waves.Count - 1].fadeEnd;
    }

    private void TickWaves()
    {
        foreach (var w in waves)
        {
            if (w.done) continue;
            if (!w.started && frame >= w.warnFrame) StartWave(w);
            if (!w.started) continue;
            if (!w.landed && frame >= w.landFrame)
            {
                w.landed = true;
                if (fx != null)
                    foreach (var p in w.pillars)
                        fx.Ring(new Vector3(p.x, maestro.GroundY, maestro.Swarm.FrontZ), pillarW * 0.3f, pillarW * 0.9f, 0.3f, MaestroFx.MoonWhite, flat: true);
                if (maestro.Look != null) maestro.Look.Flick();
            }
            if (w.landed && !w.hit && frame < w.activeEnd) HitCheck(w);
            if (frame >= w.fadeEnd) { w.done = true; ReleaseFx(w); }
        }
    }

    // 화면 전체를 띠로 — 기둥 칸 가운데 x들
    private void StartWave(Wave w)
    {
        w.started = true;
        float period = pillarW + safeW;
        float start = minX - period + Mathf.Repeat(w.phase, period);
        for (float x = start; x <= maxX + period; x += period)
        {
            float cx = x + pillarW * 0.5f;
            if (cx + pillarW * 0.5f < minX || cx - pillarW * 0.5f > maxX) continue;
            var p = new Pillar { x = cx };
            if (fx != null)
            {
                p.warn = fx.Acquire(MaestroFx.Column);
                p.mark = fx.Acquire(MaestroFx.Column);
                p.glow = fx.Acquire(MaestroFx.Column);
                p.core = fx.Acquire(MaestroFx.Column);
            }
            w.pillars.Add(p);
        }
    }

    // 판정 = 바닥에 닿은 기둥 칸 (회피 무적이면 안 맞음). 한 번 떨굴 때 한 번
    private void HitCheck(Wave w)
    {
        float h = topY - maestro.GroundY;
        foreach (var p in w.pillars)
        {
            var c = new Vector2(p.x, maestro.GroundY + h * 0.5f);
            int count = Physics2D.OverlapBox(c, new Vector2(pillarW * 0.9f, h), 0f, overlapFilter, overlap);
            for (int k = 0; k < count; k++)
            {
                if (!overlap[k].CompareTag("Player")) continue;
                if (ApplyDamageToPlayer(overlap[k].gameObject)) { w.hit = true; return; }
                break;
            }
        }
    }

    private bool TopY(float z, out float y)
    {
        y = 0f;
        var cam = Camera.main;
        if (cam == null) return false;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 1f, 0f));
        if (Mathf.Abs(ray.direction.z) < 1e-6f) return false;
        float t = (z - ray.origin.z) / ray.direction.z;
        if (t <= 0f) return false;
        y = ray.origin.y + ray.direction.y * t + 0.5f; // 화면 위로 조금 더
        return true;
    }

    // ───────────────────────── 그림 ─────────────────────────

    private void LateUpdate()
    {
        if (fx == null || waves.Count == 0 || maestro == null) return;
        float f = frame + CombatClock.InterpolationAlpha;
        float ground = maestro.GroundY, z = maestro.Swarm.FrontZ;
        float fullH = topY - ground;
        var lav = MaestroFx.Lavender; var white = MaestroFx.MoonWhite; var vio = MaestroFx.Violet;
        bool blinkOn = (Mathf.FloorToInt(Time.time * 16f) & 1) == 0;
        foreach (var w in waves)
        {
            if (!w.started || w.done) continue;
            foreach (var p in w.pillars)
            {
                if (p.warn == null) continue;
                if (f < w.dropFrame)
                {
                    // 예고: 기둥 칸이 옅게 → 진하게, 마지막 0.2초 깜박 + 바닥 표시줄
                    float u = Mathf.InverseLerp(w.warnFrame, w.dropFrame, f);
                    float a = Mathf.Lerp(openData.warnAlpha.x, openData.warnAlpha.y, u * u);
                    if ((w.dropFrame - f) * CombatClock.TICK <= 0.2f) a *= blinkOn ? 1.7f : 0.5f;
                    fx.Place(p.warn, new Vector3(p.x, ground + fullH * 0.5f, z), new Vector2(pillarW, fullH), new Color(vio.r, vio.g, vio.b, a));
                    fx.Place(p.mark, new Vector3(p.x, ground + 0.04f, z), new Vector2(pillarW, playerH * 0.08f), new Color(white.r, white.g, white.b, Mathf.Min(1f, a * 2.5f)));
                    Hide(p.glow); Hide(p.core);
                }
                else
                {
                    // 낙하: 위에서 바닥까지 내려꽂힘 → 버팀 → 가늘어지며 사라짐
                    float drop = Mathf.Clamp01(Mathf.InverseLerp(w.dropFrame, w.landFrame, f));
                    float fade = f < w.activeEnd ? 1f : 1f - Mathf.Clamp01(Mathf.InverseLerp(w.activeEnd, w.fadeEnd, f));
                    float bottom = Mathf.Lerp(topY, ground, drop * drop);
                    float len = Mathf.Max(0.01f, topY - bottom);
                    var mid = new Vector3(p.x, bottom + len * 0.5f, z);
                    float thin = Mathf.Lerp(0.35f, 1f, fade);
                    fx.Place(p.glow, mid, new Vector2(pillarW * 1.15f * thin, len), new Color(lav.r, lav.g, lav.b, 0.55f * fade));
                    fx.Place(p.core, mid, new Vector2(pillarW * 0.7f * thin, len), new Color(white.r, white.g, white.b, 0.95f * fade));
                    fx.Place(p.warn, new Vector3(p.x, ground + fullH * 0.5f, z), new Vector2(pillarW, fullH), new Color(vio.r, vio.g, vio.b, openData.warnAlpha.y * 0.5f * fade));
                    fx.Place(p.mark, new Vector3(p.x, ground + 0.04f, z), new Vector2(pillarW * 1.2f, playerH * 0.12f), new Color(white.r, white.g, white.b, fade * (w.landed ? 1f : 0.6f)));
                }
            }
        }
    }

    private static void Hide(SpriteRenderer r) { if (r != null) r.color = Color.clear; }
}
