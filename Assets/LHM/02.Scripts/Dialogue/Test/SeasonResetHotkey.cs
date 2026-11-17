using RealSteel.BossRace;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    // ======================== [TEST ONLY] 테스트 전용 ========================
    // 용도: Ctrl+F11 시즌 초기화 · Ctrl+PgDn 다음 보스
    // 위치: ⚠ DialogueSystem.prefab에 붙어 있어 빌드에 포함됨 (출시 전 제거 · 비활성화 필요)
    // 실제 게임 로직이 아님. 출시 빌드 씬/프리팹에 넣지 말 것
    // ==========================================================================
    // 테스트용: Ctrl + 키 = 시즌 기록 초기화 (프리시즌으로 되돌림). 시작일이 지났으면 길드 전화가 바로 다시 옴
    public class SeasonResetHotkey : MonoBehaviour
    {
        private static readonly string[] SeasonVariables = { "season.started", "season.day", "season.endingSoon", "race.id" };
        private static readonly string[] SeasonDialogues = { "system_season_ending", "season_call_question", "season_call_confirm", "season_call_accept", "mono_receptionist_season_end" };

        // HuntDeparture · SeasonRaceCall과 같은 지역 순서 (race.id)
        private static readonly string[] RaceIds = { "race1", "race2", "race3", "final" };

        [SerializeField] private Key resetKey = Key.F11;
        [Tooltip("Ctrl + 이 키 = 지금 지역을 시즌 1위로 처리하고 다음 지역 보스 시즌으로 넘어감")]
        [SerializeField] private Key nextBossKey = Key.PageDown;

        public Key ResetKey => resetKey;
        public Key NextBossKey => nextBossKey;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.ctrlKey.isPressed) return;

            if (resetKey != Key.None && keyboard[resetKey].wasPressedThisFrame) ResetSeason();
            else if (nextBossKey != Key.None && keyboard[nextBossKey].wasPressedThisFrame) NextBoss();
        }

        public void NextBoss()
        {
            DialogueManager manager = FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            if (manager == null || !manager.IsReady || manager.IsFallback || manager.Variables == null)
            {
                Debug.LogWarning("[NextBoss] 대사 데이터가 준비되지 않아 다음 보스로 넘기지 못했습니다.", this);
                return;
            }

            SeasonRaceCall call = FindFirstObjectByType<SeasonRaceCall>(FindObjectsInactive.Include);
            if (call != null && call.IsBusy)
            {
                Debug.LogWarning("[NextBoss] 시즌 전화가 진행 중이라 넘기지 않습니다. 전화를 끝낸 뒤 다시 누르세요.", this);
                return;
            }

            DialogueVariables variables = manager.Variables;
            int current = Mathf.Max(0, System.Array.IndexOf(RaceIds, variables.Get("race.id")));
            int next = current + 1;
            if (next >= RaceIds.Length)
            {
                Debug.Log($"[NextBoss] 이미 마지막 지역({RaceIds[current]})입니다. 처음부터 하려면 Ctrl + {resetKey}(시즌 초기화).", this);
                return;
            }

            // 지역 카드 잠금 해제: 지금까지 지역을 시즌 1위로 (보스 목록 순서 = race.id 순서)
            BossRaceSelectUI bossList = FindFirstObjectByType<BossRaceSelectUI>(FindObjectsInactive.Include);
            BossProgressStore store = FindFirstObjectByType<BossProgressStore>(FindObjectsInactive.Include);
            string nextBossName = RaceIds[next];
            if (bossList != null && bossList.Entries != null)
            {
                for (int i = 0; i <= current && i < bossList.Entries.Count; i++)
                {
                    BossRaceEntry entry = bossList.Entries[i];
                    if (entry != null && store != null && !store.IsRegionCleared(entry.bossId)) store.ToggleRegionCleared(entry.bossId);
                }
                if (next < bossList.Entries.Count && bossList.Entries[next] != null)
                {
                    BossRaceEntry entry = bossList.Entries[next];
                    nextBossName = $"{entry.RegionText} {entry.bossName} ({entry.bossId})";
                }
            }

            // SeasonRaceCall.StartSeason과 같은 값으로 새 시즌 시작
            variables.Set("race.id", RaceIds[next]);
            variables.Set("race.stage", 0);
            variables.Set("race.seasonFirst", "no");
            variables.Set("season.day", 1);
            variables.Set("season.endingSoon", "no");
            variables.Set("season.started", "yes");
            manager.LogState($"다음 보스 테스트 ({RaceIds[current]} → {RaceIds[next]})");
            manager.Save();

            Debug.Log($"[NextBoss] {RaceIds[current]} 시즌 1위 처리 → 다음 시즌 보스: {nextBossName}. 접수원 토벌 신청에서 확인하세요.", this);
        }

        public void ResetSeason()
        {
            DialogueManager manager = FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            if (manager == null || !manager.IsReady || manager.IsFallback || manager.Variables == null)
            {
                Debug.LogWarning("[SeasonReset] 대사 데이터가 준비되지 않아 시즌을 초기화하지 못했습니다.", this);
                return;
            }

            SeasonRaceCall call = FindFirstObjectByType<SeasonRaceCall>(FindObjectsInactive.Include);
            if (call != null && call.IsBusy)
            {
                Debug.LogWarning("[SeasonReset] 시즌 전화가 진행 중이라 초기화하지 않습니다. 전화를 끝낸 뒤 다시 누르세요.", this);
                return;
            }

            foreach (string name in SeasonVariables) manager.Variables.ResetToDefault(name);
            int forgotten = manager.ForgetDialogues(SeasonDialogues);
            manager.LogState($"시즌 초기화 (시즌 대사 기록 {forgotten}개 지움)");
            manager.Save();

            DialogueDayProcessor processor = FindFirstObjectByType<DialogueDayProcessor>(FindObjectsInactive.Include);
            bool rang = processor != null && processor.RecheckFirstSeason();
            Debug.Log(rang
                ? "[SeasonReset] 시즌 초기화 완료 — 시작일이 지나 길드 전화가 다시 울립니다."
                : "[SeasonReset] 시즌 초기화 완료 — 프리시즌 상태. 시즌 시작일(달력) 00시에 길드 전화가 옵니다.", this);
        }
    }
}
