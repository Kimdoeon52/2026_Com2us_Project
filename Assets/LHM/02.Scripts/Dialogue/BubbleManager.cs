using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class BubbleManager : MonoBehaviour
    {
        public const string CrowdChannel = "crowd";
        public const string NpcChannel = "npc";

        [Header("연결")]
        [SerializeField] private DialogueManager manager;
        [SerializeField] private BubbleView template;
        [SerializeField] private Camera worldCamera;

        [Header("표시")]
        [SerializeField, Min(1)] private int poolSize = 8;
        [SerializeField, Min(0f)] private float holdSeconds = 2.5f;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.3f;

        private readonly List<BubbleView> views = new List<BubbleView>();

        private void Awake()
        {
            if (template == null || !template.IsValid)
            {
                Debug.LogError("[Bubble] Template이 없거나 Template의 Group·Text 칸이 비어 있습니다. 15단계 배치 메뉴를 실행하세요.", this);
                enabled = false;
                return;
            }

            if (worldCamera == null) worldCamera = Camera.main;

            template.gameObject.SetActive(false);
            views.Add(template);
            for (int i = 1; i < poolSize; i++)
            {
                BubbleView copy = Instantiate(template, template.transform.parent);
                copy.name = $"{template.name} ({i})";
                copy.gameObject.SetActive(false);
                views.Add(copy);
            }
        }

        private void Update()
        {
            bool suppressed = manager != null && manager.IsTalking;
            float deltaTime = Time.unscaledDeltaTime;
            foreach (BubbleView view in views) view.Tick(deltaTime, suppressed);
        }

        public bool IsShowingOn(Transform target)
        {
            return Find(target) != null;
        }

        public int CountChannel(string channel)
        {
            int count = 0;
            foreach (BubbleView view in views)
            {
                if (view.IsShowing && view.Channel == channel) count++;
            }
            return count;
        }

        public bool Show(Transform target, Vector3 offset, string body, string channel)
        {
            if (!enabled || target == null || string.IsNullOrEmpty(body)) return false;

            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null)
            {
                Debug.LogWarning("[Bubble] 카메라가 없어 말풍선을 표시할 수 없습니다.", this);
                return false;
            }

            BubbleView view = Find(target) ?? views.Find(v => !v.IsShowing);
            if (view == null)
            {
                Debug.Log($"[Bubble] 남은 말풍선이 없어 건너뜀 ({views.Count}개 모두 사용 중): {body}", this);
                return false;
            }

            view.transform.SetAsLastSibling();
            view.Show(target, offset, body, channel, holdSeconds, fadeSeconds, worldCamera);
            Debug.Log($"[Bubble] {channel} @ {target.name}: {body}", this);
            return true;
        }

        public void HideAll()
        {
            foreach (BubbleView view in views) view.Hide();
        }

        private BubbleView Find(Transform target)
        {
            foreach (BubbleView view in views)
            {
                if (view.IsShowing && view.Target == target) return view;
            }
            return null;
        }
    }
}
