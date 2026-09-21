using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private void Awake()
    {
        widgetLookup.Clear();
        foreach (var widget in partGaugeWidgets)
        {
            if (widget != null)
            {
                widgetLookup[widget.TargetPart] = widget;
            }
        }
    }

    private void Start()
    {
        // CombatDataHub 싱글톤 이벤트 구독
        if (CombatDataHub.Instance != null)
        {
            CombatDataHub.Instance.OnHpChanged += HandleHpChanged;
            CombatDataHub.Instance.OnPartDurabilityChanged += HandlePartDurabilityChanged;

            // 씬 시작 시 초기 데이터 전체 동기화
            RefreshAllData();
        }
    }

    private void OnDestroy()
    {
        // CombatDataHub 이벤트 구독 해제
        if (CombatDataHub.Instance != null)
        {
            CombatDataHub.Instance.OnHpChanged -= HandleHpChanged;
            CombatDataHub.Instance.OnPartDurabilityChanged -= HandlePartDurabilityChanged;
        }
    }

    /// <summary>
    /// 전체 수치 강제 동기화 (씬 시작 시, 또는 전투 재시작 시)
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    private void RefreshAllData()
    {
        var hub = CombatDataHub.Instance;
        if (hub == null)
        {
            Debug.LogWarning("CombatDataHub 인스턴스가 존재하지 않습니다.");
            return;
        }
        // 1. 코어 본체 체력 동기화
        int currentHp = hub.GetCurrentHp(targetFighterID);
        int maxHp = hub.GetMaxHp(targetFighterID);
        UpdateCoreHpDisplay(currentHp, maxHp);

        if(widgetLookup.TryGetValue(BodyPart.Core, out var coreWidget))
        {
            coreWidget.UpdateDurability(currentHp, maxHp);
        }
        // 2. 부위별 내구도 동기화
        foreach (var kvp in widgetLookup)
        {
            if (kvp.Key == BodyPart.Core) continue; // 코어는 이미 처리했으므로 건너뜀
            {
                int curDurability = hub.GetPartDurability(targetFighterID, kvp.Key);
                int maxDurability = hub.GetPartMaxDurability(targetFighterID, kvp.Key);
                kvp.Value.UpdateDurability(curDurability, maxDurability);
            }
        }
    }
    private void HandleHpChanged(string fighterId, int currentHp, int maxHp)
    {
        if (!fighterId.Equals(targetFighterID, StringComparison.OrdinalIgnoreCase))
        {
            return; // 대상이 아닌 경우 무시
        }
        UpdateCoreHpDisplay(currentHp, maxHp);

        if (widgetLookup.TryGetValue(BodyPart.Core, out var coreWidget)) // 코어 게이지 갱신
        {
            coreWidget.UpdateDurability(currentHp, maxHp);
        }

        if(currentHp <= 0)
        {
            Debug.Log($"<color=red>[전투 종료]{targetFighterID} 코어 체력 0 이하 K.O</color>");
            if (coreWidget != null)
            {
                coreWidget.UpdateDurability(0, maxHp);
            }
        }

    }

    private void HandlePartDurabilityChanged(string fighterId, BodyPart part, int currentHp, int maxHp)
    {
        if (!fighterId.Equals(targetFighterID, StringComparison.OrdinalIgnoreCase))
        {
            return; // 대상이 아닌 경우 무시
        }

        if (widgetLookup.TryGetValue(part, out var widget))
        {
            widget.UpdateDurability(currentHp, maxHp);
        }
    }

    private void UpdateCoreHpDisplay(int currentHp, int maxHp)
    {
          // 1. 슬라이더가 있다면 슬라이더도 함께 갱신
        if (totalHealthSlider != null)
        {
            totalHealthSlider.maxValue = maxHp;
            totalHealthSlider.value = currentHp;
        }
        // 2. 텍스트 갱신 
        if (totalHealthText != null)
        {
            totalHealthText.text = $"<color=#00FF44>{currentHp}</color> <color=#777777>/ {maxHp}</color>";
        }
    }

}
