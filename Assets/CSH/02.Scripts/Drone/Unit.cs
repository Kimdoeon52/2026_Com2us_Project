using UnityEngine;

/// <summary>
/// 드론 1기의 표시 전용 컴포넌트 (View). 위치는 전부 DroneControllHub가 넣어준다.
/// 드론 스스로는 "장전/소모 색"과 출격 이동에 필요한 값만 들고 있다. 판정에는 관여하지 않는다.
///
/// 드론은 전투 내내 꺼지지 않는다 — 실린더가 비면 검고 반투명한 "빈 탄" 상태로 등 뒤에 남는다.
/// </summary>
public class Unit : MonoBehaviour
{
    [Tooltip("소모(빈 탄) 상태 색. 스프라이트 원래 색 대신 이 색으로 바뀐다 (검정 + 반투명)")]
    [SerializeField] private Color spentColor = new Color(0f, 0f, 0f, 0.5f);

    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private TrailRenderer[] trails;

    /// <summary>장전 상태(원래 색)인지. false면 빈 탄(검정 반투명)</summary>
    public bool IsCharged { get; private set; } = true;

    /// <summary>출격 중(스킬 자리로 이동 ~ 액티브 끝)인지. 허브가 관리한다</summary>
    [HideInInspector] public bool inSortie;

    /// <summary>등 뒤 슬롯 번호 (0 ~ MaxCylinder-1). 생성 시 고정</summary>
    [HideInInspector] public int slotIndex;

    // 대기 슬롯으로 부드럽게 따라갈 때 쓰는 속도 버퍼 (SmoothDamp용)
    [HideInInspector] public Vector3 velocity;

    // ---- 출격 이동 데이터 (허브가 채운다) ----
    [HideInInspector] public Vector3 sortieStartPos;
    [HideInInspector] public Vector3 sortieLocalTarget;   // 피벗 기준 로컬 (x,y는 오른쪽 볼 때 기준, z는 깊이 오프셋)

    protected virtual void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) originalColors[i] = renderers[i].color;

        trails = GetComponentsInChildren<TrailRenderer>(true);
    }

    /// <summary>위치를 바로 옮긴다 (전투 시작 배치용)</summary>
    public void Place(Vector3 position)
    {
        transform.position = position;
        velocity = Vector3.zero;
        ClearTrails();
    }

    /// <summary>스킬 자리로 출격 시작</summary>
    public void BeginSortie(Vector3 localTarget)
    {
        inSortie = true;
        sortieStartPos = transform.position;
        sortieLocalTarget = localTarget;
    }

    /// <summary>출격 종료 — 이후엔 허브가 등 뒤 슬롯으로 되돌린다</summary>
    public void EndSortie()
    {
        inSortie = false;
        velocity = Vector3.zero;
    }

    /// <summary>장전(원래 색) / 빈 탄(검정 반투명) 전환</summary>
    public void SetCharged(bool charged)
    {
        if (IsCharged == charged) return;
        IsCharged = charged;

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].color = charged ? originalColors[i] : spentColor;
    }

    private void ClearTrails()
    {
        if (trails == null) return;
        foreach (var t in trails) t.Clear();
    }
}