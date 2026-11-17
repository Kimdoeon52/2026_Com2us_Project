using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

// 씬 전환을 맡는 전역 매니저
public class GameSceneManager : PersistentSingleton<GameSceneManager>
{
    // 빌드 설정에 없는 씬이면 에러 로그만 남긴다
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[GameSceneManager] 씬 이름이 비어 있습니다.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[GameSceneManager] '{sceneName}' 씬을 로드할 수 없습니다. Build Settings에 등록되어 있는지 확인하세요.");
            return;
        }

        UnitySceneManager.LoadScene(sceneName);
    }
}
