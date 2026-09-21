using UnityEngine;

// 같은 강도값을 받는 사운드. 파동 연출 컴포넌트와 서로 참조하지 않는다
[RequireComponent(typeof(AudioSource))]
public class ScrapDetectionAudio : MonoBehaviour
{
    [Tooltip("강도 공급원")]
    [SerializeField] private ScrapNodeDetector _detector;

    [Tooltip("탐지음")]
    [SerializeField] private AudioClip _pingClip;

    [Tooltip("강도 0일 때 재생 간격(초)")]
    [Min(0.01f)]
    [SerializeField] private float _intervalAtMin = 1.6f;

    [Tooltip("강도 1일 때 재생 간격(초)")]
    [Min(0.01f)]
    [SerializeField] private float _intervalAtMax = 0.18f;

    [Tooltip("강도 0일 때와 1일 때의 볼륨")]
    [MinMax(0f, 1f)]
    [SerializeField] private Vector2 _volumeRange = new Vector2(0.15f, 1f);

    [Tooltip("강도 0일 때와 1일 때의 피치")]
    [SerializeField] private Vector2 _pitchRange = new Vector2(0.9f, 1.25f);

    [Tooltip("이 강도 미만이면 소리를 내지 않는다")]
    [Range(0f, 1f)]
    [SerializeField] private float _minIntensity = 0.01f;

    private AudioSource _source;
    private float _timer;

    private void Awake() => _source = GetComponent<AudioSource>();

    private void Update()
    {
        float intensity = _detector != null ? _detector.State.Intensity01 : 0f;

        if (_pingClip == null || intensity < _minIntensity)
        {
            _timer = 0f;
            return;
        }

        _timer -= Time.deltaTime;
        if (_timer > 0f)
            return;

        _timer = Mathf.Lerp(_intervalAtMin, _intervalAtMax, intensity);
        _source.pitch = Mathf.Lerp(_pitchRange.x, _pitchRange.y, intensity);
        _source.PlayOneShot(_pingClip, Mathf.Lerp(_volumeRange.x, _volumeRange.y, intensity));
    }
}
