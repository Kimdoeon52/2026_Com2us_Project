using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance;

    [Header("UI Settings")]
    public GameObject loadingCanvas; // 켜고 끌 로딩 화면 (Canvas)

    // 로딩 화면 연출(LHM LoadingScreenView)이 읽는 값. 0~1 진행률 / 이동할 씬 이름 / 로딩 중 여부
    public static float Progress { get; private set; }
    public static string TargetScene { get; private set; } = "";
    public static bool IsLoading { get; private set; }

    // 로딩 화면이 켜 두면, 다 불러온 뒤 Continue()가 불릴 때까지(아무 키 입력) 씬 전환을 기다림. 꺼져 있으면 바로 전환
    public static bool HoldUntilContinue { get; set; }
    public static bool IsWaitingForContinue { get; private set; }
    private static bool continueRequested;

    public static void Continue()
    {
        if (IsWaitingForContinue) continueRequested = true;
    }

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

        TargetScene = sceneName;
        Progress = 0f;
        IsLoading = true;
        loadingCanvas.SetActive(true); // 1. 로딩 화면 켜기

        yield return SceneManager.LoadSceneAsync("LoadingScene"); // 2. 빈 씬(LoadingScene)을 거쳐 메모리 정리
        Progress = 0.1f;

        // 3. 목표 씬 비동기 로딩 시작
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError($"[LoadingManager] '{sceneName}' 씬 로딩을 시작하지 못했습니다.");
            IsLoading = false;
            loadingCanvas.SetActive(false);
            yield break;
        }
        operation.allowSceneActivation = false; // 자동 씬 전환 방지

        while (operation.progress < 0.9f)
        {
            Progress = 0.1f + 0.9f * (operation.progress / 0.9f);
            yield return null;
        }

        Progress = 1f;

        // 4. 로딩 화면이 원하면 아무 키 입력까지 대기 (화면에 100%와 안내 문구가 보인 뒤 입력)
        continueRequested = false;
        IsWaitingForContinue = HoldUntilContinue;
        while (IsWaitingForContinue && HoldUntilContinue && !continueRequested)
        {
            yield return null;
        }
        IsWaitingForContinue = false;

        operation.allowSceneActivation = true;

        while (!operation.isDone)
        {
            yield return null;
        }

        IsLoading = false;
        loadingCanvas.SetActive(false);
    }
}
