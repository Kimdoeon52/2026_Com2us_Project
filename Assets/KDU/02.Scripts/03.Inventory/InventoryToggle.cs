using UnityEngine;

// 인벤토리 UI 열고 닫기. 렌더와 데이터는 건드리지 않는다
public class InventoryToggle : MonoBehaviour
{
    [Tooltip("켜고 끌 인벤토리 UI 루트. 이 컴포넌트가 붙은 오브젝트를 지정하면 다시 켤 수 없다")]
    [SerializeField] private GameObject _inventoryRoot;

    [Tooltip("열고 닫는 키")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.Tab;

    [Tooltip("시작할 때 열어둘지")]
    [SerializeField] private bool _openOnStart;

    public bool IsOpen => _inventoryRoot != null && _inventoryRoot.activeSelf;

    private void Start() => SetOpen(_openOnStart);

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey))
            SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        if (_inventoryRoot != null)
            _inventoryRoot.SetActive(open);
    }
}
