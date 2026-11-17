using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance;

    [Header("UI Settings")]
    public GameObject loadingCanvas; // 켜고 끌 로딩 화면 (Canvas)

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            loadingCanvas.SetActive(false);
        }
        else 
        { 
            Destroy(gameObject); 
            return; 
        }
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneProcess(sceneName));
    }

    private IEnumerator LoadSceneProcess(string sceneName)
    {
        // 0. Build Settings에 없는 씬이면 로딩 씬으로 넘어가기 전에 중단 (넘어간 뒤엔 LoadSceneAsync가 null → 로딩 화면에 갇힘)
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[LoadingManager] '{sceneName}' 씬을 열 수 없습니다. File > Build Profiles(Build Settings)의 씬 목록에 추가했는지 확인하세요.");
            yield break;
        }

        loadingCanvas.SetActive(true); // 1. 로딩 화면 켜기

        yield return SceneManager.LoadSceneAsync("LoadingScene"); // 2. 빈 씬(LoadingScene)을 거쳐 메모리 정리

        // 3. 목표 씬 비동기 로딩 시작
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError($"[LoadingManager] '{sceneName}' 씬 로딩을 시작하지 못했습니다.");
            loadingCanvas.SetActive(false);
            yield break;
        }
        operation.allowSceneActivation = false; // 자동 씬 전환 방지

        while (operation.progress < 0.9f)
        {
            yield return null;
        }

        operation.allowSceneActivation = true;

        while (!operation.isDone)
        {
            yield return null;
        }

        loadingCanvas.SetActive(false);
    }
}
