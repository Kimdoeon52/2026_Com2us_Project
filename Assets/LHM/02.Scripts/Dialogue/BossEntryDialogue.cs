using System;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class BossEntryDialogue : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private DialogueManager manager;

        [Header("데이터")]
        [SerializeField] private string poolId = "boss_entry";

        private Action pending;
        private DialogueBox subscribedBox;

        public bool IsPlaying => pending != null;

        public void Play(string bossId, string difficulty, int attemptCount, int loseCount, Action onFinished)
        {
            if (manager != null && manager.IsReady && !manager.IsFallback)
            {
                DialogueVariables variables = manager.Variables;
                variables.Set("hunt.targetBoss", bossId ?? "");
                variables.Set("hunt.targetDifficulty", string.IsNullOrEmpty(difficulty) ? "none" : difficulty);
                variables.Set("hunt.bossAttemptCount", attemptCount);
                variables.Set("hunt.bossLoseCount", loseCount);
                Debug.Log($"[BossEntry] 진입 대상 {bossId} ({difficulty}) — 이 보스 도전 {attemptCount}회, 패배 {loseCount}회", this);
            }

            Play(onFinished);
        }

        public void Play(Action onFinished)
        {
            if (pending != null)
            {
                Debug.LogWarning("[BossEntry] 이미 진입 대사를 재생 중입니다.", this);
                return;
            }

            if (manager == null || manager.Box == null || !manager.Talk(poolId))
            {
                Debug.LogWarning($"[BossEntry] 진입 대사 '{poolId}'를 재생하지 못해 바로 전투를 시작합니다.", this);
                onFinished?.Invoke();
                return;
            }

            pending = onFinished ?? (() => { });
            subscribedBox = manager.Box;
            subscribedBox.Closed += OnClosed;
        }

        private void OnClosed()
        {
            subscribedBox.Closed -= OnClosed;
            subscribedBox = null;

            Action callback = pending;
            pending = null;
            callback();
        }

        private void OnDestroy()
        {
            if (subscribedBox != null) subscribedBox.Closed -= OnClosed;
        }
    }
}
