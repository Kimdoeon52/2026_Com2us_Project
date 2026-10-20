using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealSteel.UI
{
    /// <summary>
    /// [전투 결과 UI (BattleResultUI)]
    /// BattleManager.OnBattleFinished 이벤트를 수신하여 하드코어 전투 정산 결과(승패, 골드, 코어 EXP, 파츠 파괴)를
    /// DOTween 시퀀스 연출과 함께 화면에 표시하는 피날레 뷰 컨트롤러임.
    /// </summary>
    public class BattleResultUI : MonoBehaviour
    {
        public static BattleResultUI Instance { get; private set; }

        [Header("=== 루트 패널 및 캔버스 그룹 ===")]
        [SerializeField] private GameObject resultPanelRoot;
        [SerializeField] private CanvasGroup mainCanvasGroup;

        [Header("=== 승패 타이틀 배너 ===")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private Image bannerBackgroundImage;

        [Header("=== 색상 테마 설정 ===")]
        [SerializeField] private Color victoryColor = new Color(0.1f, 0.9f, 0.7f, 1f);     // 네온 시안/골드
        [SerializeField] private Color defeatColor = new Color(1.0f, 0.25f, 0.25f, 1f);    // 크림슨 레드

        [Header("=== 보상 및 코어 성장 섹션 ===")]
        [SerializeField] private TextMeshProUGUI goldValueText;
        [SerializeField] private TextMeshProUGUI expValueText;
        [SerializeField] private Slider coreExpSlider;
        [SerializeField] private TextMeshProUGUI coreStatusText;

        [Header("=== 하드코어 파츠 상태 섹션 ===")]
        [SerializeField] private Transform partSlotContainer;
        [SerializeField] private GameObject partSlotPrefab;
        [SerializeField] private TextMeshProUGUI destroyedWarningText;
        [SerializeField] private GameObject destroyedWarningPanel;

        [Header("=== 인터랙션 버튼 ===")]
        [SerializeField] private Button btnReturnBase;
        [SerializeField] private string lobbySceneName = "Lobby";

        [Header("=== 연출 타이밍 설정 ===")]
        [SerializeField] private float bannerPunchDuration = 0.4f;
        [SerializeField] private float numberRollDuration = 0.8f;

        private BattleSettlementResult _currentSettlement;
        private Sequence _animSequence;

        private void Awake()
        {
            Instance = this;

            // 인스펙터 미할당 시 자동 탐색 보정
            if (resultPanelRoot == null) resultPanelRoot = gameObject;
            if (mainCanvasGroup == null) mainCanvasGroup = GetComponent<CanvasGroup>();

            // 버튼 리스너 바인딩
            if (btnReturnBase != null) btnReturnBase.onClick.AddListener(OnClickReturnToBase);

            // 시작 시 화면에서 숨김 (단, GameObject 본체를 끄면 OnDisable로 이벤트 수신이 끊기므로 CanvasGroup 또는 자식 패널로 숨김)
            HidePanel();
        }

        /// <summary>
        /// 결과 패널 숨기기 (GameObject를 비활성화하지 않고 투명화하여 이벤트 수신 유지)
        /// </summary>
        public void HidePanel()
        {
            if (mainCanvasGroup != null)
            {
                mainCanvasGroup.alpha = 0f;
                mainCanvasGroup.interactable = false;
                mainCanvasGroup.blocksRaycasts = false;
            }
            else if (resultPanelRoot != null && resultPanelRoot != gameObject)
            {
                resultPanelRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            BattleManager.OnBattleFinished += HandleBattleFinished;
            BattleSettlementProcessor.OnSettlementCompleted += HandleSettlementCompleted;
        }

        private void OnDisable()
        {
            BattleManager.OnBattleFinished -= HandleBattleFinished;
            BattleSettlementProcessor.OnSettlementCompleted -= HandleSettlementCompleted;

            if (_animSequence != null && _animSequence.IsActive())
            {
                _animSequence.Kill();
            }
        }

        /// <summary>
        /// BattleManager 이벤트 수신 핸들러
        /// </summary>
        private void HandleBattleFinished(bool playerWon, string winnerId, BattleSettlementResult settlement)
        {
            Debug.Log($"<color=cyan><b>[BattleResultUI] HandleBattleFinished 수신 성공! (승자: {winnerId}, 승리여부: {playerWon})</b></color>");
            ShowResult(settlement, playerWon);
        }

        /// <summary>
        /// 정산기 단독 이벤트 수신 핸들러 (폴백)
        /// </summary>
        private void HandleSettlementCompleted(BattleSettlementResult settlement)
        {
            if (_currentSettlement == null && settlement != null)
            {
                Debug.Log($"<color=cyan>[BattleResultUI] OnSettlementCompleted 수신 성공!</color>");
                ShowResult(settlement, settlement.PlayerWon);
            }
        }

        /// <summary>
        /// 정산 데이터를 받아 결과 UI를 활성화하고 DOTween 시퀀스를 재생함
        /// </summary>
        public void ShowResult(BattleSettlementResult settlement, bool playerWon)
        {
            Debug.Log($"<color=green><b>[BattleResultUI] ShowResult 시작! (PlayerWon: {playerWon}, Root: {resultPanelRoot?.name})</b></color>");

            // 0. GameObject 및 루트 패널 강제 활성화
            gameObject.SetActive(true);
            if (resultPanelRoot != null)
            {
                resultPanelRoot.SetActive(true);
            }

            if (mainCanvasGroup != null)
            {
                mainCanvasGroup.alpha = 1f;
                mainCanvasGroup.interactable = true;
                mainCanvasGroup.blocksRaycasts = true;
            }

            if (settlement == null)
            {
                // 데이터 누락 시 안전한 기본값 생성
                settlement = new BattleSettlementResult
                {
                    PlayerWon = playerWon,
                    GoldDelta = playerWon ? 150 : -100,
                    CoreExpGained = playerWon ? 120 : 20,
                    SummaryMessage = playerWon ? "전투에서 승리했습니다!" : "기체가 대파되어 패배했습니다."
                };
            }

            _currentSettlement = settlement;

            // 1. 헤더 배너 텍스트 및 컬러 적용
            ApplyHeaderVisuals(playerWon);

            // 2. 파츠 내구도 및 영구 파괴 목록 바인딩
            ApplyPartsStatus(settlement);

            // 3. DOTween 단계별 시퀀스 연출 개시
            PlayPresentationSequence(settlement, playerWon);
        }

        /// <summary>
        /// 승패에 따른 헤더 비주얼(타이틀, 서브타이틀, 배너 색상) 구성
        /// </summary>
        private void ApplyHeaderVisuals(bool playerWon)
        {
            Color themeColor = playerWon ? victoryColor : defeatColor;

            if (titleText != null)
            {
                titleText.text = playerWon ? "VICTORY" : "DEFEAT";
                titleText.color = themeColor;
            }

            if (subtitleText != null)
            {
                subtitleText.text = playerWon ? "작전 성공 — 목표 대상을 완파했습니다." : "작전 실패 — 기체 제어 불능 및 대파.";
            }

            if (bannerBackgroundImage != null)
            {
                bannerBackgroundImage.color = new Color(themeColor.r, themeColor.g, themeColor.b, 0.25f);
            }

            if (coreStatusText != null)
            {
                if (_currentSettlement.CoreUnstable)
                {
                    coreStatusText.text = "<color=#FF3333>● 코어 불안정 (Unstable)</color>";
                }
                else
                {
                    coreStatusText.text = "<color=#33FF88>● 코어 정상 가동 (Stable)</color>";
                }
            }
        }

        /// <summary>
        /// 하드코어 파츠 잔여 내구도 및 영구 파괴 경고 목록 바인딩
        /// </summary>
        private void ApplyPartsStatus(BattleSettlementResult settlement)
        {
            // 영구 파괴 부품 경고 처리
            bool hasDestroyedParts = settlement.DestroyedPartIDs != null && settlement.DestroyedPartIDs.Count > 0;
            if (destroyedWarningPanel != null)
            {
                destroyedWarningPanel.SetActive(hasDestroyedParts);
            }

            if (destroyedWarningText != null)
            {
                if (hasDestroyedParts)
                {
                    string destroyedNames = string.Join(", ", settlement.DestroyedPartIDs);
                    destroyedWarningText.text = $"<color=#FF2222><b>[경고] 영구 파손된 부품 소멸:</b> {destroyedNames}</color>";
                }
                else
                {
                    destroyedWarningText.text = "<color=#88FF88>※ 영구 파괴된 부품 없음 (모든 파츠 보존)</color>";
                }
            }

            // 부위별 내구도 슬롯 동적 갱신
            if (partSlotContainer != null && settlement.FinalPartDurabilities != null)
            {
                // 기존 동적 슬롯 정리
                for (int i = partSlotContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(partSlotContainer.GetChild(i).gameObject);
                }

                // 5개 부위 슬롯 생성
                foreach (var kvp in settlement.FinalPartDurabilities)
                {
                    CreatePartSlot(kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// 개별 부위 내구도 슬롯 UI 생성
        /// </summary>
        private void CreatePartSlot(string partKey, int currentDur)
        {
            GameObject slotObj;
            if (partSlotPrefab != null)
            {
                slotObj = Instantiate(partSlotPrefab, partSlotContainer);
            }
            else
            {
                // 프리팹 미할당 시 간단한 텍스트 오브젝트 생성
                slotObj = new GameObject($"Slot_{partKey}");
                slotObj.transform.SetParent(partSlotContainer, false);
                var textComp = slotObj.AddComponent<TextMeshProUGUI>();
                textComp.fontSize = 18;
                textComp.alignment = TextAlignmentOptions.Center;
            }

            var tmp = slotObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                string durColor = currentDur <= 0 ? "#FF4444" : (currentDur <= 20 ? "#FFCC00" : "#FFFFFF");
                tmp.text = $"{partKey}: <color={durColor}>{currentDur}</color>";
            }
        }

        /// <summary>
        /// DOTween 기반 순차 연출 (딤드 -> 배너 펀치 -> 숫자 롤링 -> 버튼 활성화)
        /// </summary>
        private void PlayPresentationSequence(BattleSettlementResult settlement, bool playerWon)
        {
            if (_animSequence != null && _animSequence.IsActive())
            {
                _animSequence.Kill();
            }

            _animSequence = DOTween.Sequence();

            // 1. 캔버스 그룹 페이드인
            if (mainCanvasGroup != null)
            {
                mainCanvasGroup.alpha = 0f;
                _animSequence.Append(mainCanvasGroup.DOFade(1f, 0.3f));
            }

            // 2. 타이틀 배너 펀치 스케일
            if (titleText != null)
            {
                titleText.transform.localScale = Vector3.one * 0.5f;
                _animSequence.Append(titleText.transform.DOScale(1f, bannerPunchDuration).SetEase(Ease.OutBack));
                _animSequence.Join(titleText.transform.DOPunchScale(Vector3.one * 0.25f, 0.3f));
            }

            // 3. 골드 수치 롤링 카운팅
            int targetGold = settlement.GoldDelta;
            if (goldValueText != null)
            {
                goldValueText.text = "0 G";
                _animSequence.Append(DOVirtual.Int(0, targetGold, numberRollDuration, val =>
                {
                    string sign = val > 0 ? "+" : "";
                    goldValueText.text = $"{sign}{val} G";
                }).SetEase(Ease.OutQuad));
            }

            // 4. 코어 경험치 롤링 및 슬라이더 증가
            int targetExp = settlement.CoreExpGained;
            if (expValueText != null)
            {
                expValueText.text = "+0 EXP";
                _animSequence.Join(DOVirtual.Int(0, targetExp, numberRollDuration, val =>
                {
                    expValueText.text = $"+{val} EXP";
                }).SetEase(Ease.OutQuad));
            }

            if (coreExpSlider != null)
            {
                coreExpSlider.value = 0f;
                _animSequence.Join(coreExpSlider.DOValue(0.75f, numberRollDuration).SetEase(Ease.OutQuad));
            }

            // 5. 버튼 인터랙션 활성화
            if (btnReturnBase != null) btnReturnBase.interactable = true;
        }

        /// <summary>
        /// [거점 복귀] 버튼 클릭 처리
        /// </summary>
        public void OnClickReturnToBase()
        {
            Debug.Log($"<color=cyan>[BattleResultUI] 거점 복귀 클릭: '{lobbySceneName}' 씬으로 전환합니다.</color>");
            if (Application.CanStreamedLevelBeLoaded(lobbySceneName))
            {
                SceneManager.LoadScene(lobbySceneName);
            }
            else
            {
                Debug.LogWarning($"[BattleResultUI] '{lobbySceneName}' 씬이 Build Settings에 등록되지 않아 현재 씬을 유지합니다.");
                if (resultPanelRoot != null) resultPanelRoot.SetActive(false);
            }
        }

        // ========================================================================
        // 프리팹 미연결 상태에서도 게임 뷰에서 즉시 검증할 수 있는 OnGUI 폴백 렌더러
        // ========================================================================
        private void OnGUI()
        {
            // 인스펙터 바인딩 없이 단독 실행 중이거나 패널이 활성화되었을 때 간단한 OnGUI 팝업 제공
            if (_currentSettlement == null || (resultPanelRoot != null && !resultPanelRoot.activeSelf))
            {
                return;
            }

            // Canvas UI가 연결되어 있지 않은 경우에만 비상 OnGUI 렌더링
            if (titleText == null)
            {
                RenderFallbackOnGUI();
            }
        }

        private void RenderFallbackOnGUI()
        {
            float width = 420;
            float height = 340;
            float x = (Screen.width - width) * 0.5f;
            float y = (Screen.height - height) * 0.5f;

            GUI.Box(new Rect(x, y, width, height), "");
            GUILayout.BeginArea(new Rect(x + 15, y + 15, width - 30, height - 30));

            string title = _currentSettlement.PlayerWon ? "<color=#22FF88>★ VICTORY (승리) ★</color>" : "<color=#FF4444>✖ DEFEAT (패배) ✖</color>";
            GUILayout.Label($"<size=22><b>{title}</b></size>");
            GUILayout.Space(8);

            GUILayout.Label($"<b>획득 판돈:</b> {_currentSettlement.GoldDelta} G");
            GUILayout.Label($"<b>코어 경험치:</b> +{_currentSettlement.CoreExpGained} EXP");
            GUILayout.Label($"<b>코어 상태:</b> {(_currentSettlement.CoreUnstable ? "<color=red>불안정 (Unstable)</color>" : "<color=green>정상 (Stable)</color>")}");
            GUILayout.Space(8);

            if (_currentSettlement.DestroyedPartIDs != null && _currentSettlement.DestroyedPartIDs.Count > 0)
            {
                GUILayout.Label($"<color=red><b>[영구 파괴된 부품]:</b> {string.Join(", ", _currentSettlement.DestroyedPartIDs)}</color>");
            }
            else
            {
                GUILayout.Label("<color=green>※ 영구 파괴된 부품 없음</color>");
            }

            GUILayout.Space(12);
            GUILayout.Label($"<size=11><i>{_currentSettlement.SummaryMessage}</i></size>");
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("기지로 귀환", GUILayout.Height(40)))
            {
                OnClickReturnToBase();
            }

            GUILayout.EndArea();
        }
    }
}

