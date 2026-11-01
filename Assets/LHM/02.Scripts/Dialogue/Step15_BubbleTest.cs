using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public class Step15_BubbleTest : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private DialogueManager manager;
        [SerializeField] private DialogueEventQueue queue;
        [SerializeField] private CrowdBubbles crowd;
        [SerializeField] private NpcBubbles npcBubbles;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Start()
        {
            if (manager == null)
            {
                Debug.LogError("[Step15] Manager가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            Debug.Log("[Step15] H: 귀가 처리(트리거 평가 → 반응 말풍선 대기) / J: 관중 켜기·끄기 / K: 관중 말풍선 1개 바로 / Shift+K: 대기 목록 / Shift+H: 토벌 난이도(none→veryEasy→hell) / Shift+J: 기본 파츠 사용 전환");
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !manager.IsReady || manager.IsTalking) return;

            bool shift = keyboard.shiftKey.isPressed;

            if (keyboard.hKey.wasPressedThisFrame && shift)
            {
                string current = manager.Variables.Get("hunt.targetDifficulty");
                string next = current == "none" ? "veryEasy" : current == "veryEasy" ? "hell" : "none";
                manager.Variables.Set("hunt.targetDifficulty", next);
                Debug.Log($"[Step15] hunt.targetDifficulty = {next}");
            }
            else if (keyboard.hKey.wasPressedThisFrame)
            {
                if (queue != null) queue.Evaluate("귀가");
                else Debug.LogWarning("[Step15] EventQueue가 없습니다. 11-2단계 배치를 먼저 실행하세요.", this);
            }
            else if (keyboard.jKey.wasPressedThisFrame && shift)
            {
                string next = manager.Variables.Get("player.usingBasicParts") == "yes" ? "no" : "yes";
                manager.Variables.Set("player.usingBasicParts", next);
                Debug.Log($"[Step15] player.usingBasicParts = {next}");
            }
            else if (keyboard.jKey.wasPressedThisFrame && crowd != null)
            {
                crowd.Running = !crowd.Running;
                Debug.Log($"[Step15] 관중 말풍선 {(crowd.Running ? "켜짐" : "꺼짐")}");
            }
            else if (keyboard.kKey.wasPressedThisFrame)
            {
                if (shift)
                {
                    Debug.Log($"[Step15] 이벤트 큐 대기: {(queue != null ? queue.Dump() : "(큐 없음)")} / 반응 말풍선 대기 {(npcBubbles != null ? npcBubbles.PendingCount : 0)}개");
                }
                else if (crowd != null && !crowd.ShowOne())
                {
                    Debug.Log("[Step15] 관중 말풍선 생략 (동시 3개 / 빈 관중 없음)");
                }
            }
        }
#endif
    }
}
