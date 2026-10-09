using UnityEngine;
using UnityEngine.Events;

namespace RealSteel.Dialogue.Unity
{
    /// <summary>
    /// 씬 진입 시 메인 스토리 풀 호출. 어떤 챕터 대사가 나올지는 story.chapter 등 조건이 결정한다.
    /// 해당 챕터에 할 말이 없으면(NoCandidate) 그냥 통과한다.
    /// </summary>
    public sealed class StorySceneTrigger : MonoBehaviour
    {
        [SerializeField] private string poolId = "story.scrapyard.enter";
        [SerializeField, Tooltip("씬 로딩 연출 후 시작하도록 지연")] private float delaySec = 0.5f;
        [SerializeField] private UnityEvent onFinished;

        private void Start() => Invoke(nameof(Fire), delaySec);

        private void Fire()
        {
            var service = DialogueService.Instance;
            if (service == null) { onFinished.Invoke(); return; }
            service.RequestBlocking(poolId, onDone: _ => onFinished.Invoke());
        }
    }
}
