using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using RealSteel.NanoBoss;
using UnityEngine;

/// <summary>
/// 마에스트로 1악장 「거대 지휘봉」 (보스3_월광_기획 1.3, 2026-11-24 4단계). 파훼형 + 기믹, 타격 부위 오른팔(패턴 데이터).
///   ① 흐름   몸과 지휘봉이 봇을 끌어모아 거대해짐 (보스 피격 가능)
///   ② 이동   보스가 배경(뒤판)으로 빠짐. 지휘봉은 앞판에 남음 (보스 무적)
///   ③ 예고   궤적이 희미한 선으로 먼저 보임 (난이도별 1.0~0.6초). 기준점 = 패턴 시작 순간의 플레이어 위치(고정)
///   ④ 공격   지휘봉 끝이 8자(세로) 또는 무한대(가로) 리사주 궤적을 그림. 지휘봉 피격 가능 (보스는 무적)
///   ⑤ 반복   궤적 2~3회 (난이도별, 헬은 회차마다 가속)
///   ⑥ 붕괴   보스와 지휘봉이 모래로 무너진 뒤 제자리에서 원래 크기로 재생성 (보스 피격 가능)
/// 판정: 지휘봉 전체에 닿으면 피해(1회 뒤 hitCooldown). 지휘봉 체력 = 보스 악장 최대 HP × 5%(임시).
/// 파괴: 패턴이 즉시 끝나고 보스가 제자리로 떨어져 그로기(KKH InterruptToGroggy, 임시 4초). 파괴 실패면 ⑥으로 정상 종료.
///
/// 지휘봉 = 군집 줄기 A(TubeGuide, 손잡이 굵고 끝 가는 테이퍼). 끝은 궤적 위, 손잡이는 궤적 중심 위의 '손' 쪽을 향하는 곧은 막대.
/// 플레이어 공격은 지휘봉 판정 콜라이더(IPlayerHitTarget)로 받는다 — 보스 HP로 가지 않는다(HitDetection 규칙).
/// 로직은 전투 틱(CombatClock), 그림은 LateUpdate.
/// </summary>
public class MaestroBatonPattern : BossPatternGimmickBase, IPlayerHitTarget
{
    [Tooltip("거대 지휘봉 수치 (궤적·반복·지휘봉 크기·체력·그로기)")]
    [SerializeField] private MaestroBatonData batonData;

    private enum Phase { Idle, Gather, Move, Tell, Attack, Collapse }

    private static readonly List<Collider2D> overlap = new List<Collider2D>(8);
    private static readonly ContactFilter2D overlapFilter = new ContactFilter2D { useTriggers = true };
    private const int CurveSamples = 96;

    private MaestroBossController maestro;
    private MaestroBatonData.DifficultyRow row;
    private readonly List<int> batonBots = new List<int>(4096);
    private readonly List<int> tmp = new List<int>(4096);
    private readonly List<int> loopEnds = new List<int>(4);
    private BoxCollider2D hurtBox;
    private LineRenderer tellLine;

    private Phase phase = Phase.Idle;
    private bool running, subscribed, broken, eight;
    private int frame, gatherEnd, moveEnd, tellEnd, attackEnd, collapseEnd, hitCooldownLeft;
    private int side;
    private int batonHp, batonMaxHp;
    private float batonLength;
    private Vector3 refCenter, handStart;
    private Vector3 grip, tip, prevGrip, prevTip;   // 논리 위치 (틱)
    private Vector3 moveFromGrip, moveFromTip;

    protected override bool UseExternalTellPhase => true;

    private RSNanoSwarm Swarm => maestro != null ? maestro.Swarm : null;
    private static int Frames(float seconds) => Mathf.Max(0, Mathf.RoundToInt(seconds / CombatClock.TICK));

    public bool IsHittable => phase == Phase.Attack && !broken && running;
    public int BatonHp => batonHp;

    // ───────────────────────── KKH 패턴 골격 ─────────────────────────

