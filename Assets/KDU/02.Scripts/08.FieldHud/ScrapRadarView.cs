using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 미니맵 레이더. 플레이어 기준으로 부채꼴이 돌고, 부채꼴이 훑은 고물 노드만 잠깐 점으로 보인다
public class ScrapRadarView : MonoBehaviour
{
    [Header("추적")]
    [Tooltip("플레이어 Transform을 여기서 읽는다")]
    [SerializeField] private ScrapNodeDetector _detector;

    [Tooltip("탐지기가 없을 때 추적 대상")]
    [SerializeField] private Transform _target;

    [Tooltip("레이더 가장자리에 해당하는 월드 거리")]
    [Min(0.1f)][SerializeField] private float _range = 30f;

    [Tooltip("카메라 방향에 맞춰 회전")]
    [SerializeField] private bool _alignToCamera = true;

    [Header("부채꼴")]
    [Tooltip("Filled Radial360 이미지. 채움 비율이 부채꼴 각도")]
    [SerializeField] private Image _sweep;

    [Tooltip("초당 회전 각도")]
    [SerializeField] private float _sweepSpeed = 90f;

    [Header("노드 점")]
    [Tooltip("점이 놓일 영역. 중심 기준")]
    [SerializeField] private RectTransform _blipRoot;

    [Tooltip("점 원본. 꺼진 상태로 둔다")]
    [SerializeField] private GameObject _blipTemplate;

    [Tooltip("훑은 뒤 사라지는 초")]
    [Min(0.01f)][SerializeField] private float _fadeTime = 1.5f;

    // 노드별 마지막으로 훑인 시각
    private readonly Dictionary<ScrapNode, float> _lastHit = new Dictionary<ScrapNode, float>();
    private readonly List<ScrapNode> _expired = new List<ScrapNode>();

    private Transform Target => _detector != null && _detector.TrackedTarget != null ? _detector.TrackedTarget : _target;

    private void OnDisable()
    {
        _lastHit.Clear();
    }

    private void Update()
    {
        if (_sweep != null)
            _sweep.rectTransform.Rotate(0f, 0f, -_sweepSpeed * Time.unscaledDeltaTime);

        RefreshBlips();
    }

    private void RefreshBlips()
    {
        if (_blipRoot == null || _blipTemplate == null)
            return;

        Transform target = Target;
        float now = Time.unscaledTime;
        int count = 0;

        if (target != null)
        {
            float radius = Mathf.Min(_blipRoot.rect.width, _blipRoot.rect.height) * 0.5f;
            Quaternion view = GetViewRotation();
            IReadOnlyList<ScrapNode> nodes = ScrapNodeRegistry.Nodes;

            for (int i = 0; i < nodes.Count; i++)
            {
                ScrapNode node = nodes[i];
                if (node == null || node.Consumed)
                    continue;

                Vector3 offset = view * (node.transform.position - target.position);
                Vector2 flat = new Vector2(offset.x, offset.z);
                if (flat.magnitude > _range)
                    continue;

                if (IsInSweep(flat))
                    _lastHit[node] = now;

                if (!_lastHit.TryGetValue(node, out float hit))
                    continue;

                float alpha = 1f - (now - hit) / _fadeTime;
                if (alpha <= 0f)
                    continue;

                Transform blip = _blipRoot.GetOrCreateChild(_blipTemplate, count);
                blip.gameObject.SetActive(true);
                ((RectTransform)blip).anchoredPosition = flat / _range * radius;
                SetAlpha(blip, MathUtils.EaseOutQuad(alpha));
                count++;
            }
        }

        for (int i = count; i < _blipRoot.childCount; i++)
        {
            _blipRoot.GetChild(i).gameObject.SetActive(false);
        }

        ForgetStale(now);
    }

    // 부채꼴은 위쪽 가장자리에서 반시계로 펼쳐진다
    private bool IsInSweep(Vector2 flat)
    {
        if (_sweep == null || flat.sqrMagnitude < 0.0001f)
            return false;

        float edge = 90f + _sweep.rectTransform.localEulerAngles.z;
        float angle = Mathf.Atan2(flat.y, flat.x) * Mathf.Rad2Deg;
        float diff = Mathf.DeltaAngle(edge, angle);
        return diff >= 0f && diff <= _sweep.fillAmount * 360f;
    }

    // 다 사라졌거나 수집된 노드 기록은 지운다
    private void ForgetStale(float now)
    {
        _expired.Clear();
        foreach (KeyValuePair<ScrapNode, float> pair in _lastHit)
        {
            if (pair.Key == null || pair.Key.Consumed || now - pair.Value > _fadeTime)
                _expired.Add(pair.Key);
        }

        for (int i = 0; i < _expired.Count; i++)
        {
            _lastHit.Remove(_expired[i]);
        }
    }

    private static void SetAlpha(Transform blip, float alpha)
    {
        if (!blip.TryGetComponent(out Graphic graphic))
            return;

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    // 화면 위쪽이 카메라 정면이 되도록 월드 좌표를 돌린다
    private Quaternion GetViewRotation()
    {
        Camera cam = _alignToCamera ? Camera.main : null;
        if (cam == null)
            return Quaternion.identity;

        return Quaternion.Euler(0f, -cam.transform.eulerAngles.y, 0f);
    }
}
