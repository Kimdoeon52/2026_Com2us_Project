using UnityEditor;
using UnityEngine;

// 가중치 뽑기를 N회 돌려 실제 분포를 확인한다
public class GradeDistributionWindow : EditorWindow
{
    private ScrapyardDefinition _definition;
    private MinigameRewardTable _rewardTable;
    private int _trials = 10000;
    private int _seed = 1;

    private int[] _counts;
    private int _rolled;
    private int _minigameHits;
    private string _sourceLabel;

    [MenuItem("KDU/고물상/확률 분포 검증")]
    private static void Open()
    {
        GetWindow<GradeDistributionWindow>("확률 분포 검증");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("대상", EditorStyles.boldLabel);
        _definition = (ScrapyardDefinition)EditorGUILayout.ObjectField("지역 정의", _definition, typeof(ScrapyardDefinition), false);
        _rewardTable = (MinigameRewardTable)EditorGUILayout.ObjectField("보상 테이블", _rewardTable, typeof(MinigameRewardTable), false);
        EditorGUILayout.HelpBox("보상 테이블을 넣으면 그쪽 가중치를 검증한다.", MessageType.None);

        EditorGUILayout.Space();
        _trials = Mathf.Max(1, EditorGUILayout.IntField("시행 횟수", _trials));
        _seed = EditorGUILayout.IntField("시드", _seed);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(_definition == null && _rewardTable == null))
        {
            if (GUILayout.Button("시행"))
                Run();
        }

        if (_counts != null)
            DrawResult();
    }

    private void Run()
    {
        GradeWeight[] weights = _rewardTable != null ? _rewardTable.SuccessWeights : _definition.InstantWeights;
        _sourceLabel = _rewardTable != null ? "미니게임 보상" : "즉시 획득";

        _counts = new int[PartGrades.Count];
        _rolled = 0;
        _minigameHits = 0;

        int total = GradeWeightUtil.TotalWeight(weights);
        if (total <= 0)
        {
            Debug.LogWarning($"[분포 검증] {_sourceLabel} 가중치 합계가 0이다.");
            return;
        }

        var rng = new Rng(_seed);
        bool branchEnabled = _rewardTable == null && _definition != null && _definition.HasRewardTable;

        for (int i = 0; i < _trials; i++)
        {
            if (branchEnabled && rng.Chance(_definition.MinigameChance))
            {
                _minigameHits++;
                continue;
            }

            if (!GradeRoller.TryRoll(weights, rng, out PartGrade grade))
                continue;

            _counts[(int)grade]++;
            _rolled++;
        }

        LogResult(weights, total);
    }

    private void DrawResult()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"결과 ({_sourceLabel})", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("시행", _trials.ToString());

        if (_minigameHits > 0)
            EditorGUILayout.LabelField("미니게임 분기", $"{_minigameHits} ({_minigameHits * 100f / _trials:F2}%)");

        EditorGUILayout.LabelField("등급 뽑기", _rolled.ToString());

        for (int i = 0; i < _counts.Length; i++)
        {
            if (_counts[i] == 0)
                continue;

            float percent = _rolled > 0 ? _counts[i] * 100f / _rolled : 0f;
            EditorGUILayout.LabelField(PartGrades.ToLabel((PartGrade)i), $"{_counts[i]} ({percent:F2}%)");
        }
    }

    private void LogResult(GradeWeight[] weights, int total)
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine($"[분포 검증] {_sourceLabel} / 시행 {_trials} / 시드 {_seed}");

        if (_minigameHits > 0)
            text.AppendLine($"미니게임 분기 {_minigameHits} ({_minigameHits * 100f / _trials:F2}%)");

        for (int i = 0; i < weights.Length; i++)
        {
            PartGrade grade = weights[i].Grade;
            int count = PartGrades.IsValid(grade) ? _counts[(int)grade] : 0;
            float actual = _rolled > 0 ? count * 100f / _rolled : 0f;
            float expected = weights[i].Weight > 0 ? weights[i].Weight * 100f / total : 0f;
            text.AppendLine($"{PartGrades.ToLabel(grade)} 기대 {expected:F2}% / 실제 {actual:F2}% ({count})");
        }

        Debug.Log(text.ToString());
    }
}
