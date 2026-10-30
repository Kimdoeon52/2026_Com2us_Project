using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public enum InputMode
    {
        Field,
        Battle,
        Dialogue,
    }

    [DefaultExecutionOrder(-100)]
    public class InputModeController : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private InputActionAsset actions;

        [Header("상태")]
        [SerializeField] private InputMode baseMode = InputMode.Field;

        private InputActionMap fieldMap;
        private InputActionMap battleMap;
        private InputActionMap dialogueMap;
        private bool waitingRelease;

        public static InputModeController Current { get; private set; }

        public InputMode Mode { get; private set; }
        public InputMode BaseMode => baseMode;
        public bool IsWaitingRelease => waitingRelease;

        private void Awake()
        {
            if (actions == null)
            {
                Debug.LogError("[InputMode] Actions가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            fieldMap = actions.FindActionMap("Field", true);
            battleMap = actions.FindActionMap("Battle", true);
            dialogueMap = actions.FindActionMap("Dialogue", true);
        }

        private void OnEnable()
        {
            if (fieldMap == null) return;

            Current = this;
            Mode = baseMode;
            Apply(false);
        }

        private void Start()
        {
            if (UiStack.Current == null)
            {
                Debug.LogWarning("[InputMode] UI 스택이 없어 대화 상태로 전환할 수 없습니다. DummyUiStack(10-1단계)을 배치하세요.", this);
            }
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            if (actions != null) actions.Disable();
        }

        private void Update()
        {
            InputMode desired = UiStack.Current != null && UiStack.Current.Count > 0 ? InputMode.Dialogue : baseMode;

            if (desired != Mode)
            {
                bool leavingDialogue = Mode == InputMode.Dialogue;
                Debug.Log($"[InputMode] {Mode} → {desired}{(leavingDialogue ? " (키를 모두 뗄 때까지 입력 대기)" : "")}");
                Mode = desired;
                Apply(leavingDialogue);
            }

            if (waitingRelease && !AnyHeld())
            {
                waitingRelease = false;
                GetMap(Mode).Enable();
            }
        }

        public void SetBaseMode(InputMode mode)
        {
            if (mode == InputMode.Dialogue || mode == baseMode) return;
            baseMode = mode;
        }

        public bool WasPressed(InputMode map, string action)
        {
            InputAction found = Find(map, action);
            return found != null && found.enabled && found.WasPressedThisFrame();
        }

        public bool IsPressed(InputMode map, string action)
        {
            InputAction found = Find(map, action);
            return found != null && found.enabled && found.IsPressed();
        }

        public T Read<T>(InputMode map, string action) where T : struct
        {
            InputAction found = Find(map, action);
            return found != null && found.enabled ? found.ReadValue<T>() : default;
        }

        private InputAction Find(InputMode map, string action)
        {
            InputActionMap actionMap = GetMap(map);
            return actionMap != null ? actionMap.FindAction(action) : null;
        }

        private InputActionMap GetMap(InputMode mode)
        {
            switch (mode)
            {
                case InputMode.Battle: return battleMap;
                case InputMode.Dialogue: return dialogueMap;
                default: return fieldMap;
            }
        }

        private void Apply(bool waitForRelease)
        {
            fieldMap.Disable();
            battleMap.Disable();
            dialogueMap.Disable();

            waitingRelease = waitForRelease && AnyHeld();
            if (!waitingRelease) GetMap(Mode).Enable();
        }

        private static bool AnyHeld()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.isPressed) return true;

            Mouse mouse = Mouse.current;
            return mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed);
        }
    }
}
