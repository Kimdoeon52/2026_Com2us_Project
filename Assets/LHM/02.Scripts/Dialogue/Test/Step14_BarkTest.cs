using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    // ======================== [TEST ONLY] 테스트 전용 ========================
    // 용도: 6 ~ 0 = 전투 바크 (파츠 파괴 · 자막 · 그로기) 시험
    // 위치: DialogueSystem_Test.prefab
    // 실제 게임 로직이 아님. 출시 빌드 씬/프리팹에 넣지 말 것
    // ==========================================================================
    public class Step14_BarkTest : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private BattleBarkPlayer barks;

        private static readonly BodyPart[] PlayerParts = { BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg, BodyPart.Head };
        private static readonly string[] BossParts = { "TIRE_PART", "TEST_PART" };

        private int playerIndex;
        private int bossIndex;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Start()
        {
            if (barks == null)
            {
                Debug.LogError("[Step14] Barks가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            Debug.Log("[Step14] 6: 내 파츠 파괴(순서대로) / 7: 전투 자막 튜토리얼(T10, 1회) / 8: 보스 부위 파괴(TIRE_PART ↔ TEST_PART) / 9: 보스 그로기, Shift+9: 체력 20% 그로기 / 0: 전투 기록 초기화");
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit6Key.wasPressedThisFrame)
            {
                BodyPart part = PlayerParts[playerIndex];
                playerIndex = (playerIndex + 1) % PlayerParts.Length;
                barks.NotifyPlayerPartBroken(part);
            }
            else if (keyboard.digit7Key.wasPressedThisFrame)
            {
                if (!barks.PlaySubtitleDialogue("tutorial_t10_battle")) Debug.Log("[Step14] 전투 자막 튜토리얼은 이미 봤거나 tutorial 풀이 없습니다 (18단계 배치)");
            }
            else if (keyboard.digit8Key.wasPressedThisFrame)
            {
                string id = BossParts[bossIndex];
                bossIndex = (bossIndex + 1) % BossParts.Length;
                barks.NotifyBossPartBroken(id);
            }
            else if (keyboard.digit9Key.wasPressedThisFrame)
            {
                barks.NotifyBossGroggy(keyboard.shiftKey.isPressed ? 20 : -1);
            }
            else if (keyboard.digit0Key.wasPressedThisFrame)
            {
                playerIndex = 0;
                bossIndex = 0;
                barks.ResetBattle();
            }
        }
#endif
    }
}
