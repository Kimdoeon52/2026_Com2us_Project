using UnityEngine;

/// <summary>
/// 쿄다이 어깨 레이저 팔 (2026-11-05 사용자: 눈 레일건을 어깨로 옮겨 2개, 팔도 유기적으로).
/// 어깨(이 오브젝트, 본체 자식) → 윗팔(어깨에서 돎) → 팔꿈치(3_bracket_front의 빨간 원) → 레일건(팔꿈치에서 돎) → 총구.
/// 총구 = KyodaiLaser의 렌즈(판정 시작점)라 KyodaiLaser.Tick이 전투 틱마다 부른다 — 팔이 움직이면 레이저 시작점도 같이.
///
/// 움직임:
///   · 쉴 때: 윗팔은 쉬는 각도에서 천천히 숨 쉬듯 흔들리고, 레일건은 아래를 본다
///   · 조준: 윗팔이 목표 쪽으로 조금 기울며(스프링 — 늦게 따라가 출렁), 레일건은 목표를 정확히 본다(회전 빠르기 제한)
///   · 발사: 윗팔이 반동으로 뒤로 튕겼다 돌아온다
/// 왼쪽을 볼 땐 그림 위아래를 뒤집어 추진기·몸통이 위로 오게 한다.
/// </summary>
public class KyodaiLaserArm : MonoBehaviour
{
    [Header("뼈대 (빌더가 만든다)")]
    [Tooltip("윗팔 — 어깨 자리에서 돈다")]
    [SerializeField] private Transform upperArm;

    [Tooltip("윗팔 그림 묶음(팔 + 팔꿈치 받침대) — 왼쪽을 볼 때 위아래를 뒤집는다")]
    [SerializeField] private Transform upperArt;

    [Tooltip("팔꿈치 = 레일건 회전축 (윗팔 자식)")]
    [SerializeField] private Transform elbow;

    [Header("쉬는 자세 (도, 0 = 오른쪽, -90 = 아래)")]
    [Tooltip("윗팔이 쉴 때 각도")]
    [SerializeField] private float restAngle = -50f;

    [Tooltip("레일건이 쉴 때 각도")]
    [SerializeField] private float gunRestAngle = -90f;

    [Header("움직임")]
    [Tooltip("조준할 때 윗팔이 목표 쪽으로 기우는 정도 (0 = 안 기욺, 1 = 목표를 똑바로)")]
    [Range(0f, 1f)] [SerializeField] private float aimLean = 0.35f;

    [Tooltip("윗팔 스프링 빠르기(Hz) — 작을수록 묵직하게 늦게 따라간다")]
    [Min(0.1f)] [SerializeField] private float armFrequency = 1.4f;

    [Tooltip("윗팔 스프링 탄성 — 1보다 작을수록 지나쳤다 돌아온다")]
    [Range(0.1f, 1.5f)] [SerializeField] private float armDamping = 0.5f;

    [Tooltip("레일건이 조준할 때 도는 최대 빠르기(도/초)")]
    [Min(1f)] [SerializeField] private float gunAimSpeed = 360f;

    [Tooltip("레일건이 쉬는 각도로 돌아가는 빠르기(도/초)")]
    [Min(1f)] [SerializeField] private float gunReturnSpeed = 120f;

    [Tooltip("쉴 때 흔들림 크기(도) — 조준 중엔 줄어든다")]
    [Min(0f)] [SerializeField] private float swayAngle = 6f;

    [Tooltip("흔들림 주기(초)")]
    [Min(0.1f)] [SerializeField] private float swayPeriod = 3.2f;

    [Tooltip("발사 반동 — 윗팔이 목표 반대쪽으로 튕기는 세기(도/초)")]
    [SerializeField] private float recoil = 220f;

    private float upper = float.NaN, upperVel;
    private float gun = float.NaN;
    private float clock;

    public Transform Elbow => elbow;

