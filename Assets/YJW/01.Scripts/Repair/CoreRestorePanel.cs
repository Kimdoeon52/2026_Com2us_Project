using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 코어 복구 패널. 진단 그림의 코어 표시도 같이 갱신한다
public class CoreRestorePanel : MonoBehaviour
{
    [SerializeField] private GameObject _warningBanner;
    [SerializeField] private RepairBadge _badge;
    [SerializeField] private TextMeshProUGUI _level;
    [SerializeField] private TextMeshProUGUI _guide;
    [SerializeField] private Image _icon;
    [SerializeField] private Image _iconInner;

    [Header("복구비")]
    [SerializeField] private TextMeshProUGUI _levelCostLabel;
    [SerializeField] private TextMeshProUGUI _levelCost;
    [SerializeField] private TextMeshProUGUI _totalCost;
    [SerializeField] private TextMeshProUGUI _time;
    [SerializeField] private TextMeshProUGUI _balance;
    [SerializeField] private Button _startButton;
    [SerializeField] private TextMeshProUGUI _startLabel;

    [Header("진단 그림의 코어")]
    [SerializeField] private TextMeshProUGUI _slotLevel;
    [SerializeField] private TextMeshProUGUI _slotState;
    [SerializeField] private Image _slotIcon;

    public void Refresh()
    {
        InventoryHost host = InventoryHost.Instance;
        FixWindowManager manager = FixWindowManager.Instance;
        bool unstable = host.Save.Core.IsUnstable;
        int level = host.CoreLevel;
        int cost = RepairRules.CoreRestoreCost(level);
        int balance = manager.PlayerGold - cost;
        Color stateColor = unstable ? RepairColors.Bad : RepairColors.Good;

        _slotLevel.text = $"CORE Lv.{level}";
        _slotState.text = unstable ? "불안정" : "정상";
        _slotState.color = stateColor;
        _slotIcon.color = unstable ? RepairColors.Bad : RepairColors.Cyan;

        _warningBanner.SetActive(unstable);
        _badge.SetCore(unstable);
        _level.text = $"CORE Lv.{level}";
        // 골드가 모자라도 막히지 않게 무료로 골드를 모으는 길을 같이 알려준다
        if (!unstable) _guide.text = "코어가 정상입니다.";
        else if (balance < 0) _guide.text = "복구 전에는 출전할 수 없습니다.\n골드가 부족하면 고물상에서 부품을 모아 파츠를 제작하고 상점에 판매하세요.";
        else _guide.text = "복구 전에는 출전할 수 없습니다.";
        _icon.color = stateColor;
        _iconInner.color = stateColor;

        _levelCostLabel.text = $"+ 코어 레벨 {level} × 50G";
        _levelCost.text = $"{level * 50}G";
        _totalCost.text = $"{cost}G";
        _time.text = unstable ? $"{RepairRules.RestoreHours}시간 · 완료 {manager.FinishTimeText(RepairRules.RestoreHours)}" : "-";
        _balance.text = unstable ? $"{balance:N0}G" : "-";
        _balance.color = unstable && balance < 0 ? RepairColors.Bad : RepairColors.Bone;

        _startButton.interactable = unstable && balance >= 0;
        if (!unstable) _startLabel.text = "복구 불필요";
        else _startLabel.text = balance < 0 ? "골드 부족" : "코어 복구 시작";
    }
}
