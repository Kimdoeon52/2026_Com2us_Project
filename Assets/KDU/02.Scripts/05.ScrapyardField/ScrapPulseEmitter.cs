using System.Collections.Generic;
using UnityEngine;

// 발밑 파동 연출. 플레이어 자식으로 붙이되 플레이어 스크립트는 참조하지 않는다
public class ScrapPulseEmitter : MonoBehaviour
{
    [Tooltip("강도 공급원")]
    [SerializeField] private ScrapNodeDetector _detector;

    [Tooltip("파동 프리팹. 바닥에 눕힌 회전을 프리팹에 넣어둔다")]
    [SerializeField] private ScrapPulseInstance _pulsePrefab;

    [Tooltip("강도 0일 때 발생 간격(초)")]
    [Min(0.01f)]
    [SerializeField] private float _intervalAtMin = 1.6f;

    [Tooltip("강도 1일 때 발생 간격(초)")]
    [Min(0.01f)]
    [SerializeField] private float _intervalAtMax = 0.18f;

    [Tooltip("강도 0일 때와 1일 때의 알파")]
    [MinMax(0f, 1f)]
    [SerializeField] private Vector2 _alphaRange = new Vector2(0.2f, 1f);

    [Tooltip("이 강도 미만이면 파동을 내지 않는다")]
    [Range(0f, 1f)]
    [SerializeField] private float _minIntensity = 0.01f;

    private readonly List<ScrapPulseInstance> _instances = new List<ScrapPulseInstance>();
    private float _timer;

    private void Update()
    {
        float intensity = _detector != null ? _detector.State.Intensity01 : 0f;

        if (intensity < _minIntensity)
        {
            _timer = 0f;
            return;
        }

        _timer -= Time.deltaTime;
        if (_timer > 0f)
            return;

        _timer = Mathf.Lerp(_intervalAtMin, _intervalAtMax, intensity);
        Emit(Mathf.Lerp(_alphaRange.x, _alphaRange.y, intensity));
    }

    private void Emit(float alpha)
    {
        if (_pulsePrefab == null)
            return;

        ScrapPulseInstance instance = Rent();

        // 파동은 발생 지점에 머문다. 플레이어 회전을 따라가면 타원이 돌아버린다
        instance.transform.SetPositionAndRotation(transform.position, _pulsePrefab.transform.rotation);
        instance.Play(alpha);
    }

    private ScrapPulseInstance Rent()
    {
        for (int i = 0; i < _instances.Count; i++)
        {
            if (!_instances[i].Playing)
                return _instances[i];
        }

        ScrapPulseInstance created = Instantiate(_pulsePrefab);
        _instances.Add(created);
        return created;
    }
}
