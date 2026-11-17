using UnityEngine;
/// <summary>
/// 핵심 UI를 모은 프리팹 Root. 파괴 방지용 코드
/// </summary>
public class CoreSystem_UI : MonoBehaviour
{
    public static CoreSystem_UI Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else{ Destroy(gameObject); }
    }
}
