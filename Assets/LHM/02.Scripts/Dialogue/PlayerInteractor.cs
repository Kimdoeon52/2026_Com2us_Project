using System.Reflection;
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

        // SYS 캐릭터(RSSimpleCharacter)는 Q/E를 '카메라 45° 돌리기'로도 써서, E로 말 걸 때마다 카메라가 45°씩 돌아감
        // → 상호작용 대상이 범위 안에 있거나 대화 중일 때만 그 회전 각도(rotateStep)를 0으로 막고, 벗어나면 원래 값으로 돌려놓음
        // SYS 파일은 고치지 않고, 이름으로 찾아서 없으면 아무 일도 안 함
        private const string CameraStepField = "rotateStep";

        private NpcInteractable current;
        private NpcInteractable shownFor;
        private Component cameraStepOwner;
        private FieldInfo cameraStepField;
        private float savedCameraStep;
        private bool cameraStepBlocked;

        public NpcInteractable Current => current;
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
            FindCameraStep();
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

        private void FindCameraStep()
        {
            foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour == this) continue;

                FieldInfo field = behaviour.GetType().GetField(CameraStepField, BindingFlags.Public | BindingFlags.Instance);
                if (field == null || field.FieldType != typeof(float)) continue;

                cameraStepOwner = behaviour;
                cameraStepField = field;
                Debug.Log($"[PlayerInteractor] {behaviour.GetType().Name}의 E/Q 카메라 회전을 상호작용 범위 안에서는 막습니다 (E 키 충돌 방지).", this);
                return;
            }
        }

        private void BlockCameraStep(bool block)
        {
            if (cameraStepField == null || cameraStepOwner == null || block == cameraStepBlocked) return;

            if (block)
            {
                savedCameraStep = (float)cameraStepField.GetValue(cameraStepOwner);
                cameraStepField.SetValue(cameraStepOwner, 0f);
            }
            else
            {
                cameraStepField.SetValue(cameraStepOwner, savedCameraStep);
            }
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
