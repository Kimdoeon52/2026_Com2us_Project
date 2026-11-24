using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using RealSteel.NanoBoss;
using UnityEngine;

/// <summary>
/// 마에스트로 「크레셴도」 (2026-11-24 — 2악장 후보, 기획서에 아직 없음. 타격 부위 오른다리 = 기획서 0.3 '2악장에서 배정').
///   ① 지휘봉을 치켜든다 (예고)
///   ② 박자마다 바닥으로 내리친다 → 발밑에 고리가 번쩍 → 모래 파도가 좌우로 바닥을 따라 밀려간다
///   ③ 파도는 작게(pp) 시작해 점점 크게(ff) — 낮은 건 점프로 넘고, 마지막 큰 파도(어려움·헬)는 회피 무적으로
///   ④ 화면 끝에 닿으면 모래로 무너져 재흡수. 지나가며 뒤로 모래를 흘린다
/// 판정: 파도 자리의 상자(폭 × 높이, 바닥부터)에 플레이어가 겹치면 — 파도마다 한 번. 회피 무적이면 통과.
/// 로직은 전투 틱(CombatClock), 그림은 LateUpdate. 수치는 MaestroWaveData(전부 TODO 임시값).
/// </summary>
public class MaestroWavePattern : BossPatternGimmickBase, IMaestroMovementGate
{
    [SerializeField] private MaestroWaveData waveData;
    [Tooltip("이 악장부터 나온다 (2 = 2악장 후보). 1로 바꾸면 1악장에서도 시험할 수 있다")]
    [Min(1)] [SerializeField] private int minMovement = 2;

    public int MinMovement => minMovement;

    private class Wave
    {
        public float x, prevX, height;
        public int dir;
        public bool hit, done;
        public float sprayAcc;
        public readonly List<int> bots = new List<int>(512);
        public readonly List<Vector2> local = new List<Vector2>(512); // 파도 중심 바닥 기준 (앞 = +x)
    }

    private static readonly List<Collider2D> overlap = new List<Collider2D>(8);
    private static readonly ContactFilter2D overlapFilter = new ContactFilter2D { useTriggers = true };
    private static readonly Color32 Sand = new Color32(60, 54, 68, 255);

    private MaestroBossController maestro;
    private MaestroWaveData.DifficultyRow row;
    private readonly List<Wave> waves = new List<Wave>(12);
    private readonly List<int> tmp = new List<int>(512);
    private readonly List<int> strikeFrames = new List<int>(8);
    private readonly List<int> gestureFrames = new List<int>(8);
    private MaestroFx fx;
    private bool running, subscribed;
    private int frame, nextStrike, nextGesture, delay, launched;
    private float gestureLen, minX, maxX;

    protected override bool UseExternalTellPhase => true;
    private RSNanoSwarm Swarm => maestro != null ? maestro.Swarm : null;
    private static int Frames(float s) => Mathf.Max(0, Mathf.RoundToInt(s / CombatClock.TICK));

    protected override void OnAttached()
    {
        maestro = OwnerBoss as MaestroBossController;
        if (maestro == null) Debug.LogError($"[MaestroWavePattern] {name}: 마에스트로 밑에 있어야 함");
        if (waveData == null) Debug.LogError($"[MaestroWavePattern] {name}: 크레셴도 수치(MaestroWaveData)가 비어 있음");
    }

