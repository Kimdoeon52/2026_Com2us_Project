using System;
using System.Collections.Generic;

namespace RealSteel.Dialogue
{
    public enum DialogueEventKind
    {
        Story = 0,
        System = 1,
        Bubble = 2,
    }

    [Serializable]
    public class DialogueTriggerTable
    {
        public List<DialogueTriggerData> triggers;

        public static List<DialogueTriggerData> Validate(string fileName, string json, Func<string, bool> hasDialogue, Func<string, string> firstSpeaker, List<string> errors)
        {
            List<DialogueTriggerData> valid = new List<DialogueTriggerData>();

            DialogueTriggerTable table;
            try
            {
                table = UnityEngine.JsonUtility.FromJson<DialogueTriggerTable>(json ?? "");
            }
            catch (ArgumentException e)
            {
                errors.Add($"{fileName}: JSON 문법 오류 — {e.Message}");
                return valid;
            }

            if (table?.triggers == null) return valid;

            for (int i = 0; i < table.triggers.Count; i++)
            {
                DialogueTriggerData trigger = table.triggers[i];
                string where = $"{fileName} › {i + 1}번째 트리거";

                if (!TryParseKind(trigger.queue, out trigger.parsedKind)) errors.Add($"{where}: queue '{trigger.queue}'는 story / system / bubble 중 하나여야 합니다");
                else if (!hasDialogue(trigger.dialogueId)) errors.Add($"{where}: 대사 풀에 없는 대화 '{trigger.dialogueId}'");
                else if (valid.Exists(t => t.dialogueId == trigger.dialogueId)) errors.Add($"{where}: '{trigger.dialogueId}' 중복");
                else if (trigger.parsedKind == DialogueEventKind.Bubble && string.IsNullOrEmpty(firstSpeaker(trigger.dialogueId))) errors.Add($"{where}: 말풍선 '{trigger.dialogueId}'의 첫 줄에 speaker가 없습니다 (말풍선을 띄울 NPC를 화자로 찾음)");
                else valid.Add(trigger);
            }

            return valid;
        }

        public static bool TryParseKind(string value, out DialogueEventKind kind)
        {
            switch (value)
            {
                case "story": kind = DialogueEventKind.Story; return true;
                case "system": kind = DialogueEventKind.System; return true;
                case "bubble": kind = DialogueEventKind.Bubble; return true;
                default: kind = DialogueEventKind.Story; return false;
            }
        }
    }

    [Serializable]
    public class DialogueTriggerData
    {
        public string dialogueId;
        public string queue;

        [NonSerialized] public DialogueEventKind parsedKind;
    }

    public class DialogueEvent
    {
        public string dialogueId;
        public DialogueEventKind kind;

        public override string ToString() => $"{kind} {dialogueId}";
    }
}
