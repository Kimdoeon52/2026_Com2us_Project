using UnityEngine;

// 씬에서 골드 경계 구현을 찾는다
public static class GoldSources
{
    // 씬을 다시 불러오면 씬에 박힌 사본과 씬 전환 유지 인스턴스가 한 프레임 겹친다.
    // 사본은 그 프레임 끝에 파괴되므로 살아남는 쪽을 고른다
    public static IGoldSource Find()
    {
        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        IGoldSource fallback = null;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (!(behaviours[i] is IGoldSource gold))
                continue;

            // 씬 전환 유지 오브젝트는 빌드 인덱스가 없다
            if (behaviours[i].gameObject.scene.buildIndex < 0)
                return gold;

            if (fallback == null)
                fallback = gold;
        }

        return fallback;
    }

    // 인터페이스로 들고 있으면 파괴된 MonoBehaviour가 null로 잡히지 않는다
    public static bool IsAlive(IGoldSource gold)
    {
        if (gold == null)
            return false;

        return !(gold is Object obj) || obj != null;
    }
}
