using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>손 그림 종류 (2026-11-04 2D 개편: 내려찍는 주먹 / 미는 펼친 손바닥)</summary>
public enum KyodaiHandPose { Fist, Palm }

/// <summary>키 목표 자리의 기준점</summary>
public enum KyodaiHandAnchor
{
    Home,        // 손의 대기 자리 (오프셋 y도 대기 자리 기준)
    Player,      // 플레이어 x
    ArenaFar,    // 경기장에서 플레이어 반대쪽 끝
    ArenaNear,   // 경기장에서 플레이어 쪽 끝
    ArenaCenter, // 경기장 가운데
    Keep,        // 앞 키의 목표 x 그대로
    ArenaLeft,   // 경기장 왼쪽 끝 (2026-11-04, 양손 협공 — 플레이어 위치와 무관한 고정 끝)
    ArenaRight   // 경기장 오른쪽 끝
    // 새 값은 맨 뒤에만 추가할 것 — 에셋에 숫자로 저장된다
}

/// <summary>손 그림이 바라보는 쪽 (2026-11-05)</summary>
public enum KyodaiHandFacing
{
    Auto,   // 방향 키(mirrorByDirection)면 공격 방향, 아니면 손 원래 방향(씬에서 정한 Flip X 그대로)
    Inward  // 반대 손 쪽 — 왼손은 오른쪽, 오른손은 왼쪽 (양손 협공처럼 가운데로 모으는 동작)
    // 새 값은 맨 뒤에만 추가할 것 — 에셋에 숫자로 저장된다
}

/// <summary>목표까지 가는 방식</summary>
public enum KyodaiHandMove
{
    Spring, // 스프링: 무게감·출렁임. 도착 시각은 정확하지 않다
    Ease    // 이징: 키 끝 프레임에 정확히 도착 (찍는 순간처럼 타이밍이 중요한 키)
}

/// <summary>
/// 손 동작의 키 하나. "어디로 갈지·무슨 모양·판정"만 정한다 — 어떻게 가는지(떠다님·기울기·찌그러짐)는 KyodaiHand가 코드로 만든다.
/// </summary>
[Serializable]
public class KyodaiHandKey
{
    [Tooltip("이름 (인스펙터·로그용)")]
    public string label = "키";

    [Tooltip("이 키가 이어지는 프레임 수 (60fps)")]
    [Min(1)] public int frames = 30;

    [Header("목표 자리")]
    public KyodaiHandAnchor anchor = KyodaiHandAnchor.Player;

    [Tooltip("x = 기준점에서의 거리, y = 바닥에서의 높이 (Home 기준이면 대기 자리에서의 높이)")]
    public Vector2 offset = new Vector2(0f, 5f);

    [Tooltip("켜면 오프셋 x·회전·판정 위치를 공격 방향(플레이어 쪽)에 맞춰 뒤집는다 — 밀기처럼 방향이 있는 동작")]
    public bool mirrorByDirection;

    [Tooltip("그림이 바라보는 쪽. 그림 기준 방향 = 손가락·주먹이 오른쪽")]
    public KyodaiHandFacing facing = KyodaiHandFacing.Auto;

    [Tooltip("켜면 목표 x를 경기장 안으로 자른다. 화면 밖에서 들어오는 동작은 끈다")]
    public bool clampToArena = true;

    [Tooltip("이 키 동안 목표 x가 플레이어를 계속 따라간다 (스프링이 알아서 늦게 따라감)")]
    public bool trackPlayer;

    [Header("가는 방식")]
    public KyodaiHandMove move = KyodaiHandMove.Spring;

    [Tooltip("스프링 빠르기(Hz). 클수록 빨리 따라간다")]
    [Min(0.1f)] public float springFrequency = 2f;

    [Tooltip("스프링 탄성. 1 = 출렁임 없이 도착, 1보다 작을수록 지나쳤다 돌아온다")]
    [Range(0.1f, 1.5f)] public float springDamping = 0.7f;

    [Tooltip("이징 곡선 (0 → 1). 가로 = 키 진행, 세로 = 시작 자리 → 목표 자리")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("모양")]
    public KyodaiHandPose pose = KyodaiHandPose.Fist;

    [Tooltip("손 회전(도). 부드럽게 따라 돈다")]
    public float rotation;

    [Tooltip("손 크기 배율. 부드럽게 따라 커진다")]
    [Min(0.1f)] public float scale = 1f;

    [Tooltip("가로만 더 늘리는 배율 (손바닥 압착처럼 넓게 덮는 동작). 맞는 자리도 같이 넓어진다")]
    [Min(0.1f)] public float widthScale = 1f;

    [Tooltip("떠다니는 흔들림 세기 (0 = 멈춤, 1 = 대기 중과 같음). 공격 순간엔 줄인다")]
    [Range(0f, 1f)] public float floatWeight = 1f;

    [Header("판정")]
    [Tooltip("이 키 동안 손에 닿으면 플레이어가 맞는다 (패턴당 1번)")]
    public bool hitActive;

    [Tooltip("판정 상자 크기(월드)")]
    public Vector2 hitSize = new Vector2(2f, 2f);

    [Tooltip("판정 상자 중심 — 손 중심에서의 거리")]
    public Vector2 hitOffset;

    [Header("예고·충격")]
    [Tooltip("0보다 크면 이 키 동안 목표 자리 바닥에 이 너비의 예고 그림자")]
    [Min(0f)] public float telegraphWidth;

    [Tooltip("켜면 예고 그림자를 경기장 전체(가운데·전체 너비)에 깐다 — 바닥을 쓸고 지나가는 동작. telegraphWidth는 무시")]
    public bool telegraphFullArena;

    [Tooltip("키가 끝나는 순간 충격 (잠깐 멈춤·찌그러짐·화면 흔들림·먼지)")]
    public bool impactOnEnd;

    [Tooltip("충격 화면 흔들림 크기(월드)")]
    [Min(0f)] public float impactShake = 0.3f;

    [Tooltip("충격 순간 손이 멈추는 프레임 (히트스톱)")]
    [Min(0)] public int impactFreezeFrames = 4;

    [Tooltip("켜면 이 키 끝의 충격에서 충격파가 바닥을 따라 좌우로 퍼진다 (충격파 내려치기 — 점프로 넘는다)")]
    public bool spawnShockwave;
}

/// <summary>
/// 쿄다이 손 동작 하나 (2026-11-04 2D 개편). 키를 순서대로 재생하고, 마지막 키가 끝나면 손은 대기 자리로 돌아간다.
/// 패턴은 이 에셋만 바꿔 끼운다 — 패턴마다 클래스를 만들지 않는다 (CLAUDE_1 §1).
/// </summary>
[CreateAssetMenu(menuName = "NYH/Boss/Kyodai Hand Motion", fileName = "KyodaiHandMotion")]
public class KyodaiHandMotion : ScriptableObject
{
    public List<KyodaiHandKey> keys = new List<KyodaiHandKey>();

    /// <summary>전체 프레임 수 (충격 히트스톱 제외)</summary>
    public int TotalFrames
    {
        get
        {
            int sum = 0;
            foreach (var key in keys) sum += key.frames;
            return sum;
        }
    }
}
