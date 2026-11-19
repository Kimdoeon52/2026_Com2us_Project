using DG.Tweening;
using RealSteel.Tutorial;
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
        _blockedLastFrame = AnyBlockerOpen();

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
        GamePause.Remove(this);
    }

    public void Open()
    {
        _closing = false;
        _group.DOKill();
        _group.interactable = true;
        _panel.SetActive(true);
        FitContent();
        GamePause.Push(this);
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
            GamePause.Remove(this);
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
        GamePause.Remove(this);
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
