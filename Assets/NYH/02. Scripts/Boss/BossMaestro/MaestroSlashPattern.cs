using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using RealSteel.NanoBoss;
using UnityEngine;

/// <summary>
/// 마에스트로 「지휘봉 3연속 횡베기」 (2026-11-25 사용자 시트 '지휘봉 패턴 모션' 기준으로 다시 씀. 1악장, 타격 부위 오른팔).
///   준비 자세  몸을 웅크리고 두 팔을 앞으로 모은다 (지휘봉 생성) — 이번 초승달 윤곽이 옅게 미리 보인다
///   1타 R→L   몇 걸음 왼쪽으로 런지하며 크고 두꺼운 초승달(C자)을 그린다 — 위 오른쪽 → 위 → 왼쪽 → 아래
///   전환 동작  똑바로 서며 팔을 거둔다 (다음 초승달 윤곽 예고)
///   2타 L→R   거울 — 오른쪽으로 런지하며 거꾸로 된 초승달
///   전환 동작
///   3타 R→L   가장 크고 두꺼운 마무리 초승달
///   → 원래 자리로 돌아감
/// 판정 = 초승달이 그려지는 띠 그 자체 (보이는 곳 = 맞는 곳), 한 번 벨 때 한 번. 띠가 바닥부터 머리 위까지 지나가 회피 무적으로 피하는 구조.
/// 로직(보스 위치·초승달 진행·판정)은 전투 틱(CombatClock), 그림은 LateUpdate. 수치는 MaestroSlashData(TODO 임시값).
/// </summary>
public class MaestroSlashPattern : BossPatternGimmickBase
{
    [SerializeField] private MaestroSlashData slashData;

    private enum Phase { Idle, Windup, Strike, Gap, Return }

    private static readonly List<Collider2D> overlap = new List<Collider2D>(8);
    private static readonly ContactFilter2D overlapFilter = new ContactFilter2D { useTriggers = true };

    private MaestroBossController maestro;
    private MaestroSlashData.DifficultyRow row;
    private MaestroFx fx;
    private MaestroFx.Crescent slash, preview;

    private Phase phase = Phase.Idle;
    private bool running, subscribed, hitThisStrike;
    private bool posing, released; // 키 포즈(모션 시트 B-2)로 움직이나 · 마지막 포즈를 풀었나
    private int afterimages;
    private int curDir = -1;   // 이번 베기 방향 (준비 시작에 정함)
    private Transform player;
    private int frame, phaseStart, phaseEnd, strikeIndex, glintFrame;
    private float homeX, leftX, rightX, fromX, toX, bossH;
    private float reveal, prevReveal;
    // 이번 베기 초승달 (로직)
    private Vector3 cCenter;
    private float cRx, cRy, cA0, cA1, cThick;

    protected override bool UseExternalTellPhase => true;
    private static int Frames(float s) => Mathf.Max(1, Mathf.RoundToInt(s / CombatClock.TICK));
    private int StrikeCount => slashData.directions != null && slashData.directions.Length > 0 ? slashData.directions.Length : 3;
    private int Dir(int k) => slashData.directions != null && slashData.directions.Length > 0 ? (slashData.directions[k % slashData.directions.Length] < 0 ? -1 : 1) : (k % 2 == 0 ? -1 : 1);
    private float Tempo => Mathf.Max(0.1f, slashData.tempo);
    private float StrikeSeconds => slashData.Dash(maestro.SelectedDifficulty, row) / Tempo;
    private bool Last => strikeIndex >= StrikeCount - 1;

    protected override void OnAttached()
    {
        maestro = OwnerBoss as MaestroBossController;
        if (maestro == null) Debug.LogError($"[MaestroSlashPattern] {name}: 마에스트로 밑에 있어야 함");
        if (slashData == null) Debug.LogError($"[MaestroSlashPattern] {name}: 지휘봉 베기 수치(MaestroSlashData)가 비어 있음");
    }

