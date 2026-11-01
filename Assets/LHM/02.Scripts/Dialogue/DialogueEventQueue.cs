using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class DialogueEventQueue : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private DialogueManager manager;

        [Header("데이터")]
        [SerializeField] private TextAsset triggersJson;

        [Header("출력")]
        [SerializeField, Min(0f)] private float delayAfterReturn = 0.5f;

        private readonly List<DialogueTriggerData> triggers = new List<DialogueTriggerData>();
        private readonly List<DialogueEvent> pending = new List<DialogueEvent>();
        private float idleTime;

        public int Count => pending.Count;

        public event Action<string> BubbleRequested;

        private void Start()
        {
            if (manager == null)
            {
                Debug.LogError("[EventQueue] Manager가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            LoadTriggers();
        }

        private void Update()
        {
            if (!IsFieldIdle())
            {
                idleTime = 0f;
                return;
            }

            idleTime += Time.unscaledDeltaTime;
            if (pending.Count == 0 || idleTime < delayAfterReturn) return;

            DialogueEvent next = pending[0];
            pending.RemoveAt(0);
            idleTime = 0f;
            Play(next);
        }

        public int EvaluateMidnight()
        {
            return Evaluate("00시");
        }

        public int Evaluate(string label)
        {
            StringBuilder log = new StringBuilder($"[EventQueue] {label} 대사 트리거 평가\n");
            int added = 0;

            foreach (DialogueTriggerData trigger in triggers)
            {
                if (Contains(trigger.dialogueId))
                {
                    log.AppendLine($"  = {trigger.dialogueId} — 이미 대기 중");
                    continue;
                }

                StringBuilder detail = new StringBuilder();
                if (manager.CanPlay(trigger.dialogueId, detail))
                {
                    Insert(trigger.dialogueId, trigger.parsedKind);
                    log.AppendLine($"  + {trigger.dialogueId} — 적재 ({trigger.parsedKind})");
                    added++;
                }
                else
                {
                    log.AppendLine($"  - {FirstReason(detail)}");
                }
            }

            log.Append($"적재 {added}개 → 대기 {pending.Count}개: {Dump()}");
            Debug.Log(log.ToString(), this);
            return added;
        }

        public bool Enqueue(string dialogueId, DialogueEventKind kind)
        {
            if (!manager.HasDialogue(dialogueId))
            {
                Debug.LogWarning($"[EventQueue] 대사 풀에 없는 대화 '{dialogueId}'는 적재하지 않습니다.", this);
                return false;
            }

            if (Contains(dialogueId)) return false;

            Insert(dialogueId, kind);
            Debug.Log($"[EventQueue] 적재: {kind} {dialogueId} → 대기 {pending.Count}개", this);
            return true;
        }

        public void Clear()
        {
            pending.Clear();
        }

        public string Dump()
        {
            return pending.Count == 0 ? "(없음)" : string.Join(" → ", pending);
        }

        private bool IsFieldIdle()
        {
            return IsFieldIdle(manager);
        }

        public static bool IsFieldIdle(DialogueManager manager)
        {
            if (manager == null || !manager.IsReady || manager.IsTalking) return false;
            if (UiStack.Current != null && UiStack.Current.Count > 0) return false;

            InputModeController input = InputModeController.Current;
            return input == null || (input.BaseMode == InputMode.Field && !input.IsWaitingRelease);
        }

        private void Play(DialogueEvent item)
        {
            if (item.kind == DialogueEventKind.Bubble)
            {
                if (BubbleRequested != null) BubbleRequested(item.dialogueId);
                else Debug.Log($"[EventQueue] 말풍선 출력 (15단계 전까지 로그만, 기록 안 함): {item.dialogueId}", this);
                return;
            }

            if (!manager.PlayDialogue(item.dialogueId))
            {
                Debug.LogWarning($"[EventQueue] {item} — 출력 시점에 조건이 바뀌어 건너뜁니다.", this);
                return;
            }

            Debug.Log($"[EventQueue] 출력: {item} → 남은 대기 {pending.Count}개", this);
        }

        private void Insert(string dialogueId, DialogueEventKind kind)
        {
            DialogueEvent item = new DialogueEvent { dialogueId = dialogueId, kind = kind };

            int index = pending.FindIndex(e => e.kind > kind);
            if (index < 0) pending.Add(item);
            else pending.Insert(index, item);
        }

        private bool Contains(string dialogueId)
        {
            return pending.Exists(e => e.dialogueId == dialogueId);
        }

        private void LoadTriggers()
        {
            triggers.Clear();
            if (triggersJson == null)
            {
                Debug.LogWarning("[EventQueue] Triggers Json이 연결되지 않아 00시 평가 대상이 없습니다.", this);
                return;
            }

            if (manager.IsFallback)
            {
                Debug.LogWarning("[EventQueue] 대사 데이터 폴백 상태라 트리거를 읽지 않습니다 (이벤트 대사 없음).", this);
                return;
            }

            List<string> errors = new List<string>();
            triggers.AddRange(DialogueTriggerTable.Validate(
                triggersJson.name,
                triggersJson.text,
                manager.HasDialogue,
                id => manager.TryGetFirstLine(id, out DialogueLine line) ? line.speakerId : null,
                errors));

            if (errors.Count > 0) Debug.LogError($"[EventQueue] 트리거 {errors.Count}건 제외\n - " + string.Join("\n - ", errors), this);
            Debug.Log($"[EventQueue] 트리거 {triggers.Count}개 로드", this);
        }

        private static string FirstReason(StringBuilder detail)
        {
            foreach (string line in detail.ToString().Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("X ")) return trimmed.Substring(2);
            }
            return "나올 수 없음";
        }
    }
}
