using System;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

/// <summary>
/// 단축키(F1~F5)로 테스트 씬을 전환하는 전역 씬 매니저.
/// 씬 목록은 인스펙터에서 수정할 수 있음.
/// </summary>
public class GameSceneManager : PersistentSingleton<GameSceneManager>
{
    [Serializable]
    public struct SceneShortcut
    {
        public KeyCode Key;
        public string SceneName;

        public SceneShortcut(KeyCode key, string sceneName)
        {
            Key = key;
            SceneName = sceneName;
        }
    }

    [SerializeField]
    private SceneShortcut[] _shortcuts =
    {
        new SceneShortcut(KeyCode.F1, "Map_Test"),
        new SceneShortcut(KeyCode.F2, "UiTest"),
        new SceneShortcut(KeyCode.F3, "Auction"),
        new SceneShortcut(KeyCode.F4, "JunkShop"),
        new SceneShortcut(KeyCode.F5, "Battle"),
    };

    [SerializeField]
    private bool _shortcutEnabled = true;

    private void Update()
    {
        if (!_shortcutEnabled || _shortcuts == null)
            return;

        for (int i = 0; i < _shortcuts.Length; i++)
        {
            if (Input.GetKeyDown(_shortcuts[i].Key))
            {
                LoadScene(_shortcuts[i].SceneName);
                return;
            }
        }
    }

    /// <summary>씬 이름으로 씬을 로드함. 빌드 설정에 없는 씬이면 에러 로그만 남기고 무시함.</summary>
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
