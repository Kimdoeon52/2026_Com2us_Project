using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using RealSteel.NanoBoss;
using UnityEngine;

/// <summary>
/// 마에스트로 1악장 「달빛 커튼」 (보스3_월광_기획 1.5, 2026-11-19 3단계). 탄막형(안전지대 이동), 타격 부위 머리(패턴 데이터).
///   ① 흐름 1초  몸의 봇이 천장으로 흘러올라 달빛 띠를 만든다
///   ② 예고 1초  천장에서 틈 위치에 빛 기둥 (안전지대 미리보기)
///   ③ 공격 6초  입자가 커튼처럼 흘러내린다. 틈(안전지대)이 천천히 좌우로 이동 — 난이도별 틈 수·너비·속도·방향 전환
///   ④ 붕괴 1초  흘러내리던 커튼이 바닥에 모래로 쌓였다가 몸으로 재흡수
/// 판정 = 실제로 떨어지는 입자 줄기 (보이는 것 = 맞는 것). 1회 맞으면 hitCooldown 동안 커튼에 다시 안 맞는다. 회피 무적이면 안 맞는다.
/// 입자는 틈 밖에서만 떨어지기 시작한다 — 틈이 움직이면 이미 떨어지던 입자는 그 자리 그대로라 틈 가장자리가 살짝 늦게 따라온다.
/// 커튼은 몸의 봇을 빌려 쓰므로 몸이 bodyMinRatio 아래로 줄지 않게 커튼 봇 수를 깎는다 (기획서 '본체 최소 크기 상한').
/// 로직은 전투 틱(CombatClock), 그림은 LateUpdate (음표 공격과 같은 방식).
/// </summary>
public class MaestroCurtainPattern : BossPatternGimmickBase
{
    [Tooltip("커튼 수치 (단계 시간·난이도별 틈·낙하·판정)")]
    [SerializeField] private MaestroCurtainData curtainData;

    private enum Phase { Idle, Flow, Tell, Attack, Collapse }
    private enum StreakState { Band, Falling, Done }

    private struct Streak
    {
        public StreakState state;
        public float x, y, prevX, prevY;
        public int delay;
    }

    private MaestroBossController maestro;
    private readonly List<int> bots = new List<int>(4096);
    private Streak[] streaks = new Streak[0];
    private int streakCount, streakLen;
    private float[] gaps = new float[0];
    private readonly List<int> turnFrames = new List<int>(4);
    private readonly List<SpriteRenderer> pillars = new List<SpriteRenderer>(4);
    private readonly List<Collider2D> playerColliders = new List<Collider2D>(8);
    private static Sprite pillarSprite;
    private MaestroFx fx;
    private Vector3 headCluster; // 시트: 머리 위로 봇이 먼저 모이는 자리

    private MaestroCurtainData.DifficultyRow row;
    private Phase phase = Phase.Idle;
    private bool running, subscribed;
    private int frame, flowEnd, tellEnd, attackEnd, collapseEnd, fallFrames, hitCooldownLeft, nextRandomTurn;
    private int gapDir = 1;
    private float minX, maxX, ceiling, ground, spacing;

    protected override bool UseExternalTellPhase => true;

    private RSNanoSwarm Swarm => maestro != null ? maestro.Swarm : null;
    private static int Frames(float seconds) => Mathf.Max(0, Mathf.RoundToInt(seconds / CombatClock.TICK));

    // ───────────────────────── KKH 패턴 골격 ─────────────────────────

    protected override void OnAttached()
    {
        maestro = OwnerBoss as MaestroBossController;
        if (maestro == null) Debug.LogError($"[MaestroCurtainPattern] {name}: 마에스트로(MaestroBossController) 밑에 있어야 함");
        if (curtainData == null) Debug.LogError($"[MaestroCurtainPattern] {name}: 커튼 수치(MaestroCurtainData)가 비어 있음");
        if (patternData == null) Debug.LogWarning($"[MaestroCurtainPattern] {name}: 패턴 데이터가 비어 있음 — 데미지·부위가 KKH 기본값");
    }