    protected override void OnAttached()
    {
        maestro = OwnerBoss as MaestroBossController;
        if (maestro == null) Debug.LogError($"[MaestroBatonPattern] {name}: 마에스트로(MaestroBossController) 밑에 있어야 함");
        if (batonData == null) Debug.LogError($"[MaestroBatonPattern] {name}: 지휘봉 수치(MaestroBatonData)가 비어 있음");
        if (patternData == null) Debug.LogWarning($"[MaestroBatonPattern] {name}: 패턴 데이터가 비어 있음 — 데미지·부위가 KKH 기본값");
        EnsureHurtBox();
        EnsureTellLine();
    }

    protected override void OnPreExecute()
    {
        ReleaseBaton(0);
        broken = false;
        var swarm = Swarm;
        if (maestro == null || batonData == null || swarm == null) return;
        row = batonData.Row(maestro.SelectedDifficulty);
        if (row == null) return;

        // 기준점 = 지금 플레이어 위치 (고정)
        Transform player = maestro.PlayerTransform;
        Vector3 body = maestro.BodyCenter;
        side = player != null && player.position.x < body.x ? -1 : 1;
        float refX = player != null ? player.position.x : body.x + side * 4f;
        refCenter = new Vector3(refX, maestro.GroundY + batonData.centerHeight, swarm.FrontZ);
        eight = Random.value < batonData.eightChance;

        // 지휘봉 봇 — 몸 최소 크기를 지키는 만큼. 모자라면 굵기 대신 길이를 줄인다
        int spare = maestro.SpareBots(batonData.bodyMinRatio);
        int n = Mathf.Min(batonData.maxBots, spare);
        if (n < 32)
        {
            Debug.LogWarning("[Maestro] 거대 지휘봉 — 몸에 남은 봇이 모자라 지휘봉을 만들지 못함 (bodyMinRatio)");
            return;
        }
        float r0 = batonData.thickness.x, r1 = batonData.thickness.y;
        float sp = swarm.spacing;
        float maxLen = n * sp * sp / Mathf.Max(0.01f, r0 + r1);
        batonLength = Mathf.Clamp(maxLen, 0.5f, batonData.length);

        handStart = maestro.HandPoint(side); // 외형이면 지휘봉 끝에서 거대 지휘봉이 자라난다
        handStart.z = swarm.FrontZ;
        swarm.Collect(RSNanoSwarm.G_Blob, handStart, n, batonBots);
        swarm.TubeA.r0 = r0;
        swarm.TubeA.r1 = r1;
        swarm.TubeA.glow = batonData.glow;
        swarm.TubeA.danger = false; // 판정은 이 스크립트가 (전투 틱)
        foreach (int i in batonBots) swarm.Bind(i, RSNanoSwarm.G_TubeA);

        // 단계 시간
        gatherEnd = Frames(batonData.gatherSeconds);
        moveEnd = gatherEnd + Frames(batonData.moveSeconds);
        tellEnd = moveEnd + Frames(row.tellSeconds);
        loopEnds.Clear();
        float loop = row.loopSeconds;
        int at = tellEnd;
        for (int k = 0; k < Mathf.Max(1, row.loops); k++)
        {
            at += Mathf.Max(1, Frames(loop));
            loopEnds.Add(at);
            loop *= row.loopSpeedUp;
        }
        attackEnd = at;
        collapseEnd = attackEnd + Frames(batonData.collapseSeconds);

        batonMaxHp = Mathf.Max(1, Mathf.RoundToInt((OwnerBoss.Snapshot != null ? OwnerBoss.Snapshot.maxHp : 1000) * batonData.hpRatio));
        batonHp = batonMaxHp;
        hitCooldownLeft = 0;

        grip = prevGrip = handStart;
        tip = prevTip = handStart;
        frame = 0;
        phase = Phase.Gather;
        running = true;
        if (maestro.Look != null) maestro.Look.Play(MaestroGesture.RaiseBoth, (moveEnd + Frames(row.tellSeconds)) * CombatClock.TICK); // ①~③ 지휘봉을 치켜든다
        Debug.Log($"[Maestro] 거대 지휘봉 — {(eight ? "8자(세로)" : "무한대(가로)")} {row.loops}회, 지휘봉 체력 {batonHp}, 길이 {batonLength:0.0}m·{batonBots.Count}봇 ({maestro.SelectedDifficulty})");
    }

