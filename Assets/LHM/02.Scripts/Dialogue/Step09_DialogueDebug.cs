using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public class Step09_DialogueDebug : MonoBehaviour, RealSteel.BossRace.IBossRaceProgress, RealSteel.BossRace.IRobotPartStatus
    {
        private static readonly string[] Difficulties = { "veryEasy", "easy", "normal", "hard", "hell" };
        private static readonly string[] RankBands = { "101+", "31-100", "11-30", "2-10", "1" };

        [Header("연결")]
        [SerializeField] private DialogueManager manager;

        [Header("F4로 말 걸 대사 풀")]
        [SerializeField] private string talkPoolId = "auction_npc";

        [Header("디버그 토벌 보스 (G로 전환)")]
        [SerializeField] private string[] regionBosses = { "boss.wheel", "boss.kyodai" };

        [Header("보스 레이스 선택 UI 테스트")]
        [SerializeField, Min(1)] private int seasonLength = 90;

        [Header("난이도 선택 UI 테스트 — 파츠 내구도 0~1 (머리·왼팔·오른팔·왼다리·오른다리)")]
        [SerializeField] private float[] partDurability = { 0.82f, 0.64f, 0.30f, 0.91f, 0f };

        private readonly Dictionary<string, int> attempts = new Dictionary<string, int>();
        private readonly Dictionary<string, int> loses = new Dictionary<string, int>();
        private readonly Dictionary<string, int> bestCleared = new Dictionary<string, int>();
        private readonly HashSet<string> clearedRegions = new HashSet<string>();
        private int difficultyIndex = 2;
        private int bossIndex;

        public string SelectedDifficulty => Difficulties[difficultyIndex];
        public string TargetBoss => regionBosses != null && regionBosses.Length > 0 ? regionBosses[bossIndex % regionBosses.Length] : "boss.wheel";

        public int AttemptsOf(string boss) => attempts.TryGetValue(boss, out int count) ? count : 0;
        public int LosesOf(string boss) => loses.TryGetValue(boss, out int count) ? count : 0;
        public int BestDifficulty(string boss) => bestCleared.TryGetValue(boss, out int index) ? index : -1;
        public bool IsRegionCleared(string boss) => clearedRegions.Contains(boss);
        public int SeasonDaysLeft => Mathf.Max(0, seasonLength - manager.Variables.GetInt("season.day"));
        public string RankText => $"{manager.Variables.Get("rank.band")}위";
        public string ScoreText => "-";

        public bool TryGetDurability(BodyPart part, out float ratio)
        {
            int index = (int)part;
            ratio = partDurability != null && index < partDurability.Length ? partDurability[index] : 0f;
            return partDurability != null && index < partDurability.Length;
        }

        public void SetDifficulty(string difficulty)
        {
            int index = Array.IndexOf(Difficulties, difficulty);
            if (index < 0)
            {
                Debug.LogWarning($"[Step09] 알 수 없는 난이도 '{difficulty}'", this);
                return;
            }
            SelectDifficulty(index);
        }

        public void SetTargetBoss(string boss)
        {
            int index = regionBosses != null ? Array.IndexOf(regionBosses, boss) : -1;
            if (index < 0)
            {
                Debug.LogWarning($"[Step09] 디버그 보스 목록에 없는 보스 '{boss}' — Region Bosses에 추가하세요. 대상 보스를 바꾸지 않습니다.", this);
                return;
            }

            bossIndex = index;
            Debug.Log($"[Step09] 대상 보스: {TargetBoss} (도전 {AttemptsOf(TargetBoss)}회, 패배 {LosesOf(TargetBoss)}회)");
        }

        private void ToggleRegionCleared()
        {
            string boss = TargetBoss;
            if (!clearedRegions.Remove(boss)) clearedRegions.Add(boss);
            Debug.Log($"[Step09] {boss} 지역 레이스 시즌 1위: {(clearedRegions.Contains(boss) ? "달성 → 다음 지역 개방" : "취소")}");
        }

        public void AddAttempt(string boss)
        {
            attempts[boss] = AttemptsOf(boss) + 1;
        }

        private void Start()
        {
            if (manager == null)
            {
                Debug.LogError("[Step09] Manager가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Step09] 1~5 난이도 / G 대상 보스 전환 / Shift+G 대상 보스 지역 시즌 1위(다음 지역 개방) 전환 / F1 패배 / F2 승리 / F3 경매 정산(판매) / F4 말 걸기 / F5 기록 / F6 저장 / F7 다시 읽기 / F8 세이브 폴더 / F9 순위 올리기 (Shift: 내리기) / F10 풀스크린 / F11 인터미션 / F12 초기화 후 저장 / Shift+F12 세이브 삭제");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (!manager.IsReady || manager.IsTalking) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool shift = keyboard.shiftKey.isPressed;

            if (keyboard.digit1Key.wasPressedThisFrame) SelectDifficulty(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) SelectDifficulty(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) SelectDifficulty(2);
            else if (keyboard.digit4Key.wasPressedThisFrame) SelectDifficulty(3);
            else if (keyboard.digit5Key.wasPressedThisFrame) SelectDifficulty(4);
            else if (keyboard.gKey.wasPressedThisFrame && shift) ToggleRegionCleared();
            else if (keyboard.gKey.wasPressedThisFrame) NextBoss();
            else if (keyboard.f1Key.wasPressedThisFrame) RecordHunt("lose", TargetBoss);
            else if (keyboard.f2Key.wasPressedThisFrame) RecordHunt("win", TargetBoss);
            else if (keyboard.f3Key.wasPressedThisFrame) RecordAuction("sold");
            else if (keyboard.f4Key.wasPressedThisFrame) manager.Talk(talkPoolId);
            else if (keyboard.f5Key.wasPressedThisFrame) Debug.Log("[Step09] 본 대사 기록\n" + manager.History.Dump());
            else if (keyboard.f6Key.wasPressedThisFrame) manager.Save();
            else if (keyboard.f7Key.wasPressedThisFrame) manager.LoadFromFile();
            else if (keyboard.f8Key.wasPressedThisFrame) OpenSaveFolder();
            else if (keyboard.f9Key.wasPressedThisFrame) ShiftRank(shift ? -1 : 1);
            else if (keyboard.f10Key.wasPressedThisFrame) manager.OpenDirect("test_prologue");
            else if (keyboard.f11Key.wasPressedThisFrame) manager.OpenDirect("test_intermission");
            else if (keyboard.f12Key.wasPressedThisFrame) manager.ResetAll(shift);
        }
#endif

        private void SelectDifficulty(int index)
        {
            difficultyIndex = index;
            Debug.Log($"[Step09] 다음 토벌 난이도: {Difficulties[difficultyIndex]}");
        }

        private void NextBoss()
        {
            bossIndex = regionBosses == null || regionBosses.Length == 0 ? 0 : (bossIndex + 1) % regionBosses.Length;
            Debug.Log($"[Step09] 대상 보스: {TargetBoss} (도전 {AttemptsOf(TargetBoss)}회, 패배 {LosesOf(TargetBoss)}회)");
        }

        public void RecordHunt(string result, string boss)
        {
            DialogueVariables variables = manager.Variables;
            string difficulty = Difficulties[difficultyIndex];

            variables.Set("hunt.lastResult", result);
            variables.Set("hunt.lastBoss", boss);
            variables.Set("hunt.lastDifficulty", difficulty);
            variables.Add("hunt.count", 1);
            variables.Add(result == "win" ? "hunt.winCount" : "hunt.loseCount", 1);
            if (result == "lose") loses[boss] = LosesOf(boss) + 1;
            else if (difficultyIndex > BestDifficulty(boss)) bestCleared[boss] = difficultyIndex;

            manager.LogState($"{(result == "win" ? "승리" : "패배")} ({boss}, {difficulty}) — 이 보스 패배 {LosesOf(boss)}회");
            manager.Save();
        }

        private void RecordAuction(string result)
        {
            manager.Variables.Set("auction.lastResult", result);
            manager.Variables.Add("auction.settleCount", 1);
            manager.LogState($"경매 정산 ({result})");
            manager.Save();
        }

        private void ShiftRank(int step)
        {
            DialogueVariables variables = manager.Variables;
            int current = Math.Max(0, Array.IndexOf(RankBands, variables.Get("rank.band")));
            int next = Mathf.Clamp(current + step, 0, RankBands.Length - 1);
            if (next == current) return;

            variables.Set("rank.band", RankBands[next]);
            variables.Set("rank.dailyChange", next - current);
            if (next >= Array.IndexOf(RankBands, "2-10")) variables.Set("rank.top10Reached", "yes");

            manager.LogState($"순위 구간 {RankBands[current]} → {RankBands[next]}");
            manager.Save();
        }

        private void OpenSaveFolder()
        {
            string path = manager.SavePath;
            Debug.Log($"[Step09] 세이브 위치: {path}");
            Application.OpenURL("file:///" + Path.GetDirectoryName(path).Replace('\\', '/'));
        }
    }
}
