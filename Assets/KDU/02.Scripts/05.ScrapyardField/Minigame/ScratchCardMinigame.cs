using UnityEngine;
using UnityEngine.UI;

// 마우스로 표면을 긁어 일정 비율을 넘기면 성공
public class ScratchCardMinigame : ScrapMinigameBase
{
    [Header("긁기")]
    [Tooltip("긁기 표면")]
    [SerializeField] private ScratchSurface _surface;

    [Tooltip("성공으로 인정할 긁힘 비율. 1로 두면 한 픽셀만 남아도 끝나지 않는다")]
    [Range(0f, 1f)]
    [SerializeField] private float _requiredCleared01 = 0.95f;

    [Header("연출")]
    [Tooltip("긁으면 드러나는 밑그림")]
    [SerializeField] private Image _rewardImage;

    [Tooltip("밑그림 색 후보. 판마다 하나를 골라 매번 다른 판임을 보여준다")]
    [SerializeField] private Color[] _rewardColors =
    {
        new Color(0.85f, 0.66f, 0.28f),
        new Color(0.36f, 0.72f, 0.65f),
        new Color(0.78f, 0.35f, 0.26f)
    };

    [Tooltip("난수 시드. 0이면 실행할 때마다 달라진다")]
    [SerializeField] private int _seed;

    private Rng _rng;

    protected override void OnBegin()
    {
        if (_rng == null)
            _rng = new Rng(_seed != 0 ? _seed : System.Environment.TickCount);

        PickRewardColor();

        if (_surface == null)
            return;

        _surface.Rebuild();
        _surface.SetInputEnabled(true);
    }

    // 색은 연출일 뿐이라 보상 등급과 무관하다
    private void PickRewardColor()
    {
        if (_rewardImage == null || _rewardColors == null || _rewardColors.Length == 0)
            return;

        _rewardImage.color = _rewardColors[_rng.Range(0, _rewardColors.Length)];
    }

    protected override void OnTick()
    {
        if (_surface == null)
            return;

        if (_surface.Cleared01 >= _requiredCleared01)
            Finish(ScrapMinigameResult.Win(_surface.Cleared01));
    }

    protected override void OnFinish()
    {
        if (_surface != null)
            _surface.SetInputEnabled(false);
    }

    protected override float PerformanceOnTimeout() => _surface != null ? _surface.Cleared01 : 0f;
}
