using DG.Tweening;
using RealSteel.Dialogue;
using RealSteel.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Esc로 여는 일시정지 메뉴. 이 오브젝트는 항상 켜 두고 _panel만 켜고 끈다
// 여는 연출은 _panel에 붙은 WindowOpenEffect가 다른 창들과 똑같이 해 준다
public class PauseMenu : MonoBehaviour
{
    private const float CloseTime = 0.15f;

    [Header("화면")]
    [Tooltip("켜고 끌 일시정지 화면")]
    [SerializeField] private GameObject _panel;

    [Tooltip("기준 해상도로 배치한 내용. 화면 크기에 맞춰 비율을 유지한 채 줄이고 늘린다")]
    [SerializeField] private RectTransform _content;
    [SerializeField] private Vector2 _referenceSize = new Vector2(1920f, 1080f);

    [Header("버튼")]
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _quitButton;

    [Header("현재 상태")]
    [Tooltip("날짜 / 멈춘 시각 / 현재 위치를 보여줄 글자")]
    [SerializeField] private TMP_Text _statusText;

    [Tooltip("씬 이름별 현재 지역 이름. 목록에 없는 씬은 지역을 비운다")]
    [SerializeField] private SceneLabel[] _sceneLabels =
    {
        new SceneLabel { Scene = "Main_Home_Workshop", Label = "거점" },
        new SceneLabel { Scene = "Main_Junk_Shop", Label = "고물상" },
        new SceneLabel { Scene = "Main_Auction_Hall", Label = "경매장" },
        new SceneLabel { Scene = "Main_League_Lobby", Label = "길드" },
        new SceneLabel { Scene = "Main_Fight_Map_1", Label = "경기장" },
        new SceneLabel { Scene = "Main_Boss1", Label = "보스전" },
    };

    [System.Serializable]
    private struct SceneLabel
    {
        public string Scene;
        public string Label;
    }

    [Header("연결")]
    [Tooltip("환경설정 버튼으로 열 창. 비어 있으면 경고만 띄운다")]
    [SerializeField] private GameObject _settingsWindow;

    [Tooltip("게임 나가기로 이동할 씬 (Build Settings에 있어야 함). 비우면 프로그램을 종료한다")]
    [SerializeField] private string _quitScene = "MainMenu";

    [Tooltip("이 창들 중 하나라도 열려 있으면 Esc는 그 창 닫기에 양보하고 메뉴를 열지 않는다")]
    [SerializeField] private GameObject[] _blockers;

    [SerializeField] private KeyCode _key = KeyCode.Escape;

    private CanvasGroup _group;
    private bool _blockedLastFrame;
    private bool _closing;

    // 게임 시계는 Time.timeScale과 상관없이 흐르므로 따로 멈췄는지 기억한다
    private bool _stoppedClock;
    private bool _pushedUiStack;

    public bool IsOpen => _panel.activeSelf && !_closing;

    private void Awake()
    {
        _group = _panel.GetComponent<CanvasGroup>();
        if (_group == null) _group = _panel.AddComponent<CanvasGroup>();
        _panel.SetActive(false);

        _resumeButton.onClick.AddListener(Resume);
        _settingsButton.onClick.AddListener(OpenSettings);
        _quitButton.onClick.AddListener(QuitGame);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(_key)) return;

