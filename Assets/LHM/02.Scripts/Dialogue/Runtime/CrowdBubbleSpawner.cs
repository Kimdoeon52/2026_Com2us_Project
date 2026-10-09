using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace RealSteel.Dialogue.Unity
{
    public enum CrowdMoment { Idle, RoundStart, BossStunned, PlayerHit, PartBroken, BossLowHp }

    /// <summary>
    /// 관중 비강제 말풍선. 플레이어가 무시해도 아무 문제 없는 "분위기" 연출이므로
    ///  - 입력/시간을 절대 건드리지 않는다
    ///  - 동시 개수 상한 + 화면 밖 관중은 제외 + 같은 관중 중복 발화 금지
    ///  - Idle은 랜덤 간격, 전투 이벤트는 확률적으로 반응 (매번 반응하면 시끄럽다)
    /// </summary>
    public sealed class CrowdBubbleSpawner : MonoBehaviour
    {
        [SerializeField] private string poolId = "crowd.arena01";
        [SerializeField] private Transform[] crowdAnchors;
        [SerializeField] private CrowdBubble bubblePrefab;
        [SerializeField] private Camera cam;
        [SerializeField] private int maxConcurrent = 3;
        [SerializeField] private float bubbleLifeSec = 2.0f;
        [SerializeField] private Vector2 idleIntervalSec = new Vector2(4f, 8f);
        [SerializeField, Range(0f, 1f)] private float reactChance = 0.7f;

        private ObjectPool<CrowdBubble> _pool;
        private readonly List<CrowdBubble> _active = new List<CrowdBubble>();
        private readonly List<Transform> _candidates = new List<Transform>();
        private readonly Dictionary<string, DVal> _args = new Dictionary<string, DVal>();
        private float _nextIdleAt;

        /// <summary>보스 체력 비율 공급 (BossLowHp 판정용). 보스 스크립트가 설정</summary>
        public System.Func<float> BossHpRatio = () => 1f;

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
            _pool = new ObjectPool<CrowdBubble>(
                createFunc: () => Instantiate(bubblePrefab, transform),
                actionOnGet: b => _active.Add(b),
                actionOnRelease: b => _active.Remove(b),
                actionOnDestroy: b => { if (b != null) Destroy(b.gameObject); },
                collectionCheck: false, defaultCapacity: maxConcurrent, maxSize: maxConcurrent * 2);
            ScheduleIdle();
        }

        private void Update()
        {
            if (Time.time >= _nextIdleAt)
            {
                Emit(CrowdMoment.Idle, force: true);
                ScheduleIdle();
            }
        }

        private void ScheduleIdle() => _nextIdleAt = Time.time + Random.Range(idleIntervalSec.x, idleIntervalSec.y);

        /// <summary>전투 이벤트에서 호출. force=false면 reactChance 확률로만 반응</summary>
        public void Emit(CrowdMoment moment, bool force = false)
        {
            if (_active.Count >= maxConcurrent) return;
            if (!force && Random.value > reactChance) return;

            var anchor = PickAnchor();
            if (anchor == null) return;

            var service = DialogueService.Instance;
            if (service == null) return;

            _args.Clear();
            _args["event.crowdMoment"] = DVal.Str(moment.ToString());
            _args["event.bossHpRatio"] = DVal.Float(BossHpRatio());

            var entry = service.RequestInstant(poolId, _args);
            if (entry == null) return;

            var bubble = _pool.Get();
            bubble.Show(anchor, service.Text.ResolveLine(entry.lines[0]), bubbleLifeSec, b => _pool.Release(b));
        }

        private Transform PickAnchor()
        {
            _candidates.Clear();
            foreach (var a in crowdAnchors)
            {
                if (a == null || IsOccupied(a)) continue;
                if (cam != null)
                {
                    var vp = cam.WorldToViewportPoint(a.position);
                    if (vp.z < 0f || vp.x < 0.05f || vp.x > 0.95f || vp.y < 0.05f || vp.y > 0.95f) continue;
                }
                _candidates.Add(a);
            }
            return _candidates.Count == 0 ? null : _candidates[Random.Range(0, _candidates.Count)];
        }

        private bool IsOccupied(Transform a)
        {
            foreach (var b in _active) if (b.Anchor == a) return true;
            return false;
        }
    }
}
