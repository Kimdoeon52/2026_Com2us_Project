using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [전투 상태 HUD (FighterStatusHUD)]
/// CombatDataHub로부터 실시간 코어 HP 및 5개 파츠 내구도 변경 이벤트를 수신하여 UI를 갱신함.
/// 자가 진단(Diagnostic) 및 자동 자식 탐색(Auto-Fallback) 기능이 내장되어 있어 인스펙터 바인딩 실수를 방지함.
/// </summary>
public class FighterStatusHUD : MonoBehaviour
{
    [Header("대상 캐릭터 식별자")]
    [Tooltip("플레이어 HUD는 Player, 적군 HUD는 Enemy로 설정")]
    [SerializeField] private string targetFighterID = "Player";

    [Header("하단 총합체력 UI")]
    [SerializeField] private Slider totalHealthSlider; // 총합체력 슬라이더
    [SerializeField] private TextMeshProUGUI totalHealthText; // 총합체력 텍스트

    [Header("부위별 게이지 위젯 목록(총 6개 : core, head, leftArm, rightArm, leftLeg, rightLeg)")]
    [SerializeField] private List<PartGaugeWidget> partGaugeWidgets = new List<PartGaugeWidget>();

    private Dictionary<BodyPart, PartGaugeWidget> widgetLookup = new Dictionary<BodyPart, PartGaugeWidget>();
    private bool isSubscribed = false;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttachInTestScene()
    {
        var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (currentScene.name == "Battle_Test_KKH")
        {
            if (FindFirstObjectByType<FighterStatusHUD>() == null)
            {
                var hudObj = GameObject.Find("FighterStatusHUD");
                if (hudObj != null)
                {
                    hudObj.AddComponent<FighterStatusHUD>();
                    Debug.Log("<color=cyan><b>[FighterStatusHUD] 씬 내 'FighterStatusHUD' 게임오브젝트를 감지하여 스크립트를 자동 부착(Auto-Attach)했습니다!</b></color>");
                }
                else
                {
                    Debug.LogWarning("[FighterStatusHUD] 씬에서 'FighterStatusHUD' 이름의 오브젝트를 찾을 수 없습니다.");
                }
            }
        }
    }
#endif

    private void Awake()
    {
        InitializeWidgets();
    }

    /// <summary>
    /// 위젯 수집 및 룩업 테이블 빌드 (미할당 시 자식 오브젝트 자동 탐색)
    /// </summary>
    private void InitializeWidgets()
    {
        // 1. 위젯 리스트가 비어있다면 자식 오브젝트에서 자동 수집
        if (partGaugeWidgets == null || partGaugeWidgets.Count == 0)
        {
            var found = GetComponentsInChildren<PartGaugeWidget>(true);
            partGaugeWidgets = new List<PartGaugeWidget>(found);
            Debug.Log($"<color=yellow>[FighterStatusHUD:{targetFighterID}] 인스펙터 위젯 목록이 비어있어 자식 오브젝트에서 {found.Length}개의 위젯을 자동 수집함.</color>");
        }

        // 2. 하단 슬라이더/텍스트가 미할당된 경우 자동 탐색
        if (totalHealthSlider == null)
        {
            totalHealthSlider = GetComponentInChildren<Slider>(true);
            if (totalHealthSlider != null)
                Debug.Log($"<color=yellow>[FighterStatusHUD:{targetFighterID}] totalHealthSlider 자동 연결됨: {totalHealthSlider.gameObject.name}</color>");
        }

        if (totalHealthText == null && totalHealthSlider != null)
        {
            totalHealthText = totalHealthSlider.GetComponentInChildren<TextMeshProUGUI>(true);
            if (totalHealthText != null)
                Debug.Log($"<color=yellow>[FighterStatusHUD:{targetFighterID}] totalHealthText 자동 연결됨: {totalHealthText.gameObject.name}</color>");
        }

        // 3. 부위별 룩업 테이블 구축 및 중복 검증
        widgetLookup.Clear();
        foreach (var widget in partGaugeWidgets)
        {
            if (widget == null) continue;

            if (widgetLookup.ContainsKey(widget.TargetPart))
            {
                Debug.LogWarning($"<color=orange>[FighterStatusHUD:{targetFighterID}] 부위 중복 감지! '{widget.TargetPart}' 부위가 '{widgetLookup[widget.TargetPart].gameObject.name}'에서 '{widget.gameObject.name}'(으)로 덮어씌워집니다. 각 위젯의 TargetPart 설정을 확인하세요.</color>");
            }

            widgetLookup[widget.TargetPart] = widget;
        }

        Debug.Log($"<color=cyan><b>[FighterStatusHUD:{targetFighterID}] 위젯 초기화 완료 (등록된 부위 수: {widgetLookup.Count}/6)</b></color>");
        foreach (var kvp in widgetLookup)
        {
            Debug.Log($"   └ [{kvp.Key}] -> 오브젝트: {kvp.Value.gameObject.name}");
        }
    }

