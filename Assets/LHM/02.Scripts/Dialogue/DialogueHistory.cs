using System;
using System.Collections.Generic;
using System.Text;

namespace RealSteel.Dialogue
{
    [Serializable]
    public class PlayRecord
    {
        public string id;
        public int playCount;
        public int lastOrder;
    }

    public class DialogueHistory
    {
        private const string GroupKeyPrefix = "group:";

        private readonly Dictionary<string, PlayRecord> records = new Dictionary<string, PlayRecord>();
        private readonly Dictionary<string, string> eventMarks = new Dictionary<string, string>();
        private readonly Dictionary<string, float> lastPlayedTimes = new Dictionary<string, float>();
        private int playOrder;

        public int GetPlayCount(string id)
        {
            return records.TryGetValue(id, out PlayRecord record) ? record.playCount : 0;
        }

        public int GetLastOrder(string id)
        {
            return records.TryGetValue(id, out PlayRecord record) ? record.lastOrder : 0;
        }

        public string GetBlockReason(DialogueData dialogue, DialogueVariables variables, float now)
        {
            int count = GetPlayCount(dialogue.id);

            if (dialogue.maxPlays > 0 && count >= dialogue.maxPlays)
            {
                return dialogue.maxPlays == 1 ? "1회 대사 — 이미 봄" : $"최대 {dialogue.maxPlays}회 — 이미 {count}회 봄";
            }

            if (!string.IsNullOrEmpty(dialogue.perEvent))
            {
                string current = variables.Get(dialogue.perEvent);
                if (eventMarks.TryGetValue(EventKey(dialogue), out string marked) && marked == current)
                {
                    string owner = string.IsNullOrEmpty(dialogue.eventGroup) ? "" : $"[{dialogue.eventGroup}] ";
                    return $"사건당 1회 — {owner}{dialogue.perEvent}={current}에 이미 반응함";
                }
            }

            if (dialogue.cooldownSec > 0f && lastPlayedTimes.TryGetValue(dialogue.id, out float playedAt))
            {
                float remaining = dialogue.cooldownSec - (now - playedAt);
                if (remaining > 0f) return $"쿨다운 — {remaining:0.0}초 남음";
            }

            return null;
        }

        public void RecordPlay(DialogueData dialogue, DialogueVariables variables, float now)
        {
            if (!records.TryGetValue(dialogue.id, out PlayRecord record))
            {
                record = new PlayRecord { id = dialogue.id };
                records.Add(dialogue.id, record);
            }

            record.playCount++;
            record.lastOrder = ++playOrder;

            if (!string.IsNullOrEmpty(dialogue.perEvent))
            {
                eventMarks[EventKey(dialogue)] = variables.Get(dialogue.perEvent);
            }

            lastPlayedTimes[dialogue.id] = now;
        }

        public DialogueSaveData ToSaveData()
        {
            DialogueSaveData data = new DialogueSaveData
            {
                playOrder = playOrder,
                records = new List<PlayRecord>(records.Values),
                eventMarks = new List<EventMark>(),
            };

            foreach (KeyValuePair<string, string> mark in eventMarks)
            {
                data.eventMarks.Add(new EventMark { key = mark.Key, value = mark.Value });
            }

            return data;
        }

        public void LoadFrom(DialogueSaveData data)
        {
            Clear();
            if (data == null) return;

            playOrder = data.playOrder;

            if (data.records != null)
            {
                foreach (PlayRecord record in data.records)
                {
                    if (string.IsNullOrEmpty(record.id) || records.ContainsKey(record.id)) continue;
                    records.Add(record.id, record);
                    if (record.lastOrder > playOrder) playOrder = record.lastOrder;
                }
            }

            if (data.eventMarks != null)
            {
                foreach (EventMark mark in data.eventMarks)
                {
                    if (!string.IsNullOrEmpty(mark.key)) eventMarks[mark.key] = mark.value;
                }
            }
        }

        public void MigrateIds(IReadOnlyList<DialogueData> dialogues)
        {
            foreach (DialogueData dialogue in dialogues)
            {
                if (dialogue.formerIds == null) continue;

                foreach (string formerId in dialogue.formerIds)
                {
                    if (string.IsNullOrEmpty(formerId)) continue;

                    if (records.TryGetValue(formerId, out PlayRecord old))
                    {
                        if (!records.TryGetValue(dialogue.id, out PlayRecord current))
                        {
                            current = new PlayRecord { id = dialogue.id };
                            records.Add(dialogue.id, current);
                        }

                        current.playCount += old.playCount;
                        if (old.lastOrder > current.lastOrder) current.lastOrder = old.lastOrder;
                        records.Remove(formerId);
                        UnityEngine.Debug.Log($"[History] 기록 이관: {formerId} → {dialogue.id} ({old.playCount}회)");
                    }

                    if (string.IsNullOrEmpty(dialogue.eventGroup) && eventMarks.TryGetValue(formerId, out string mark))
                    {
                        if (!eventMarks.ContainsKey(dialogue.id)) eventMarks[dialogue.id] = mark;
                        eventMarks.Remove(formerId);
                    }
                }
            }
        }

        public void Clear()
        {
            records.Clear();
            eventMarks.Clear();
            lastPlayedTimes.Clear();
            playOrder = 0;
        }

        public string Dump()
        {
            if (records.Count == 0) return "(기록 없음)";

            StringBuilder builder = new StringBuilder();
            foreach (PlayRecord record in records.Values)
            {
                builder.AppendLine($"  {record.id}: {record.playCount}회, 순번 {record.lastOrder}");
            }
            foreach (KeyValuePair<string, string> mark in eventMarks)
            {
                builder.AppendLine($"  [사건 표시] {mark.Key} = {mark.Value}");
            }
            return builder.ToString();
        }

        private static string EventKey(DialogueData dialogue)
        {
            return string.IsNullOrEmpty(dialogue.eventGroup) ? dialogue.id : GroupKeyPrefix + dialogue.eventGroup;
        }
    }
}
