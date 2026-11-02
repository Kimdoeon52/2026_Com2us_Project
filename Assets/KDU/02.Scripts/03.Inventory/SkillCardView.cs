using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스킬 카드 1장. 이름·종류 태그·코스트 칸을 그린다
public class SkillCardView : MonoBehaviour
{
    [Tooltip("부위")]
    [SerializeField] private Slot _slot;

    [Tooltip("스킬 이름")]
    [SerializeField] private TextMeshProUGUI _nameLabel;

    [Tooltip("부위·파츠 이름")]
    [SerializeField] private TextMeshProUGUI _partLabel;

    [Tooltip("종류 태그 루트")]
    [SerializeField] private GameObject _tagRoot;

    [Tooltip("종류 태그 글자")]
    [SerializeField] private TextMeshProUGUI _tagLabel;

    [Tooltip("코스트 칸. 앞에서부터 켠다")]
    [SerializeField] private Image[] _costPips;

    [Tooltip("빈 카드 문구")]
    [SerializeField] private string _emptyText = "비어 있음";

    [Tooltip("빈 카드 투명도")]
    [Range(0f, 1f)][SerializeField] private float _emptyAlpha = 0.4f;

    private CanvasGroup _group;

    public Slot Slot => _slot;

    private void Awake()
    {
        _group = gameObject.GetOrAddComponent<CanvasGroup>();
    }

    public void Show(PartsDefinition part, string skillName, string typeLabel)
    {
        SetAlpha(1f);
        SetText(_nameLabel, string.IsNullOrEmpty(skillName) ? part.ActiveSkillId : skillName);
        SetText(_partLabel, part.DisplayName);

        bool hasTag = !string.IsNullOrEmpty(typeLabel);
        if (_tagRoot != null)
            _tagRoot.SetActive(hasTag);

        if (hasTag)
            SetText(_tagLabel, typeLabel);

        SetCost(part.SkillCost);
    }

    public void ShowEmpty()
    {
        SetAlpha(_emptyAlpha);
        SetText(_nameLabel, _emptyText);
        SetText(_partLabel, "");

        if (_tagRoot != null)
            _tagRoot.SetActive(false);

        SetCost(0);
    }

    private void SetCost(int cost)
    {
        if (_costPips == null)
            return;

        for (int i = 0; i < _costPips.Length; i++)
        {
            if (_costPips[i] != null)
                _costPips[i].gameObject.SetActive(i < cost);
        }
    }

    private void SetAlpha(float alpha)
    {
        if (_group == null)
            _group = gameObject.GetOrAddComponent<CanvasGroup>();

        _group.alpha = alpha;
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
            label.text = value;
    }
}