    private void Start()
    {
        // CombatDataHub 싱글톤 이벤트 구독 시도
        TrySubscribe();

        // 씬 로드 타이밍 문제로 아직 허브가 없다면 대기 코루틴 가동
        if (!isSubscribed)
        {
            StartCoroutine(WaitForHubAndSubscribeCoroutine());
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (isSubscribed) return;

        var hub = CombatDataHub.Instance;
        if (hub != null)
        {
            hub.OnHpChanged += HandleHpChanged;
            hub.OnPartDurabilityChanged += HandlePartDurabilityChanged;
            isSubscribed = true;

            Debug.Log($"<color=green><b>[FighterStatusHUD:{targetFighterID}] CombatDataHub 이벤트 구독 성공!</b></color>");

            // 초기 데이터 전체 동기화
            RefreshAllData();
        }
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;

        var hub = CombatDataHub.Instance;
        if (hub != null)
        {
            hub.OnHpChanged -= HandleHpChanged;
            hub.OnPartDurabilityChanged -= HandlePartDurabilityChanged;
        }
        isSubscribed = false;
    }

    /// <summary>
    /// CombatDataHub가 런타임에 지연 생성될 경우를 대비한 자동 재구독 코루틴
    /// </summary>
    private IEnumerator WaitForHubAndSubscribeCoroutine()
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (!isSubscribed && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
            TrySubscribe();
        }

        if (!isSubscribed)
        {
            Debug.LogError($"<color=red>[FighterStatusHUD:{targetFighterID}] {timeout}초 내에 CombatDataHub 인스턴스를 찾을 수 없어 이벤트 구독에 실패했습니다!</color>");
        }
    }

    /// <summary>
    /// 전체 수치 강제 동기화 (씬 시작 시, 또는 전투 재시작 시)
    /// </summary>
    public void RefreshAllData()
    {
        var hub = CombatDataHub.Instance;
        if (hub == null) return;

        // 1. 코어 본체 체력 동기화
        int currentHp = hub.GetCurrentHp(targetFighterID);
        int maxHp = hub.GetMaxHp(targetFighterID);
        UpdateCoreHpDisplay(currentHp, maxHp);

        if (widgetLookup.TryGetValue(BodyPart.Core, out var coreWidget))
        {
            coreWidget.UpdateDurability(currentHp, maxHp);
            coreWidget.UpdateLabelDisplay();
        }

        // 2. 부위별 내구도 동기화
        foreach (var kvp in widgetLookup)
        {
            if (kvp.Key == BodyPart.Core) continue;

            int curDurability = hub.GetPartDurability(targetFighterID, kvp.Key);
            int maxDurability = hub.GetPartMaxDurability(targetFighterID, kvp.Key);
            kvp.Value.UpdateDurability(curDurability, maxDurability);
            kvp.Value.UpdateLabelDisplay();
        }

        Debug.Log($"<color=cyan>[FighterStatusHUD:{targetFighterID}] 전체 데이터 동기화 완료 (Core HP: {currentHp}/{maxHp})</color>");
    }

    private void HandleHpChanged(string fighterId, int currentHp, int maxHp)
    {
        // 대상 캐릭터 검사 (공백 무시 및 대소문자 무시)
        if (!fighterId.Trim().Equals(targetFighterID.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Debug.Log($"<color=lime>[FighterStatusHUD:{targetFighterID}] <b>OnHpChanged 수신</b> -> HP: {currentHp}/{maxHp}</color>");
        UpdateCoreHpDisplay(currentHp, maxHp);

        if (widgetLookup.TryGetValue(BodyPart.Core, out var coreWidget))
        {
            coreWidget.UpdateDurability(currentHp, maxHp);
        }

        if (currentHp <= 0)
        {
            Debug.Log($"<color=red><b>[전투 종료] {targetFighterID} 코어 체력 0 도달 (K.O)</b></color>");
            if (coreWidget != null)
            {
                coreWidget.UpdateDurability(0, maxHp);
            }
        }
    }

    private void HandlePartDurabilityChanged(string fighterId, BodyPart part, int currentHp, int maxHp)
    {
        // 대상 캐릭터 검사
        if (!fighterId.Trim().Equals(targetFighterID.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Debug.Log($"<color=yellow>[FighterStatusHUD:{targetFighterID}] <b>OnPartDurabilityChanged 수신</b> -> {part}: {currentHp}/{maxHp}</color>");

        if (widgetLookup.TryGetValue(part, out var widget))
        {
            widget.UpdateDurability(currentHp, maxHp);
        }
        else
        {
            Debug.LogWarning($"<color=red>[FighterStatusHUD:{targetFighterID}] 수신된 '{part}' 부위에 해당하는 위젯이 widgetLookup에 등록되어 있지 않습니다!</color>");
        }
    }

    private void UpdateCoreHpDisplay(int currentHp, int maxHp)
    {
        if (totalHealthSlider != null)
        {
            totalHealthSlider.maxValue = maxHp;
            totalHealthSlider.value = currentHp;
        }

        if (totalHealthText != null)
        {
            totalHealthText.text = $"<color=#00FF44>{currentHp}</color> <color=#777777>/ {maxHp}</color>";
        }
    }
}