    protected override void OnPreExecute()
    {
        ReleaseAll();
        var swarm = Swarm;
        if (maestro == null || curtainData == null || swarm == null) return;
        row = curtainData.Row(maestro.SelectedDifficulty);
        if (row == null) return;

        // 경기장 (화면 좌우 끝 · 천장 = 화면 위 끝)
        ground = maestro.GroundY;
        float z = swarm.FrontZ;
        if (!CameraBounds.TryGetVisibleX(Camera.main, z, ground, out minX, out maxX))
        {
            minX = maestro.BodyCenter.x - 10f;
            maxX = maestro.BodyCenter.x + 10f;
        }
        ceiling = TryCameraTop(z, out float top) ? top - curtainData.ceilingPadding : ground + curtainData.fallbackCeiling;
        if (ceiling < ground + 2f) ceiling = ground + curtainData.fallbackCeiling;
        spacing = swarm.spacing;

        // 커튼 봇 — 몸 최소 크기를 지키는 만큼만
        streakLen = Mathf.Max(1, curtainData.streakBots);
        int spare = maestro.SpareBots(curtainData.bodyMinRatio);
        int pool = Mathf.Min(curtainData.curtainBots, spare);
        streakCount = Mathf.Max(0, pool / streakLen);
        if (streakCount == 0)
        {
            Debug.LogWarning("[Maestro] 달빛 커튼 — 몸에 남은 봇이 모자라 커튼을 만들지 못함 (bodyMinRatio)");
            return;
        }
        Vector3 bodyTop = maestro.BodyCenter + Vector3.up * swarm.Blob.radius.y;
        swarm.Collect(RSNanoSwarm.G_Blob, bodyTop, streakCount * streakLen, bots);
        streakCount = bots.Count / streakLen;
        if (streaks.Length < streakCount) streaks = new Streak[streakCount];
        for (int s = 0; s < streakCount; s++)
        {
            float x = Random.Range(minX, maxX);
            float y = ceiling + Random.Range(0f, 0.3f);
            streaks[s] = new Streak { state = StreakState.Band, x = x, y = y, prevX = x, prevY = y };
            for (int k = 0; k < streakLen; k++)
            {
                int i = bots[s * streakLen + k];
                // 대기를 길게 걸어 엔진이 스스로 날리지 않게 — 자리는 LateUpdate가 정한다 (몸에서 천장으로 흘러간다)
                swarm.Fire(i, new Vector3(x, y + k * spacing, z), 9999f, Vector3.zero, 9999f, RSNanoSwarm.G_Blob, clump: 1);
                // 시트: 머리(아래 끝)가 가장 밝은 빛줄기 — 꼬리로 갈수록 옅게. 몸에서 빌린 어두운 봇을 달빛색으로
                float u = streakLen > 1 ? k / (float)(streakLen - 1) : 0f;
                swarm.SetGlow(i, curtainData.glow * Mathf.Lerp(2.6f, 0.5f, u));
                swarm.SetTint(i, Color.Lerp(MaestroFx.MoonWhite, MaestroFx.Lavender, u));
            }
        }

        // 단계 시간
        flowEnd = Frames(curtainData.flowSeconds);
        tellEnd = flowEnd + Frames(curtainData.tellSeconds);
        attackEnd = tellEnd + Frames(curtainData.attackSeconds);
        collapseEnd = attackEnd + Frames(curtainData.collapseSeconds);
        fallFrames = Mathf.Max(1, Frames((ceiling - ground) / curtainData.fallSpeed));

        PlaceGaps();
        PlanTurns();
        EnsurePillars(gaps.Length);

        frame = 0;
        hitCooldownLeft = 0;
        phase = Phase.Flow;
        running = true;
        fx = MaestroFx.Get(maestro);
        float bossH = maestro.Look != null && maestro.Look.Ready ? maestro.Look.Height : 3f;
        headCluster = maestro.BodyCenter + Vector3.up * bossH * 0.55f;
        headCluster.z = z;
        // 시트 '연출 순서' 첫 장면: 보스 발밑에 원형 바닥이 퍼진다
        if (fx != null) fx.Ring(new Vector3(maestro.BodyCenter.x, ground, z), 0.3f, bossH * 0.7f, 0.9f, MaestroFx.Lavender, flat: true);
        // ① 지휘봉(과 두 팔)을 위로 — 커튼이 끝날 때까지 들고 있다
        if (maestro.Look != null) maestro.Look.Play(MaestroGesture.RaiseBoth, attackEnd * CombatClock.TICK);
        Debug.Log($"[Maestro] 달빛 커튼 — 틈 {row.gapCount}개 · 너비 {row.gapWidth}m · 줄기 {streakCount}개({streakCount * streakLen}봇) ({maestro.SelectedDifficulty})");
    }

