using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// 장착 팝업의 플레이어 스탯 표시. 장착이 바뀔 때마다 다시 그린다
public class PlayerStatView : MonoBehaviour
{
    [Tooltip("스탯 텍스트")]
    [SerializeField] private TextMeshProUGUI _text;

    [Tooltip("제목")]
    [SerializeField] private string _title = "스펙";

    [Tooltip("골드 라벨")]
    [SerializeField] private string _goldLabel = "골드";

    private readonly StringBuilder _builder = new StringBuilder();
    private IPlayerStatSource _source = new DummyPlayerStatSource();
    private IGoldSource _gold;
    private InventoryHost _host;

    // 실제 스탯 계산을 붙일 때 호출한다
    public void Bind(IPlayerStatSource source)
    {
        _source = source ?? new DummyPlayerStatSource();
        Refresh();
    }

    private void OnEnable()
    {
        Hook();
    }

    // 씬 시작 시 OnEnable이 InventoryHost.Awake보다 먼저 불릴 수 있다
    private void Start()
    {
        Hook();

        if (_host == null)
            Debug.LogWarning("InventoryHost가 없다.");
    }

    private void OnDisable()
    {
        if (_host != null)
            _host.Changed -= Refresh;

        _host = null;
    }

    private void Hook()
    {
        if (!IsAlive(_gold))
            _gold = FindGoldSource();

        if (_host != null || InventoryHost.Instance == null)
            return;

        _host = InventoryHost.Instance;
        _host.Changed += Refresh;
        Refresh();
    }

    // 씬을 다시 불러오면 씬에 박힌 사본과 씬 전환 유지 인스턴스가 한 프레임 겹친다.
    // 사본은 그 프레임 끝에 파괴되므로 살아남는 쪽을 고른다
    private static IGoldSource FindGoldSource()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        IGoldSource fallback = null;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (!(behaviours[i] is IGoldSource gold))
                continue;

            // 씬 전환 유지 오브젝트는 빌드 인덱스가 없다
            if (behaviours[i].gameObject.scene.buildIndex < 0)
                return gold;

            if (fallback == null)
                fallback = gold;
        }

        return fallback;
    }

    // 인터페이스로 들고 있으면 파괴된 MonoBehaviour가 null로 잡히지 않는다
    private static bool IsAlive(IGoldSource gold)
    {
        if (gold == null)
            return false;

        return !(gold is Object obj) || obj != null;
    }

    public void Refresh()
    {
        if (_text == null || _host == null)
            return;

        // 씬을 다시 불러온 뒤면 캐시한 원본이 파괴된 사본일 수 있다
        if (!IsAlive(_gold))
            _gold = FindGoldSource();

        IReadOnlyList<StatLine> lines = _source.GetStats(_host.GetEquippedPartIds());

        _builder.Clear();
        if (!string.IsNullOrEmpty(_title))
            _builder.Append("<b>").Append(_title).Append("</b>\n");

        if (lines != null)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                AppendLine(lines[i].Label, lines[i].Value);
            }
        }

        if (IsAlive(_gold))
            AppendLine(_goldLabel, _gold.PlayerGold.ToString("N0"));

        _text.text = _builder.ToString();
    }

    // 라벨 왼쪽, 값 오른쪽을 같은 줄에
    private void AppendLine(string label, string value)
    {
        _builder.Append("<align=left>").Append(label).Append("<line-height=0>\n");
        _builder.Append("<align=right>").Append(value).Append("<line-height=1em>\n");
    }
}
