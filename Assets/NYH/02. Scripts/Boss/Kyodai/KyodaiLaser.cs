using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 쿄다이 본체 눈의 레이저 (2026-11-04 2D 개편). 플레이어를 노리고 쏘고, 손에 맞으면 그 손을 정지시킨다(KyodaiHand.Freeze).
/// 플레이어가 레이저를 손 쪽으로 유도하는 기믹 — 냉각탑 유도 파괴를 대체한다.
///
/// 단계: 조준(조준점이 플레이어를 늦게 따라가고 선이 점점 가늘어짐 = 예고) → 고정(깜빡임, 피할 틈) → 발사(판정) → 사라짐.
/// 로직은 전투 틱(KyodaiBossController가 손 다음에 부른다 — 손 위치가 최신일 때 맞는지 본다), 그림은 Update(UpdateVisual).
/// 이 오브젝트 위치 = 눈(경기장 평면 위 — 뒤에 있는 본체 렌즈가 화면에서 겹쳐 보이는 자리).
///
/// 쓸기 모드 (2026-11-04, KyodaiLaserData.sweepLength > 0): 조준점이 플레이어 뒤쪽 바닥을 따라오다 고정 → 레이저가 바닥을
/// 가로로 쓸고 지나감(그을음이 남음) → 쓸고 난 뒤 explosionDelayFrames(사용자: 약 1.2초) 지나서 쓸고 간 방향을 따라
/// 그을음 자리에서 천천히 하나씩 폭발. 쓸고 가다 손에 막히면 그 손은 떨어지고, 막힌 동안은 바닥에 그을음이 안 남는다.
/// </summary>
public class KyodaiLaser : MonoBehaviour
{
    private enum Phase { Idle, Aim, Lock, Fire, Fade, Burn }

    [Tooltip("그림 순서 — 손(5)보다 앞")]
    [SerializeField] private int sortingOrder = 8;

    private static Sprite lineSprite, dotSprite;
    private static readonly List<Collider2D> overlap = new List<Collider2D>(8);
    private static readonly ContactFilter2D overlapFilter = new ContactFilter2D { useTriggers = true };

    private KyodaiBossController owner;
    private KyodaiLaserData data;
    private Func<GameObject, bool> onTouch;
    private Phase phase = Phase.Idle;
    private int frame;
    private Vector2 aim, aimVel;
    private Vector2 dir = Vector2.down;
    private Vector2 end;
    private bool touched;
    private readonly HashSet<KyodaiHand> frozenThisShot = new HashSet<KyodaiHand>();

    private SpriteRenderer aimLine, beam, core, flash, scorch;

    // 쓸기 모드
    private class Explosion { public float x; public int trigger; public bool hit; }
    private int sweepDir = 1;
    private float sweepStartX, sweepEndX;
    private readonly List<float> burnPoints = new List<float>();     // 바닥에 그을음이 남은 자리 (쓸고 간 순서)
    private readonly List<Explosion> explosions = new List<Explosion>();
    private readonly List<SpriteRenderer> explosionRenderers = new List<SpriteRenderer>();
    private int lastExplodedIndex = -1;

    private bool Sweep => data != null && data.sweepLength > 0f;

    public bool IsActive => phase != Phase.Idle;

    // 보스 크기(루트 스케일) — 굵기 값은 보스 단위라 쓸 때 곱한다. 조준 높이(플레이어 가슴)·최대 길이는 월드 그대로
    private float S => owner != null ? owner.Size : 1f;
    // 루트 기준 상대 자리 + 고정 자리(Anchor) — KKH가 루트를 프레임 중간에 옮겨도 눈 위치가 흔들리지 않게
    public Vector2 Eye => owner != null ? (Vector2)(owner.Anchor + (transform.position - owner.transform.position)) : (Vector2)transform.position;
    public Vector2 BeamEnd => end;

    /// <summary>손 하나가 레이저에 맞아 정지할 때 (소리·이펙트용)</summary>
    public event Action<KyodaiHand> OnHandFrozen;

    public void Init(KyodaiBossController boss)
    {
        owner = boss;
        Stop();
    }

