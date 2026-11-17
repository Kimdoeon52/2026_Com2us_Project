using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class CrowdBubbles : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private DialogueManager manager;
        [SerializeField] private BubbleManager bubbles;
        [SerializeField] private Transform[] members;

        [Header("대사")]
        [SerializeField] private string poolId = "crowd";
        [SerializeField] private Vector3 headOffset = new Vector3(0f, 0.6f, 0f);

        [Header("간격")]
        [SerializeField, Min(0.1f)] private float minDelay = 2.5f;
        [SerializeField, Min(0.1f)] private float maxDelay = 5f;
        [SerializeField, Min(1)] private int maxConcurrent = 3;

        private readonly List<Transform> free = new List<Transform>();
        private float timer;

        public bool Running { get; set; } = true;

        private void Start()
        {
            if (manager == null || bubbles == null || members == null || members.Length == 0)
            {
                Debug.LogError("[Crowd] Manager, Bubbles, Members 중 비어 있는 칸이 있습니다.", this);
                enabled = false;
                return;
            }

            timer = Random.Range(minDelay, maxDelay);
        }

        private void Update()
        {
            if (!Running || !manager.IsReady || manager.IsTalking) return;

            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;

            timer = Random.Range(minDelay, Mathf.Max(minDelay, maxDelay));
            ShowOne();
        }

        public bool ShowOne()
        {
            if (bubbles.CountChannel(BubbleManager.CrowdChannel) >= maxConcurrent) return false;

            free.Clear();
            foreach (Transform member in members)
            {
                if (member != null && member.gameObject.activeInHierarchy && !bubbles.IsShowingOn(member)) free.Add(member);
            }
            if (free.Count == 0) return false;

            if (!manager.TryPickLine(poolId, "관중", out DialogueLine line, false)) return false;

            Transform target = free[Random.Range(0, free.Count)];
            return bubbles.Show(target, headOffset, line.text, BubbleManager.CrowdChannel);
        }
    }
}
