using UnityEngine;

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
