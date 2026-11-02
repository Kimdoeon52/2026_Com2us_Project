using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 위탁 창 왼쪽 보유 파츠 목록 한 줄
public class ConsignmentRow : MonoBehaviour
{
    [Tooltip("클릭 버튼")]
    [SerializeField] private Button _button;

    [Tooltip("배경")]
    [SerializeField] private Image _background;

    [Tooltip("파츠 이름")]
    [SerializeField] private TextMeshProUGUI _nameLabel;

    [Tooltip("등급·내구도")]
    [SerializeField] private TextMeshProUGUI _infoLabel;

    [Tooltip("기본 배경색")]
    [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0.06f);

    [Tooltip("선택 배경색")]
    [SerializeField] private Color _selectedColor = new Color(0.208f, 0.816f, 0.871f, 0.35f);

    [Tooltip("판매 불가 투명도")]
    [Range(0f, 1f)][SerializeField] private float _lockedAlpha = 0.45f;

    private Action _onClick;
    private CanvasGroup _group;

    private void Awake()
    {
        _group = gameObject.GetOrAddComponent<CanvasGroup>();
        if (_button != null)
            _button.onClick.AddListener(() => _onClick?.Invoke());
    }

    public void Show(string partName, string info, bool selected, bool sellable, Action onClick)
    {
        if (_group == null)
            _group = gameObject.GetOrAddComponent<CanvasGroup>();

        _onClick = onClick;
        _nameLabel.text = partName;
        _infoLabel.text = info;
        _background.color = selected ? _selectedColor : _normalColor;
        _group.alpha = sellable ? 1f : _lockedAlpha;
    }
}
