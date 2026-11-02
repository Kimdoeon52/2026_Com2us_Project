using TMPro;
using UnityEngine;

// 로봇 상태 카드와 수리 경고. 장착이나 내구도가 바뀔 때마다 다시 그린다
public class RobotStatusView : MonoBehaviour
{
    [Header("제목")]
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _subtitle;

    [Tooltip("기체 이름")]
    [SerializeField] private string _robotName = "R-07";

    [Header("수리 경고")]
    [SerializeField] private GameObject _repairRoot;
    [SerializeField] private TextMeshProUGUI _repairDetail;

    [Tooltip("이 비율 미만이면 수리 경고")]
    [Range(0f, 1f)][SerializeField] private float _repairThreshold = 0.4f;

    [Header("색")]
    [Tooltip("이 비율 이상이면 높음 색")]
    [Range(0f, 1f)][SerializeField] private float _highThreshold = 0.8f;

    [SerializeField] private Color _highColor = new Color(1f, 0.91f, 0f);
    [SerializeField] private Color _midColor = new Color(1f, 0.54f, 0.12f);
    [SerializeField] private Color _lowColor = new Color(1f, 0.29f, 0.23f);
    [SerializeField] private Color _valueColor = new Color(0.93f, 0.93f, 0.91f);

    private RobotStatusRow[] _rows;
    private InventoryHost _host;

    private void Awake()
    {
        _rows = GetComponentsInChildren<RobotStatusRow>(true);
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
        if (_host == null || _rows == null)
            return;

        SetText(_title, _robotName);
        CoreDefinition core = _host.Core;
        SetText(_subtitle, core != null ? $"{core.DisplayName} · LV.{_host.CoreLevel}" : "코어 없음");

        string worstName = null;
        float worstRatio = 1f;

        for (int i = 0; i < _rows.Length; i++)
        {
            RobotStatusRow row = _rows[i];
            if (row.Slot == Slot.Core)
            {
                ShowCore(row, core);
                continue;
            }

            PartsDefinition part = _host.GetEquipped(row.Slot);
            if (part == null)
            {
                row.ShowEmpty();
                continue;
            }

            float ratio = part.Durability > 0 ? (float)_host.GetDurability(row.Slot) / part.Durability : 0f;
            Color color = GetColor(ratio);
            row.Show(part.DisplayName, ratio, color, $"{Mathf.RoundToInt(ratio * 100f)}%", ratio < _repairThreshold ? _lowColor : _valueColor);

            if (ratio < worstRatio)
            {
                worstRatio = ratio;
                worstName = part.DisplayName;
            }
        }

        RefreshRepair(worstName, worstRatio);
    }

    // 코어는 내구도 대신 안정 여부를 막대로
    private void ShowCore(RobotStatusRow row, CoreDefinition core)
    {
        if (core == null)
        {
            row.ShowEmpty();
            return;
        }

        bool stable = _host.CanSortie;
        row.Show(core.DisplayName, stable ? 1f : 0f, stable ? _highColor : _lowColor, $"LV.{_host.CoreLevel}", stable ? _valueColor : _lowColor);
    }

    private void RefreshRepair(string worstName, float worstRatio)
    {
        bool need = worstName != null && worstRatio < _repairThreshold;
        if (_repairRoot != null)
            _repairRoot.SetActive(need);

        if (need)
            SetText(_repairDetail, $"{worstName} DURABILITY {Mathf.RoundToInt(worstRatio * 100f)}%\nREPAIR BEFORE NEXT BOUT");
    }

    private Color GetColor(float ratio)
    {
        if (ratio >= _highThreshold)
            return _highColor;

        return ratio >= _repairThreshold ? _midColor : _lowColor;
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
            label.text = value;
    }
}
