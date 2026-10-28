using System;
using UnityEngine;

/// <summary>
/// KKH 보스의 "맞을 수 있는 자리" 하나 (2026-10-28, 쿄다이 레이어 규칙용).
///
/// HitDetection이 플레이어 Hit박스 자리에서 이 콜라이더를 찾으면, 부모의 BossControllerBase에 데미지를 넣되
/// 피해 배율(damageScale)을 곱한다. 맞을 수 없는 동안(배경·전이·공격·복귀)은 콜라이더를 꺼서 아예 안 걸리게 한다.
/// 이 컴포넌트가 없는 보스 콜라이더(정크 휠러 등)는 예전처럼 배율 1로 맞는다.
///
/// 근거: 기획서10_28 쿄다이 "레이어 판정 규칙"(전경·잔류만 타격 가능), "부위 구성 HP 구조"(부위별 피해 배율, HP는 공유)
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BossHurtZone : MonoBehaviour
{
    [Tooltip("부위 이름 (예: 왼팔, 오른팔, 왼다리, 오른다리, 메인렌즈). 무력화 게이지·로그용")]
    [SerializeField] private string partId;

    [Tooltip("이 자리를 때렸을 때 데미지 배율. 기획서10_28: 팔 1.0 / 다리 0.8 / 메인 렌즈 2.0")]
    [Min(0f)] [SerializeField] private float damageScale = 1f;

    private BoxCollider2D box;

    public string PartId => partId;
    public float DamageScale => damageScale;

    /// <summary>맞을 때마다(최종 데미지). 부위 무력화 게이지가 여기 붙을 자리</summary>
    public event Action<BossHurtZone, int> OnHit;

    public BoxCollider2D Box => box != null ? box : (box = GetComponent<BoxCollider2D>());

    /// <summary>지금 맞을 수 있는지. 끄면 콜라이더도 꺼져서 판정에 안 걸린다</summary>
    public bool Vulnerable
    {
        get => Box.enabled;
        set => Box.enabled = value;
    }

    private void Awake()
    {
        Box.isTrigger = true;
    }

    /// <summary>월드 크기로 판정 범위를 정한다 (부모 스케일과 무관하게)</summary>
    public void SetWorldSize(Vector2 size)
    {
        Vector3 s = transform.lossyScale;
        Box.size = new Vector2(size.x / Mathf.Max(Mathf.Abs(s.x), 0.0001f), size.y / Mathf.Max(Mathf.Abs(s.y), 0.0001f));
        Box.offset = Vector2.zero;
    }

    // 호출: HitDetection(보스에 데미지가 들어간 직후)
    public void NotifyHit(int damage) => OnHit?.Invoke(this, damage);
}
