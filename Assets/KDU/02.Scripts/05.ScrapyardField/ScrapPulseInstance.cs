using UnityEngine;

// 파동 1개. 픽셀아트라 스케일 보간 없이 스프라이트 프레임만 교체한다
[RequireComponent(typeof(SpriteRenderer))]
public class ScrapPulseInstance : MonoBehaviour
{
    [Tooltip("재생할 프레임. 배열 순서대로 교체된다")]
    [SerializeField] private Sprite[] _frames;

    [Tooltip("초당 프레임 수")]
    [Min(1f)]
    [SerializeField] private float _framesPerSecond = 12f;

    private SpriteRenderer _renderer;
    private float _timer;
    private int _frame;
    private float _alpha = 1f;
    private bool _playing;

    public bool Playing => _playing;

    // 풀에서 꺼낼 때 비활성 상태라 Awake에 의존할 수 없다
    private SpriteRenderer Renderer => _renderer != null ? _renderer : (_renderer = GetComponent<SpriteRenderer>());

    // 알파는 강도에서 온다
    public void Play(float alpha)
    {
        if (_frames == null || _frames.Length == 0)
            return;

        _alpha = Mathf.Clamp01(alpha);
        _frame = 0;
        _timer = 0f;
        _playing = true;
        gameObject.SetActive(true);
        ApplyFrame();
    }

    public void Stop()
    {
        _playing = false;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_playing)
            return;

        _timer += Time.deltaTime;

        int target = Mathf.FloorToInt(_timer * _framesPerSecond);
        if (target == _frame)
            return;

        _frame = target;
        if (_frame >= _frames.Length)
        {
            Stop();
            return;
        }

        ApplyFrame();
    }

    private void ApplyFrame()
    {
        Renderer.sprite = _frames[_frame];

        Color color = Renderer.color;
        color.a = _alpha;
        Renderer.color = color;
    }
}
