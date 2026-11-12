using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using UnityEngine;

/// <summary>
/// 쿄다이 촉수 생성기 (2026-11-04 해킹 대체 기믹). 전투 내내 패턴과 따로 촉수를 하나씩 늘린다(최대 10개).
///
///   · 생성: 간격마다 하나(2페이즈는 더 빠르게). 플레이어 바로 옆·다른 촉수 바로 옆은 피한다
///   · 디버프: 살아 있는 촉수가 3개부터 플레이어 이동속도 감소(RobotMover.SetSpeedModifier). 촉수를 부수면 줄어든다
///     방어력 감소(기획)는 KKH 스탯이라 협의 전까지 미적용
///   · 휘두르기 데미지: KKH 방식(회피 무적 검사 + 이 오브젝트 패턴 데이터의 데미지)으로 내려고 BossPatternGimmickBase 상속 —
///     컨트롤러가 등록 직후 패턴 목록에서 뺀다(IKyodaiHazard)
/// 촉수 원본(template)은 꺼둔 자식 — 복제해서 쓴다.
/// </summary>
public class KyodaiTentacleSpawner : BossPatternGimmickBase, IKyodaiHazard
{
    private const string SpeedModifierKey = "Kyodai.촉수";

    [SerializeField] private KyodaiTentacleData data;

    [Tooltip("촉수 원본 (꺼둔 자식) — 복제해서 경기장에 세운다")]
    [SerializeField] private KyodaiTentacle template;

    private readonly List<KyodaiTentacle> live = new List<KyodaiTentacle>();
    private KyodaiBossController kyodai;
    private bool running, paused, phase2;
    private int timer;
    private RobotMover playerMover;
    private float appliedMultiplier = 1f;
    private bool warnedDefense;

    /// <summary>살아 있는(디버프에 들어가는) 촉수 수</summary>
    public int Count
    {
        get
        {
            int n = 0;
            foreach (var t in live) if (t != null && t.Counts) n++;
            return n;
        }
    }

    /// <summary>최대 수 — 쿄다이 난이도 데이터가 있으면 그 난이도 값 (2026-11-12)</summary>
    public int MaxCount => data == null ? 0 : kyodai != null ? kyodai.TentacleMax(data.maxCount) : data.maxCount;
    public float SpeedMultiplier => appliedMultiplier;
    public float Size => kyodai != null ? kyodai.Size : 1f;
    public float PlayerX => kyodai != null ? kyodai.PlayerX : transform.position.x;

    protected override void OnAttached()
    {
        kyodai = OwnerBoss as KyodaiBossController;
        if (kyodai == null) Debug.LogError("[KyodaiTentacleSpawner] KyodaiBossController(2D) 밑에 있어야 함");
        if (data == null) Debug.LogWarning($"[KyodaiTentacleSpawner] {name}: 촉수 수치가 비어 있음");
        if (template == null) Debug.LogWarning($"[KyodaiTentacleSpawner] {name}: 촉수 원본이 비어 있음");
        else template.gameObject.SetActive(false);
    }

    /// <summary>생성 시작/정지. 시작하면 첫 촉수까지 firstSpawnFrames</summary>
    public void SetRunning(bool value)
    {
        if (value == running) return;
        running = value;
        if (running) timer = data != null ? data.firstSpawnFrames : 600;
    }

    /// <summary>2페이즈 — 생성 간격이 짧아진다(남은 대기도 새 간격을 넘지 않게)</summary>
    public void SetPhase2(bool value)
    {
        phase2 = value;
        if (phase2 && data != null) timer = Mathf.Min(timer, data.phase2SpawnIntervalFrames);
    }

    /// <summary>그로기 동안: 새로 안 만들고, 촉수도 안 휘두른다</summary>
    public void SetPaused(bool value)
    {
        paused = value;
        foreach (var t in live) if (t != null) t.SetPaused(value);
    }

    /// <summary>격파 — 전부 쓰러지고 더 안 만든다</summary>
    public void KillAll()
    {
        running = false;
        foreach (var t in live) if (t != null) t.Kill();
    }