    /// <summary>레이저 한 발. touch: 발사 중 플레이어에 닿을 때 — true를 돌려주면 그 발에선 더 안 맞는다</summary>
    public void Play(KyodaiLaserData laserData, Func<GameObject, bool> touch)
    {
        if (laserData == null || owner == null) return;
        data = laserData;
        onTouch = touch;
        touched = false;
        frozenThisShot.Clear();
        aimVel = Vector2.zero;
        burnPoints.Clear();
        ClearExplosions();
        if (Sweep)
        {
            // 플레이어 쪽에서 경기장 가운데 방향으로 쓴다 — 벽에 붙어 있어도 쓸 자리가 남게
            sweepDir = owner.PlayerX < owner.ArenaCenterX ? 1 : -1;
            aim = SweepStartGoal();
        }
        else aim = PlayerAimPoint();
        UpdateDirection();
        phase = Phase.Aim;
        frame = 0;
    }

    public void Stop()
    {
        phase = Phase.Idle;
        frame = 0;
        onTouch = null;
        SetVisible(false);
        ClearExplosions();
        burnPoints.Clear();
        if (scorch != null) scorch.enabled = false;
    }

    // ───────────────────────── 전투 틱 ─────────────────────────

    /// <summary>호출: KyodaiBossController(CombatClock.OnCombatTick), 손 Tick 다음</summary>
    public void Tick()
    {
        if (phase == Phase.Idle || data == null) return;
        frame++;
        switch (phase)
        {
            case Phase.Aim:
                if (Sweep) FollowGoal(SweepStartGoal());
                else FollowPlayer();
                UpdateDirection();
                end = TraceEnd(out _);
                if (frame >= data.aimFrames)
                {
                    if (Sweep) LockSweep();
                    Next(data.lockFrames > 0 ? Phase.Lock : Phase.Fire);
                }
                break;
            case Phase.Lock:
                end = TraceEnd(out _);
                if (frame >= data.lockFrames) Next(Phase.Fire);
                break;
            case Phase.Fire:
                if (frame == 1 && Application.isPlaying) KyodaiCameraShake.Shake(data.fireShake, 0.2f);
                if (Sweep)
                {
                    // 바닥을 따라 시작 → 끝으로 (천천히 출발해 천천히 멈춤)
                    float k = Mathf.SmoothStep(0f, 1f, frame / (float)Mathf.Max(1, data.sweepFrames));
                    aim = new Vector2(Mathf.Lerp(sweepStartX, sweepEndX, k), owner.GroundY);
                    UpdateDirection();
                    KyodaiHand blocker = FireTick();
                    if (blocker == null || !data.blockedByHand) RecordBurn(end.x);
                    if (frame >= data.sweepFrames)
                    {
                        BuildExplosions();
                        Next(Phase.Burn);
                    }
                }
                else
                {
                    FireTick();
                    if (frame >= data.fireFrames) Next(data.fadeFrames > 0 ? Phase.Fade : Phase.Idle);
                }
                break;
            case Phase.Fade:
                if (frame >= data.fadeFrames) Next(Phase.Idle);
                break;
            case Phase.Burn:
                if (TickExplosions()) Next(Phase.Idle);
                break;
        }
    }

    private void Next(Phase next)
    {
        phase = next;
        frame = 0;
        if (next == Phase.Idle) Stop();
    }

    private Vector2 PlayerAimPoint() => new Vector2(owner.PlayerX, owner.GroundY + data.aimHeight);

    private void FollowPlayer() => FollowGoal(PlayerAimPoint());

    // 쓸기 시작점: 플레이어보다 조금 뒤쪽 바닥 (쓸고 가면서 플레이어 자리를 지나간다)
    private Vector2 SweepStartGoal() =>
        new Vector2(owner.ClampToArena(owner.PlayerX - sweepDir * data.sweepLength * data.sweepStartBehind), owner.GroundY);

    private void LockSweep()
    {
        sweepStartX = owner.ClampToArena(aim.x);
        sweepEndX = owner.ClampToArena(sweepStartX + sweepDir * data.sweepLength);
    }

    // 그을음 자리 기록 — explosionSpacing 간격마다 하나
    private void RecordBurn(float x)
    {
        if (burnPoints.Count > 0 && Mathf.Abs(x - burnPoints[burnPoints.Count - 1]) < data.explosionSpacing) return;
        burnPoints.Add(x);
    }

    private void BuildExplosions()
    {
        ClearExplosions();
        for (int i = 0; i < burnPoints.Count; i++)
            explosions.Add(new Explosion { x = burnPoints[i], trigger = data.explosionDelayFrames + i * data.explosionIntervalFrames });
        lastExplodedIndex = -1;
    }

