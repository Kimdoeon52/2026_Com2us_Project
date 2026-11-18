using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue
{
    // 씬 이동 공용. LJS LoadingManager(로딩 화면)가 있으면 그걸로, 없으면 비동기 로드
    // LJS 파일이라 타입 이름으로만 찾고 리플렉션으로 호출 (LJS 코드 수정 없음)
    public static class SceneLoader
    {
        private const string LoadingTypeName = "LoadingManager";

        public static void Load(string sceneName, GameObject loadingScreenPrefab = null)
        {
            MonoBehaviour loading = FindLoadingManager();
            if (loading == null && loadingScreenPrefab != null) loading = SpawnLoadingManager(loadingScreenPrefab);

            MethodInfo load = loading != null ? loading.GetType().GetMethod("LoadScene", new[] { typeof(string) }) : null;
            if (load != null)
            {
                load.Invoke(loading, new object[] { sceneName });
                return;
            }

            if (SceneManager.LoadSceneAsync(sceneName) == null)
            {
                Debug.LogError($"[SceneLoader] '{sceneName}' 씬 로딩을 시작하지 못했습니다. Build Settings를 확인하세요.");
            }
        }

        private static MonoBehaviour FindLoadingManager()
        {
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour != null && behaviour.GetType().Name == LoadingTypeName) return behaviour;
            }
            return null;
        }

        private static MonoBehaviour SpawnLoadingManager(GameObject prefab)
        {
            GameObject instance = Object.Instantiate(prefab);
            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == LoadingTypeName) return behaviour;
            }
            Debug.LogWarning($"[SceneLoader] 로딩 프리팹 '{prefab.name}'에 {LoadingTypeName}가 없습니다. 바로 이동합니다.");
            Object.Destroy(instance);
            return null;
        }
    }
}