        if (IsOpen)
        {
            // 설정 창이 떠 있으면 그것부터 닫는다
            if (_settingsWindow != null && _settingsWindow.activeSelf)
                _settingsWindow.SetActive(false);
            else
                Resume();
        }
        // 같은 Esc로 다른 창이 먼저 닫혔을 수 있어서, 지난 프레임 기준으로 막혀 있었는지 본다
        else if (!_panel.activeSelf && !_blockedLastFrame)
        {
            Open();
        }
    }

    private void LateUpdate()
    {
        // 메뉴가 닫혀 있을 때만 잰다 (열려 있는 동안은 메뉴 자신이 UI 스택 · 게임 정지에 들어가 있음)
        _blockedLastFrame = !_panel.activeSelf && (AnyBlockerOpen() || OtherUiOpen());

        // 열려 있는 동안 창 크기가 바뀌어도 맞춘다
        if (_panel.activeSelf) FitContent();
    }

    // 캔버스가 고정 픽셀 크기라 해상도가 달라도 기준 해상도 배치가 그대로 보이게 비율을 맞춘다
    private void FitContent()
    {
        if (_content == null || !(_content.parent is RectTransform parent)) return;

        Rect area = parent.rect;
        float scale = Mathf.Min(area.width / _referenceSize.x, area.height / _referenceSize.y);
        if (scale > 0f) _content.localScale = new Vector3(scale, scale, 1f);
    }

    private void OnDisable()
    {
        ResumeTime();
    }

    public void Open()
    {
        _closing = false;
        _group.DOKill();
        _group.interactable = true;

        // 글자가 한 글자씩 나오는 연출이 켜질 때 글자 수를 재므로 켜기 전에 채운다
        RefreshStatus();
        _panel.SetActive(true);
        FitContent();
        StopTime();
    }

    // 실제 시간(timeScale)과 게임 시계(TimeSystemManager)를 함께 멈춘다
    // 열린 UI를 세는 LHM UI 스택이 있으면 거기에 등록해서 다른 창들과 같은 방식으로 시계를 멈춘다
    private void StopTime()
    {
        GamePause.Push(this);

        if (UiStack.Current != null)
        {
            UiStack.Push(this);
            _pushedUiStack = true;
            return;
        }

        // 시계가 정지 주체를 세므로 다른 창이 이미 멈춰 두었어도 등록해 둔다
        TimeSystemManager clock = GameClock();
        if (clock != null)
        {
            clock.Pause(this);
            _stoppedClock = true;
        }
    }

    // 메뉴가 멈춘 것만 되돌린다. 메뉴 동안 다른 창이 시계 상태를 바꿨으면 건드리지 않는다
    private void ResumeTime()
    {
        GamePause.Remove(this);

        if (_pushedUiStack)
        {
            _pushedUiStack = false;
            UiStack.Remove(this);
        }

        if (!_stoppedClock) return;
        _stoppedClock = false;

        TimeSystemManager clock = GameClock();
        if (clock != null)
            clock.Resume(this);
    }

    // 게임 내 날짜·시각 (TimeSystemManager의 year · month · day · hour) / 현재 지역
    // 예: 2080.03.28  /  14:00  /  고물상
    private void RefreshStatus()
    {
        string location = CurrentLocation();

        // 진행 상황 · 저장 정보 · 버전 글자를 실제 값으로 채운다 (값이 없는 칸은 "-")
        RealSteel.MainMenu.PauseMenuInfo.Fill(_panel.transform, location);

        if (_statusText == null) return;

        TimeSystemManager clock = GameClock();
        string date = clock != null ? $"{clock.year}.{clock.month:D2}.{clock.day:D2}  /  {clock.hour:D2}:00" : "----.--.--  /  --:--";

        _statusText.text = string.IsNullOrEmpty(location) ? date : $"{date}  /  {location}";
    }

    // 싱글톤이 아직 안 잡혔거나 꺼져 있어도 씬에 있으면 찾는다
    private static TimeSystemManager GameClock()
    {
        if (TimeSystemManager.Instance != null) return TimeSystemManager.Instance;
        return FindAnyObjectByType<TimeSystemManager>(FindObjectsInactive.Include);
    }

    private string CurrentLocation()
    {
        string scene = SceneManager.GetActiveScene().name;

        // 목록에 있는 본 게임(Main) 씬만 지역을 보여주고, 테스트 씬 등은 비운다
        if (_sceneLabels != null)
        {
            foreach (SceneLabel label in _sceneLabels)
            {
                if (label.Scene == scene && !string.IsNullOrEmpty(label.Label))
                    return label.Label;
            }
        }

        return "";
    }

    public void Resume()
    {
        if (!_panel.activeSelf || _closing) return;

        _closing = true;
        _group.interactable = false;
        if (_settingsWindow != null) _settingsWindow.SetActive(false);

        _group.DOKill();
        _group.DOFade(0f, CloseTime).SetUpdate(true).SetLink(gameObject).OnComplete(() =>
        {
            _panel.SetActive(false);
            _closing = false;
            ResumeTime();
        });
    }

    public void OpenSettings()
    {
        if (_settingsWindow == null)
        {
            Debug.LogWarning("[PauseMenu] 환경설정 창이 연결되지 않았습니다.", this);
            return;
        }

        _settingsWindow.SetActive(true);
    }

    public void QuitGame()
    {
        ResumeTime();
        _group.DOKill();

        if (!string.IsNullOrEmpty(_quitScene) && Application.CanStreamedLevelBeLoaded(_quitScene))
        {
            SceneManager.LoadScene(_quitScene);
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 대화창 · NPC 선택지 · 튜토리얼 · 보스 선택 · 달력 같은 창은 열릴 때 LHM UI 스택이나 게임 정지에 등록되므로
    // 하나라도 열려 있으면 Esc는 그 창(대화 스킵 · 닫기)에 양보한다. 창마다 _blockers에 넣지 않아도 된다
    private static bool OtherUiOpen()
    {
        if (UiStack.Current != null && UiStack.Current.Count > 0) return true;
        return GamePause.IsPaused;
    }

    private bool AnyBlockerOpen()
    {
        if (_blockers == null) return false;

        foreach (GameObject blocker in _blockers)
        {
            if (blocker != null && blocker.activeInHierarchy) return true;
        }
        return false;
    }
}
