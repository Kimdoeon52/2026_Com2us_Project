using System;
using UnityEngine;

// 잔량 구간 하나. 배열 길이가 곧 구간 개수다
[Serializable]
public struct ScrapPileStage
{
    [Tooltip("이 구간이 적용되는 최소 잔량 비율")]
    [Range(0f, 1f)]
    public float MinRemaining01;

    [Tooltip("이 구간에서만 켜둘 비주얼 오브젝트")]
    public GameObject Visual;
}