    protected override void OnPreExecute()
    {
        ClearFx();
        if (maestro == null || slashData == null || maestro.Swarm == null) return;
        row = slashData.Row(maestro.SelectedDifficulty);
        if (row == null) return;
        fx = MaestroFx.Get(maestro);

        homeX = maestro.transform.position.x;
        if (!CameraBounds.TryGetVisibleX(Camera.main, maestro.Swarm.FrontZ, maestro.GroundY, out float minX, out float maxX)) { minX = homeX - 10f; maxX = homeX + 10f; }
        leftX = minX + slashData.edgeMargin;
        rightX = maxX - slashData.edgeMargin;
        if (rightX < leftX) leftX = rightX = (minX + maxX) * 0.5f;
        bossH = maestro.Look != null && maestro.Look.Ready ? maestro.Look.Height : 3f;

        if (maestro.Look != null) { maestro.Look.SetMoveSmooth(0.05f); maestro.Look.FormBaton(0.5f); } // 시트 '지휘봉 생성'
        posing = maestro.Look != null && maestro.Look.Ready && maestro.Look.HasKeyPoses;
        released = false;
        frame = 0;
        strikeIndex = 0;
        running = true;
        BeginWindup();
        Debug.Log($"[Maestro] 지휘봉 3연속 횡베기 — 베기 {StrikeSeconds}초 ({maestro.SelectedDifficulty}, {(posing ? "키 포즈" : "퍼펫 몸짓")})");
    }

    protected override async UniTask ExecuteActionAsync(CancellationToken token)
    {
        if (!running) return;
        await UniTask.WaitUntil(() => !running, cancellationToken: token);
    }

    // 끝났든 끊겼든: 이펙트 정리, 원래 자리로(그림은 부드럽게 미끄러져 감)
    protected override void CleanupEffects()
    {
        running = false;
        phase = Phase.Idle;
        ClearFx();
        if (maestro != null)
        {
            SetBossX(homeX);
            if (maestro.Look != null) { maestro.Look.SetMoveSmooth(-1f); maestro.Look.Stop(); maestro.Look.PoseRelease(0.3f); }
        }
    }