    // 폭발마다 터지는 순간부터 explosionHitFrames 동안 판정(폭발마다 한 번). 다 터지고 그림까지 끝나면 true
    private bool TickExplosions()
    {
        Vector2 size = new Vector2(data.explosionSize.x, data.explosionSize.y) * S;
        int lastEnd = 0;
        for (int i = 0; i < explosions.Count; i++)
        {
            var e = explosions[i];
            lastEnd = Mathf.Max(lastEnd, e.trigger + Mathf.Max(data.explosionHitFrames, data.explosionVisualFrames));
            if (frame == e.trigger)
            {
                lastExplodedIndex = i;
                if (Application.isPlaying) KyodaiCameraShake.Shake(data.explosionShake, 0.15f);
            }
            if (frame < e.trigger || frame >= e.trigger + data.explosionHitFrames || e.hit || onTouch == null) continue;
            int count = Physics2D.OverlapBox(new Vector2(e.x, owner.GroundY + size.y * 0.5f), size, 0f, overlapFilter, overlap);
            for (int k = 0; k < count; k++)
            {
                if (!overlap[k].CompareTag("Player")) continue;
                if (onTouch(overlap[k].gameObject)) e.hit = true; // 회피 무적이면 false — 판정 동안 계속 본다
                break;
            }
        }
        return frame >= lastEnd;
    }

    private void ClearExplosions()
    {
        foreach (var r in explosionRenderers) if (r != null) r.enabled = false;
        explosions.Clear();
        lastExplodedIndex = -1;
    }

    private void FollowGoal(Vector2 goal)
    {
        float w = 2f * Mathf.PI * data.aimFrequency;
        aimVel += (w * w * (goal - aim) - 2f * data.aimDamping * w * aimVel) * CombatClock.TICK;
        aim += aimVel * CombatClock.TICK;
    }

    private void UpdateDirection()
    {
        Vector2 d = aim - Eye;
        if (d.sqrMagnitude > 0.0001f) dir = d.normalized;
    }

    // 반환: 이번 틱에 길을 막은 손 (없으면 null)
    private KyodaiHand FireTick()
    {
        end = TraceEnd(out KyodaiHand blocker);

        // 손에 맞음 → 정지 (한 발에 손마다 한 번)
        if (blocker != null && frozenThisShot.Add(blocker))
        {
            blocker.Freeze(data.handFreezeFrames);
            OnHandFrozen?.Invoke(blocker);
        }

        if (touched || onTouch == null) return blocker;
        Vector2 center = (Eye + end) * 0.5f;
        float length = Vector2.Distance(Eye, end);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        int count = Physics2D.OverlapBox(center, new Vector2(length, data.beamWidth * S), angle, overlapFilter, overlap);
        for (int i = 0; i < count; i++)
        {
            if (!overlap[i].CompareTag("Player")) continue;
            if (onTouch(overlap[i].gameObject)) touched = true; // 회피 무적이면 false — 발사 동안 계속 본다
            return blocker;
        }
        return blocker;
    }

    // 눈에서 dir로: 바닥에 닿는 곳까지(최대 길이). 손이 길을 막으면 손에서 끝난다(blockedByHand).
    // 손은 맞는 자리 상자에 레이저 굵기 절반만큼 여유를 둔다. 조준선도 같은 계산 — 손에 막히는 게 미리 보여야 유도할 수 있다
    private Vector2 TraceEnd(out KyodaiHand blocker)
    {
        blocker = null;
        float maxT = data.maxLength;
        if (dir.y < -0.0001f) maxT = Mathf.Min(maxT, (owner.GroundY - Eye.y) / dir.y);

        float best = maxT;
        foreach (var hand in owner.Hands)
        {
            if (hand == null || hand.HurtZone == null || !hand.HurtZone.Vulnerable) continue;
            var box = hand.HurtZone.Box;
            Vector3 ls = hand.HurtZone.transform.lossyScale; // 콜라이더 크기는 로컬 — 보스 크기만큼 줄어 있다
            Vector2 worldSize = new Vector2(box.size.x * Mathf.Abs(ls.x), box.size.y * Mathf.Abs(ls.y));
            Vector2 c = (Vector2)hand.HurtZone.transform.position + new Vector2(box.offset.x * ls.x, box.offset.y * ls.y);
            Vector2 half = worldSize * 0.5f + Vector2.one * (data.beamWidth * S * 0.5f);
            if (RayBox(Eye, dir, c - half, c + half, out float t) && t < best)
            {
                // 막지 않는 설정이어도 맞은 손은 정지한다 — 가장 가까운 손 하나
                blocker = hand;
                best = t;
            }
        }
        float stop = data.blockedByHand || blocker == null ? best : maxT;
        return Eye + dir * Mathf.Max(0f, stop);
    }

