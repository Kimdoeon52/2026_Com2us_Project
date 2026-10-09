using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RealSteel.Dialogue
{
    public enum IssueSeverity { Info, Warning, Error }

    public sealed class ValidationIssue
    {
        public IssueSeverity Severity;
        public string File;
        public string Context;   // poolId / entryId / key 등
        public string Message;

        public override string ToString() =>
            $"[{Severity}] {File ?? "-"} :: {Context ?? "-"} :: {Message}";
    }

    /// <summary>로드·검증 결과. 에러가 있어도 가능한 만큼은 로드하고, 무엇이 빠졌는지 여기에 남긴다.</summary>
    public sealed class ValidationReport
    {
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>();
        public IReadOnlyList<ValidationIssue> Issues => _issues;

        public bool HasErrors => _issues.Any(i => i.Severity == IssueSeverity.Error);
        public int ErrorCount => _issues.Count(i => i.Severity == IssueSeverity.Error);
        public int WarningCount => _issues.Count(i => i.Severity == IssueSeverity.Warning);

        public void Error(string file, string ctx, string msg) => Add(IssueSeverity.Error, file, ctx, msg);
        public void Warn(string file, string ctx, string msg) => Add(IssueSeverity.Warning, file, ctx, msg);
        public void Info(string file, string ctx, string msg) => Add(IssueSeverity.Info, file, ctx, msg);

        private void Add(IssueSeverity s, string f, string c, string m) =>
            _issues.Add(new ValidationIssue { Severity = s, File = f, Context = c, Message = m });

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Dialogue validation: {ErrorCount} error(s), {WarningCount} warning(s)");
            foreach (var i in _issues.OrderByDescending(x => x.Severity)) sb.AppendLine(i.ToString());
            return sb.ToString();
        }
    }
}
