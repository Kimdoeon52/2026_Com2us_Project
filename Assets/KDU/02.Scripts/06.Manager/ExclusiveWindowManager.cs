using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// 등록한 창 중 하나가 켜지면 나머지를 닫는다. 씬마다 하나 두고, 씬을 넘어 남는 창은 실행 중에 찾아 등록한다
public class ExclusiveWindowManager : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        [Tooltip("이름")]
        public string Name;

        [Tooltip("감시할 창")]
        public GameObject Root;

        [Tooltip("이 창이 켜질 때 계속 켜 둘 오브젝트. 다른 창과 함께 쓰는 인벤토리 그리드")]
        public GameObject Companion;

        [Tooltip("닫기 처리. 비우면 창만 끈다")]
        public UnityEvent OnForceClose = new UnityEvent();

        [Tooltip("단축키. None이면 쓰지 않는다")]
        public KeyCode Key = KeyCode.None;

        [Tooltip("Shift 동시 입력")]
        public bool Shift;

        [Tooltip("단축키 열기/닫기 처리")]
        public UnityEvent OnToggle = new UnityEvent();
    }

    private const string AutoName = "ExclusiveWindowManager (Auto)";

    [Tooltip("서로 배타적인 창")]
    [SerializeField] private List<Entry> _entries = new List<Entry>();

    private bool[] _wasOpen;
    private bool _coreRegistered;

#if UNITY_EDITOR
    public List<Entry> EditorEntries => _entries;
#endif

    // 실행 중에 창을 추가한다. 같은 창은 한 번만 등록된다
    public void Register(Entry entry)
    {
        if (entry == null || entry.Root == null || Contains(entry.Root))
            return;

        _entries.Add(entry);
    }

    private void Update()
    {
        // 지도·달력·랭킹은 상주 프리팹이 늦게 생길 수 있어 찾을 때까지 시도한다
        if (!_coreRegistered)
            _coreRegistered = CoreSystemWindows.TryRegister(this);

        // 미니게임 중에는 단축키로 창을 열지 않는다
        if (ScrapMinigameBase.IsAnyRunning)
            return;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        for (int i = 0; i < _entries.Count; i++)
        {
            Entry entry = _entries[i];
            if (entry.Key == KeyCode.None || (entry.Shift && !shift) || !Input.GetKeyDown(entry.Key))
                continue;

            Toggle(entry);
            return;
        }
    }

    // 열 때는 다른 창을 먼저 닫는다
    private void Toggle(Entry entry)
    {
        if (IsOpen(entry))
        {
            entry.OnToggle?.Invoke();
            return;
        }

        if (!TimeSystemWindowGate.CanOpen(IsAnyOpen()))
            return;

        CloseAllExcept(entry);
        entry.OnToggle?.Invoke();
    }

    // 창을 여는 Update가 모두 끝난 뒤에 본다
    private void LateUpdate()
    {
        // 첫 프레임과 창이 추가된 프레임은 상태만 기록한다
        if (_wasOpen == null || _wasOpen.Length != _entries.Count)
        {
            _wasOpen = new bool[_entries.Count];
            Record();
            return;
        }

        int opened = FindOpened();
        if (opened >= 0)
            CloseAllExcept(_entries[opened]);

        Record();
    }

    private int FindOpened()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (IsOpen(_entries[i]) && !_wasOpen[i])
                return i;
        }

        return -1;
    }

    private void CloseAllExcept(Entry keep)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            Entry entry = _entries[i];
            if (entry != keep && IsOpen(entry))
                Close(entry);
        }

        // 다른 창을 닫으면서 함께 꺼졌을 수 있다
        if (keep.Companion != null && !keep.Companion.activeSelf)
            keep.Companion.SetActive(true);
    }

    private static void Close(Entry entry)
    {
        entry.OnForceClose?.Invoke();

        if (entry.Root != null && entry.Root.activeSelf)
            entry.Root.SetActive(false);
    }

    private bool IsAnyOpen()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (IsOpen(_entries[i]))
                return true;
        }

        return false;
    }

    private bool Contains(GameObject root)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Root == root)
                return true;
        }

        return false;
    }

    private void Record()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            _wasOpen[i] = IsOpen(_entries[i]);
        }
    }

    private static bool IsOpen(Entry entry)
    {
        return entry.Root != null && entry.Root.activeInHierarchy;
    }

    // 매니저를 두지 않은 씬에서도 지도 단축키가 동작하도록 없으면 만든다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Subscribe()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindAnyObjectByType<ExclusiveWindowManager>() != null)
            return;

        var go = new GameObject(AutoName);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<ExclusiveWindowManager>();
    }
}
