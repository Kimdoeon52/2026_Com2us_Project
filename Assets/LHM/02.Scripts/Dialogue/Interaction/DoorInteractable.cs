using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue
{
    // 문 앞에 두는 씬 이동 지점. 플레이어가 가까이 오면 "[E] 들어가기"가 뜨고, E를 누르면 지정한 씬으로 넘어감
    // 대화 시스템(PlayerInteractor)이 없는 씬에서도 혼자 동작함 (플레이어는 SYS RSSimpleCharacter → PlayerInteractor → "Player" 태그 순으로 찾음)
    [DisallowMultipleComponent]
    public class DoorInteractable : MonoBehaviour
    {
        private const float MinArrivalDistance = 0.8f;
        private const float DefaultPlayerRadius = 0.3f;
        private const float DefaultPlayerHeight = 1.8f;
        private const float GroundClearance = 0.15f;

        private static readonly List<DoorInteractable> active = new List<DoorInteractable>();
        private static TMP_Text sharedPrompt;
        private static DoorInteractable promptOwner;

        [Header("이동할 씬")]
        [Tooltip("Build Settings에 있으면 이 경로로 엶. 없으면 에디터 플레이에서만 경로로 열고, 빌드에서는 오류 로그")]
        [SerializeField] private string scenePath = "";
        [SerializeField] private string sceneName = "";

        [Header("도착 위치 (문끼리 짝 맞추기)")]
        [Tooltip("이 문의 이름표. 다른 씬의 문이 '도착할 문'으로 이 값을 적으면, 그 문으로 넘어왔을 때 이 문 앞에 나타남")]
        [SerializeField] private string doorId = "";
        [Tooltip("넘어간 씬에서 나타날 문의 이름표 (그 씬 문의 Door Id). 비우면 그 씬의 원래 시작 위치")]
        [SerializeField] private string arriveAtDoorId = "";
        [Tooltip("이 문으로 도착했을 때 서는 자리 (이 오브젝트 기준, 앞 = +Z)")]
        [SerializeField] private Vector3 arrivalOffset = new Vector3(0f, 0f, 0.8f);

        [Header("안내")]
        [SerializeField] private string label = "들어가기";
        [SerializeField, Min(0.1f)] private float interactRadius = 1.6f;
        [SerializeField] private Vector3 promptOffset = new Vector3(0f, 2.2f, 0f);

        [Header("연결 (비우면 자동)")]
        [SerializeField] private Transform player;
        [Tooltip("비우면 PlayerInteractor의 [E] 안내를 복사해 쓰고, 그것도 없으면 이 폰트로 새로 만듦")]
        [SerializeField] private TMP_Text promptTemplate;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Camera worldCamera;

        private PlayerInteractor interactor;
        private float nextPlayerSearch;
        private bool blocking;
        private bool leaving;
        // 이 문으로 막 도착했으면 한 번 범위 밖으로 나갈 때까지 안내 · E를 받지 않음 (도착하자마자 되돌아가지 않게)
        private bool waitForExit;

        private static string pendingArrival;

        public string ScenePath => scenePath;
        public string SceneName => sceneName;
        public string DoorId => doorId;
        public string ArriveAtDoorId => arriveAtDoorId;
        public Vector3 ArrivalPosition => transform.TransformPoint(arrivalOffset);
        public float InteractRadius => interactRadius;
        public Vector3 PromptPosition => transform.position + promptOffset;

        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
            HidePrompt();
            SetBlocking(false);
        }

        private void Update()
        {
            if (leaving) return;

            if (player == null && Time.unscaledTime >= nextPlayerSearch)
            {
                nextPlayerSearch = Time.unscaledTime + 1f;
                player = FindPlayer();
            }

            if (waitForExit && player != null && HorizontalDistance(player.position) > interactRadius) waitForExit = false;

            bool inRange = player != null && !waitForExit && Time.timeScale > 0f && !InteractorBusy() && IsNearestInRange();
            SetBlocking(inRange);
            if (!inRange)
            {
                HidePrompt();
                return;
            }

            ShowPrompt();
            if (InteractPressed()) Enter();
        }

        public void Enter()
        {
            if (leaving) return;

            string target = ResolveScene();
            pendingArrival = string.IsNullOrEmpty(arriveAtDoorId) ? null : arriveAtDoorId;
            if (target == null)
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(scenePath) && System.IO.File.Exists(scenePath))
                {
                    Debug.LogWarning($"[Door] '{scenePath}'이 Build Settings에 없어 에디터에서만 경로로 엽니다. 빌드에서는 Build Settings에 추가해야 합니다.", this);
                    leaving = true;
                    HidePrompt();
                    UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
                    return;
                }
#endif
                pendingArrival = null;
                Debug.LogError($"[Door] 이동할 씬을 열 수 없습니다 (경로 '{scenePath}', 이름 '{sceneName}'). Build Settings에 추가했는지 확인하세요.", this);
                return;
            }

            leaving = true;
            HidePrompt();
            Debug.Log($"[Door] {name} → {target}", this);

            // LJS 로딩 화면(LoadingManager)이 떠 있으면 그걸로, 없으면 바로 이동. 다른 팀 파일이라 이름으로만 찾음
            MonoBehaviour loading = FindByTypeName("LoadingManager");
            MethodInfo load = loading != null ? loading.GetType().GetMethod("LoadScene", new[] { typeof(string) }) : null;
            if (load != null) load.Invoke(loading, new object[] { target });
            else SceneManager.LoadScene(target);
        }

        // ── 도착 ─────────────────────────────
        // sceneLoaded는 새 씬의 Awake 뒤 · Start 전에 불림 → 플레이어(RSSimpleCharacter)가 Start에서 카메라 · 바닥을 새 위치 기준으로 맞춤
        // 로딩 씬(LJS LoadingManager)을 거쳐도 짝이 되는 문이 있는 씬이 열릴 때까지 기다림

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pendingArrival = null;
            sharedPrompt = null;
            promptOwner = null;
            active.Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (string.IsNullOrEmpty(pendingArrival)) return;

            DoorInteractable arrival = null;
            foreach (DoorInteractable door in FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None))
            {
                if (door.gameObject.scene == scene && door.doorId == pendingArrival) arrival = door;
            }
            if (arrival == null) return;

            pendingArrival = null;
            arrival.PlacePlayerHere();
        }

        private void PlacePlayerHere()
        {
            Transform target = FindPlayer();
            if (target == null)
            {
                Debug.LogWarning($"[Door] '{doorId}' 문 앞에 세울 플레이어를 찾지 못해 원래 시작 위치에서 시작합니다.", this);
                return;
            }

            var controller = target.GetComponent<CharacterController>();
            Vector3 position = FindFreeArrival(target, controller);
            Vector3 facing = Vector3.ProjectOnPlane(position - transform.position, Vector3.up);
            if (facing.sqrMagnitude < 0.0001f) facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

            // CharacterController가 켜져 있으면 위치를 바로 바꿔도 되돌아감 → 잠깐 끄고 옮김
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled) controller.enabled = false;
            target.position = position;
            if (facing.sqrMagnitude > 0.0001f) target.rotation = Quaternion.LookRotation(facing, Vector3.up);
            if (wasEnabled) controller.enabled = true;

            player = target;
            waitForExit = true;
            Debug.Log($"[Door] '{doorId}' 문 앞에 도착 ({target.name} → {position})", this);
        }

        // 도착 자리가 벽 · 컨테이너에 막혀 있으면 문 반대쪽 → 더 먼 곳 → 옆 순서로 빈자리를 찾음 (씬마다 문 방향이 달라도 끼지 않게)
        private Vector3 FindFreeArrival(Transform target, CharacterController controller)
        {
            float distance = Mathf.Max(MinArrivalDistance, Mathf.Abs(arrivalOffset.z));
            Vector3[] candidates =
            {
                arrivalOffset,
                new Vector3(arrivalOffset.x, arrivalOffset.y, -arrivalOffset.z),
                new Vector3(arrivalOffset.x, arrivalOffset.y, distance * 2f),
                new Vector3(arrivalOffset.x, arrivalOffset.y, -distance * 2f),
                new Vector3(distance * 1.5f, arrivalOffset.y, 0f),
                new Vector3(-distance * 1.5f, arrivalOffset.y, 0f),
            };

            Physics.SyncTransforms();
            Vector3 first = Vector3.zero;
            for (int i = 0; i < candidates.Length; i++)
            {
                Vector3 position = transform.TransformPoint(candidates[i]);
                position.y = transform.position.y;
                if (i == 0) first = position;
                if (!IsFree(target, controller, position)) continue;

                if (i > 0) Debug.Log($"[Door] '{doorId}' 도착 자리가 막혀 있어 {i + 1}번째 후보 자리로 옮김 ({position})", this);
                return position;
            }

            Debug.LogWarning($"[Door] '{doorId}' 문 주변에 빈자리가 없어 기본 도착 자리에 세웁니다. 문의 Arrival Offset을 확인하세요.", this);
            return first;
        }

        private static bool IsFree(Transform target, CharacterController controller, Vector3 position)
        {
            Vector3 scale = target.lossyScale;
            float radius = controller != null ? controller.radius * Mathf.Max(scale.x, scale.z) : DefaultPlayerRadius;
            float height = controller != null ? controller.height * scale.y : DefaultPlayerHeight;
            Vector3 center = position + (controller != null ? Vector3.Scale(controller.center, scale) : Vector3.up * height * 0.5f);

            float half = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 bottom = center - Vector3.up * half;
            Vector3 top = center + Vector3.up * half;
            bottom.y = Mathf.Min(bottom.y + GroundClearance, top.y);

            foreach (Collider hit in Physics.OverlapCapsule(bottom, top, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(target)) return false;
            }
            return true;
        }

        private string ResolveScene()
        {
            if (!string.IsNullOrEmpty(scenePath) && SceneUtility.GetBuildIndexByScenePath(scenePath) >= 0) return scenePath;
            if (!string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName)) return sceneName;
            return null;
        }

        // ── 범위 · 입력 ─────────────────────────────

        private float HorizontalDistance(Vector3 position)
        {
            Vector3 d = position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        // 문이 여러 개 겹치면 가장 가까운 문만 안내
        private bool IsNearestInRange()
        {
            Vector3 position = player.position;
            float mine = HorizontalDistance(position);
            if (mine > interactRadius) return false;

            foreach (DoorInteractable door in active)
            {
                if (door == this || door.leaving) continue;
                float other = door.HorizontalDistance(position);
                if (other <= door.interactRadius && other < mine) return false;
            }
            return true;
        }

        // NPC 안내가 떠 있거나 대화 중이면 문은 양보 (E 하나로 두 가지가 같이 일어나지 않게)
        private bool InteractorBusy()
        {
            if (interactor == null) return false;
            return interactor.isActiveAndEnabled && (interactor.Current != null || interactor.IsBusy);
        }

        private static bool InteractPressed()
        {
            InputModeController input = InputModeController.Current;
            if (input != null) return input.WasPressed(InputMode.Field, "Interact");
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.eKey.wasPressedThisFrame;
        }

        private void SetBlocking(bool block)
        {
            if (block == blocking) return;
            if (player != null) CameraStepBlock.Set(this, player.gameObject, block);
            blocking = block;
        }

        private Transform FindPlayer()
        {
            interactor = FindAnyObjectByType<PlayerInteractor>();

            MonoBehaviour character = FindByTypeName("RSSimpleCharacter");
            if (character != null) return character.transform;
            if (interactor != null) return interactor.transform;

            GameObject tagged = GameObject.FindWithTag("Player");
            return tagged != null ? tagged.transform : null;
        }

        private static MonoBehaviour FindByTypeName(string typeName)
        {
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour != null && behaviour.GetType().Name == typeName) return behaviour;
            }
            return null;
        }

        // ── 안내 문구 ─────────────────────────────

        private void ShowPrompt()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return;

            Vector3 screen = worldCamera.WorldToScreenPoint(PromptPosition);
            if (screen.z < 0f)
            {
                HidePrompt();
                return;
            }

            TMP_Text text = EnsurePrompt();
            if (text == null) return;

            promptOwner = this;
            text.text = string.IsNullOrEmpty(label) ? "[E]" : $"[E] {label}";
            text.transform.position = screen;
            text.gameObject.SetActive(true);
        }

        private void HidePrompt()
        {
            if (promptOwner != this) return;
            promptOwner = null;
            if (sharedPrompt != null) sharedPrompt.gameObject.SetActive(false);
        }

        // 문들이 같이 쓰는 안내 하나. NPC 안내와 같은 모양이 되도록 PlayerInteractor 것을 복사함
        private TMP_Text EnsurePrompt()
        {
            if (sharedPrompt != null) return sharedPrompt;

            TMP_Text template = promptTemplate != null ? promptTemplate : interactor != null ? interactor.Prompt : null;
            if (template != null)
            {
                sharedPrompt = Instantiate(template, template.transform.parent);
                sharedPrompt.name = "DoorPrompt";
                return sharedPrompt;
            }

            var canvasObject = new GameObject("DoorPrompt Canvas", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var textObject = new GameObject("DoorPrompt", typeof(RectTransform));
            textObject.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(400f, 50f);

            var text = textObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = 30f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 200);

            sharedPrompt = text;
            return sharedPrompt;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, PromptPosition);
            if (!string.IsNullOrEmpty(doorId)) Gizmos.DrawSphere(ArrivalPosition, 0.15f);
        }
    }
}
