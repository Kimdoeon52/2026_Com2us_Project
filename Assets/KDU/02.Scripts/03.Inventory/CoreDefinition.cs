using System;
using UnityEngine;

// 코어 레벨 1단계 수치
[Serializable]
public struct CoreLevel
{
    [Tooltip("최대 체력")]
    [Min(1)]
    public int Hp;

    [Tooltip("방어력")]
    [Min(0)]
    public int Defense;

    [Tooltip("다음 레벨까지 경험치. 최대 레벨은 0")]
    [Min(0)]
    public int ExpToNext;

    [Tooltip("코어 복구비")]
    [Min(0)]
    public int RepairCost;
}

// 코어 정의. 레벨별 수치는 배열 순서가 곧 레벨(0번 = Lv1)
[CreateAssetMenu(menuName = "KDU/인벤토리/코어 정의", fileName = "CoreDefinition")]
public class CoreDefinition : ScriptableObject
{
    [Tooltip("식별자")]
    [SerializeField] private string _id;

    [Tooltip("표시명")]
    [SerializeField] private string _displayName;

    [Tooltip("설명")]
    [SerializeField] private string _description;

    [Tooltip("코어 이미지")]
    [SerializeField] private Sprite _sprite;

    [Tooltip("레벨별 수치")]
    [SerializeField] private CoreLevel[] _levels = new CoreLevel[1];

    public string Id => _id;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Sprite => _sprite;
    public int MaxLevel => _levels != null ? _levels.Length : 0;

    // 범위 밖 레벨은 양 끝으로 맞춘다
    public CoreLevel GetLevel(int level)
    {
        if (_levels == null || _levels.Length == 0)
            return default;

        return _levels[Mathf.Clamp(level, 1, _levels.Length) - 1];
    }

    public string GetTooltip(int level)
    {
        CoreLevel stats = GetLevel(level);
        string exp = level >= MaxLevel ? "MAX" : stats.ExpToNext.ToString("N0");

        return $"이름: {_displayName}\n" +
            "------------------------------\n" +
            $"설명: {_description}\n" +
            $"레벨: {level} / {MaxLevel}\n" +
            $"체력: {stats.Hp:N0}\n" +
            $"방어력: {stats.Defense:N0}\n" +
            $"다음 레벨까지: {exp}\n" +
            $"복구비: {stats.RepairCost:N0}G";
    }
}
