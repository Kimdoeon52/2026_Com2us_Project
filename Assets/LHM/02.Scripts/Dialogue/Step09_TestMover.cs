using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
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

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            float x = Axis(keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed, keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
            float y = Axis(keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed, keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);

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
