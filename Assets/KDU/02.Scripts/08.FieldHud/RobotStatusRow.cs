using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 로봇 상태 카드의 부위 한 줄. 이름·수치·칸 막대
public class RobotStatusRow : MonoBehaviour
{
    [Tooltip("부위")]
    [SerializeField] private Slot _slot;

    [SerializeField] private TextMeshProUGUI _nameLabel;
    [SerializeField] private TextMeshProUGUI _valueLabel;

    [Tooltip("막대 칸. 왼쪽부터 채운다")]
    [SerializeField] private Image[] _segments;

    [Tooltip("빈 칸 색")]
    [SerializeField] private Color _emptyColor = new Color(1f, 1f, 1f, 0.15f);

    [Tooltip("빈 부위 문구")]
    [SerializeField] private string _emptyText = "미장착";

    public Slot Slot => _slot;

    public void Show(string name, float ratio, Color color, string value, Color valueColor)
    {
        SetText(_nameLabel, name);
        SetText(_valueLabel, value);
        if (_valueLabel != null)
            _valueLabel.color = valueColor;

        Fill(ratio, color);
    }

    public void ShowEmpty()
    {
        SetText(_nameLabel, _emptyText);
        SetText(_valueLabel, "-");
        Fill(0f, _emptyColor);
    }

    private void Fill(float ratio, Color color)
    {
        if (_segments == null)
            return;

        int filled = Mathf.CeilToInt(Mathf.Clamp01(ratio) * _segments.Length);
        for (int i = 0; i < _segments.Length; i++)
        {
            if (_segments[i] != null)
                _segments[i].color = i < filled ? color : _emptyColor;
        }
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
            label.text = value;
    }
}