    /// <summary>
    /// 전투 틱마다 (KyodaiLaser.Tick). aiming이면 target(경기장 평면 위 점)을 노린다.
    /// project: 3D 자리 → 화면에서 겹쳐 보이는 경기장 평면 위 점 (KyodaiLaser가 넘긴다 — 고정 자리 보정 포함)
    /// exact: 발사·사라짐 동안 — 레일건이 회전 빠르기 제한 없이 목표를 정확히 본다 (2026-11-09: 바닥 쓸기 중 제한 때문에 최대 9° 뒤처졌다)
    /// </summary>
    public void Tick(bool aiming, Vector2 target, System.Func<Vector3, Vector2?> project, bool exact = false)
    {
        if (upperArm == null || elbow == null) return;
        if (float.IsNaN(upper)) { upper = restAngle; gun = gunRestAngle; }
        float dt = CombatClock.TICK;
        clock += dt;

        // 윗팔: 쉬는 각도(+ 흔들림) ↔ 목표 쪽으로 조금 기욺. 각도 스프링이라 묵직하게 늦게 따라가고 살짝 지나친다
        float sway = Mathf.Sin(clock * 2f * Mathf.PI / swayPeriod + (restAngle < -90f ? 1.7f : 0f)) * swayAngle * (aiming ? 0.3f : 1f);
        float want = restAngle + sway;
        Vector2? shoulder = project(upperArm.position);
        if (aiming && shoulder.HasValue)
        {
            Vector2 d = target - shoulder.Value;
            if (d.sqrMagnitude > 0.0001f) want = Mathf.LerpAngle(restAngle, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, aimLean) + sway;
        }
        float w = 2f * Mathf.PI * armFrequency;
        upperVel += (w * w * Mathf.DeltaAngle(upper, want) - 2f * armDamping * w * upperVel) * dt;
        upper += upperVel * dt;
        SetAngle(upperArm, upperArt, upper);

        // 레일건: 팔꿈치에서 목표를 정확히 (팔이 먼저 움직인 뒤의 팔꿈치 자리로)
        float gunWant = gunRestAngle + sway * 0.5f;
        float speed = gunReturnSpeed;
        Vector2? e = project(elbow.position);
        if (aiming && e.HasValue)
        {
            Vector2 d = target - e.Value;
            if (d.sqrMagnitude > 0.0001f) { gunWant = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; speed = gunAimSpeed; }
        }
        gun = aiming && exact ? gunWant : Mathf.MoveTowardsAngle(gun, gunWant, speed * dt);
        SetAngle(elbow, elbow, gun);
    }

    /// <summary>
    /// 레일건만 지금 목표로 바로 돌린다 (윗팔 스프링은 안 돎). 바닥 쓸기에서 이번 틱에 옮긴 조준점을 같은 틱에 따라가게 —
    /// Tick은 조준점을 옮기기 전에 불려서 한 틱 늦다
    /// </summary>
    public void SnapGun(Vector2 target, System.Func<Vector3, Vector2?> project)
    {
        if (elbow == null || float.IsNaN(gun)) return;
        Vector2? e = project(elbow.position);
        if (!e.HasValue) return;
        Vector2 d = target - e.Value;
        if (d.sqrMagnitude < 0.0001f) return;
        gun = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        SetAngle(elbow, elbow, gun);
    }

    /// <summary>발사 순간 반동 — 목표 반대쪽으로 튕긴다</summary>
    public void Recoil(Vector2 target, System.Func<Vector3, Vector2?> project)
    {
        if (upperArm == null) return;
        Vector2? s = project(upperArm.position);
        if (!s.HasValue) return;
        float toTarget = Mathf.Atan2(target.y - s.Value.y, target.x - s.Value.x) * Mathf.Rad2Deg;
        upperVel += Mathf.DeltaAngle(upper, toTarget) > 0f ? -recoil : recoil;
    }

    // 회전(월드 z) + 왼쪽을 보면 그림 위아래 뒤집기. 거의 수직(±15°)일 땐 지금 뒤집힘 그대로 —
    // 아래를 보고 흔들릴 때 그림이 매 틱 뒤집혔다 돌아왔다 하며 튀지 않게
    private static void SetAngle(Transform pivot, Transform art, float angle)
    {
        pivot.rotation = Quaternion.Euler(0f, 0f, angle);
        if (art == null) return;
        float cos = Mathf.Cos(angle * Mathf.Deg2Rad);
        if (Mathf.Abs(cos) < 0.26f) return;
        Vector3 s = art.localScale;
        s.y = Mathf.Abs(s.y) * (cos < 0f ? -1f : 1f);
        art.localScale = s;
    }
}
