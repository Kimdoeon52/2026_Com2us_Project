using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 툴팁 상자. 이름 줄, 등급색 그라데이션 위 아이콘과 등급 줄, 설명 순서로 그린다
public class TooltipView : MonoBehaviour
{
    [Header("이름 줄")]
    [SerializeField] private Image _headerLine;
    [SerializeField] private TextMeshProUGUI _name;

    [Header("아이콘 줄")]
    [SerializeField] private Image _bodyGlow;
    [SerializeField] private Image _icon;
    [Tooltip("다리 파츠 아이콘을 칸보다 크게 그리는 배율. 칸 크기는 그대로라 옆 글자는 밀리지 않는다")]
    [SerializeField] private float _legIconScale = 1.5f;
    [SerializeField] private TextMeshProUGUI _subtitle;

    [Header("설명")]
    [SerializeField] private GameObject _divider;
    [SerializeField] private TextMeshProUGUI _description;

    [Header("색")]
    [Tooltip("아이콘 줄 뒤 등급색 그라데이션의 진하기")]
    [Range(0f, 1f)][SerializeField] private float _bodyGlowAlpha = 0.35f;
    [Tooltip("이름 아래 등급색 선의 진하기")]
    [Range(0f, 1f)][SerializeField] private float _headerLineAlpha = 0.9f;
    [Tooltip("이름·등급 글자를 등급색에서 흰색 쪽으로 밝히는 정도")]
    [Range(0f, 1f)][SerializeField] private float _textLighten = 0.3f;
    [Tooltip("이름 아래 줄 스킬 이름의 글자 크기 (이름 대비 %)")]
    [Range(50, 100)][SerializeField] private int _skillNameSize = 80;
    [Tooltip("기본 등급 파츠에 쓰는 색")]
    [SerializeField] private Color _basicColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    public void Show(string name, Sprite icon, PartGrade grade, string subtitle, string description, bool isLeg = false)
    {
        _icon.rectTransform.localScale = Vector3.one * (isLeg ? _legIconScale : 1f);

        Color gradeColor = grade == PartGrade.Basic ? _basicColor : PartOutline.GradeColor(grade);
        Color textColor = Color.Lerp(gradeColor, Color.white, _textLighten);

        _headerLine.color = WithAlpha(gradeColor, _headerLineAlpha);
        _name.text = FormatName(name);
        _name.color = textColor;

        _bodyGlow.color = WithAlpha(gradeColor, _bodyGlowAlpha);
        _icon.sprite = icon;
        _icon.enabled = icon != null;

        _subtitle.text = subtitle;
        _subtitle.color = textColor;

        bool hasDescription = !string.IsNullOrEmpty(description);
        _description.text = description;
        _description.gameObject.SetActive(hasDescription);
        _divider.SetActive(hasDescription);

        // 크기 맞춤은 프레임 끝에 일어나서, 켜지는 첫 프레임에 이전 크기로 보이지 않게 바로 다시 잰다
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
    }

    // "A사 보급형 왼팔 [붐버]" → 이름 아래 줄에 스킬 이름을 조금 작게 쓴다
    private string FormatName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        int open = name.LastIndexOf('[');
        if (open < 0 || !name.EndsWith("]")) return name;

        string title = name.Substring(0, open).TrimEnd();
        string skill = name.Substring(open + 1, name.Length - open - 2).Trim();
        if (title.Length == 0 || skill.Length == 0) return name;

        return $"{title}\n<size={_skillNameSize}%>{skill}</size>";
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
