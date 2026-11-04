using System;
using System.Collections.Generic;
using UnityEngine;

public enum KyodaiHandSide { Left, Right }

/// <summary>
/// 쿄다이의 떠다니는 손 하나 (2026-11-04 2D 개편). 그림은 스프라이트 한 장(주먹/손바닥)이라, 살아 보이는 건 전부 움직임이 만든다.
///
/// 움직임 4겹:
///   ① 키(KyodaiHandMotion) — 어디로 갈지·모양·판정                      ┐ 전투 틱(CombatClock)에서.
///   ② 따라가기 — 스프링(무게감·출렁임) 또는 이징(정확한 도착)            ┘ 손 루트 위치 = 판정 위치
///   ③ 떠다니기 — 주기가 다른 사인 2개 + 노이즈, 양손 위상 어긋남          ┐ Update에서 그림 자식(Visual)에만.
///   ④ 표정 — 속도 쪽 기울기·늘어남, 충격 찌그러짐, 반동, 바닥 그림자      ┘ 판정과 무관 (KyodaiHandFeel 값)
///
/// 손은 항상 맞는다(BossHurtZone 상시 켜짐, 11-04 결정 — 옛 레이어 규칙 폐기). 레이저에 맞으면 Freeze로 정지.
/// 수치: 데미지는 패턴 데이터(KKH BossPatternData), 프레임은 모션 에셋 — 여기엔 숫자를 쓰지 않는다.
/// </summary>
public class KyodaiHand : MonoBehaviour
{
    [SerializeField] private KyodaiHandSide side = KyodaiHandSide.Right;

    [Tooltip("대기 자리 — 보스 루트에서의 거리(월드)")]
    [SerializeField] private Vector2 homeLocal = new Vector2(4f, 5f);

    [Tooltip("움직임 느낌 (떠다니기·기울기·찌그러짐·그림자)")]
    [SerializeField] private KyodaiHandFeel feel;

    [SerializeField] private Sprite fistSprite;
    [SerializeField] private Sprite palmSprite;

