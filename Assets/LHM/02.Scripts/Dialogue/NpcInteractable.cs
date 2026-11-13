using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public enum NpcRole
    {
        Other,
        GuildReceptionist,
        JunkDealer,
        Workbench,
    }

    public class NpcInteractable : MonoBehaviour
    {
        private static readonly List<NpcInteractable> active = new List<NpcInteractable>();

        public static IReadOnlyList<NpcInteractable> Active => active;

        [Header("NPC")]
        [SerializeField] private NpcRole role = NpcRole.Other;
        [SerializeField] private string poolId = "auction_npc";
        [SerializeField] private string speakerId = "auctioneer";
        [Tooltip("선택지 전에 대화창에 먼저 띄울 대사 풀. 비우면 바로 선택지가 열림 (예: 작업대 \"……\")")]
        [SerializeField] private string greetingPoolId = "";

        [Header("상호작용")]
        [SerializeField, Min(0.1f)] private float interactRadius = 1.5f;
        [SerializeField] private Vector3 promptOffset = new Vector3(0f, 1.8f, 0f);

        [Header("클로즈업 카메라")]
        [SerializeField] private Vector3 closeUpFocusOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private Transform closeUpPoint;

        public NpcRole Role => role;
        public Vector3 CloseUpFocus => transform.position + closeUpFocusOffset;
        public Transform CloseUpPoint => closeUpPoint;
        public string PoolId => poolId;
        public string SpeakerId => speakerId;
        public string GreetingPoolId => greetingPoolId;
        public float InteractRadius => interactRadius;
        public Vector3 PromptPosition => transform.position + promptOffset;

        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, PromptPosition);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(CloseUpFocus, 0.1f);
        }
    }
}
