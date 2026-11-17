using RealSteel.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Tutorial
{
    // ======================== [TEST ONLY] 테스트 전용 ========================
    // 용도: Insert = 튜토리얼 화면 시험 · Shift+Insert 본 기록 지우기
    // 위치: TutorialSetup이 필요할 때 씬에 배치
    // 실제 게임 로직이 아님. 출시 빌드 씬/프리팹에 넣지 말 것
    // ==========================================================================
    public class TutorialTest : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private TutorialPanel panel;
        [SerializeField] private TutorialGuide guide;

        [Header("테스트")]
        [SerializeField] private Key openKey = Key.Insert;
        [Tooltip("Play 시작 때 아직 안 본 튜토리얼이면 자동으로 열기")]
        [SerializeField] private bool openOnceOnStart;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Start()
        {
            if (panel == null || guide == null)
            {
                Debug.LogError("[Tutorial] Panel 또는 Guide가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            Debug.Log($"[Tutorial] {openKey}: 튜토리얼 열기 / Shift+{openKey}: 본 기록 지우기");
            if (openOnceOnStart) panel.OpenOnce(guide);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || panel.IsOpen || !keyboard[openKey].wasPressedThisFrame) return;

            if (keyboard.shiftKey.isPressed)
            {
                TutorialPanel.ResetSeen(guide.guideId);
                Debug.Log($"[Tutorial] '{guide.guideId}' 본 기록을 지웠습니다.");
                return;
            }

            if (UiStack.Current != null && UiStack.Current.Count > 0)
            {
                Debug.Log("[Tutorial] 다른 UI가 열려 있어 열지 않습니다.");
                return;
            }

            panel.Open(guide);
        }
#endif
    }
}
