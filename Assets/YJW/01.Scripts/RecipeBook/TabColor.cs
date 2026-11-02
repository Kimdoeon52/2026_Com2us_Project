using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class TabColor : MonoBehaviour
{
    private Image background;
    [SerializeField] private Color onColor = Color.gray;
    [SerializeField] private Color offColor = Color.white;

    [Tooltip("탭 글자색도 바꿀 때만 연결")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color onLabelColor = Color.black;
    [SerializeField] private Color offLabelColor = Color.white;

    private Toggle toggle;

    private void Awake()
    {
        background = GetComponent<Image>();
        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(UpdateColor);
        UpdateColor(toggle.isOn);   // 시작 상태 반영
    }

    private void UpdateColor(bool isOn)
    {
        background.color = isOn ? onColor : offColor;
        if (label != null)
            label.color = isOn ? onLabelColor : offLabelColor;
    }
}
