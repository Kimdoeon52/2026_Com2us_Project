using System;
using UnityEngine;

// 장착 코어 상태. 세이브 대상
[Serializable]
public class CoreSaveData
{
    [SerializeField] private string _id;
    [SerializeField] private int _level = 1;
    [SerializeField] private int _exp;

    // 비었으면 null
    public string Id => string.IsNullOrEmpty(_id) ? null : _id;
    public int Level => Mathf.Max(1, _level);
    public int Exp => _exp;

    // 다른 코어로 바꾸면 레벨과 경험치는 처음부터
    public void Set(string id)
    {
        _id = id;
        _level = 1;
        _exp = 0;
    }

    public void Clear()
    {
        Set(null);
    }
}
