using System;
using System.Collections.Generic;
using UnityEngine;

public enum HudMissionKind
{
    Deadline,
    Omen,
    Todo
}

// 미션 패널 한 줄
[Serializable]
public struct HudMission
{
    [Tooltip("태그")]
    public string Tag;

    [Tooltip("내용")]
    public string Text;

    [Tooltip("종류")]
    public HudMissionKind Kind;
}

// 필드 HUD 중 담당 시스템이 아직 없는 값
public interface IFieldHudSource
{
    // 날씨 표기
    string Weather { get; }

    // 오늘의 징후
    string Omen { get; }

    // 이동 소요 분
    int TravelMinutes { get; }

    int Scrap { get; }
    int Refined { get; }

    IReadOnlyList<HudMission> Missions { get; }
}
