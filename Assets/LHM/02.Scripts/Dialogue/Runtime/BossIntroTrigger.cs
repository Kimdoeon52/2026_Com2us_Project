using UnityEngine;
using UnityEngine.Events;

namespace RealSteel.Dialogue.Unity
{
    /// <summary>
    /// 보스 아레나 진입 연출. 대화가 끝나면(완료/스킵/반복 생략/후보 없음 모두) onIntroFinished로 전투 시작.
    /// 재도전 피로도는 데이터의 repeatPolicy(ShortOnRepeat/SkipOnRepeat)가 처리한다 → 코드 분기 없음.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class BossIntroTrigger : MonoBehaviour
    {
        [SerializeField] private string poolId = "boss.wheel01.intro";
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private UnityEvent onIntroFinished;

        private bool _fired;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_fired || !other.CompareTag(playerTag)) return;
            _fired = true;

            var service = DialogueService.Instance;
            if (service == null) { onIntroFinished.Invoke(); return; }
            service.RequestBlocking(poolId, onDone: _ => onIntroFinished.Invoke());
        }

        /// <summary>재도전 시 씬을 다시 로드하지 않는 구조라면 리셋 호출</summary>
        public void ResetTrigger() => _fired = false;
    }
}
