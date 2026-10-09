using UnityEngine;

/// <summary>
/// 날아가는 판정체(총알 등) 하나의 불변 데이터 (1층 · 데이터). §11-15 투사체 구조.
///
/// 지금까지의 판정은 전부 "캐릭터 피벗에 붙은 로컬 FrameBox"였는데, 총알은 캐릭터와 따로 움직이므로
/// 그 구조로는 표현할 수 없다. 그래서 투사체는 자기 위치를 가진 별도 객체(Projectile)로 만들고,
/// 그 객체가 읽는 수치만 여기 모아둔다 — 총알 종류가 늘어도 클래스는 안 늘고 이 에셋만 늘어난다(§1).
///
/// 데미지는 여기 두지 않는다. 쏜 행동(ActionData.Damage)을 그대로 쓴다 — KKH의 ProcessHit이
/// ActionData를 받는 계약이라, 데미지 출처를 하나로 두는 편이 안전하다(§14).
/// </summary>
[CreateAssetMenu(menuName = "NYH/Combat/Projectile Data", fileName = "NewProjectileData")]
public class ProjectileData : ScriptableObject
{
    [Header("이동 — 값 전부 임시 (기획 확정 전)")]
    [Tooltip("TODO: 임시값. 초당 이동 거리(사거리와 같은 단위). 틱마다 speed / 60만큼 전진")]
    [Min(0f)] [SerializeField] private float speed = 8f;

    [Tooltip("TODO: 임시값. 이 프레임 수가 지나면 아무것도 못 맞혀도 그 자리에서 터진다 (최대 사거리 = speed × 프레임/60)")]
    [Min(1)] [SerializeField] private int lifetimeFrames = 60;

    [Header("판정")]
    [Tooltip("투사체 위치 기준 로컬 Hit박스(오른쪽으로 날아갈 때 기준). 좌우 반전은 BoxResolver가 처리")]
    [SerializeField] private Rect hitBox = new Rect(-0.08f, -0.05f, 0.16f, 0.1f);

    [Header("연출")]
    [Tooltip("총알 오브젝트에 붙일 Animator Controller. 아래 두 스테이트를 갖고 있어야 한다")]
    [SerializeField] private RuntimeAnimatorController animatorController;

    [Tooltip("날아가는 동안 재생할 스테이트 이름")]
    [SerializeField] private string flyStateName;

    [Tooltip("맞거나 사거리 끝에 닿았을 때 재생할 폭발 스테이트 이름")]
    [SerializeField] private string impactStateName;

    [Tooltip("TODO: 임시값. 폭발 스테이트를 보여주는 프레임 수. 끝나면 오브젝트를 지운다")]
    [Min(1)] [SerializeField] private int impactFrames = 15;

    public float Speed => speed;
    public int LifetimeFrames => lifetimeFrames;
    public Rect HitBox => hitBox;
    public RuntimeAnimatorController AnimatorController => animatorController;
    public string FlyStateName => flyStateName;
    public string ImpactStateName => impactStateName;
    public int ImpactFrames => impactFrames;
}
