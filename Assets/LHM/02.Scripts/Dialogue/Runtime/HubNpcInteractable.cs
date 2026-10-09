using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue.Unity
{
    /// <summary>
    /// 허브 NPC (고물상, 경매장 등). 범위 안에서 Interact(↓) → 반응형 풀에서 대사 선택.
    /// Interact는 Gameplay 맵 액션이므로 대화/메뉴 중에는 자동으로 들어오지 않는다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class HubNpcInteractable : MonoBehaviour
    {
        [SerializeField] private string poolId = "hub.junkshop.talk";
        [SerializeField] private InputActionReference interact;
        [SerializeField] private GameObject prompt;
        [SerializeField] private string playerTag = "Player";

        private bool _inRange;

        private void Awake() { if (prompt != null) prompt.SetActive(false); }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            _inRange = true;
            if (prompt != null) prompt.SetActive(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            _inRange = false;
            if (prompt != null) prompt.SetActive(false);
        }

        private void Update()
        {
            if (!_inRange || interact == null || !interact.action.WasPressedThisFrame()) return;
            var input = InputContextManager.Instance;
            if (input != null && !input.GameplayAllowed) return;

            var service = DialogueService.Instance;
            if (service == null || service.IsBlockingActive) return;

            if (prompt != null) prompt.SetActive(false);
            service.RequestBlocking(poolId, onDone: _ => { if (_inRange && prompt != null) prompt.SetActive(true); });
        }
    }
}
