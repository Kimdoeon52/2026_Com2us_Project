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
    private int startDelay; // 재생 전 기다리는 틱 (양손 패턴에서 한 손만 늦게)
    private GroundTelegraph telegraph;
    private bool telegraphShown;
    private int hitStopFrames;
    private int frozenFrames;
    private bool down, dead;
    private Vector2 fallVel;   // 레이저에 맞아 떨어지는 속도 (2026-11-04)
    private bool landed, bounced;

    // ── 그림 상태 (연출 전용) ──
    private KyodaiHandPose pose = KyodaiHandPose.Fist;
    private float wantRotation, wantScale = 1f, wantWidth = 1f;
    private float width = 1f, widthVel;
    private int disableDamage; // 팔 무력화 게이지 — 맞은 데미지 누적 (2026-11-04, 기획서 부위 무력화)
    private bool flip;
    private float floatWeight = 1f;
    private float rotation, rotationVel, scale = 1f, scaleVel;
    private Vector2 smoothVel;
    private float squash, squashVel;
    private bool impactPending;
    private float flashUntil;
    private float alpha = 1f;
    private float seed;
    private float floatClock; // 떠다니기 시계 — 빠르기가 바뀌어도 흔들림이 튀지 않게 시간을 누적한다

    public KyodaiHandSide Side => side;
    public bool IsPlaying => motion != null;
    public bool IsFrozen => frozenFrames > 0;
    public bool IsDown => down;
    public Vector2 Position => pos;
    // 루트 자리 대신 고정 자리(Anchor) — KKH가 루트를 프레임 중간에 옮겨도 대기 자리가 흔들리지 않게
    public Vector2 Home => owner != null ? (Vector2)owner.Anchor + homeLocal * owner.Size : (Vector2)transform.position;

    // 보스 크기(루트 스케일). 키·느낌 값은 보스 단위라 쓸 때 곱한다
    private float S => owner != null ? owner.Size : 1f;
    public BossHurtZone HurtZone => hurtZone;

    /// <summary>떠다니는 흔들림 빠르기 배율 (그림만). 2페이즈에서 컨트롤러가 올린다</summary>
    public float FloatSpeed { get; set; } = 1f;

    private KyodaiHandKey CurrentKey => motion != null && keyIndex < motion.keys.Count ? motion.keys[keyIndex] : null;

    // ── 미리보기·디버그용 조회 ──
    public KyodaiHandMotion PlayingMotion => motion;
    public int KeyIndex => keyIndex;
    public int KeyFrame => keyFrame;
    public Vector2 Target => target;
    public bool InHitStop => hitStopFrames > 0;

    /// <summary>팔 무력화 게이지 (누적 데미지). 기준은 KyodaiBossController.ArmDisableDamage</summary>
    public int DisableDamage => disableDamage;

    /// <summary>팔이 무력화될 때 (소리·UI용)</summary>
    public event Action<KyodaiHand> OnDisabled;
    public Transform Visual => visual;
    public SpriteRenderer Renderer => spriteRenderer;
    public SpriteRenderer Shadow => shadow;

    /// <summary>마지막 틱에 판정이 켜져 있었다면 그 판정 상자(월드). 미리보기 창이 그리고, 가상 플레이어와 겹치는지 본다</summary>
    public bool TryGetHitBox(out Rect box)
    {
        box = lastHitBox;
        return hadHitBox;
    }

    private Rect lastHitBox;
    private bool hadHitBox;

    /// <summary>컨트롤러가 시작할 때 부른다 — 대기 자리에서 시작</summary>
    public void Init(KyodaiBossController boss)
    {
        owner = boss;
        pos = prevPos = Home;
        vel = Vector2.zero;
        motion = null;
        down = dead = false;
        frozenFrames = hitStopFrames = 0;
        keyIndex = keyFrame = 0;
        target = pos;
        hadHitBox = false;
        // 그림 상태도 처음으로 — 미리보기가 처음부터 다시 돌려도 같은 결과가 나오게
        alpha = 1f;
        floatWeight = 1f;
        rotation = rotationVel = scaleVel = 0f;
        scale = wantScale = 1f;
        width = wantWidth = 1f;
        widthVel = 0f;
        disableDamage = 0;
        wantRotation = 0f;
        flip = false;
        smoothVel = Vector2.zero;
        squash = squashVel = 0f;
        impactPending = false;
        flashUntil = 0f;
        // 양손이 같이 출렁이면 기계적으로 보인다 — 손마다 위상을 다르게
        seed = side == KyodaiHandSide.Left ? 0f : 37.7f;
        floatClock = 0f;
        startDelay = 0;
        SetPose(KyodaiHandPose.Fist, false);
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);

        if (hurtZone != null)
        {
            hurtZone.Vulnerable = true;
            hurtZone.OnHit -= HandleHit;
            hurtZone.OnHit += HandleHit;
        }
        if (feel == null && Application.isPlaying) Debug.LogWarning($"[KyodaiHand] {name}: 움직임 느낌(KyodaiHandFeel)이 비어 있음 — 떠다니기·표정 없음");
    }

    private void OnDestroy()
    {
        if (hurtZone != null) hurtZone.OnHit -= HandleHit;
    }

    /// <summary>
    /// 동작 재생. touch: 판정 키에서 플레이어에 닿을 때 — true를 돌려주면 그 판정 구간에선 더 안 맞는다
    /// (판정 키가 끊겼다 다시 시작하면 새로 맞을 수 있다 — 한 동작 안의 여러 번 찍기).
    /// delayFrames: 이만큼 틱을 대기 자리에서 기다렸다가 시작 (양손 패턴)
    /// </summary>
    public void Play(KyodaiHandMotion handMotion, Func<GameObject, bool> touch, GroundTelegraph groundTelegraph, int delayFrames = 0)
    {
        startDelay = Mathf.Max(0, delayFrames);
        // 레이저에 맞아 떨어져 있는 손은 새 공격을 받지 않는다 (양손 패턴은 다른 손만 하고 끝난다)
        if (handMotion == null || handMotion.keys.Count == 0 || dead || frozenFrames > 0) return;
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
    /// <summary>
    /// 레이저에 맞음 (2026-11-04 사용자: 공중의 주먹이 레이저에 맞으면 바닥으로 떨어지게). 하던 공격은 취소하고,
    /// 중력으로 떨어져 튕겼다가 frames 동안 바닥에 누워 있은 뒤(맞을 기회) 대기 자리로 올라간다. 이미 바닥이면 그 자리에 눕는다
    /// </summary>
    public void Freeze(int frames)
    {
        bool fresh = frozenFrames <= 0;
        frozenFrames = Mathf.Max(frozenFrames, frames);
        if (!fresh) return;
        if (motion != null) Finish(); // 공격 취소 — 패턴은 손이 끝난 걸 보고 다음으로 넘어간다
        fallVel = new Vector2(vel.x * 0.3f, Mathf.Min(vel.y, 0f));
        landed = bounced = false;
        wantRotation = feel != null ? (side == KyodaiHandSide.Left ? feel.fallRotation : -feel.fallRotation) : 0f; // 쓰러진 듯 기울어짐
        wantScale = 1f;
        SetPose(KyodaiHandPose.Fist, true);
    }

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
        hadHitBox = false;
        if (owner == null) return;
        if (hitStopFrames > 0) { hitStopFrames--; return; }
        if (frozenFrames > 0)
        {
            frozenFrames--;
            StepFall();
            if (frozenFrames == 0) wantRotation = 0f; // 일어나 대기 자리로 (StepIdle 스프링)
            transform.position = new Vector3(pos.x, pos.y, transform.position.z);
            return;
        }

        if (motion != null) StepMotion();
        else StepIdle();

        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
    }

    // 떨어지기: 중력 → 바닥에서 한 번 튕김(찌그러짐·작은 화면 흔들림) → 눕기
    private void StepFall()
    {
        if (landed) { vel = Vector2.zero; return; }
        float gravity = (feel != null ? feel.fallGravity : -45f) * S;
        float floor = owner.GroundY + (feel != null ? feel.downHeight : 1f) * S;
        fallVel.y += gravity * CombatClock.TICK;
        fallVel.x *= 0.95f;
        pos += fallVel * CombatClock.TICK;
        if (pos.y <= floor)
        {
            pos.y = floor;
            float minBounce = (feel != null ? feel.fallBounceMinSpeed : 2f) * S;
            if (-fallVel.y > minBounce)
            {
                if (!bounced)
                {
                    bounced = true;
                    impactPending = true;
                    if (Application.isPlaying) KyodaiCameraShake.Shake(feel != null ? feel.fallShake : 0.2f, 0.2f);
                }
                fallVel.y = -fallVel.y * (feel != null ? feel.fallBounce : 0.3f);
            }
            else
            {
                landed = true;
                fallVel = Vector2.zero;
            }
        }
        vel = fallVel;
    }

    private void StepIdle()
    {
        Vector2 home = Home;
        Vector2 goal = down ? new Vector2(home.x, owner.GroundY + (feel != null ? feel.downHeight : 1f) * S) : home;
        float f = feel != null ? feel.idleFrequency : 1.1f;
        float z = feel != null ? feel.idleDamping : 0.65f;
        // 쓰러질 땐 무겁게 툭 떨어지도록 더 빠르고 덜 출렁이게
        if (down) { f *= 2.2f; z = Mathf.Max(z, 0.85f); }
        Spring(goal, f, z);
    }

    private void StepMotion()
    {
        if (startDelay > 0)
        {
            startDelay--;
            StepIdle();
            return;
        }
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
        // 판정 구간이 새로 시작되면 다시 맞을 수 있다 (연타의 두 번째 찍기)
        if (key.hitActive && (keyIndex == 0 || !motion.keys[keyIndex - 1].hitActive)) touched = false;
        target = new Vector2(ResolveX(key), ResolveY(key));
        bool mirror = key.mirrorByDirection && dir < 0;
        wantRotation = mirror ? -key.rotation : key.rotation;
        wantScale = key.scale;
        wantWidth = key.widthScale;
        flip = mirror;
        SetPose(key.pose, true);
        FitHurtZone(key.scale, key.widthScale); // 크게 키운 손(손바닥 압착)도 보이는 만큼 맞게
    }

    // 맞는 자리 = 그림 크기 × 키 크기(가로는 가로 배율까지) × 보스 크기
    private void FitHurtZone(float keyScale, float keyWidth)
    {
        Sprite sprite = pose == KyodaiHandPose.Palm ? palmSprite : fistSprite;
        if (hurtZone == null || sprite == null) return;
        float h = feel != null ? feel.hurtScale : 0.85f;
        Vector2 size = sprite.bounds.size;
        hurtZone.SetWorldSize(new Vector2(size.x * keyWidth, size.y) * keyScale * h * S);
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
            case KyodaiHandAnchor.ArenaLeft: x = owner.ArenaMinX; break;
            case KyodaiHandAnchor.ArenaRight: x = owner.ArenaMaxX; break;
            default: x = target.x; break; // Keep
        }
        x += (key.mirrorByDirection ? key.offset.x * dir : key.offset.x) * S;
        return key.clampToArena ? owner.ClampToArena(x) : x;
    }

    private float ResolveY(KyodaiHandKey key)
        => (key.anchor == KyodaiHandAnchor.Home ? Home.y : owner.GroundY) + key.offset.y * S;

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
        if (key.telegraphFullArena || key.telegraphWidth > 0f)
        {
            float x = key.telegraphFullArena ? owner.ArenaCenterX : target.x;
            float width = key.telegraphFullArena ? owner.ArenaWidth : key.telegraphWidth * S;
            if (!telegraphShown) telegraph.Show(x, owner.GroundY, width);
            else telegraph.MoveTo(x);
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
        Vector2 offset = (key.mirrorByDirection ? new Vector2(key.hitOffset.x * dir, key.hitOffset.y) : key.hitOffset) * S;
        Vector2 size = key.hitSize * S;
        lastHitBox = new Rect(pos + offset - size * 0.5f, size);
        hadHitBox = true;
        if (touched || onTouch == null) return;
        int count = Physics2D.OverlapBox(pos + offset, size, 0f, overlapFilter, overlap);
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
        var ground = new Vector2(pos.x, owner.GroundY);
        if (!Application.isPlaying) return; // 에디터 미리보기 — 화면 흔들림·먼지 생성 없음

        KyodaiCameraShake.Shake(key.impactShake, feel != null ? feel.shakeSeconds : 0.3f);
        if (key.spawnShockwave) owner.SpawnShockwave(ground.x, onTouch); // 충격파 내려치기 — 바닥을 따라 좌우로
        if (feel != null) PixelFx.Play(feel.impactFx, new Vector3(ground.x, ground.y, transform.position.z));
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
        wantWidth = 1f;
        flip = false;
        SetPose(KyodaiHandPose.Fist, true);
        FitHurtZone(1f, 1f);
    }

    private void SetPose(KyodaiHandPose next, bool pop)
    {
        bool changed = next != pose;
        pose = next;
        Sprite sprite = pose == KyodaiHandPose.Palm ? palmSprite : fistSprite;
        if (spriteRenderer != null) spriteRenderer.sprite = sprite;
        if (hurtZone != null && sprite != null)
            hurtZone.SetWorldSize(sprite.bounds.size * (feel != null ? feel.hurtScale : 0.85f) * S); // 그림이 보스 크기만큼 작게 보이니 맞는 자리도
        // 그림이 바뀌는 순간을 톡 튀는 움직임으로 가린다
        if (changed && pop && feel != null) squashVel += feel.posePop;
    }

    private void HandleHit(BossHurtZone zone, int damage)
    {
        if (feel != null)
        {
            flashUntil = Time.time + feel.hitFlashSeconds;
            squashVel -= feel.hitKick;
            // 손 그림의 실제 가운데(떠다니기·기울기 포함)에 내고, 이펙트가 끝날 때까지 손을 따라간다 — 손 위·아래로 어긋나지 않게
            Transform follow = spriteRenderer != null ? spriteRenderer.transform : (visual != null ? visual : transform);
            Vector3 at = spriteRenderer != null ? spriteRenderer.bounds.center : follow.position;
            var fx = PixelFx.Play(feel.hitFx, at, 1f, owner != null && owner.PlayerX < at.x); // 플레이어 쪽에서 맞은 방향으로
            if (fx != null) fx.Follow(follow);
        }

        // 팔 무력화 (기획서 "부위 구성 HP 구조": 팔 무력화 = 8초간 그 팔 패턴 봉인). 떨어져 누워 있는 동안은 안 쌓인다.
        // 무력화 = 레이저에 맞았을 때처럼 바닥으로 떨어져 누움(하던 공격 취소, 새 공격 안 받음) → 그 팔 패턴이 자연히 봉인된다
        if (owner == null || dead || IsFrozen || owner.ArmDisableDamage <= 0) return;
        disableDamage += Mathf.Max(0, damage);
        if (disableDamage < owner.ArmDisableDamage) return;
        disableDamage = 0;
        Freeze(owner.ArmDisableFrames);
        OnDisabled?.Invoke(this);
        Debug.Log($"[Kyodai] {name} 무력화 — {owner.ArmDisableFrames / 60f:0.#}초");
    }

    // ───────────────────────── 그림 (연출 전용) ─────────────────────────

    private void LateUpdate()
    {
        if (!Application.isPlaying) return; // 에디터에선 미리보기 창이 UpdateVisual을 직접 부른다
        // 프레임이 크게 밀려도 출렁임 계산이 터지지 않게. 틱 사이 보간 — 고주사율 모니터에서도 60Hz 계단이 안 보이게
        UpdateVisual(Mathf.Min(Time.deltaTime, 1f / 30f), CombatClock.InterpolationAlpha, Time.time);
    }

    /// <summary>그림 갱신 (떠다니기·표정·색·그림자). 플레이 중엔 LateUpdate, 에디터에선 패턴 미리보기 창이 부른다</summary>
    public void UpdateVisual(float dt, float interpolation, float time)
    {
        if (owner == null || visual == null || feel == null) return;
        Vector2 basePos = Vector2.Lerp(prevPos, pos, interpolation);

        // ③ 떠다니기
        KyodaiHandKey key = CurrentKey;
        float wantWeight = dead || IsFrozen ? 0f : down ? feel.downFloatWeight : key != null ? key.floatWeight : 1f;
        floatWeight = Mathf.MoveTowards(floatWeight, wantWeight, feel.floatBlendSpeed * dt);

        floatClock += dt * FloatSpeed;
        float t = floatClock + seed;
        const float TAU = Mathf.PI * 2f;
        Vector2 bob = new Vector2(
            feel.floatAmplitude.x * Mathf.Sin(t * TAU / feel.floatPeriodB) + feel.noiseAmplitude * (Mathf.PerlinNoise(t * feel.noiseSpeed, seed) * 2f - 1f),
            feel.floatAmplitude.y * Mathf.Sin(t * TAU / feel.floatPeriodA) + feel.noiseAmplitude * (Mathf.PerlinNoise(seed + 11.1f, t * feel.noiseSpeed) * 2f - 1f));
        bob *= floatWeight;
        float floatRot = feel.floatRotation * Mathf.Sin(t * TAU / (feel.floatPeriodA * 1.37f) + 1.3f) * floatWeight;

        // ④ 표정 — 속도 쪽으로 기울고, 빠르면 늘어난다
        // 속도는 보스 단위로 — 작은 보스는 같은 동작도 월드 속도가 느리니, 크기로 나눠야 기울기·늘어남이 같게 나온다
        float size = S;
        smoothVel = Vector2.Lerp(smoothVel, vel / size, 1f - Mathf.Exp(-feel.velocitySmoothing * dt));
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

        Vector2 shake = IsFrozen && landed ? UnityEngine.Random.insideUnitCircle * feel.frozenShake : Vector2.zero; // 누운 뒤 지직거림
        // Visual은 크기가 줄어든 루트 밑이라 로컬 1 = 월드 Size — 보간(월드)만 나눠서 넣고, 흔들림(보스 단위)은 그대로
        visual.localPosition = (Vector3)((basePos - pos) / size + bob + shake);
        visual.localRotation = Quaternion.Euler(0f, 0f, rotation + lean + floatRot);
        width = Mathf.SmoothDamp(width, wantWidth, ref widthVel, feel.scaleSmoothTime, Mathf.Infinity, dt);
        visual.localScale = new Vector3(sx * scale * width * (flip ? -1f : 1f), sy * scale, 1f);

        // 색
        alpha = Mathf.MoveTowards(alpha, dead ? 0.35f : 1f, dt);
        Color c = Color.white;
        if (down) c = feel.downTint;
        if (IsFrozen) c = feel.frozenTint;
        if (time < flashUntil) c = feel.hitFlashColor;
        c.a = alpha;
        if (spriteRenderer != null) spriteRenderer.color = c;

        UpdateShadow(basePos + bob * size);
    }

    // 높이 뜰수록 그림자가 작고 옅어진다 — 떠 있다는 느낌의 대부분이 여기서 나온다
    private void UpdateShadow(Vector2 shown)
    {
        if (shadow == null) return;
        float height = Mathf.Max(0f, shown.y - owner.GroundY);
        float k = Mathf.Clamp01(height / (feel.shadowFadeHeight * S)); // 높이는 월드, 기준은 보스 단위
        // 크기: 그림자는 크기가 줄어든 루트 밑이라 로컬 값 그대로 쓰면 월드에선 보스 크기만큼 작아진다
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
        Vector3 home = Application.isPlaying ? (Vector3)Home
            : transform.parent != null ? transform.parent.position + (Vector3)(homeLocal * Mathf.Abs(transform.parent.lossyScale.x)) : transform.position;
        Gizmos.DrawWireSphere(home, 0.3f);
        if (!TryGetHitBox(out Rect box)) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(box.center, box.size);
    }
#endif
}