    protected override async UniTask ExecuteActionAsync(CancellationToken token)
    {
        if (!running) return;
        await UniTask.WaitUntil(() => !running, cancellationToken: token);
    }

    // 정상 종료든 끊김(파괴 그로기·악장 전환·전투 종료)이든: 판정 끄고, 남은 지휘봉은 모래로, 몸은 제자리 앞판으로, 무적 해제
    protected override void CleanupEffects()
    {
        running = false;
        phase = Phase.Idle;
        SetHurtBox(false);
        if (tellLine != null) tellLine.enabled = false;
        ReleaseBaton(batonData != null ? Frames(batonData.collapseSeconds) : 30);
        RestoreBoss();
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
        prevGrip = grip;
        prevTip = tip;
        if (hitCooldownLeft > 0) hitCooldownLeft--;

        switch (phase)
        {
            case Phase.Gather:
            {
                // ① 몸이 커지고, 손 자리에서 지휘봉이 위로 자라난다
                float t = Mathf.Clamp01(frame / (float)Mathf.Max(1, gatherEnd));
                maestro.SetBodyPose(Vector2.zero, Mathf.Lerp(1f, batonData.growScale, t), false);
                grip = handStart;
                tip = handStart + Vector3.up * batonLength * Mathf.Lerp(0.15f, 1f, t);
                if (frame >= gatherEnd)
                {
                    // ② 보스는 배경으로, 지휘봉은 앞판에 남아 궤적 시작 자리로
                    phase = Phase.Move;
                    maestro.SetBodyPose(batonData.backOffset, batonData.growScale, true);
                    SetInvincible(true);
                    moveFromGrip = grip;
                    moveFromTip = tip;
                }
                break;
            }
            case Phase.Move:
            {
                float t = Mathf.SmoothStep(0f, 1f, (frame - gatherEnd) / (float)Mathf.Max(1, moveEnd - gatherEnd));
                PoseAt(0f, out Vector3 g, out Vector3 tp);
                grip = Vector3.Lerp(moveFromGrip, g, t);
                tip = Vector3.Lerp(moveFromTip, tp, t);
                if (frame >= moveEnd)
                {
                    phase = Phase.Tell; // ③ 궤적 예고선
                    DrawTellLine();
                }
                break;
            }
            case Phase.Tell:
                PoseAt(0f, out grip, out tip);
                if (frame >= tellEnd)
                {
                    phase = Phase.Attack; // ④ 공격 — 지휘봉 피격 가능
                    if (tellLine != null) tellLine.enabled = false;
                    // 배경의 보스는 몸 전체로 지휘 (기획서 1.3 ④)
                    if (maestro.Look != null) maestro.Look.Play(MaestroGesture.ConductFast, (attackEnd - tellEnd) * CombatClock.TICK);
                }
                break;
            case Phase.Attack:
            {
                PoseAt(AttackProgress(), out grip, out tip);
                UpdateHurtBox();
                CheckPlayerHit();
                if (frame >= attackEnd) BeginCollapse();
                break;
            }
            case Phase.Collapse:
                if (frame >= collapseEnd) running = false;
                break;
        }
    }

    // 지금 몇 바퀴째의 어디쯤인지 → 0~1 (한 바퀴 안 진행률)
    private float AttackProgress()
    {
        int start = tellEnd;
        for (int k = 0; k < loopEnds.Count; k++)
        {
            if (frame < loopEnds[k]) return (frame - start) / (float)Mathf.Max(1, loopEnds[k] - start);
            start = loopEnds[k];
        }
        return 1f;
    }

