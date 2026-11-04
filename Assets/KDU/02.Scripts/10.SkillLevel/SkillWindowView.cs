using TMPro;
using UnityEngine;

// 스킬 창. 장착 파츠의 레벨업 가능한 스킬을 줄로 보여 주고 포인트를 찍는다
public class SkillWindowView : MonoBehaviour
{
    [Tooltip("켜고 끌 창")]
    [SerializeField] private GameObject _panel;

    [Tooltip("스킬 포인트 글자")]
    [SerializeField] private TextMeshProUGUI _pointsLabel;

    [Tooltip("포인트 형식. {0} = 남은 포인트")]
    [SerializeField] private string _pointsFormat = "스킬 포인트 {0}";

    [Tooltip("표시할 스킬이 없을 때 안내")]
    [SerializeField] private GameObject _emptyNotice;

    [Tooltip("열고 닫는 키")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.V;

    [Tooltip("닫는 키")]
    [SerializeField] private KeyCode _closeKey = KeyCode.Escape;

    private SkillRowView[] _rows;
    private SkillLevelSystem _system;
    private SkillRowView _selected;
    private Slot _selectedSlot;
    private bool _open;

    private void Awake()
    {
        _rows = GetComponentsInChildren<SkillRowView>(true);
        for (int i = 0; i < _rows.Length; i++)
        {
            _rows[i].Init(Select, LevelDown, LevelUp);
        }

        _panel.SetActive(false);
    }

    private void Update()
    {
        if (_open)
        {
            if (Input.GetKeyDown(_toggleKey) || Input.GetKeyDown(_closeKey))
                Close();

            return;
        }

        // 미니게임·대화 중에는 열지 않는다
        if (Input.GetKeyDown(_toggleKey) && !ScrapInputGate.IsBlocked)
            Open();
    }

    // 씬 전환으로 꺼져도 입력 차단이 남지 않게. 비활성화 도중이라 창은 건드리지 않는다
    private void OnDisable()
    {
        Release();
    }

    private void Open()
    {
        _system = SkillLevelSystem.Instance;
        if (_system == null)
        {
            Debug.LogWarning("SkillLevelSystem이 없다.", this);
            return;
        }

        _open = true;
        ScrapInputGate.Push();
        _system.Changed += Refresh;
        Refresh();
        _panel.SetActive(true);
    }

    private void Close()
    {
        Release();
        _panel.SetActive(false);
    }

    private void Release()
    {
        if (!_open)
            return;

        _open = false;
        ScrapInputGate.Pop();
        _system.Changed -= Refresh;
    }

    private void Refresh()
    {
        _pointsLabel.text = string.Format(_pointsFormat, _system.AvailablePoints);

        int shown = 0;
        SkillRowView selected = null;
        for (int i = 0; i < SkillLevelSystem.SkillSlots.Length && shown < _rows.Length; i++)
        {
            Slot slot = SkillLevelSystem.SkillSlots[i];
            if (!_system.TryGetSkill(slot, out PartSkillCatalog.Entry entry))
                continue;

            SkillRowView row = _rows[shown++];
            row.Show(slot, entry.DisplayName, _system.GetLevel(slot), _system.CanLevelDown(slot), _system.CanLevelUp(slot));

            if (_selected != null && slot == _selectedSlot)
                selected = row;
        }

        for (int i = shown; i < _rows.Length; i++)
        {
            _rows[i].gameObject.SetActive(false);
        }

        if (_emptyNotice != null)
            _emptyNotice.SetActive(shown == 0);

        // 선택한 부위가 사라지면 첫 줄로
        ApplySelection(selected != null ? selected : shown > 0 ? _rows[0] : null);
    }

    private void ApplySelection(SkillRowView row)
    {
        _selected = row;
        if (row != null)
            _selectedSlot = row.Slot;

        for (int i = 0; i < _rows.Length; i++)
        {
            _rows[i].SetSelected(_rows[i] == row);
        }
    }

    private void Select(SkillRowView row)
    {
        ApplySelection(row);
    }

    private void LevelUp(SkillRowView row)
    {
        ChangeLevel(row, _system.TryLevelUp(row.Slot));
    }

    private void LevelDown(SkillRowView row)
    {
        ChangeLevel(row, _system.TryLevelDown(row.Slot));
    }

    // 레벨을 바꾼 줄을 선택 상태로 둔다
    private void ChangeLevel(SkillRowView row, bool changed)
    {
        ApplySelection(row);

        if (changed)
            row.PlayLevelChanged();
    }
}