    protected override async UniTask ExecuteActionAsync(CancellationToken token)
    {
        if (!running) return;
        await UniTask.WaitUntil(() => !running, cancellationToken: token);
    }

    // 끝났든 끊겼든 커튼 봇은 모래로, 빛 기둥은 끈다
    protected override void CleanupEffects()
    {
        running = false;
        phase = Phase.Idle;
        ReleaseAll();
        ShowPillars(false);
    }

    // ───────────────────────── 틈 (안전지대) ─────────────────────────

    // 경기장을 틈 수만큼 나눠 각 칸 안 아무 데나. 함께 움직이며 방향은 경기장 가운데 쪽부터
    private void PlaceGaps()
    {
        int n = Mathf.Max(1, row.gapCount);
        if (gaps.Length != n) gaps = new float[n];
        float half = row.gapWidth * 0.5f;
        float lo = minX + curtainData.wallMargin + half, hi = maxX - curtainData.wallMargin - half;
        if (hi < lo) { lo = hi = (minX + maxX) * 0.5f; }
        float seg = (hi - lo) / n;
        float mean = 0f;
        for (int g = 0; g < n; g++)
        {
            gaps[g] = Random.Range(lo + seg * g, lo + seg * (g + 1));
            mean += gaps[g];
        }
        mean /= n;
        float center = (minX + maxX) * 0.5f;
        gapDir = Mathf.Abs(center - mean) < 0.01f ? (Random.value < 0.5f ? -1 : 1) : (center > mean ? 1 : -1);
    }

    private void PlanTurns()
    {
        turnFrames.Clear();
        int attackFrames = attackEnd - tellEnd;
        if (row.randomTurns)
        {
            nextRandomTurn = tellEnd + RandomTurnGap();
            return;
        }
        for (int t = 0; t < row.turns; t++)
            turnFrames.Add(tellEnd + attackFrames * (t + 1) / (row.turns + 1));
    }

    private int RandomTurnGap() => Mathf.Max(1, Frames(curtainData.randomTurnSeconds * Random.Range(0.5f, 1.5f)));

    private void MoveGaps()
    {
        if (turnFrames.Contains(frame)) gapDir = -gapDir;
        if (row.randomTurns && frame >= nextRandomTurn)
        {
            gapDir = -gapDir;
            nextRandomTurn = frame + RandomTurnGap();
        }

        float step = gapDir * row.gapSpeed * CombatClock.TICK;
        float half = row.gapWidth * 0.5f;
        float lo = minX + curtainData.wallMargin + half, hi = maxX - curtainData.wallMargin - half;
        bool bounce = false;
        for (int g = 0; g < gaps.Length; g++)
        {
            gaps[g] += step;
            if (gaps[g] < lo) { gaps[g] = lo; bounce = true; }
            if (gaps[g] > hi) { gaps[g] = hi; bounce = true; }
        }
        if (bounce) gapDir = -gapDir; // 벽에 닿으면 되돌아온다 (전환 횟수와 별개)
    }

    private bool InGap(float x)
    {
        float half = row.gapWidth * 0.5f;
        for (int g = 0; g < gaps.Length; g++)
            if (Mathf.Abs(x - gaps[g]) <= half) return true;
        return false;
    }

