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
        }
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneProcess(sceneName));
    }

    private IEnumerator LoadSceneProcess(string sceneName)
    {
        loadingCanvas.SetActive(true); // 1. 로딩 화면 켜기

        yield return SceneManager.LoadSceneAsync("LoadingScene"); // 2. 빈 씬(LoadingScene)을 거쳐 메모리 정리

        // 3. 목표 씬 비동기 로딩 시작
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
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
