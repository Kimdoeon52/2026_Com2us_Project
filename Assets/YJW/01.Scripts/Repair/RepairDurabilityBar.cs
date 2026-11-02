using UnityEngine;
using UnityEngine.UI;

public class RepairDurabilityBar : MonoBehaviour
{
    [SerializeField] private Image[] _cells;

    public void SetValue(int current, int max, RepairPartState state)
    {
        RepairSprites sprites = RepairSprites.Instance;

        // 값이 1이라도 있으면 한 칸은 보이게 한다
        int filled = max > 0 ? Mathf.CeilToInt((float)current / max * _cells.Length) : 0;

        for (int i = 0; i < _cells.Length; i++)
        {
            _cells[i].sprite = i < filled ? sprites.Cell(state) : sprites.cellEmpty;
        }
    }
}
