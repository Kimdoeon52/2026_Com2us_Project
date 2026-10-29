using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public class Step09_DialogueDebug : MonoBehaviour
    {
        private static readonly string[] Difficulties = { "veryEasy", "easy", "normal", "hard", "hell" };
        private static readonly string[] RankBands = { "101+", "31-100", "11-30", "2-10", "1" };

        [Header("연결")]
        [SerializeField] private DialogueManager manager;

        [Header("F4로 말 걸 대사 풀")]
        [SerializeField] private string talkPoolId = "auction_npc";

        [Header("디버그 토벌 보스")]
        [SerializeField] private string seasonBoss = "boss.wheel";
        [SerializeField] private string otherBoss = "boss.hammer";

        private int difficultyIndex = 2;

        private void Start()
        {
            if (manager == null)
            {
                Debug.LogError("[Step09] Manager가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Step09] 1~5 난이도 / F1 패배 (Shift: 다른 보스) / F2 승리 / F3 유찰 (Shift: 판매) / F4 말 걸기 / F5 기록 / F6 저장 / F7 다시 읽기 / F8 세이브 폴더 / F9 순위 올리기 (Shift: 내리기) / F10 풀스크린 / F11 인터미션 / F12 초기화 후 저장 / Shift+F12 세이브 삭제");
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
            else if (keyboard.f1Key.wasPressedThisFrame) RecordHunt("lose", shift ? otherBoss : seasonBoss);
            else if (keyboard.f2Key.wasPressedThisFrame) RecordHunt("win", seasonBoss);
            else if (keyboard.f3Key.wasPressedThisFrame) RecordAuction(shift ? "sold" : "unsold");
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

        private void RecordHunt(string result, string boss)
        {
            DialogueVariables variables = manager.Variables;
            string difficulty = Difficulties[difficultyIndex];

            variables.Set("hunt.lastResult", result);
            variables.Set("hunt.lastBoss", boss);
            variables.Set("hunt.lastDifficulty", difficulty);
            variables.Add("hunt.count", 1);
            variables.Add(result == "win" ? "hunt.winCount" : "hunt.loseCount", 1);
            if (result == "lose" && boss == seasonBoss) variables.Add("hunt.bossLoseCount", 1);

            manager.LogState($"{(result == "win" ? "승리" : "패배")} ({boss}, {difficulty})");
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
