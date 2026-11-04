using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스킬 창 한 줄. 이름·레벨·찍기 버튼을 그린다
public class SkillRowView : MonoBehaviour
{
    [Tooltip("스킬 이름")]
    [SerializeField] private TextMeshProUGUI _nameLabel;

    [Tooltip("부위·키")]
    [SerializeField] private TextMeshProUGUI _slotLabel;

    [Tooltip("레벨")]
    [SerializeField] private TextMeshProUGUI _levelLabel;

    [Tooltip("줄 선택 버튼")]
    [SerializeField] private Button _selectButton;

    [Tooltip("레벨 내리기")]
    [SerializeField] private Button _minusButton;

    [Tooltip("레벨 올리기")]
    [SerializeField] private Button _plusButton;

    [Tooltip("내리기 글자")]
    [SerializeField] private TextMeshProUGUI _minusGlyph;

    [Tooltip("올리기 글자")]
    [SerializeField] private TextMeshProUGUI _plusGlyph;

    [Tooltip("선택 표시")]
    [SerializeField] private GameObject _highlight;

    [Tooltip("활성 글자색")]
    [SerializeField] private Color _glyphEnabled = Color.white;

    [Tooltip("비활성 글자색")]
    [SerializeField] private Color _glyphDisabled = Color.gray;

    [Tooltip("레벨 변경 연출 크기")]
    [SerializeField] private float _punchScale = 0.35f;

    [Tooltip("레벨 변경 연출 시간 (초)")]
    [Min(0f)][SerializeField] private float _punchTime = 0.2f;

    public Slot Slot { get; private set; }

    public void Init(Action<SkillRowView> onSelect, Action<SkillRowView> onLevelDown, Action<SkillRowView> onLevelUp)
    {
        _selectButton.onClick.AddListener(() => onSelect(this));
        _minusButton.onClick.AddListener(() => onLevelDown(this));
        _plusButton.onClick.AddListener(() => onLevelUp(this));
    }

    private void OnDisable()
    {
        RectTransform level = _levelLabel.rectTransform;
        level.DOKill();
        level.localScale = Vector3.one;
    }

    public void Show(Slot slot, string skillName, int level, bool canLevelDown, bool canLevelUp)
    {
        Slot = slot;
        gameObject.SetActive(true);

        _nameLabel.text = skillName;
        _slotLabel.text = SlotText(slot);
        _levelLabel.text = level.ToString();

        SetButton(_minusButton, _minusGlyph, canLevelDown);
        SetButton(_plusButton, _plusGlyph, canLevelUp);
    }

    public void SetSelected(bool selected)
    {
        _highlight.SetActive(selected);
    }

    public void PlayLevelChanged()
    {
        RectTransform level = _levelLabel.rectTransform;
        level.DOKill(true);
        level.DOPunchScale(Vector3.one * _punchScale, _punchTime).SetUpdate(true).SetLink(gameObject);
    }

    private void SetButton(Button button, TextMeshProUGUI glyph, bool enabled)
    {
        button.interactable = enabled;
        glyph.color = enabled ? _glyphEnabled : _glyphDisabled;
    }

    private static string SlotText(Slot slot)
    {
        switch (slot)
        {
            case Slot.LeftArm: return "L.ARM · Q";
            case Slot.RightArm: return "R.ARM · W";
            case Slot.LeftLeg: return "L.LEG · E";
            case Slot.RightLeg: return "R.LEG · R";
            default: return slot.ToString();
        }
    }
}
