using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 등록한 창 중 하나가 켜지면 나머지를 닫는다. 창 코드를 고치지 않고 켜지는 순간만 감시한다
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
    }

    [Tooltip("서로 배타적인 창")]
    [SerializeField] private List<Entry> _entries = new List<Entry>();

    private bool[] _wasOpen;

#if UNITY_EDITOR
    public List<Entry> EditorEntries => _entries;
#endif

    // 창을 여는 Update가 모두 끝난 뒤에 본다
    private void LateUpdate()
    {
        // 첫 프레임은 시작 상태만 기록한다
        if (_wasOpen == null)
        {
            _wasOpen = new bool[_entries.Count];
            Record();
            return;
        }

        int opened = FindOpened();
        if (opened >= 0)
            CloseOthers(opened);

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

    private void CloseOthers(int opened)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (i != opened && IsOpen(_entries[i]))
                Close(_entries[i]);
        }

        // 다른 창을 닫으면서 함께 꺼졌을 수 있다
        GameObject companion = _entries[opened].Companion;
        if (companion != null && !companion.activeSelf)
            companion.SetActive(true);
    }

    private static void Close(Entry entry)
    {
        entry.OnForceClose?.Invoke();

        if (entry.Root.activeSelf)
            entry.Root.SetActive(false);
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
}
