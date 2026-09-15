using System;
using UnityEngine;

// 인벤토리에 배치된 파츠 1개. 세이브 대상
[Serializable]
public class PartsInstance
{
    public string DefinitionId;
    public Vector2Int Origin;

    public PartsInstance() { }

    public PartsInstance(string definitionId, Vector2Int origin)
    {
        DefinitionId = definitionId;
        Origin = origin;
    }
}
