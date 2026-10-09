using System;
using System.Collections.Generic;
using System.Linq;

namespace RealSteel.Dialogue
{
    /// <summary>
    /// 파싱/컴파일 이후의 "의미" 검증. 에디터 메뉴, JSON 변경 시 자동 검사, 빌드 전 차단에서 공용으로 사용.
    /// 규칙 목록은 DESIGN.md 5장과 동일하게 유지할 것.
    /// </summary>
    public static class DialogueValidator
    {
        public static void Validate(DialogueDatabase db, ValidationReport report)
        {
            var allLineIds = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var pool in db.Pools.Values)
            {
                string file = pool.SourceFile;
                ValidatePool(pool, file, report);

                foreach (var e in pool.entries)
                {
                    ValidateEntry(db, pool, e, file, report);

                    foreach (var line in e.lines.Concat(e.shortLines))
                    {
                        if (allLineIds.TryGetValue(line.ResolvedLineId, out var owner))
                            report.Error(file, e.id, $"lineId 중복 '{line.ResolvedLineId}' (기존: {owner})");
                        else
                            allLineIds.Add(line.ResolvedLineId, e.id);
                    }
                }

                FindUnreachable(pool, file, report);
            }

            // formerIds가 현재 살아있는 ID와 겹치면 세이브 이관이 꼬인다
            foreach (var e in db.Entries.Values)
                foreach (var old in e.formerIds)
                    if (db.Entries.ContainsKey(old))
                        report.Error(e.Pool.SourceFile, e.id, $"formerIds '{old}'가 현재 존재하는 엔트리 ID와 겹칩니다.");
        }

        private static PresentationMode ExpectedMode(DialogueCategory c)
        {
            switch (c)
            {
                case DialogueCategory.PartBreak: return PresentationMode.Bark;
                case DialogueCategory.Crowd: return PresentationMode.Bubble;
                default: return PresentationMode.Blocking;
            }
        }

        private static void ValidatePool(DialoguePool pool, string file, ValidationReport report)
        {
            if (pool.entries.Count == 0) report.Warn(file, pool.poolId, "엔트리가 없는 풀");

            if (pool.mode != ExpectedMode(pool.category))
                report.Warn(file, pool.poolId, $"{pool.category} 풀은 보통 {ExpectedMode(pool.category)} 모드입니다 (현재 {pool.mode}).");

            // 허브/관중/파츠파괴는 조건이 다 빗나가면 아무 말도 안 하는 사고가 난다
            bool needsFallback = pool.category == DialogueCategory.Hub
                              || pool.category == DialogueCategory.Crowd
                              || pool.category == DialogueCategory.PartBreak;
            if (needsFallback && !pool.entries.Any(IsUnconditionalRepeatable))
                report.Warn(file, pool.poolId, "조건 없고 반복 가능한 Fallback 엔트리가 없습니다 → 조건이 모두 빗나가면 침묵합니다.");
        }

        private static bool IsUnconditionalRepeatable(DialogueEntry e) =>
            e.conditions == null && !e.once && e.maxPlays == 0;

