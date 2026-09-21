using UnityEngine;

// 미니게임 성공 보상. 즉시 획득에 배율을 얹지 않고 독립 테이블로 둔다
[CreateAssetMenu(menuName = "KDU/고물상/미니게임 보상 테이블", fileName = "MinigameRewardTable")]
public class MinigameRewardTable : ScriptableObject
{
    [Tooltip("표시명")]
    [SerializeField] private string _displayName;

    [Tooltip("성공 등급 가중치")]
    [SerializeField] private GradeWeight[] _successWeights;

    [Tooltip("성공 수집량 범위")]
    [SerializeField] private Vector2Int _successAmount;

    [Tooltip("실패 등급 가중치. 일반 부품만 넣으면 실패 시 항상 일반 부품")]
    [SerializeField] private GradeWeight[] _failureWeights;

    [Tooltip("실패 수집량 범위")]
    [SerializeField] private Vector2Int _failureAmount;

    public string DisplayName => _displayName;
    public GradeWeight[] SuccessWeights => _successWeights;
    public Vector2Int SuccessAmount => _successAmount;
    public GradeWeight[] FailureWeights => _failureWeights;
    public Vector2Int FailureAmount => _failureAmount;

    private void OnValidate()
    {
        GradeWeightUtil.RecalculatePercents(_successWeights);
        GradeWeightUtil.RecalculatePercents(_failureWeights);
    }
}
