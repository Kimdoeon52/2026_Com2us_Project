using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
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
