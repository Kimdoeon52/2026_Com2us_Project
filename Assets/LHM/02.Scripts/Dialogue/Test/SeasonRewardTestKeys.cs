using RealSteel.Reward;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public class SeasonRewardTestKeys : MonoBehaviour
    {
        [SerializeField] private string playerName = "플레이어";
        [SerializeField] private bool showHelp = true;

        private string lastResult = "";

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.ctrlKey.isPressed) return;

            if (keyboard.jKey.wasPressedThisFrame) RunSeasonEnd(!keyboard.shiftKey.isPressed);
            else if (keyboard.gKey.wasPressedThisFrame) Report();
        }

        private void RunSeasonEnd(bool top)
        {
            RankingSystemManager ranking = RankingSystemManager.Instance != null
                ? RankingSystemManager.Instance
                : FindAnyObjectByType<RankingSystemManager>(FindObjectsInactive.Include);
            if (ranking == null || ranking.seasonResult == null)
            {
                Show("LJS RankingSystemManager 또는 seasonResult가 없습니다 (CoreSystemRoot 확인)");
                return;
            }

            ranking.UpdatePlayerRecord(playerName, 0, top ? 999999 : -1);
            int before = SeasonRewardStore.Pending.Count;
            ranking.seasonResult.ProcessSeasonReward(ranking.npcList);
            int added = SeasonRewardStore.Pending.Count - before;
            Show(added > 0 ? $"LJS 분기 보상 처리: 1위 → 받을 포상 +{added}건 (대기 {SeasonRewardStore.Pending.Count}건, {SeasonRewardStore.PendingGold:N0}골드). 접수원 → 포상 수령"
                : top ? "LJS 분기 보상 처리: 이번 시즌 포상이 이미 기록되어 있어 추가 안 됨 (같은 시즌 중복 방지). 먼저 수령하거나 Ctrl+F12로 테스트"
                : "LJS 분기 보상 처리: 10위 밖 → 포상 없음 (접수원 → 포상 수령 = '아직 받을 포상이 없습니다')");
        }

        private void Report()
        {
            string gold = GlobalGoldWallet.IsAvailable ? $"{GlobalGold.Instance.PlayerGold:N0}" : "(GlobalGold 없음)";
            Show($"플레이어 골드 {gold} / 받을 포상 {SeasonRewardStore.Pending.Count}건 {SeasonRewardStore.PendingGold:N0}골드");
        }

        private void Show(string message)
        {
            lastResult = message;
            Debug.Log($"[SeasonRewardTest] {message}", this);
        }

        private void OnGUI()
        {
            if (!showHelp) return;
            GUI.Box(new Rect(10f, 10f, 620f, 92f),
                "시즌 보상 테스트\n" +
                "Ctrl+J = LJS 분기 보상 처리 (플레이어 1위)    Ctrl+Shift+J = 같은 처리 (플레이어 10위 밖)\n" +
                "Ctrl+F12 / Ctrl+Shift+F12 = 받을 포상 직접 추가 (1위 / 3위)    Ctrl+G = 골드 · 대기 포상 보기\n" +
                lastResult);
        }
    }
}
