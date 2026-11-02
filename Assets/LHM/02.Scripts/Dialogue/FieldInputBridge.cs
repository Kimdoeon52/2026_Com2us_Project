using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    [DefaultExecutionOrder(-90)]
    public class FieldInputBridge : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private PlayerInteractor interactor;

        [Header("대화·UI 중 끌 컴포넌트 (플레이어 이동·카메라)")]
        [SerializeField] private Behaviour[] movement = new Behaviour[0];

        [Header("NPC 근처·대화 중 끌 컴포넌트 (같은 E키를 쓰는 상호작용과 그 안내)")]
        [SerializeField] private Behaviour[] interactions = new Behaviour[0];
        [SerializeField] private GameObject[] hiddenPrompts = new GameObject[0];

        private readonly List<Behaviour> disabledMovement = new List<Behaviour>();
        private readonly List<Behaviour> disabledInteractions = new List<Behaviour>();
        private readonly List<GameObject> hiddenPromptObjects = new List<GameObject>();
        private bool blocked;
        private bool waitingRelease;
        private bool interactorDisabledByUs;

        private void OnDisable()
        {
            if (blocked) Unblock();
            RestoreInteractions();
            if (interactorDisabledByUs && interactor != null) interactor.enabled = true;
            interactorDisabledByUs = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            bool uiOpen = UiStack.Current != null && UiStack.Current.Count > 0;
            bool nearNpc = interactor != null && interactor.isActiveAndEnabled && interactor.Current != null;
            bool starting = nearNpc && keyboard != null && keyboard.eKey.wasPressedThisFrame;

            if (uiOpen || starting)
            {
                waitingRelease = false;
                if (!blocked) Block();
            }
            else if (blocked)
            {
                if (!waitingRelease) waitingRelease = true;
                if (keyboard == null || !keyboard.anyKey.isPressed)
                {
                    waitingRelease = false;
                    Unblock();
                }
            }

            if (blocked || nearNpc) SuppressInteractions();
            else RestoreInteractions();

            bool otherSystemBlocking = !blocked && ScrapInputGate.IsBlocked;
            if (interactor != null && otherSystemBlocking != interactorDisabledByUs)
            {
                interactor.enabled = !otherSystemBlocking;
                interactorDisabledByUs = otherSystemBlocking;
            }
        }

        private void Block()
        {
            blocked = true;
            ScrapInputGate.Push();
            foreach (Behaviour behaviour in movement)
            {
                if (behaviour == null || !behaviour.enabled) continue;
                behaviour.enabled = false;
                disabledMovement.Add(behaviour);
            }
        }

        private void Unblock()
        {
            blocked = false;
            ScrapInputGate.Pop();
            if (!ScrapInputGate.IsBlocked)
            {
                foreach (Behaviour behaviour in disabledMovement)
                {
                    if (behaviour != null) behaviour.enabled = true;
                }
            }
            disabledMovement.Clear();
        }

        private void SuppressInteractions()
        {
            foreach (Behaviour behaviour in interactions)
            {
                if (behaviour == null || !behaviour.enabled) continue;
                behaviour.enabled = false;
                disabledInteractions.Add(behaviour);
            }

            foreach (GameObject prompt in hiddenPrompts)
            {
                if (prompt == null || !prompt.activeSelf) continue;
                prompt.SetActive(false);
                hiddenPromptObjects.Add(prompt);
            }
        }

        private void RestoreInteractions()
        {
            if (disabledInteractions.Count == 0 && hiddenPromptObjects.Count == 0) return;
            foreach (Behaviour behaviour in disabledInteractions)
            {
                if (behaviour != null) behaviour.enabled = true;
            }
            disabledInteractions.Clear();

            foreach (GameObject prompt in hiddenPromptObjects)
            {
                if (prompt != null) prompt.SetActive(true);
            }
            hiddenPromptObjects.Clear();
        }
    }
}
