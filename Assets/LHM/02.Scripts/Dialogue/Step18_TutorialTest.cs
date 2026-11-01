using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public class Step18_TutorialTest : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private DialogueManager manager;

        [Header("UI 직접 재생 튜토리얼")]
        [SerializeField] private string mapTutorialId = "tutorial_t3_map";
        [SerializeField] private string huntApplyTutorialId = "tutorial_t9_hunt_apply";

        private readonly object mapUi = new object();
        private readonly object huntApplyUi = new object();
        private bool mapOpen;
        private bool huntApplyOpen;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Start()
        {
            if (manager == null)
            {
                Debug.LogError("[Step18] Manager가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            Debug.Log("[Step18] O: 가짜 지도 UI 열기/닫기 (T3) / I: 가짜 토벌 신청 UI 열기/닫기 (T9) — 처음 열 때만 튜토리얼이 UI 위에서 바로 재생");
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !manager.IsReady || manager.IsTalking) return;

            if (keyboard.oKey.wasPressedThisFrame) Toggle(mapUi, ref mapOpen, "지도", mapTutorialId);
            else if (keyboard.iKey.wasPressedThisFrame) Toggle(huntApplyUi, ref huntApplyOpen, "토벌 신청", huntApplyTutorialId);
        }

        private void OnDisable()
        {
            if (mapOpen) UiStack.Remove(mapUi);
            if (huntApplyOpen) UiStack.Remove(huntApplyUi);
            mapOpen = huntApplyOpen = false;
        }

        private void Toggle(object ui, ref bool open, string label, string tutorialId)
        {
            open = !open;

            if (!open)
            {
                UiStack.Remove(ui);
                Debug.Log($"[Step18] 가짜 {label} UI 닫힘 (UI 스택 {UiStack.Current?.Count ?? 0})");
                return;
            }

            UiStack.Push(ui);
            bool played = manager.PlayDialogueIfAvailable(tutorialId);
            Debug.Log($"[Step18] 가짜 {label} UI 열림 (UI 스택 {UiStack.Current?.Count ?? 0}) — 튜토리얼 {tutorialId}: {(played ? "재생" : "이미 봄 / 없음")}");
        }
#endif
    }
}
