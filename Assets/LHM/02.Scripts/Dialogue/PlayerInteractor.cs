using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private DialogueManager manager;
        [SerializeField] private NpcChoiceMenu menu;
        [SerializeField] private TMP_Text prompt;
        [SerializeField] private Camera worldCamera;

        private NpcInteractable current;
        private NpcInteractable shownFor;

        public NpcInteractable Current => current;
        public bool IsBusy => (menu != null && menu.IsBusy) || (manager != null && manager.IsTalking);

        private void Start()
        {
            if (manager == null || menu == null)
            {
                Debug.LogError("[PlayerInteractor] Manager 또는 Menu가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            if (worldCamera == null) worldCamera = Camera.main;
            HidePrompt();
        }

        private void Update()
        {
            if (!manager.IsReady || IsBusy || menu.EndedThisFrame)
            {
                current = null;
                HidePrompt();
                return;
            }

            current = FindNearest();
            UpdatePrompt();

            if (current == null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                HidePrompt();
                menu.Open(current);
            }
        }

        private NpcInteractable FindNearest()
        {
            NpcInteractable nearest = null;
            float nearestDistance = float.MaxValue;
            Vector3 position = transform.position;

            foreach (NpcInteractable npc in NpcInteractable.Active)
            {
                float distance = Vector3.Distance(position, npc.transform.position);
                if (distance > npc.InteractRadius || distance >= nearestDistance) continue;

                nearest = npc;
                nearestDistance = distance;
            }

            return nearest;
        }

        private void UpdatePrompt()
        {
            if (prompt == null) return;

            if (current == null || worldCamera == null)
            {
                HidePrompt();
                return;
            }

            Vector3 screen = worldCamera.WorldToScreenPoint(current.PromptPosition);
            if (screen.z < 0f)
            {
                HidePrompt();
                return;
            }

            if (shownFor != current)
            {
                prompt.text = $"[E] {manager.GetSpeakerName(current.SpeakerId)}";
                shownFor = current;
            }

            prompt.transform.position = screen;
            prompt.gameObject.SetActive(true);
        }

        private void HidePrompt()
        {
            shownFor = null;
            if (prompt != null) prompt.gameObject.SetActive(false);
        }
    }
}
