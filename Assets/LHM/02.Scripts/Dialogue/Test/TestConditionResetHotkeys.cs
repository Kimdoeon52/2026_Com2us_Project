using System.Collections.Generic;
using RealSteel.Tutorial;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue
{
    /// <summary>
    /// 테스트용 조건 초기화 단축키 (Map_Test, League_Lobby, Auction, Home_Workshop 씬에서 동작).
    /// - Ctrl + F9  : 대화 조건 초기화 (DialogueManager.ResetAll(true))
    /// - Ctrl + F10 : 튜토리얼 초기화 (TutorialDirector.ResetAll())
    /// - Ctrl + F11 : 시즌 초기화 (SeasonResetHotkey.ResetSeason())
    /// - Ctrl + F12 : 테스트 포상 추가 (1위 10,000골드, Shift를 같이 누르면 3위 6,000골드)
    /// </summary>
    public class TestConditionResetHotkeys : MonoBehaviour
    {
        private static readonly HashSet<string> TargetScenes = new HashSet<string>
        {
            "Map_Test",
            "League_Lobby",
            "Auction",
            "Home_Workshop",
            "SeasonRewardTest"
        };

        private static TestConditionResetHotkeys instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (instance != null) return;

            GameObject go = new GameObject("[TestConditionResetHotkeys]");
            instance = go.AddComponent<TestConditionResetHotkeys>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            string activeScene = SceneManager.GetActiveScene().name;
            if (!TargetScenes.Contains(activeScene)) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.ctrlKey.isPressed) return;

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                ResetDialogueConditions();
            }
            else if (keyboard.f10Key.wasPressedThisFrame)
            {
                ResetTutorial();
            }
            else if (keyboard.f11Key.wasPressedThisFrame)
            {
                ResetSeason();
            }
            else if (keyboard.f12Key.wasPressedThisFrame)
            {
                bool first = !keyboard.shiftKey.isPressed;
                Reward.SeasonRewardStore.Add(first ? 1 : 3, first ? 10000 : 6000, $"테스트({System.DateTime.Now:HH:mm:ss})");
                Debug.Log($"[TestHotkey] 테스트 포상 추가: {(first ? "1위 10,000" : "3위 6,000")}골드 → 접수원 '포상 수령' (Ctrl{(first ? "" : " + Shift")} + F12)", this);
            }
        }

        public void ResetDialogueConditions()
        {
            DialogueManager manager = FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                Debug.LogWarning("[TestHotkey] DialogueManager를 찾을 수 없어 대화 조건을 초기화하지 못했습니다.", this);
                return;
            }

            manager.ResetAll(true);
            Debug.Log("[TestHotkey] 대화 조건 및 세이브 데이터 초기화 완료 (Ctrl + F9)", this);
        }

        public void ResetTutorial()
        {
            TutorialDirector director = FindFirstObjectByType<TutorialDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                Debug.LogWarning("[TestHotkey] TutorialDirector를 찾을 수 없어 튜토리얼을 초기화하지 못했습니다.", this);
                return;
            }

            director.ResetAll();
            Debug.Log("[TestHotkey] 튜토리얼 초기화 완료 (Ctrl + F10)", this);
        }

        public void ResetSeason()
        {
            SeasonResetHotkey seasonHotkey = FindFirstObjectByType<SeasonResetHotkey>(FindObjectsInactive.Include);
            if (seasonHotkey == null)
            {
                seasonHotkey = gameObject.AddComponent<SeasonResetHotkey>();
            }

            seasonHotkey.ResetSeason();
            Debug.Log("[TestHotkey] 시즌 초기화 실행 (Ctrl + F11)", this);
        }
    }
}

