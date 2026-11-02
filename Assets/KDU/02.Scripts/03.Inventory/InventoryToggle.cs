using System;
using UnityEngine;
using UnityEngine.Events;

// 키마다 인벤토리와 짝 패널을 같이 열고 닫는다. 렌더와 데이터는 건드리지 않는다
public class InventoryToggle : MonoBehaviour
{
    [Serializable]
    private class Mode
    {
        [Tooltip("열고 닫는 키")]
        public KeyCode Key = KeyCode.Tab;

        [Tooltip("인벤토리와 같이 열 패널")]
        public GameObject[] Panels;

        [Tooltip("키로 닫을 때 추가 처리")]
        public UnityEvent OnClose;

        [Tooltip("인벤토리 위치 변경")]
        public bool MoveRoot;

        [Tooltip("이 모드의 인벤토리 위치")]
        public Vector2 RootPosition;

        [Tooltip("인벤토리를 가운데에 맞출 칸. 지정하면 위치값 대신 사용")]
        public RectTransform RootAnchor;
    }

    [Tooltip("켜고 끌 인벤토리 UI 루트. 이 컴포넌트가 붙은 오브젝트를 지정하면 다시 켤 수 없다")]
    [SerializeField] private GameObject _inventoryRoot;

    [Tooltip("키별 모드")]
    [SerializeField] private Mode[] _modes = { new Mode() };

    [Tooltip("전부 닫는 키")]
    [SerializeField] private KeyCode _closeKey = KeyCode.Escape;

    private Mode _current;
    private RectTransform _rootRect;
    private Vector2 _rootHome;

    public bool IsOpen => _current != null;

    private void Start()
    {
        _rootRect = _inventoryRoot != null ? _inventoryRoot.transform as RectTransform : null;
        if (_rootRect != null)
            _rootHome = _rootRect.anchoredPosition;

        SetActive(_inventoryRoot, false);
        for (int i = 0; i < _modes.Length; i++)
        {
            SetPanels(_modes[i], false);
        }
    }

    private void Update()
    {
        // 패널이 밖에서 닫히면 나머지도 닫는다
        if (_current != null && !IsFullyOpen(_current))
            Close(false);

        if (_current != null && Input.GetKeyDown(_closeKey))
        {
            Close(true);
            return;
        }

        for (int i = 0; i < _modes.Length; i++)
        {
            if (!Input.GetKeyDown(_modes[i].Key))
                continue;

            bool same = _current == _modes[i];
            if (_current != null)
                Close(true);

            if (!same)
                Open(_modes[i]);

            return;
        }
    }

    private void Open(Mode mode)
    {
        _current = mode;

        PlaceRoot(mode);

        SetActive(_inventoryRoot, true);
        SetPanels(mode, true);
    }

    // 기준 칸이 있으면 그 중심에, 없으면 위치값에 둔다
    private void PlaceRoot(Mode mode)
    {
        if (_rootRect == null)
            return;

        _rootRect.anchoredPosition = mode.MoveRoot ? mode.RootPosition : _rootHome;

        if (mode.RootAnchor == null)
            return;

        Vector3 center = mode.RootAnchor.TransformPoint(mode.RootAnchor.rect.center);
        Vector3 offset = _rootRect.TransformPoint(_rootRect.rect.center) - _rootRect.position;
        _rootRect.position = center - offset;
    }

    // invokeHooks가 false면 이미 밖에서 닫힌 경우
    private void Close(bool invokeHooks)
    {
        Mode mode = _current;
        _current = null;

        // 훅에서 예외가 나도 창은 닫히게 먼저 끈다
        SetActive(_inventoryRoot, false);
        SetPanels(mode, false);

        if (invokeHooks)
            mode.OnClose?.Invoke();
    }

    private bool IsFullyOpen(Mode mode)
    {
        if (_inventoryRoot != null && !_inventoryRoot.activeSelf)
            return false;

        if (mode.Panels == null)
            return true;

        for (int i = 0; i < mode.Panels.Length; i++)
        {
            if (mode.Panels[i] != null && !mode.Panels[i].activeSelf)
                return false;
        }

        return true;
    }

    private void SetPanels(Mode mode, bool active)
    {
        if (mode.Panels == null)
            return;

        for (int i = 0; i < mode.Panels.Length; i++)
        {
            SetActive(mode.Panels[i], active);
        }
    }

    private void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }
}
