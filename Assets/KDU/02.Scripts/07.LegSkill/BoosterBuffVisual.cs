using System;
using UnityEngine;

// 부스터 연출 설정
[Serializable]
public struct BoosterVisualSettings
{
    [Tooltip("발광·잔상 머티리얼")]
    public Material glowMaterial;

    [Tooltip("몸 발광 색")]
    public Color glowColor;

    [Tooltip("발광 깜빡임 속도")]
    [Min(0f)] public float glowPulseSpeed;

    [Tooltip("발광 최저 밝기 비율")]
    [Range(0f, 1f)] public float glowPulseMin;

    [Tooltip("잔상 색")]
    public Color afterImageColor;

    [Tooltip("잔상 간격(틱)")]
    [Min(1)] public int afterImageIntervalFrames;

    [Tooltip("잔상 수명(초)")]
    [Min(0.01f)] public float afterImageLifetime;
}

// 부스터 지속 동안 몸 발광 + 이동 잔상
public class BoosterBuffVisual : MonoBehaviour
{
    private const float MoveEpsilonSqr = 0.000001f;

    private sealed class Ghost
    {
        public SpriteRenderer renderer;
        public float remaining;
    }

    private BoosterVisualSettings settings;
    private SpriteRenderer source;
    private SpriteRenderer glow;
    private Ghost[] ghosts = Array.Empty<Ghost>();
    private int nextGhost;
    private int remainingTicks;
    private int elapsedTicks;
    private Vector3 lastSnapPosition;
    private bool subscribed;

    // 연출 시작. 다시 걸면 지속시간 덮어씀
    public void Play(in BoosterVisualSettings visual, int durationTicks)
    {
        if (source == null)
            source = RobotView.FindRenderer(this);
        if (source == null || visual.glowMaterial == null || durationTicks <= 0)
            return;

        settings = visual;
        remainingTicks = durationTicks;
        elapsedTicks = 0;
        lastSnapPosition = source.transform.position;

        EnsureGlow();
        EnsurePool();
        glow.enabled = true;
        Subscribe();
    }

    private void EnsureGlow()
    {
        if (glow == null)
        {
            var go = new GameObject("BoosterGlow");
            go.transform.SetParent(source.transform, false);
            glow = go.AddComponent<SpriteRenderer>();
        }
        glow.sharedMaterial = settings.glowMaterial;
    }

    // 수명 ÷ 간격만큼 미리 생성
    private void EnsurePool()
    {
        float interval = Mathf.Max(1, settings.afterImageIntervalFrames) * CombatClock.TICK;
        int size = Mathf.CeilToInt(settings.afterImageLifetime / interval) + 1;
        if (ghosts.Length >= size)
            return;

        var grown = new Ghost[size];
        Array.Copy(ghosts, grown, ghosts.Length);
        for (int i = ghosts.Length; i < size; i++)
        {
            var go = new GameObject($"BoosterGhost_{name}_{i}");
            var r = go.AddComponent<SpriteRenderer>();
            r.enabled = false;
            grown[i] = new Ghost { renderer = r };
        }
        ghosts = grown;
    }

    private void Subscribe()
    {
        if (subscribed)
            return;
        CombatClock.Instance.OnCombatTick += OnTick;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;
        var clock = CombatClock.Existing;
        if (clock != null)
            clock.OnCombatTick -= OnTick;
        subscribed = false;
    }

    // 지속 틱 차감, 이동 중이면 간격마다 잔상
    private void OnTick()
    {
        if (remainingTicks <= 0)
        {
            Stop();
            return;
        }

        remainingTicks--;
        elapsedTicks++;
        if (elapsedTicks % Mathf.Max(1, settings.afterImageIntervalFrames) != 0)
            return;

        Vector3 pos = source.transform.position;
        if ((pos - lastSnapPosition).sqrMagnitude < MoveEpsilonSqr)
            return;

        lastSnapPosition = pos;
        Snapshot();
    }

    private void Stop()
    {
        if (glow != null)
            glow.enabled = false;
        Unsubscribe();
    }

    // 현재 그림을 잔상 하나에 복사
    private void Snapshot()
    {
        if (source.sprite == null || ghosts.Length == 0)
            return;

        Ghost ghost = ghosts[nextGhost];
        nextGhost = (nextGhost + 1) % ghosts.Length;

        Transform t = ghost.renderer.transform;
        t.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        t.localScale = source.transform.lossyScale;

        SpriteRenderer r = ghost.renderer;
        r.sprite = source.sprite;
        r.flipX = source.flipX;
        r.flipY = source.flipY;
        r.sharedMaterial = settings.glowMaterial;
        r.sortingLayerID = source.sortingLayerID;
        r.sortingOrder = source.sortingOrder - 1;
        ghost.remaining = settings.afterImageLifetime;
        ApplyGhostAlpha(ghost);
        r.enabled = true;
    }

    private void Update()
    {
        foreach (var ghost in ghosts)
        {
            if (ghost.remaining <= 0f)
                continue;

            ghost.remaining -= Time.deltaTime;
            if (ghost.remaining <= 0f)
            {
                ghost.renderer.enabled = false;
                continue;
            }
            ApplyGhostAlpha(ghost);
        }
    }

    // 몸 그림 따라가기 + 깜빡임
    private void LateUpdate()
    {
        if (glow == null || !glow.enabled || source == null)
            return;

        glow.sprite = source.sprite;
        glow.flipX = source.flipX;
        glow.flipY = source.flipY;
        glow.sortingLayerID = source.sortingLayerID;
        glow.sortingOrder = source.sortingOrder + 1;

        float wave = (Mathf.Sin(Time.time * settings.glowPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        Color c = settings.glowColor;
        c.a *= Mathf.Lerp(settings.glowPulseMin, 1f, wave);
        glow.color = c;
    }

    private void ApplyGhostAlpha(Ghost ghost)
    {
        Color c = settings.afterImageColor;
        c.a *= ghost.remaining / settings.afterImageLifetime;
        ghost.renderer.color = c;
    }

    private void OnDisable()
    {
        Stop();
    }

    // 잔상은 로봇 자식이 아니라 직접 제거
    private void OnDestroy()
    {
        Unsubscribe();
        foreach (var ghost in ghosts)
        {
            if (ghost.renderer != null)
                Destroy(ghost.renderer.gameObject);
        }
    }
}