    protected override void OnPreExecute()
    {
        Clear();
        var swarm = Swarm;
        if (maestro == null || waveData == null || swarm == null) return;
        row = waveData.Row(maestro.SelectedDifficulty);
        if (row == null) return;
        fx = MaestroFx.Get(maestro);
        if (!CameraBounds.TryGetVisibleX(Camera.main, swarm.FrontZ, maestro.GroundY, out minX, out maxX))
        {
            minX = maestro.BodyCenter.x - 15f;
            maxX = maestro.BodyCenter.x + 15f;
        }

        // 박자: 첫 내리치기 = 치켜든 뒤, 그다음 간격마다. 몸짓(내리치기)은 '내리치는 순간'이 박에 맞게 미리 건다
        strikeFrames.Clear();
        gestureFrames.Clear();
        int interval = Mathf.Max(1, Frames(row.interval));
        int first = Frames(waveData.windupSeconds);
        gestureLen = Mathf.Min(row.interval * 0.95f, 0.9f);
        int lead = Frames(gestureLen * 0.55f); // Strike 키: 55% 지점에서 내리침
        for (int k = 0; k < Mathf.Max(1, row.waves); k++)
        {
            int s = first + k * interval;
            strikeFrames.Add(s);
            gestureFrames.Add(Mathf.Max(0, s - lead));
        }
        nextStrike = nextGesture = 0;
        delay = Frames(waveData.launchDelay);
        launched = 0;
        frame = 0;
        running = true;
        if (maestro.Look != null) maestro.Look.FormBaton(Mathf.Min(0.5f, waveData.windupSeconds * 0.7f));
        Debug.Log($"[Maestro] 크레셴도 — 파도 {row.waves}개, 간격 {row.interval}초, 높이 {row.height.x}→{row.height.y}m ({maestro.SelectedDifficulty})");
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

        // 내리치기 몸짓 (그림) — 박보다 조금 먼저
        while (nextGesture < gestureFrames.Count && frame >= gestureFrames[nextGesture])
        {
            if (maestro.Look != null) maestro.Look.Play(MaestroGesture.Strike, gestureLen);
            nextGesture++;
        }
        // 내리치는 박 — 발밑 고리, launchDelay 뒤 파도 출발
        while (nextStrike < strikeFrames.Count && frame >= strikeFrames[nextStrike])
        {
            if (fx != null) fx.Ring(new Vector3(maestro.BodyCenter.x, maestro.GroundY, Swarm.FrontZ), 0.2f, waveData.width * 1.5f, 0.35f, MaestroFx.MoonWhite, flat: true);
            nextStrike++;
        }
        while (launched < strikeFrames.Count && frame >= strikeFrames[launched] + delay)
        {
            float u = strikeFrames.Count > 1 ? launched / (float)(strikeFrames.Count - 1) : 1f;
            float h = Mathf.Lerp(row.height.x, row.height.y, u * u); // 크레셴도 — 뒤로 갈수록 가파르게 커짐
            Spawn(-1, h);
            Spawn(1, h);
            launched++;
        }

        bool anyLeft = launched < strikeFrames.Count;
        foreach (var w in waves)
        {
            if (w.done) continue;
            Move(w);
            if (!w.done) anyLeft = true;
        }
        if (!anyLeft) running = false;
    }

    // 파도 하나 — 반쪽 사인 언덕 모양을 봇 간격으로 채움. 몸에서 빌린 봇이 발밑에서 솟는다
    private void Spawn(int dir, float height)
    {
        var swarm = Swarm;
        var w = new Wave { dir = dir, height = height, x = maestro.BodyCenter.x + dir * 0.3f };
        w.prevX = w.x;
        float pitch = Mathf.Max(0.01f, waveData.botPitch), half = waveData.width * 0.5f;
        var pts = new List<Vector2>();
        for (float lx = -half; lx <= half; lx += pitch)
        {
            float top = height * Mathf.Sin(Mathf.PI * (lx + half) / waveData.width);
            for (float ly = 0f; ly <= top; ly += pitch) pts.Add(new Vector2(lx, ly));
        }
        int want = Mathf.Min(pts.Count, Mathf.Max(0, maestro.SpareBots(0.5f)));
        swarm.Collect(RSNanoSwarm.G_Blob, new Vector3(w.x, maestro.GroundY, swarm.FrontZ), want, tmp);
        for (int k = 0; k < tmp.Count; k++)
        {
            int i = tmp[k];
            Vector2 l = pts[k];
            var p = WorldOf(w, l);
            swarm.Fire(i, p, 9999f, Vector3.zero, 9999f, RSNanoSwarm.G_Blob, snap: true, clump: 1);
            // 마루(위쪽)는 달빛 라벤더로 빛나고 몸통은 모래색
            float crest = Mathf.Clamp01(l.y / Mathf.Max(0.01f, height * Mathf.Sin(Mathf.PI * (l.x + half) / waveData.width)) * 1.3f - 0.3f);
            swarm.SetTint(i, Color32.Lerp(Sand, (Color32)MaestroFx.Lavender, crest));
            swarm.SetGlow(i, waveData.crestGlow * crest);
            w.bots.Add(i);
            w.local.Add(l);
        }
        waves.Add(w);
    }