    private float RandomCurtainX()
    {
        for (int t = 0; t < 12; t++)
        {
            float x = Random.Range(minX, maxX);
            if (!InGap(x)) return x;
        }
        return Random.value < 0.5f ? minX : maxX;
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
        if (hitCooldownLeft > 0) hitCooldownLeft--;

        if (phase == Phase.Flow && frame >= flowEnd)
        {
            phase = Phase.Tell;
            ShowPillars(true); // ② 틈 위치에 빛 기둥
            // 시트 '연출 순서': 바닥 원형 → 기둥 — 틈 자리 바닥에 퍼지는 고리
            if (fx != null) foreach (float gx in gaps) fx.Ring(new Vector3(gx, ground, Swarm.FrontZ), 0.2f, row.gapWidth * 0.6f, 0.6f, MaestroFx.Lavender, flat: true);
        }
        else if (phase == Phase.Tell && frame >= tellEnd)
        {
            phase = Phase.Attack;
            ShowPillars(false);
            // 줄기마다 출발을 낙하 시간 안에서 흩어 커튼이 위에서부터 차오르게
            for (int s = 0; s < streakCount; s++) streaks[s].delay = Random.Range(0, fallFrames);
        }
        else if (phase == Phase.Attack && frame >= attackEnd)
        {
            phase = Phase.Collapse;
            // 아직 천장에 있던 줄기는 바로 모래로 떨어진다
            for (int s = 0; s < streakCount; s++)
                if (streaks[s].state == StreakState.Band) ReleaseStreak(s, new Vector3(0f, -1f, 0f));
        }
        else if (phase == Phase.Collapse && frame >= collapseEnd)
        {
            running = false; // 남은 봇은 CleanupEffects가 모래로
            return;
        }

        if (phase != Phase.Attack && phase != Phase.Collapse) return;
        if (phase == Phase.Attack) MoveGaps();
        TickStreaks();
        CheckHit();
    }

    private void TickStreaks()
    {
        float dy = curtainData.fallSpeed * CombatClock.TICK;
        for (int s = 0; s < streakCount; s++)
        {
            ref Streak st = ref streaks[s];
            st.prevX = st.x;
            st.prevY = st.y;
            if (st.state == StreakState.Band)
            {
                if (--st.delay > 0) continue; // 천장 띠에서 대기
                // 출발 — 틈 안이면 틈 밖으로 옮겨서 (천장 띠 안에서라 티가 거의 안 난다)
                if (InGap(st.x)) st.x = st.prevX = RandomCurtainX();
                st.state = StreakState.Falling;
                continue;
            }
            if (st.state != StreakState.Falling) continue;
            st.y -= dy;
            if (st.y > ground) continue;
            if (phase == Phase.Attack)
            {
                // 바닥에 닿은 줄기는 천장에서 다시 (봇을 돌려 쓴다 — 바닥에 쌓이는 건 ④ 붕괴에서)
                st.x = st.prevX = RandomCurtainX();
                st.y = st.prevY = ceiling + Random.Range(0f, 0.3f);
            }
            else ReleaseStreak(s, new Vector3(0f, -curtainData.fallSpeed * 0.3f, 0f));
        }
    }

    // 떨어지는 줄기 중 하나라도 플레이어 몸에 겹치면 1회 피격 → 쿨다운
    private void CheckHit()
    {
        if (hitCooldownLeft > 0) return;
        Transform player = maestro.PlayerTransform;
        if (player == null || !PlayerBounds(player, out Bounds b)) return;
        float hw = curtainData.streakHitWidth * 0.5f;
        float len = streakLen * spacing;
        for (int s = 0; s < streakCount; s++)
        {
            ref Streak st = ref streaks[s];
            if (st.state != StreakState.Falling) continue;
            if (st.x + hw < b.min.x || st.x - hw > b.max.x) continue;
            if (st.y > b.max.y || st.y + len < b.min.y) continue;
            if (ApplyDamageToPlayer(player.gameObject)) hitCooldownLeft = Frames(curtainData.hitCooldown);
            return; // 무적(회피)이면 이번 틱은 통과
        }
    }

    // 플레이어 판정 범위 — Player 태그 콜라이더들 (없으면 플레이어 밑 콜라이더 전부)
    private bool PlayerBounds(Transform player, out Bounds bounds)
    {
        bounds = default;
        player.GetComponentsInChildren(false, playerColliders);
        bool any = false;
        for (int pass = 0; pass < 2 && !any; pass++)
            foreach (var c in playerColliders)
            {
                if (c == null || !c.enabled || (pass == 0 && !c.CompareTag("Player"))) continue;
                if (!any) { bounds = c.bounds; any = true; }
                else bounds.Encapsulate(c.bounds);
            }
        return any;
    }

    // ───────────────────────── 봇 정리 ─────────────────────────

