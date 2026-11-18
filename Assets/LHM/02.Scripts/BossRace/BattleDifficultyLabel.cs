using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.BossRace
{
    // 전투 씬 난이도 표시 (BUG-027)
    // LJS BattleUiCanvas 프리팹의 'Difficulty/Text (TMP)'가 "Normal" 고정 글자라
    // 전투 씬이 열리면 KKH BattleManager의 실제 난이도(토벌 신청에서 고른 값)로 바꿔 줌. LJS · KKH 파일은 수정하지 않음
    public static class BattleDifficultyLabel
    {
        private const string LabelParentName = "Difficulty";
        private static readonly string[] Texts = { "Very Easy", "Easy", "Normal", "Hard", "Hell" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Refresh();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Refresh();
        }

        public static void Refresh()
        {
            BattleManager battle = Object.FindAnyObjectByType<BattleManager>(FindObjectsInactive.Include);
            if (battle == null) return;

            BossDifficulty difficulty = BattleManager.HasPreBattleDifficulty ? BattleManager.PreBattleDifficulty : battle.SelectedDifficulty;
            int index = (int)difficulty;
            string text = index >= 0 && index < Texts.Length ? Texts[index] : difficulty.ToString();

            int changed = 0;
            foreach (TMP_Text label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform parent = label.transform.parent;
                if (parent == null || parent.name != LabelParentName) continue;

                label.text = text;
                changed++;
            }

            if (changed > 0) Debug.Log($"[BattleDifficulty] 전투 난이도 표시 → {text}");
        }
    }
}