        private static void ValidateEntry(DialogueDatabase db, DialoguePool pool, DialogueEntry e, string file, ValidationReport report)
        {
            if (e.subPriority < 0 || e.subPriority > 99)
                report.Error(file, e.id, $"subPriority는 0~99 (현재 {e.subPriority}). 더 큰 차이는 band로 표현하세요.");

            if (e.lines.Count == 0) report.Error(file, e.id, "lines가 비어 있습니다.");

            if (e.repeatPolicy == RepeatPolicy.ShortOnRepeat && e.shortLines.Count == 0)
                report.Error(file, e.id, "repeatPolicy=ShortOnRepeat 인데 shortLines가 비어 있습니다.");
            if (e.repeatPolicy != RepeatPolicy.ShortOnRepeat && e.shortLines.Count > 0)
                report.Warn(file, e.id, "shortLines가 있지만 repeatPolicy가 ShortOnRepeat가 아니라 사용되지 않습니다.");
            if (e.repeatPolicy != RepeatPolicy.Full && pool.mode != PresentationMode.Blocking)
                report.Warn(file, e.id, "repeatPolicy는 Blocking 대화에서만 의미가 있습니다.");
            if (e.once && e.repeatPolicy != RepeatPolicy.Full)
                report.Warn(file, e.id, "once 엔트리는 다시 재생되지 않으므로 repeatPolicy가 무의미합니다.");

            if (e.band == PriorityBand.Critical && pool.category != DialogueCategory.MainStory && pool.category != DialogueCategory.BossIntro)
                report.Warn(file, e.id, "Critical 밴드는 MainStory/BossIntro 전용입니다.");

            if (e.band == PriorityBand.Fallback && e.conditions != null)
                report.Warn(file, e.id, "Fallback 밴드인데 조건이 있습니다. 조건이 있으면 Ambient 이상을 쓰세요.");

            if (pool.mode != PresentationMode.Blocking)
            {
                if (e.lines.Count > 1) report.Warn(file, e.id, $"{pool.mode} 대사는 1줄 권장 (현재 {e.lines.Count}줄).");
                if (e.cooldownSec <= 0) report.Warn(file, e.id, $"{pool.mode} 대사에 cooldownSec이 없으면 같은 대사가 연속 출력될 수 있습니다.");
            }

            if (pool.category == DialogueCategory.Hub && e.band == PriorityBand.Reactive
                && e.oncePer == null && !e.once && e.maxPlays == 0)
                report.Warn(file, e.id, "허브 Reactive 대사에 소비 규칙(oncePer/once/maxPlays)이 없어 같은 반응이 계속 반복됩니다.");

            if (e.cooldownSec < 0) report.Error(file, e.id, "cooldownSec은 음수일 수 없습니다.");
            if (e.maxPlays < 0) report.Error(file, e.id, "maxPlays는 음수일 수 없습니다.");

            if (pool.category == DialogueCategory.MainStory && e.lines.Any(l => string.IsNullOrEmpty(l.lineId)))
                report.Warn(file, e.id, "메인 스토리 대사는 lineId를 직접 지정하세요. 자동 ID(#번호)는 대사 삽입 시 '읽음' 기록이 밀립니다.");

            foreach (var l in e.lines.Concat(e.shortLines))
            {
                if (string.IsNullOrEmpty(l.textKey) && string.IsNullOrEmpty(l.text))
                    report.Error(file, e.id, $"대사 '{l.ResolvedLineId}'에 textKey와 text가 모두 없습니다.");

                if (pool.mode == PresentationMode.Bubble && string.IsNullOrEmpty(l.speaker))
                    continue; // 관중 말풍선은 화자 생략 허용

                if (string.IsNullOrEmpty(l.speaker))
                {
                    report.Error(file, e.id, $"대사 '{l.ResolvedLineId}'에 speaker가 없습니다.");
                    continue;
                }
                if (!db.Speakers.TryGetValue(l.speaker, out var sp))
                {
                    report.Error(file, e.id, $"speakers.json에 없는 화자 '{l.speaker}'");
                    continue;
                }
                if (!string.IsNullOrEmpty(l.portrait) && !sp.portraits.Contains(l.portrait))
                    report.Error(file, e.id, $"화자 '{l.speaker}'에 없는 초상화 '{l.portrait}'");
            }

            CheckSeenRefs(db, e.conditions, file, e.id, report);
        }

        private static void CheckSeenRefs(DialogueDatabase db, ConditionNode n, string file, string ctx, ValidationReport report)
        {
            if (n == null) return;
            switch (n.Kind)
            {
                case ConditionKind.All:
                case ConditionKind.Any:
                    foreach (var c in (n.all ?? n.any)) CheckSeenRefs(db, c, file, ctx, report);
                    break;
                case ConditionKind.Not:
                    CheckSeenRefs(db, n.not, file, ctx, report);
                    break;
                case ConditionKind.Leaf:
                    if (n.CompiledEntryRef != null && !db.Entries.ContainsKey(n.CompiledEntryRef))
                        report.Error(file, ctx, $"seen/notSeen이 존재하지 않는 엔트리 '{n.CompiledEntryRef}'를 참조합니다.");
                    break;
            }
        }

        /// <summary>
        /// 도달 불가 탐지: 같은 풀에 "조건 없음 + 무제한 + 쿨다운 없음" 엔트리 D가 있고
        /// D의 (band, subPriority)가 E보다 엄격히 높으면 E는 절대 선택되지 않는다.
        /// </summary>
        private static void FindUnreachable(DialoguePool pool, string file, ValidationReport report)
        {
            var dominators = pool.entries.Where(d => IsUnconditionalRepeatable(d) && d.cooldownSec <= 0).ToList();
            if (dominators.Count == 0) return;

            foreach (var e in pool.entries)
            {
                foreach (var d in dominators)
                {
                    if (ReferenceEquals(d, e)) continue;
                    bool higher = (int)d.band > (int)e.band || ((int)d.band == (int)e.band && d.subPriority > e.subPriority);
                    if (higher)
                    {
                        report.Warn(file, e.id, $"도달 불가: 항상 참인 '{d.id}'({d.band}/{d.subPriority})에 가려져 선택될 수 없습니다.");
                        break;
                    }
                }
            }
        }
    }
}
