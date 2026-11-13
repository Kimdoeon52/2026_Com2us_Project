using Michsky.UI.Heat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.MainMenu
{
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        public const string MasterVolumeKey = "RealSteel Master Volume";
        public const string HomePanel = "Home";
        public const string SettingsPanel = "Settings";
        private const string HeatSliderPrefix = "Slider_";

        [Header("연결")]
        [SerializeField] private PanelManager mainPanels;
        [SerializeField] private CreditsManager credits;
        [SerializeField] private ModalWindowManager quitWindow;
        [SerializeField] private BoxButtonManager continueButton;

        [Header("씬")]
        [SerializeField] private string newGameScene = "OpeningCutscene";
        [SerializeField] private string continueScene = "JunkShop_Map";

        [Header("이어하기")]
        [SerializeField] private bool continueAvailable;

        private bool isLeaving;
        private bool isCreditsOpen;
        private int escapeBlockedFrame = -1;

        public bool ContinueAvailable => continueAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedVolume()
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(HeatSliderPrefix + MasterVolumeKey, 1f));
        }

        private void Awake()
        {
            if (mainPanels == null) Debug.LogError("[MainMenu] Main Panels(PanelManager)가 연결되지 않았습니다.", this);
            if (credits != null)
            {
                credits.onOpen.AddListener(() => isCreditsOpen = true);
                credits.onClose.AddListener(() =>
                {
                    isCreditsOpen = false;
                    escapeBlockedFrame = Time.frameCount;
                });
            }
            if (quitWindow != null) quitWindow.onClose.AddListener(() => escapeBlockedFrame = Time.frameCount);
        }

        private void Start()
        {
            SetContinueAvailable(continueAvailable);
        }

        public void SetContinueAvailable(bool available)
        {
            continueAvailable = available;
            if (continueButton == null) return;
            continueButton.isInteractable = available;
            continueButton.UpdateUI();
        }

        public void NewGame()
        {
            LoadScene(newGameScene, "새 게임");
        }

        public void Continue()
        {
            if (!continueAvailable)
            {
                Debug.Log("[MainMenu] 이어하기 — 저장된 진행이 없습니다.", this);
                return;
            }
            LoadScene(continueScene, "이어하기");
        }

        public void OpenHome()
        {
            if (mainPanels != null) mainPanels.OpenPanel(HomePanel);
        }

        public void OpenSettings()
        {
            if (mainPanels != null) mainPanels.OpenPanel(SettingsPanel);
        }

        public void OpenCredits()
        {
            if (credits == null)
            {
                Debug.LogWarning("[MainMenu] 크레딧이 연결되지 않았습니다.", this);
                return;
            }
            credits.OpenPanel();
        }

        public void RequestQuit()
        {
            if (isLeaving || isCreditsOpen || escapeBlockedFrame == Time.frameCount) return;
            if (quitWindow == null)
            {
                Quit();
                return;
            }
            quitWindow.OpenWindow();
        }

        public void Quit()
        {
            Debug.Log("[MainMenu] 게임 종료", this);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void SetMasterVolume(float volume)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
        }

        // Heat GraphicsManager.SetVSync는 vSyncCount = 2 (주사율 절반)라서 따로 둠
        public void SetVSync(bool on)
        {
            QualitySettings.vSyncCount = on ? 1 : 0;
        }

        private void LoadScene(string sceneName, string label)
        {
            if (isLeaving) return;
            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[MainMenu] {label} — 씬 '{sceneName}'이 Build Settings에 없어 이동하지 않습니다.", this);
                return;
            }
            isLeaving = true;
            Debug.Log($"[MainMenu] {label} → {sceneName}", this);
            SceneManager.LoadScene(sceneName);
        }
    }
}
