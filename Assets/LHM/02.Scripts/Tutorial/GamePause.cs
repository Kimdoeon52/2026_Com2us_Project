using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Tutorial
{
    // 창이 떠 있는 동안 게임 전체 정지 (Time.timeScale = 0)
    //  - 여러 창이 겹쳐도 마지막 창이 닫힐 때 한 번만 원래 속도로 되돌림 (도움말 목록 → 튜토리얼 창 전환 중에도 계속 정지)
    //  - 되돌릴 때는 멈추기 직전 값을 씀 (경매 포기 배속처럼 1이 아닌 속도도 유지)
    //  - 정지 중에 다른 코드가 속도를 바꿨으면 그 값을 존중하고 덮어쓰지 않음
    public static class GamePause
    {
        private static readonly HashSet<Object> owners = new HashSet<Object>();
        private static float savedTimeScale = 1f;

        public static bool IsPaused => owners.Count > 0;

        public static void Push(Object owner)
        {
            if (owner == null || !owners.Add(owner)) return;
            if (owners.Count > 1) return;

            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            Debug.Log($"[GamePause] 게임 정지 ({owner.name}), 이전 속도 {savedTimeScale:0.##}");
        }

        public static void Remove(Object owner)
        {
            if (!owners.Remove(owner)) return;
            owners.RemoveWhere(o => o == null);
            if (owners.Count > 0) return;

            if (Time.timeScale == 0f) Time.timeScale = savedTimeScale;
            Debug.Log($"[GamePause] 게임 재개, 속도 {Time.timeScale:0.##}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            owners.Clear();
            savedTimeScale = 1f;
        }
    }
}