    private void ReleaseStreak(int s, Vector3 vel)
    {
        streaks[s].state = StreakState.Done;
        var swarm = Swarm;
        if (swarm == null || maestro == null) return;
        int due = Frames(curtainData != null ? curtainData.reabsorbDelay : 0.6f);
        for (int k = 0; k < streakLen; k++)
        {
            int idx = s * streakLen + k;
            if (idx >= bots.Count) break;
            int i = bots[idx];
            if (i < swarm.Count && swarm.Bots[i].mode == BotMode.Free)
                maestro.ReleaseToSand(i, vel + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0f, 0.8f), 0f), due);
        }
    }

    private void ReleaseAll()
    {
        for (int s = 0; s < streakCount; s++)
            if (streaks[s].state != StreakState.Done) ReleaseStreak(s, Vector3.down);
        streakCount = 0;
        bots.Clear();
    }

    // ───────────────────────── 그림 ─────────────────────────

    // 군집(실행 순서 200)보다 먼저 줄기 봇 자리를 정한다. 천장 띠는 자리만(몸에서 흘러온다), 떨어지는 줄기는 바로 붙인다
    private void LateUpdate()
    {
        var swarm = Swarm;
        if (swarm != null && streakCount > 0)
        {
            float a = CombatClock.InterpolationAlpha;
            float z = swarm.FrontZ;
            var arr = swarm.Bots;
            for (int s = 0; s < streakCount; s++)
            {
                Streak st = streaks[s];
                if (st.state == StreakState.Done) continue;
                bool snap = st.state == StreakState.Falling;
                float x = Mathf.Lerp(st.prevX, st.x, a), y = Mathf.Lerp(st.prevY, st.y, a);
                if (phase == Phase.Flow && frame < flowEnd / 2 && maestro != null)
                {
                    // 시트: 먼저 머리 위로 봇이 모여 빛 덩어리가 됐다가 천장으로 퍼진다
                    float hs = Mathf.Repeat(Mathf.Sin(s * 12.9898f) * 43758.5453f, 1f);
                    float ang = hs * Mathf.PI * 2f + Time.time * 3f;
                    float rad = 0.25f + 0.35f * Mathf.Repeat(hs * 7.31f, 1f);
                    Vector3 c = headCluster + new Vector3(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad * 0.6f, 0f);
                    x = c.x;
                    y = c.y;
                }
                for (int k = 0; k < streakLen; k++)
                {
                    int idx = s * streakLen + k;
                    if (idx >= bots.Count) break;
                    int i = bots[idx];
                    if (i >= arr.Length || arr[i].mode != BotMode.Free) continue;
                    var p = new Vector3(x, y + k * spacing, z);
                    arr[i].hold = p;
                    arr[i].wait = 9999f;
                    if (snap) { arr[i].pos = p; arr[i].vel = Vector3.zero; }
                }
            }
        }

        if (phase == Phase.Tell && curtainData != null)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 10f);
            for (int g = 0; g < pillars.Count && g < gaps.Length; g++)
            {
                var r = pillars[g];
                // 부드러운 빛기둥 (가운데 밝고 양옆·위아래로 옅게)
                r.transform.position = new Vector3(gaps[g], (ground + ceiling) * 0.5f, swarm != null ? swarm.FrontZ + 0.05f : 0f);
                r.transform.localScale = new Vector3(row.gapWidth * 1.3f, ceiling - ground, 1f);
                Color c = curtainData.pillarColor;
                c.a *= pulse;
                r.color = c;
            }
        }
    }

    private void EnsurePillars(int n)
    {
        if (pillarSprite == null) pillarSprite = MaestroFx.Beam;
        while (pillars.Count < n)
        {
            var go = new GameObject("달빛 커튼 빛 기둥") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = pillarSprite;
            r.sortingOrder = curtainData != null ? curtainData.pillarSortingOrder : 3;
            r.enabled = false;
            pillars.Add(r);
        }
    }

    private void ShowPillars(bool on)
    {
        for (int g = 0; g < pillars.Count; g++)
            if (pillars[g] != null) pillars[g].enabled = on && g < gaps.Length;
    }

    // 화면 위 끝이 경기장 평면(z)에 닿는 높이
    private static bool TryCameraTop(float planeZ, out float y)
    {
        y = 0f;
        var cam = Camera.main;
        if (cam == null) return false;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 1f, 0f));
        if (Mathf.Abs(ray.direction.z) < 1e-6f) return false;
        float t = (planeZ - ray.origin.z) / ray.direction.z;
        if (t <= 0f) return false;
        y = ray.origin.y + ray.direction.y * t;
        return true;
    }
}
