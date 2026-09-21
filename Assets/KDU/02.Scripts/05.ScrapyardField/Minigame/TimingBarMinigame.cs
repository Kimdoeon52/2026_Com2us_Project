using UnityEngine;

// 왕복하는 커서를 성공 구간에서 멈추는 게임.
// 난이도 수치는 전부 인스펙터에 있고 기본값은 임시다
public class TimingBarMinigame : ScrapMinigameBase
{
    [Header("바")]
    [Tooltip("커서가 지나다니는 트랙. 가로 길이를 기준으로 좌표를 계산한다")]
    [SerializeField] private RectTransform _trackRect;

    [Tooltip("움직이는 커서. 앵커는 가운데로 둔다")]
    [SerializeField] private RectTransform _cursorRect;

    [Tooltip("성공 판정 구간. 앵커는 가운데로 둔다")]
    [SerializeField] private RectTransform _zoneRect;

    [Header("난이도")]
    [Tooltip("초당 왕복 횟수")]
    [Min(0f)]
    [SerializeField] private float _speed = 0.8f;

    [Tooltip("성공 구간 폭. 트랙 가로 대비 비율")]
    [Range(0f, 1f)]
    [SerializeField] private float _zoneWidth01 = 0.073f;

    [Tooltip("성공 구간 위치. 켜면 판마다 무작위, 끄면 항상 트랙 한가운데")]
    [SerializeField] private bool _randomizeZone = true;

    [Tooltip("성공으로 인정할 횟수")]
    [Min(1)]
    [SerializeField] private int _requiredHits = 1;

    [Tooltip("연속으로 맞혀야 하는지. 켜면 한 번 빗나갈 때 성공 횟수가 0으로 돌아간다")]
    [SerializeField] private bool _requireConsecutive;

    [Tooltip("허용 실패 횟수. 0이면 무제한이라 제한 시간으로만 진다")]
    [Min(0)]
    [SerializeField] private int _maxMisses = 3;

    [Header("입력")]
    [Tooltip("판정 키")]
    [SerializeField] private KeyCode _hitKey = KeyCode.Space;

    [Tooltip("난수 시드. 0이면 실행할 때마다 달라진다")]
    [SerializeField] private int _seed;

    private Rng _rng;
    private float _elapsed;
    private float _cursor01;
    private float _zoneCenter01;
    private int _hits;
    private int _misses;

    protected override void OnBegin()
    {
        // 시드를 고정하면 매 판 같은 자리에 구간이 뜬다
        if (_rng == null)
            _rng = new Rng(_seed != 0 ? _seed : System.Environment.TickCount);

        _elapsed = 0f;
        _cursor01 = 0f;
        _hits = 0;
        _misses = 0;

        PlaceZone();
        PlaceCursor();
    }

    protected override void OnTick()
    {
        _elapsed += Time.deltaTime;
        _cursor01 = Mathf.PingPong(_elapsed * _speed, 1f);
        PlaceCursor();

        if (!Input.GetKeyDown(_hitKey))
            return;

        if (Mathf.Abs(_cursor01 - _zoneCenter01) <= _zoneWidth01 * 0.5f)
            Hit();
        else
            Miss();
    }

    protected override float PerformanceOnTimeout() => Performance();

    private void Hit()
    {
        _hits++;

        if (_hits >= _requiredHits)
        {
            Finish(ScrapMinigameResult.Win(Performance()));
            return;
        }

        PlaceZone();
    }

    private void Miss()
    {
        _misses++;

        if (_requireConsecutive)
            _hits = 0;

        if (_maxMisses > 0 && _misses >= _maxMisses)
        {
            Finish(ScrapMinigameResult.Lose(Performance()));
            return;
        }

        PlaceZone();
    }

    private float Performance()
    {
        return _requiredHits <= 0 ? 0f : Mathf.Clamp01((float)_hits / _requiredHits);
    }

    private void PlaceCursor()
    {
        if (_cursorRect == null || _trackRect == null)
            return;

        float width = _trackRect.rect.width;
        _cursorRect.anchoredPosition = new Vector2((_cursor01 - 0.5f) * width, _cursorRect.anchoredPosition.y);
    }

    private void PlaceZone()
    {
        float half = _zoneWidth01 * 0.5f;
        _zoneCenter01 = _randomizeZone ? _rng.Range(half, 1f - half) : 0.5f;

        if (_zoneRect == null || _trackRect == null)
            return;

        float width = _trackRect.rect.width;
        _zoneRect.sizeDelta = new Vector2(width * _zoneWidth01, _zoneRect.sizeDelta.y);
        _zoneRect.anchoredPosition = new Vector2((_zoneCenter01 - 0.5f) * width, _zoneRect.anchoredPosition.y);
    }
}
