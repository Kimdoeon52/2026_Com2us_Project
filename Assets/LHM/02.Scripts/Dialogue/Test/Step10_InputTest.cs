using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    // ======================== [TEST ONLY] 테스트 전용 ========================
    // 용도: ` 키 = 필드 ↔ 전투 입력 전환 시험
    // 위치: 테스트 씬 (JunkShop_Map) · DialogueSystem_Test.prefab
    // 실제 게임 로직이 아님. 출시 빌드 씬/프리팹에 넣지 말 것
    // ==========================================================================
    public class Step10_InputTest : MonoBehaviour
    {
        private static readonly string[] BattleButtons = { "Jump", "Dodge", "Basic", "PartQ", "PartW", "PartE", "PartR" };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Start()
        {
            Debug.Log("[Step10] ` (숫자 1 왼쪽 키) 필드 ↔ 전투 전환 / 전투: ←→ 이동, ↑ 점프, Space 회피, D 기본기, QWER 파츠 스킬");
        }

        private void Update()
        {
            InputModeController input = InputModeController.Current;
            if (input == null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.backquoteKey.wasPressedThisFrame && input.Mode != InputMode.Dialogue)
            {
                InputMode next = input.BaseMode == InputMode.Field ? InputMode.Battle : InputMode.Field;
                input.SetBaseMode(next);
                Debug.Log($"[Step10] 기본 상태 → {next}");
            }

            foreach (string action in BattleButtons)
            {
                if (input.WasPressed(InputMode.Battle, action)) Debug.Log($"[Battle] {action}");
            }
        }
#endif
    }
}
