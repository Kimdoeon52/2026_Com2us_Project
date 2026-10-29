using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public enum NpcRole
    {
        Other,
        GuildReceptionist,
        JunkDealer,
    }

    public class NpcInteractable : MonoBehaviour
    {
        private static readonly List<NpcInteractable> active = new List<NpcInteractable>();

        public static IReadOnlyList<NpcInteractable> Active => active;

        [Header("NPC")]
        [SerializeField] private NpcRole role = NpcRole.Other;
        [SerializeField] private string poolId = "auction_npc";
        [SerializeField] private string speakerId = "auctioneer";

        [Header("상호작용")]
        [SerializeField, Min(0.1f)] private float interactRadius = 1.5f;
        [SerializeField] private Vector3 promptOffset = new Vector3(0f, 1.8f, 0f);

        public NpcRole Role => role;
        public string PoolId => poolId;
        public string SpeakerId => speakerId;
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
        }
    }
}
