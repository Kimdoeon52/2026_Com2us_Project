using System;
using UnityEngine;

// ============================================================================
// ActionVfx — 행동(스킬) 구간별 이펙트 프리팹 정의 (데이터)
// ----------------------------------------------------------------------------
// ActionData.vfx 목록에 넣으면 ActionVfxPlayer(로봇에 RobotView가 자동 부착)가
// ActionState 이벤트(Begin / ActiveStart / RecoveryStart / End)에 맞춰 생성·종료한다.
// 순수 연출 — 판정·데미지와 무관하고, 생성된 오브젝트의 콜라이더는 꺼진다.
// ============================================================================

/// <summary>이펙트가 살아 있는 구간. 시작 구간 진입 시 생성되고, 끝 구간이 끝날 때 종료된다</summary>
public enum ActionVfxPhase
{
    Startup,          // 선딜
    StartupToActive,  // 선딜 ~ 활성
    Active,           // 활성
    ActiveToRecovery, // 활성 ~ 후딜
    Recovery,         // 후딜
    Whole,            // 전체 (선딜 ~ 후딜)
}

/// <summary>구간이 끝났을 때 이펙트를 어떻게 정리할지</summary>
public enum ActionVfxEndMode
{
    StopEmitting, // 파티클 방출만 멈추고 남은 입자가 사라지면 제거 (파티클 없으면 즉시 제거)
    Destroy,      // 즉시 제거
    KeepPlaying,  // 건드리지 않음 — 파티클/애니메이션 길이만큼 재생 후 자동 제거
}

/// <summary>구간 안에서 몇 번 생성할지</summary>
public enum ActionVfxRepeat
{
    Once,               // 구간 시작에 1번
    FixedInterval,      // repeatIntervalFrames마다 다시 생성
    ProjectileInterval, // ActionData.ProjectileIntervalFrames마다 (투사체 발사와 같은 틱 — 연사 머즐 플래시)
    HitInterval,        // ActionData.HitIntervalFrames마다 (다단히트와 같은 간격)
}

[Serializable]
public class ActionVfxEntry
{
    [Tooltip("생성할 이펙트 프리팹")]
    public GameObject prefab;

    [Tooltip("이펙트가 유지될 구간")]
    public ActionVfxPhase phase = ActionVfxPhase.Active;

    [Tooltip("구간 안에서 반복 생성할지. 반복이면 구간 시작 틱에 1번, 그 뒤 간격마다 1번씩 새로 만든다")]
    public ActionVfxRepeat repeat = ActionVfxRepeat.Once;

    [Tooltip("repeat = FixedInterval일 때 생성 간격(틱)")]
    [Min(1)] public int repeatIntervalFrames = 10;

    [Tooltip("구간이 끝났을 때 정리 방식")]
    public ActionVfxEndMode endMode = ActionVfxEndMode.StopEmitting;

    [Tooltip("캐릭터 피벗 기준 위치(오른쪽을 볼 때 기준). 좌우 반전은 자동")]
    public Vector2 offset;

    [Tooltip("Z축 회전 각도(도). 0이면 회전 없음. 오른쪽을 볼 때 기준 — 좌우 반전 시 부호가 자동으로 뒤집힌다")]
    public float rotation;

    [Tooltip("켜면 로봇을 따라 움직인다 (꺼두면 생성 위치에 남음)")]
    public bool followOwner = true;

    [Tooltip("켜면 왼쪽을 볼 때 좌우를 뒤집는다")]
    public bool flipWithFacing = true;

    [Min(0.01f)] public float scale = 1f;

    [Tooltip("파티클 재생 속도 배율. 0.5면 절반 속도로 2배 길게 재생된다 (플립북 애니메이션도 같이 느려짐)")]
    [Min(0.01f)] public float playbackSpeed = 1f;

    [Tooltip("로봇 그림보다 몇 칸 앞에 그릴지 (sortingOrder 가산)")]
    public int sortingOrderOffset = 1;

    // 구간 범위 — Startup=0, Active=1, Recovery=2
    public int StartIndex => phase switch
    {
        ActionVfxPhase.Active or ActionVfxPhase.ActiveToRecovery => 1,
        ActionVfxPhase.Recovery => 2,
        _ => 0,
    };

    /// <summary>반복 생성 간격(틱). 0이면 반복 안 함</summary>
    public int ResolveInterval(ActionData action) => repeat switch
    {
        ActionVfxRepeat.FixedInterval => Math.Max(1, repeatIntervalFrames),
        ActionVfxRepeat.ProjectileInterval => action != null ? Math.Max(1, action.ProjectileIntervalFrames) : 0,
        ActionVfxRepeat.HitInterval => action != null ? Math.Max(0, action.HitIntervalFrames) : 0,
        _ => 0,
    };

    public int EndIndex => phase switch
    {
        ActionVfxPhase.Startup => 0,
        ActionVfxPhase.StartupToActive or ActionVfxPhase.Active => 1,
        _ => 2,
    };
}
