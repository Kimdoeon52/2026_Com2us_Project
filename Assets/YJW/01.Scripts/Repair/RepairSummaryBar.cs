using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 수리 화면 하단의 선택 합계
public class RepairSummaryBar : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _count;
    [SerializeField] private TextMeshProUGUI _cost;
    [SerializeField] private TextMeshProUGUI _time;
    [SerializeField] private TextMeshProUGUI _balance;
    [SerializeField] private TextMeshProUGUI _finishTime;
    [SerializeField] private Button _startButton;

    public void Refresh()
    {
        FixWindowManager manager = FixWindowManager.Instance;
        int count = manager.CheckedCount;
        int cost = manager.CheckedCost();
        int hours = count * RepairRules.RepairHours;
        int balance = manager.PlayerGold - cost;

        _count.text = $"{count} <size=60%>부위</size>";
        _cost.text = $"{cost:N0} <size=60%>G</size>";
        _time.text = $"{hours} <size=60%>시간</size>";
        _balance.text = $"{balance:N0} <size=60%>G</size>";
        _balance.color = balance < 0 ? RepairColors.Bad : RepairColors.Bone;
        _finishTime.text = count > 0 ? $"완료 예정 {manager.FinishTimeText(hours)}" : "수리할 부위를 선택하세요";

        _startButton.interactable = count > 0 && balance >= 0;
    }
}
