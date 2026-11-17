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
        [SerializeField] private NpcTalkCamera talkCamera;

        private NpcInteractable current;
        private NpcInteractable shownFor;
        private bool cameraStepBlocked;

        public NpcInteractable Current => current;
        public TMP_Text Prompt => prompt;
        public bool IsBusy => (menu != null && menu.IsBusy) || (manager != null && manager.IsTalking) ||
                              (talkCamera != null && talkCamera.IsBusy);

        public void Bind(DialogueManager newManager, NpcChoiceMenu newMenu, TMP_Text newPrompt, Camera camera, NpcTalkCamera newTalkCamera)
        {
            if (newManager != null) manager = newManager;
            if (newMenu != null) menu = newMenu;
            if (newPrompt != null) prompt = newPrompt;
            if (camera != null) worldCamera = camera;
            if (newTalkCamera != null) talkCamera = newTalkCamera;
        }

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

        private void OnDisable()
        {
            current = null;
            HidePrompt();
            BlockCameraStep(false);
        }

        private void Update()
        {
            if (!manager.IsReady || IsBusy || menu.EndedThisFrame)
            {
                // 대화가 끝나는 프레임에 이동 스크립트가 다시 켜지며 같은 E를 읽을 수 있어 대화 중에도 막아 둠
                BlockCameraStep(IsBusy || menu.EndedThisFrame);
                current = null;
                HidePrompt();
                return;
            }

            current = FindNearest();
            BlockCameraStep(current != null);
            UpdatePrompt();

            if (current == null) return;

            InputModeController input = InputModeController.Current;
            Keyboard keyboard = Keyboard.current;
            bool interact = input != null
                ? input.WasPressed(InputMode.Field, "Interact")
                : keyboard != null && keyboard.eKey.wasPressedThisFrame;

            if (interact)
            {
                HidePrompt();
                bool camera = talkCamera != null && talkCamera.isActiveAndEnabled && talkCamera.Play(current, transform);
                if (!camera) menu.Open(current);
            }
        }

        // E/Q 카메라 회전 막기 (CameraStepBlock 참고)
        private void BlockCameraStep(bool block)
        {
            if (block == cameraStepBlocked) return;
            CameraStepBlock.Set(this, gameObject, block);
            cameraStepBlocked = block;
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
                string speakerName = manager.GetSpeakerName(current.SpeakerId);
                prompt.text = string.IsNullOrEmpty(speakerName) ? "[E]" : $"[E] {speakerName}";
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
