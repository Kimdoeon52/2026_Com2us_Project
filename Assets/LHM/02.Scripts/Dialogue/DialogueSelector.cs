using System.Collections.Generic;
using System.Text;

namespace RealSteel.Dialogue
{
    public class DialogueSelector
    {
        public DialogueData Select(
            IReadOnlyList<DialogueData> dialogues,
            DialogueVariables variables,
            DialogueHistory history,
            float now,
            StringBuilder report)
        {
            List<DialogueData> passed = new List<DialogueData>();

            report?.AppendLine("[후보]");
            foreach (DialogueData dialogue in dialogues)
            {
                string failed = FindFailedCondition(dialogue, variables);
                if (failed != null)
                {
                    report?.AppendLine($"  X {dialogue.id}  — 불충족: {failed}");
                    continue;
                }

                string blocked = history.GetBlockReason(dialogue, variables, now);
                if (blocked != null)
                {
                    report?.AppendLine($"  X {dialogue.id}  — 소비됨: {blocked}");
                    continue;
                }

                passed.Add(dialogue);
                report?.AppendLine($"  O {dialogue.id}  ({dialogue.parsedBand} / 세부 {dialogue.subPriority} / 조건 {dialogue.ConditionCount}개 / 본 횟수 {history.GetPlayCount(dialogue.id)})");
            }

            if (passed.Count == 0)
            {
                report?.AppendLine("[선택] 없음 — 나올 수 있는 대사가 없습니다");
                return null;
            }

            passed.Sort((a, b) => Compare(a, b, history));

            int tieCount = 1;
            while (tieCount < passed.Count && Compare(passed[0], passed[tieCount], history) == 0) tieCount++;

            DialogueData chosen = passed[UnityEngine.Random.Range(0, tieCount)];
            DialogueData runnerUp = tieCount < passed.Count ? passed[tieCount] : null;

            report?.AppendLine($"[선택] {chosen.id}");
            report?.AppendLine($"[이유] {Explain(chosen, runnerUp, passed.Count, tieCount, history)}");
            return chosen;
        }

        private static string FindFailedCondition(DialogueData dialogue, DialogueVariables variables)
        {
            if (dialogue.conditions == null) return null;

            foreach (ConditionData condition in dialogue.conditions)
            {
                if (!DialogueConditions.Evaluate(condition, variables))
                {
                    return $"{condition} (현재: {variables.Get(condition.variable)})";
                }
            }

            return null;
        }

        private static int Compare(DialogueData a, DialogueData b, DialogueHistory history)
        {
            int result = b.parsedBand.CompareTo(a.parsedBand);
            if (result != 0) return result;

            result = b.subPriority.CompareTo(a.subPriority);
            if (result != 0) return result;

            result = b.ConditionCount.CompareTo(a.ConditionCount);
            if (result != 0) return result;

            result = history.GetPlayCount(a.id).CompareTo(history.GetPlayCount(b.id));
            if (result != 0) return result;

            return history.GetLastOrder(a.id).CompareTo(history.GetLastOrder(b.id));
        }

        private static string Explain(DialogueData chosen, DialogueData runnerUp, int passedCount, int tieCount, DialogueHistory history)
        {
            string tie = tieCount > 1 ? $" / 동률 {tieCount}개 중 무작위" : "";

            if (passedCount == 1) return "통과한 후보가 하나뿐" + tie;
            if (runnerUp == null) return "통과한 후보가 모두 동률" + tie;

            if (chosen.parsedBand != runnerUp.parsedBand)
                return $"우선순위 단계가 더 높음 ({chosen.parsedBand} > {runnerUp.parsedBand}: {runnerUp.id})" + tie;

            if (chosen.subPriority != runnerUp.subPriority)
                return $"세부 순위가 더 높음 ({chosen.subPriority} > {runnerUp.subPriority}: {runnerUp.id})" + tie;

            if (chosen.ConditionCount != runnerUp.ConditionCount)
                return $"조건이 더 구체적 ({chosen.ConditionCount}개 > {runnerUp.ConditionCount}개: {runnerUp.id})" + tie;

            int chosenCount = history.GetPlayCount(chosen.id);
            int runnerCount = history.GetPlayCount(runnerUp.id);
            if (chosenCount != runnerCount)
                return $"덜 본 대사 ({chosenCount}회 < {runnerCount}회: {runnerUp.id})" + tie;

            return $"더 오래전에 본 대사 ({runnerUp.id}를 더 최근에 봄)" + tie;
        }
    }
}
