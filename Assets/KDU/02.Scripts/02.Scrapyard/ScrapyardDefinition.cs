using UnityEngine;

// 지역 하나의 고물상 정의. 지역 추가는 이 에셋 1개 생성으로 끝난다
[CreateAssetMenu(menuName = "KDU/고물상/지역 정의", fileName = "ScrapyardDefinition")]
public class ScrapyardDefinition : ScriptableObject
{
    [Tooltip("지역 번호")]
    [SerializeField] private int _regionIndex;

    [Tooltip("표시명")]
    [SerializeField] private string _displayName;

    [Header("즉시 획득")]
    [Tooltip("등급 가중치")]
    [SerializeField] private GradeWeight[] _instantWeights;

    [Tooltip("수집량 범위")]
    [SerializeField] private Vector2Int _instantAmount;

    [Header("미니게임")]
    [Tooltip("미니게임 분기 확률")]
    [Range(0f, 1f)]
    [SerializeField] private float _minigameChance = 0.3f;

    [Tooltip("성공 보상 테이블")]
    [SerializeField] private MinigameRewardTable[] _rewardTables;

    [Header("총량 / 시간")]
    [Tooltip("월간 총량. 0이면 무제한")]
    [Min(0)]
    [SerializeField] private int _monthlyTotal;

    [Tooltip("수집 1회 시간 소모")]
    [Min(0f)]
    [SerializeField] private float _timeCostPerCollect;

    public int RegionIndex => _regionIndex;
    public string DisplayName => _displayName;
    public GradeWeight[] InstantWeights => _instantWeights;
    public Vector2Int InstantAmount => _instantAmount;
    public float MinigameChance => _minigameChance;
    public MinigameRewardTable[] RewardTables => _rewardTables;
    public int MonthlyTotal => _monthlyTotal;
    public float TimeCostPerCollect => _timeCostPerCollect;

    public bool HasRewardTable => _rewardTables != null && _rewardTables.Length > 0;

    // 등록된 보상 테이블 중 하나를 뽑는다. 비어 있으면 null
    public MinigameRewardTable PickRewardTable(Rng rng)
    {
        if (!HasRewardTable || rng == null)
            return null;

        return _rewardTables[rng.Range(0, _rewardTables.Length)];
    }

    private void OnValidate()
    {
        GradeWeightUtil.RecalculatePercents(_instantWeights);
    }
}