    // ⑥ 보스와 지휘봉이 모래로 무너진 뒤 제자리에서 원래 크기로 재생성 (보스 피격 가능)
    private void BeginCollapse()
    {
        phase = Phase.Collapse;
        SetHurtBox(false);
        int collapse = Frames(batonData.collapseSeconds);
        int rebuild = Mathf.Max(1, Frames(batonData.rebuildSeconds));
        ReleaseBaton(collapse, rebuild);
        // 몸 봇도 전부 모래로 — 제자리(앞판)로 되돌린 자세에 나눠서 다시 쌓인다
        // 외형이면 그림 부위(리그) 봇도 — 모래가 발밑 웅덩이로 돌아가면 리그가 다시 가져가 그림이 차오른다
        var swarm = Swarm;
        for (int g = 0; g < 2; g++)
        {
            swarm.AllIn(BotMode.Bound, tmp, g == 0 ? RSNanoSwarm.G_Blob : RSNanoSwarm.G_Rig);
            for (int k = 0; k < tmp.Count; k++)
                maestro.ReleaseToSand(tmp[k], new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.5f, 2.5f), 0f),
                                      collapse + rebuild * k / Mathf.Max(1, tmp.Count));
        }
        RestoreBoss();
        collapseEnd = frame + collapse + rebuild;
    }

    // ───────────────────────── 궤적 · 지휘봉 자세 ─────────────────────────

    // 리사주 — 무한대(가로) x = A·sin(θ), y = B·sin(2θ) / 8자(세로) x = A·sin(2θ), y = B·sin(θ). θ는 바깥 끝(π/2)부터
    private Vector3 CurvePoint(float u)
    {
        float th = Mathf.PI * 0.5f + u * Mathf.PI * 2f;
        Vector2 size = eight ? batonData.eightSize : batonData.infinitySize;
        float x = eight ? size.x * Mathf.Sin(2f * th) : size.x * Mathf.Sin(th);
        float y = eight ? size.y * Mathf.Sin(th) : size.y * Mathf.Sin(2f * th);
        // 무한대는 보스 쪽 끝에서 시작하도록 좌우를 맞춘다
        x *= -side;
        var p = refCenter + new Vector3(x, y, 0f);
        p.y = Mathf.Max(p.y, maestro.GroundY + 0.1f);
        return p;
    }

    // 끝은 궤적 위, 손잡이는 궤적 중심 위의 손 쪽으로 곧게 (길이 고정)
    private void PoseAt(float u, out Vector3 g, out Vector3 t)
    {
        t = CurvePoint(u);
        Vector3 hand = refCenter + Vector3.up * batonData.gripHeight;
        Vector3 dir = hand - t;
        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
        g = t + dir * batonLength;
    }

    // ───────────────────────── 판정 ─────────────────────────

    // 지휘봉(회전한 상자)에 플레이어가 겹치면 1회 피격 → 쿨다운. 회피 무적이면 통과
    private void CheckPlayerHit()
    {
        if (hitCooldownLeft > 0) return;
        Vector3 d = tip - grip;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        var size = new Vector2(d.magnitude, batonData.hitThickness);
        int count = Physics2D.OverlapBox((grip + tip) * 0.5f, size, angle, overlapFilter, overlap);
        for (int k = 0; k < count; k++)
        {
            if (!overlap[k].CompareTag("Player")) continue;
            if (ApplyDamageToPlayer(overlap[k].gameObject)) hitCooldownLeft = Frames(batonData.hitCooldown);
            return;
        }
    }

    // 플레이어 공격에 맞음 (HitDetection → IPlayerHitTarget). 보스 HP와 별개
    public void TakePlayerHit(float damage, Vector2 point)
    {
        if (!IsHittable) return;
        batonHp -= Mathf.Max(1, Mathf.RoundToInt(damage));
        var swarm = Swarm;
        if (swarm != null) foreach (int i in batonBots) if (i < swarm.Count) swarm.SetFlash(i, 0.8f);
        if (batonHp > 0) return;

        // 파괴 → 패턴 즉시 끝, 보스가 제자리로 떨어져 그로기
        broken = true;
        batonHp = 0;
        Debug.Log($"[Maestro] 지휘봉 파괴 → 그로기 {batonData.groggySeconds}초");
        SetHurtBox(false);
        ReleaseBaton(Frames(batonData.collapseSeconds));
        RestoreBoss();
        running = false;
        InterruptPattern(InterruptReason.GimmickGroggy);
        OwnerBoss.InterruptToGroggy(batonData.groggySeconds, batonData.groggyDamageMultiplier);
    }

    private void EnsureHurtBox()
    {
        if (hurtBox != null) return;
        var go = new GameObject("지휘봉 판정") { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(transform, false);
        hurtBox = go.AddComponent<BoxCollider2D>();
        hurtBox.isTrigger = true;
        hurtBox.enabled = false;
    }

    // 플레이어가 때리는 판정 — 지휘봉 막대를 따라 회전한 상자 (부모 스케일 무시하고 월드 크기)
    private void UpdateHurtBox()
    {
        if (hurtBox == null) return;
        Vector3 d = tip - grip;
        Transform t = hurtBox.transform;
        t.position = (grip + tip) * 0.5f;
        t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        Vector3 s = t.lossyScale;
        hurtBox.size = new Vector2(d.magnitude / Mathf.Max(0.0001f, Mathf.Abs(s.x)), batonData.hurtThickness / Mathf.Max(0.0001f, Mathf.Abs(s.y)));
        hurtBox.offset = Vector2.zero;
        hurtBox.enabled = IsHittable;
        Physics2D.SyncTransforms(); // 같은 틱의 플레이어 공격 판정이 새 자리로 보게
    }

    private void SetHurtBox(bool on)
    {
        if (hurtBox != null) hurtBox.enabled = on;
    }

    // ───────────────────────── 보스 · 봇 정리 ─────────────────────────

    private void SetInvincible(bool on)
    {
        if (OwnerBoss == null || OwnerBoss.Snapshot == null) return;
        if (!on && maestro != null && maestro.InTransition) return; // 악장 전환의 무적은 건드리지 않는다
        OwnerBoss.Snapshot.isInvincible = on;
    }

    private void RestoreBoss()
    {
        if (maestro == null) return;
        maestro.ResetBodyPose();
        if (maestro.Look != null) maestro.Look.Stop();
        SetInvincible(false);
    }

    // 지휘봉 봇을 모래로 → delay(+ 나눠서 spread) 뒤 몸으로 재흡수
    private void ReleaseBaton(int delay, int spread = 0)
    {
        var swarm = Swarm;
        if (swarm != null && maestro != null)
        {
            for (int k = 0; k < batonBots.Count; k++)
            {
                int i = batonBots[k];
                if (i >= swarm.Count) continue;
                var b = swarm.Bots[i];
                if (b.mode != BotMode.Bound || b.guide != RSNanoSwarm.G_TubeA) continue;
                maestro.ReleaseToSand(i, new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.5f, 2.5f), 0f),
                                      delay + (spread > 0 ? spread * k / Mathf.Max(1, batonBots.Count) : 0));
            }
            swarm.TubeA.glow = 0f;
        }
        batonBots.Clear();
    }

    // ───────────────────────── 그림 ─────────────────────────

    // 줄기 A를 논리 위치(보간)에 놓는다. 손잡이(node 0)가 굵고 끝이 가늘다
    private void LateUpdate()
    {
        var swarm = Swarm;
        if (swarm == null || !running || batonBots.Count == 0) return;
        float a = CombatClock.InterpolationAlpha;
        Vector3 g = Vector3.Lerp(prevGrip, grip, a), t = Vector3.Lerp(prevTip, tip, a);
        g.z = t.z = swarm.FrontZ;
        swarm.TubeA.SetLine(g, t);
    }

    private void EnsureTellLine()
    {
        if (tellLine != null) return;
        var go = new GameObject("지휘봉 궤적 예고선") { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(transform, false);
        tellLine = go.AddComponent<LineRenderer>();
        tellLine.useWorldSpace = true;
        tellLine.loop = true;
        tellLine.positionCount = CurveSamples;
        tellLine.material = new Material(Shader.Find("Sprites/Default"));
        tellLine.enabled = false;
    }

    private void DrawTellLine()
    {
        if (tellLine == null) return;
        tellLine.startWidth = tellLine.endWidth = batonData.tellWidth;
        tellLine.startColor = tellLine.endColor = batonData.tellColor;
        tellLine.sortingOrder = batonData.tellSortingOrder;
        for (int k = 0; k < CurveSamples; k++)
        {
            Vector3 p = CurvePoint(k / (float)CurveSamples);
            p.z = Swarm.FrontZ - 0.05f;
            tellLine.SetPosition(k, p);
        }
        tellLine.enabled = true;
    }
}