    private void Move(Wave w)
    {
        w.prevX = w.x;
        w.x += w.dir * row.speed * CombatClock.TICK;

        // 판정 — 파도 자리 상자 (바닥부터 높이까지)
        if (!w.hit)
        {
            var size = new Vector2(waveData.width * waveData.hitWidthRatio, w.height);
            int count = Physics2D.OverlapBox(new Vector2(w.x, maestro.GroundY + w.height * 0.5f), size, 0f, overlapFilter, overlap);
            for (int k = 0; k < count; k++)
            {
                if (!overlap[k].CompareTag("Player")) continue;
                if (ApplyDamageToPlayer(overlap[k].gameObject)) w.hit = true; // 회피 무적이면 false — 계속 본다
                break;
            }
        }

        // 지나가며 뒤로 모래를 흘린다 (뒤쪽 아래 봇부터)
        w.sprayAcc += waveData.spray * CombatClock.TICK;
        int due = Frames(waveData.reabsorbDelay);
        while (w.sprayAcc >= 1f && w.bots.Count > 8)
        {
            w.sprayAcc -= 1f;
            int pick = Random.Range(0, w.bots.Count);
            if (w.local[pick].x * w.dir > 0f) continue; // 앞쪽은 남긴다
            maestro.ReleaseToSand(w.bots[pick], new Vector3(-w.dir * Random.Range(0.5f, 1.5f), Random.Range(0.5f, 1.5f), 0f), due);
            w.bots.RemoveAt(pick);
            w.local.RemoveAt(pick);
        }

        // 화면 끝 → 무너짐
        if (w.x < minX - waveData.width || w.x > maxX + waveData.width) Collapse(w);
    }

    private void Collapse(Wave w)
    {
        var swarm = Swarm;
        int due = Frames(waveData != null ? waveData.reabsorbDelay : 0.8f);
        if (swarm != null && maestro != null)
            foreach (int i in w.bots)
                if (i < swarm.Count && swarm.Bots[i].mode == BotMode.Free)
                    maestro.ReleaseToSand(i, new Vector3(w.dir * Random.Range(0.5f, 2f), Random.Range(0.5f, 2.5f), 0f), due);
        w.bots.Clear();
        w.local.Clear();
        w.done = true;
    }

    private void Clear()
    {
        foreach (var w in waves) if (!w.done) Collapse(w);
        waves.Clear();
    }

    // 파도 모양 → 월드. 앞으로 기울어(마루가 앞으로 쏠림) 밀려가는 느낌, 마루가 살짝 출렁
    private Vector3 WorldOf(Wave w, Vector2 l, float x = float.NaN, float t = 0f)
    {
        float cx = float.IsNaN(x) ? w.x : x;
        float lean = 0.35f * l.y;
        float ripple = 1f + 0.06f * Mathf.Sin(t * 14f + l.x * 9f);
        return new Vector3(cx + w.dir * (l.x + lean), maestro.GroundY + l.y * ripple, Swarm.FrontZ);
    }

    // ───────────────────────── 그림 ─────────────────────────

    private void LateUpdate()
    {
        var swarm = Swarm;
        if (swarm == null || waves.Count == 0 || !running) return;
        float alpha = CombatClock.InterpolationAlpha;
        var arr = swarm.Bots;
        float t = Time.time;
        foreach (var w in waves)
        {
            if (w.done) continue;
            float x = Mathf.Lerp(w.prevX, w.x, alpha);
            for (int k = 0; k < w.bots.Count; k++)
            {
                int i = w.bots[k];
                if (i >= arr.Length || arr[i].mode != BotMode.Free) continue;
                var p = WorldOf(w, w.local[k], x, t);
                arr[i].hold = p;
                arr[i].wait = 9999f;
                arr[i].pos = p;
                arr[i].vel = Vector3.zero;
            }
        }
    }
}
