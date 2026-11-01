using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class NpcBubbles : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private DialogueManager manager;
        [SerializeField] private BubbleManager bubbles;
        [SerializeField] private DialogueEventQueue queue;
        [SerializeField] private Transform player;

        [Header("반응 말풍선 (이벤트 큐)")]
        [SerializeField, Min(0.1f)] private float reactionRadius = 4f;

        [Header("근접 혼잣말")]
        [SerializeField] private string monologuePoolPrefix = "mono_";
        [SerializeField, Min(0.1f)] private float monologueRadius = 3f;
        [SerializeField, Min(0f)] private float monologueInterval = 10f;

        [Header("표시")]
        [SerializeField] private Vector3 headOffset = new Vector3(0f, 2.3f, 0f);

        private readonly List<PendingReaction> reactions = new List<PendingReaction>();
        private readonly Dictionary<NpcInteractable, bool> inside = new Dictionary<NpcInteractable, bool>();
        private readonly Dictionary<NpcInteractable, float> lastMonologueAt = new Dictionary<NpcInteractable, float>();
        private readonly List<string> matched = new List<string>();

        private struct PendingReaction
        {
            public string dialogueId;
            public string speakerId;
        }

        public int PendingCount => reactions.Count;

        private void OnEnable()
        {
            if (queue != null) queue.BubbleRequested += OnBubbleRequested;
        }

        private void OnDisable()
        {
            if (queue != null) queue.BubbleRequested -= OnBubbleRequested;
        }

        private void Start()
        {
            if (manager == null || bubbles == null || player == null)
            {
                Debug.LogError("[NpcBubble] Manager, Bubbles, Player 중 비어 있는 칸이 있습니다.", this);
                enabled = false;
                return;
            }

            if (queue == null) Debug.LogWarning("[NpcBubble] Queue가 비어 있어 반응 말풍선은 받지 않습니다 (혼잣말만 동작).", this);
        }

        private void Update()
        {
            bool idle = DialogueEventQueue.IsFieldIdle(manager);
            Vector3 position = player.position;

            foreach (NpcInteractable npc in NpcInteractable.Active)
            {
                float distance = Vector3.Distance(position, npc.transform.position);
                bool now = distance <= monologueRadius;
                inside.TryGetValue(npc, out bool before);
                inside[npc] = now;

                if (!idle) continue;
                if (distance <= reactionRadius && TryReaction(npc)) continue;
                if (now && !before) TryMonologue(npc);
            }
        }

        public void AddReaction(string dialogueId)
        {
            if (!manager.TryGetFirstLine(dialogueId, out DialogueLine line))
            {
                Debug.LogWarning($"[NpcBubble] 반응 말풍선 '{dialogueId}'의 대사를 찾지 못했습니다.", this);
                return;
            }

            if (reactions.Exists(r => r.dialogueId == dialogueId)) return;

            reactions.Add(new PendingReaction { dialogueId = dialogueId, speakerId = line.speakerId });
            bool hasNpc = false;
            foreach (NpcInteractable npc in NpcInteractable.Active) hasNpc |= npc.SpeakerId == line.speakerId;
            Debug.Log($"[NpcBubble] 반응 말풍선 대기: {dialogueId} → '{line.speakerId}' 근처({reactionRadius}m)에 가면 1회{(hasNpc ? "" : " (주의: 이 씬에 해당 NPC 없음)")} / 대기 {reactions.Count}개", this);
        }

        public void ClearReactions()
        {
            reactions.Clear();
        }

        private void OnBubbleRequested(string dialogueId)
        {
            AddReaction(dialogueId);
        }

        private bool TryReaction(NpcInteractable npc)
        {
            matched.Clear();
            foreach (PendingReaction reaction in reactions)
            {
                if (reaction.speakerId == npc.SpeakerId) matched.Add(reaction.dialogueId);
            }
            if (matched.Count == 0) return false;

            bool shown = manager.TryPlayBest(matched, "반응 말풍선", out DialogueLine line, out string chosenId);
            if (shown)
            {
                reactions.RemoveAll(r => r.dialogueId == chosenId);
                bubbles.Show(npc.transform, headOffset, line.text, BubbleManager.NpcChannel);
                lastMonologueAt[npc] = Time.unscaledTime;
            }

            List<string> dropped = new List<string>();
            foreach (string id in matched)
            {
                if (id == chosenId || manager.CanPlay(id, null)) continue;
                dropped.Add(id);
            }

            if (dropped.Count > 0)
            {
                reactions.RemoveAll(r => dropped.Contains(r.dialogueId));
                Debug.Log($"[NpcBubble] 더 이상 나올 수 없는 반응 말풍선 제거 (이미 반응함 / 조건 바뀜): {string.Join(", ", dropped)}", this);
            }

            return shown;
        }

        private void TryMonologue(NpcInteractable npc)
        {
            string poolId = monologuePoolPrefix + npc.SpeakerId;
            if (!manager.HasPool(poolId) || bubbles.IsShowingOn(npc.transform)) return;

            float now = Time.unscaledTime;
            if (lastMonologueAt.TryGetValue(npc, out float last) && now - last < monologueInterval) return;

            if (!manager.TryPickLine(poolId, "혼잣말", out DialogueLine line)) return;

            lastMonologueAt[npc] = now;
            bubbles.Show(npc.transform, headOffset, line.text, BubbleManager.NpcChannel);
        }
    }
}
