using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace RealSteel.Dialogue
{
    public class Step06_DialogueTest : MonoBehaviour
    {
        private static readonly string[] Difficulties = { "veryEasy", "easy", "normal", "hard", "hell" };
        private static readonly string[] RankBands = { "101+", "31-100", "11-30", "2-10", "1" };

        [Header("연결")]
        [SerializeField] private DialogueBox dialogueBox;

        [Header("데이터")]
        [SerializeField] private TextAsset[] poolJson;
        [SerializeField] private TextAsset stringTableJson;
        [SerializeField] private TextAsset variablesJson;

        [Header("세이브")]
        [SerializeField] private string saveFileName = "dialogue_save.json";

        [Header("디버그 토벌 보스")]
        [FormerlySerializedAs("mainOpponent")]
        [SerializeField] private string seasonBoss = "boss.wheel";
        [FormerlySerializedAs("otherOpponent")]
        [SerializeField] private string otherBoss = "boss.hammer";

        private readonly DialogueDatabase database = new DialogueDatabase();
        private readonly DialogueSelector selector = new DialogueSelector();
        private readonly DialogueHistory history = new DialogueHistory();
        private DialogueVariables variables;
        private DialogueSaveSystem saveSystem;
        private int difficultyIndex = 2;
        private bool ready;

        private void Start()
        {
            if (dialogueBox == null)
            {
                Debug.LogError("[Step06] Dialogue Box가 연결되지 않았습니다.", this);
                return;
            }

            if (!database.Load(poolJson, stringTableJson, variablesJson)) return;

            variables = new DialogueVariables(database.VariableDefs);
            saveSystem = new DialogueSaveSystem(Path.Combine(Application.persistentDataPath, saveFileName));
            LoadFromFile();
            ready = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Step06] 1~5 난이도 선택 / F1 패배 (Shift: 다른 보스) / F2 승리 / F3 유찰 (Shift: 판매) / F4 말 걸기 / F5 기록 / F6 저장 / F7 다시 읽기 / F8 세이브 폴더 / F9 순위 올리기 (Shift: 내리기) / F10 풀스크린 시험 / F11 인터미션 시험 / F12 초기화 후 저장 / Shift+F12 세이브 삭제");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (!ready || dialogueBox.IsOpen) return;

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
            else if (keyboard.f4Key.wasPressedThisFrame) Talk();
            else if (keyboard.f5Key.wasPressedThisFrame) Debug.Log("[Step06] 본 대사 기록\n" + history.Dump());
            else if (keyboard.f6Key.wasPressedThisFrame) SaveToFile();
            else if (keyboard.f7Key.wasPressedThisFrame) LoadFromFile();
            else if (keyboard.f8Key.wasPressedThisFrame) OpenSaveFolder();
            else if (keyboard.f9Key.wasPressedThisFrame) ShiftRank(shift ? -1 : 1);
            else if (keyboard.f10Key.wasPressedThisFrame) OpenDirect("test_prologue");
            else if (keyboard.f11Key.wasPressedThisFrame) OpenDirect("test_intermission");
            else if (keyboard.f12Key.wasPressedThisFrame) ResetAll(shift);
        }
#endif

        private void SelectDifficulty(int index)
        {
            difficultyIndex = index;
            Debug.Log($"[Step06] 다음 토벌 난이도: {Difficulties[difficultyIndex]}");
        }

        private void RecordHunt(string result, string boss)
        {
            string difficulty = Difficulties[difficultyIndex];
            variables.Set("hunt.lastResult", result);
            variables.Set("hunt.lastBoss", boss);
            variables.Set("hunt.lastDifficulty", difficulty);
            variables.Add("hunt.count", 1);
            variables.Add(result == "win" ? "hunt.winCount" : "hunt.loseCount", 1);
            if (result == "lose" && boss == seasonBoss) variables.Add("hunt.bossLoseCount", 1);
            LogState($"{(result == "win" ? "승리" : "패배")} ({boss}, {difficulty})");
            SaveToFile();
        }

        private void RecordAuction(string result)
        {
            variables.Set("auction.lastResult", result);
            variables.Add("auction.settleCount", 1);
            LogState($"경매 정산 ({result})");
            SaveToFile();
        }

        private void ShiftRank(int step)
        {
            int current = Math.Max(0, Array.IndexOf(RankBands, variables.Get("rank.band")));
            int next = Mathf.Clamp(current + step, 0, RankBands.Length - 1);
            if (next == current) return;

            variables.Set("rank.band", RankBands[next]);
            variables.Set("rank.dailyChange", next - current);
            if (next >= Array.IndexOf(RankBands, "2-10")) variables.Set("rank.top10Reached", "yes");
            LogState($"순위 구간 {RankBands[current]} → {RankBands[next]}");
            SaveToFile();
        }

        private void Talk()
        {
            float now = Time.unscaledTime;
            StringBuilder report = new StringBuilder();
            DialogueData chosen = selector.Select(database.Dialogues, variables, history, now, report);
            Debug.Log("[Step06] 말 걸기\n" + report);

            if (chosen == null) return;

            if (database.TryGetLines(chosen, out List<DialogueLine> lines))
            {
                history.RecordPlay(chosen, variables, now);
                SaveToFile();
                dialogueBox.Open(lines, chosen.parsedDisplay, chosen.background);
            }
        }

        private void OpenDirect(string dialogueId)
        {
            if (!database.TryGetDialogue(dialogueId, out DialogueData data))
            {
                Debug.LogWarning($"[Step06] 대화 '{dialogueId}'가 대사 풀에 없습니다.", this);
                return;
            }

            if (database.TryGetLines(data, out List<DialogueLine> lines))
            {
                Debug.Log($"[Step06] 직접 열기: {dialogueId} ({data.parsedDisplay})");
                dialogueBox.Open(lines, data.parsedDisplay, data.background);
            }
        }

        private void SaveToFile()
        {
            SaveFile data = new SaveFile
            {
                dialogue = history.ToSaveData(),
                variables = variables.ToSaveList(),
            };
            saveSystem.Save(data);
        }

        private void LoadFromFile()
        {
            if (saveSystem.TryLoad(out SaveFile data))
            {
                history.LoadFrom(data.dialogue);
                variables.LoadFrom(data.variables);
                history.MigrateIds(database.Dialogues);
            }
            else
            {
                history.Clear();
                variables.Reset();
            }

            LogState("불러오기");
        }

        private void ResetAll(bool deleteFile)
        {
            history.Clear();
            variables.Reset();

            if (deleteFile)
            {
                saveSystem.DeleteAll();
                LogState("세이브 삭제 + 초기화");
            }
            else
            {
                LogState("초기화");
                SaveToFile();
            }
        }

        private void OpenSaveFolder()
        {
            string folder = Path.GetDirectoryName(saveSystem.MainPath);
            Debug.Log($"[Step06] 세이브 위치: {saveSystem.MainPath}");
            Application.OpenURL("file:///" + folder.Replace('\\', '/'));
        }

        private void LogState(string label)
        {
            Debug.Log($"[Step06] {label} → {variables.Dump()}\n{history.Dump()}");
        }
    }
}
