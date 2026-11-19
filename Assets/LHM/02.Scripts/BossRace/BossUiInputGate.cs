using RealSteel.Dialogue;
using RealSteel.Tutorial;
using UnityEngine;

namespace RealSteel.BossRace
{
    // 보스 지역 · 난이도 선택 화면 위에 튜토리얼 안내 대사 · 창이 떠 있는 동안 화면 입력을 막음 (BUG-047)
    // 안내 대사를 넘기는 Space · E가 화면의 '확정'으로도 읽혀 선택이 먼저 끝나 버리던 문제
    internal static class BossUiInputGate
    {
        private const float SearchInterval = 1f;

        private static DialogueManager manager;
        private static float nextSearch;

        public static bool IsBlocked()
        {
            TutorialDirector tutorial = TutorialDirector.Instance;
            if (tutorial != null && tutorial.IsBusy) return true;

            DialogueManager dialogue = FindManager();
            return dialogue != null && dialogue.IsTalking;
        }

        private static DialogueManager FindManager()
        {
            if (manager != null && manager.isActiveAndEnabled) return manager;
            if (Time.unscaledTime < nextSearch) return null;

            nextSearch = Time.unscaledTime + SearchInterval;
            manager = Object.FindAnyObjectByType<DialogueManager>();
            return manager;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            manager = null;
            nextSearch = 0f;
        }
    }
}