    [Tooltip("그림 자식 — 떠다니기·기울기·찌그러짐은 여기에만 (판정은 루트)")]
    [SerializeField] private Transform visual;

    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("바닥 그림자 (1월드 폭 타원 스프라이트)")]
    [SerializeField] private SpriteRenderer shadow;

    [Tooltip("맞는 자리 (루트 자식 — 항상 켜짐)")]
    [SerializeField] private BossHurtZone hurtZone;

    [Tooltip("충격 때 바닥에 생성할 먼지 (비우면 없음 — 도트 플립북 프리팹 예정)")]
    [SerializeField] private GameObject impactDustPrefab;

    /// <summary>충격 순간 (바닥 위치). 소리·이펙트용</summary>
    public event Action<KyodaiHand, Vector2> OnImpact;

    private static readonly List<Collider2D> overlap = new List<Collider2D>(8);
    private static readonly ContactFilter2D overlapFilter = new ContactFilter2D { useTriggers = true };

    private KyodaiBossController owner;

    // ── 틱 상태 (판정 기준) ──
    private Vector2 pos, prevPos, vel;
    private KyodaiHandMotion motion;
    private int keyIndex, keyFrame;
    private Vector2 keyStart, target;
    private int dir = 1; // 공격 방향: 플레이어가 경기장 오른쪽이면 +1
    private Func<GameObject, bool> onTouch;
    private bool touched;
    private GroundTelegraph telegraph;
    private bool telegraphShown;
    private int hitStopFrames;
    private int frozenFrames;
    private bool down, dead;

    // ── 그림 상태 (연출 전용) ──
    private KyodaiHandPose pose = KyodaiHandPose.Fist;
    private float wantRotation, wantScale = 1f;
    private bool flip;
    private float floatWeight = 1f;
    private float rotation, rotationVel, scale = 1f, scaleVel;
    private Vector2 smoothVel;
    private float squash, squashVel;
    private bool impactPending;
    private float flashUntil;
    private float alpha = 1f;
    private float seed;

    public KyodaiHandSide Side => side;
    public bool IsPlaying => motion != null;
    public bool IsFrozen => frozenFrames > 0;
    public bool IsDown => down;
    public Vector2 Position => pos;
    public Vector2 Home => owner != null ? (Vector2)owner.transform.position + homeLocal : (Vector2)transform.position;
    public BossHurtZone HurtZone => hurtZone;

    private KyodaiHandKey CurrentKey => motion != null && keyIndex < motion.keys.Count ? motion.keys[keyIndex] : null;

    /// <summary>컨트롤러가 시작할 때 부른다 — 대기 자리에서 시작</summary>
    public void Init(KyodaiBossController boss)
    {
        owner = boss;
        pos = prevPos = Home;
        vel = Vector2.zero;
        motion = null;
        down = dead = false;
        frozenFrames = hitStopFrames = 0;
        alpha = 1f;
        // 양손이 같이 출렁이면 기계적으로 보인다 — 손마다 위상을 다르게
        seed = side == KyodaiHandSide.Left ? 0f : 37.7f;
        SetPose(KyodaiHandPose.Fist, false);
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);

        if (hurtZone != null)
        {
            hurtZone.Vulnerable = true;
            hurtZone.OnHit -= HandleHit;
            hurtZone.OnHit += HandleHit;
        }
        if (feel == null) Debug.LogWarning($"[KyodaiHand] {name}: 움직임 느낌(KyodaiHandFeel)이 비어 있음 — 떠다니기·표정 없음");
    }

    private void OnDestroy()
    {
        if (hurtZone != null) hurtZone.OnHit -= HandleHit;
    }

    /// <summary>동작 재생. touch: 판정 키에서 플레이어에 닿을 때 — true를 돌려주면 그 재생에선 더 안 맞는다</summary>
    public void Play(KyodaiHandMotion handMotion, Func<GameObject, bool> touch, GroundTelegraph groundTelegraph)
    {
        if (handMotion == null || handMotion.keys.Count == 0 || dead) return;
        motion = handMotion;
        keyIndex = keyFrame = 0;
        onTouch = touch;
        touched = false;
        telegraph = groundTelegraph;
        telegraphShown = false;
        target = pos;
        dir = owner.PlayerX >= owner.ArenaCenterX ? 1 : -1;
    }

    /// <summary>재생을 끊고 대기 자리로 돌아간다 (패턴 정리·그로기)</summary>
    public void Stop()
    {
        if (motion != null) Finish();
    }

    /// <summary>frames 동안 정지 — 레이저에 맞은 팔 (기획: 4초). 정지 중엔 움직임·판정이 멈추고 재생은 그 자리에서 이어진다</summary>
    public void Freeze(int frames) => frozenFrames = Mathf.Max(frozenFrames, frames);

    /// <summary>그로기 동안 손이 바닥으로 떨어진다</summary>
    public void SetDown(bool value)
    {
        down = value;
        if (down) Stop();
    }

    /// <summary>격파 — 떨어져서 흐려지고 더는 안 맞는다</summary>
    public void Kill()
    {
        dead = true;
        SetDown(true);
        if (hurtZone != null) hurtZone.Vulnerable = false;
    }

    // ───────────────────────── 전투 틱 ─────────────────────────

    /// <summary>호출: KyodaiBossController(CombatClock.OnCombatTick). 1/60초에 한 번</summary>
    public void Tick()
    {
        prevPos = pos;
        if (owner == null) return;
        if (hitStopFrames > 0) { hitStopFrames--; return; }
        if (frozenFrames > 0) { frozenFrames--; vel = Vector2.zero; return; }

        if (motion != null) StepMotion();
        else StepIdle();

        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
    }

    private void StepIdle()
    {
        Vector2 home = Home;
        Vector2 goal = down ? new Vector2(home.x, owner.GroundY + (feel != null ? feel.downHeight : 1f)) : home;
        float f = feel != null ? feel.idleFrequency : 1.1f;
        float z = feel != null ? feel.idleDamping : 0.65f;
        // 쓰러질 땐 무겁게 툭 떨어지도록 더 빠르고 덜 출렁이게
        if (down) { f *= 2.2f; z = Mathf.Max(z, 0.85f); }
        Spring(goal, f, z);
    }

    private void StepMotion()
    {
        KyodaiHandKey key = CurrentKey;
        if (keyFrame == 0) BeginKey(key);
        else if (key.trackPlayer) target.x = ResolveX(key);

        if (key.move == KyodaiHandMove.Spring)
        {
            Spring(target, key.springFrequency, key.springDamping);
        }
        else
        {
            float t = key.easeCurve.Evaluate((keyFrame + 1) / (float)key.frames);
            Vector2 next = Vector2.LerpUnclamped(keyStart, target, t);
            vel = (next - pos) / CombatClock.TICK;
            pos = next;
        }

        UpdateTelegraph(key);
        if (key.hitActive) CheckTouch(key);

        keyFrame++;
        if (keyFrame < key.frames) return;

        if (key.impactOnEnd) Impact(key);
        keyIndex++;
        keyFrame = 0;
        if (keyIndex >= motion.keys.Count) Finish();
    }

    private void BeginKey(KyodaiHandKey key)
    {
        keyStart = pos;
        target = new Vector2(ResolveX(key), ResolveY(key));
        bool mirror = key.mirrorByDirection && dir < 0;
        wantRotation = mirror ? -key.rotation : key.rotation;
        wantScale = key.scale;
        flip = mirror;
        SetPose(key.pose, true);
    }

    private float ResolveX(KyodaiHandKey key)
    {
        float x;
        switch (key.anchor)
        {
            case KyodaiHandAnchor.Home: x = Home.x; break;
            case KyodaiHandAnchor.Player: x = owner.PlayerX; break;
            case KyodaiHandAnchor.ArenaFar: x = dir > 0 ? owner.ArenaMinX : owner.ArenaMaxX; break;
            case KyodaiHandAnchor.ArenaNear: x = dir > 0 ? owner.ArenaMaxX : owner.ArenaMinX; break;
            case KyodaiHandAnchor.ArenaCenter: x = owner.ArenaCenterX; break;
            default: x = target.x; break; // Keep
        }
        x += key.mirrorByDirection ? key.offset.x * dir : key.offset.x;
        return key.clampToArena ? owner.ClampToArena(x) : x;
    }

    private float ResolveY(KyodaiHandKey key)
        => (key.anchor == KyodaiHandAnchor.Home ? Home.y : owner.GroundY) + key.offset.y;

    // 반암시적 오일러 — 60Hz 고정 틱이라 스프링 12Hz 정도까지 안정
    private void Spring(Vector2 goal, float frequency, float damping)
    {
        float w = 2f * Mathf.PI * frequency;
        Vector2 accel = w * w * (goal - pos) - 2f * damping * w * vel;
        vel += accel * CombatClock.TICK;
        pos += vel * CombatClock.TICK;
    }

    private void UpdateTelegraph(KyodaiHandKey key)
    {
        if (telegraph == null) return;
        if (key.telegraphWidth > 0f)
        {
            if (!telegraphShown) telegraph.Show(target.x, owner.GroundY, key.telegraphWidth);
            else telegraph.MoveTo(target.x);
            telegraphShown = true;
        }
        else if (telegraphShown)
        {
            telegraph.Hide();
            telegraphShown = false;
        }
    }

    private void CheckTouch(KyodaiHandKey key)
    {
        if (touched || onTouch == null) return;
        Vector2 offset = key.mirrorByDirection ? new Vector2(key.hitOffset.x * dir, key.hitOffset.y) : key.hitOffset;
        int count = Physics2D.OverlapBox(pos + offset, key.hitSize, 0f, overlapFilter, overlap);
        for (int i = 0; i < count; i++)
        {
            if (!overlap[i].CompareTag("Player")) continue;
            // 회피 무적이면 false — 판정 키가 끝날 때까지 계속 본다(겹친 채 무적이 풀리면 맞는다)
            if (onTouch(overlap[i].gameObject)) touched = true;
            return;
        }
    }

    private void Impact(KyodaiHandKey key)
    {
        hitStopFrames = key.impactFreezeFrames;
        vel = Vector2.zero;
        impactPending = true;
        KyodaiCameraShake.Shake(key.impactShake, feel != null ? feel.shakeSeconds : 0.3f);

        var ground = new Vector2(pos.x, owner.GroundY);
        if (impactDustPrefab != null)
            Destroy(Instantiate(impactDustPrefab, new Vector3(ground.x, ground.y, transform.position.z), Quaternion.identity), 3f);
        OnImpact?.Invoke(this, ground);
    }

    private void Finish()
    {
        motion = null;
        onTouch = null;
        if (telegraph != null && telegraphShown) telegraph.Hide();
        telegraphShown = false;
        telegraph = null;
        wantRotation = 0f;
        wantScale = 1f;
        flip = false;
        SetPose(KyodaiHandPose.Fist, true);
    }

    private void SetPose(KyodaiHandPose next, bool pop)
    {
        bool changed = next != pose;
        pose = next;
        Sprite sprite = pose == KyodaiHandPose.Palm ? palmSprite : fistSprite;
        if (spriteRenderer != null) spriteRenderer.sprite = sprite;
        if (hurtZone != null && sprite != null)
            hurtZone.SetWorldSize(sprite.bounds.size * (feel != null ? feel.hurtScale : 0.85f));
        // 그림이 바뀌는 순간을 톡 튀는 움직임으로 가린다
        if (changed && pop && feel != null) squashVel += feel.posePop;
    }

    private void HandleHit(BossHurtZone zone, int damage)
    {
        if (feel == null) return;
        flashUntil = Time.time + feel.hitFlashSeconds;
        squashVel -= feel.hitKick;
    }

    // ───────────────────────── 그림 (연출 전용) ─────────────────────────

    private void LateUpdate()
    {
        if (owner == null || visual == null || feel == null) return;
        // 프레임이 크게 밀려도 출렁임 계산이 터지지 않게
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

        // 틱 사이 보간 — 고주사율 모니터에서도 60Hz 계단이 안 보이게
        Vector2 basePos = Vector2.Lerp(prevPos, pos, CombatClock.InterpolationAlpha);

        // ③ 떠다니기
        KyodaiHandKey key = CurrentKey;
        float wantWeight = dead || IsFrozen ? 0f : down ? feel.downFloatWeight : key != null ? key.floatWeight : 1f;
        floatWeight = Mathf.MoveTowards(floatWeight, wantWeight, feel.floatBlendSpeed * dt);

        float t = Time.time + seed;
        const float TAU = Mathf.PI * 2f;
        Vector2 bob = new Vector2(
            feel.floatAmplitude.x * Mathf.Sin(t * TAU / feel.floatPeriodB) + feel.noiseAmplitude * (Mathf.PerlinNoise(t * feel.noiseSpeed, seed) * 2f - 1f),
            feel.floatAmplitude.y * Mathf.Sin(t * TAU / feel.floatPeriodA) + feel.noiseAmplitude * (Mathf.PerlinNoise(seed + 11.1f, t * feel.noiseSpeed) * 2f - 1f));
        bob *= floatWeight;
        float floatRot = feel.floatRotation * Mathf.Sin(t * TAU / (feel.floatPeriodA * 1.37f) + 1.3f) * floatWeight;

        // ④ 표정 — 속도 쪽으로 기울고, 빠르면 늘어난다
        smoothVel = Vector2.Lerp(smoothVel, vel, 1f - Mathf.Exp(-feel.velocitySmoothing * dt));
        float lean = Mathf.Clamp(-smoothVel.x * feel.leanPerSpeed, -feel.maxLean, feel.maxLean);
        rotation = Mathf.SmoothDampAngle(rotation, wantRotation, ref rotationVel, feel.rotationSmoothTime, Mathf.Infinity, dt);
        scale = Mathf.SmoothDamp(scale, wantScale, ref scaleVel, feel.scaleSmoothTime, Mathf.Infinity, dt);

        float speed = smoothVel.magnitude;
        float stretch = Mathf.Min(speed * feel.stretchPerSpeed, feel.maxStretch);
        Vector2 d = speed > 0.001f ? smoothVel / speed : Vector2.zero;
        float sx = 1f + stretch * Mathf.Abs(d.x) - stretch * 0.5f * Mathf.Abs(d.y);
        float sy = 1f + stretch * Mathf.Abs(d.y) - stretch * 0.5f * Mathf.Abs(d.x);

        // 찌그러짐 스프링 — 충격엔 납작하게, 피격·모양 바뀜엔 톡
        if (impactPending)
        {
            squash = -feel.impactSquash;
            squashVel = 0f;
            impactPending = false;
        }
        float w = TAU * feel.squashFrequency;
        squashVel += (-w * w * squash - 2f * feel.squashDamping * w * squashVel) * dt;
        squash += squashVel * dt;
        sy *= 1f + squash;
        sx *= 1f - squash * feel.squashWiden;

        Vector2 shake = IsFrozen ? UnityEngine.Random.insideUnitCircle * feel.frozenShake : Vector2.zero;
        visual.localPosition = (Vector3)(basePos - pos + bob + shake);
        visual.localRotation = Quaternion.Euler(0f, 0f, rotation + lean + floatRot);
        visual.localScale = new Vector3(sx * scale * (flip ? -1f : 1f), sy * scale, 1f);

        // 색
        alpha = Mathf.MoveTowards(alpha, dead ? 0.35f : 1f, dt);
        Color c = Color.white;
        if (down) c = feel.downTint;
        if (IsFrozen) c = feel.frozenTint;
        if (Time.time < flashUntil) c = feel.hitFlashColor;
        c.a = alpha;
        if (spriteRenderer != null) spriteRenderer.color = c;

        UpdateShadow(basePos + bob);
    }

    // 높이 뜰수록 그림자가 작고 옅어진다 — 떠 있다는 느낌의 대부분이 여기서 나온다
    private void UpdateShadow(Vector2 shown)
    {
        if (shadow == null) return;
        float height = Mathf.Max(0f, shown.y - owner.GroundY);
        float k = Mathf.Clamp01(height / feel.shadowFadeHeight);
        float m = Mathf.Lerp(1f, feel.shadowMinScale, k) * scale;
        shadow.transform.position = new Vector3(shown.x, owner.GroundY + 0.02f, shadow.transform.position.z);
        shadow.transform.localScale = new Vector3(feel.shadowSize.x * m, feel.shadowSize.y * m, 1f);
        shadow.transform.rotation = Quaternion.identity;
        shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(feel.shadowMaxAlpha, feel.shadowMinAlpha, k) * alpha);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 home = Application.isPlaying ? (Vector3)Home : transform.parent != null ? transform.parent.position + (Vector3)homeLocal : transform.position;
        Gizmos.DrawWireSphere(home, 0.3f);
        KyodaiHandKey key = CurrentKey;
        if (key == null || !key.hitActive) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(pos + key.hitOffset, key.hitSize);
    }
#endif
}
