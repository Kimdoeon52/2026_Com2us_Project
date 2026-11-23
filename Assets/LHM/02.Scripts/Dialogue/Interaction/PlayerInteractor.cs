using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    // FieldInputBridge(-90)보다 먼저 돌아야 같은 프레임의 대상 NPC로 고철 안내 · 이동을 막음 (InputModeController -100 다음)
    [DefaultExecutionOrder(-95)]
    public class PlayerInteractor : MonoBehaviour
    {
        // 이미 잡은 대상은 반경보다 조금 더 멀어져야 놓음 (경계에서 이름표가 깜빡이지 않게)
        private const float KeepTargetMargin = 0.3f;

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

        private void OnEnable()
        {
            Canvas.willRenderCanvases += FollowPrompt;
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= FollowPrompt;
            current = null;
            HidePrompt();
            BlockCameraStep(false);
        }

        private void Update()
        {
            if (!manager.IsReady || IsBusy || menu.EndedThisFrame || OtherUiOpen())
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
                float distance = HorizontalDistance(position, npc.transform.position);
                float radius = npc == current ? npc.InteractRadius + KeepTargetMargin : npc.InteractRadius;
                if (distance > radius || distance >= nearestDistance) continue;

                nearest = npc;
                nearestDistance = distance;
            }

            return nearest;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
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

        // SYS 카메라는 LateUpdate에서 움직이므로, 모든 LateUpdate가 끝난 뒤(캔버스 그리기 직전) 이름표 위치를 다시 맞춤
        private void FollowPrompt()
        {
            if (prompt == null || shownFor == null || worldCamera == null || !prompt.gameObject.activeSelf) return;

            Vector3 screen = worldCamera.WorldToScreenPoint(shownFor.PromptPosition);
            if (screen.z < 0f) HidePrompt();
            else prompt.transform.position = screen;
        }

        private static bool OtherUiOpen()
        {
            return RealSteel.Tutorial.GamePause.IsPaused || (UiStack.Current != null && UiStack.Current.Count > 0);
        }

        private void HidePrompt()
        {
            shownFor = null;
            if (prompt != null) prompt.gameObject.SetActive(false);
        }
    }
}
