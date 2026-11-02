using UnityEngine;

// 경매장 위탁 규칙
[CreateAssetMenu(menuName = "KDU/위탁/위탁 정의", fileName = "ConsignmentDefinition")]
public class ConsignmentDefinition : ScriptableObject
{
    [Tooltip("위탁 슬롯 수")]
    [Min(1)][SerializeField] private int _slotCount = 7;

    [Tooltip("낙찰가 변동 폭 (기준가 대비)")]
    [Range(0f, 1f)][SerializeField] private float _priceSpread = 0.3f;

    [Tooltip("변동 굴림 횟수. 클수록 기준가 근처에 몰린다")]
    [Min(1)][SerializeField] private int _rollCount = 3;

    public int SlotCount => _slotCount;

    public int MinPrice(int cost) => Mathf.RoundToInt(cost * (1f - _priceSpread));

    public int MaxPrice(int cost) => Mathf.RoundToInt(cost * (1f + _priceSpread));

    // 여러 번 굴린 평균이라 가운데로 몰린다
    public int RollPrice(int cost, Rng rng)
    {
        float sum = 0f;
        for (int i = 0; i < _rollCount; i++)
        {
            sum += rng.Value;
        }

        return Mathf.RoundToInt(Mathf.Lerp(MinPrice(cost), MaxPrice(cost), sum / _rollCount));
    }
}
