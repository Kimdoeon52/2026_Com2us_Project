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

    public string DisplayName => _displayName;
    public GradeWeight[] SuccessWeights => _successWeights;
    public Vector2Int SuccessAmount => _successAmount;

    private void OnValidate()
    {
        GradeWeightUtil.RecalculatePercents(_successWeights);
    }
}
