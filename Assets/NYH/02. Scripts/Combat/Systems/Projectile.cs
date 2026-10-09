using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 날아가는 판정체 한 발 (§11-15 투사체 구조). ActionExecutor가 활성 구간에 Spawn()으로 만든다.
///
/// 캐릭터 로컬 FrameBox로는 "캐릭터와 따로 움직이는 판정"을 표현할 수 없어서 자기 위치를 가진 객체로 뺐다.
/// 대신 판정 규칙은 근접과 똑같이 맞춘다:
///   · 이동·수명은 CombatClock 틱으로만 센다 (§3) — 주사율이 달라도 같은 프레임에 같은 위치
///   · 겹침 검사는 HitDetection이 한다 — 이 클래스는 "내 Hit박스가 지금 어디인가"(GetWorldHitBox)만 알려준다
///   · 판정과 표시가 같은 함수를 본다 (§4) — OnDrawGizmos도 GetWorldHitBox()를 그린다
///
/// 날아가는 동안만 Live 목록에 있고, 터진 뒤(폭발 연출 중)에는 목록에서 빠져서 더 이상 아무도 못 맞힌다.
/// </summary>
public class Projectile : MonoBehaviour
{
    private static readonly List<Projectile> live = new List<Projectile>();

    /// <summary>지금 날아가는 중인(=맞힐 수 있는) 투사체 전부. HitDetection이 매 틱 이 목록을 검사한다</summary>
    public static IReadOnlyList<Projectile> Live => live;

    // 도메인 리로드를 끈 채로 플레이를 다시 시작해도 지난 판의 투사체가 목록에 남지 않게 (CombatClock.ResetStatic과 같은 이유)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => live.Clear();

    /// <summary>쏜 로봇. 자기 총알에 자기가 맞지 않게, 그리고 ProcessHit의 공격자 ID로 쓴다</summary>
    public ActionExecutor Owner { get; private set; }

    /// <summary>쏜 행동. 데미지는 이 ActionData.Damage를 그대로 쓴다(ProcessHit 계약)</summary>
    public ActionData SourceAction { get; private set; }

    private ProjectileData data;
    private Animator animator;
    private bool facingRight;
    private int ageFrames;
    private bool exploded;
    private int impactAgeFrames;

    // 호출: ActionExecutor.EmitProjectiles. 받음: 총알 데이터, 쏜 행동, 쏜 로봇, 총구 월드 좌표, 날아갈 방향
    public static Projectile Spawn(ProjectileData data, ActionData sourceAction, ActionExecutor owner, Vector3 muzzle, bool facingRight)
    {
        GameObject go = SpriteEffect.Create($"Projectile_{data.name}", data.AnimatorController, muzzle, owner.transform, facingRight);
        var projectile = go.AddComponent<Projectile>();
        projectile.Init(data, sourceAction, owner, facingRight);
        return projectile;
    }

    private void Init(ProjectileData data, ActionData sourceAction, ActionExecutor owner, bool facingRight)
    {
        this.data = data;
        SourceAction = sourceAction;
        Owner = owner;
        this.facingRight = facingRight;
        animator = GetComponent<Animator>();

        PlayState(data.FlyStateName);
        live.Add(this);
        CombatClock.Instance.OnCombatTick += Tick;
    }

    /// <summary>지금 Hit박스의 월드 좌표. HitDetection(판정)과 OnDrawGizmos(표시)가 둘 다 이것만 쓴다 (§4)</summary>
    public Rect GetWorldHitBox() => BoxResolver.ToWorldRect(transform.position, data.HitBox, facingRight);

    /// <summary>
    /// 맞았거나 사거리 끝에 닿았을 때 — 그 자리에서 멈추고 폭발 연출로 바뀐다. Live에서 바로 빠지므로
    /// 같은 틱에 다른 대상을 또 맞히는 일은 없다(한 발 = 한 번)
    /// </summary>
    // 호출: HitDetection(명중) / Tick(수명 끝)
    public void Explode()
    {
        if (exploded) return;
        exploded = true;
        live.Remove(this);
        PlayState(data.ImpactStateName);
    }

    // 호출: CombatClock.OnCombatTick (1/60초마다). 날아가는 중이면 전진, 터진 뒤면 연출 시간만 센다
    private void Tick()
    {
        if (!exploded)
        {
            float direction = facingRight ? 1f : -1f;
            transform.position += Vector3.right * direction * data.Speed * CombatClock.TICK;

            ageFrames++;
            if (ageFrames >= data.LifetimeFrames) Explode(); // 기획서1009 붐버 "최대 사거리 도달 시 공중 폭발"과 같은 규칙
            return;
        }

        impactAgeFrames++;
        if (impactAgeFrames >= data.ImpactFrames) Destroy(gameObject);
    }

    private void PlayState(string stateName)
    {
        if (animator != null && !string.IsNullOrEmpty(stateName)) animator.Play(stateName, 0, 0f);
    }

    private void OnDestroy()
    {
        live.Remove(this);
        var clock = CombatClock.Existing;
        if (clock != null) clock.OnCombatTick -= Tick;
    }

    // BoxDrawer와 같은 색 규칙(Hit = 빨강). 날아가는 중일 때만 — 터진 뒤엔 판정이 없다
    private void OnDrawGizmos()
    {
        if (data == null || exploded) return;
        Rect r = GetWorldHitBox();
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new Vector3(r.center.x, r.center.y, transform.position.z), new Vector3(r.width, r.height, 0.01f));
    }
}
