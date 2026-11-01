using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public static class DialogueInput
    {
        public static bool ConfirmPressed()
        {
            InputModeController input = InputModeController.Current;
            if (input != null) return input.WasPressed(InputMode.Dialogue, "Confirm");

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.spaceKey.wasPressedThisFrame ||
                 keyboard.enterKey.wasPressedThisFrame ||
                 keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        public static bool SkipPressed()
        {
            InputModeController input = InputModeController.Current;
            if (input != null) return input.WasPressed(InputMode.Dialogue, "Skip");

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        public static bool FastForwardHeld()
        {
            InputModeController input = InputModeController.Current;
            if (input != null) return input.IsPressed(InputMode.Dialogue, "FastForward");

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.ctrlKey.isPressed;
        }

        public static bool AutoPressed()
        {
            InputModeController input = InputModeController.Current;
            if (input != null) return input.WasPressed(InputMode.Dialogue, "Auto");

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.tabKey.wasPressedThisFrame;
        }

        public static bool BacklogPressed()
        {
            InputModeController input = InputModeController.Current;
            if (input != null) return input.WasPressed(InputMode.Dialogue, "Backlog");

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.lKey.wasPressedThisFrame;
        }
    }
}
