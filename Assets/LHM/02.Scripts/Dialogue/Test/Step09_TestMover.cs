using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    // ======================== [TEST ONLY] 테스트 전용 ========================
    // 용도: 테스트 씬용 임시 플레이어 이동
    // 위치: Step09_SceneSetup이 테스트 플레이어에 붙임
    // 실제 게임 로직이 아님. 출시 빌드 씬/프리팹에 넣지 말 것
    // ==========================================================================
    public enum TestMovePlane
    {
        XZ,
        XY,
    }

    public class Step09_TestMover : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField, Min(0f)] private float speed = 4f;
        [SerializeField] private TestMovePlane plane = TestMovePlane.XZ;

        private void Update()
        {
            if (interactor != null && interactor.IsBusy) return;

            float x, y;
            InputModeController input = InputModeController.Current;

            if (input != null)
            {
                Vector2 move = input.Read<Vector2>(InputMode.Field, "Move");
                x = move.x;
                y = move.y;
            }
            else
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard == null) return;

                x = Axis(keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed, keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
                y = Axis(keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed, keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
            }

            Vector3 direction = plane == TestMovePlane.XZ ? new Vector3(x, 0f, y) : new Vector3(x, y, 0f);
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            transform.position += direction * (speed * Time.deltaTime);
        }

        private static float Axis(bool positive, bool negative)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }
    }
}
