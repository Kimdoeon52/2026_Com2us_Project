using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RealSteel.Dialogue.Unity
{
    /// <summary>
    /// Blocking 대화창 UI. 규칙은 DialogueSession(Core)에 있고, 여기는 입력 → 세션, 세션 → 화면 연결만 한다.
    /// 모든 시간은 unscaled (timeScale=0이어도 동작).
    ///
    /// 편의 기능
    ///  - Confirm      : 타이핑 중이면 완성, 완성이면 다음
    ///  - FastForward  : 홀드. 읽은 대사만 고속 진행 (옵션 allowSkipUnread)
    ///  - Auto         : 토글. 대화 간 유지
    ///  - Skip         : 읽은 대사뿐이면 즉시, 안 읽은 대사가 있으면 확인 팝업
    ///  - Backlog      : 열면 진행 정지, 닫으면 재개
    /// </summary>
    public sealed class DialogueBoxPresenter : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private GameObject nextIndicator;
        [SerializeField] private GameObject autoIndicator;
        [SerializeField] private GameObject fastForwardIndicator;
        [SerializeField] private GameObject skipConfirmPanel;
        [SerializeField] private BacklogView backlogView;
        [SerializeField] private PortraitLibrary portraits;

        [Header("Input (Dialogue Action Map)")]
        [SerializeField] private InputActionReference confirm;
        [SerializeField] private InputActionReference fastForward;
        [SerializeField] private InputActionReference autoToggle;
        [SerializeField] private InputActionReference skip;
        [SerializeField] private InputActionReference backlog;
        [SerializeField] private InputActionReference cancel;

        /// <summary>음성 재생 연결 지점 (line.voice). 사운드 시스템이 구독</summary>
        public event Action<DialogueLine> VoiceRequested;

        private DialogueSession _session;
        private bool _autoMode; // 대화 간 유지

        private void Awake()
        {
            root.SetActive(false);
            if (skipConfirmPanel != null) skipConfirmPanel.SetActive(false);
        }

        public void Show(DialogueSession session)
        {
            _session = session;
            _session.AutoMode = _autoMode;
            _session.LineStarted += OnLineStarted;
            _session.LineCompleted += OnLineCompleted;
            root.SetActive(true);
            if (skipConfirmPanel != null) skipConfirmPanel.SetActive(false);
            if (backlogView != null) backlogView.Close();
        }

        public void Hide()
        {
            if (_session != null)
            {
                _session.LineStarted -= OnLineStarted;
                _session.LineCompleted -= OnLineCompleted;
                _session = null;
            }
            if (skipConfirmPanel != null) skipConfirmPanel.SetActive(false);
            if (backlogView != null) backlogView.Close();
            root.SetActive(false);
        }

        private void Update()
        {
            if (_session == null) return;
            float dt = Time.unscaledDeltaTime;

            // 1) 백로그 열림: 진행 정지
            if (backlogView != null && backlogView.IsOpen)
            {
                if (Pressed(backlog) || Pressed(cancel) || Pressed(skip)) backlogView.Close();
                return;
            }

            // 2) 스킵 확인 팝업: 확인/취소만 받음
            if (skipConfirmPanel != null && skipConfirmPanel.activeSelf)
            {
                if (Pressed(confirm)) { skipConfirmPanel.SetActive(false); _session.ForceSkipAll(); }
                else if (Pressed(cancel) || Pressed(skip)) skipConfirmPanel.SetActive(false);
                return;
            }

            // 3) 일반 진행
            if (Pressed(backlog) && backlogView != null) { backlogView.Open(DialogueService.Instance.Engine.Backlog); return; }

            if (Pressed(skip))
            {
                if (!_session.TrySkipAll() && skipConfirmPanel != null) skipConfirmPanel.SetActive(true);
                if (_session == null) return; // 스킵으로 종료됨 (Hide 호출됨)
            }

            if (Pressed(autoToggle)) { _autoMode = !_autoMode; _session.AutoMode = _autoMode; }
            _session.FastForwardHeld = fastForward != null && fastForward.action.IsPressed();

            if (Pressed(confirm)) _session.Confirm();
            if (_session == null) return;

            _session.Tick(dt);
            if (_session == null) return;

            bodyText.maxVisibleCharacters = _session.VisibleChars;
            if (nextIndicator != null) nextIndicator.SetActive(_session.State == SessionState.WaitingForInput);
            if (autoIndicator != null) autoIndicator.SetActive(_autoMode);
            if (fastForwardIndicator != null) fastForwardIndicator.SetActive(_session.FastForwardHeld && _session.IsCurrentLineSeen);
        }

        private static bool Pressed(InputActionReference r) => r != null && r.action.WasPressedThisFrame();

        private void OnLineStarted(DialogueSession s)
        {
            var line = s.CurrentLine;
            speakerLabel.text = s.CurrentSpeakerName;
            speakerLabel.gameObject.SetActive(!string.IsNullOrEmpty(s.CurrentSpeakerName));

            // TMP: 리치 텍스트 태그는 maxVisibleCharacters에 포함되지 않음 → Core의 VisibleLength와 일치
            bodyText.text = s.CurrentText;
            bodyText.maxVisibleCharacters = 0;

            var sprite = portraits != null ? portraits.Find(line.speaker, line.portrait) : null;
            if (portraitImage != null)
            {
                portraitImage.sprite = sprite;
                portraitImage.enabled = sprite != null;
            }

            if (!string.IsNullOrEmpty(line.voice)) VoiceRequested?.Invoke(line);
        }

        private void OnLineCompleted(DialogueSession s)
        {
            bodyText.maxVisibleCharacters = int.MaxValue;
        }
    }
}
