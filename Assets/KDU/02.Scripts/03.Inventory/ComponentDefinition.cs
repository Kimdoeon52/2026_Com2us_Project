using System;
using UnityEngine;

// 부품 1종 정의. 카탈로그 SO 안의 원소라 에셋 파일을 따로 만들지 않는다
[Serializable]
public class ComponentDefinition
{
    [Tooltip("식별자")]
    [SerializeField] private string _id;

    [Tooltip("표시명")]
    [SerializeField] private string _displayName;

    [Tooltip("등급")]
    [SerializeField] private PartGrade _grade;

    [Tooltip("최대 소지 개수. 0이면 무제한")]
    [Min(0)]
    [SerializeField] private int _maxCount;

    public string Id => _id;
    public string DisplayName => _displayName;
    public PartGrade Grade => _grade;
    public int MaxCount => _maxCount;

    // 현재 수량에서 더 받을 수 있는 양
    public int RemainingCapacity(int current)
    {
        if (_maxCount <= 0)
            return int.MaxValue;

        return Mathf.Max(0, _maxCount - current);
    }
}
