using System;
using TMPro;
using UnityEngine;

// 미니게임 3종의 공통 뼈대. 오버레이·입력 차단·제한 시간을 처리하고
// 파생 클래스는 게임 로직만 담당한다
public abstract class ScrapMinigameBase : MonoBehaviour, IScrapMinigame
{
    [Header("공통")]
    [Tooltip("미니게임 UI 루트. 시작할 때 켜고 끝나면 끈다")]
    [SerializeField] private GameObject _overlayRoot;

    [Tooltip("제한 시간(초). 0이면 무제한")]
    [Min(0f)]
    [SerializeField] private float _timeLimit;

    [Tooltip("남은 시간 표시. 00.00 형식")]
    [SerializeField] private TMP_Text _timerLabel;

    private static int _runningCount;

    private Action<ScrapMinigameResult> _onComplete;
    private float _remaining;
    private bool _running;

    // 미니게임이 하나라도 진행 중인지. 다른 창이 열리지 않게 막는 데 쓴다
    public static bool IsAnyRunning => _runningCount > 0;

    protected bool IsRunning => _running;
    protected float TimeLimit => _timeLimit;
    protected float Remaining => _remaining;

    public void Begin(Action<ScrapMinigameResult> onComplete)
    {
        if (_running)
            return;

        _onComplete = onComplete;
        _running = true;
        _remaining = _timeLimit;
        _runningCount++;

        ScrapInputGate.Push();

        if (_overlayRoot != null)
            _overlayRoot.SetActive(true);

        UpdateTimerLabel();
        OnBegin();
    }

    // 파생 클래스는 Update 대신 OnTick을 쓴다
    private void Update()
    {
        if (!_running)
            return;

        if (_timeLimit > 0f)
        {
            _remaining = Mathf.Max(0f, _remaining - Time.deltaTime);
            UpdateTimerLabel();

            if (_remaining <= 0f)
            {
                OnTimeout();
                return;
            }
        }

        OnTick();
    }

    // 게임을 끝낼 때 파생 클래스가 부른다
    protected void Finish(ScrapMinigameResult result)
    {
        if (!_running)
            return;

        _running = false;
        OnFinish();

        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);

        ScrapInputGate.Pop();
        _runningCount = Mathf.Max(0, _runningCount - 1);

        Action<ScrapMinigameResult> callback = _onComplete;
        _onComplete = null;
        callback?.Invoke(result);
    }

    protected virtual void OnTimeout() => Finish(ScrapMinigameResult.Lose(PerformanceOnTimeout()));

    // 시간 초과 시에도 성과도는 남긴다
    protected virtual float PerformanceOnTimeout() => 0f;

    protected abstract void OnBegin();

    protected virtual void OnTick() { }

    protected virtual void OnFinish() { }

    // 도메인 리로드를 끈 상태에서도 플레이 진입 시 풀려 있어야 한다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _runningCount = 0;

    private void UpdateTimerLabel()
    {
        if (_timerLabel == null)
            return;

        _timerLabel.text = _timeLimit > 0f ? _remaining.ToString("00.00") : string.Empty;
    }
}
