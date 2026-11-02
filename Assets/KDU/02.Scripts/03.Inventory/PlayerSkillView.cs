using TMPro;
using UnityEngine;

// 장착 파츠 스킬 목록. 장착이 바뀔 때마다 카드를 다시 채운다
public class PlayerSkillView : MonoBehaviour
{
    [Tooltip("스킬 매핑")]
    [SerializeField] private PartSkillCatalog _catalog;

    [Tooltip("헤더 글자")]
    [SerializeField] private TextMeshProUGUI _header;

    [Tooltip("헤더 형식. {0} = 장착 스킬 수")]
    [SerializeField] private string _headerFormat = "SKILLS // {0}";

    private SkillCardView[] _cards;
    private InventoryHost _host;

    private void Awake()
    {
        _cards = GetComponentsInChildren<SkillCardView>(true);
    }

    private void OnEnable()
    {
        Hook();
    }

    // 씬 시작 시 OnEnable이 InventoryHost.Awake보다 먼저 불릴 수 있다
    private void Start()
    {
        Hook();
    }

    private void OnDisable()
    {
        if (_host != null)
            _host.Changed -= Refresh;

        _host = null;
    }

    private void Hook()
    {
        if (_host != null || InventoryHost.Instance == null)
            return;

        _host = InventoryHost.Instance;
        _host.Changed += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        if (_host == null || _cards == null)
            return;

        int count = 0;
        for (int i = 0; i < _cards.Length; i++)
        {
            PartsDefinition part = _host.GetEquipped(_cards[i].Slot);
            if (part == null || string.IsNullOrEmpty(part.ActiveSkillId))
            {
                _cards[i].ShowEmpty();
                continue;
            }

            PartSkillCatalog.Entry entry = default;
            if (_catalog != null)
                _catalog.TryFind(part.ActiveSkillId, out entry);

            _cards[i].Show(part, entry.DisplayName, entry.TypeLabel);
            count++;
        }

        if (_header != null)
            _header.text = string.Format(_headerFormat, count);
    }
}
