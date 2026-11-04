using UnityEngine;

// 테스트용 코어 레벨업 키. Shift와 함께 누른다
public class DebugCoreLevelKey : MonoBehaviour
{
    [Tooltip("코어 레벨업 키")]
    [SerializeField] private KeyCode _key = KeyCode.X;

    // 에디터·개발 빌드에서만 씬마다 따로 배치하지 않아도 생긴다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (!Debug.isDebugBuild || FindAnyObjectByType<DebugCoreLevelKey>() != null)
            return;

        var go = new GameObject(nameof(DebugCoreLevelKey));
        DontDestroyOnLoad(go);
        go.AddComponent<DebugCoreLevelKey>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(_key) || !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            return;

        InventoryHost host = InventoryHost.Instance;
        if (host == null || host.Core == null)
            return;

        if (host.CoreLevel >= host.Core.MaxLevel)
        {
            Debug.Log($"[코어] 이미 최대 레벨이다. LV.{host.CoreLevel}");
            return;
        }

        host.Save.Core.SetLevel(host.CoreLevel + 1);
        host.NotifyChanged();
        Debug.Log($"[코어] LV.{host.CoreLevel}");
    }
}
