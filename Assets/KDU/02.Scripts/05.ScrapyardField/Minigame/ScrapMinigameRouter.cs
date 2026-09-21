using System;
using UnityEngine;

// 후보 미니게임 중 하나를 뽑아 위임한다. 호출부는 이것만 알면 된다.
// 게임 추가는 이 목록에 넣는 것으로 끝난다
public class ScrapMinigameRouter : MonoBehaviour, IScrapMinigame
{
    [Tooltip("후보 미니게임")]
    [SerializeField] private ScrapMinigameBase[] _minigames;

    [Tooltip("난수 시드. 0이면 실행할 때마다 순서가 달라진다")]
    [SerializeField] private int _seed;

    private Rng _rng;

    private void Awake() => _rng = new Rng(_seed != 0 ? _seed : System.Environment.TickCount);

    public void Begin(Action<ScrapMinigameResult> onComplete)
    {
        ScrapMinigameBase game = Pick();

        if (game == null)
        {
            onComplete?.Invoke(ScrapMinigameResult.Lose(0f));
            return;
        }

        game.Begin(onComplete);
    }

    private ScrapMinigameBase Pick()
    {
        if (_minigames == null || _minigames.Length == 0)
            return null;

        int start = _rng.Range(0, _minigames.Length);

        // 비어 있는 슬롯은 건너뛴다
        for (int i = 0; i < _minigames.Length; i++)
        {
            ScrapMinigameBase candidate = _minigames[(start + i) % _minigames.Length];
            if (candidate != null)
                return candidate;
        }

        return null;
    }
}