    // 반직선과 축 정렬 상자의 첫 교차 거리 (슬랩 방식)
    private static bool RayBox(Vector2 o, Vector2 d, Vector2 min, Vector2 max, out float t)
    {
        float tMin = 0f, tMax = float.MaxValue;
        for (int i = 0; i < 2; i++)
        {
            if (Mathf.Abs(d[i]) < 1e-6f)
            {
                if (o[i] < min[i] || o[i] > max[i]) { t = 0f; return false; }
                continue;
            }
            float a = (min[i] - o[i]) / d[i], b = (max[i] - o[i]) / d[i];
            if (a > b) (a, b) = (b, a);
            tMin = Mathf.Max(tMin, a);
            tMax = Mathf.Min(tMax, b);
            if (tMin > tMax) { t = 0f; return false; }
        }
        t = tMin;
        return true;
    }

    // ───────────────────────── 그림 ─────────────────────────

    private void Update()
    {
        if (Application.isPlaying) UpdateVisual(Time.time);
    }

    /// <summary>그림 갱신. 플레이 중엔 Update, 에디터 미리보기에선 창이 부른다</summary>
    public void UpdateVisual(float time)
    {
        EnsureRenderers();
        if (phase == Phase.Idle || data == null) { SetVisible(false); if (scorch != null) scorch.enabled = false; return; }
        if (Sweep) DrawSweepExtras(time);

        float length = Vector2.Distance(Eye, end);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        SetVisible(false);

        switch (phase)
        {
            case Phase.Aim:
            {
                // 좁아지는 조준선 — 끝으로 갈수록 빨리 가늘어진다
                float k = Mathf.Clamp01(frame / (float)data.aimFrames);
                float width = Mathf.Lerp(data.aimStartWidth, data.aimEndWidth, k * k) * S;
                Color c = data.aimColor;
                c.a *= Mathf.Lerp(0.5f, 1f, k) * (0.85f + 0.15f * Mathf.Sin(time * 30f));
                Line(aimLine, length, width, angle, c);
                break;
            }
            case Phase.Lock:
            {
                // 깜빡임 4프레임 간격
                if ((frame / 4) % 2 == 0)
                {
                    Color c = data.aimColor;
                    c.a = 1f;
                    Line(aimLine, length, Mathf.Max(data.aimEndWidth, 0.08f) * S, angle, c);
                }
                break;
            }
            case Phase.Fire:
            case Phase.Fade:
            case Phase.Burn:
            {
                // 쓸기 모드는 Burn 처음 fadeFrames 동안 레이저가 가늘어지며 사라지고, 그 뒤엔 그을음·폭발만
                if (phase == Phase.Burn && frame >= data.fadeFrames) break;
                float k = phase == Phase.Fire ? 1f : 1f - Mathf.Clamp01(frame / (float)Mathf.Max(1, data.fadeFrames));
                // 첫 몇 프레임은 살짝 굵게 터졌다가 자리 잡는다 + 미세한 떨림
                float burst = phase == Phase.Fire ? Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(frame / 4f)) : 1f;
                float jitter = 1f + 0.08f * Mathf.Sin(time * 70f);
                float width = data.beamWidth * S * k * burst * jitter;
                Line(beam, length, width, angle, data.beamColor);
                Line(core, length, width * 0.4f, angle, data.coreColor);
                float f = width * 1.6f;
                flash.enabled = true;
                flash.transform.position = new Vector3(end.x, end.y, transform.position.z);
                flash.transform.rotation = Quaternion.Euler(0f, 0f, 45f + time * 360f);
                float fl = LocalScaleDivisor();
                flash.transform.localScale = new Vector3(f / fl, f / fl, 1f);
                flash.color = data.coreColor;
                break;
            }
        }
    }

    // 쓸기 모드: 바닥 그을음(아직 안 터진 구간 — 폭발 차례가 다가올수록 밝게 깜빡) + 폭발(커지며 사라지는 섬광)
    private void DrawSweepExtras(float time)
    {
        float d = LocalScaleDivisor();
        int from = phase == Phase.Burn ? lastExplodedIndex + 1 : 0;
        bool hasScorch = burnPoints.Count > 0 && from < burnPoints.Count && (phase == Phase.Fire || phase == Phase.Burn);
        scorch.enabled = hasScorch;
        if (hasScorch)
        {
            float a = burnPoints[from];
            float b = phase == Phase.Fire ? end.x : burnPoints[burnPoints.Count - 1];
            float left = Mathf.Min(a, b), len = Mathf.Abs(b - a) + data.explosionSpacing * 0.5f;
            left -= data.explosionSpacing * 0.25f;
            float soon = phase == Phase.Burn ? Mathf.Clamp01(frame / (float)Mathf.Max(1, data.explosionDelayFrames)) : 0f;
            Color c = Color.Lerp(data.scorchColor, data.explosionColor, soon * (0.5f + 0.5f * Mathf.Sin(time * (10f + 20f * soon))));
            scorch.transform.position = new Vector3(left, owner.GroundY + 0.03f, transform.position.z);
            scorch.transform.rotation = Quaternion.identity;
            scorch.transform.localScale = new Vector3(len / d, data.scorchThickness * S / d, 1f);
            scorch.color = c;
        }

        if (phase != Phase.Burn) return;
        while (explosionRenderers.Count < explosions.Count)
            explosionRenderers.Add(MakeRenderer("바닥 폭발", sortingOrder + 1, dotSprite));
        for (int i = 0; i < explosions.Count; i++)
        {
            var e = explosions[i];
            var r = explosionRenderers[i];
            int age = frame - e.trigger;
            bool show = age >= 0 && age < data.explosionVisualFrames;
            r.enabled = show;
            if (!show) continue;
            float t = age / (float)Mathf.Max(1, data.explosionVisualFrames);
            float sz = Mathf.Lerp(0.4f, 1.2f, 1f - (1f - t) * (1f - t)) * data.explosionSize.x * S;
            r.transform.position = new Vector3(e.x, owner.GroundY + sz * 0.45f, transform.position.z);
            r.transform.rotation = Quaternion.Euler(0f, 0f, 45f + age * 6f);
            r.transform.localScale = new Vector3(sz / d, sz / d, 1f);
            Color c = Color.Lerp(data.coreColor, data.explosionColor, Mathf.Clamp01(t * 2f));
            c.a = 1f - t * t;
            r.color = c;
        }
    }

    // 길이·굵기는 월드 값 — 눈이 줄어든 보스 루트 밑이라 로컬 크기는 그만큼 나눠서 넣는다
    private float LocalScaleDivisor() => Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));

    private void Line(SpriteRenderer r, float length, float width, float angle, Color color)
    {
        r.enabled = true;
        r.transform.position = new Vector3(Eye.x, Eye.y, transform.position.z);
        r.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        float d = LocalScaleDivisor();
        r.transform.localScale = new Vector3(length / d, width / d, 1f);
        r.color = color;
    }

    private void SetVisible(bool visible)
    {
        if (aimLine != null) aimLine.enabled = visible;
        if (beam != null) beam.enabled = visible;
        if (core != null) core.enabled = visible;
        if (flash != null) flash.enabled = visible;
    }

    // 그림 파일 없이 흰 1픽셀을 늘려서 쓴다. 선은 왼쪽 끝 기준(눈에서 뻗게), 섬광은 가운데 기준.
    // 자식은 저장하지 않는다(DontSave) — 씬에 남지 않게
    private void EnsureRenderers()
    {
        if (aimLine != null) return;
        if (lineSprite == null)
        {
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            lineSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            dotSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            lineSprite.hideFlags = dotSprite.hideFlags = HideFlags.HideAndDontSave;
        }
        aimLine = MakeRenderer("조준선", sortingOrder, lineSprite);
        beam = MakeRenderer("레이저", sortingOrder, lineSprite);
        core = MakeRenderer("레이저 심", sortingOrder + 1, lineSprite);
        flash = MakeRenderer("끝 섬광", sortingOrder + 1, dotSprite);
        scorch = MakeRenderer("바닥 그을음", sortingOrder - 1, lineSprite);
    }

    private SpriteRenderer MakeRenderer(string name, int order, Sprite sprite)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(transform, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = order;
        r.enabled = false;
        return r;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
#endif
}
