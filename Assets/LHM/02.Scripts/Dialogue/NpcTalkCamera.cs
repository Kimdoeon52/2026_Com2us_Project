using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RealSteel.Dialogue
{
    public enum TalkCameraApproach
    {
        PlayerSide,
        MainCameraSide,
    }

    public class NpcTalkCamera : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private NpcChoiceMenu menu;
        [SerializeField] private Camera closeUpCamera;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Image fadeOverlay;
        [SerializeField] private DialogueDirector director;

        [Header("페이드")]
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.25f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.35f;
        [SerializeField] private bool disableMainCamera = true;

        [Header("시작 위치 (플레이어 근처)")]
        [SerializeField] private TalkCameraApproach approach = TalkCameraApproach.PlayerSide;
        [SerializeField, Min(0f)] private float startBehindPlayer = 1.2f;
        [SerializeField] private float startHeight = 0.6f;
        [SerializeField] private float startAngle = -35f;

        [Header("클로즈업 (정지 위치)")]
        [SerializeField, Min(0.1f)] private float stopDistance = 1.6f;
        [SerializeField] private float closeUpHeight = 0.05f;
        [SerializeField] private float closeUpAngle = 20f;
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, -0.15f, 0f);
        [SerializeField, Range(0f, 120f)] private float closeUpFieldOfView = 40f;

        [Header("이동")]
        [SerializeField, Min(0.05f)] private float moveSeconds = 1.2f;
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Min(0f)] private float holdSeconds = 0.15f;

        private NpcInteractable target;
        private Transform player;
        private Coroutine routine;
        private bool busy;
        private bool closeUpActive;
        private bool mainDisabledByUs;
        private bool listening;

        private readonly List<Canvas> movedCanvases = new List<Canvas>();

        private Vector3 focus;
        private Vector3 baseDirection;
        private float startDistance;
        private float startFov;
        private Vector3 startPosition;
        private Quaternion startRotation;

        public bool IsBusy => busy;
        public NpcInteractable Target => target;

        private void Awake()
        {
            if (menu == null || closeUpCamera == null)
            {
                Debug.LogError("[TalkCam] Menu 또는 Close Up Camera가 연결되지 않았습니다. 카메라 연출 없이 선택지를 엽니다.", this);
                enabled = false;
                return;
            }

            closeUpCamera.gameObject.SetActive(false);
            SetAlpha(0f);
            if (fadeOverlay != null) fadeOverlay.raycastTarget = false;
        }

        private void OnDisable()
        {
            if (!busy) return;

            if (routine != null) StopCoroutine(routine);
            routine = null;
            StopListening();
            SwitchToMain();
            SetAlpha(0f);
            Finish();
        }

        public bool Play(NpcInteractable npc, Transform playerTransform)
        {
            if (!isActiveAndEnabled || busy || npc == null) return false;

            if (mainCamera == null) mainCamera = Camera.main;
            target = npc;
            player = playerTransform;
            busy = true;
            UiStack.Push(this);

            routine = StartCoroutine(EnterRoutine());
            return true;
        }

        private IEnumerator EnterRoutine()
        {
            yield return Fade(1f, fadeOutSeconds);

            SwitchToCloseUp();
            BuildPath();
            ApplyPose(0f);

            float total = Mathf.Max(moveSeconds, fadeInSeconds);
            float elapsed = 0f;
            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyPose(ease.Evaluate(Mathf.Clamp01(elapsed / moveSeconds)));
                SetAlpha(fadeInSeconds > 0f ? 1f - Mathf.Clamp01(elapsed / fadeInSeconds) : 0f);
                yield return null;
            }

            ApplyPose(1f);
            SetAlpha(0f);

            if (holdSeconds > 0f) yield return new WaitForSecondsRealtime(holdSeconds);

            routine = null;
            Debug.Log($"[TalkCam] {target.name} 클로즈업 정지 (거리 {Vector3.Distance(closeUpCamera.transform.position, focus):0.00}m) → 선택지 열기");

            menu.Ended += OnMenuEnded;
            listening = true;
            menu.Open(target);

            if (!menu.IsBusy)
            {
                Debug.LogWarning("[TalkCam] 선택지가 열리지 않아 카메라를 되돌립니다.", this);
                StopListening();
                routine = StartCoroutine(ExitRoutine());
            }
        }

        private void OnMenuEnded()
        {
            StopListening();
            if (busy && routine == null) routine = StartCoroutine(ExitRoutine());
        }

        private IEnumerator ExitRoutine()
        {
            yield return Fade(1f, fadeOutSeconds);
            SwitchToMain();
            yield return Fade(0f, fadeInSeconds);

            routine = null;
            Finish();
        }

        private void Finish()
        {
            Debug.Log($"[TalkCam] 메인 카메라 복귀 ({(target != null ? target.name : "-")})");
            UiStack.Remove(this);
            target = null;
            player = null;
            busy = false;
        }

        private void StopListening()
        {
            if (!listening) return;
            menu.Ended -= OnMenuEnded;
            listening = false;
        }

        private void SwitchToCloseUp()
        {
            if (closeUpActive) return;

            startFov = mainCamera != null ? mainCamera.fieldOfView : closeUpCamera.fieldOfView;
            closeUpCamera.gameObject.SetActive(true);
            closeUpCamera.enabled = true;

            if (mainCamera != null && mainCamera != closeUpCamera)
            {
                if (disableMainCamera && mainCamera.enabled)
                {
                    MoveCameraCanvases(mainCamera, closeUpCamera);
                    mainCamera.enabled = false;
                    mainDisabledByUs = true;
                }
                else
                {
                    closeUpCamera.depth = mainCamera.depth + 1f;
                }
            }

            if (director != null) director.UseCamera(closeUpCamera);
            closeUpActive = true;
        }

        private void SwitchToMain()
        {
            if (!closeUpActive) return;

            if (director != null) director.UseCamera(null);
            closeUpCamera.gameObject.SetActive(false);

            if (mainDisabledByUs && mainCamera != null) mainCamera.enabled = true;
            foreach (Canvas canvas in movedCanvases)
            {
                if (canvas != null) canvas.worldCamera = mainCamera;
            }
            movedCanvases.Clear();
            mainDisabledByUs = false;
            closeUpActive = false;
        }

        private void MoveCameraCanvases(Camera from, Camera to)
        {
            movedCanvases.Clear();
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceCamera || canvas.worldCamera != from) continue;
                canvas.worldCamera = to;
                movedCanvases.Add(canvas);
            }

            if (movedCanvases.Count > 0) Debug.Log($"[TalkCam] 메인 카메라를 쓰는 Canvas {movedCanvases.Count}개를 클로즈업 동안 옮김");
        }

        private void BuildPath()
        {
            Vector3 up = Vector3.up;
            focus = target.CloseUpFocus;

            Vector3 from;
            if (approach == TalkCameraApproach.PlayerSide && player != null) from = player.position;
            else if (mainCamera != null) from = mainCamera.transform.position;
            else from = focus + Vector3.back;

            Vector3 direction = Vector3.ProjectOnPlane(from - focus, up);
            if (direction.sqrMagnitude < 0.0001f && mainCamera != null) direction = Vector3.ProjectOnPlane(-mainCamera.transform.forward, up);
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.back;
            baseDirection = direction.normalized;

            float playerDistance = player != null ? Vector3.ProjectOnPlane(player.position - focus, up).magnitude : stopDistance;
            startDistance = Mathf.Max(playerDistance + startBehindPlayer, stopDistance);

            startPosition = OrbitPosition(startDistance, startAngle, startHeight);
            startRotation = Quaternion.LookRotation(focus + lookOffset - startPosition, up);
        }

        private void ApplyPose(float t)
        {
            Transform point = target.CloseUpPoint;
            Vector3 position;
            Quaternion rotation;

            if (point != null)
            {
                position = Vector3.LerpUnclamped(startPosition, point.position, t);
                rotation = Quaternion.SlerpUnclamped(startRotation, point.rotation, t);
            }
            else
            {
                float distance = Mathf.LerpUnclamped(startDistance, stopDistance, t);
                float angle = Mathf.LerpUnclamped(startAngle, closeUpAngle, t);
                float height = Mathf.LerpUnclamped(startHeight, closeUpHeight, t);
                position = OrbitPosition(distance, angle, height);
                rotation = Quaternion.LookRotation(focus + lookOffset - position, Vector3.up);
            }

            closeUpCamera.transform.SetPositionAndRotation(position, rotation);
            if (closeUpFieldOfView > 0f) closeUpCamera.fieldOfView = Mathf.LerpUnclamped(startFov, closeUpFieldOfView, t);
        }

        private Vector3 OrbitPosition(float distance, float angle, float height)
        {
            return focus + Quaternion.AngleAxis(angle, Vector3.up) * baseDirection * distance + Vector3.up * height;
        }

        private IEnumerator Fade(float to, float seconds)
        {
            if (fadeOverlay == null)
            {
                yield break;
            }

            float from = fadeOverlay.color.a;
            if (seconds <= 0f || Mathf.Approximately(from, to))
            {
                SetAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, elapsed / seconds));
                yield return null;
            }

            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            if (fadeOverlay == null) return;

            Color color = fadeOverlay.color;
            color.a = Mathf.Clamp01(alpha);
            fadeOverlay.color = color;
            fadeOverlay.enabled = color.a > 0f;
        }
    }
}