    private void ClearFx()
    {
        if (slash != null) { slash.Finish(); slash = null; }
        if (preview != null) { preview.Finish(); preview = null; }
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

    private void StartPhase(Phase p, float seconds)
    {
        phase = p;
        phaseStart = frame;
        phaseEnd = frame + Frames(seconds);
    }

    private float PhaseU => Mathf.Clamp01((frame - phaseStart) / (float)Mathf.Max(1, phaseEnd - phaseStart));

    private void CombatTick()
    {
        if (!running || maestro == null) return;
        frame++;
        float u = PhaseU;
        switch (phase)
        {
            case Phase.Windup:
                WarnTick(u);
                if (frame == glintFrame) Glint();
                if (frame >= phaseEnd) BeginStrike();
                break;
            case Phase.Gap:
                // 베기 사이 멈춤 (11-25 사용자: 연속 공격 사이 딜레이를 명확하게) — 타격 자세로 버틴다
                if (frame >= phaseEnd) BeginWindup();
                break;
            case Phase.Strike:
                Strike(u);
                break;
            case Phase.Return:
                SetBossX(Mathf.Lerp(fromX, toX, Mathf.SmoothStep(0f, 1f, u)));
                // 시트 '회수': 마지막 타격 포즈로 버틴 뒤 평소 모습으로 풀린다
                if (posing && !released && frame - phaseStart >= Frames(slashData.endHoldSeconds))
                {
                    released = true;
                    maestro.Look.PoseRelease(0.45f);
                }
                if (frame >= phaseEnd) running = false;
                break;
        }
    }

    // 준비 자세(첫 번째) / 전환 동작 — 다음 베기 초승달을 정하고 윤곽을 옅게 미리 보인다
    private void BeginWindup()
    {
        float wind = (strikeIndex == 0 ? row.firstWindup : row.windup) / Tempo * slashData.windupScale;
        StartPhase(Phase.Windup, wind);
        int dir = curDir = slashData.towardPlayer ? PlayerDir() : Dir(strikeIndex);
        float hold = Last ? slashData.endHoldSeconds : row.windup / Tempo;
        glintFrame = phaseEnd - Frames(Mathf.Min(slashData.glintLead, wind * 0.6f));
        if (posing)
            maestro.Look.PoseSlashPrep(strikeIndex, dir, wind * 0.8f); // 시트 '준비 자세' / '전환 동작' — 남은 20%는 그 자세로 멈춰 버팀(예고)
        else if (maestro.Look != null)
            maestro.Look.PlayTimed(dir < 0 ? MaestroGesture.SlashLeft : MaestroGesture.SlashRight, wind, StrikeSeconds, hold);

        // 이번 베기: 몇 걸음 나아갈 자리와 초승달
        float width = rightX - leftX + slashData.edgeMargin * 2f;
        float d = width * slashData.dashDistanceRatio;
        fromX = maestro.transform.position.x;
        toX = Mathf.Clamp(fromX + dir * d, leftX, rightX);
        float scale = Last ? slashData.finalScale.x : 1f;
        // 크기는 플레이어 키·카메라 기준 (11-25 사용자: 너무 커서 안 맞음) — 위쪽 띠는 머리 높이, 아래쪽 띠는 발밑을 지난다
        float ph = maestro.Look != null && maestro.Look.Ready ? maestro.Look.PlayerHeight : 1.5f;
        cRx = Mathf.Min((Mathf.Abs(toX - fromX) * 0.5f + ph * slashData.nearReachPerPlayer) * scale, width * slashData.maxReachScreen);
        cRy = ph * slashData.heightPerPlayer * scale;
        cThick = slashData.crescentThickness * (Last ? slashData.finalScale.y : 1f);
        // 가까이 (11-25 사용자: 멀리만 때려 붙어 있으면 안 맞음) — 중심을 보스 쪽으로 당겨 초승달이 보스 몸 바로 옆부터 감싸듯 지나간다
        cCenter = new Vector3(fromX + dir * cRx * slashData.nearCenterAhead, maestro.GroundY + ph * slashData.centerPerPlayer, maestro.Swarm.FrontZ);
        Vector2 ang = slashData.sweepAngles;
        cA0 = dir < 0 ? ang.x : 180f - ang.x;
        cA1 = dir < 0 ? ang.y : 180f - ang.y;

        if (fx != null)
        {
            preview = Make(slashData.warnAlphaRange.x);
            preview.reveal = 1f;
            preview.fadeSeconds = 0.15f;
        }
    }

    // 전조: 베기 직전 지휘봉 끝이 크게 번쩍 + 고리 (11-25 사용자: 모션이 작아 전조를 알기 힘듦)
    private void Glint()
    {
        if (fx == null) return;
        Vector3 tip = maestro.Look != null && maestro.Look.Ready ? maestro.Look.BatonTip : cCenter;
        float ph = maestro.Look != null && maestro.Look.Ready ? maestro.Look.PlayerHeight : 1.5f;
        fx.Flash(MaestroFx.Disc, tip, Vector2.one * ph * 0.9f, 0f, MaestroFx.MoonWhite, 0.25f);
        fx.Ring(tip, ph * 0.1f, ph * 0.6f, 0.3f, MaestroFx.MoonWhite);
    }

    private MaestroFx.Crescent Make(float alpha)
    {
        var c = fx.CreateCrescent();
        c.center = cCenter; c.rx = cRx; c.ry = cRy; c.a0 = cA0; c.a1 = cA1; c.thickness = cThick;
        c.core = new Color(MaestroFx.MoonWhite.r, MaestroFx.MoonWhite.g, MaestroFx.MoonWhite.b, alpha);
        c.edge = new Color(MaestroFx.Lavender.r, MaestroFx.Lavender.g, MaestroFx.Lavender.b, alpha);
        c.fill = new Color(0f, 0f, 0f, slashData.fillHit ? slashData.fillAlpha * Mathf.Clamp01(alpha * 1.5f) : 0f);
        c.fadeSeconds = slashData.crescentFade;
        return c;
    }

    private void SetAlpha(MaestroFx.Crescent c, float a)
    {
        c.core.a = a;
        c.edge.a = a;
        c.fill.a = slashData.fillHit ? slashData.fillAlpha * Mathf.Clamp01(a * 1.5f) : 0f;
    }

    // 경고 (11-25 사용자: 사전 경고를 명확하게) — 윤곽이 점점 진해지고, 베기 직전엔 빠르게 깜박인다
    private void WarnTick(float u)
    {
        if (preview == null) return;
        Vector2 r = slashData.warnAlphaRange;
        float a = Mathf.Lerp(r.x, r.y, u * u);
        int blinkFrames = Frames(slashData.warnBlinkSeconds);
        if (slashData.warnBlinkSeconds > 0f && phaseEnd - frame <= blinkFrames)
            a = ((phaseEnd - frame) / 3) % 2 == 0 ? Mathf.Min(1f, r.y * 1.8f) : r.y * 0.5f; // 3프레임마다 밝게·어둡게
        SetAlpha(preview, a);
    }

    private void BeginStrike()
    {
        StartPhase(Phase.Strike, StrikeSeconds);
        hitThisStrike = false;
        reveal = prevReveal = 0f;
        if (preview != null) { preview.Finish(); preview = null; }
        if (fx != null) slash = Make(1f);
        afterimages = 0;
        if (posing)
        {
            // 준비 자세 잔상을 남기고 타격 포즈로 확 꽂힌다 (벰 시간의 앞 35% 안에)
            maestro.Look.PoseAfterimage(0.3f, AfterimageColor(0.35f));
            maestro.Look.PoseSlashHit(strikeIndex, curDir, StrikeSeconds * 0.35f);
        }
    }

    private static Color AfterimageColor(float a) => new Color(MaestroFx.Lavender.r, MaestroFx.Lavender.g, MaestroFx.Lavender.b, a);

    // 1·2·3타 — 런지(빠른 출발·감속)하며 초승달이 그려진다. 판정 = 이번 틱에 새로 그려진 띠
    private void Strike(float u)
    {
        SetBossX(Mathf.Lerp(fromX, toX, 1f - (1f - u) * (1f - u) * (1f - u)));
        prevReveal = reveal;
        reveal = 1f - (1f - u) * (1f - u); // 확 그려지다 끝에서 느려짐

        // 시트 '잔상': 런지하며 지나온 길에 타격 포즈 실루엣이 겹겹이 남는다
        if (posing && afterimages < 2 && u >= 0.3f + 0.3f * afterimages)
        {
            afterimages++;
            maestro.Look.PoseAfterimage(0.35f, AfterimageColor(0.3f - 0.08f * afterimages));
        }

        if (!hitThisStrike)
        {
            float r = Mathf.Min(cRx, cRy);
            for (float s = prevReveal; s <= reveal + 1e-4f; s += 0.02f)
            {
                float th = Mathf.Max(0.05f, cThick * MaestroFx.Crescent.Profile(s));
                float a = Mathf.Lerp(cA0, cA1, s) * Mathf.Deg2Rad;
                var dirV = new Vector3(Mathf.Cos(a) * cRx, Mathf.Sin(a) * cRy, 0f);
                // 띠 + (fillHit) 안쪽 부채꼴 — 바깥 띠에서 중심 쪽으로 몇 점 (11-25: 붙어 있어도 맞게)
                int rings = slashData.fillHit ? 4 : 1;
                bool done = false;
                for (int q = 0; q < rings && !done; q++)
                {
                    float rad = q == 0 ? 1f - th * 0.5f : (1f - th) * (1f - q / (float)rings) + 0.08f;
                    float hr = q == 0 ? Mathf.Max(0.15f, th * r * 0.6f) : Mathf.Max(0.2f, r / rings * 0.75f);
                    int count = Physics2D.OverlapCircle(cCenter + dirV * rad, hr, overlapFilter, overlap);
                    for (int k = 0; k < count; k++)
                    {
                        if (!overlap[k].CompareTag("Player")) continue;
                        if (ApplyDamageToPlayer(overlap[k].gameObject)) { hitThisStrike = true; done = true; } // 회피 무적이면 false
                        break;
                    }
                }
                if (done) break;
            }
        }

        if (u < 1f) return;
        if (slash != null) { slash.reveal = 1f; slash.Finish(); slash = null; }
        strikeIndex++;
        if (strikeIndex < StrikeCount)
        {
            if (slashData.strikeGap > 0f) StartPhase(Phase.Gap, slashData.strikeGap);
            else BeginWindup();
        }
        else
        {
            // 마지막: 잠시 버티고 원래 자리로
            fromX = maestro.transform.position.x;
            toX = homeX;
            StartPhase(Phase.Return, slashData.endHoldSeconds + slashData.positionReturnSeconds.y);
        }
    }

    // 플레이어 쪽 (11-25 사용자: 좌우 번갈아가 아니라 플레이어 쪽으로) — 거의 겹쳐 있으면 지난 방향 유지
    private int PlayerDir()
    {
        if (player == null) { var go = GameObject.FindWithTag("Player"); if (go != null) player = go.transform; }
        if (player == null) return Dir(strikeIndex);
        float dx = player.position.x - maestro.transform.position.x;
        return Mathf.Abs(dx) < 0.1f ? curDir : (dx < 0f ? -1 : 1);
    }

    private void SetBossX(float x)
    {
        var p = maestro.transform.position;
        maestro.transform.position = new Vector3(x, p.y, p.z);
    }

    // ───────────────────────── 그림 ─────────────────────────

    private void LateUpdate()
    {
        if (!running || slash == null) return;
        slash.reveal = Mathf.Lerp(prevReveal, reveal, CombatClock.InterpolationAlpha);
    }
}