    /// <summary>촉수 휘두르기가 플레이어에 닿음 — 회피 무적이면 false</summary>
    public bool DamagePlayer(GameObject player) => ApplyDamageToPlayer(player);

    // ───────────────────────── 전투 틱 ─────────────────────────

    /// <summary>호출: KyodaiBossController(CombatClock.OnCombatTick)</summary>
    public void Tick()
    {
        if (kyodai == null || data == null) return;

        for (int i = live.Count - 1; i >= 0; i--)
        {
            var t = live[i];
            if (t == null) { live.RemoveAt(i); continue; }
            t.Tick();
            if (!t.IsDead) continue;
            live.RemoveAt(i);
            Destroy(t.gameObject);
        }

        if (running && !paused && --timer <= 0)
        {
            if (Count < MaxCount) Spawn();
            timer = phase2 ? data.phase2SpawnIntervalFrames : data.spawnIntervalFrames;
        }
        UpdateDebuff();
    }

    private void Spawn()
    {
        if (template == null) return;
        float x = PickX();
        var t = Instantiate(template, template.transform.parent);
        t.name = $"촉수 {live.Count + 1}";
        t.gameObject.SetActive(true);
        t.transform.position = new Vector3(x, kyodai.GroundY, template.transform.position.z);
        t.Init(this, data, Random.value * 10f);
        if (paused) t.SetPaused(true);
        live.Add(t);
    }

    // 후보 몇 개 중 다른 촉수와 가장 먼 자리. 플레이어 바로 옆은 되도록 피한다
    private float PickX()
    {
        float s = Size;
        float min = kyodai.ArenaMinX + 0.5f * s, max = kyodai.ArenaMaxX - 0.5f * s;
        float best = (min + max) * 0.5f, bestScore = float.MinValue;
        for (int i = 0; i < 12; i++)
        {
            float x = Random.Range(min, max);
            float nearest = float.MaxValue;
            foreach (var t in live) if (t != null && t.Counts) nearest = Mathf.Min(nearest, Mathf.Abs(t.transform.position.x - x));
            float score = Mathf.Min(nearest, data.minSpacing * s * 3f);
            if (Mathf.Abs(x - PlayerX) < data.minPlayerDistance * s) score -= 1000f; // 플레이어 옆은 마지막 수단
            if (score > bestScore) { bestScore = score; best = x; }
        }
        return best;
    }

    // 3개부터 하나마다 이동속도 감소. 바뀔 때만 건다
    private void UpdateDebuff()
    {
        int n = Count;
        float mult = n >= data.debuffStartCount
            ? Mathf.Max(data.minSpeedMultiplier, 1f - data.speedPenaltyPerTentacle * (n - data.debuffStartCount + 1))
            : 1f;
        if (Mathf.Approximately(mult, appliedMultiplier)) return;

        if (playerMover == null && kyodai.PlayerTransform != null)
        {
            playerMover = kyodai.PlayerTransform.GetComponentInParent<RobotMover>();
            if (playerMover == null) playerMover = kyodai.PlayerTransform.GetComponentInChildren<RobotMover>();
        }
        if (playerMover == null) return;
        playerMover.SetSpeedModifier(SpeedModifierKey, mult);
        appliedMultiplier = mult;

        if (mult < 1f && !warnedDefense)
        {
            warnedDefense = true;
            Debug.Log("[Kyodai] 촉수 디버프 — 이동속도만 적용. 방어력 감소는 KKH 스탯 협의 전이라 미적용");
        }
    }

    private void OnDisable()
    {
        if (playerMover != null) playerMover.ClearSpeedModifier(SpeedModifierKey);
        appliedMultiplier = 1f;
    }

    // 패턴 순서에는 안 들어간다(컨트롤러가 뺀다) — 불려도 바로 끝
    protected override UniTask ExecuteActionAsync(CancellationToken token) => UniTask.CompletedTask;
}
